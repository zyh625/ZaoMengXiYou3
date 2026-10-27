using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

//所有人物共用的状态入口；表现按需从公共预制体池取得，不要求角色预先挂冰块或毒雾子物体。
[DefaultExecutionOrder(10000)]
public class ActorStatus : MonoBehaviour
{
    [SerializeField] private StatusEffectView frozenPrefab, poisonPrefab;//允许单独覆盖表现；留空时使用Resources/Status中的公共预制体
    [SerializeField, Min(0f)] private float frozenDuration = 2f;//没有新的攻击时先保持完全冰冻，再进入融化
    [SerializeField, Min(0.01f)] private float meltFrameInterval = 1f;//自然融化每隔多久推进一帧
    [SerializeField, Min(1f)] private float frozenDamageMultiplier = 1.5f;//只有真正破冰的那一次攻击享受倍率，毒素不破冰
    private enum IcePhase { None, Frozen, Melting }
    private IcePhase phase;//操作封锁与位移封锁分别读取，不强迫玩家和敌人共用同一状态枚举
    private readonly Dictionary<Object, float> freezeSources = new();//同一块冰记录各来源；取消一个技能不误清其他来源
    private readonly HashSet<Object> motionLockSources = new();//冰冻与原有眩晕、携带共用一份位置约束，避免交叠后永久锁住
    private readonly List<Object> removedLocks = new();//复用失效来源清理列表
    private Rigidbody2D body;//只锁位置与速度，不接管其他技能正在维护的重力
    private Health health;
    private SpriteRenderer bodyRenderer;//始终指向角色本体，不能取到新增技能特效的渲染器
    private RigidbodyConstraints2D savedConstraints;//最后一个位置锁结束后恢复真正的原约束
    private bool constraintsSaved;
    private Vector2 frozenPosition;//冰冻及融化期间始终保持施加状态时的世界位置
    private float nextMeltAt;//计时融化不会因为主动攻击推进一帧而重新从头计时
    private int meltFrame;
    private StatusEffectView iceView, poisonView;//当前状态的表现；破裂后交给表现自身播放完并归还池
    private static StatusEffectView defaultFrozen, defaultPoison;//资源仅首次需要时加载，新增角色不必重复手动配置
    public bool BlocksActions => phase == IcePhase.Frozen;
    public bool BlocksMovement => phase != IcePhase.None;
    public bool IsFrozen => phase != IcePhase.None;
    public bool IsMelting => phase == IcePhase.Melting;
    public event Action Changed;//只在冰冻阶段改变时通知动作系统，不每帧重复重置动画

    private void Awake() => ResolveReferences();

    private void ResolveReferences()//可在状态比控制器更早Awake时安全补齐本体引用
    {
        if (health == null) health = GetComponent<Health>();
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (bodyRenderer != null) return;
        PlayerController player = GetComponent<PlayerController>();
        if (player != null && player.BodySpriteManager != null)
            bodyRenderer = player.BodySpriteManager.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (health == null) return;
        health.DebuffsCleared += ClearStatusEffects;
        health.Died += OnDied;
    }

    public bool ApplyFreeze(Object source, float duration = -1f)//技能和以后新增的攻击效果共用此入口
    {
        ResolveReferences();
        if (source == null || !isActiveAndEnabled || health == null || health.Dead || health.IsInvincible || health.Boost) return false;
        EnemyMovement enemy = GetComponent<EnemyMovement>();
        if (enemy != null && !enemy.CanReceiveHit) return false;
        float hold = duration >= 0f ? duration : frozenDuration;
        float expiresAt = Time.time + Mathf.Max(0f, hold);
        if (freezeSources.TryGetValue(source, out float previous)) expiresAt = Mathf.Max(previous, expiresAt);
        freezeSources[source] = expiresAt;
        bool changed = phase != IcePhase.Frozen;
        if (phase == IcePhase.None) frozenPosition = body != null ? body.position : (Vector2)transform.position;
        phase = IcePhase.Frozen;
        meltFrame = 0;
        AcquireMotionLock(this);
        EnforcePosition();
        ShowIceLoop();
        if (changed) Changed?.Invoke();
        return true;
    }

    public bool HasFreeze(Object source)//融化仍属于同一块冰，碎裂帧可以打碎它
        => source != null && IsFrozen && freezeSources.ContainsKey(source);

    public void CancelFreeze(Object source)//技能提前中断只取消自己的来源，不额外造成破裂伤害
    {
        if (source == null || !freezeSources.Remove(source)) return;
        if (freezeSources.Count == 0) EndFreeze(false);
    }

    public float ConsumeFreezeBreak(DamageInfo damage)//Health在扣血前调用：先解冻，再计算这一次命中的额外倍率
    {
        if (!IsFrozen || damage.IsPeriodic || damage.Amount <= 0f) return 1f;
        float multiplier = Mathf.Max(1f, frozenDamageMultiplier);
        EndFreeze(true);
        return multiplier;
    }

    public void NotifyAttackStarted()//只在实际开始一次攻击时调用，按键失败或重复检查同一帧不会加速融化
    {
        if (IsMelting) AdvanceMelt();
    }

    private void Update()
    {
        if (health == null || health.Dead) return;
        if (phase == IcePhase.Frozen)
        {
            bool holding = false;
            foreach (float expiresAt in freezeSources.Values)
                if (Time.time < expiresAt) { holding = true; break; }
            if (!holding) BeginMelting();
        }
        while (IsMelting && Time.time >= nextMeltAt)
        {
            nextMeltAt += Mathf.Max(0.01f, meltFrameInterval);
            AdvanceMelt();
        }
        SyncPoisonVisual();
    }

    private void BeginMelting()//仅放开攻击与动画，原有位置锁继续保留到冰完全消失
    {
        phase = IcePhase.Melting;
        meltFrame = 0;
        nextMeltAt = Time.time + Mathf.Max(0.01f, meltFrameInterval);
        if (iceView != null) iceView.ShowMeltingFrame(meltFrame);
        Changed?.Invoke();
    }

    private void AdvanceMelt()
    {
        meltFrame++;
        int count = iceView != null ? iceView.MeltingFrameCount : 6;//没有配置表现时状态规则仍可正常结束
        if (meltFrame >= Mathf.Max(1, count)) EndFreeze(false);
        else if (iceView != null) iceView.ShowMeltingFrame(meltFrame);
    }

    private void EndFreeze(bool breaking)
    {
        if (!IsFrozen && freezeSources.Count == 0) return;
        phase = IcePhase.None;
        freezeSources.Clear();
        ReleaseMotionLock(this);
        StatusEffectView previous = iceView;
        iceView = null;
        if (previous != null)
        {
            if (breaking) previous.PlayBreaking();
            else previous.Release();
        }
        Changed?.Invoke();
    }

    private void ShowIceLoop()//反复刷新冰冻来源不会每帧重播同一个循环
    {
        if (iceView == null)
        {
            StatusEffectView prefab = frozenPrefab != null ? frozenPrefab : GetDefault(true);
            iceView = StatusEffectView.Rent(prefab, transform, bodyRenderer);
        }
        if (iceView != null) iceView.PlayLoop();
    }

    private void SyncPoisonVisual()//毒层与计时仍以Health为唯一数据源，沙僧已有毒素自然接入相同表现
    {
        if (health.PoisonStacks <= 0)
        {
            if (poisonView != null) poisonView.Release();
            poisonView = null;
            return;
        }
        if (poisonView != null) return;
        StatusEffectView prefab = poisonPrefab != null ? poisonPrefab : GetDefault(false);
        poisonView = StatusEffectView.Rent(prefab, transform, bodyRenderer);
        if (poisonView != null) poisonView.PlayLoop();
    }

    private static StatusEffectView GetDefault(bool frozen)//公共Resources回退让新角色只需要Health组件即可显示状态
    {
        StatusEffectView prefab = frozen ? defaultFrozen : defaultPoison;
        if (prefab != null) return prefab;
        GameObject asset = Resources.Load<GameObject>(frozen ? "Status/FrozenStatus" : "Status/PoisonStatus");
        prefab = asset != null ? asset.GetComponent<StatusEffectView>() : null;
        if (frozen) defaultFrozen = prefab;
        else defaultPoison = prefab;
        return prefab;
    }

    public void AcquireMotionLock(Object source)//与原有眩晕和携带共享约束保存，只有第一个来源记录原约束
    {
        if (source == null) return;
        ResolveReferences();
        RemoveDeadLocks();
        if (body == null || !motionLockSources.Add(source)) return;
        if (!constraintsSaved)
        {
            savedConstraints = body.constraints;
            constraintsSaved = true;
        }
        body.constraints = savedConstraints | RigidbodyConstraints2D.FreezePosition;
        body.linearVelocity = Vector2.zero;
    }

    public void ReleaseMotionLock(Object source)//解除自己的状态不会放开仍被其他来源冻结的人物
    {
        if (ReferenceEquals(source, null) || !motionLockSources.Remove(source)) return;
        RestoreMotionConstraints();
    }

    private void RestoreMotionConstraints()
    {
        if (motionLockSources.Count != 0 || !constraintsSaved) return;
        if (body != null) body.constraints = savedConstraints;
        constraintsSaved = false;
    }

    private void RemoveDeadLocks()//被销毁的来源不能留下永久位置约束
    {
        removedLocks.Clear();
        foreach (Object source in motionLockSources)
            if (source == null) removedLocks.Add(source);
        foreach (Object source in removedLocks) motionLockSources.Remove(source);
        RestoreMotionConstraints();
    }

    public void EnforcePosition()//除了刚体速度也钳制直接body.position位移，烈焰闪和冲刺仍能播放但不能挪动
    {
        if (!BlocksMovement) return;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = frozenPosition;
        }
        Vector3 position = transform.position;
        position.x = frozenPosition.x;
        position.y = frozenPosition.y;
        transform.position = position;
    }

    private void FixedUpdate()
    {
        RemoveDeadLocks();
        EnforcePosition();//在各个Controller、技能和吸附都更新之后归回锚点，再进入物理解算
    }

    private void LateUpdate() => EnforcePosition();//也防止Update阶段的直接Transform位移

    public void ClearDebuffs()//对外的驱散入口统一交给Health，不能只关掉毒雾却留下实际毒层
    {
        if (health != null) health.ClearDebuffs();
        else ClearStatusEffects();
    }

    private void ClearStatusEffects()//驱散不算受击，不产生碎裂伤害
    {
        EndFreeze(false);
        if (poisonView != null) poisonView.Release();
        poisonView = null;
    }

    private void OnDied(DamageInfo damage) => ClearStatusEffects();

    private void OnDisable()//换人、死亡回收、场景关闭都不能把状态和位置锁带进下一次生命
    {
        if (health != null)
        {
            health.DebuffsCleared -= ClearStatusEffects;
            health.Died -= OnDied;
        }
        ClearStatusEffects();
        motionLockSources.Clear();
        RestoreMotionConstraints();
    }
}

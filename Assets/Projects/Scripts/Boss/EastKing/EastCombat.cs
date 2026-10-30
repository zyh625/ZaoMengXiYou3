using System;
using UnityEngine;

public class EastCombat : BossCombat
{
    [Header("音符攻击（白、红、蓝、绿）")]
    [SerializeField] private EastNoteManager[] notePrefabs = new EastNoteManager[4];//独立音符模板，由场景对象池创建发射实例
    [SerializeField] private float[] noteCooldowns = { 2f, 6f, 10f, 8f };//各招式从成功起手时分别计算冷却，顺序与音符模板一致
    [SerializeField, Min(0f)] private float minimumAttackInterval = 0.75f;//一轮出招完成或被打断后，至少休息此时长再开始下一轮
    [SerializeField, Min(0f)] private float firstAttackDelay = 1f;//刚现身时给玩家准备时间，之后仅由各自冷却和公共间隔决定
    [SerializeField, Min(0)] private int shootFrame = 9;//弹琴动作第十帧发射，下标从零开始

    [Header("急弦")]
    [SerializeField, Min(0.01f)] private float stringEnergyFull = 100f;//急弦能量上限，满值后保持至强化结束
    [SerializeField, Min(0f)] private float stringEnergyOnHit = 5f;//白、红音符实际扣除玩家血量时增加
    [SerializeField, Min(0f)] private float stringEnergyOnMiss = 8f;//白、红音符落空、被免伤挡住时增加；蓝、绿音符不参与
    [SerializeField, Min(0f)] private float stringEnergyOnDamaged = 10f;//每次非周期伤害实际扣血后增加
    [SerializeField, Min(0.01f)] private float rapidStringsDuration = 8f;//急弦持续时间，使用游戏时间并遵守暂停
    [SerializeField, Range(0f, 1f)] private float rapidStringsCooldownRatio = 0.5f;//强化期间四种音符冷却相对原值的倍率
    [SerializeField] private float stringEnergy;//当前能量，死亡时保留供状态槽定格

    private readonly float[] lastNoteTimes = new float[4];//只记录真正开始的出招，查询准备状态不会消耗冷却
    private Health health;//统一检查死亡、受击与冰冻，禁止被打断后补发音符
    private EastMovement movement;//普通眩晕与携带期间同样禁止新起手或继续发射
    private bool castActive, noteReleased;//本轮动作和已发射标记分开，重复检查动作帧只发射一次
    private int currentNote = -1, nextNote;//依次轮询已就绪招式，防止普通攻击一直抢占其他音符
    private float nextAttackTime, lastAttackStartedAt;//公共收招间隔与本次起手时间独立计算
    private float rapidStringsUntil;//急弦到期时刻，不依赖当前动作是否受击或被控制
    private bool rapidStringsActive, deadFrozen;//死亡后停止强化计时，不把特殊槽清零

    public bool IsCasting => castActive;
    public bool NoteReleased => noteReleased;
    public int CurrentNoteIndex => currentNote;
    public float LastAttackStartedAt => lastAttackStartedAt;
    public float StringEnergy => Mathf.Clamp(stringEnergy, 0f, MaxStringEnergy);
    public float MaxStringEnergy => Mathf.Max(0.01f, stringEnergyFull);
    public bool RapidStringsActive => rapidStringsActive && health != null && !health.Dead;
    public float NoteCooldownMultiplier => RapidStringsActive ? Mathf.Clamp01(rapidStringsCooldownRatio) : 1f;//四招共用动态倍率，结束后立即恢复原配置
    public event Action StringEnergyChanged;//特殊状态条只订阅实际变化，不逐帧查找Boss

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveReferences();
        castActive = noteReleased = false;
        currentNote = -1;
        nextNote = 0;
        nextAttackTime = Time.time + Mathf.Max(0f, firstAttackDelay);
        lastAttackStartedAt = float.NegativeInfinity;
        deadFrozen = rapidStringsActive = false;
        stringEnergy = rapidStringsUntil = 0f;
        for (int i = 0; i < lastNoteTimes.Length; i++) lastNoteTimes[i] = float.NegativeInfinity;
        if (health != null)
        {
            health.Died += OnDied;
            health.DamageReceived += OnDamageReceived;
        }
        StringEnergyChanged?.Invoke();
    }

    private void ResolveReferences()//兼容对象池启用顺序，不把当前目标或场景对象写回预制体
    {
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<EastMovement>();
    }

    private bool CanAct()//只管演员自身是否可出招，不设置距离或攻击检测箱要求
        => isActiveAndEnabled && health != null && !health.Dead
            && (!health.Attacked || health.Boost)
            && !(health.Status != null && health.Status.BlocksActions)
            && (movement == null || !movement.IsStunned && !movement.IsCarried);

    public int GetReadyAttack()//返回下一种已冷却的有效音符，所有招式都没有距离限制
    {
        if (castActive || !CanAct() || Time.time < nextAttackTime) return -1;
        for (int step = 0; step < lastNoteTimes.Length; step++)
        {
            int index = (nextNote + step) % lastNoteTimes.Length;
            if (notePrefabs == null || index >= notePrefabs.Length || notePrefabs[index] == null) continue;
            if (Time.time - lastNoteTimes[index] >= CooldownFor(index)) return index;
        }
        return -1;
    }

    private float CooldownFor(int index)//错误或缺失的冷却配置不会造成越界或永久卡住
        => noteCooldowns != null && index < noteCooldowns.Length && !float.IsNaN(noteCooldowns[index])
            ? Mathf.Max(0f, noteCooldowns[index]) * NoteCooldownMultiplier : 0f;

    public bool BeginAttack(int index, bool right)//控制器真正切到弹琴动作时才消耗本招冷却
    {
        if (index < 0 || index >= lastNoteTimes.Length || castActive || !CanAct()
            || Time.time < nextAttackTime || notePrefabs == null || index >= notePrefabs.Length
            || notePrefabs[index] == null || Time.time - lastNoteTimes[index] < CooldownFor(index)) return false;
        currentNote = index;
        nextNote = (index + 1) % lastNoteTimes.Length;
        castActive = true;
        noteReleased = false;
        AttackFaceRight = right;
        lastNoteTimes[index] = lastAttackStartedAt = Time.time;
        health.Status?.NotifyAttackStarted();
        return true;
    }

    public void CheckNote(int frame)//物理更新和渲染更新可共用此入口，跨过第十帧也不会漏发或重复发射
    {
        if (!castActive || noteReleased) return;
        if (!CanAct())
        {
            CancelAttack();
            return;
        }
        if (frame < Mathf.Max(0, shootFrame)) return;
        noteReleased = true;
        EastNoteManager prefab = notePrefabs != null && currentNote >= 0 && currentNote < notePrefabs.Length
            ? notePrefabs[currentNote] : null;
        if (prefab != null)
            EastNotePool.GetOrCreate().Fire(prefab, this, transform.position, AttackFaceRight);
    }

    public void CancelAttack()//不回收已经飞出的独立音符，只丢弃本轮尚未执行的发射请求
    {
        if (!castActive) return;
        castActive = false;
        currentNote = -1;
        nextAttackTime = Time.time + Mathf.Max(0f, minimumAttackInterval);
    }

    public void ReportNoteResult(bool hit, int lifeVersion)//音符离手后独立结算，同一生命的每枚白、红音符只报告一次
    {
        if (health == null || health.LifeVersion != lifeVersion || !BattleRun.IsRunning) return;
        AddStringEnergy(hit ? stringEnergyOnHit : stringEnergyOnMiss);
    }

    private void OnDamageReceived(DamageInfo damage, float actualDamage)//持续中毒不按每帧增加能量，与巨灵神怒气规则一致
    {
        if (actualDamage > 0f && !damage.IsPeriodic) AddStringEnergy(stringEnergyOnDamaged);
    }

    private void AddStringEnergy(float amount)//强化期间锁满，不积压能量；控制状态本身不阻止积累
    {
        if (!isActiveAndEnabled || deadFrozen || health == null || health.Dead || rapidStringsActive
            || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        stringEnergy = Mathf.Min(MaxStringEnergy, stringEnergy + amount);
        if (stringEnergy >= MaxStringEnergy)
        {
            rapidStringsActive = true;
            rapidStringsUntil = Time.time + Mathf.Max(0.01f, rapidStringsDuration);
        }
        StringEnergyChanged?.Invoke();
    }

    private void Update()//只结束急弦本身，不重置招式时间戳或打断已经起手的弹琴动作
    {
        if (deadFrozen || health == null || health.Dead || !rapidStringsActive || Time.time < rapidStringsUntil) return;
        rapidStringsActive = false;
        stringEnergy = 0f;
        StringEnergyChanged?.Invoke();
    }

    private void OnDied(DamageInfo damage)//死亡不允许补发音符，也不在死亡动画期间把特殊状态槽归零
    {
        deadFrozen = true;
        rapidStringsActive = false;
        CancelAttack();
    }

    private void OnDisable()//对象池禁用时退订，下一次出现重新计算首轮准备时间与冷却
    {
        if (health != null)
        {
            health.Died -= OnDied;
            health.DamageReceived -= OnDamageReceived;
        }
        CancelAttack();
        noteReleased = false;
        rapidStringsActive = false;
    }
}

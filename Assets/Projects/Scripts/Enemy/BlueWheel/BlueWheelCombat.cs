using System.Collections.Generic;
using UnityEngine;

public class BlueWheelCombat : EnemyCombat
{
    public enum WheelAction { None, Throw, Return, HideWindup }
    [Header("回旋镖")]
    [SerializeField] private BlueBoomearng wheelPrefab;//由独立场景池管理，不依附持有者启用状态。
    [SerializeField, Min(0f)] private float throwCooldown = 8f;//单双轮共用冷却，从成功起手计算。
    [SerializeField, Range(0f, 1f)] private float doubleChance = 0.25f;//每次真正起手决定单双轮。
    [SerializeField, Min(0f)] private float throwRange = 12f;//未填检测器时使用水平距离。
    [SerializeField] private EnemyCheckAttack throwCheck;//可选范围检测器，不强求额外场景对象。
    [SerializeField] private Vector2 wheelOffset = new(2.5f, 0.5f);//普通投掷相对本体的生成/收回点。
    [SerializeField, Min(0f)] private float doubleSpacing = 3f;//双轮沿投掷方向的间隔。
    [SerializeField, Min(0)] private int releaseFrame = 7, catchFrame = 7;//投出和收回动作各第八帧生效。
    [Header("隐身")]
    [SerializeField, Min(0f)] private float hideCooldown = 16f;//破隐、退出或前摇打断后重新计时。
    [SerializeField, Min(0)] private int hideStartFrame = 12;//前十二帧前摇，第十三帧开始隐身。
    [SerializeField, Min(0.05f)] private float shadowInterval = 1.5f;//后四帧循环和显影周期。
    [SerializeField, Range(0f, 1f)] private float shadowAlpha = 0.3f;//后四帧的影子透明度，空白帧天然形成显影间隔。
    private Health health;
    private BlueMovement movement;
    private SpriteRenderer visual;//仅控制本体，不隐藏血条和异常状态特效。
    private float originalAlpha = 1f, lastThrowTime, lastHideTime;
    private bool wheelsReleased, wheelsCaught;
    private readonly List<BlueBoomearng> liveWheels = new();//当前生命发出的所有轮，允许短冷却并发。
    private readonly List<BlueBoomearng> currentWheels = new();//仅本轮普通投掷按收回帧消失。
    public WheelAction Action { get; private set; }
    public bool IsHidden { get; private set; }
    public bool DoubleThrow { get; private set; }
    public int HideStartFrame => Mathf.Max(0, hideStartFrame);
    public int CatchFrame => Mathf.Max(0, catchFrame);
    public float ShadowInterval => Mathf.Max(0.05f, shadowInterval);
    public Vector2 CatchPosition => (Vector2)transform.position + (IsHidden ? Vector2.zero
        : new Vector2((AttackFaceRight ? 1f : -1f) * Mathf.Abs(wheelOffset.x), wheelOffset.y));
    private float CurrentThrowCooldown => Mathf.Max(0f, throwCooldown) * (Angry ? AngerCooldownMultiplier : 1f);
    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
        if (visual != null) originalAlpha = visual.color.a;
    }
    private void ResolveReferences()//预制体不缓存场景玩家或池实例。
    {
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<BlueMovement>();
        if (visual == null) visual = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>(true);
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveReferences();
        Action = WheelAction.None;
        IsHidden = wheelsReleased = wheelsCaught = false;
        lastThrowTime = lastHideTime = Time.time;
        liveWheels.Clear(); currentWheels.Clear();
        movement?.ResetHiddenPatrol();
        RestoreAlpha();
        if (health != null)
        {
            health.DamageReceived += OnDamageReceived;
            health.Died += OnDied;
        }
    }
    public bool CanAct => BattleRun.IsRunning && isActiveAndEnabled && health != null && !health.Dead
        && (!health.Attacked || health.Boost) && !(health.Status != null && health.Status.BlocksActions)
        && movement != null && !movement.IsExternallyControlled;
    public bool CanHide => CanAct && !IsHidden && Action == WheelAction.None
        && Time.time - lastHideTime >= Mathf.Max(0f, hideCooldown);
    public bool CanThrow => CanAct && Action == WheelAction.None && wheelPrefab != null
        && Time.time - lastThrowTime >= CurrentThrowCooldown
        && (throwCheck != null ? throwCheck.PlayerIn : Mathf.Abs(movement.DeltaX) <= Mathf.Max(0f, throwRange));
    public bool BeginHide()//控制器确认帧数据有效后才提交。
    {
        if (!CanHide) return false;
        Action = WheelAction.HideWindup;
        lastHideTime = Time.time;
        health.Status?.NotifyAttackStarted();
        return true;
    }
    public bool BeginThrow(bool right)
    {
        if (IsHidden || !CanThrow) return false;
        Action = WheelAction.Throw;
        BeginThrowData(right);
        return true;
    }
    private void BeginThrowData(bool right)
    {
        lastThrowTime = Time.time;
        AttackFaceRight = right;
        DoubleThrow = Random.value < Mathf.Clamp01(doubleChance);
        wheelsReleased = wheelsCaught = false;
        currentWheels.Clear();
        health.Status?.NotifyAttackStarted();
    }
    public void CheckReleaseFrame(int frame)//物理和渲染更新共用一次性门闩，跨帧不漏发。
    {
        if (Action == WheelAction.Throw && !wheelsReleased && CanAct && frame >= Mathf.Max(0, releaseFrame))
            EmitWheels(false);
    }
    private void EmitWheels(bool hidden)
    {
        wheelsReleased = true;
        liveWheels.RemoveAll(wheel => wheel == null || !wheel.BelongsTo(this));
        int count = DoubleThrow ? 2 : 1;
        BlueWheelPool pool = BlueWheelPool.GetOrCreate();
        for (int i = 0; i < count; i++)
        {
            Vector2 position = hidden ? (Vector2)transform.position
                : (Vector2)transform.position + new Vector2((AttackFaceRight ? 1f : -1f)
                    * (Mathf.Abs(wheelOffset.x) + Mathf.Max(0f, doubleSpacing) * i), wheelOffset.y);
            //隐身双轮都从本体产生，第二轮延后一个间距的飞行时间，避免重叠。
            float delay = hidden && i > 0 ? Mathf.Max(0f, doubleSpacing) / Mathf.Max(0.01f, wheelPrefab.FlySpeed) : 0f;
            BlueBoomearng wheel = pool.Fire(wheelPrefab, this, position, AttackFaceRight, hidden, delay);
            if (wheel == null) continue;
            liveWheels.Add(wheel);
            if (!hidden) currentWheels.Add(wheel);
        }
    }
    public void BeginReturn(float animationFps)//安排在收回第八帧前飞回，避免从远处突然消失。
    {
        if (Action != WheelAction.Throw) return;
        if (!wheelsReleased) CheckReleaseFrame(int.MaxValue);
        Action = WheelAction.Return;
        float duration = Mathf.Max(Time.fixedDeltaTime, CatchFrame / Mathf.Max(1f, animationFps));
        foreach (BlueBoomearng wheel in currentWheels)
            if (wheel != null && wheel.BelongsTo(this)) wheel.BeginReturn(duration, false);//池内旧引用不能让另一位敌人刚借出的轮提前折返。
    }
    public void CheckCatchFrame(int frame)
    {
        if (Action != WheelAction.Return || wheelsCaught || frame < CatchFrame) return;
        wheelsCaught = true;
        foreach (BlueBoomearng wheel in currentWheels)
            if (wheel != null) wheel.Catch(this);
        currentWheels.Clear();
    }
    public void CompleteReturn()
    {
        if (Action != WheelAction.Return) return;
        CheckCatchFrame(int.MaxValue);
        Action = WheelAction.None;
    }
    public void EnterHidden()//不添加无敌、免控或固定结束时间。
    {
        if (Action != WheelAction.HideWindup || !CanAct) return;
        Action = WheelAction.None;
        IsHidden = true;
        movement.ResetHiddenPatrol();
    }
    public void TryHiddenThrow()//不改变本体动画，直接提交独立飞轮。
    {
        if (!IsHidden || !CanThrow) return;
        BeginThrowData(movement.DeltaX > 0f);
        EmitWheels(true);
    }
    public void InterruptAction()//中断后旧飞轮继续向当前方向飞出场地。
    {
        bool hiding = IsHidden || Action == WheelAction.HideWindup;
        foreach (BlueBoomearng wheel in liveWheels)
            if (wheel != null && wheel.IsFlying) wheel.Abandon(this);
        liveWheels.Clear(); currentWheels.Clear();
        Action = WheelAction.None;
        IsHidden = false;
        if (hiding) lastThrowTime = lastHideTime = Time.time;
        movement?.ResetHiddenPatrol();
        RestoreAlpha();
    }
    private void OnDamageReceived(DamageInfo damage, float actualDamage)
    {
        if (actualDamage > 0f && !damage.IsPeriodic) InterruptAction();//毒素周期扣血不作为一次新的命中破隐。
    }
    private void OnDied(DamageInfo damage) => InterruptAction();
    private void Update()
    {
        if (!IsHidden || visual == null) return;
        Color color = visual.color;
        color.a = originalAlpha * Mathf.Clamp01(shadowAlpha);//由精灵表本身的透明帧决定何时显影，避免两个计时窗口错开。
        visual.color = color;
    }
    private void RestoreAlpha()
    {
        if (visual == null) return;
        Color color = visual.color;
        color.a = originalAlpha;
        visual.color = color;
    }
    private void OnDisable()
    {
        if (health != null)
        {
            health.DamageReceived -= OnDamageReceived;
            health.Died -= OnDied;
        }
        InterruptAction();
    }
}

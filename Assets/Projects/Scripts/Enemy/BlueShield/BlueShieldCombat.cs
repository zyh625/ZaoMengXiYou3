using System;
using UnityEngine;

public class BlueShieldCombat : EnemyCombat
{
    [Header("举盾")]
    [SerializeField, Range(0f, 1f)] private float shieldDamageReduction = 0.9f;//正面举盾时减免的伤害比例，0.9表示减伤90%。
    [SerializeField, Min(0f)] private float shieldCooldown = 8f;//真正卸盾后开始计时，普通受击不会反复延长已有冷却。
    private Health health;//接受命中前确定方向，实际扣血后再请求播放对应受击动作。
    private BlueShieldMovement movement;//朝向来自真实SpriteRenderer，不用玩家当前位置反推本次盾朝向。
    private float shieldReadyAt;//本次生命内盾牌最早可重新举起的时间。
    private bool raiseAfterHit, preparedShieldHit;//前者等待普通受击结束，后者只记录当前接受的一次直接命中。

    public bool Shielded { get; private set; }//仅本组件能切换盾形态，Controller只选择对应动画。
    public bool RaiseRequested => raiseAfterHit;
    public event Action ShieldChanged;//切换已发生时立即刷新行走/待机表现，不每帧重播动画。
    public event Action ShieldImpacted;//有效正面命中播放盾硬直；即使调成100%减伤也有格挡动作。

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    private void ResolveReferences()//兼容各组件Awake与OnEnable的执行先后顺序。
    {
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<BlueShieldMovement>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveReferences();
        Shielded = raiseAfterHit = preparedShieldHit = false;
        shieldReadyAt = Time.time;//出生时普通形态，第一次受伤后无需等卸盾冷却。
        if (health == null) return;
        health.RemoveDamageMultiplier(this);
        health.DamagePreparing += OnDamagePreparing;
        health.DamageReceived += OnDamageReceived;
        health.Died += OnDied;
    }

    private void OnDamagePreparing(DamageInfo damage)//在命中已通过无敌和格挡过滤后，决定本击是否吃到盾牌减伤。
    {
        preparedShieldHit = false;
        if (Shielded && (movement != null && movement.IsExternallyControlled
            || health != null && health.Status != null && health.Status.BlocksActions))
            LowerShield();//同一物理步刚被冻结或抓住后再被攻击，也不能继续吃到尚未清理的盾减伤。
        if (!Shielded || damage.IsPeriodic || damage.Amount <= 0f) return;
        if (IsFrontHit(damage))
        {
            preparedShieldHit = true;
            ApplyShieldReduction();
            ShieldImpacted?.Invoke();//已通过免疫和格挡检查，不以最终扣血是否为零决定动作。
        }
        else LowerShield();//背击先卸盾，本次伤害即不再享有盾牌减伤。
    }

    private bool IsFrontHit(DamageInfo damage)//没有方向信息或来源重叠时按正面处理，避免不稳定的零距离翻面。
    {
        if (damage.Source == null || movement == null) return true;
        float deltaX = damage.Source.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.001f) return true;
        return (deltaX > 0f) == movement.FaceRight;
    }

    private void OnDamageReceived(DamageInfo damage, float actualDamage)
    {
        if (damage.IsPeriodic || actualDamage <= 0f || health == null || health.Dead) return;
        if (preparedShieldHit && Shielded)
        {
            health.AttackedEnd();//盾硬直只停下，不把这一击积压成之后的击退或挑飞。
            raiseAfterHit = false;
            UpdateAngry();//盾硬直不进入基类普通受击分支，实际扣血仍沿用小怪原有怒气规则。
        }
        else raiseAfterHit = true;//普通受击先播完NormalAttacked，恢复自主行动时再检查当时的盾冷却。
        preparedShieldHit = false;
    }

    public bool TryRaiseShield()//仅在普通受击结束、角色恢复自主行动后由Controller提交。
    {
        if (!raiseAfterHit || Shielded || !isActiveAndEnabled
            || health == null || health.Dead || health.Attacked) return false;
        raiseAfterHit = false;//本次受击结束只检查一次；未冷却好就丢弃，不自动等到期举盾。
        if (Time.time < shieldReadyAt) return false;
        Shielded = true;
        ApplyShieldReduction();
        ShieldChanged?.Invoke();
        return true;
    }

    private void ApplyShieldReduction()//始终按来源写倍率，不覆盖领域或其他独立减伤。
    {
        if (health != null) health.SetDamageMultiplier(this, 1f - Mathf.Clamp01(shieldDamageReduction));
    }

    public void LowerShield()//攻击、背击或控制卸盾；没有拿盾时不重置冷却。
    {
        raiseAfterHit = false;
        preparedShieldHit = false;
        if (health != null) health.RemoveDamageMultiplier(this);
        if (!Shielded) return;
        Shielded = false;
        shieldReadyAt = Time.time + Mathf.Max(0f, shieldCooldown);
        ShieldChanged?.Invoke();
    }

    private void OnDied(DamageInfo damage) => LowerShield();//死亡优先，立即撤销本来源保护。

    private void OnDisable()//对象池下一次启用不能继承上次生命的保护或请求。
    {
        if (health != null)
        {
            health.DamagePreparing -= OnDamagePreparing;
            health.DamageReceived -= OnDamageReceived;
            health.Died -= OnDied;
        }
        LowerShield();
    }
}

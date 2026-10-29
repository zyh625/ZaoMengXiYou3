using System;
using UnityEngine;

//第二关巨灵神专属的怒气，不使用小怪原有的怒气与攻速逻辑。
public class TitanCombat : BossCombat
{
    [Header("巨灵神怒气")]
    [SerializeField, Min(0.01f)] private float rageFull = 100f;//怒气上限
    [SerializeField, Min(0f)] private float rageOnHit = 5f;//完整一招首次实际命中时增加
    [SerializeField, Min(0f)] private float rageOnMiss = 8f;//开启攻击窗口后正常结束且未命中时增加
    [SerializeField, Min(0f)] private float rageOnDamaged = 10f;//每次直接伤害实际扣血后增加，霸体期间也能正确通知
    [SerializeField, Min(0.01f)] private float rageDuration = 8f;//满怒强化持续秒数，暂停时停止计时
    [SerializeField, Min(0f)] private float rageAttackMultiplier = 1.3f;//强化期间所有攻击命中时使用的额外伤害倍率
    [SerializeField] private float rage;//当前怒气，死亡后保留给界面定格

    private Health health;//统一伤害事件不依赖受击动画，冰冻或霸体中受伤也不会漏算
    private float rageBoostUntil;//满怒强化的结束时刻
    private bool rageBoostActive, deadFrozen;//死亡冻结怒气和强化计时，不能在死亡动画期间归零
    public float Rage => Mathf.Clamp(rage, 0f, MaxRage);
    public float MaxRage => Mathf.Max(0.01f, rageFull);
    public bool RageBoostActive => rageBoostActive;
    public event Action RageChanged;//特殊状态UI只订阅本Boss的数据变化
    public event Action<bool> RageBoostChanged;//Controller统一处理霸体、移速、残影以及结束时的动作与技能冷却
    public override float DamageMultiplier => base.DamageMultiplier * (rageBoostActive && health != null && !health.Dead
        ? Mathf.Max(0f, rageAttackMultiplier) : 1f);//专属怒气仍与通用外部增幅独立叠乘

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Health>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (health == null) health = GetComponent<Health>();
        deadFrozen = rageBoostActive = false;
        rageBoostUntil = 0f;
        rage = 0f;
        if (health != null)
        {
            health.DamageReceived += OnDamageReceived;
            health.Died += OnDied;
        }
        RageChanged?.Invoke();
    }

    private void OnDisable()//停用只退订并停止强化，不改变死亡后保留的槽值；Controller负责移除自己的表现和属性来源
    {
        if (health != null)
        {
            health.DamageReceived -= OnDamageReceived;
            health.Died -= OnDied;
        }
        rageBoostActive = false;
    }

    public override void UpdateAngry() { }//旧小怪的攻击和受击入口不参与巨灵神怒气，避免重复结算
    protected override void OnAttackHit(int attack) => AddRage(rageOnHit);
    protected override void OnAttackMissed(int attack) => AddRage(rageOnMiss);

    private void OnDamageReceived(DamageInfo damage, float actualDamage)//毒素每帧扣血不是一次新攻击，不按每帧增加怒气
    {
        if (actualDamage > 0f && !damage.IsPeriodic) AddRage(rageOnDamaged);
    }

    private void AddRage(float amount)//强化期间锁在满值，不把期间收到的事件积压到结束后
    {
        if (!isActiveAndEnabled || deadFrozen || health == null || health.Dead || rageBoostActive || amount <= 0f) return;
        rage = Mathf.Min(MaxRage, rage + amount);
        if (rage >= MaxRage)
        {
            rageBoostActive = true;
            rageBoostUntil = Time.time + Mathf.Max(0.01f, rageDuration);
            RageBoostChanged?.Invoke(true);
        }
        RageChanged?.Invoke();
    }

    private void Update()//怒气强化独立计时，受击、控制和冲撞动画不会阻止到期
    {
        if (deadFrozen || health == null || health.Dead || !rageBoostActive || Time.time < rageBoostUntil) return;
        rageBoostActive = false;
        rage = 0f;
        ResetAllAttackCooldowns();
        RageBoostChanged?.Invoke(false);
        RageChanged?.Invoke();
    }

    private void OnDied(DamageInfo damage)//先停止攻击窗口，槽值保持死亡瞬间的数值，不发送结束归零事件
    {
        deadFrozen = true;
        rageBoostActive = false;
        AttackEnd();
        ClearAttackRequests();
    }
}

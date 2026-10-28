using System.Collections.Generic;
using UnityEngine;

//普通控制受霸体限制，强控制（冰冻、抓取、携带等）只受无敌、死亡和对象可用性限制。
public enum ControlStrength
{
    Ordinary,
    Strong
}

[RequireComponent(typeof(ActorStatus))]
public class Health : MonoBehaviour,IDamage
{
    [SerializeField] private float beginBlood;
    [SerializeField]private float curBlood;
    public float BeginBlood => beginBlood;
    public float CurBlood => curBlood;
    public int LifeVersion { get; private set; }//对象池每次真正重生递增，投射物可区分同一实例的新旧生命
    private ActorStatus status;//生命与通用状态共用一次命中结算，新人物自动拥有状态组件
    public ActorStatus Status => status != null ? status : status = GetComponent<ActorStatus>();
    public event System.Action<float, float> HealthChanged;//真实血量变化时通知界面，参数为当前血量和上限
    public event System.Action<DamageInfo, float> DamageReceived;//真实扣血后通知本次伤害及实际扣血量，霸体也会收到，无敌拒绝的命中不会收到
    public event System.Action<DamageInfo> Died;//本次生命只通知一次死亡，并保留致死来源与命中数据
    public event System.Action DebuffsCleared;//所有负面状态组件统一订阅：清计时、解除自己来源的控制与属性，不移除其他正面增益
    public event System.Action<DamageInfo> InvincibleHit;//有效攻击被无敌挡住时通知精准抵挡，周期扣血与反伤不触发
    private bool die, attacked;
    public bool Dead => die;
    public bool Attacked => attacked;
    public bool AttackedEnd()
    {
        attackedWithoutKnockback = false;
        attackedBack = false;
        attackedUp = false;
        return attacked = false;
    }
    private bool attackedBack;//是否需要水平击退，与竖直击飞完全独立
    public bool AttackedBack => attackedBack;
    private bool attackedWithoutKnockback;//反伤受击只播放动画，不施加击退或挑飞
    public bool AttackedWithoutKnockback => attackedWithoutKnockback;
    [SerializeField]private bool attackedUp = false;
    public bool AttackedUp => attackedUp;
    private Transform target;
    public float DeltaX => target != null ? target.position.x - transform.position.x : 0f;
    public bool AttackedRight => DeltaX <= 0f;//向右边击退
    private bool slow = false;//当前是否被减速
    public bool Slow => slow;
    private bool boost = false;//旧接口的普通霸体：只免受击反应和晕眩，不免冰冻或抓取
    public void SetBoost(bool flag)=>boost=flag;
    public bool Boost
    {
        get
        {
            if (boost) return true;
            foreach (Object source in superArmorSources)
                if (source != null) return true;
            return false;
        }
    }
    public bool CanReceiveControl(ControlStrength strength = ControlStrength.Ordinary)//以后新增控制也使用统一分级规则，不在技能中重复判断霸体
        => isActiveAndEnabled && !die && !IsInvincible && (strength == ControlStrength.Strong || !Boost);
    public float ratio => curBlood / beginBlood;
    private bool preserveForSwitch;//仅玩家换人时保留生命值；敌人对象池仍在启用时正常重置
    private readonly Dictionary<Object, float> damageMultipliers = new();//按来源叠乘伤害倍率，各增益独立移除
    private readonly HashSet<Object> superArmorSources = new();//按来源记录霸体，结束某种霸体不影响其他来源
    private readonly Dictionary<Object, float> reflectionRatios = new();//按来源记录反伤比例，使用减伤前的原始伤害
    private readonly Dictionary<Object, float> invincibilityUntil = new();//各来源的短暂无敌分别计时，结束一种保护不影响另一种
    public bool IsInvincible
    {
        get
        {
            foreach (KeyValuePair<Object, float> protection in invincibilityUntil)
                if (protection.Key != null && Time.time < protection.Value) return true;
            return false;
        }
    }
    private readonly Dictionary<Object, NextHitVulnerability> nextHitVulnerabilities = new();//同一来源只刷新一次性易伤，不重复叠层
    private Transform poisonSource;//骷髅与流沙共用同一组毒层，最后施加者保留致死经验归属
    private int poisonStacks;//所有中毒来源累加层数，重新施加时刷新整体持续时间
    private float poisonUntil, poisonDamagePerStack, poisonUpdatedAt;//连续结算至到期时刻，不受渲染帧率影响
    public int PoisonStacks => poisonStacks;
    private readonly struct NextHitVulnerability//过期或下一次有效攻击后移除，无需每帧轮询
    {
        public readonly float Multiplier, ExpiresAt;
        public NextHitVulnerability(float multiplier, float expiresAt)
        {
            Multiplier = multiplier;
            ExpiresAt = expiresAt;
        }
    }
    void OnEnable()
    {
        ClearSourceEffects();
        if (preserveForSwitch)
        {
            preserveForSwitch = false;
            return;
        }
        LifeVersion++;
        die = false;
        AttackedEnd();
        curBlood = beginBlood;slow = false;
        HealthChanged?.Invoke(curBlood, beginBlood);
    }
    //普通换人不是重生，下一次启用时继续使用该角色已有的生命值。
    public void PreserveForSwitch() => preserveForSwitch = true;
    //同一来源重复设置只更新该来源，不会每帧重复叠加。
    public void SetDamageMultiplier(Object source, float multiplier)
    {
        if (source != null) damageMultipliers[source] = Mathf.Clamp01(multiplier);
    }
    public void RemoveDamageMultiplier(Object source)//仅移除指定来源的减伤
    {
        if (source != null) damageMultipliers.Remove(source);
    }
    public void SetSuperArmor(Object source, bool active)//保留原SetBoost接口，新增霸体来源互不覆盖
    {
        if (source == null) return;
        if (active)
        {
            superArmorSources.Add(source);
            AttackedEnd();
        }
        else superArmorSources.Remove(source);
    }
    public void SetReflection(Object source, float ratio)//比例1表示反弹原始伤害的100%
    {
        if (source != null) reflectionRatios[source] = Mathf.Max(0f, ratio);
    }
    public void RemoveReflection(Object source)//结束反甲时只移除自己的反伤来源
    {
        if (source != null) reflectionRatios.Remove(source);
    }
    public void GrantInvincibility(Object source, float duration)//命中结算层的短暂无敌，不依赖受击箱开关
    {
        if (source == null || die) return;
        if (duration <= 0f)
        {
            invincibilityUntil.Remove(source);
            return;
        }
        float expiresAt = Time.time + duration;//同一来源重新授予时不会缩短尚未结束的保护
        if (invincibilityUntil.TryGetValue(source, out float previousExpiry))
            expiresAt = Mathf.Max(expiresAt, previousExpiry);
        invincibilityUntil[source] = expiresAt;
    }
    public void RemoveInvincibility(Object source)//只移除指定来源的保护
    {
        if (source != null) invincibilityUntil.Remove(source);
    }
    public void ApplyNextHitVulnerability(Object source, float bonus, float duration)//下一次有效伤害增伤，重复施加同来源只刷新
    {
        if (source == null || die || IsInvincible || !isActiveAndEnabled) return;
        if (bonus <= 0f || duration <= 0f)
        {
            nextHitVulnerabilities.Remove(source);
            return;
        }
        nextHitVulnerabilities[source] = new NextHitVulnerability(1f + bonus, Time.time + duration);
    }
    public void ClearDebuffs()//无双开启时清除易伤、减速等负面效果，保留圣盾、领域等正面增益
    {
        AttackedEnd();//统一驱散入口也清除尚未处理的受击、击退和击飞请求
        target = null;
        slow = false;
        nextHitVulnerabilities.Clear();
        ClearPoison();
        DebuffsCleared?.Invoke();
    }
    //各技能共享毒层；先结清已有层数，再增加新层数，避免本帧追溯放大旧伤害。
    public void ApplyPoison(Transform source, float duration = 5f, float damagePerStack = 1f, int stacks = 1)
    {
        if (die || IsInvincible || !isActiveAndEnabled || duration <= 0f || damagePerStack <= 0f || stacks <= 0) return;
        UpdatePoison();
        if (die || !isActiveAndEnabled) return;
        poisonSource = source;
        poisonStacks += stacks;
        poisonDamagePerStack = damagePerStack;
        poisonUntil = Time.time + duration;
        poisonUpdatedAt = Time.time;
    }
    private void Update() => UpdatePoison();//持续伤害不依赖攻击箱开关，也不重复产生受击请求
    private void UpdatePoison()
    {
        if (poisonStacks <= 0 || die) return;
        float end = Mathf.Min(Time.time, poisonUntil);//最后一帧只结算实际剩余的中毒时间
        float elapsed = Mathf.Max(0f, end - poisonUpdatedAt);
        poisonUpdatedAt = end;
        if (elapsed > 0f)
            ReceiveDamage(new DamageInfo(poisonSource, poisonStacks * poisonDamagePerStack * elapsed,
                false, false, null, false, true));
        if (die || Time.time >= poisonUntil) ClearPoison();
    }
    private void ClearPoison()//只清理中毒数据，不影响减伤、霸体等正面来源
    {
        poisonStacks = 0;
        poisonSource = null;
        poisonUntil = poisonDamagePerStack = poisonUpdatedAt = 0f;
    }
    public void Heal(float amount)//持续回血共用入口，不超过血量上限，也不复活死亡角色
    {
        if (die || amount <= 0f) return;
        float previousBlood = curBlood;//满血时不发送重复通知
        curBlood = Mathf.Min(beginBlood, curBlood + amount);
        if (curBlood != previousBlood) HealthChanged?.Invoke(curBlood, beginBlood);
    }
    public void TakeDamage(Transform tran,float damage)
    {
        //兼容旧调用；新的攻击箱使用DamageInfo，伤害数值本身不再承担击飞含义。
        TakeDamage(new DamageInfo(tran, Mathf.Abs(damage), true, damage < 0f));
    }
    public void TakeDamage(DamageInfo damage) => ReceiveDamage(damage);

    private void ReceiveDamage(DamageInfo damage)//基础伤害、命中反应与附加效果分别结算
    {
        if (die) return;
        if (IsInvincible)//仍然拒绝整次命中，只额外通知护盾确实挡住了一次攻击
        {
            if (damage.Amount > 0f && !damage.IsPeriodic && !damage.IsReflected)
                InvincibleHit?.Invoke(damage);
            return;
        }
        if (!damage.IsPeriodic) target = damage.Source;//毒素不覆盖正等待处理的攻击方向
        float originalDamage = damage.Amount;//保留攻击者输入的基础伤害，反伤不使用减伤或易伤后的结果
        float vulnerabilityMultiplier = originalDamage > 0f && !damage.IsPeriodic
            ? ConsumeNextHitVulnerability() : 1f;//反伤基础仍按原始攻击计算，接收者照常结算自己的易伤和减伤
        float damageMultiplier = 1f;//多个来源乘法叠加，例如0.5与0.9得到0.45
        foreach (KeyValuePair<Object, float> modifier in damageMultipliers)
            if (modifier.Key != null) damageMultiplier *= modifier.Value;
        float frozenMultiplier = Status != null ? Status.ConsumeFreezeBreak(damage) : 1f;//毒素与无敌拒绝的命中不会打碎冰块
        float previousBlood = curBlood;//霸体和反甲同样需要在扣血后更新界面
        curBlood = Mathf.Max(0f, curBlood - originalDamage * damageMultiplier * vulnerabilityMultiplier * frozenMultiplier);
        BattleRun.RecordDamage(this, damage, previousBlood - curBlood);//只统计实际扣除的血量，毒素和反伤沿用真实来源，不把过量伤害计入MVP
        bool killed = curBlood == 0f;//当前结算完成后再发送死亡事件，防止切场景打断反伤结算
        if (killed)
        {
            die = true;
            AttackedEnd();
        }
        else if (!damage.IsPeriodic && !Boost)
        {
            attacked = true;
            attackedBack = !damage.IsReflected && damage.Knockback;
            attackedUp = !damage.IsReflected && damage.Launch;
            attackedWithoutKnockback = !attackedBack && !attackedUp;
        }
        else if (!damage.IsPeriodic) AttackedEnd();//毒素不清除另一笔真实攻击尚未处理的受击请求
        if (curBlood != previousBlood) HealthChanged?.Invoke(curBlood, beginBlood);

        if (curBlood < previousBlood) DamageReceived?.Invoke(damage, previousBlood - curBlood);

        if (!die && !damage.IsReflected && !damage.IsPeriodic && damage.Effects != null)
            foreach (AttackEffect effect in damage.Effects)
                if (effect != null && !die) effect.Apply(this, damage);//新效果在本次基础命中之后应用

        if (!damage.IsReflected && !damage.IsPeriodic && damage.Source != null && originalDamage > 0f)
        {
            Health attacker = damage.Source.GetComponentInParent<Health>();//攻击箱和角色根节点都能正确定位来源
            if (attacker != null && attacker != this)
            {
                float reflectionRatio = 0f;//多来源反伤合并结算一次，避免重复播放同一次受击
                foreach (KeyValuePair<Object, float> reflection in reflectionRatios)
                    if (reflection.Key != null) reflectionRatio += reflection.Value;
                if (reflectionRatio > 0f)
                    attacker.ReceiveDamage(new DamageInfo(transform, originalDamage * reflectionRatio,
                        false, false, null, true));
            }
        }
        if (killed) Died?.Invoke(damage);
    }

    private float ConsumeNextHitVulnerability()//所有未过期来源相乘，结算这一击后统一消耗
    {
        float multiplier = 1f;
        foreach (KeyValuePair<Object, NextHitVulnerability> vulnerability in nextHitVulnerabilities)
            if (vulnerability.Key != null && Time.time < vulnerability.Value.ExpiresAt)
                multiplier *= vulnerability.Value.Multiplier;
        nextHitVulnerabilities.Clear();
        return multiplier;
    }
    private void OnDisable() => ClearSourceEffects();//对象池和换人都不能留下已经失效的来源增益

    private void ClearSourceEffects()//不改变现有boost字段和换人保留的真实生命值
    {
        damageMultipliers.Clear();
        superArmorSources.Clear();
        reflectionRatios.Clear();
        invincibilityUntil.Clear();
        ClearDebuffs();
    }

}

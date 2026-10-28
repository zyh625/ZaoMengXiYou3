using System.Collections.Generic;
using UnityEngine;

public class BajieCombat : PlayerCombat
{
    private HolyShieldManager holyShield;
    private PlayerManager effectManager;//独立领域由关卡的统一对象池创建，不在本体缓存唯一领域实例
    [SerializeField] private int yieldOpenFrame = 15;//领域开启对应的帧序列，从0开始
    private HolyLightManager holyLight;//技能一同时播放的自身圣光
    private BajieMovement movement;//地面限定施法使用实时地面接触校验
    private Health health;//阻止受击、死亡期间释放技能
    private BajieController controller;//当前仍在受击动画中时也不能释放技能一
    private Combat skillTwo, skillThree;//缓存动作数据，生效窗口直接使用Combat中的开关帧
    private readonly HashSet<EnemyMovement> attractedEnemies = new();//本次曾被吸引的敌人，离开范围也不会丢失
    [SerializeField, Min(0f)] private float suctionRange = 8f;//吸附范围，以八戒当前位置为圆心
    [SerializeField, Min(0f)] private float suctionSpeed = 12f;//吸附移动速度
    [SerializeField, Min(0f)] private float suctionHeight = 2f;//收束位置位于八戒正上方的高度
    private Vector2 lastSuctionTarget;//最后一次有效动作帧的位置，完整释放后使用这个终点
    private bool suctionFinished;//本次有效窗口已经结束，不能再次捕获敌人
    private bool yieldCreated;//每次施法只生成一次新领域，不移动或刷新之前已经生效的领域
    [SerializeField, Min(0f)] private float suctionDamageBonus = 0.3f;//被吸附后下一次攻击增加的伤害比例
    [SerializeField, Min(0f)] private float suctionBonusDuration = 5f;//下一击易伤未触发时的保留时间
    private InputReader input;//反甲手势始终从输入Action读取，支持后续改键
    [SerializeField, Min(0f)] private float reflectionTapDuration = 0.2f;//第一次S按住不超过这个时间才算短按
    [SerializeField, Min(0f)] private float reflectionTapGap = 0.3f;//第一次松开到第二次按下S的最长间隔
    [SerializeField, Min(0f)] private float reflectionRatio = 1f;//反弹原始伤害的比例，1表示100%
    [SerializeField, Range(0f, 1f)] private float reflectionDamageMultiplier = 0.9f;//反甲时自身承受的伤害倍率
    private float quickDownPressedAt = -1f, lastQuickDownReleasedAt = -1f;//仅保存本次反甲手势的按下、短按松开时间
    private bool reflectionRequest;//第二次按下后，交给控制器按优先级进入反甲
    public bool Reflecting { get; private set; }//反甲期间只允许技能、松键或死亡结束当前模式
    protected override void Awake()
    {
        base.Awake();
        holyShield = GetComponentInChildren<HolyShieldManager>(true);
        holyLight = GetComponentInChildren<HolyLightManager>(true);
        movement = GetComponent<BajieMovement>();
        health = GetComponent<Health>();
        controller = GetComponent<BajieController>();
        skillTwo = GetAction("Skill2");
        skillThree = GetAction("Skill3");
        input = GetComponent<InputReader>();
    }

    private void OnEnable()
    {
        ResetReflectionInput();
        input.QuickDownPressed += OnReflectionPressed;
        input.QuickDownReleased += OnReflectionReleased;
    }

    public override void OnAttack()
    {
        if (Reflecting) return;//反甲期间的普攻输入直接舍弃，松键后不会补放
        base.OnAttack();
    }

    private void OnReflectionPressed()//第一次须短按，第二次按下后保持即可维持反甲
    {
        if (Reflecting || !CanEnterReflection())
        {
            ResetReflectionInput();
            return;
        }
        float now = Time.unscaledTime;//手势间隔不受游戏速度变化影响
        if (lastQuickDownReleasedAt >= 0f && now - lastQuickDownReleasedAt <= reflectionTapGap)
        {
            reflectionRequest = true;
            lastQuickDownReleasedAt = -1f;
        }
        quickDownPressedAt = now;
    }

    private void OnReflectionReleased()
    {
        if (Reflecting || reflectionRequest)
        {
            EndReflection();
            return;
        }
        float now = Time.unscaledTime;//只有短按松开才能作为下一次反甲的第一下
        lastQuickDownReleasedAt = quickDownPressedAt >= 0f && now - quickDownPressedAt <= reflectionTapDuration
            ? now : -1f;
        quickDownPressedAt = -1f;
    }

    private bool CanEnterReflection() => Skilled == -1 && controller.State() != PlayerState.Skill
        && CanReleaseSkill() && GetAction("ComboAttack1")?.animation?.sprites?.Length > 0;

    public bool ConsumeReflectionRequest()//请求只处理一次，不缓存技能、受击期间按下的S
    {
        bool requested = reflectionRequest;
        reflectionRequest = false;
        return requested && input.QuickDownHeld && CanEnterReflection();
    }

    public void BeginReflection()
    {
        ResetReflectionInput();
        Reflecting = true;
        ClearAttackRequests();
        health.SetDamageMultiplier(this, reflectionDamageMultiplier);
        health.SetSuperArmor(this, true);
        health.SetReflection(this, reflectionRatio);
    }

    public void EndReflection()//每个效果只移除反甲自己的来源，圣盾和领域可以继续存在
    {
        if (!Reflecting && !reflectionRequest) return;
        ResetReflectionInput();
        Reflecting = false;
        ClearAttackRequests();
        health.RemoveReflection(this);
        health.RemoveDamageMultiplier(this);
        health.SetSuperArmor(this, false);
    }

    private void ResetReflectionInput()//换人、施法打断和松键后不能沿用上一次双击
    {
        quickDownPressedAt = lastQuickDownReleasedAt = -1f;
        reflectionRequest = false;
    }
    public override void OnSkill(int k)
    {
        if (k < 1 || k > 3 || !CanReleaseSkill()) return;
        if (k == 1)//圣盾无前摇，除了受击、死亡都可直接释放
        {
            if (!TrySpendSkillMana(1)) return;
            holyShield?.OpenShield();
            holyLight?.OpenLight();
            return;
        }
        if (k == 3 && !movement.IsOnGroundNow()) return;
        base.OnSkill(k);
    }
    //输入时与实际开始施法时各检查一次，领域不能使用空中缓存请求释放。
    public bool CanStartSkill() => CanReleaseSkill() &&
        (Skilled == 2 || (Skilled == 3 && movement.IsOnGroundNow()));

    private bool CanReleaseSkill() => isActiveAndEnabled && !health.Dead && !health.Attacked &&
        controller.State() != PlayerState.Attacked && controller.State() != PlayerState.Die;

    //在进入技能动作时重置本次记录；不改变跳跃速度、移动输入或技能一特效。
    public void BeginSkill()
    {
        FinishSuction(false);
        suctionFinished = false;
        yieldCreated = false;
    }

    public void SetEffectManager(PlayerManager manager) => effectManager = manager;//由PlayerManager初始化角色时统一注入

    public void SetYieldManager(YieldManager yield)//兼容旧的注入入口，实际释放仍走PlayerManager的领域池
    {
        if (effectManager != null || yield == null) return;
        effectManager = yield.GetComponentInParent<PlayerManager>();
        if (effectManager == null) effectManager = FindFirstObjectByType<PlayerManager>();
    }
    public void CheckYield(int idx)
    {
        if (yieldCreated || effectManager == null) return;
        int openFrame = Mathf.Clamp(yieldOpenFrame, 0, LastFrame(skillThree));//末帧之后仍按末帧处理
        if (idx >= openFrame)
        {
            yieldCreated = true;
            effectManager.SpawnYield(transform);
        }
    }

    //吸附窗口为Combat的[openFrame, closeFrame)，每次更新都能接纳范围内新生成的敌人。
    public void CheckSuction(int idx)
    {
        if (skillTwo == null || suctionFinished) return;
        int frame = Mathf.Min(idx, LastFrame(skillTwo));//播放器结束时CurFrame可能已经超过末帧
        if (frame < skillTwo.openFrame) return;
        if (frame >= skillTwo.closeFrame)
        {
            FinishSuction(true);
            return;
        }

        Vector2 position = transform.position;//当前八戒的位置，移动施法时终点随之更新
        lastSuctionTarget = position + Vector2.up * suctionHeight;
        float rangeSquared = suctionRange * suctionRange;//避免范围判断反复开平方
        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            Health enemyHealth = enemy.GetComponent<Health>();//死亡单位不参与吸附
            if (enemyHealth != null && enemyHealth.Dead) continue;
            if (((Vector2)enemy.transform.position - position).sqrMagnitude > rangeSquared) continue;
            if (attractedEnemies.Add(enemy) && enemyHealth != null)
                enemyHealth.ApplyNextHitVulnerability(this, suctionDamageBonus, suctionBonusDuration);//每次施法只挂一次，消耗后不会逐帧补回
            enemy.BeginSuction(this, lastSuctionTarget, suctionSpeed);
        }
        foreach (EnemyMovement enemy in attractedEnemies)
            if (enemy != null && enemy.isActiveAndEnabled)
                enemy.UpdateSuctionTarget(this, lastSuctionTarget);
    }

    //只有控制器确认完整播放结束时才收束；中断统一走SkillEnd，不会瞬移到终点。
    public void CompleteSkill()
    {
        if (Skilled == 2) FinishSuction(true);
    }

    public override void SkillEnd()
    {
        FinishSuction(false);
        yieldCreated = false;
    }

    protected override void OnDisable()//切场景、外部停用也要立即释放敌人并恢复领域施法前的重力
    {
        input.QuickDownPressed -= OnReflectionPressed;
        input.QuickDownReleased -= OnReflectionReleased;
        EndReflection();
        ResetReflectionInput();
        FinishSuction(false);
        if (movement != null) movement.EndSkillMove();
        base.OnDisable();
    }

    private void FinishSuction(bool complete)//保留完整结束与被打断两种不同释放方式
    {
        foreach (EnemyMovement enemy in attractedEnemies)
            if (enemy != null)
                enemy.EndSuction(this, complete, lastSuctionTarget);
        attractedEnemies.Clear();
        suctionFinished = true;
    }

    private static int LastFrame(Combat action) => action?.animation?.sprites == null
        ? 0 : Mathf.Max(0, action.animation.sprites.Length - 1);//帧序列从0开始
}

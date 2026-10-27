using UnityEngine;

public class TangCombat : PlayerCombat
{
    [SerializeField, Min(0)] private int normalAttackFrame = 9;//动作第10帧发射，下标从0开始；帧跳过时也只补发一次
    private PlayerManager effectManager;//场景角色管理器注入，普攻投射物不放在唐僧本体下面
    private bool normalAttackReleased;//同一轮攻击只发射一次，不按渲染帧重复生成
    private Health health;//施法开始与同步帧时都检查受击、死亡，打断当帧不能补放技能效果
    private TangController controller;//输入时拒绝受击、死亡状态，避免瞬发技能越过限制
    private ProtectionManager protection;//技能一完全由独立护罩管理持续时间和精准抵挡
    private OceanBreakerManager oceanBreaker;//水柱与本体帧一一对应
    private FrostFormationManager frostFormation;//冰阵自己的冻结来源可单独解除

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Health>();
        controller = GetComponent<TangController>();
        protection = GetComponentInChildren<ProtectionManager>(true);
        oceanBreaker = GetComponentInChildren<OceanBreakerManager>(true);
        frostFormation = GetComponentInChildren<FrostFormationManager>(true);
    }

    private void OnEnable()//死亡事件即时关闭子技能，不等待下一次FixedUpdate才停止命中
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null) health.Died += OnDied;
    }

    public void SetEffectManager(PlayerManager manager) => effectManager = manager;//由PlayerManager创建角色时统一接入

    public void BeginNormalAttack() => normalAttackReleased = false;//只在真正切入本轮普通攻击动画时重置

    public void CheckNormalAttack(int frame, bool right)//动画已经换到有效帧后检查，提前受击、冰冻或换人均不能补发
    {
        if (normalAttackReleased || frame < normalAttackFrame || Attacked != 1 || effectManager == null) return;
        if (!isActiveAndEnabled || health == null || health.Dead || health.Attacked
            || health.Status != null && health.Status.BlocksActions
            || controller == null || controller.State() != PlayerState.NormalAttack) return;
        normalAttackReleased = true;
        effectManager.SpawnTangNormalAttack(transform, right);
    }

    public override void OnSkill(int k)//金蝉护体瞬发无本体动作，另外两招沿用公共施法流程
    {
        if (k < 1 || k > 3 || !CanReleaseSkill()) return;
        if (k == 1)
        {
            if (protection == null) return;
            if (!TrySpendSkillMana(1)) return;//瞬发技能同样经过蓝量和冷却入口
            protection.OpenProtection();//不修改Skilled，不切本体动作，也不打断正在释放的其他技能
            return;
        }
        if (k == 2 && oceanBreaker == null || k == 3 && frostFormation == null) return;
        base.OnSkill(k);
    }

    private bool CanReleaseSkill()//动作受击阶段尚未结束时同样不能用瞬发技能
        => isActiveAndEnabled && health != null && !health.Dead && !health.Attacked
            && controller != null && controller.State() != PlayerState.Attacked && controller.State() != PlayerState.Die;

    public bool CanStartSkill()//公共控制器确认开始前再检查，失败请求不消耗蓝量或进入冷却
        => CanReleaseSkill() && (Skilled == 2 && oceanBreaker != null || Skilled == 3 && frostFormation != null);

    public void BeginSkill()//真正进入技能动作后才建立本轮特效与命中记录
    {
        StopSkillEffects();
        if (Skilled == 2) oceanBreaker?.BeginCast(transform);
        else if (Skilled == 3) frostFormation?.BeginCast(transform);
    }

    public void CheckSkillEffect(int frame, bool right)//FixedUpdate判定与LateUpdate显示都走同一同步入口
    {
        if (!isActiveAndEnabled || health == null || health.Dead || health.Attacked)
        {
            StopSkillEffects();
            return;
        }
        if (Skilled == 2) oceanBreaker?.SyncFrame(frame, right);
        else if (Skilled == 3) frostFormation?.SyncFrame(frame, right);
    }

    public override void SkillEnd()//正常结束与提前受击都关闭水柱、冰阵，独立护罩继续自己的寿命
    {
        StopSkillEffects();
        base.SkillEnd();
    }

    private void StopSkillEffects()//停止冰阵只解除自己施加的冻结，不执行碎裂伤害
    {
        oceanBreaker?.StopEffect();
        frostFormation?.StopEffect();
    }

    private void OnDied(DamageInfo damage)
    {
        normalAttackReleased = true;//死亡当帧先取消尚未发出的普攻，不等待控制器下一次状态判断
        StopSkillEffects();
        protection?.CloseProtection();
    }

    protected override void OnDisable()//换出和场景关闭立即清理自己创建的效果，不残留永久免伤
    {
        normalAttackReleased = true;//仅取消本体尚未生成的投射物，已发出的由PlayerManager继续管理
        if (health != null) health.Died -= OnDied;
        StopSkillEffects();
        protection?.CloseProtection();
        base.OnDisable();
    }
}

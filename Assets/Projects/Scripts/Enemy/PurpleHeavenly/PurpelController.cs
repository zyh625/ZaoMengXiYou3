using UnityEngine;

public class PurpelController : EnemyController
{
    [SerializeField] private SpriteData AttackOne, AttackTwo;//两种普攻沿用预制体已配置的检测箱、攻击箱和有效帧
    [SerializeField] private SpriteData Parry;//举刀格挡的专属姿势，不和普通受击动作共用
    private PurpleHeavenlyCombat combat;//战斗组件负责连击判断和下一击拦截，控制器只安排动作
    private PurpelMovement movement;//进入弹反时终止旧击退，举刀期间停止自主移动
    private bool parryPoseHeld;//一次举刀只切换一次精灵动画

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<PurpleHeavenlyCombat>();
        movement = GetComponent<PurpelMovement>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        parryPoseHeld = false;
        if (combat == null) return;
        combat.ParryStarted += OnParryStarted;
        combat.ParryEnded += OnParryEnded;
    }

    protected override void OnDisable()
    {
        if (combat != null)
        {
            combat.ParryStarted -= OnParryStarted;
            combat.ParryEnded -= OnParryEnded;
        }
        parryPoseHeld = false;
        base.OnDisable();
    }

    protected override void HandleAttack(int attack)
        => SM.ChangeState(attack == 0 ? AttackOne : AttackTwo);//攻击编号继续对应已有的两组范围

    protected override void HandleSkill() => SM.ChangeState(Parry);//复用公共特殊动作状态，弹反计时由Combat独立控制

    protected override bool ProcessSpecialState()//死亡判定之后、普通控制之前处理弹反，不被旧受击动画覆盖
    {
        if (combat == null || !combat.ParryActive) return false;
        movement.Stop();
        return true;
    }

    private void OnParryStarted()//伤害结算后立刻举刀，不等待下一次物理步才能从冰冻中恢复
    {
        if (EH == null || EH.Dead) return;
        AttackOver();
        ReleaseControlPose();
        movement.PrepareParry();
        SM.Resume();
        SetState(EnemyState.Skill);
        parryPoseHeld = true;
    }

    private void OnParryEnded()//新施加的强控制仍由原控制来源处理，结束格挡不能解除它们
    {
        if (!parryPoseHeld) return;
        parryPoseHeld = false;
        if (EH == null || EH.Dead || EH.Status != null && EH.Status.BlocksActions
            || movement.IsCarried || movement.IsStunned) return;
        SM.Resume();
        if (state == EnemyState.Skill) SetState(EnemyState.Idle);
    }
}

using UnityEngine;

public class EastController : BossController
{
    [SerializeField] private SpriteData NormalAttack, StunAttack, SummonAttack, BuffAttack;//四种弹琴动作分别使用自己的帧图片
    private EastCombat combat;//音符冷却、发射时机与对象池入口由战斗组件统一负责
    private EastMovement movement;//沿用公共受击、地面检测和控制逻辑，不重复整套更新顺序
    private int pendingAttack = -1;//起手判定和切动画之间暂存招式，不提前打开攻击箱或生成音符

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<EastCombat>();
        movement = GetComponent<EastMovement>();
    }

    protected override bool SkillReady => state != EnemyState.Skill && combat != null && movement != null
        && (state != EnemyState.Attacked || SM.IsFinished)
        && (movement.IsOnGroundNow() || EH.Status != null && EH.Status.IsMelting)
        && combat.GetReadyAttack() >= 0;//持国天王只在地面起手，融化时遵守共用的原地施法规则

    protected override void SkillSet() => pendingAttack = combat.GetReadyAttack();//查询不改冷却，真正切入动作时再提交

    protected override void HandleSkill()
    {
        if (!combat.BeginAttack(pendingAttack, movement.FaceTowardsPlayer))
        {
            SetState(EnemyState.Idle);
            return;
        }
        SpriteData animation = AnimationFor(pendingAttack);
        if (animation == null || animation.sprites == null || animation.sprites.Length == 0)
        {
            combat.CancelAttack();
            SetState(EnemyState.Idle);
            return;//错误的动画引用不能让角色永远停在技能状态
        }
        SM.ChangeSpriteFace(combat.AttackFaceRight);
        SM.ChangeState(animation);
    }

    private SpriteData AnimationFor(int index)//招式数据集中对应，后续调整动画不会改变音符发射的序号
    {
        switch (index)
        {
            case 0: return NormalAttack;
            case 1: return StunAttack;
            case 2: return SummonAttack;
            default: return BuffAttack;
        }
    }

    protected override void CheckSkill()
    {
        if (combat == null || movement == null) return;
        if (state == EnemyState.Skill && EH.Attacked && !EH.Boost)
        {
            combat.CancelAttack();
            SetState(EnemyState.Idle);//交回共用受击分支，本次伤害的击退与受击动画正常执行
        }
        if (state == EnemyState.Skill) combat.CheckNote(SM.CurFrame);
        else combat.CancelAttack();//冰冻、携带和吸附切走动作后，不保留旧发射请求
    }

    protected override void SpecialAttackOver()//共用控制器在受击、死亡、冰冻和吸附时都会调用，立即停止待发音符
    {
        combat?.CancelAttack();
    }

    protected override void SkillOver()
    {
        combat.CancelAttack();
        pendingAttack = -1;
        SetState(EnemyState.Idle);//明确结束本轮技能状态，下一次冷却完成后才能重新起手
    }

    private void LateUpdate()//第十帧本体显示后的同一渲染帧发射，不额外等待下一轮物理更新
    {
        if (combat == null) return;
        if (state == EnemyState.Skill) combat.CheckNote(SM.CurFrame);
        else combat.CancelAttack();
    }

    protected override void OnDisable()
    {
        combat?.CancelAttack();
        pendingAttack = -1;
        base.OnDisable();
    }
}

using UnityEngine;

public class PurpleController : EnemyController
{
    [SerializeField] private SpriteData Attack;//射箭动作沿用技能调度，不创建近战攻击箱
    private PurpleRangedCombat combat;//发射时机与虚化由远程天兵自己的战斗脚本管理
    private PurpleRangedMovement movement;//开弓时锁定朝向，收招后才重新转向

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<PurpleRangedCombat>();
        movement = GetComponent<PurpleRangedMovement>();
    }

    protected override bool SkillReady => state != EnemyState.Skill && combat != null && combat.CanShoot
        && (state != EnemyState.Attacked || SM.IsFinished);//普通受击动画播放完再重新开弓，不抢过受击的优先级
    protected override void SkillSet() => combat.BeginShot(movement.DeltaX > 0f);
    protected override void HandleSkill()
    {
        SM.ChangeSpriteFace(combat.ShotFaceRight);
        SM.ChangeState(Attack);
    }
    protected override void CheckSkill()
    {
        if (combat == null || movement == null) return;
        combat.UpdateCheckFace(movement.DeltaX > 0f);
        if (state == EnemyState.Skill && EH.Attacked && !EH.Boost)
        {
            combat.CancelShot();
            SetState(EnemyState.Idle);//退出开弓调度，基类紧接着按本次伤害播放受击并执行击退
        }
        if (state == EnemyState.Skill) combat.CheckShot(SM.CurFrame);
        else combat.CancelShot();//受击、冰冻或吸附切出本轮动作后，不能再补出尚未生成的箭
    }
    protected override void SkillOver() => combat.CancelShot();

    private void LateUpdate()//本体换到第十帧的同一个渲染帧立即发射，不额外等待物理帧
    {
        if (state == EnemyState.Skill && combat != null) combat.CheckShot(SM.CurFrame);
        else if (combat != null) combat.CancelShot();
    }
}

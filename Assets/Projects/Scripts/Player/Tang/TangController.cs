using UnityEngine;

//唐僧沿用公共状态优先级、两段跳和普通攻击，独有技能只接收当前动作帧。
public class TangController : PlayerController
{
    private TangCombat combat;//动作帧和技能特效之间的唯一转发入口
    private TangMovement movement;//视觉朝向与水柱的方向保持一致

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<TangCombat>();
        movement = GetComponent<TangMovement>();
    }

    protected override bool CanStartSkill() => combat.CanStartSkill();

    protected override void HandleNormalAttack()//普通攻击只有一招，每次真正开始动画才开启一次发射机会
    {
        base.HandleNormalAttack();
        combat.BeginNormalAttack();
    }

    protected override void HandleRiseAttack()//每次真正进入空中攻击动画，只开启本轮一次发射机会
    {
        base.HandleRiseAttack();
        combat.BeginRiseAttack();
    }

    protected override void StartSkill()
    {
        base.StartSkill();
        movement.BeginSkillMove(combat.Skilled);//开始即悬停，不等下一次物理步才清除跳跃速度
        combat.BeginSkill();
    }

    protected override void UpdateMovement()//保持公共移动规则，施法时不遗留旧朝向
    {
        if (state == PlayerState.Skill) movement.CheckSkillMove(combat.Skilled, SM.CurFrame);
        else base.UpdateMovement();
        if (state == PlayerState.Skill) SM.ChangeSpriteFace(movement.FaceRight);
    }

    protected override void SpecialSkill()
    {
        base.SpecialSkill();
        combat.CheckSkillEffect(SM.CurFrame, movement.FaceRight);
    }

    protected override void SkillOver()//正常收招、受击、冰冻及换人都归还悬停接管的重力
    {
        movement.EndSkillMove();
        base.SkillOver();
    }

    private void LateUpdate()//本体播放器在Update换帧后同步显示，避免特效落后一个物理步
    {
        if (combat == null || movement == null) return;
        if (state == PlayerState.NormalAttack)
            combat.CheckNormalAttack(SM.CurFrame, movement.FaceRight);
        else if (state == PlayerState.RiseAttack)
            combat.CheckRiseAttack(SM.CurFrame, movement.FaceRight);
        else if (state == PlayerState.Skill && (combat.Skilled == 2 || combat.Skilled == 3))
            combat.CheckSkillEffect(SM.CurFrame, movement.FaceRight);
    }
}

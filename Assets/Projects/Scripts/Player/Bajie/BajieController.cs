using UnityEngine;

public class BajieController : PlayerController
{
    private BajieCombat BC;
    private BajieMovement BM;//八戒自己的施法移动规则
    protected override void Awake()
    {
        base.Awake();
        BC=GetComponent<BajieCombat>();
        BM=GetComponent<BajieMovement>();
    }
    protected override bool CanStartSkill() => BC.CanStartSkill();
    protected override void StartSkill()
    {
        ComboOver();//主动释放技能可以结束反甲，圣盾不切换人物动作，因此不会经过这里
        BC.BeginSkill();
        BM.Skilled();
        BM.CheckSkillMove(BC.Skilled, 0);//第一帧立即停止领域移动，吸附则保留当前跳跃速度
        SM.ChangeSpriteFace(BM.FaceRight);
    }
    protected override void UpdateMovement()
    {
        if (state == PlayerState.Skill)
        {
            BM.CheckSkillMove(BC.Skilled, SM.CurFrame);
            SM.ChangeSpriteFace(BM.FaceRight);
        }
        else if (BC.Reflecting) BM.CheckComboMove(1);
        else if (state == PlayerState.NormalAttack && BC.Attacked == 3 && !BM.IsMoving)
            BM.CheckNormalAttackMove(SM.CurFrame);//第三段的主动前冲不当作玩家跑步输入
        else base.UpdateMovement();
    }
    protected override void HandleNormalAttack()
    {
        BM.BeginNormalAttack();
        base.HandleNormalAttack();
    }
    protected override void HandleJump()
    {
        BM.CheckNormalHorizonMove();//跳跃打断第三段时，不沿用冲撞的水平速度
        base.HandleJump();
    }

    protected override bool TryPriorityComboAttack()
    {
        if (state == PlayerState.ComboAttack && BC.Comboed == 1)
        {
            if (BC.Reflecting && IR.QuickDownHeld)
            {
                BM.CheckComboMove(1);
                SM.ChangeSpriteFace(BM.FaceRight);
                return true;//保持反甲循环，跳跃、落地和普通攻击都不能覆盖动画
            }
            ComboOver();
            BM.AllowJump(true);
        }
        if (!BC.ConsumeReflectionRequest()) return false;
        if (!BC.TryBeginCombo(1)) return false;
        AttackOver();
        ComboOver();
        RiseOver();
        BC.BeginReflection();
        BC.Comboed = 1;
        BM.AllowJump(false);
        BM.BeginReflectionMove();
        SM.ChangeSpriteFace(BM.FaceRight);
        SetState(PlayerState.ComboAttack);
        return true;
    }

    protected override void ComboOver()
    {
        BC.EndReflection();
        base.ComboOver();
    }
    protected override bool ComboEnd => !BC.Reflecting;//松键后旧Combo状态也必须结束，不能再用-1访问招式数据
    protected override void SpecialSkill()
    {
        if (BC.Skilled == 2)
            BC.CheckSuction(SM.CurFrame);
        if (BC.Skilled == 3)
            BC.CheckYield(SM.CurFrame);
    }
    protected override void CompleteSkill() => BC.CompleteSkill();
    protected override void SkillOver()
    {
        BM.EndSkillMove();
        base.SkillOver();
    }
}

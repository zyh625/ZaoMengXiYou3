using UnityEngine;

public class WukongController : PlayerController
{
    private WukongCombat WC;
    private WukongMovement WM;
    protected override void Awake()
    {
        base.Awake();
        WC = GetComponent<WukongCombat>();
        WM= GetComponent<WukongMovement>();
    }
    protected override void UpdateMovement()
    {
        if (WC.Skilled != -1)
            WM.CheckSkillMove(WC.Skilled, WC.SkillThreeCur);
        else if (state == PlayerState.ComboAttack && WC.Comboed == 2)
            WM.CheckComboMove(WC.Comboed);
        else
            base.UpdateMovement();
    }
    //技能攻击箱沿用施法开始时的方向，不随施法期间的移动输入翻转。
    protected override bool SkillFaceRight => WM.SkillFaceRight;
    protected override void CheckImmune()
    {
        if (WC.Skilled == 2) return;
        int immune = WC.CheckInvincible(SM.CurFrame);
        if (immune == 0 || immune == 3) return;
        if (immune == 1)
            checkAttacked.gameObject.SetActive(false);
        else
            checkAttacked.gameObject.SetActive(true);
    }
    protected override void SkillOver()
    {
        WC.SkillThreeCur = 0;
        base.SkillOver();
    }
    protected override void StartSkill()
    {
        WM.Skilled();
    }
    protected override void SpecialSkill()
    {
        if (WC.Skilled == 3)
        {
            WC.CheckSkillThree(SM.CurFrame);
            if (WC.SkillThreeCur == 2)
            {
                SM.Pause();
                if (WM.AirState() == 0)
                {
                    SM.Resume();
                    WC.SkillThreeCur = 3;
                }
            }
        }
    }
    protected override bool ComboEnd => (WC.Comboed == 1 && SM.IsFinished)
        || (WC.Comboed == 2 && (!WM.IsMoving || !IR.AttackHeld));
    protected override bool TryComboAttack()
    {
        if (WM.IsMoving&&WC.AttackThisFrame)
        {
            WC.AttackThisFrame = false;
            WC.Comboed = 2;
            SetState(PlayerState.ComboAttack);
            return true;
        }
        if (PI.IsInteracting() && WC.AttackThisFrame)
        {
            WC.AttackThisFrame = false;
            WC.Comboed = 1;
            SetState(PlayerState.ComboAttack);
            return true;
        }
        return false;
    }
}

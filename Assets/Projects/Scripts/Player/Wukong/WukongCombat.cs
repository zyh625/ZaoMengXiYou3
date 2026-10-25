using UnityEngine;

public class WukongCombat : PlayerCombat
{
    [SerializeField] private int[] SkillThreeState;//到第几帧换阶段，整个运动过程类似尖三角
    public int SkillThreeCur = 0;//当前为技能三的哪一阶段，起跳->攻击->下落->起立
    private int skillThreeAttackIdx = -1;
    private bool SkillThreeBoxHasShut = true;
    [SerializeField] private float skillThreeGroundRatio = 1.5f;
    [SerializeField] private int SkillThreeGroundOpenIdx;//空斩落地攻击开始帧，沿用原来的独立配置
    private PlayerAttackBox skillThreeBox;
    private int invincibleState = 0;
    [SerializeField] private int Skill1ImmuneBegin, Skill1ImmuneEnd, Skill3ImmuneBegin, Skill3ImmuneEnd;

    private void Start() => skillThreeBox = GetAction("Skill3").box.GetComponent<PlayerAttackBox>();

    public override void OnSkill(int k)
    {
        if (Skilled != -1) return;
        skillThreeAttackIdx = -1;
        base.OnSkill(k);
    }

    public override void SkillEnd() => invincibleState = 0;

    //空斩落地时会再次开启同一个攻击箱，不能被普通技能的关闭判断提前关闭。
    public override bool SkillShut(int idx, int k) =>
        base.SkillShut(idx, k) && (Skilled != 3 || idx < SkillThreeGroundOpenIdx);

    public void CheckSkillThree(int idx)
    {
        if (SkillThreeCur == 0 && idx >= SkillThreeState[0] + 1)
            SkillThreeCur = 1;
        else if (SkillThreeCur == 1)
        {
            if (idx >= SkillThreeState[1] + 1) SkillThreeCur = 2;
            else if (idx != skillThreeAttackIdx)//斩击动画按帧叠加
            {
                skillThreeAttackIdx = idx;
                skillThreeBox.UpdateAttack();
            }
        }
        else if (SkillThreeCur == 2 && idx >= SkillThreeState[2] + 1) SkillThreeCur = 3;
        if (SkillThreeCur == 3 && idx >= SkillThreeGroundOpenIdx
            && skillThreeAttackIdx < SkillThreeGroundOpenIdx)
        {
            skillThreeBox.gameObject.SetActive(true);
            skillThreeBox.ChangeAttack(skillThreeGroundRatio);
            SkillThreeBoxHasShut = false;
            skillThreeAttackIdx = idx;
            skillThreeBox.UpdateAttack();
        }
        if (SkillThreeCur == 3 && idx >= SkillThreeGroundOpenIdx + 1 && !SkillThreeBoxHasShut)//只开启一帧
        {
            skillThreeBox.gameObject.SetActive(false);
            skillThreeAttackIdx = idx;
            SkillThreeBoxHasShut = true;
        }
    }

    public int CheckInvincible(int idx)
    {
        if (invincibleState == 0 && ((Skilled == 1 && idx >= Skill1ImmuneBegin)
            || (Skilled == 3 && idx >= Skill3ImmuneBegin)))
            invincibleState = 1;
        else if (invincibleState == 1 && ((Skilled == 1 && idx >= Skill1ImmuneEnd)
            || (Skilled == 3 && idx >= Skill3ImmuneEnd)))
            invincibleState = 2;
        else if (invincibleState == 2 && ((Skilled == 1 && idx > Skill1ImmuneEnd)
            || (Skilled == 3 && idx > Skill3ImmuneEnd)))
            invincibleState = 3;
        return invincibleState;
    }
}

using UnityEngine;

//唐僧的两种攻击技能在空中施法时悬停，其他时间沿用公共移动与两段跳。
public class TangMovement : PlayerMovement
{
    private bool holdingAirSkill;//只在本次技能从空中开始时接管位置与重力
    private Vector2 skillPosition;//空中释放时固定的是施法起始位置，不追随后续移动输入
    private float skillGravity;//技能结束或打断后恢复原来的重力，不永久停在空中

    public void BeginSkillMove(int skill)
    {
        EndSkillMove();
        if ((skill != 2 && skill != 3) || IsOnGroundNow()) return;
        skillPosition = body.position;
        skillGravity = body.gravityScale;
        holdingAirSkill = true;
        HoldAirSkill();
    }

    public override void CheckSkillMove(int skill, int state)
    {
        if (HoldStatusPosition()) return;
        if (holdingAirSkill) HoldAirSkill();
        else CheckNormalHorizonMove();
    }

    private void HoldAirSkill()//不用追加永久位置约束，保留通用冰冻状态对约束的来源管理
    {
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.position = skillPosition;
    }

    public void EndSkillMove()
    {
        if (!holdingAirSkill) return;
        holdingAirSkill = false;
        body.gravityScale = skillGravity;
        body.linearVelocity = Vector2.zero;
    }
}

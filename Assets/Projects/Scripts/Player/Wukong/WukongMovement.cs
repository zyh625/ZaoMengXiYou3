using System;
using UnityEngine;

public class WukongMovement : PlayerMovement
{
    [SerializeField] private float flameFlashSpeed, airSlashHorizon,
        airSlashUp,airSlashDown,comboSpeed;//烈焰闪、空斩、戳棍对应速度
    private bool skillFaceRight;
    public bool SkillFaceRight => skillFaceRight;
    public override void CheckSkillMove(int skill,int state)
    {
        UpdateDirect();//与重构前一样更新输入方向，技能运动和攻击箱仍使用skillFaceRight锁定方向
        if (HoldStatusPosition()) return;//融化时烈焰闪、空斩仍走原技能阶段，但不能写入速度或直接挪动刚体
        Vector2 velocity = body.linearVelocity;
        switch (skill)
        {
            case 1: velocity.x = (skillFaceRight ? 1f : -1f) * flameFlashSpeed;break;
            case 2:velocity = Vector2.zero; break;
            case 3: SkillThreeMove(state);velocity = Vector2.zero; break;
        }
        body.linearVelocity = velocity;
    }
    private void SkillThreeMove(int state)
    {
        if (state == 1 || state == 3) return;
        Vector2 dir = new((skillFaceRight ? 1f : -1f) * airSlashHorizon,
            state == 0 ? airSlashUp : -airSlashDown);
        body.position += Time.fixedDeltaTime * dir;
    }
    public override void CheckComboMove(int combo)
    {
        if (!IsMoving)
        {
            Stop();return;
        }
        if (combo == 1) CheckNormalHorizonMove();
        else MoveWithInput(comboSpeed);
    }
    public override void Skilled()=>skillFaceRight = right;
    public override void HasSkilled() => right = skillFaceRight;
}

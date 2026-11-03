using UnityEngine;

public class BajieMovement : PlayerMovement
{
    private Vector2 skillPosition;//领域施法开始时的位置，整个动作期间保持不变
    private bool skillFaceRight;//领域施法时锁定的朝向
    private bool holdingGroundSkill;//只有领域施法才暂时停用重力
    private float skillGravity;//恢复施法前的重力，不影响吸附技能的跳跃速度
    private bool reflectionFaceRight;//反甲期间保持进入时的朝向，不响应左右移动转身
    [SerializeField, Min(0f)] private float attackThreeSpeed = 6f;//第三段普攻的主动冲撞速度，不改基础走跑速度
    [SerializeField, Min(0)] private int attackThreeMoveStartFrame = 4, attackThreeMoveEndFrame = 10;//冲撞的动作帧窗口，左闭右开，从0开始
    private bool normalAttackFaceRight;//普攻开始时的方向，冲撞期间不会自行反转

    public void BeginNormalAttack() => normalAttackFaceRight = right;

    public void CheckNormalAttackMove(int frame)//只由第三段普攻调用，前摇与收招时停止冲撞
    {
        right = normalAttackFaceRight;
        float speed = frame >= attackThreeMoveStartFrame && frame < attackThreeMoveEndFrame ? attackThreeSpeed : 0f;
        SetHorizonSpeed(normalAttackFaceRight ? speed : -speed);
    }

    public void BeginReflectionMove()//地面停止水平运动，空中立即进入快速下落
    {
        reflectionFaceRight = right;
        ForceQuickDown();
        CheckComboMove(1);
    }

    public override void CheckComboMove(int combo)
    {
        if (combo != 1)
        {
            base.CheckComboMove(combo);
            return;
        }
        right = reflectionFaceRight;
        SetHorizonSpeed(0f);
    }

    public override void Skilled()
    {
        skillPosition = body.position;
        skillFaceRight = right;
    }

    public override void CheckSkillMove(int skill, int state)
    {
        if (HoldStatusPosition()) return;//融化时吸附和领域动作照常播放，不再由本技能接管位置或重力
        if (skill != 3)
        {
            CheckNormalHorizonMove();//吸附使用原有地面、跑步和空中速度，并保留竖直速度
            return;
        }
        if (!holdingGroundSkill)
        {
            skillGravity = body.gravityScale;
            holdingGroundSkill = true;
        }
        right = skillFaceRight;
        body.gravityScale = 0f;
        body.position = skillPosition;
        body.linearVelocity = Vector2.zero;
    }

    public void EndSkillMove()//完整结束、受击和换人均恢复原有物理参数
    {
        if (!holdingGroundSkill) return;
        holdingGroundSkill = false;
        body.gravityScale = skillGravity;
    }
}

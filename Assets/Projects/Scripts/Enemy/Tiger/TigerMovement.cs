using System;
using UnityEngine;

public class TigerMovement : EnemyMovement
{
    [SerializeField] private float attackTwoSpeed;//突刺的速度
    [SerializeField]private int startLunge, endLunge;
    public int StartLunge => startLunge;
    public int EndLunge => endLunge;
    private int curState = 0;
    public void AttackTwoEnd() => curState = 0;
    public bool AttackFaceRight;
    public int CheckAttackTwoMove(int idx)
    {
        if (idx >= startLunge && curState == 0)
        {
            curState = 1;
            Vector2 velocity = body.linearVelocity;
            velocity.x = (AttackFaceRight ? 1f : -1f) * attackTwoSpeed;
            body.linearVelocity = velocity;
        }
        else if (idx >= endLunge && curState == 1)
        {
            curState = 2;
            Stop();
        }
        return curState;
    }
}

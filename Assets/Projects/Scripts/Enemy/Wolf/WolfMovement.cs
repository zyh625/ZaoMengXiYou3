using UnityEngine;

public class WolfMovement : EnemyMovement
{
    [SerializeField] private float lungeSpeed;//突刺的速度
    [SerializeField] private int startLunge, endLunge;
    public int StartLunge => startLunge;
    public int EndLunge => endLunge;
    public bool AttackFaceRight;
    public void CheckAttackTwoMove(int idx)
    {
        if (idx >= startLunge && idx<endLunge)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = (AttackFaceRight ? 1f : -1f) * lungeSpeed;
            body.linearVelocity = velocity;
        }
        else if (idx >= endLunge)
            Stop();
    }
}

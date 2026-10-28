using UnityEngine;

public class TitanMovement : EnemyMovement, IEnemyArenaBounds
{
    [SerializeField] private float lungeDuration,lungeSpeed;//来回冲刺，限定时间和速度
    [SerializeField, Min(0f)] private float rageMoveMultiplier = 1.3f;//怒气无双的移速倍率，同时用于走动与冲撞
    private bool rageBoost;//只控制怒气来源的加速，不改写原始移动与冲撞速度
    protected override float MoveSpeedMultiplier => rageBoost ? rageMoveMultiplier : 1f;
    public void SetRageBoost(bool active) => rageBoost = active;//无双结束和死亡只撤销自己的速度来源
    private float edgeL, edgeR;
    private float beginTime = -1f;
    public bool BeginLunge => beginTime != -1f;
    public void StartLunge()
    {
        beginTime= Time.time;
        Lunge();
    }
    public bool StopLunge => BeginLunge && Time.time - beginTime >= lungeDuration;
    public bool AttackFaceRight;
    public void LungeOver() => beginTime = -1f;
    public bool Lunging()
    {
        if ((!AttackFaceRight&&body.position.x <= edgeL) || 
            (AttackFaceRight&&body.position.x >= edgeR))
            AttackFaceRight = !AttackFaceRight;//转向
        Lunge();
        return AttackFaceRight;
    }
    public void SetEdge(float left,float right)
    {
        edgeL= left; edgeR= right;
    }
    public void SetArenaBounds(float left, float right) => SetEdge(left, right);//通用关卡通过接口注入边界，保留原SetEdge入口
    private void Lunge()
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = lungeSpeed * MoveSpeedMultiplier * (AttackFaceRight ? 1f : -1f);
        body.linearVelocity = velocity;
    }
}

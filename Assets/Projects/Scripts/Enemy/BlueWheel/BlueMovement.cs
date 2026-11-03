using UnityEngine;
using zmxy.GamePlay;

public class BlueMovement : EnemyMovement
{
    [SerializeField, Min(0f)] private float hiddenSpeedMultiplier = 1.2f;//隐身倍率叠乘原移速、怒气与减速。
    [SerializeField, Min(0.5f)] private float hiddenDistance = 4f;//隐身与玩家保持的最近距离。
    [SerializeField, Min(0.2f)] private float patrolWidth = 3f;//在玩家同侧来回走动的区间宽度。
    [SerializeField, Min(0.02f)] private float turnTolerance = 0.15f;//到端点后才换目标，避免原地反复翻转。
    private BlueWheelCombat combat;//不另写Awake，保留EnemyMovement初始化。
    private CameraManager cameraManager;
    private bool patrolInitialized, towardFar;
    private float patrolSide;
    public float PatrolDirection { get; private set; }//控制器读取本步方向翻转精灵。
    protected override float MoveSpeedMultiplier
    {
        get
        {
            if (combat == null) combat = GetComponent<BlueWheelCombat>();
            return combat != null && combat.IsHidden ? Mathf.Max(0f, hiddenSpeedMultiplier) : 1f;
        }
    }
    public void ResetHiddenPatrol()//每次隐身不沿用上一轮端点。
    {
        patrolInitialized = false;
        PatrolDirection = 0f;
    }
    public void PatrolHidden(bool slow)//共用速度入口，冻结、融化和流沙仍然有效。
    {
        if (!patrolInitialized)
        {
            patrolInitialized = true;
            patrolSide = DeltaX > 0f ? -1f : 1f;
            towardFar = true;
        }
        if (cameraManager == null && Camera.main != null)
            cameraManager = Camera.main.GetComponent<CameraManager>();
        float playerX = transform.position.x + DeltaX;
        if ((transform.position.x - playerX) * patrolSide < -0.5f)
        {
            patrolSide = -patrolSide;
            towardFar = true;//玩家越过敌人后向新的外侧退开，不横穿玩家去追旧端点。
        }
        float near = playerX + patrolSide * Mathf.Max(0.5f, hiddenDistance);
        float far = playerX + patrolSide * (Mathf.Max(0.5f, hiddenDistance) + Mathf.Max(0.2f, patrolWidth));
        if (cameraManager != null && cameraManager.CameraHalf > 0f)
        {
            float margin = BodyCollider != null ? BodyCollider.bounds.extents.x + 0.2f : 1f;
            float left = cameraManager.EdgeL - cameraManager.CameraHalf + margin;
            float right = cameraManager.EdgeR + cameraManager.CameraHalf - margin;
            if (left <= right)
            {
                near = Mathf.Clamp(near, left, right);
                far = Mathf.Clamp(far, left, right);
            }
        }
        float destination = towardFar ? far : near;
        float distance = destination - transform.position.x;
        if (Mathf.Abs(distance) <= Mathf.Max(0.02f, turnTolerance))
        {
            towardFar = !towardFar;
            destination = towardFar ? far : near;
            distance = destination - transform.position.x;
        }
        if (Mathf.Abs(distance) <= Mathf.Max(0.02f, turnTolerance))
        {
            PatrolDirection = 0f;
            Stop();
            return;//边缘把两端夹成一点时站定，不来回翻转。
        }
        PatrolDirection = Mathf.Sign(distance);
        MoveAlong(PatrolDirection, slow);
        if (body != null)
        {
            Vector2 velocity = body.linearVelocity;
            float limit = distance / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            velocity.x = distance > 0f ? Mathf.Min(velocity.x, limit) : Mathf.Max(velocity.x, limit);
            body.linearVelocity = velocity;//再高的自身移速也只抵达端点，不跨点反复转向；流沙仍交给共用末步结算。
        }
    }
}

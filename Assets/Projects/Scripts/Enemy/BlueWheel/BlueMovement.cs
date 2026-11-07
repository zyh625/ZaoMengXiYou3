using UnityEngine;
using zmxy.GamePlay;

public class BlueMovement : EnemyMovement
{
    [SerializeField, Min(0f)] private float hiddenSpeedMultiplier = 1.2f;//隐身倍率叠乘原移速、怒气与减速。
    [SerializeField, Min(0.5f)] private float hiddenDistance = 4f;//隐身与玩家保持的最近距离。
    [SerializeField, Min(0.2f)] private float patrolWidth = 3f;//在玩家同侧来回走动的区间宽度。
    [SerializeField, Min(0.02f)] private float turnTolerance = 0.15f;//到端点后才换目标，避免原地反复翻转。
    [Header("非隐身时保持距离")]
    [SerializeField, Min(0.5f)] private float keepNear = 4f;//玩家比这更近时后退
    [SerializeField, Min(0.5f)] private float keepFar = 6.5f;//玩家比这更远时靠近，两者之间站定投掷
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
    public bool HasTarget => Target != null;
    public float KeepDistanceDirection { get; private set; }//本步移动方向：1右、-1左、0站定

    //远了靠近、近了后退，保持在飞轮射程内；后退被屏幕边缘挡住时原地站定，不走出画面。
    public void KeepDistance(bool slow)
    {
        float dis = DeltaX, distance = Mathf.Abs(dis), toward = Mathf.Sign(dis);
        float near = Mathf.Min(keepNear, keepFar), far = Mathf.Max(keepFar, near + 0.5f);
        float direction = distance > far ? toward : distance < near ? -toward : 0f;
        if (direction != 0f && BlockedByScreenEdge(direction)) direction = 0f;
        KeepDistanceDirection = direction;
        if (direction == 0f) Stop();
        else MoveAlong(direction, slow);
    }

    private bool BlockedByScreenEdge(float direction)
    {
        if (cameraManager == null && Camera.main != null) cameraManager = Camera.main.GetComponent<CameraManager>();
        if (cameraManager == null || cameraManager.CameraHalf <= 0f) return false;
        float margin = BodyCollider != null ? BodyCollider.bounds.extents.x + 0.2f : 1f;
        float x = transform.position.x;
        return direction < 0f ? x <= cameraManager.EdgeL - cameraManager.CameraHalf + margin
                              : x >= cameraManager.EdgeR + cameraManager.CameraHalf - margin;
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
        if ((transform.position.x - playerX) * patrolSide < -BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.1", 0.5f))
        {
            patrolSide = -patrolSide;
            towardFar = true;//玩家越过敌人后向新的外侧退开，不横穿玩家去追旧端点。
        }
        float near = playerX + patrolSide * Mathf.Max(BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.2", 0.5f), hiddenDistance);
        float far = playerX + patrolSide * (Mathf.Max(BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.3", 0.5f), hiddenDistance) + Mathf.Max(BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.4", 0.2f), patrolWidth));
        if (cameraManager != null && cameraManager.CameraHalf > 0f)
        {
            float margin = BodyCollider != null ? BodyCollider.bounds.extents.x + BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.5", 0.2f) : 1f;
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
        if (Mathf.Abs(distance) <= Mathf.Max(BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.6", 0.02f), turnTolerance))
        {
            towardFar = !towardFar;
            destination = towardFar ? far : near;
            distance = destination - transform.position.x;
        }
        if (Mathf.Abs(distance) <= Mathf.Max(BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.7", 0.02f), turnTolerance))
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
            float limit = distance / Mathf.Max(BalanceRuntime.Number(this, "BlueMovement.PatrolHidden.8", 0.0001f), Time.fixedDeltaTime);
            velocity.x = distance > 0f ? Mathf.Min(velocity.x, limit) : Mathf.Max(velocity.x, limit);
            body.linearVelocity = velocity;//再高的自身移速也只抵达端点，不跨点反复转向；流沙仍交给共用末步结算。
        }
    }
}

using UnityEngine;

[DefaultExecutionOrder(100)] // Reassert the prescribed roll velocity after the common field-motion step.
public sealed class RollingFurnaceMovement : EnemyMovement, IEnemyArenaBounds
{
    [SerializeField] private float rollSpeed = 6f, jumpSpeed = 6f;
    private float savedRollGravity, remaining, direction, previousX;
    private bool rolling, gravityOwned, arenaSet, gravityRecoveryPending;
    private Vector2 rollVelocity;
    private float arenaLeft, arenaRight;
    private Camera view;
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[16];
    public bool Rolling => rolling;
    public float Remaining => remaining;
    public void ResetMotion()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        rolling = gravityOwned = gravityRecoveryPending = false;
        body.gravityScale = BalanceRuntime.Number(this, "RollingFurnaceMovement.ResetMotion.1", 0.9f); body.linearVelocity = Vector2.zero;
    }
    private void FixedUpdate()
    { if (rolling) { body.gravityScale = 0f; body.linearVelocity = rollVelocity; } }
    private void LateUpdate()
    {
        // Carry saves gravity before it notifies the controller. Repair that saved zero on release,
        // without releasing the other system's freeze/position constraints or its active zero gravity.
        if (!gravityRecoveryPending || IsSuctionActive || IsCarried || IsStunned || (GetComponent<Health>().Status?.BlocksActions ?? false)) return;
        body.gravityScale = savedRollGravity; gravityRecoveryPending = false;
    }
    public void SetArenaBounds(float left, float right)
    { arenaSet = true; arenaLeft = left; arenaRight = right; }
    public void WalkDirection(float sign, bool slow) => MoveAlong(sign, slow);
    public void BeginRoll(float sign)
    {
        direction = sign; remaining = BalanceRuntime.Number(this, "RollingFurnaceMovement.BeginRoll.1", 8f); previousX = body.position.x;
        savedRollGravity = body.gravityScale; gravityOwned = true; body.gravityScale = 0f; rolling = true;
        rollVelocity = Vector2.zero; body.linearVelocity = Vector2.zero;
    }
    // 0 rolling, 1 wall/camera edge, 2 travelled the complete eight-unit path.
    public int StepRoll()
    {
        remaining = Mathf.Max(0f, remaining - Mathf.Abs(body.position.x - previousX)); previousX = body.position.x;
        if (remaining <= BalanceRuntime.Number(this, "RollingFurnaceMovement.StepRoll.1", 0.001f)) { StopRoll(); return 2; }
        float step = Mathf.Min(rollSpeed * Time.fixedDeltaTime, remaining);
        Collider2D shell = BodyCollider;
        float left = float.NegativeInfinity, right = float.PositiveInfinity;
        if (view == null) view = Camera.main;
        if (view != null && view.orthographic)
        { float half = view.orthographicSize * view.aspect; left = view.transform.position.x - half; right = view.transform.position.x + half; }
        if (arenaSet) { left = Mathf.Max(left, arenaLeft); right = Mathf.Min(right, arenaRight); }
        float clearance = direction > 0f ? right - shell.bounds.max.x : shell.bounds.min.x - left;
        ContactFilter2D filter = new(); filter.SetLayerMask(1 << 6); filter.useTriggers = false;
        int count = shell.Cast(Vector2.right * direction, filter, wallHits, step + BalanceRuntime.Number(this, "RollingFurnaceMovement.StepRoll.2", 0.02f));
        bool wall = clearance <= step + BalanceRuntime.Number(this, "RollingFurnaceMovement.StepRoll.3", 0.02f);
        for (int i = 0; i < count; i++)
            if (wallHits[i].normal.x * direction < -BalanceRuntime.Number(this, "RollingFurnaceMovement.StepRoll.4", 0.5f)) wall = true; // Ground contact is never a wall.
        if (wall) { body.linearVelocity = Vector2.zero; rolling = false; return 1; }
        body.gravityScale = 0f; rollVelocity = new Vector2(direction * step / Time.fixedDeltaTime, 0f);
        body.linearVelocity = rollVelocity; return 0;
    }
    public void Rebound() { direction = -direction; previousX = body.position.x; rolling = true; rollVelocity = Vector2.zero; body.gravityScale = 0f; }
    public void StopRoll()
    {
        rolling = false;
        if (body == null) return;
        body.linearVelocity = Vector2.zero;
        if (gravityOwned) { body.gravityScale = savedRollGravity; gravityRecoveryPending = true; }
        gravityOwned = false;
    }
    public void Jump() { body.linearVelocity = new Vector2(0f, jumpSpeed); }
    public void HoldStill() { if (body != null) body.linearVelocity = Vector2.zero; }
}

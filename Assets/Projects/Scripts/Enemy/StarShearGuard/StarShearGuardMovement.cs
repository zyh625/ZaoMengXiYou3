using UnityEngine;

public sealed class StarShearGuardMovement : EnemyMovement
{
    [SerializeField, Min(.01f)] private float motionBaseSpeed = 1f;
    [SerializeField, Min(0f)] private float backstepDistance = 2.5f;
    [SerializeField, Min(.02f)] private float backstepDuration = .4f;
    [SerializeField, Min(0f)] private float followStepDistance = .25f;
    [SerializeField, Min(.02f)] private float followStepDuration = .15f;
    private float startX, distance, duration, elapsed, direction, speedMultiplier = 1f;
    public bool Stepping { get; private set; }
    protected override float MoveSpeedMultiplier => speedMultiplier;

    public void BeginBackstep(bool right) => BeginStep(right ? -1f : 1f, backstepDistance, backstepDuration);
    public void BeginFollowStep(bool right) => BeginStep(right ? 1f : -1f, followStepDistance, followStepDuration);
    private void BeginStep(float facing, float units, float seconds)
    {
        startX = body.position.x; direction = facing; distance = units; duration = seconds;
        elapsed = 0f; Stepping = units > 0f;
    }
    // Called only inside EnemyController's shared BeginMotionStep/EndMotionStep pair.
    // MoveAlong preserves gravity, freeze locks, slows, rebirth and field movement.
    public void TickStep(bool slow)
    {
        if (!Stepping) { Stop(); return; }
        float remaining = Mathf.Max(0f, distance - (body.position.x - startX) * direction);
        if (remaining <= BalanceRuntime.Number(this, "StarShearGuardMovement.TickStep.1", 0.001f) || elapsed >= duration)
        { CancelStep(); Stop(); return; }
        float speed = Mathf.Min(distance / duration, remaining / Time.fixedDeltaTime);
        speedMultiplier = speed / Mathf.Max(BalanceRuntime.Number(this, "StarShearGuardMovement.TickStep.2", 0.01f), motionBaseSpeed * RebirthScaling.Current.Movement);
        MoveAlong(direction, slow);
        elapsed += Time.fixedDeltaTime;
    }
    public void CancelStep() { Stepping = false; speedMultiplier = 1f; }
}

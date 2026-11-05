using UnityEngine;

public sealed class ResonantStarOwlMovement : EnemyMovement
{
    private Health hp;
    private bool initialized;
    private int life;
    private float hoverY;
    public float FloorY { get; private set; }
    public bool Falling { get; private set; }
    private void Start() { hp = GetComponent<Health>(); hp.DamageReceived += OnDamage; hp.Died += OnDeath; }
    // Spawn positions are assigned after activation; initialize on the first motion step.
    public void StepFlight()
    {
        if (hp == null) hp = GetComponent<Health>();
        if (!initialized || life != hp.LifeVersion)
        {
            initialized = true; life = hp.LifeVersion; Falling = false;
            var floor = Physics2D.Raycast(body.position + Vector2.up * BalanceRuntime.Number(this, "ResonantStarOwlMovement.StepFlight.1", 3f), Vector2.down, BalanceRuntime.Number(this, "ResonantStarOwlMovement.StepFlight.2", 40f), 1 << 6);
            FloorY = floor.collider != null ? floor.point.y : body.position.y - BalanceRuntime.Number(this, "ResonantStarOwlMovement.StepFlight.3", 2.2f);
            hoverY = FloorY + BalanceRuntime.Number(this, "ResonantStarOwlMovement.StepFlight.4", 2.2f); body.gravityScale = 0f; body.bodyType = RigidbodyType2D.Kinematic;
            body.position = new Vector2(body.position.x, hoverY); body.linearVelocity = Vector2.zero;
        }
        if (IsExternallyControlled && !IsSuctionActive && !IsStunned && !IsCarried) { body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 1f; }
        if (Falling)
        {
            body.gravityScale = 1f; CheckIsGround();
            if (IsGround && !hp.Dead && !hp.Attacked && !IsExternallyControlled)
            { Falling = false; body.gravityScale = 0f; }
        }
    }
    public void Hover(float direction, bool slow, bool rise = false)
    {
        if (Falling || IsExternallyControlled || hp.Status != null && hp.Status.BlocksMovement) return;
        body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0f; MoveAlong(direction, slow);
        float y = hoverY + (rise ? BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.1", 0.65f) : 0f) + Mathf.Sin(Time.time * BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.2", 2f)) * BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.3", 0.12f);
        Vector2 velocity = new Vector2(body.linearVelocity.x, Mathf.Clamp((y - body.position.y) * BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.4", 3f), -BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.5", 0.9f), BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.6", 0.9f)));
        // Flying bodies do not resolve ground contacts. Sweep first so they cannot enter terrain.
        Vector2 delta = velocity * Time.fixedDeltaTime;
        if (delta.sqrMagnitude > 0f)
        {
            var shape = GetComponent<BoxCollider2D>();
            var wall = Physics2D.BoxCast(shape.bounds.center, shape.bounds.size, 0f, delta.normalized, delta.magnitude + BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.7", 0.03f), 1 << 6);
            if (wall.collider != null) velocity *= Mathf.Clamp01((wall.distance - BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.8", 0.03f)) / delta.magnitude);
        }
        body.linearVelocity = velocity;
    }
    private void OnDamage(DamageInfo damage, float actual) { if (actual > 0f && damage.Launch) KnockDown(); }
    private void OnDeath(DamageInfo damage) => KnockDown();
    public void KnockDown() { Falling = true; body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 1f; }
    private void LateUpdate() { if (hp != null && hp.Dead) { body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 1f; } }
    private void OnDestroy() { if (hp != null) { hp.DamageReceived -= OnDamage; hp.Died -= OnDeath; } }
}

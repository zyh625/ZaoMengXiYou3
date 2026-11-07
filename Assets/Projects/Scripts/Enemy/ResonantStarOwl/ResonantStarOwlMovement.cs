using UnityEngine;

public sealed class ResonantStarOwlMovement : EnemyMovement
{
    private Health hp;
    private bool initialized;
    private int life;
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
            body.gravityScale = 0f; body.bodyType = RigidbodyType2D.Kinematic; body.linearVelocity = Vector2.zero;//出生点由关卡决定，不再贴到固定离地高度
        }
        FloorY = FindFloor(body.position);
        if (IsExternallyControlled && !IsSuctionActive && !IsStunned && !IsCarried) { body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 1f; }
        if (Falling)
        {
            body.gravityScale = 1f; CheckIsGround();
            if (IsGround && !hp.Dead && !hp.Attacked && !IsExternallyControlled)
            { Falling = false; body.gravityScale = 0f; }
        }
    }
    //地面检测：从给定点向下找最近的地面；找不到时按给定点下方 2.2 处估算
    public float FindFloor(Vector2 from)
    {
        var floor = Physics2D.Raycast(from + Vector2.up * BalanceRuntime.Number(this, "ResonantStarOwlMovement.StepFlight.1", 3f), Vector2.down, BalanceRuntime.Number(this, "ResonantStarOwlMovement.StepFlight.2", 40f), 1 << 6);
        return floor.collider != null ? floor.point.y : from.y - BalanceRuntime.Number(this, "ResonantStarOwlMovement.StepFlight.3", 2.2f);
    }
    public Vector2 Velocity => body.linearVelocity;
    //自由飞行：按给定速度移动，叠加轻微上下浮动；先扫掠一次，避免钻进地形
    public void Fly(Vector2 velocity)
    {
        if (Falling || IsExternallyControlled || hp.Status != null && hp.Status.BlocksMovement) return;
        body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0f;
        velocity.y += Mathf.Cos(Time.time * BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.2", 2f)) * BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.3", 0.12f) * 2f;
        Vector2 delta = velocity * Time.fixedDeltaTime;
        if (delta.sqrMagnitude > 0f)
        {
            var shape = GetComponent<BoxCollider2D>();
            //检测框四边各内缩0.05：贴着地面起飞时不会因为“起点已重叠”被判定为撞墙而原地不动
            Vector2 size = Vector2.Max((Vector2)shape.bounds.size - Vector2.one * 0.1f, Vector2.one * 0.05f);
            var wall = Physics2D.BoxCast(shape.bounds.center, size, 0f, delta.normalized, delta.magnitude + BalanceRuntime.Number(this, "ResonantStarOwlMovement.Hover.7", 0.03f), 1 << 6);
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

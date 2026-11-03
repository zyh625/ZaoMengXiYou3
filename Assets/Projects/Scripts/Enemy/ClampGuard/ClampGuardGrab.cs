using UnityEngine;

// Strong tether: ordinary movement/jump is held; skill displacement and strong control can break it.
// Input remains available so the victim can counter with a skill instead of being silently frozen.
[DisallowMultipleComponent, DefaultExecutionOrder(1500)]
public sealed class ClampGuardGrab : MonoBehaviour
{
    public const float Duration = 1.5f;
    private const float Skin = .025f;
    private ClampGuardCombat source;
    private Health health, sourceHealth;
    private PlayerController player;
    private PlayerMovement movement;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private int victimLife, sourceLife, ticks;
    private float beganAt, nextTick, savedGravity, scale;
    private float duration, tickInterval, tickDamage;//开始抓取时保存本次序列化参数快照。
    private int tickLimit;//时长内应结算的跳伤次数。
    private bool holding;
    private Vector2 expectedPosition, ownerPosition;
    private readonly RaycastHit2D[] casts = new RaycastHit2D[24];
    private readonly Collider2D[] solids = new Collider2D[24];
    public int TicksApplied => ticks;
    public int ThrowsApplied { get; private set; }
    public bool IsHolding(ClampGuardCombat owner) => holding && source == owner;

    private void Awake()
    {
        health = GetComponent<Health>(); player = GetComponent<PlayerController>();
        movement = GetComponent<PlayerMovement>(); body = GetComponent<Rigidbody2D>(); bodyCollider = GetComponent<Collider2D>();
    }
    private void OnEnable()
    { if (health != null) { health.DebuffsCleared += Cancel; health.Died += Died; } }
    public bool Begin(ClampGuardCombat owner)
    {
        if (holding || owner == null || player == null || GetComponent<BossController>() != null || body == null
            || bodyCollider == null || health == null || !health.CanReceiveControl(ControlStrength.Strong)
            || (health.Status?.BlocksMovement ?? false)) return false;
        sourceHealth = owner.GetComponent<Health>();
        if (sourceHealth == null || sourceHealth.Dead) return false;
        source = owner; sourceLife = sourceHealth.LifeVersion; victimLife = health.LifeVersion;
        duration = owner.GrabDuration; tickInterval = owner.GrabTickInterval; tickDamage = owner.GrabTickDamage;
        tickLimit = Mathf.Max(0, Mathf.FloorToInt(duration / tickInterval + .0001f));
        scale = owner.DamageMultiplier; beganAt = Time.time; nextTick = beganAt + tickInterval; ticks = 0; ThrowsApplied = 0;
        savedGravity = body.gravityScale; expectedPosition = body.position; ownerPosition = owner.transform.position;
        holding = true; movement?.SetMoveMultiplier(this, 0f); body.gravityScale = 0f; body.linearVelocity = Vector2.zero;
        return true;
    }
    private bool Invalid => source == null || !source.CanMaintainGrab || sourceHealth == null || sourceHealth.Dead
        || sourceHealth.LifeVersion != sourceLife || health == null || health.Dead || health.LifeVersion != victimLife
        || !player.isActiveAndEnabled || !health.CanReceiveControl(ControlStrength.Strong)
        || (health.Status?.BlocksMovement ?? false) || (GetComponent<ActorStun>()?.IsStunned ?? false)
        || ((Vector2)source.transform.position - ownerPosition).sqrMagnitude > .16f;
    private bool Displaced()
    {
        if ((body.position - expectedPosition).sqrMagnitude > .0144f
            || ((Vector2)transform.position - expectedPosition).sqrMagnitude > .0144f) return true;
        // Wukong's dash writes velocity; AirSlash and other teleports write position.
        return player.State() == PlayerState.Skill && body.linearVelocity.sqrMagnitude > .25f;
    }
    private void FixedUpdate()
    {
        if (!holding) return;
        if (Invalid || Displaced()) { Cancel(); return; }
        while (holding && ticks < tickLimit && Time.time + .0001f >= nextTick)
        {
            ticks++; nextTick = beganAt + (ticks + 1) * tickInterval;
            health.TakeDamage(new DamageInfo(source.transform, tickDamage * scale, false, false, null, false, true, DamageKind.Skill));
            if (!holding || Invalid) { Cancel(); return; }
        }
        if (Time.time >= beganAt + duration) { ThrowBehind(); return; }
        float side = source.AttackFaceRight ? 1f : -1f;
        Collider2D ownerBox = source.GetComponent<Collider2D>();
        float offset = bodyCollider.bounds.center.x - body.position.x;
        float destination = ownerBox.bounds.center.x + side * (ownerBox.bounds.extents.x + bodyCollider.bounds.extents.x + .12f) - offset;
        float remaining = side * (body.position.x - destination);
        if (remaining > 0f)
        {
            float step = Mathf.Min(remaining, 12f * Time.fixedDeltaTime);
            Vector2 direction = Vector2.left * side;
            var filter = new ContactFilter2D(); filter.SetLayerMask((1 << 6) | (1 << 11)); filter.useTriggers = false;
            int count = bodyCollider.Cast(direction, filter, casts, step + Skin);
            for (int i = 0; i < count; i++)
                if (Vector2.Dot(casts[i].normal, direction) < -.01f)
                    step = Mathf.Min(step, Mathf.Max(0f, casts[i].distance - Skin));
            expectedPosition += direction * step;
        }
        Hold();
    }
    private void LateUpdate()
    {
        if (!holding) return;
        if (Invalid || Displaced()) { Cancel(); return; }
        Hold();
    }
    private void Hold()
    {
        body.gravityScale = 0f;
        body.position = expectedPosition;
        transform.position = new Vector3(expectedPosition.x, expectedPosition.y, transform.position.z);
        body.linearVelocity = Vector2.zero;
    }
    private void ThrowBehind()
    {
        float side = source.AttackFaceRight ? 1f : -1f;
        Collider2D ownerBox = source.GetComponent<Collider2D>();
        Vector2 target = body.position;
        target.x = ownerBox.bounds.center.x - side * (ownerBox.bounds.extents.x + bodyCollider.bounds.extents.x + .35f)
            - (bodyCollider.bounds.center.x - body.position.x);
        target.y += .18f;
        Vector2 displacement = target - body.position;
        var filter = new ContactFilter2D(); filter.SetLayerMask(1 << 6); filter.useTriggers = false;
        int count = bodyCollider.Cast(displacement.normalized, filter, casts, displacement.magnitude + Skin);
        bool safe = true;
        for (int i = 0; i < count; i++)
            if (Vector2.Dot(casts[i].normal, displacement.normalized) < -.01f && casts[i].distance < displacement.magnitude) safe = false;
        Vector2 center = (Vector2)bodyCollider.bounds.center + displacement;
        int overlaps = Physics2D.OverlapBox(center, (Vector2)bodyCollider.bounds.size * .94f, 0f, filter, solids);
        if (overlaps > 0) safe = false;
        // Release before writing the throw so callbacks cannot throw the same target twice.
        Cancel();
        if (!safe || health.Dead || !isActiveAndEnabled) return;
        ThrowsApplied++; body.position = target;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        body.linearVelocity = new Vector2(-side * 3f, 2f);
    }
    public void CancelFrom(ClampGuardCombat owner) { if (source == owner) Cancel(); }
    public void Cancel()
    {
        if (!holding) return;
        holding = false; var previous = source; source = null;
        movement?.RemoveMoveMultiplier(this);
        if (body != null && health != null && health.LifeVersion == victimLife) body.gravityScale = savedGravity;
        previous?.Released(this);
    }
    private void Died(DamageInfo _) => Cancel();
    private void OnDisable()
    { Cancel(); if (health != null) { health.DebuffsCleared -= Cancel; health.Died -= Died; } }
}

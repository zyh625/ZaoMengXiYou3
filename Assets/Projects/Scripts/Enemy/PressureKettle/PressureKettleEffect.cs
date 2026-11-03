using System.Collections.Generic;
using UnityEngine;

public sealed class PressureKettleEffect : MonoBehaviour
{
    private enum Phase { Steam, Flying, Fuse, Blast }
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private PolygonCollider2D cone;
    [SerializeField] private SpriteData appearance, explosion;
    [SerializeField] private Vector2[] flightOffsets;
    [SerializeField] private Vector2 explosionOffset;
    [SerializeField] private float nativeWidth = 2f;
    [Header("Steam: duration = interval x ticks")]
    [SerializeField, Min(0f)] private float steamDamage = 3f;
    [SerializeField, Min(0.01f)] private float steamTickInterval = 0.25f;
    [SerializeField, Min(1)] private int steamTicks = 4;
    [SerializeField, Min(0.01f)] private float steamRange = 3.5f;
    [SerializeField] private bool steamKnockback = true;
    [Header("Pressure bomb")]
    [SerializeField, Min(0f)] private float bombDamage = 12f;
    [SerializeField, Min(0f)] private float bombFuse = 0.6f;
    [SerializeField, Min(0.01f)] private float bombRadius = 2f;
    [Header("Pressure burst")]
    [SerializeField, Min(0f)] private float burstDamage = 11f;
    [SerializeField, Min(0.01f)] private float burstRadius = 4f;
    [SerializeField] private bool burstKnockback = true;
    private readonly Collider2D[] hits = new Collider2D[64];
    private readonly RaycastHit2D[] casts = new RaycastHit2D[24];
    private readonly HashSet<Health> victims = new(16);
    private Phase phase;
    private Transform owner;
    private Health ownerHealth;
    private PressureKettleCombat caster;
    private int life;
    private float scale, nextTick, expiresAt, detonateAt;
    private bool faceRight;
    private Vector2 velocity;
    public int TickCount { get; private set; }
    public int ExplosionCount { get; private set; }
    public bool HasLanded { get; private set; }
    public bool HasExploded => ExplosionCount > 0;
    public float LandedAt { get; private set; }
    public float ExplodedAt { get; private set; }
    private bool OwnerAlive => ownerHealth != null && ownerHealth.isActiveAndEnabled && !ownerHealth.Dead && ownerHealth.LifeVersion == life;
    private Transform DamageSource => OwnerAlive ? owner : transform;
    private static ContactFilter2D PlayerFilter
    { get { ContactFilter2D f = new(); f.SetLayerMask(1 << 3); f.useTriggers = true; return f; } }

    private void Prepare(PressureKettleCombat source)
    {
        caster = source; owner = source.transform; ownerHealth = source.GetComponent<Health>(); life = ownerHealth.LifeVersion;
        scale = source.DamageMultiplier; faceRight = source.FaceRight;
        TickCount = ExplosionCount = 0; HasLanded = false; LandedAt = ExplodedAt = 0f;
        transform.localScale = Vector3.one;
        visual.transform.localScale = Vector3.one; visual.transform.localPosition = Vector3.zero; visual.flipX = false;
        SpriteRenderer renderer = source.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) { visual.sortingLayerID = renderer.sortingLayerID; visual.sortingOrder = renderer.sortingOrder + 1; }
        if (cone != null) cone.enabled = false;
        gameObject.SetActive(true); spriteManager.ChangeState(appearance);
    }
    public void BeginSteam(PressureKettleCombat source)
    {
        Prepare(source); phase = Phase.Steam;
        transform.position = source.Origin;
        // Source art points LEFT. Root mirroring also mirrors the physical cone.
        // The existing cone is authored at radius 3.5; scale it with the tunable range.
        float coneScale = steamRange / 3.5f;
        transform.localScale = new Vector3((faceRight ? -1f : 1f) * coneScale, coneScale, 1f);
        visual.transform.localScale = Vector3.one * (3.5f / nativeWidth);
        cone.enabled = true;
        nextTick = Time.time + steamTickInterval; expiresAt = Time.time + steamTickInterval * steamTicks;
    }
    public void Launch(PressureKettleCombat source, PlayerController target)
    {
        Prepare(source); phase = Phase.Flying; transform.position = source.Origin;
        Collider2D shape = target.GetComponent<Collider2D>();
        Vector2 aim = shape != null ? new Vector2(shape.bounds.center.x, shape.bounds.min.y + 0.12f) : (Vector2)target.transform.position;
        RaycastHit2D floor = Physics2D.Raycast(aim, Vector2.down, 30f, 1 << 6);
        if (floor.collider != null) aim.y = floor.point.y + 0.12f;
        const float flightTime = 0.9f, gravity = 18f;
        velocity = new Vector2((aim.x - transform.position.x) / flightTime,
            (aim.y - transform.position.y + gravity * flightTime * flightTime / 2f) / flightTime);
        expiresAt = Time.time + 8f;
        AlignBombVisual();
    }
    public void BeginBurst(PressureKettleCombat source)
    {
        Prepare(source); phase = Phase.Blast;
        transform.position = (Vector2)source.transform.position + Vector2.up * 0.85f;
        visual.transform.localScale = Vector3.one * (burstRadius * 2f / nativeWidth);
        expiresAt = Time.time + appearance.sprites.Length / appearance.fps;
        ExplosionCount = 1; ExplodedAt = Time.time;
        DamageArea(burstDamage, burstRadius, burstKnockback);
    }
    private void Update() { if (phase is Phase.Flying or Phase.Fuse) AlignBombVisual(); }
    private void AlignBombVisual()
    {
        if (flightOffsets == null || flightOffsets.Length == 0) return;
        Vector2 offset = flightOffsets[Mathf.Min(spriteManager.CurFrame, flightOffsets.Length - 1)];
        visual.transform.localPosition = new Vector3(faceRight ? -offset.x : offset.x, offset.y, 0f);
        visual.flipX = faceRight;
    }
    private void FixedUpdate()
    {
        if (phase == Phase.Steam)
        {
            if (!OwnerAlive || caster == null || caster.CurrentSkill != 0) { Cancel(); return; }
            transform.position = caster.Origin;
            Physics2D.SyncTransforms();
            while (TickCount < steamTicks && Time.time + 0.0001f >= nextTick)
            {
                nextTick += steamTickInterval; TickCount++;
                DamageArea(steamDamage, steamRange, steamKnockback);
                if (!gameObject.activeSelf) return;
            }
            if (TickCount == steamTicks) Cancel();
        }
        else if (phase == Phase.Flying)
        {
            if (Time.time >= expiresAt) { Cancel(); return; }
            float dt = Time.fixedDeltaTime;
            Vector2 from = transform.position, delta = velocity * dt + Vector2.down * (9f * dt * dt);
            velocity += Vector2.down * (18f * dt);
            ContactFilter2D filter = new(); filter.SetLayerMask(1 << 6); filter.useTriggers = false;
            int count = Physics2D.CircleCast(from, 0.12f, delta.normalized, filter, casts, delta.magnitude);
            int nearest = -1;
            for (int i = 0; i < count; i++) if (nearest < 0 || casts[i].distance < casts[nearest].distance) nearest = i;
            if (nearest < 0) transform.position = from + delta;
            else
            {
                RaycastHit2D hit = casts[nearest]; transform.position = hit.centroid + hit.normal * 0.01f;
                if (hit.normal.y >= 0.5f)
                {
                    phase = Phase.Fuse; HasLanded = true; LandedAt = Time.time; detonateAt = Time.time + bombFuse;
                    spriteManager.Pause();
                }
                else { if (Mathf.Abs(hit.normal.x) > 0.1f) velocity.x = 0f; if (hit.normal.y < 0f) velocity.y = Mathf.Min(0f, velocity.y); }
            }
        }
        else if (phase == Phase.Fuse && Time.time >= detonateAt)
        {
            // Commit the phase BEFORE applying damage (events may reenter or recycle actors).
            phase = Phase.Blast; ExplosionCount++; ExplodedAt = Time.time;
            visual.flipX = false;
            visual.transform.localScale = Vector3.one * (bombRadius * 2f / nativeWidth);
            visual.transform.localPosition = (Vector3)(explosionOffset * (bombRadius * 2f / nativeWidth));
            spriteManager.ChangeState(explosion);
            expiresAt = Time.time + explosion.sprites.Length / explosion.fps;
            DamageArea(bombDamage, bombRadius, false);
        }
        else if (phase == Phase.Blast && Time.time >= expiresAt) Cancel();
    }
    private void DamageArea(float damage, float radius, bool knockback)
    {
        ContactFilter2D filter = PlayerFilter;
        int count = phase == Phase.Steam ? cone.Overlap(filter, hits) : Physics2D.OverlapCircle(transform.position, radius, filter, hits);
        victims.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy) continue;
            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) continue;
            Health hp = player.GetComponent<Health>();
            if (hp == null || hp.Dead || !victims.Add(hp)) continue;
            hp.TakeDamage(new DamageInfo(DamageSource, damage * scale, knockback, kind: DamageKind.Skill));
            if (!gameObject.activeSelf) return;
        }
    }
    public void Cancel() => gameObject.SetActive(false);
    private void OnDisable()
    {
        if (spriteManager != null) spriteManager.Pause();
        if (cone != null) cone.enabled = false;
        owner = null; ownerHealth = null; caster = null; victims.Clear();
    }
}

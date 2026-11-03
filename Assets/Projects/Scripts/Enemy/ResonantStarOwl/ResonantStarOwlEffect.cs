using System.Collections.Generic;
using UnityEngine;

public sealed class ResonantStarOwlEffect : MonoBehaviour
{
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private SpriteData appearance;
    private readonly Collider2D[] hits = new Collider2D[128];
    private readonly Dictionary<Health,int> victims = new();
    private Transform owner;
    private Health ownerHealth;
    private int ownerLife, kind, segment;
    private float beganAt, duration, multiplier, previousRadius;
    private Vector2 size;
    public static bool IsJumping(PlayerController player)
    {
        var move = player != null ? player.GetComponent<PlayerMovement>() : null;
        return move != null && move.HasJumped > 0 && !move.IsOnGroundNow();
    }
    public void Begin(ResonantStarOwlCombat source, int skill, int part)
    {
        owner = source.transform; ownerHealth = source.GetComponent<Health>(); ownerLife = ownerHealth.LifeVersion;
        kind = skill; segment = part; multiplier = source.DamageMultiplier; victims.Clear();
        beganAt = Time.time; duration = kind == 0 ? 1.2f : kind == 1 ? 0.35f : 2f;
        previousRadius = 1.6f;
        float sign = source.FaceRight ? 1f : -1f;
        transform.position = kind == 0 ? source.CastOrigin : new Vector2(source.CastOrigin.x + sign*(kind == 1 ? 3.9f : 4f),
            source.CastFloor + (kind == 1 ? (segment == 0 ? 0.55f : 2.6f) : 2.5f));
        size = kind == 1 ? new Vector2(6.2f,0.55f) : new Vector2(0.55f,1.6f);
        gameObject.SetActive(true); spriteManager.ChangeState(appearance); spriteManager.Pause();
        UpdateVisual();
        if (kind == 1) CheckDamage(0f,0f);
    }
    private bool OwnerAlive => ownerHealth != null && ownerHealth.isActiveAndEnabled && !ownerHealth.Dead && ownerHealth.LifeVersion == ownerLife;
    public static bool IntersectsAnnulus(Bounds bounds, Vector2 center, float inner, float outer)
    {
        Vector2 nearest = bounds.ClosestPoint(center);
        float min = Vector2.Distance(center,nearest);
        float dx = Mathf.Max(Mathf.Abs(bounds.min.x-center.x),Mathf.Abs(bounds.max.x-center.x));
        float dy = Mathf.Max(Mathf.Abs(bounds.min.y-center.y),Mathf.Abs(bounds.max.y-center.y));
        return min <= outer && dx*dx+dy*dy >= inner*inner;
    }
    private void FixedUpdate()
    {
        if (!BattleRun.IsRunning || kind == 2 && !OwnerAlive) { Cancel(); return; }
        float elapsed = Mathf.Min(Time.time-beganAt,duration);
        if (kind == 0)
        {
            float radius = Mathf.Lerp(1.6f,7f,elapsed/duration);
            CheckDamage(Mathf.Max(0f,previousRadius-0.18f),radius+0.18f); previousRadius = radius;
        }
        else if (kind == 2) CheckDamage(0f,0f);
        if (Time.time-beganAt >= duration) Cancel();
    }
    private void LateUpdate() => UpdateVisual();
    private void UpdateVisual()
    {
        if (appearance == null || appearance.sprites.Length == 0) return;
        float progress = Mathf.Clamp01((Time.time-beganAt)/duration);
        visual.sprite = appearance.sprites[Mathf.Min(15,Mathf.FloorToInt(progress*16f))];
        Vector2 dimensions = kind == 0 ? Vector2.one*(2f*Mathf.Lerp(1.6f,7f,progress)) : size;
        Vector2 native = visual.sprite.bounds.size;
        visual.transform.localScale = new Vector3(dimensions.x/native.x,dimensions.y/native.y,1f);
        // Tight slices use a compensated pivot. Align the drawn energy with the query geometry.
        Vector3 center = visual.sprite.bounds.center;
        visual.transform.localPosition = -Vector3.Scale(center,visual.transform.localScale);
    }
    private void CheckDamage(float inner,float outer)
    {
        ContactFilter2D filter = new(); filter.SetLayerMask(1 << 3); filter.useTriggers = true;
        int count = kind == 0 ? Physics2D.OverlapCircle(transform.position,outer,filter,hits)
            : Physics2D.OverlapBox(transform.position,size,0f,filter,hits);
        for (int i=0;i<count;i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy) continue;
            var player = hit.GetComponentInParent<PlayerController>();
            if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) continue;
            var hp = player.GetComponent<Health>();
            if (hp == null || hp.Dead || hp.IsInvincible || victims.TryGetValue(hp,out int life) && life == hp.LifeVersion) continue;
            if (kind == 0 && !IntersectsAnnulus(hit.bounds,transform.position,inner,outer)) continue;
            if (kind == 2 && !IsJumping(player)) continue;
            victims[hp] = hp.LifeVersion;
            hp.TakeDamage(new DamageInfo(OwnerAlive ? owner : transform,(kind == 0 ? 8f : kind == 1 ? 10f : 6f)*multiplier,false,kind:DamageKind.Skill));
            if (kind == 2 && !hp.Dead && !hp.IsInvincible)
            {
                var move = player.GetComponent<PlayerMovement>(); move.ClearJump();
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x,Mathf.Min(-1f,rb.linearVelocityY));
            }
            if (!gameObject.activeSelf) return;
        }
    }
    public void Cancel() => gameObject.SetActive(false);
    private void OnDisable() { spriteManager?.Pause(); victims.Clear(); owner = null; ownerHealth = null; }
}

using UnityEngine;

[RequireComponent(typeof(SpriteManager))]
public sealed class WuYingWangOrb : MonoBehaviour
{
    [SerializeField] private SpriteData trail, burst;
    [SerializeField, Min(0f)] private float damage = 20f;
    [SerializeField, Min(0.1f)] private float speed = 5f, radius = 0.38f, lifetime = 4f;
    private SpriteManager sprites;
    private WuYingWangCombat owner;
    private Health ownerHealth;
    private int ownerLife, playerLayer;
    private float expiresAt, left, right, launchDamage;
    private Vector2 direction;
    private bool hit;

    public void Launch(WuYingWangCombat source, Vector2 aim, float arenaLeft, float arenaRight)
    {
        owner = source;
        ownerHealth = source.GetComponent<Health>();
        ownerLife = ownerHealth.LifeVersion;
        playerLayer = LayerMask.GetMask("Player");
        expiresAt = Time.time + lifetime;
        left = arenaLeft - 1f;
        right = arenaRight + 1f;
        direction = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector2.down;
        launchDamage = damage * owner.DamageMultiplier;
        sprites = GetComponent<SpriteManager>();
        sprites.ChangeState(trail);
    }

    private void FixedUpdate()
    {
        if (ownerHealth == null || ownerHealth.Dead || !owner.isActiveAndEnabled || ownerLife != ownerHealth.LifeVersion)
        {
            Destroy(gameObject);
            return;
        }
        if (hit)
        {
            if (sprites.IsFinished) Destroy(gameObject);
            return;
        }
        if (Time.time >= expiresAt || transform.position.x < left || transform.position.x > right)
        {
            owner.ReportMiss();
            Destroy(gameObject);
            return;
        }

        float travel = speed * Time.fixedDeltaTime;
        RaycastHit2D contact = Physics2D.CircleCast(transform.position, radius, direction, travel, playerLayer);
        if (contact.collider != null && contact.collider.GetComponentInParent<PlayerController>() is PlayerController player
            && player.isActiveAndEnabled && player.GetComponent<Health>() is Health health && !health.Dead)
        {
            transform.position += (Vector3)(direction * contact.distance);
            hit = true;
            float previous = health.CurBlood;
            health.TakeDamage(new DamageInfo(owner.transform, launchDamage, false));
            if (health.CurBlood >= previous) owner.ReportMiss();
            sprites.ChangeState(burst);
        }
        else transform.position += (Vector3)(direction * travel);
    }
}

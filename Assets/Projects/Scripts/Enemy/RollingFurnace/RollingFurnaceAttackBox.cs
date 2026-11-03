using UnityEngine;

public sealed class RollingFurnaceAttackBox : EnemyAttackBox
{
    [SerializeField] private bool landingOnly;
    [SerializeField] private float landingRadius = 2.5f;
    private bool sampling;
    private readonly Collider2D[] overlaps = new Collider2D[64];
    public void SampleNow()
    {
        ContactFilter2D filter = new(); filter.SetLayerMask(1 << 3); filter.useTriggers = true;
        sampling = true;
        try
        {
            int count = landingOnly
                ? Physics2D.OverlapCircle(transform.root.position, landingRadius, filter, overlaps)
                : GetComponent<BoxCollider2D>().Overlap(filter, overlaps);
            for (int i = 0; i < count; i++) TryAttack(overlaps[i]);
        }
        finally { sampling = false; }
    }
    protected override void TryAttack(Collider2D hit)
    {
        if (!isActiveAndEnabled || landingOnly && !sampling || hit == null) return;
        if (GetComponentInParent<Health>() is not Health owner || owner.Dead) return;
        PlayerController player = hit.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) return;
        Health hp = player.GetComponent<Health>();
        if (hp == null || hp.Dead || hp.IsInvincible) return;
        base.TryAttack(hit); // One shared Health/IDamage entry across all player colliders, per leg.
    }
}

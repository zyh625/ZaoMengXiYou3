using UnityEngine;

public sealed class RollingFurnaceAttackBox : EnemyAttackBox
{
    [SerializeField] private bool landingOnly;
    [SerializeField] private float landingRadius = 2.5f;
    private bool sampling;
    private readonly Collider2D[] overlaps = new Collider2D[64];
    //以炉兽本体为圆心。不能用 transform.root：训练场把敌人挂在 TrainingEnemies 下，root 会变成场景原点
    private Transform Owner => GetComponentInParent<EnemyController>() is EnemyController owner ? owner.transform : transform.root;
    public void SampleNow()
    {
        ContactFilter2D filter = new(); filter.SetLayerMask(1 << 3); filter.useTriggers = true;
        sampling = true;
        try
        {
            int count = landingOnly
                ? Physics2D.OverlapCircle(Owner.position, landingRadius, filter, overlaps)
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

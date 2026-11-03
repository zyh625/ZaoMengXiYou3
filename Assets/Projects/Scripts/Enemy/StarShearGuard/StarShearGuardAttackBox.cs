using UnityEngine;

public sealed class StarShearGuardAttackBox : EnemyAttackBox
{
    [SerializeField] private int windowIndex;
    [SerializeField] private LayerMask targetLayers = 1 << 3;
    private StarShearGuardCombat owner;
    private readonly Collider2D[] overlaps = new Collider2D[32];
    public void SampleNow()
    {
        var filter = new ContactFilter2D(); filter.SetLayerMask(targetLayers); filter.useTriggers = true;
        int count = GetComponent<BoxCollider2D>().Overlap(filter, overlaps);
        for (int i = 0; i < count; i++) TryAttack(overlaps[i]);
    }
    protected override void TryAttack(Collider2D hit)
    {
        owner ??= GetComponentInParent<StarShearGuardCombat>();
        if (owner == null || !owner.IsWindowActive(windowIndex)) return;
        var player = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
        if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) return;
        var hp = player.GetComponent<Health>();
        if (hp == null || hp.Dead || hitEnemies.Contains(hp)) return;
        // Lock facing at wind-up: crossing behind defeats either cut, including tall colliders.
        if ((player.transform.position.x - owner.transform.position.x) * (owner.AttackFaceRight ? 1f : -1f) <= 0f) return;
        if (!hitEnemies.Add(hp)) return;
        float before = hp.CurBlood;
        hp.TakeDamage(CreateDamageInfo(owner.transform));
        if (hp.CurBlood < before) owner.RecordWindowHit(windowIndex);
    }
}

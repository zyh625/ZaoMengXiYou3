using UnityEngine;

public sealed class ClampGuardAttackBox : EnemyAttackBox
{
    [SerializeField] private int attackIndex;
    private ClampGuardCombat owner;
    private bool instantSample;
    private readonly Collider2D[] overlaps = new Collider2D[32];
    public void SampleNow()
    {
        var filter = new ContactFilter2D(); filter.SetLayerMask(1 << 3); filter.useTriggers = true;
        instantSample = true;
        try
        {
            int count = GetComponent<BoxCollider2D>().Overlap(filter, overlaps);
            for (int i = 0; i < count; i++) TryAttack(overlaps[i]);
        }
        finally { instantSample = false; }
    }
    protected override void TryAttack(Collider2D hit)
    {
        if (attackIndex == 2 && !instantSample) return;
        owner ??= GetComponentInParent<ClampGuardCombat>();
        if (owner == null || owner.Attack != attackIndex) return;
        var player = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
        if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit || player.GetComponent<BossController>() != null) return;
        var hp = player.GetComponent<Health>();
        if (hp == null || hp.Dead || hitEnemies.Contains(hp)) return;
        if (attackIndex == 2)
        {
            Vector2 origin = (Vector2)owner.transform.position + new Vector2(owner.AttackFaceRight ? BalanceRuntime.Number(this, "ClampGuardAttackBox.TryAttack.1", 0.45f) : -BalanceRuntime.Number(this, "ClampGuardAttackBox.TryAttack.2", 0.45f), BalanceRuntime.Number(this, "ClampGuardAttackBox.TryAttack.3", 1.1f));
            Vector2 p = hit.ClosestPoint(origin) - origin;
            float forward = p.x * (owner.AttackFaceRight ? 1f : -1f);
            if (forward < 0f || forward > BalanceRuntime.Number(this, "ClampGuardAttackBox.TryAttack.4", 3f) || Mathf.Abs(p.y) > BalanceRuntime.Number(this, "ClampGuardAttackBox.TryAttack.5", 0.12f) + forward * BalanceRuntime.Number(this, "ClampGuardAttackBox.TryAttack.6", 0.4f)) return;
        }
        if (!hitEnemies.Add(hp)) return;
        if (attackIndex == 1)
        { if (owner.TryGrab(hp)) owner.ReportAttackHit(this); return; }
        float before = hp.CurBlood; float scale = DamageMultiplier;
        hp.TakeDamage(new DamageInfo(owner.transform, curAttack * scale, false, false, null, false, false, DamageKind.Skill));
        if (hp.CurBlood >= before || hp.Dead) return;
        owner.ReportAttackHit(this);
        if (attackIndex == 0)
            (player.GetComponent<ActorStun>() ?? player.gameObject.AddComponent<ActorStun>()).Apply(owner, owner.ClampRootDuration);
        else ClampGuardBurn.Apply(hp, owner.GetComponent<Health>(), scale,
            owner.BurnTickDamage, owner.BurnDuration, owner.BurnTickInterval);
    }
}

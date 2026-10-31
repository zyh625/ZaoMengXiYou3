using UnityEngine;

public sealed class RedChainAttackBox : EnemyAttackBox
{
    public enum HitType { Sweep, Chain, Ground }
    [SerializeField] private HitType hitType;
    [SerializeField] private LayerMask playerLayer = 1 << 3;
    private readonly Collider2D[] overlaps = new Collider2D[16];
    private bool instantSample;

    public void SampleNow()
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(playerLayer);
        filter.useTriggers = true;
        instantSample = true;
        int count = GetComponent<BoxCollider2D>().Overlap(filter, overlaps);
        for (int i = 0; i < count; i++) TryAttack(overlaps[i]);
        instantSample = false;
    }

    protected override void TryAttack(Collider2D collider)
    {
        //冲击波只有落地瞬间的采样结算；后续火焰和裂纹没有重复伤害。
        if (hitType == HitType.Ground && !instantSample) return;
        PlayerController player = collider.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) return;
        Health health = player.GetComponent<Health>();
        if (health == null || health.Dead || hitEnemies.Contains(health)) return;
        float before = health.CurBlood;
        bool alreadyAttacked = health.Attacked;
        base.TryAttack(collider);
        if (hitType != HitType.Chain || health.CurBlood >= before || health.Dead) return;
        //这次链击不额外清空输入、跳跃或施法请求，也不消除先前另一击的受击请求。
        if (!alreadyAttacked) health.AttackedEnd();
        RedChainPull pull = player.GetComponent<RedChainPull>();
        if (pull == null) pull = player.gameObject.AddComponent<RedChainPull>();
        pull.Begin(GetComponentInParent<RedChainCombat>());
    }
}

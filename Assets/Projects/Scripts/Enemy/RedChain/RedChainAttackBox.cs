using UnityEngine;

public sealed class RedChainAttackBox : EnemyAttackBox
{
    public enum HitType { Sweep, Chain, Ground }
    [SerializeField] private HitType hitType;
    [SerializeField] private LayerMask playerLayer = 1 << 3;
    private readonly Collider2D[] overlaps = new Collider2D[16];
    private bool instantSample;

    //链子：判定从本体伸到链头当前位置（由动画帧的前缘决定），最长不超过预制体上这个攻击框的远端
    public bool ChainLanded { get; private set; }//本次出招已经勾中，收链途中不再判定
    public void ResetChain() => ChainLanded = false;
    public Rect WorldRect//攻击框停用时 bounds 为空，按变换自己计算世界范围
    {
        get
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            Vector2 center = box.transform.TransformPoint(box.offset);
            Vector3 scale = box.transform.lossyScale;
            Vector2 size = new(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y));
            return new Rect(center - size * 0.5f, size);
        }
    }
    public void SampleChain(float bodyX, float tipX)
    {
        if (ChainLanded) return;
        Rect limit = WorldRect;
        float dir = Mathf.Sign(limit.center.x - bodyX); if (dir == 0f) dir = 1f;
        float far = dir > 0f ? limit.xMax : limit.xMin;
        float tip = dir > 0f ? Mathf.Clamp(tipX, bodyX, far) : Mathf.Clamp(tipX, far, bodyX);
        Vector2 size = new(Mathf.Max(0.05f, Mathf.Abs(tip - bodyX)), limit.height);
        Vector2 center = new((bodyX + tip) * 0.5f, limit.center.y);
        ContactFilter2D filter = new();
        filter.SetLayerMask(playerLayer);
        filter.useTriggers = true;
        instantSample = true;
        int count = Physics2D.OverlapBox(center, size, 0f, filter, overlaps);
        for (int i = 0; i < count; i++) TryAttack(overlaps[i]);
        instantSample = false;
    }

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
        //链子同样只认按链长计算的采样，不用预制体上固定大小的触发框
        if ((hitType == HitType.Ground || hitType == HitType.Chain) && !instantSample) return;
        PlayerController player = collider.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) return;
        Health health = player.GetComponent<Health>();
        if (health == null || health.Dead || hitEnemies.Contains(health)) return;
        bool alreadyAttacked = health.Attacked;
        bool invincible = health.IsInvincible;
        base.TryAttack(collider);
        //链子命中即勾回（训练场锁血、无双时同样生效），只有无敌挡住
        if (hitType != HitType.Chain || invincible || health.Dead) return;
        ChainLanded = true;
        //这次链击不额外清空输入、跳跃或施法请求，也不消除先前另一击的受击请求。
        if (!alreadyAttacked) health.AttackedEnd();
        RedChainPull pull = player.GetComponent<RedChainPull>();
        if (pull == null) pull = player.gameObject.AddComponent<RedChainPull>();
        pull.Begin(GetComponentInParent<RedChainCombat>());
    }
}

using UnityEngine;

public sealed class ConstellationWeaverCombat : EnemyCombat
{
    // Reserve during the warning too: ten pooled spiders must not release ten lines together.
    private static ConstellationWeaverCombat silkOwner;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSilkOwner() => silkOwner = null;
    [SerializeField] private StarAnchor starAnchorPrefab;
    [SerializeField] private ConstellationWeaverArea silkLinePrefab, starKnotPrefab;
    private readonly StarAnchor[] anchors = new StarAnchor[2];
    private readonly ConstellationWeaverArea[] knots = new ConstellationWeaverArea[3];
    private readonly Vector2[] nodes = new Vector2[3], ends = new Vector2[2];
    private readonly float[] readyAt = new float[3];
    [SerializeField] private float[] cooldown = { 12f, 10f, 8f };
    private Transform pool;
    private ConstellationWeaverArea silk;
    private Health health;
    private ConstellationWeaverMovement movement;
    private PlayerManager party;
    private PlayerController fallback;
    private float resolveAt, nextNodeAt, slideAt, slideFrom, slideTo;
    private bool released, sliding, recoverPending;
    private int node;
    public int CurrentSkill { get; private set; } = -1;
    public bool FaceRight { get; private set; }
    public bool HasSilk => silk != null && silk.gameObject.activeSelf && anchors[0] != null && anchors[1] != null && anchors[0].gameObject.activeSelf && anchors[1].gameObject.activeSelf;
    public bool HoldingFrame { get; private set; }
    protected override bool UsesAnger => false;
    public PlayerController Target
    {
        get
        {
            if (party != null && party.isActiveAndEnabled) return party.CurrentPlayer;
            if (fallback != null && fallback.isActiveAndEnabled && !fallback.GetComponent<Health>().Dead) return fallback;
            if (Time.time < resolveAt) return null;
            resolveAt = Time.time + BalanceRuntime.Number(this, "ConstellationWeaverCombat.Target.1", 0.5f); party = FindAnyObjectByType<PlayerManager>();
            if (party != null && party.isActiveAndEnabled) return party.CurrentPlayer;
            foreach (var player in FindObjectsByType<PlayerController>())
                if (player.isActiveAndEnabled && !player.GetComponent<Health>().Dead) return fallback = player;
            return null;
        }
    }
    public void BindTarget(PlayerController target) { fallback = target; party = null; }
    public bool InRange => Target != null && (Mathf.Abs(Target.transform.position.x - transform.position.x) >= BalanceRuntime.Number(this, "ConstellationWeaverCombat.InRange.Range1.Minimum", 3f) && Mathf.Abs(Target.transform.position.x - transform.position.x) <= BalanceRuntime.Number(this, "ConstellationWeaverCombat.InRange.Range1.Maximum", 6f));
    public int ReadySkill
    {
        get
        {
            if (CurrentSkill >= 0 || !InRange || health.Dead || health.Attacked || movement.IsExternallyControlled || health.Status.BlocksActions) return -1;
            if (!HasSilk && (silkOwner == null || silkOwner == this) && Time.time >= readyAt[0]) return 0;
            if (HasSilk && Time.time >= readyAt[2] && !health.Status.BlocksMovement) return 2;
            return Time.time >= readyAt[1] ? 1 : -1;
        }
    }
    protected override void Awake()
    {
        base.Awake(); health = GetComponent<Health>(); movement = GetComponent<ConstellationWeaverMovement>();
        EnsurePool();
    }
    //特效池是场景根节点，训练场切换目标时会清掉场景里的临时根节点；被删后在下次出招前重建。
    private bool PoolIntact()
    {
        if (pool == null || silk == null) return false;
        foreach (var anchor in anchors) if (anchor == null) return false;
        foreach (var knot in knots) if (knot == null) return false;
        return true;
    }
    private void EnsurePool()
    {
        if (PoolIntact()) return;
        if (pool != null) Destroy(pool.gameObject);
        pool = new GameObject("ConstellationWeaverEffectPool").transform;
        pool.gameObject.SetActive(false);
        for (int i = 0; i < 2; i++) { anchors[i] = Instantiate(starAnchorPrefab, pool); anchors[i].gameObject.SetActive(false); }
        silk = Instantiate(silkLinePrefab, pool); silk.gameObject.SetActive(false);
        for (int i = 0; i < 3; i++) { knots[i] = Instantiate(starKnotPrefab, pool); knots[i].gameObject.SetActive(false); }
        pool.gameObject.SetActive(true);
        if (silkOwner == this) silkOwner = null;//旧星丝已随池销毁
    }
    protected override void OnEnable()
    {
        base.OnEnable(); CurrentSkill = -1; HoldingFrame = sliding = recoverPending = false;
        for (int i = 0; i < 3; i++) readyAt[i] = Time.time;
        health.Died += Died;
    }
    // All ground points are sampled once at cast start, never from the moving target later.
    private Vector2 Ground(float x)
    {
        var floor = Physics2D.Raycast(new Vector2(x, transform.position.y + 1f), Vector2.down, BalanceRuntime.Number(this, "ConstellationWeaverCombat.Ground.1", 4f), 1 << 6);
        return new Vector2(x, floor.collider != null ? floor.point.y + BalanceRuntime.Number(this, "ConstellationWeaverCombat.Ground.2", 0.16f) : transform.position.y + BalanceRuntime.Number(this, "ConstellationWeaverCombat.Ground.3", 0.16f));
    }
    public bool BeginCast(int skill)
    {
        if (skill < 0 || skill > 2 || Target == null || CurrentSkill >= 0 || Time.time < readyAt[skill]
            || health.Dead || health.Attacked || movement.IsExternallyControlled || health.Status.BlocksActions
            || skill == 0 && silkOwner != null && silkOwner != this
            || skill == 2 && (!HasSilk || health.Status.BlocksMovement)) return false;
        EnsurePool();
        if (skill == 2 && !HasSilk) return false;
        CurrentSkill = skill; released = sliding = HoldingFrame = false; node = 0;
        readyAt[skill] = Time.time + cooldown[skill]; FaceRight = Target.transform.position.x >= transform.position.x;
        health.Status.NotifyAttackStarted();
        if (skill == 0)
        {
            silkOwner = this;
            float direction = FaceRight ? 1f : -1f;
            ends[0] = Ground(transform.position.x + direction * BalanceRuntime.Number(this, "ConstellationWeaverCombat.BeginCast.1", 0.6f)); ends[1] = Ground(transform.position.x + direction * BalanceRuntime.Number(this, "ConstellationWeaverCombat.BeginCast.2", 5.4f));
            for (int i = 0; i < 2; i++) knots[i].Show(this, ends[i], ends[i], false);
        }
        if (skill == 1)
        {
            float x = Target.transform.position.x;
            for (int i = 0; i < 3; i++) { nodes[i] = Ground(x + (i - 1) * BalanceRuntime.Number(this, "ConstellationWeaverCombat.BeginCast.3", 1.5f)); knots[i].Show(this, nodes[i], nodes[i], false); }
        }
        return true;
    }
    public void SampleFrame(int frame)
    {
        HoldingFrame = false;
        if (CurrentSkill < 0) return;
        if (health.Dead || health.Attacked || movement.IsExternallyControlled || health.Status.BlocksActions || !PoolIntact()) { CancelCast(); return; }
        if (CurrentSkill == 0 && !released && frame >= BalanceRuntime.Number(this, "ConstellationWeaverCombat.SampleFrame.Parameter1", 4f))
        {
            released = true; ExpireSilk();
            silkOwner = this;
            for (int i = 0; i < 2; i++) { knots[i].Recycle(); anchors[i].Place(this, ends[i]); }
            silk.Show(this, ends[0], ends[1], true);
        }
        if (CurrentSkill == 1 && node < 3 && frame >= (node == 0 ? 5 : node == 1 ? 7 : 10))
        {
            if (node == 0 || Time.time >= nextNodeAt)
            { knots[node++].Pulse(); nextNodeAt = Time.time + BalanceRuntime.Number(this, "ConstellationWeaverCombat.SampleFrame.1", 0.6f); }
            else HoldingFrame = true;
        }
        if (CurrentSkill == 2)
        {
            if (!HasSilk || health.Status.BlocksMovement) { CancelCast(); return; }
            if (!released && frame >= BalanceRuntime.Number(this, "ConstellationWeaverCombat.SampleFrame.Parameter2", 4f))
            {
                released = sliding = true; slideAt = Time.time; slideFrom = transform.position.x;
                slideTo = Mathf.Abs(slideFrom - ends[0].x) < Mathf.Abs(slideFrom - ends[1].x) ? ends[1].x : ends[0].x;
            }
        }
    }
    public void MotionStep()
    {
        // MovePosition commits in the next physics simulation; lock recovery after arrival.
        if (recoverPending) { recoverPending = false; movement.Stop(); movement.Stun(this, 1f); return; }
        if (!sliding || CurrentSkill != 2) { movement.Stop(); return; }
        float t = Mathf.Clamp01((Time.time - slideAt) / (BalanceRuntime.Number(this, "ConstellationWeaverCombat.MotionStep.1", 6f) / BalanceRuntime.Number(this, "ConstellationWeaverCombat.MotionStep.2", 16f)));
        if (!HasSilk || !movement.SlideTo(Mathf.Lerp(slideFrom, slideTo, t))) { CancelCast(); return; }
        if (t >= 1f) { sliding = false; recoverPending = true; }
    }
    public void AnchorBroken()
    {
        ExpireSilk(); CancelCast();
        if (!health.Dead && isActiveAndEnabled) movement.Stun(this, BalanceRuntime.Number(this, "ConstellationWeaverCombat.AnchorBroken.1", 0.8f));
    }
    public void ExpireSilk()
    {
        if (silkOwner == this) silkOwner = null;
        if (silk != null) silk.Recycle(); foreach (var anchor in anchors) if (anchor != null) anchor.Recycle();
        if (CurrentSkill == 2) CancelCast();
    }
    public void CancelCast()
    {
        CurrentSkill = -1; sliding = HoldingFrame = recoverPending = false;
        foreach (var knot in knots) if (knot != null) knot.Recycle();
        if (!HasSilk && silkOwner == this) silkOwner = null;
        if (movement != null) movement.Stop();
    }
    private void Died(DamageInfo damage) { CancelCast(); ExpireSilk(); }
    private void OnDisable() { if (health != null) health.Died -= Died; CancelCast(); ExpireSilk(); }
    private void OnDestroy() { if (pool != null) Destroy(pool.gameObject); }
}

using UnityEngine;

public sealed class ClampGuardCombat : EnemyCombat
{
    [SerializeField] private ClampGuardAttackBox[] hitBoxes;
    [SerializeField] private IndependentSkillEffect ventFlamePrefab;
    [Header("钳火机傀数值")]
    [SerializeField, Range(0f, 1f)] private float frontDamageMultiplier = .8f;//正面护甲的受伤倍率。
    [SerializeField, Min(0f)] private float rearDamageBonus = .25f;//背部散热片额外受伤比例。
    [SerializeField, Min(0f)] private float clampRootDuration = 1.2f;//钳夹普通控制持续时间。
    [SerializeField, Min(.01f)] private float grabDuration = 1.5f, grabTickInterval = .5f;//抓取时长与跳伤间隔。
    [SerializeField, Min(0f)] private float grabTickDamage = 3f;//单次抓取跳伤。
    [SerializeField, Min(0f)] private float burnTickDamage = 3f;//单次灼烧伤害。
    [SerializeField, Min(.01f)] private float burnDuration = 2f, burnTickInterval = 1f;//灼烧时长与间隔。
    public float ClampRootDuration => clampRootDuration;
    public float GrabDuration => grabDuration;
    public float GrabTickInterval => Mathf.Max(BalanceRuntime.Number(this, "ClampGuardCombat.GrabTickInterval.1", 0.01f), grabTickInterval);
    public float GrabTickDamage => grabTickDamage;
    public float BurnTickDamage => burnTickDamage;
    public float BurnDuration => burnDuration;
    public float BurnTickInterval => Mathf.Max(BalanceRuntime.Number(this, "ClampGuardCombat.BurnTickInterval.1", 0.01f), burnTickInterval);
    private Health health;
    private ClampGuardController controller;
    private ClampGuardGrab tether;
    private bool sampled;
    private readonly bool[] visited = new bool[3];
    public bool WindowClosed { get; private set; }
    public bool HasTether => tether != null && tether.IsHolding(this);
    public int FlamesEmitted { get; private set; }
    protected override bool UsesAnger => false;

    protected override void Awake()
    {
        base.Awake(); health = GetComponent<Health>(); controller = GetComponent<ClampGuardController>();
    }
    protected override void OnEnable()
    {
        base.OnEnable(); sampled = WindowClosed = false; tether = null; FlamesEmitted = 0;
        health.DamagePreparing += PrepareArmor; health.DamageResolved += ResolveArmor;
    }
    // Source-scoped armor and one-hit vulnerability never clear another modifier source.
    private void PrepareArmor(DamageInfo damage)
    {
        health.RemoveDamageMultiplier(this);
        health.ApplyNextHitVulnerability(this, 0f, 1f);
        if (damage.IsPeriodic || damage.Source == null) return;
        float delta = damage.Source.position.x - transform.position.x;
        if (Mathf.Abs(delta) < BalanceRuntime.Number(this, "ClampGuardCombat.PrepareArmor.1", 0.02f)) return;
        bool front = (delta > 0f) == controller.FacingRight;
        if (front) health.SetDamageMultiplier(this, frontDamageMultiplier);
        else health.ApplyNextHitVulnerability(this, rearDamageBonus, 1f);
    }
    private void ResolveArmor(DamageInfo _, float __) => health.RemoveDamageMultiplier(this);

    public static int EligibleMask(float distance, int ready)
    {
        distance = Mathf.Abs(distance); int result = 0;
        if (distance <= BalanceRuntime.Number(typeof(ClampGuardCombat), "ClampGuardCombat.EligibleMask.1", 2.5f) && (ready & 1) != 0) result |= 1;
        if (distance >= BalanceRuntime.Number(typeof(ClampGuardCombat), "ClampGuardCombat.EligibleMask.2", 3f) && distance <= BalanceRuntime.Number(typeof(ClampGuardCombat), "ClampGuardCombat.EligibleMask.3", 5f) && (ready & 2) != 0) result |= 2;
        if (distance <= BalanceRuntime.Number(typeof(ClampGuardCombat), "ClampGuardCombat.EligibleMask.4", 5f) && (ready & 4) != 0) result |= 4;
        return result;
    }
    public bool SelectRequest(float delta)
    {
        int ready = 0;
        for (int i = 0; i < 3; i++) if (AttackRequest[i]) ready |= 1 << i;
        int mask = EligibleMask(delta, ready);
        int chosen = (mask & 1) != 0 ? 0 : (mask & 2) != 0 ? 1 : (mask & 4) != 0 ? 2 : -1;
        ClearAttackRequests(); if (chosen >= 0) AttackRequest[chosen] = true;
        return chosen >= 0;
    }
    public void BeginCast()
    {
        CancelCast(); sampled = WindowClosed = false; FlamesEmitted = 0;
        System.Array.Clear(visited, 0, visited.Length);
    }
    public void SampleFrame(int frame)
    {
        int attack = Attack;
        if (attack < 0 || WindowClosed) return;
        if (!sampled && (AttackOpen(frame) || AttackShut(frame)))
        {
            sampled = true; AttackBegin(); Physics2D.SyncTransforms();
            if (attack == 2)
            {
                FlamesEmitted++;
                var offset = new Vector3(AttackFaceRight ? BalanceRuntime.Number(this, "ClampGuardCombat.SampleFrame.1", 0.45f) : -BalanceRuntime.Number(this, "ClampGuardCombat.SampleFrame.2", 0.45f), BalanceRuntime.Number(this, "ClampGuardCombat.SampleFrame.3", 1.1f), 0f);
                ClampGuardVentFlamePool.Emit(ventFlamePrefab, transform.position + offset, AttackFaceRight, transform, offset);
            }
            hitBoxes[attack].SampleNow();
            if (Attack != attack) return; // Reflection/death may cancel during the first hit.
        }
        if (sampled && AttackShut(frame)) { AttackEnd(); WindowClosed = true; }
        else if (sampled && AttackOpen(frame)) AttackBegin();
    }
    public bool TryGrab(Health victim)
    {
        if (Attack != 1 || visited[1] || HasTether) return false;
        var grab = victim.GetComponent<ClampGuardGrab>() ?? victim.gameObject.AddComponent<ClampGuardGrab>();
        if (!grab.Begin(this)) return false;
        tether = grab; visited[1] = true; return true;
    }
    public bool CanMaintainGrab => isActiveAndEnabled && health != null && !health.Dead && Attack == 1
        && controller != null && controller.IsCasting && !GetComponent<ClampGuardMovement>().IsExternallyControlled
        && !(health.Status?.BlocksActions ?? false);
    public void Released(ClampGuardGrab grab) { if (tether == grab) tether = null; }
    public void CancelCast()
    {
        var previous = tether; tether = null; previous?.CancelFrom(this); ClearAttackRequests();
    }
    private void OnDisable()
    {
        CancelCast(); health.DamagePreparing -= PrepareArmor; health.DamageResolved -= ResolveArmor;
        health.RemoveDamageMultiplier(this);
    }
}

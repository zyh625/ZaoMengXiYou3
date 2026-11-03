using UnityEngine;

// All state belongs to this pooled life. No changes to the common enemy/Health rules.
public sealed class PressureKettleCombat : EnemyCombat
{
    [SerializeField] private PressureKettleEffect steamPrefab, bombPrefab, burstPrefab;
    [SerializeField] private Transform muzzle;
    [Header("SteamJet / PressureBomb / PressureRelease")]
    [SerializeField] private float[] cooldowns = { 6f, 8f, 12f };
    [SerializeField] private int[] releaseFrames = { 5, 8, 7 };
    [SerializeField] private float[] windups = { 5f / 16f, 8f / 16f, 7f / 16f };
    [Header("Pressure and overheat")]
    [SerializeField, Min(1f)] private float maxPressure = 100f;
    [SerializeField, Min(0f)] private float pressurePerEvent = 8f;
    [SerializeField, Range(0f, 1f)] private float releaseHealthRatio = 0.4f;
    [SerializeField, Min(0f)] private float overheatDuration = 3f;
    [SerializeField, Min(0f)] private float overheatDamageMultiplier = 1.4f;
    private readonly float[] readyAt = new float[3];
    private Health health;
    private PlayerManager party;
    private PlayerController fallback;
    private float resolveAt, castAt, overheatUntil;
    private bool released, lowHealthLatched, releasePending;
    private PressureKettleEffect steam;
    public int CurrentSkill { get; private set; } = -1;
    public int ReleaseFrame => CurrentSkill >= 0 ? releaseFrames[CurrentSkill] : 0;
    public float Windup => CurrentSkill >= 0 ? windups[CurrentSkill] : 0f;
    public float Pressure { get; private set; }
    public bool ReleasePending => releasePending;
    public bool IsOverheated => overheatUntil > Time.time;
    public bool FaceRight { get; private set; }
    public int ReleaseCount { get; private set; }
    public PressureKettleEffect ActiveSteam => steam;
    public PressureKettleEffect LastBomb { get; private set; }
    public float CooldownReadyAt(int skill) => readyAt[skill];
    protected override bool UsesAnger => false;

    public PlayerController Target
    {
        get
        {
            if (party != null && party.isActiveAndEnabled && Alive(party.CurrentPlayer)) return party.CurrentPlayer;
            if (Alive(fallback)) return fallback;
            if (Time.time < resolveAt) return null;
            resolveAt = Time.time + 0.5f;
            party = FindAnyObjectByType<PlayerManager>();
            if (party != null && Alive(party.CurrentPlayer)) return party.CurrentPlayer;
            foreach (PlayerController player in FindObjectsByType<PlayerController>())
                if (Alive(player)) { fallback = player; return player; }
            return null;
        }
    }
    private static bool Alive(PlayerController player) => player != null && player.isActiveAndEnabled
        && player.TryGetComponent(out Health hp) && !hp.Dead;
    public bool InRange => Target != null && Mathf.Abs(Target.transform.position.x - transform.position.x) is >= 3.5f and <= 7f;
    public int ReadySkill
    {
        get
        {
            if (CurrentSkill >= 0 || health == null || health.Dead || health.Attacked
                || health.Status != null && health.Status.BlocksActions) return -1;
            // Automatic pressure release is allowed even with no target or at retreat distance.
            if (releasePending && Time.time >= readyAt[2]) return 2;
            if (!InRange) return -1;
            // The spout is ahead of the body: never waste a short cone on a distant target.
            float distance = Mathf.Abs(Target.transform.position.x - transform.position.x);
            if (Time.time >= readyAt[0] && distance <= 3.5f + Mathf.Abs(muzzle.localPosition.x)) return 0;
            return Time.time >= readyAt[1] ? 1 : -1;
        }
    }
    protected override void Awake() { base.Awake(); health = GetComponent<Health>(); }
    protected override void OnEnable()
    {
        base.OnEnable();
        if (health == null) health = GetComponent<Health>();
        RemoveOverheat();
        Pressure = 0f; releasePending = lowHealthLatched = released = false;
        CurrentSkill = -1; ReleaseCount = 0; steam = LastBomb = null;
        for (int i = 0; i < readyAt.Length; i++) readyAt[i] = Time.time;
        health.DamageReceived += OnDamage;
        health.DamagePreparing += BeforeDamage;
        health.Died += OnDied;
    }
    public void BindTarget(PlayerController target) { fallback = target; party = null; resolveAt = 0f; }
    public Vector2 Origin => (Vector2)transform.position + new Vector2((FaceRight ? 1f : -1f) * Mathf.Abs(muzzle.localPosition.x), muzzle.localPosition.y);
    private void AddPressure()
    {
        float previous = Pressure;
        Pressure = Mathf.Min(maxPressure, Pressure + pressurePerEvent);
        if (previous < maxPressure && Pressure >= maxPressure) releasePending = true;
    }
    private void OnDamage(DamageInfo damage, float actual)
    {
        if (actual <= 0f || health.Dead) return;
        AddPressure();
        CheckLowHealth();
    }
    private void CheckLowHealth()
    {
        if (!lowHealthLatched && !health.Dead && health.ratio < releaseHealthRatio)
        { lowHealthLatched = true; releasePending = true; }
    }
    private void Update()
    {
        CheckLowHealth();
        if (overheatUntil > 0f && Time.time >= overheatUntil) RemoveOverheat();
    }
    // Also expire before Health multiplies an incoming hit, regardless of Update order.
    private void BeforeDamage(DamageInfo damage)
    { if (overheatUntil > 0f && Time.time >= overheatUntil) RemoveOverheat(); }
    public bool BeginCast(int skill)
    {
        if (skill < 0 || skill > 2 || CurrentSkill >= 0 || health.Dead || health.Attacked
            || Time.time < readyAt[skill] || health.Status != null && health.Status.BlocksActions
            || skill < 2 && Target == null || skill == 2 && !releasePending) return false;
        CurrentSkill = skill; released = false; castAt = Time.time;
        readyAt[skill] = Time.time + cooldowns[skill];
        if (Target != null) FaceRight = Target.transform.position.x >= transform.position.x;
        AddPressure();
        health.Status?.NotifyAttackStarted();
        return true;
    }
    public void SampleFrame(int frame)
    {
        if (CurrentSkill < 0 || released) return;
        if (health.Dead || health.Attacked || health.Status != null && health.Status.BlocksActions)
        { CancelCast(); return; }
        if (frame < ReleaseFrame || Time.time < castAt + Windup) return;
        released = true;
        PressureKettleEffectPool pool = PressureKettleEffectPool.GetOrCreate();
        if (CurrentSkill == 0) steam = pool.Steam(steamPrefab, this);
        else if (CurrentSkill == 1)
        {
            PlayerController target = Target;
            if (target != null) LastBomb = pool.Bomb(bombPrefab, this, target);
        }
        else
        {
            // Commit before damage callbacks: reflected damage can kill/recycle the caster.
            Pressure = 0f; releasePending = false; ReleaseCount++;
            overheatUntil = Time.time + overheatDuration;
            health.SetDamageMultiplier(this, overheatDamageMultiplier);
            pool.Burst(burstPrefab, this);
        }
    }
    public void CancelCast()
    {
        if (steam != null && steam.gameObject.activeSelf) steam.Cancel();
        steam = null; CurrentSkill = -1; released = false;
        // Launched bombs belong to the scene pool, not this cast or this enemy life.
    }
    private void RemoveOverheat() { if (health != null) health.RemoveDamageMultiplier(this); overheatUntil = 0f; }
    private void OnDied(DamageInfo damage) { CancelCast(); RemoveOverheat(); }
    private void OnDisable()
    {
        if (health != null) { health.DamageReceived -= OnDamage; health.DamagePreparing -= BeforeDamage; health.Died -= OnDied; }
        CancelCast(); RemoveOverheat();
    }
}

using UnityEngine;

public sealed class ResonantStarOwlCombat : EnemyCombat
{
    [SerializeField] private ResonantStarOwlEffect ringsPrefab, chimePrefab, bandPrefab;
    private Health health;
    private PlayerManager party;
    private PlayerController fallback;
    private float resolveAt, castAt, lastJumpAt = -99f;
    private bool wasJumping;
    private int jumps, emitted;
    private int samplingFrame = -1;
    private readonly float[] readyAt = new float[3];
    [SerializeField] private float[] Cooldowns = { 7f, 6f, 9f };
    private ResonantStarOwlEffect band;
    public event System.Action<int,int> SegmentReleased;
    public void BindTarget(PlayerController player) { fallback = player; party = null; resolveAt = Time.time+1f; }
    public int CurrentSkill { get; private set; } = -1;
    public bool FaceRight { get; private set; }
    public Vector2 CastOrigin { get; private set; }
    public float CastFloor { get; private set; }
    public float CastTargetX { get; private set; }//高低鸣与共振带落在施法瞬间玩家所在的位置
    protected override bool UsesAnger => false;
    public PlayerController Target
    {
        get
        {
            if (party != null && party.isActiveAndEnabled && Alive(party.CurrentPlayer)) return party.CurrentPlayer;
            if (Alive(fallback)) return fallback;
            if (Time.time < resolveAt) return null;
            resolveAt = Time.time + BalanceRuntime.Number(this, "ResonantStarOwlCombat.Target.1", 0.5f);
            party = FindAnyObjectByType<PlayerManager>();
            if (party != null && Alive(party.CurrentPlayer)) return party.CurrentPlayer;
            foreach (PlayerController player in FindObjectsByType<PlayerController>())
                if (Alive(player)) { fallback = player; return player; }
            return null;
        }
    }
    private static bool Alive(PlayerController player) => player != null && player.isActiveAndEnabled
        && player.TryGetComponent(out Health hp) && !hp.Dead;

    public int ReadySkill
    {
        get
        {
            if (CurrentSkill >= 0 || health == null || health.Dead || health.Attacked
                || health.Status != null && health.Status.BlocksActions || Target == null) return -1;
            //在玩家头顶盘旋，只要水平距离不太远就能施法
            float dx = Mathf.Abs(Target.transform.position.x - transform.position.x);
            if (dx > BalanceRuntime.Number(this, "ResonantStarOwlCombat.ReadySkill.2", 7f)) return -1;
            if (jumps >= 2 && Time.time - lastJumpAt < BalanceRuntime.Number(this, "ResonantStarOwlCombat.ReadySkill.3", 5f) && Time.time >= readyAt[2]) return 2;
            bool air = !Target.GetComponent<PlayerMovement>().IsOnGroundNow();
            int first = air ? 1 : 0;
            if (Time.time >= readyAt[first]) return first;
            if (Time.time >= readyAt[1-first]) return 1-first;
            return Time.time >= readyAt[2] ? 2 : -1;
        }
    }
    protected override void Awake() { base.Awake(); health = GetComponent<Health>(); }
    protected override void OnEnable()
    {
        base.OnEnable(); CurrentSkill = -1; emitted = jumps = 0; wasJumping = false; lastJumpAt = -BalanceRuntime.Number(this, "ResonantStarOwlCombat.OnEnable.1", 99f);
        for (int i=0;i<3;i++) readyAt[i] = Time.time;
    }
    private void Update()
    {
        var target = Target;
        bool jumping = target != null && ResonantStarOwlEffect.IsJumping(target);
        // Rising velocity catches a second jump without requiring a landing between jumps.
        bool rising = jumping && target.GetComponent<Rigidbody2D>().linearVelocityY > BalanceRuntime.Number(this, "ResonantStarOwlCombat.Update.1", 0.1f);
        if (rising && !wasJumping) { jumps = Time.time-lastJumpAt < BalanceRuntime.Number(this, "ResonantStarOwlCombat.Update.2", 5f) ? jumps+1 : 1; lastJumpAt = Time.time; }
        wasJumping = rising;
    }
    public bool BeginCast(int skill)
    {
        if (skill < 0 || skill > 2 || CurrentSkill >= 0 || health.Dead || health.Attacked || health.Status != null && health.Status.BlocksActions || Target == null || Time.time < readyAt[skill]) return false;
        CurrentSkill = skill; castAt = Time.time; emitted = 0; readyAt[skill] = Time.time + Cooldowns[skill];
        FaceRight = Target.transform.position.x >= transform.position.x;
        CastOrigin = transform.position; CastTargetX = Target.transform.position.x;
        CastFloor = GetComponent<ResonantStarOwlMovement>().FindFloor(Target.transform.position);//以玩家脚下的地面为准
        if (skill == 2) jumps = 0;
        health.Status?.NotifyAttackStarted(); return true;
    }
    private float Elapsed => Time.time - castAt;
    // One-based design frames 6/9/12 and 6/11; hold release poses to honor real-time intervals.
    public int AnimationFrame
    {
        get
        {
            if (samplingFrame >= 0) return samplingFrame;
            float t = Elapsed;
            int count = CurrentSkill == 0 ? 3 : CurrentSkill == 1 ? 2 : 1;
            float interval = CurrentSkill == 0 ? BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.1", 0.4f) : BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.2", 0.5f);
            if (CurrentSkill >= 0 && emitted < count && t + BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.3", 0.0001f) >= BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.4", 5f)/BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.5", 16f) + emitted*interval)
                return CurrentSkill == 0 ? 5+emitted*3 : CurrentSkill == 1 ? 5+emitted*5 : 5;
            if (CurrentSkill == 0 && t >= BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.6", 0.375f) && t < BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.7", 0.5875f)) return 5;
            if (CurrentSkill == 0 && t >= BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.8", 0.775f) && t < BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.9", 0.9875f)) return 8;
            if (CurrentSkill == 1 && t >= BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.10", 0.375f) && t < BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.11", 0.5625f)) return 5;
            if (CurrentSkill == 0) { t -= Mathf.Clamp(t-BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.12", 6f)/BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.13", 16f),0f,BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.14", 0.2125f)); t -= Mathf.Clamp(Elapsed-BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.15", 0.775f),0f,BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.16", 0.2125f)); }
            else if (CurrentSkill == 1) t -= Mathf.Clamp(t-BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.17", 6f)/BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.18", 16f),0f,BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.19", 0.1875f));
            else if (CurrentSkill == 2) t -= Mathf.Clamp(t-BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.20", 11f)/BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.21", 16f),0f,BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.22", 1.5625f));
            return Mathf.Clamp(Mathf.FloorToInt(t*BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.23", 16f)+BalanceRuntime.Number(this, "ResonantStarOwlCombat.AnimationFrame.24", 0.0001f)),0,15);
        }
    }
    public bool Finished => CurrentSkill >= 0 && Elapsed >= (CurrentSkill == 0 ? BalanceRuntime.Number(this, "ResonantStarOwlCombat.Finished.1", 1.425f) : CurrentSkill == 1 ? BalanceRuntime.Number(this, "ResonantStarOwlCombat.Finished.2", 1.1875f) : BalanceRuntime.Number(this, "ResonantStarOwlCombat.Finished.3", 2.5625f));
    public void SampleCast()
    {
        if (CurrentSkill < 0) return;
        int max = CurrentSkill == 0 ? 3 : CurrentSkill == 1 ? 2 : 1;
        float interval = CurrentSkill == 0 ? BalanceRuntime.Number(this, "ResonantStarOwlCombat.SampleCast.1", 0.4f) : BalanceRuntime.Number(this, "ResonantStarOwlCombat.SampleCast.2", 0.5f);
        while (emitted < max && Elapsed + BalanceRuntime.Number(this, "ResonantStarOwlCombat.SampleCast.3", 0.0001f) >= BalanceRuntime.Number(this, "ResonantStarOwlCombat.SampleCast.4", 5f)/BalanceRuntime.Number(this, "ResonantStarOwlCombat.SampleCast.5", 16f) + emitted*interval)
        {
            int segment = emitted++;
            samplingFrame = CurrentSkill == 0 ? 5+segment*3 : CurrentSkill == 1 ? 5+segment*5 : 5;
            var prefab = CurrentSkill == 0 ? ringsPrefab : CurrentSkill == 1 ? chimePrefab : bandPrefab;
            var effect = ResonantStarOwlEffectPool.GetOrCreate().Emit(prefab, this, CurrentSkill, segment);
            if (CurrentSkill == 2) band = effect;
            SegmentReleased?.Invoke(CurrentSkill, segment);
            samplingFrame = -1;
            if (!isActiveAndEnabled || health.Dead || health.Attacked) break;
        }
    }
    public void FinishCast() { CurrentSkill = -1; band = null; }
    public void CancelCast() { if (band != null && band.gameObject.activeSelf) band.Cancel(); band = null; CurrentSkill = -1; }
    private void OnDisable() => CancelCast();
}

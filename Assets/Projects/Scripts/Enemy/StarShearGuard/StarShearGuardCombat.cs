using UnityEngine;

public sealed class StarShearGuardCombat : EnemyCombat
{
    [SerializeField] private StarShearGuardAttackBox[] hitBoxes;
    [SerializeField] private IndependentSkillEffect[] slashPrefabs;
    [SerializeField] private Vector2[] slashOffsets = { new Vector2(1.2f, .4f), new Vector2(1.1f, 1f), new Vector2(1.2f, -.2f) };
    // Zero-based, exclusive close; Inspector windows correspond to F5-8/F11-14/F6-10/F10-13.
    [SerializeField] private int[] windowOpen = { 4, 10, 5, 9 };
    [SerializeField] private int[] windowClose = { 8, 14, 10, 13 };
    [SerializeField, Min(0f)] private float scissorGap = .35f;
    [SerializeField, Min(0f)] private float secondMissRecovery = .45f;
    [SerializeField, Min(0f)] private float offbeatRange = 2.2f, upwardMinRange = 2f, upwardMaxRange = 5f;
    [SerializeField, Min(0f)] private float retreatRange = 5f, pointBlankRange = .9f;
    [SerializeField, Range(0f, 1f)] private float lowHealthRatio = .35f;
    private readonly bool[] opened = new bool[4], closed = new bool[4], hit = new bool[4];
    private Health health;
    private StarShearGuardMovement movement;
    private IndependentSkillEffect slash;
    private float firstClosedAt;
    public float RecoveryUntil { get; private set; }
    public bool FirstClosed => closed[0];
    public bool SecondOpened => opened[1];
    public bool WindowsClosed => Attack == 0 ? closed[1] : Attack >= 0 && closed[Attack + 1];
    public float GapUntil => firstClosedAt + scissorGap;
    public float GapHold(float fps) => Mathf.Max(0f, scissorGap - (windowOpen[1] - windowClose[0]) / Mathf.Max(1f, fps));
    public int EffectsEmitted { get; private set; }
    protected override bool UsesAnger => false;
    protected override void Awake()
    { base.Awake(); health = GetComponent<Health>(); movement = GetComponent<StarShearGuardMovement>(); }
    protected override void OnEnable() { base.OnEnable(); CancelCast(); }
    public bool SelectRequest(float delta)
    {
        float d = Mathf.Abs(delta);
        bool retreat = AttackRequest[2] && d <= retreatRange && (d <= pointBlankRange || health.ratio <= lowHealthRatio);
        int chosen = retreat ? 2 : AttackRequest[0] && d <= offbeatRange ? 0
            : AttackRequest[1] && d >= upwardMinRange && d <= upwardMaxRange ? 1 : -1;
        ClearAttackRequests(); if (chosen >= 0) AttackRequest[chosen] = true;
        return chosen >= 0;
    }
    public void BeginCast()
    {
        CancelCast(); System.Array.Clear(opened, 0, 4); System.Array.Clear(closed, 0, 4); System.Array.Clear(hit, 0, 4);
        EffectsEmitted = 0; RecoveryUntil = 0f; firstClosedAt = float.PositiveInfinity;
        foreach (var box in hitBoxes)
        {
            var collider = box.GetComponent<BoxCollider2D>(); var offset = collider.offset;
            offset.x = Mathf.Abs(offset.x) * (AttackFaceRight ? 1f : -1f); collider.offset = offset;
        }
        if (Attack == 2) movement.BeginBackstep(AttackFaceRight);
    }
    public bool IsWindowActive(int index) => Attack >= 0 && opened[index] && !closed[index]
        && ((Attack == 0 && index < 2) || index == Attack + 1);
    public void RecordWindowHit(int index)
    {
        hit[index] = true;
        // Preserve the shared whole-cast hit report, while retaining independent second-cut miss data.
        ReportAttackHit(hitBoxes[Attack == 0 ? 0 : Attack + 1]);
    }
    public void SampleFrame(int frame)
    {
        if (Attack < 0) return;
        if (Attack == 0) { SampleWindow(0, frame); if (Attack == 0) SampleWindow(1, frame); }
        else SampleWindow(Attack + 1, frame);
    }
    private void SampleWindow(int index, int frame)
    {
        if (closed[index] || frame < windowOpen[index]) return;
        if (index == 1 && (!closed[0] || Time.time + .0001f < GapUntil)) return;
        if (!opened[index])
        {
            opened[index] = true;
            if (index != 1) AttackBegin(); // Shared primary box and cast outcome bookkeeping.
            hitBoxes[index].gameObject.SetActive(true);
            if (index == 1) { slash?.Resume(); movement.BeginFollowStep(AttackFaceRight); }
            else
            {
                int skill = Attack; var offset = slashOffsets[skill]; offset.x *= AttackFaceRight ? 1f : -1f;
                slash = StarShearGuardEffectPool.Emit(skill, slashPrefabs[skill], transform, offset, AttackFaceRight);
                if (slash != null) slash.Completed.AddListener(OnSlashCompleted);
                EffectsEmitted++;
            }
            Physics2D.SyncTransforms(); hitBoxes[index].SampleNow();
            if (Attack < 0) return; // Reflection/death can synchronously cancel this cast.
        }
        if (frame < windowClose[index]) return;
        closed[index] = true; hitBoxes[index].gameObject.SetActive(false);
        if (index == 0) { firstClosedAt = Time.time; slash?.Pause(); }
        if (index == 1 && !hit[1]) RecoveryUntil = Time.time + secondMissRecovery;
    }
    public void CancelCast()
    {
        if (hitBoxes != null) foreach (var box in hitBoxes) if (box != null) box.gameObject.SetActive(false);
        if (slash != null)
        {
            slash.Completed.RemoveListener(OnSlashCompleted);
            if (slash.IsPlaying) slash.StopEffect();
        }
        slash = null; movement?.CancelStep(); ClearAttackRequests(); RecoveryUntil = 0f;
    }
    public void FinishVisual()
    {
        if (slash != null)
        { slash.Completed.RemoveListener(OnSlashCompleted); slash.SetFollow(null, Vector3.zero); }
        slash = null; // Completed casts let the independent visual finish its own lifecycle.
    }
    private void OnSlashCompleted()
    {
        if (slash != null) slash.Completed.RemoveListener(OnSlashCompleted);
        slash = null; // A later renter must never be stopped or detached by this old caster.
    }
    private void OnDisable() => CancelCast();
}

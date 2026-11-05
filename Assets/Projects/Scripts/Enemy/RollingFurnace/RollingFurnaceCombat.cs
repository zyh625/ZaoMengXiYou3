using UnityEngine;

// All modifiers belong to this component; pooling, death and interruption remove only this source.
public sealed class RollingFurnaceCombat : EnemyCombat
{
    public enum VentPhase { None, Closed, Open }
    [SerializeField] private RollingFurnaceAttackBox rollBox, reboundBox, slamBox;
    [SerializeField] private RollingFurnaceShockwavePool shockwaves;
    [SerializeField] private float rollCooldown = 6f, slamCooldown = 8f, ventCooldown = 12f;
    private Health health;
    private float rollReadyAt, slamReadyAt, ventReadyAt, ventStartedAt, accumulatedDamage;
    public VentPhase Vent { get; private set; }
    public bool VentReady => accumulatedDamage >= BalanceRuntime.Number(this, "RollingFurnaceCombat.VentReady.1", 40f) && Time.time >= ventReadyAt;
    public bool RollReady => Time.time >= rollReadyAt;
    public bool SlamReady => Time.time >= slamReadyAt;
    protected override bool UsesAnger => false;
    protected override void Awake() { base.Awake(); health = GetComponent<Health>(); }
    protected override void OnEnable()
    {
        base.OnEnable(); health = GetComponent<Health>();
        accumulatedDamage = 0f; rollReadyAt = slamReadyAt = ventReadyAt = Time.time;
        Vent = VentPhase.None; health.RemoveDamageMultiplier(this); health.SetSuperArmor(this, false);
        health.DamageReceived += Damaged; health.DamagePreparing += Preparing; health.Died += Died;
    }
    private void Damaged(DamageInfo info, float actual)
    { if (!health.Dead && Vent == VentPhase.None) accumulatedDamage += Mathf.Max(0f, actual); }
    private void Preparing(DamageInfo _) => AdvanceVent(Time.time);
    private void Update() => AdvanceVent(Time.time);
    public void BeginRoll() { CloseBoxes(); rollReadyAt = Time.time + rollCooldown; }
    public void BeginSlam() { CloseBoxes(); slamReadyAt = Time.time + slamCooldown; }
    public void OpenRoll(bool rebound)
    {
        CloseBoxes(); (rebound ? reboundBox : rollBox).gameObject.SetActive(true);
    }
    public void SampleRoll(bool rebound) => (rebound ? reboundBox : rollBox).SampleNow();
    public void Land(Vector2 position)
    {
        slamBox.gameObject.SetActive(true); Physics2D.SyncTransforms(); slamBox.SampleNow();
        slamBox.gameObject.SetActive(false); shockwaves.Emit(position, BalanceRuntime.Number(this, "RollingFurnaceCombat.Land.1", 5f));
    }
    public bool BeginVent()
    {
        if (!VentReady) return false;
        CloseBoxes(); accumulatedDamage = 0f; ventStartedAt = Time.time;
        ventReadyAt = Time.time + ventCooldown; Vent = VentPhase.Closed;
        health.SetDamageMultiplier(this, BalanceRuntime.Number(this, "RollingFurnaceCombat.BeginVent.1", 0.2f)); health.SetSuperArmor(this, true); return true;
    }
    public void AdvanceVent(float now)
    {
        if (Vent == VentPhase.None) return;
        if (now >= ventStartedAt + BalanceRuntime.Number(this, "RollingFurnaceCombat.AdvanceVent.1", 3f)) { EndVent(); return; }
        if (now >= ventStartedAt + BalanceRuntime.Number(this, "RollingFurnaceCombat.AdvanceVent.2", 2f) && Vent == VentPhase.Closed)
        {
            Vent = VentPhase.Open; health.SetDamageMultiplier(this, BalanceRuntime.Number(this, "RollingFurnaceCombat.AdvanceVent.3", 1.5f));
            health.SetSuperArmor(this, false); health.AttackedEnd();
        }
    }
    public void EndVent()
    {
        Vent = VentPhase.None;
        if (health != null) { health.RemoveDamageMultiplier(this); health.SetSuperArmor(this, false); }
    }
    public void CloseBoxes()
    {
        if (rollBox != null) rollBox.gameObject.SetActive(false);
        if (reboundBox != null) reboundBox.gameObject.SetActive(false);
        if (slamBox != null) slamBox.gameObject.SetActive(false);
    }
    private void Died(DamageInfo _) { CloseBoxes(); EndVent(); }
    private void OnDisable()
    {
        if (health != null)
        { health.DamageReceived -= Damaged; health.DamagePreparing -= Preparing; health.Died -= Died; }
        CloseBoxes(); EndVent();
    }
}

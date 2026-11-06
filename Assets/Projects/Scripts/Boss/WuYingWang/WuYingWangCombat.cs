using System;
using UnityEngine;

public sealed class WuYingWangCombat : EnemyCombat
{
    [SerializeField, Min(1f)] private float maxRage = 100f;
    [SerializeField, Min(0f)] private float rageOnDamage = 3f;
    [SerializeField, Min(0f)] private float rageOnMiss = 10f;
    private Health health;
    public float MaxRage => maxRage;
    public float Rage { get; private set; }
    public bool IsFlying { get; private set; }
    public event Action RageChanged;
    protected override bool UsesAnger => false;
    public override float DamageMultiplier => base.DamageMultiplier * (1f + Rage / maxRage);

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Health>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Rage = 0f;
        IsFlying = false;
        health.DamageReceived += OnDamageReceived;
        RageChanged?.Invoke();
    }

    private void OnDisable()
    {
        health.DamageReceived -= OnDamageReceived;
        IsFlying = false;
    }
    private void OnDamageReceived(DamageInfo damage, float amount)
    {
        if (!damage.IsPeriodic && amount > 0f) AddRage(rageOnDamage);
    }

    protected override void OnAttackMissed(int attack) => ReportMiss();
    public void ReportMiss() => AddRage(rageOnMiss);

    private void AddRage(float amount)
    {
        if (health.Dead || Rage >= maxRage) return;
        Rage = Mathf.Min(maxRage, Rage + amount);
        RageChanged?.Invoke();
    }

    public void EnterFlight()
    {
        IsFlying = true;
        RageChanged?.Invoke();
    }
}

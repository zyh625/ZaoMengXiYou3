using UnityEngine;

public sealed class BirdEnemyCombat : EnemyCombat
{
    [SerializeField, Range(0f, 1f)] private float eliteChance = 0.25f;
    [SerializeField, Min(1f)] private float eliteHealthMultiplier = 2f;
    [SerializeField, Min(1f)] private float eliteDamageMultiplier = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float eliteCooldownMultiplier = 0.8f;
    [SerializeField, Min(0f)] private float eliteExperience = 8f;
    private float normalHealth;
    public bool IsElite { get; private set; }

    protected override bool UsesAnger => false;
    protected override float AttackCooldownMultiplier => IsElite ? eliteCooldownMultiplier : 1f;
    public override float DamageMultiplier => base.DamageMultiplier * (IsElite ? eliteDamageMultiplier : 1f);

    public void PrepareSpawn()
    {
        IsElite = Random.value < eliteChance;
        if (!IsElite) return;
        Health health = GetComponent<Health>();
        normalHealth = health.BeginBlood;
        health.SetBaseBlood(normalHealth * eliteHealthMultiplier);
        GetComponent<EnemyRewards>().ExperienceOverride = eliteExperience;
    }

    private void OnDisable()
    {
        if (!IsElite) return;
        GetComponent<Health>().SetBaseBlood(normalHealth);
        GetComponent<EnemyRewards>().ExperienceOverride = null;
        IsElite = false;
    }
}

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ClampGuardBurn : MonoBehaviour
{
    private struct Burn { public Health Source; public int SourceLife, VictimLife, Tick, Limit; public float Began, Damage, Interval; }
    private readonly List<Burn> burns = new();
    private Health health;
    private int epoch;
    public static void Apply(Health victim, Health source, float multiplier = 1f,
        float tickDamage = 3f, float duration = 2f, float interval = 1f)
    {
        if (victim == null || source == null || victim.Dead || !victim.isActiveAndEnabled || victim.IsInvincible) return;
        var effect = victim.GetComponent<ClampGuardBurn>() ?? victim.gameObject.AddComponent<ClampGuardBurn>();
        effect.epoch++;
        interval = Mathf.Max(.01f, interval);//保留原定时结算流程，只替换本次灼烧参数。
        var entry = new Burn { Source = source, SourceLife = source.LifeVersion, VictimLife = victim.LifeVersion,
            Began = Time.time, Damage = tickDamage * multiplier, Tick = 0, Interval = interval,
            Limit = Mathf.Max(0, Mathf.FloorToInt(duration / interval + .0001f)) };
        for (int i = 0; i < effect.burns.Count; i++)
            if (effect.burns[i].Source == source) { effect.burns[i] = entry; return; }
        effect.burns.Add(entry);
    }
    private void Awake() => health = GetComponent<Health>();
    private void OnEnable() { health.DebuffsCleared += Clear; health.Died += Died; }
    private void Update()
    {
        int version = epoch;
        for (int i = burns.Count - 1; i >= 0; i--)
        {
            var b = burns[i];
            if (b.Source == null || b.Source.LifeVersion != b.SourceLife || health.LifeVersion != b.VictimLife)
            { burns.RemoveAt(i); continue; }
            while (b.Tick < b.Limit && Time.time + .0001f >= b.Began + (b.Tick + 1) * b.Interval)
            {
                b.Tick++;
                // Publish before callbacks; a damage callback can dispel or refresh this source.
                burns[i] = b;
                health.TakeDamage(new DamageInfo(b.Source.transform, b.Damage, false, false, null, false, true, DamageKind.Skill));
                if (version != epoch || health.Dead || !isActiveAndEnabled) return;
            }
            if (b.Tick >= b.Limit) burns.RemoveAt(i); else burns[i] = b;
        }
    }
    private void Died(DamageInfo _) => Clear();
    private void Clear() { epoch++; burns.Clear(); }
    private void OnDisable() { health.DebuffsCleared -= Clear; health.Died -= Died; Clear(); }
}

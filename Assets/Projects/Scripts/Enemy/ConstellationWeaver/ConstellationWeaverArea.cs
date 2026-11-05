using System.Collections.Generic;
using UnityEngine;

// Line and knot placeholders share a query path; each query deduplicates multi-collider players.
public sealed class ConstellationWeaverArea : MonoBehaviour
{
    [SerializeField] private LineRenderer line;
    private ConstellationWeaverCombat owner;
    private bool silk, pulsed;
    private float expiry, scale;
    private Vector2 from, to;
    private readonly Collider2D[] hits = new Collider2D[64];
    private readonly HashSet<Health> inside = new(), previous = new();
    private readonly Dictionary<Health, float> nextHit = new();
    public void Show(ConstellationWeaverCombat source, Vector2 a, Vector2 b, bool isSilk)
    {
        owner = source; scale = source.DamageMultiplier; silk = isSilk; from = a; to = b;
        pulsed = false; expiry = float.PositiveInfinity; inside.Clear(); previous.Clear(); nextHit.Clear();
        transform.position = a; gameObject.SetActive(true); line.enabled = true;
        line.useWorldSpace = true; line.positionCount = silk ? 2 : 33;
        if (silk) { line.SetPosition(0, a); line.SetPosition(1, b); expiry = Time.time + BalanceRuntime.Number(this, "ConstellationWeaverArea.Show.1", 10f); }
        else for (int i = 0; i < 33; i++)
        { float angle = i * Mathf.PI / BalanceRuntime.Number(this, "ConstellationWeaverArea.Show.2", 16f); line.SetPosition(i, a + new Vector2(Mathf.Cos(angle) * BalanceRuntime.Number(this, "ConstellationWeaverArea.Show.3", 0.75f), Mathf.Sin(angle) * BalanceRuntime.Number(this, "ConstellationWeaverArea.Show.4", 0.08f))); }
        SetColor(BalanceRuntime.Number(this, "ConstellationWeaverArea.Show.5", 0.25f));
    }
    private void SetColor(float alpha) => line.startColor = line.endColor = new Color(BalanceRuntime.Number(this, "ConstellationWeaverArea.SetColor.1", 0.15f), BalanceRuntime.Number(this, "ConstellationWeaverArea.SetColor.2", 0.85f), 1f, alpha);
    public void Pulse()
    {
        if (pulsed || !gameObject.activeSelf) return;
        pulsed = true; expiry = Time.time + BalanceRuntime.Number(this, "ConstellationWeaverArea.Pulse.1", 0.25f); SetColor(BalanceRuntime.Number(this, "ConstellationWeaverArea.Pulse.2", 0.55f)); Query(true);
    }
    private void FixedUpdate()
    {
        if (Time.time >= expiry) { if (silk) owner?.ExpireSilk(); else Recycle(); return; }
        if (silk) Query(false);
    }
    private void Query(bool pulse)
    {
        ContactFilter2D filter = new(); filter.SetLayerMask(1 << 3); filter.useTriggers = true;
        Vector2 center = pulse ? from : (from + to) * BalanceRuntime.Number(this, "ConstellationWeaverArea.Query.1", 0.5f);
        Vector2 size = new(pulse ? BalanceRuntime.Number(this, "ConstellationWeaverArea.Query.2", 1.5f) : Mathf.Abs(to.x - from.x), BalanceRuntime.Number(this, "ConstellationWeaverArea.Query.3", 0.24f));
        int count = Physics2D.OverlapBox(center, size, 0f, filter, hits); inside.Clear();
        for (int i = 0; i < count; i++)
        {
            var player = hits[i].GetComponentInParent<PlayerController>();
            if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) continue;
            var hp = player.GetComponent<Health>();
            if (hp == null || hp.Dead || !inside.Add(hp)) continue;
            if (!pulse && (previous.Contains(hp) || nextHit.TryGetValue(hp, out float time) && Time.time < time)) continue;
            nextHit[hp] = Time.time + BalanceRuntime.Number(this, "ConstellationWeaverArea.Query.4", 0.5f);
            float before = hp.CurBlood;
            hp.TakeDamage(new DamageInfo(owner.transform, (pulse ? BalanceRuntime.Number(this, "ConstellationWeaverArea.Query.5", 6f) : BalanceRuntime.Number(this, "ConstellationWeaverArea.Query.6", 8f)) * scale, false, kind: DamageKind.Skill));
            if (hp.CurBlood < before && !hp.Dead) ConstellationWeaverSlow.Apply(hp, pulse ? BalanceRuntime.Number(this, "ConstellationWeaverArea.Query.7", 1.5f) : 1f);
            if (!gameObject.activeSelf) return; // Reflection/death can synchronously recycle the whole trap.
        }
        previous.Clear(); foreach (var hp in inside) previous.Add(hp);
    }
    public void Recycle() { owner = null; gameObject.SetActive(false); }
}

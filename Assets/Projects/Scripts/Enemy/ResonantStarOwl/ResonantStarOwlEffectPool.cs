using System.Collections.Generic;
using UnityEngine;

// Each of the three prefab keys owns an independent scene-local pool.
public sealed class ResonantStarOwlEffectPool : MonoBehaviour
{
    private static ResonantStarOwlEffectPool current;
    private readonly Dictionary<ResonantStarOwlEffect, PrefabPool<ResonantStarOwlEffect>> pools = new();
    private Transform inactiveRoot;
    private bool used;
    public static ResonantStarOwlEffectPool GetOrCreate()
    {
        if (current != null && current.isActiveAndEnabled) return current;
        current = FindAnyObjectByType<ResonantStarOwlEffectPool>();
        if (current == null) current = new GameObject("ResonantStarOwlEffectPool").AddComponent<ResonantStarOwlEffectPool>();
        return current;
    }
    private void Awake()
    {
        GameObject inactive = new("InactiveCreation"); inactive.transform.SetParent(transform, false);
        inactive.SetActive(false); inactiveRoot = inactive.transform;
    }
    private ResonantStarOwlEffect Get(ResonantStarOwlEffect prefab)
    {
        if (prefab == null || !BattleRun.IsRunning) return null;
        if (!pools.TryGetValue(prefab, out var pool))
        { pool = new PrefabPool<ResonantStarOwlEffect>(); pools.Add(prefab, pool); pool.Prewarm(prefab, 2, transform, inactiveRoot); }
        used = true; return pool.Get(prefab, transform, inactiveRoot);
    }
    public ResonantStarOwlEffect Emit(ResonantStarOwlEffect prefab, ResonantStarOwlCombat source, int skill, int segment)
    { var effect = Get(prefab); effect?.Begin(source, skill, segment); return effect; }
    public void ReleaseAll() { foreach (var pool in pools.Values) pool.ReleaseAll(); used = false; }
    private void LateUpdate() { if (used && !BattleRun.IsRunning) ReleaseAll(); }
    private void OnDisable() => ReleaseAll();
    private void OnDestroy() { if (current == this) current = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetPool() => current = null;
}

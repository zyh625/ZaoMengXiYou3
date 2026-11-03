using System.Collections.Generic;
using UnityEngine;

// Scene ownership is essential: recycling/killing the shooter never cancels flying bombs.
public sealed class PressureKettleEffectPool : MonoBehaviour
{
    private static PressureKettleEffectPool current;
    private readonly Dictionary<PressureKettleEffect, PrefabPool<PressureKettleEffect>> pools = new();
    private Transform inactiveRoot;
    private bool used;
    public static PressureKettleEffectPool GetOrCreate()
    {
        if (current != null && current.isActiveAndEnabled) return current;
        current = FindAnyObjectByType<PressureKettleEffectPool>();
        if (current == null) current = new GameObject("PressureKettleEffectPool").AddComponent<PressureKettleEffectPool>();
        return current;
    }
    private void Awake()
    {
        GameObject inactive = new("InactiveCreation"); inactive.transform.SetParent(transform, false);
        inactive.SetActive(false); inactiveRoot = inactive.transform;
    }
    private PressureKettleEffect Get(PressureKettleEffect prefab)
    {
        if (prefab == null || !BattleRun.IsRunning) return null;
        if (!pools.TryGetValue(prefab, out var pool))
        { pool = new PrefabPool<PressureKettleEffect>(); pools.Add(prefab, pool); pool.Prewarm(prefab, 2, transform, inactiveRoot); }
        used = true; return pool.Get(prefab, transform, inactiveRoot);
    }
    public PressureKettleEffect Steam(PressureKettleEffect prefab, PressureKettleCombat source)
    { var effect = Get(prefab); effect?.BeginSteam(source); return effect; }
    public PressureKettleEffect Bomb(PressureKettleEffect prefab, PressureKettleCombat source, PlayerController target)
    { var effect = Get(prefab); effect?.Launch(source, target); return effect; }
    public PressureKettleEffect Burst(PressureKettleEffect prefab, PressureKettleCombat source)
    { var effect = Get(prefab); effect?.BeginBurst(source); return effect; }
    public void ReleaseAll() { foreach (var pool in pools.Values) pool.ReleaseAll(); used = false; }
    private void LateUpdate() { if (used && !BattleRun.IsRunning) ReleaseAll(); }
    private void OnDisable() => ReleaseAll();
    private void OnDestroy() { if (current == this) current = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetPool() => current = null;
}

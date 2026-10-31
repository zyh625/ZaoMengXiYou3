using System.Collections.Generic;
using UnityEngine;

//独立冰锥、剑阵和冰盾分别缓存，允许冷却比旧效果持续时间更短时扩容。
public sealed class SouthKingEffectPool : MonoBehaviour
{
    private static SouthKingEffectPool current;
    private readonly Dictionary<SouthKingEffect, PrefabPool<SouthKingEffect>> pools = new();
    private Transform inactiveRoot;
    public static SouthKingEffectPool GetOrCreate()
    {
        if (current != null && current.isActiveAndEnabled) return current;
        current = FindAnyObjectByType<SouthKingEffectPool>();
        if (current == null) current = new GameObject("SouthKingEffectPool").AddComponent<SouthKingEffectPool>();
        return current;
    }
    private void Awake()
    {
        if (current == null) current = this;
        GameObject inactive = new("InactiveIceCreation"); inactive.transform.SetParent(transform, false);
        inactive.SetActive(false); inactiveRoot = inactive.transform;
    }
    public SouthKingEffect Fire(SouthKingEffect prefab, SouthKingCombat source, PlayerManager players, Vector2 position, bool right)
    {
        if (prefab == null || source == null || !BattleRun.IsRunning) return null;
        if (!pools.TryGetValue(prefab, out PrefabPool<SouthKingEffect> pool))
        {
            pool = new PrefabPool<SouthKingEffect>(); pools.Add(prefab, pool);
            pool.Prewarm(prefab, 2, transform, inactiveRoot);
        }
        SouthKingEffect effect = pool.Get(prefab, transform, inactiveRoot);
        effect.Launch(source, players, position, right); return effect;
    }
    private void LateUpdate() { if (!BattleRun.IsRunning) ReleaseAll(); }
    private void ReleaseAll() { foreach (var pool in pools.Values) pool.ReleaseAll(); }
    private void OnDisable() => ReleaseAll();
    private void OnDestroy() { if (current == this) current = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCurrent() => current = null;
}

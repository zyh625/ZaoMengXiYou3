using System.Collections.Generic;
using UnityEngine;

public sealed class RedChainShockWavePool : MonoBehaviour
{
    private static RedChainShockWavePool current;
    private readonly Dictionary<RedChainShockWave, PrefabPool<RedChainShockWave>> pools = new();
    private Transform inactiveRoot;

    public static RedChainShockWavePool GetOrCreate()
    {
        if (current != null && current.isActiveAndEnabled) return current;
        current = FindAnyObjectByType<RedChainShockWavePool>();
        if (current == null) current = new GameObject("RedChainShockWavePool").AddComponent<RedChainShockWavePool>();
        return current;
    }

    private void Awake()
    {
        GameObject creation = new("InactiveCreation");
        creation.transform.SetParent(transform, false);
        creation.SetActive(false);
        inactiveRoot = creation.transform;
    }

    public RedChainShockWave Spawn(RedChainShockWave prefab, Vector2 position, SpriteRenderer owner)
    {
        if (prefab == null) return null;
        if (!pools.TryGetValue(prefab, out PrefabPool<RedChainShockWave> pool))
        {
            pool = new PrefabPool<RedChainShockWave>();
            pools.Add(prefab, pool);
            pool.Prewarm(prefab, 2, transform, inactiveRoot);
        }
        RedChainShockWave wave = pool.Get(prefab, transform, inactiveRoot);
        wave.Play(position, owner);
        return wave;
    }

    private void OnDisable() { foreach (var pool in pools.Values) pool.ReleaseAll(); }
    private void OnDestroy() { if (current == this) current = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScenePool() => current = null;
}

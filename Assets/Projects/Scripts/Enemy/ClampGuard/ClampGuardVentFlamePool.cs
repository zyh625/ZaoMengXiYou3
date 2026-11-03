using UnityEngine;

// Visual-only pool: either player or enemy may request the same prefab, with no allegiance or damage logic.
public sealed class ClampGuardVentFlamePool : MonoBehaviour
{
    private static ClampGuardVentFlamePool instance;
    private readonly PrefabPool<IndependentSkillEffect> pool = new();
    private Transform activeRoot, inactiveRoot;
    private IndependentSkillEffect cachedPrefab;
    private static ClampGuardVentFlamePool GetOrCreate()
    {
        if (instance != null) return instance;
        return instance = new GameObject("ClampGuardVentFlamePool").AddComponent<ClampGuardVentFlamePool>();
    }
    private void Awake()
    {
        activeRoot = new GameObject("Active").transform; activeRoot.SetParent(transform, false);
        inactiveRoot = new GameObject("Inactive").transform; inactiveRoot.SetParent(transform, false);
        inactiveRoot.gameObject.SetActive(false);
    }
    public static IndependentSkillEffect Emit(IndependentSkillEffect prefab, Vector3 position, bool faceRight,
        Transform follow = null, Vector3 followOffset = default)
    {
        if (prefab == null) return null;
        var host = GetOrCreate();
        // This pool owns one visual asset; the public API is independent of the attacker's class.
        if (host.cachedPrefab != null && host.cachedPrefab != prefab) return null;
        host.cachedPrefab = prefab;
        var effect = host.pool.Get(prefab, host.activeRoot, host.inactiveRoot);
        effect.transform.position = position;
        effect.transform.localScale = Vector3.one * (3f / 1.88f);
        effect.Play(follow, followOffset, faceRight, 0f);
        return effect;
    }
    public void ReleaseAll() => pool.ReleaseAll();
    private void OnDisable() => pool.ReleaseAll();
    private void OnDestroy() { if (instance == this) instance = null; }
}

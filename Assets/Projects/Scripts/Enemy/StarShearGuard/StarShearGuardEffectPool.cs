using UnityEngine;

// One cache per slash; visual instances never own hit detection or enemy state.
public sealed class StarShearGuardEffectPool : MonoBehaviour
{
    private static StarShearGuardEffectPool instance;
    private readonly PrefabPool<IndependentSkillEffect>[] pools =
    { new PrefabPool<IndependentSkillEffect>(), new PrefabPool<IndependentSkillEffect>(), new PrefabPool<IndependentSkillEffect>() };
    private readonly IndependentSkillEffect[] prefabs = new IndependentSkillEffect[3];
    private Transform activeRoot, inactiveRoot;
    private void Awake()
    {
        activeRoot = new GameObject("Active").transform; activeRoot.SetParent(transform, false);
        inactiveRoot = new GameObject("Inactive").transform; inactiveRoot.SetParent(transform, false);
        inactiveRoot.gameObject.SetActive(false);
    }
    public static IndependentSkillEffect Emit(int index, IndependentSkillEffect prefab, Transform source,
        Vector3 offset, bool faceRight)
    {
        if (prefab == null || source == null || index < 0 || index >= 3) return null;
        if (instance == null) instance = new GameObject("StarShearGuardEffectPool").AddComponent<StarShearGuardEffectPool>();
        if (instance.prefabs[index] != null && instance.prefabs[index] != prefab) return null;
        instance.prefabs[index] = prefab;
        var effect = instance.pools[index].Get(prefab, instance.activeRoot, instance.inactiveRoot);
        effect.transform.localScale = prefab.transform.localScale;
        effect.transform.position = source.position + offset;
        effect.Play(source, offset, faceRight);
        return effect;
    }
    public void ReleaseAll() { foreach (var pool in pools) pool.ReleaseAll(); }
    private void OnDisable() => ReleaseAll();
    private void OnDestroy() { if (instance == this) instance = null; }
}

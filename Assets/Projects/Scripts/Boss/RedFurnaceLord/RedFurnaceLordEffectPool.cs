using UnityEngine;

public sealed class RedFurnaceLordEffectPool : MonoBehaviour
{
    [SerializeField] private RedFurnaceLordEffect prefab;
    [SerializeField, Min(1)] private int prewarm = 1;
    private readonly PrefabPool<RedFurnaceLordEffect> pool = new();
    private Transform root, creation;
    public void Play(Vector2 at, float width, bool right, float duration)
    {
        if (root == null)
        {
            root = new GameObject(name + "_WorldEffects").transform;
            creation = new GameObject("InactiveCreation").transform;
            creation.SetParent(root, false); creation.gameObject.SetActive(false);
            pool.Prewarm(prefab, prewarm, root, creation);
        }
        pool.Get(prefab, root, creation).Play(at, width, right, duration);
    }
    public void ReleaseAll() => pool.ReleaseAll();
    private void OnDisable() => ReleaseAll();
    private void OnDestroy() { if (root != null) Destroy(root.gameObject); }
}

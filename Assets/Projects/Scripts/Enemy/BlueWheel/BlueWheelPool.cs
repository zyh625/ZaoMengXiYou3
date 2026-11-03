using System.Collections.Generic;
using UnityEngine;
using zmxy.GamePlay;

//第四关飞轮的场景级池；敌人死亡只改变飞行方向，不销毁仍在场上的投射物。
public sealed class BlueWheelPool : MonoBehaviour
{
    [SerializeField, Min(0)] private int prewarmCount = 4;//双轮与多个敌人并发时可继续扩容。
    [SerializeField, Min(0f)] private float boundaryPadding = 2f;
    [SerializeField, Min(1f)] private float fallbackDistance = 30f;//独立测试缺少相机时的安全回收边界。
    private static BlueWheelPool current;
    private readonly Dictionary<BlueBoomearng, PrefabPool<BlueBoomearng>> pools = new();
    private Transform inactiveRoot;
    private PlayerManager players;
    private CameraManager cameraManager;
    private bool hasWheels;
    public static BlueWheelPool GetOrCreate()
    {
        if (current != null && current.isActiveAndEnabled) return current;
        current = FindAnyObjectByType<BlueWheelPool>();
        if (current == null) current = new GameObject("BlueWheelPool").AddComponent<BlueWheelPool>();
        return current;
    }
    private void Awake()
    {
        if (current == null) current = this;
        GameObject creation = new("InactiveWheelCreation");
        creation.transform.SetParent(transform, false);
        creation.SetActive(false);
        inactiveRoot = creation.transform;
        ResolveReferences();
    }
    private void ResolveReferences()
    {
        if (players == null || !players.isActiveAndEnabled || players.CurrentPlayer == null)
            foreach (PlayerManager candidate in FindObjectsByType<PlayerManager>())
                if (candidate.isActiveAndEnabled && candidate.CurrentPlayer != null) { players = candidate; break; }
        if (cameraManager == null && Camera.main != null) cameraManager = Camera.main.GetComponent<CameraManager>();
    }
    public BlueBoomearng Fire(BlueBoomearng prefab, BlueWheelCombat source, Vector2 position, bool right, bool hidden, float delay)
    {
        if (prefab == null || source == null || !source.isActiveAndEnabled || !BattleRun.IsRunning) return null;
        ResolveReferences();
        if (!pools.TryGetValue(prefab, out PrefabPool<BlueBoomearng> pool))
        {
            pool = new PrefabPool<BlueBoomearng>();
            pools.Add(prefab, pool);
            pool.Prewarm(prefab, prewarmCount, transform, inactiveRoot);
        }
        Vector2 bounds = new(position.x - Mathf.Max(1f, fallbackDistance), position.x + Mathf.Max(1f, fallbackDistance));
        if (cameraManager != null && cameraManager.CameraHalf > 0f)
            bounds = new Vector2(cameraManager.EdgeL - cameraManager.CameraHalf - boundaryPadding,
                cameraManager.EdgeR + cameraManager.CameraHalf + boundaryPadding);
        BlueBoomearng wheel = pool.Get(prefab, transform, inactiveRoot);
        wheel.Launch(source, players, position, right, bounds, hidden, delay);
        hasWheels = true;
        return wheel;
    }
    private void LateUpdate()
    {
        if (hasWheels && !BattleRun.IsRunning) ReleaseAll();
    }
    public void ReleaseAll()
    {
        foreach (PrefabPool<BlueBoomearng> pool in pools.Values) pool.ReleaseAll();
        hasWheels = false;
    }
    private void OnDisable() => ReleaseAll();
    private void OnDestroy() { if (current == this) current = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScenePool() => current = null;
}

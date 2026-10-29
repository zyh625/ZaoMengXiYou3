using System.Collections.Generic;
using UnityEngine;
using zmxy.GamePlay;

//敌方投射物的场景级对象池，射手停用或死亡不会回收仍在飞行的箭。
public class EnemyProjectileManager : MonoBehaviour
{
    [SerializeField, Min(0)] private int prewarmCount = 2;//每种箭首次使用时预热，数量不足自动扩容
    [SerializeField, Min(0f)] private float boundaryPadding = 2f;//越过场地可见外边界后留出的回收距离
    [SerializeField, Min(1f)] private float fallbackDistance = 35f;//测试场景无相机边界时使用的单侧安全距离
    private static EnemyProjectileManager current;//仅缓存当前场景实例，销毁时释放静态引用
    private readonly Dictionary<NormalArrowManager, PrefabPool<NormalArrowManager>> pools = new();
    private Transform inactiveRoot;//在停用容器中实例化，发射前不会显示在预制体旧位置
    private PlayerManager players;
    private CameraManager cameraManager;
    private bool hasProjectiles;//关卡结束只统一回收一次

    public static EnemyProjectileManager GetOrCreate()
    {
        if (current != null) return current;
        current = FindFirstObjectByType<EnemyProjectileManager>();
        if (current == null) current = new GameObject("EnemyProjectilePool").AddComponent<EnemyProjectileManager>();
        return current;
    }
    private void Awake()
    {
        if (current == null) current = this;
        GameObject creation = new("InactiveProjectileCreation");
        creation.transform.SetParent(transform, false);
        creation.SetActive(false);
        inactiveRoot = creation.transform;
        players = FindFirstObjectByType<PlayerManager>();
        if (Camera.main != null) cameraManager = Camera.main.GetComponent<CameraManager>();
    }
    public void Fire(NormalArrowManager prefab, EnemyCombat source, Vector2 position, bool right)
    {
        if (prefab == null || source == null || !BattleRun.IsRunning) return;
        if (players == null) players = FindFirstObjectByType<PlayerManager>();
        if (!pools.TryGetValue(prefab, out PrefabPool<NormalArrowManager> pool))
        {
            pool = new PrefabPool<NormalArrowManager>();
            pools.Add(prefab, pool);
            pool.Prewarm(prefab, prewarmCount, transform, inactiveRoot);
        }
        Vector2 limits = new(position.x - Mathf.Max(1f, fallbackDistance), position.x + Mathf.Max(1f, fallbackDistance));
        if (cameraManager != null)
            limits = new Vector2(cameraManager.EdgeL - cameraManager.CameraHalf - boundaryPadding,
                cameraManager.EdgeR + cameraManager.CameraHalf + boundaryPadding);
        NormalArrowManager arrow = pool.Get(prefab, transform, inactiveRoot);
        arrow.Launch(source, players, position, right, limits);
        hasProjectiles = true;
    }
    private void LateUpdate()
    {
        if (hasProjectiles && !BattleRun.IsRunning) ReleaseAll();
    }
    public void ReleaseAll()//终局或场景回收时统一停止所有箭，不操作正在遍历的伤害列表
    {
        foreach (PrefabPool<NormalArrowManager> pool in pools.Values) pool.ReleaseAll();
        hasProjectiles = false;
    }
    private void OnDisable() => ReleaseAll();
    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}

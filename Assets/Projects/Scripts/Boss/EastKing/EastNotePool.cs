using System.Collections.Generic;
using UnityEngine;
using zmxy.GamePlay;

//四种音符共用场景对象池，冷却短于飞行时间时会扩容，不占用上一枚仍在飞行的音符。
public class EastNotePool : MonoBehaviour
{
    [SerializeField, Min(0)] private int prewarmCount = 2;//每种音符首次出现时预热的数量
    [SerializeField, Min(1f)] private float fallbackHalfWidth = 12f;//测试场景未设置相机时的单侧场地范围
    private static EastNotePool current;//只缓存当前场景的池，场景卸载后立即清空
    private readonly Dictionary<EastNoteManager, PrefabPool<EastNoteManager>> pools = new();//四类模板各用自己的池，不混用轨迹数据
    private Transform inactiveRoot;//停用容器防止新实例在模板位置闪现
    private PlayerManager players;//场景角色队列只查找一次，实际目标由音符每物理步读取
    private LevelEnemyManager enemies;//蓝色音符把召唤请求交回当前关卡管理器
    private CameraManager cameraManager;//出生时保存当前Boss战的可见左右边界
    private bool hasNotes;//战斗结束时仅统一回收一次

    public static EastNotePool GetOrCreate()//预制体不保存场景对象引用，首枚音符自动取得本关池
    {
        if (current != null) return current;
        current = FindAnyObjectByType<EastNotePool>();
        if (current == null) current = new GameObject("EastNotePool").AddComponent<EastNotePool>();
        return current;
    }

    private void Awake()
    {
        if (current == null) current = this;
        GameObject creation = new("InactiveNoteCreation");
        creation.transform.SetParent(transform, false);
        creation.SetActive(false);
        inactiveRoot = creation.transform;
        ResolveSceneReferences();
    }

    private void ResolveSceneReferences(EastCombat source = null)//只选正在使用的场景组件，避免拾取关卡布局中的禁用模板
    {
        if (players == null || !players.isActiveAndEnabled || players.CurrentPlayer == null)
            foreach (PlayerManager manager in FindObjectsByType<PlayerManager>())
                if (manager.isActiveAndEnabled && manager.CurrentPlayer != null) { players = manager; break; }
        if (enemies == null || !enemies.isActiveAndEnabled || source != null && enemies.CurBoss != source.transform)
        {
            LevelEnemyManager fallback = null;//独立测试可以没有登记Boss的管理器，正式关卡优先匹配实际Boss拥有者
            foreach (LevelEnemyManager manager in FindObjectsByType<LevelEnemyManager>())
            {
                if (!manager.isActiveAndEnabled) continue;
                if (fallback == null) fallback = manager;
                if (source != null && manager.CurBoss == source.transform) { enemies = manager; break; }
            }
            if (enemies == null || !enemies.isActiveAndEnabled || source != null && enemies.CurBoss != source.transform)
                enemies = fallback;
        }
        if (cameraManager == null && Camera.main != null) cameraManager = Camera.main.GetComponent<CameraManager>();
    }

    public void Fire(EastNoteManager prefab, EastCombat source, Vector2 position, bool right)//第10动作帧仅由Boss Combat调用一次
    {
        if (prefab == null || source == null || !BattleRun.IsRunning) return;
        ResolveSceneReferences(source);
        if (!pools.TryGetValue(prefab, out PrefabPool<EastNoteManager> pool))
        {
            pool = new PrefabPool<EastNoteManager>();
            pools.Add(prefab, pool);
            pool.Prewarm(prefab, prewarmCount, transform, inactiveRoot);
        }
        Vector2 bounds = new(position.x - fallbackHalfWidth, position.x + fallbackHalfWidth);
        if (cameraManager != null && cameraManager.CameraHalf > 0f)
            bounds = new Vector2(cameraManager.EdgeL - cameraManager.CameraHalf,
                cameraManager.EdgeR + cameraManager.CameraHalf);
        EastNoteManager note = pool.Get(prefab, transform, inactiveRoot);
        note.Launch(source, players, enemies, position, right, bounds);
        hasNotes = true;
    }

    private void LateUpdate()
    {
        if (hasNotes && !BattleRun.IsRunning) ReleaseAll();
    }

    public void ReleaseAll()//胜负结算、退出关卡或管理器停用时，所有颜色同时停止产生效果
    {
        foreach (PrefabPool<EastNoteManager> pool in pools.Values) pool.ReleaseAll();
        hasNotes = false;
    }

    private void OnDisable() => ReleaseAll();
    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}

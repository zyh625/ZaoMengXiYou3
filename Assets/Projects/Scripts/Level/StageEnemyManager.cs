using System;
using System.Collections.Generic;
using UnityEngine;
using zmxy.GamePlay;

//关卡只配置敌人预制体、数量和权重，不再为每一种敌人重复编写对象池。
public class StageEnemyManager : LevelEnemyManager
{
    [Serializable]
    public sealed class EnemyPool
    {
        public EnemyController prefab;//这一种敌人的预制体，使用具体敌人的Controller组件
        [Min(0)] public int poolSize = 10;//这一种敌人最多同时存在的数量
        [Min(0f)] public float weight = 1f;//有空闲成员时的相对出生权重，0表示不参与随机
    }

    //运行期对象池与Inspector配置分开，避免把场景实例写回预制体配置。
    private sealed class PoolData
    {
        public EnemyPool config;//保存这一池对应的出生权重
        public List<EnemyController> enemies;//池内成员统一登记，Boss召唤超过预热数量时可以扩容
        public float spawnOffset;//保持原关卡的相机半宽减去三倍敌人半宽的出生偏移
        public EnemyController spareEnemy;//本次随机选择时找到的空闲成员
    }

    [SerializeField] private EnemyPool[] enemyPools = Array.Empty<EnemyPool>();//按顺序配置任意种类的敌人
    [SerializeField] private Transform lungeLeft, lungeRight;//有场地边界接口的Boss共用的左右边界
    private readonly List<PoolData> pools = new();//只收集有效配置，空配置不会参与出生选择

    public override bool ValidateConfiguration(out string error)//供关卡启动前检查模板是否已经填好必要引用
    {
        if (!base.ValidateConfiguration(out error)) return false;
        bool hasEnemyPool = false;//至少一个非零权重、非空的池才能正常推进普通波次
        if (enemyPools != null)
        {
            foreach (EnemyPool config in enemyPools)
            {
                if (config == null || config.prefab == null || config.poolSize <= 0) continue;
                if (config.prefab.GetComponent<BoxCollider2D>() == null ||
                    config.prefab.GetComponentInChildren<SpriteRenderer>(true) == null)
                {
                    error = $"敌人预制体{config.prefab.name}缺少本体BoxCollider2D或SpriteRenderer。";
                    return false;
                }
                float weight = config.weight;
                if (weight > 0f && !float.IsNaN(weight) && !float.IsInfinity(weight)) hasEnemyPool = true;
            }
        }
        if (!hasEnemyPool)
        {
            error = "请至少配置一个有效敌人池，并将数量和出生权重设置为正数。";
            return false;
        }
        foreach (MonoBehaviour component in boss.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(component is IEnemyArenaBounds)) continue;
            if (lungeLeft == null || lungeRight == null || lungeLeft.position.x >= lungeRight.position.x)
            {
                error = "当前Boss需要有效的左右场地边界，且左边界必须位于右边界左侧。";
                return false;
            }
        }
        return true;
    }

    public override void InitEnemies(CameraManager camera, Transform target, PlayerManager players = null)
    {
        if (!ValidateConfiguration(out string error) || camera == null)
        {
            Debug.LogError($"{name}无法初始化敌人：{error ?? "未指定CameraManager。"}", this);
            return;//配置缺失时先停止，避免实例化空引用或创建一半的对象池
        }
        base.InitEnemies(camera, target, players);
    }

    protected override void InitEnemyPools()
    {
        pools.Clear();
        if (enemyPools == null) return;
        int maxPoolSize = 0;//交替创建各池成员，保留第二关原来虎、狼交错的渲染顺序
        foreach (EnemyPool config in enemyPools)
        {
            if (config == null || config.prefab == null || config.poolSize <= 0) continue;
            pools.Add(new PoolData { config = config, enemies = new List<EnemyController>(config.poolSize) });
            maxPoolSize = Mathf.Max(maxPoolSize, config.poolSize);
        }

        for (int i = 0; i < maxPoolSize; i++)
        {
            foreach (PoolData pool in pools)
            {
                if (i >= pool.config.poolSize) continue;
                EnemyController enemy = CreateEnemy(pool.config.prefab);
                //允许使用停用的预制体；先完成Awake与碰撞箱初始化，随后立即归还对象池。
                if (!enemy.gameObject.activeSelf) enemy.gameObject.SetActive(true);
                if (i == 0 && CM != null) pool.spawnOffset = CM.CameraHalf - enemy.enemyHalf * 3f;
                enemy.gameObject.SetActive(false);
                pool.enemies.Add(enemy);
            }
        }
    }

    protected override void ConfigureBoss()
    {
        if (curBoss == null || lungeLeft == null || lungeRight == null) return;
        //只有声明需要场地边界的组件才接收边界，普通Boss无需引用巨灵神的脚本。
        foreach (MonoBehaviour component in curBoss.GetComponentsInChildren<MonoBehaviour>(true))
            if (component is IEnemyArenaBounds arena)
                arena.SetArenaBounds(lungeLeft.position.x, lungeRight.position.x);
    }

    public override bool TryCreateEnemy(bool r, float createY)
    {
        if (CM == null) return false;
        double totalWeight = 0d;//使用双精度累加，避免很多池的有效权重相加后溢出
        PoolData lastAvailablePool = null;//浮点随机数落在上界时，仍然选择一个有效空闲池
        foreach (PoolData pool in pools)
        {
            pool.spareEnemy = FindSpareEnemy(pool);
            float weight = pool.config.weight;
            if (pool.spareEnemy == null || weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight)) continue;
            totalWeight += weight;
            lastAvailablePool = pool;
        }
        if (lastAvailablePool == null) return false;

        double choice = UnityEngine.Random.value * totalWeight;//只在尚有空闲成员的池之间按权重随机
        PoolData selectedPool = lastAvailablePool;
        foreach (PoolData pool in pools)
        {
            float weight = pool.config.weight;
            if (pool.spareEnemy == null || weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight)) continue;
            choice -= weight;
            if (choice < 0d)
            {
                selectedPool = pool;
                break;
            }
        }

        EnemyController enemy = selectedPool.spareEnemy;
        enemy.transform.position = new Vector2((r ? CM.EdgeR : CM.EdgeL) + (r ? 1f : -1f) * selectedPool.spawnOffset, createY);
        enemy.SetSpawnSide(r);
        enemy.gameObject.SetActive(true);
        return true;
    }

    private static EnemyController FindSpareEnemy(PoolData pool)//停用实例才可以复用，已被删除的实例不参与选择
    {
        foreach (EnemyController enemy in pool.enemies)
            if (enemy != null && !enemy.gameObject.activeSelf) return enemy;
        return null;
    }

    public override bool TryCreateSummonedEnemy(Vector2 groundPosition)//音符召唤不消耗普通波次出生数，仍共用奖励、换人目标和对象池
    {
        if (pools.Count == 0 || !BattleRun.IsRunning) return false;
        PoolData pool = pools[UnityEngine.Random.Range(0, pools.Count)];//每种本关敌人等概率，不沿用波次出生权重
        EnemyController enemy = FindSpareEnemy(pool);
        if (enemy == null)
        {
            enemy = CreateEnemy(pool.config.prefab);
            if (!enemy.gameObject.activeSelf) enemy.gameObject.SetActive(true);
            enemy.gameObject.SetActive(false);
            pool.enemies.Add(enemy);//新增实例仍留在本池，下次召唤优先复用
        }
        Transform actor = enemy.transform;
        BoxCollider2D body = enemy.GetComponent<BoxCollider2D>();
        float feet = body != null ? (body.offset.y - body.size.y * 0.5f) * actor.lossyScale.y : 0f;
        float centerX = body != null ? body.offset.x * actor.lossyScale.x : 0f;
        actor.position = new Vector3(groundPosition.x - centerX, groundPosition.y - feet + 0.01f, actor.position.z);
        enemy.SetSpawnSide(CM != null && groundPosition.x > CM.CameraCenter);
        enemy.gameObject.SetActive(true);
        return true;
    }
}

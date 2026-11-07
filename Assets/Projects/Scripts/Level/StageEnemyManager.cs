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

    //不走波次的敌人：从进关开始每隔一段时间生成一只，Boss战期间也继续，只受同时存活上限限制，也不阻挡波次推进。
    [Serializable]
    public sealed class TimedEnemy
    {
        public EnemyController prefab;
        [Min(1)] public int maxAlive = 2;//同时存活的上限，满员时等有一只死亡后再按间隔补
        [Min(0.5f)] public float interval = 15f;//两次生成之间的秒数
        [Min(0f)] public float firstDelay = 10f;//进关后多久生成第一只
        [Min(0f)] public float spawnHeight = 4f;//出生点高于玩家身体中心的高度
        [Min(0f)] public float spawnSideDistance = 4f;//出生点相对玩家左右偏移的最大值（左右交替）
    }

    private sealed class TimedData
    {
        public TimedEnemy config;
        public readonly List<EnemyController> enemies = new();
        public float nextAt = -1f;
        public bool right;
    }

    [SerializeField] private EnemyPool[] enemyPools = Array.Empty<EnemyPool>();//按顺序配置任意种类的敌人
    [Header("定时生成")]
    [SerializeField] private TimedEnemy[] timedEnemies = Array.Empty<TimedEnemy>();
    [SerializeField] private Transform lungeLeft, lungeRight;//有场地边界接口的Boss共用的左右边界
    [Header("Boss 召唤")]
    [SerializeField, Min(0)] private int maxSummonedEnemies = 2;//同时存在的召唤天兵上限，0表示禁止召唤
    [SerializeField, Range(0.1f, 1f)] private float summonedHpScale = 0.65f;//召唤天兵的血量倍率
    [SerializeField, Range(0f, 1f)] private float summonedDamageScale = 0.7f;//召唤天兵的伤害倍率
    private readonly List<EnemyController> summons = new();//当前仍在场的召唤单位，用于数量上限与数值恢复
    private readonly List<PoolData> pools = new();//只收集有效配置，空配置不会参与出生选择
    private readonly List<TimedData> timed = new();

#if UNITY_EDITOR
    public override IEnumerable<EnemyController> GetEnemyPrefabs()//直接使用关卡Inspector中的池配置，新增类型无需修改测试窗口。
    {
        if (enemyPools == null) yield break;
        foreach (EnemyPool pool in enemyPools)
            if (pool != null && pool.prefab != null) yield return pool.prefab;
        if (timedEnemies == null) yield break;
        foreach (TimedEnemy entry in timedEnemies)
            if (entry != null && entry.prefab != null) yield return entry.prefab;
    }
#endif

    public override bool ValidateConfiguration(out string error)//供关卡启动前检查模板是否已经填好必要引用
    {
        BalanceRuntime.ApplyLevel(this);
        if(lungeLeft!=null&&lungeRight==lungeLeft)lungeRight=null;//A duplicated left reference is invalid; reuse the existing right marker.
        if(lungeLeft==null||lungeRight==null)
        {
            var markers=transform.root.Find("transforms");
            if(markers!=null)foreach(Transform marker in markers)
            {
                if(lungeLeft==null&&marker.name.EndsWith("Left",StringComparison.Ordinal))lungeLeft=marker;
                if(lungeRight==null&&marker.name.EndsWith("Right",StringComparison.Ordinal))lungeRight=marker;
            }
        }
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
        if (boss == null) return true;//仅RequiresBoss=false的教程管理器能够通过前面的基础检查。
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
        InitTimedPools();
    }

    private void InitTimedPools()
    {
        timed.Clear();
        if (timedEnemies == null) return;
        foreach (TimedEnemy config in timedEnemies)
        {
            if (config == null || config.prefab == null) continue;
            var data = new TimedData { config = config };
            for (int i = 0; i < config.maxAlive; i++)
            {
                EnemyController enemy = CreateEnemy(config.prefab, false);
                if (!enemy.gameObject.activeSelf) enemy.gameObject.SetActive(true);//先完成Awake再停用
                enemy.gameObject.SetActive(false);
                data.enemies.Add(enemy);
            }
            timed.Add(data);
        }
    }

    public override void UpdateTimedSpawns()
    {
        if (!BattleRun.IsRunning) return;
        foreach (TimedData data in timed)
        {
            if (data.nextAt < 0f) data.nextAt = Time.time + data.config.firstDelay;
            if (Time.time < data.nextAt) continue;
            EnemyController enemy = null;
            foreach (EnemyController candidate in data.enemies)
                if (candidate != null && !candidate.gameObject.activeSelf) { enemy = candidate; break; }
            if (enemy == null) continue;//满员：有一只死亡后立即补
            SpawnTimed(enemy, data);
            data.right = !data.right;
            data.nextAt = Time.time + data.config.interval;
        }
    }

    private void SpawnTimed(EnemyController enemy, TimedData data)
    {
        Transform player = CurrentTarget;
        Vector2 center = player != null ? player.position : Vector2.zero;
        if (player != null && player.GetComponent<Collider2D>() is Collider2D body) center = body.bounds.center;
        float offset = UnityEngine.Random.Range(data.config.spawnSideDistance * 0.25f, data.config.spawnSideDistance) * (data.right ? 1f : -1f);
        float x = center.x + offset;
        if (CM != null && CM.CameraHalf > 1f)//留在画面内，飞行敌人不会被场地墙挡在外面
            x = Mathf.Clamp(x, CM.CameraCenter - CM.CameraHalf + 1f, CM.CameraCenter + CM.CameraHalf - 1f);
        enemy.transform.position = new Vector3(x, center.y + data.config.spawnHeight, enemy.transform.position.z);
        enemy.SetSpawnSide(data.right);
        Health health = enemy.GetComponent<Health>();
        Health prefabHealth = data.config.prefab.GetComponent<Health>();
        if (health != null && prefabHealth != null)
            health.SetBaseBlood(BalanceRuntime.BaseBlood(data.config.prefab.gameObject, prefabHealth.BeginBlood) * RebirthScaling.Current.HP);
        enemy.GetComponent<EnemyCombat>()?.SetExternalDamageScale(RebirthScaling.Current.Damage);
        enemy.gameObject.SetActive(true);
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
        Vector2 position = new((r ? CM.EdgeR : CM.EdgeL) + (r ? 1f : -1f) * selectedPool.spawnOffset, createY);
        enemy.transform.position = GetWaveSpawnPosition(enemy, r, position);
        enemy.SetSpawnSide(r);
        summons.Remove(enemy);//普通波次复用池成员时恢复预制体基础数值
        ApplyStatScales(enemy, selectedPool, false);
        PrepareWaveEnemy(enemy);
        enemy.gameObject.SetActive(true);
        return true;
    }

    protected virtual Vector2 GetWaveSpawnPosition(EnemyController enemy, bool right, Vector2 position) => position;
    protected virtual void PrepareWaveEnemy(EnemyController enemy) { }

    private static EnemyController FindSpareEnemy(PoolData pool)//停用实例才可以复用，已被删除的实例不参与选择
    {
        foreach (EnemyController enemy in pool.enemies)
            if (enemy != null && !enemy.gameObject.activeSelf) return enemy;
        return null;
    }

    public override bool TryCreateSummonedEnemy(Vector2 groundPosition)//音符召唤不消耗普通波次出生数，仍共用奖励、换人目标和对象池
    {
        if (pools.Count == 0 || !BattleRun.IsRunning) return false;
        PruneSummons();
        if (summons.Count >= Mathf.Max(0, maxSummonedEnemies)) return false;//同屏召唤数量上限，防止蓝音符无限累积
        PoolData pool = pools[UnityEngine.Random.Range(0, pools.Count)];//每种本关敌人等概率，不沿用波次出生权重
        EnemyController enemy = FindSpareEnemy(pool);
        if (enemy == null)
        {
            enemy = CreateEnemy(pool.config.prefab);
            if (!enemy.gameObject.activeSelf) enemy.gameObject.SetActive(true);
            enemy.gameObject.SetActive(false);
            pool.enemies.Add(enemy);//新增实例仍留在本池，下次召唤优先复用
        }
        ApplyStatScales(enemy, pool, true);//召唤天兵血量与伤害按倍率削弱，不修改预制体
        Transform actor = enemy.transform;
        BoxCollider2D body = enemy.GetComponent<BoxCollider2D>();
        float feet = body != null ? (body.offset.y - body.size.y * 0.5f) * actor.lossyScale.y : 0f;
        float centerX = body != null ? body.offset.x * actor.lossyScale.x : 0f;
        actor.position = new Vector3(groundPosition.x - centerX, groundPosition.y - feet + 0.01f, actor.position.z);
        enemy.SetSpawnSide(CM != null && groundPosition.x > CM.CameraCenter);
        enemy.gameObject.SetActive(true);
        summons.Add(enemy);
        return true;
    }

    private void PruneSummons()//死亡或回收的召唤单位不再占用上限
    {
        for (int i = summons.Count - 1; i >= 0; i--)
            if (summons[i] == null || !summons[i].gameObject.activeSelf) summons.RemoveAt(i);
    }

    private void ApplyStatScales(EnemyController enemy, PoolData pool, bool summoned)//预制体保存基础值，运行实例按倍率缩放
    {
        if (enemy == null || pool == null || pool.config == null) return;
        Health health = enemy.GetComponent<Health>();
        if (health != null)
        {
            float baseBlood = health.BeginBlood;
            if (pool.config.prefab != null)
            {
                Health prefabHealth = pool.config.prefab.GetComponent<Health>();
                if (prefabHealth != null) baseBlood = BalanceRuntime.BaseBlood(pool.config.prefab.gameObject, prefabHealth.BeginBlood);//资产提供基础HP，池复用仍只乘一次轮回/召唤倍率。
            }
            health.SetBaseBlood(baseBlood * (summoned ? Mathf.Clamp(summonedHpScale, 0.05f, 1f) : 1f) * RebirthScaling.Current.HP);
        }
        EnemyCombat combat = enemy.GetComponent<EnemyCombat>();
        if (combat != null) combat.SetExternalDamageScale((summoned ? Mathf.Clamp(summonedDamageScale, 0f, 1f) : 1f) * RebirthScaling.Current.Damage);
    }
}

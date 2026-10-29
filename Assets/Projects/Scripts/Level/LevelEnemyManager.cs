using System.Collections.Generic;
using UnityEngine;
using zmxy.GamePlay;

//共用实例登记、奖励绑定、渲染顺序和追击目标；敌人种类与出生选择交给具体关卡。
public abstract class LevelEnemyManager : MonoBehaviour
{
    [SerializeField] protected Transform boss, bossBornLocation;//各关卡都可以指定自己的Boss与出生点
    protected Transform curBoss;
    protected int enemyExtraOrder = 0;
    protected CameraManager CM;
    private readonly List<EnemyController> enemies = new();//登记所有小怪池成员，统计与换人不依赖具体敌人类型
    private Transform currentTarget;//创建对象池及Boss时共用的追击目标
    private PlayerManager currentPlayers;//创建奖励组件时注入当前使用角色的来源
    private bool initialized;//防止同一个管理器重复建立对象池

    public Transform CurBoss => curBoss;
    public int EnemyExtraOrder => enemyExtraOrder;

    public virtual bool ValidateConfiguration(out string error)//关卡通过统一接口检查配置，新增敌人管理器可继续扩展验证
    {
        if (boss == null || bossBornLocation == null)
        {
            error = "请为关卡配置Boss预制体和Boss出生点。";
            return false;
        }
        error = null;
        return true;
    }

    public virtual void InitEnemies(CameraManager camera, Transform target, PlayerManager players = null)
    {
        if (initialized) return;
        CM = camera;
        currentTarget = target;
        currentPlayers = players;
        InitEnemyPools();
        InitBoss();
        initialized = true;
    }

    protected abstract void InitEnemyPools();//子类决定敌人种类、池大小和创建顺序

    protected T CreateEnemy<T>(T prefab) where T : EnemyController//统一初始化新池成员，保留子类所需的具体类型
    {
        T enemy = Instantiate(prefab);
        enemies.Add(enemy);
        BindRewards(enemy.gameObject, currentPlayers);
        enemy.GetComponentInChildren<SpriteRenderer>(true).sortingOrder += enemyExtraOrder++;
        SetEnemyTarget(enemy.gameObject, currentTarget);
        return enemy;//子类取得真实碰撞箱宽度后，再将池成员停用
    }

    protected virtual void InitBoss()
    {
        curBoss = Instantiate(boss);
        BindRewards(curBoss.gameObject, currentPlayers);
        curBoss.GetComponent<EnemyRewards>().SetBoss(true);//同一套奖励组件，Boss只使用已选存档的首杀掉落
        curBoss.transform.position = bossBornLocation.transform.position;
        ConfigureBoss();
        curBoss.gameObject.SetActive(false);
        SetEnemyTarget(curBoss.gameObject, currentTarget);
    }

    protected virtual void ConfigureBoss() { }//冲刺边界等Boss专属设置由具体关卡提供

    protected virtual void BindRewards(GameObject enemy, PlayerManager players)
    {
        EnemyRewards rewards = enemy.GetComponent<EnemyRewards>();
        if (rewards == null) rewards = enemy.AddComponent<EnemyRewards>();
        rewards.SetPlayerManager(players);
    }

    public virtual int AliveNum()//只统计已登记的小怪，不把Boss计入普通波次数量
    {
        int ans = 0;
        foreach (EnemyController enemy in enemies)
            if (enemy != null && enemy.gameObject.activeSelf) ans++;
        return ans;
    }

    public abstract bool TryCreateEnemy(bool r, float createY);//选择哪种敌人以及出生方式由具体关卡决定

    public virtual bool TryCreateSummonedEnemy(Vector2 groundPosition) => false;//可召唤的关卡重写入口，坐标表示音符落地的脚底点

    public virtual void SetTarget(Transform target)
    {
        currentTarget = target;
        foreach (EnemyController enemy in enemies)
            if (enemy != null) SetEnemyTarget(enemy.gameObject, target);
        if (curBoss != null) SetEnemyTarget(curBoss.gameObject, target);
    }

    protected virtual void SetEnemyTarget(GameObject enemy, Transform target)//未来有不同追击组件的Boss可重写此步骤
    {
        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        if (movement != null) movement.SetTarget(target);
    }
}

using System.Collections.Generic;
using UnityEngine;

//当前关卡的金币入口：掉落先成为场景对象，完整吸附并回收后才真正增加存档金币。
public class GoldManager : MonoBehaviour
{
    [SerializeField] private GoldCoin coinPrefab;//可覆盖表现，留空时自动加载Resources/Gold/Coin
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.5f;//小怪的金币掉落概率
    [SerializeField, Min(0)] private int maxGold = 10;//前期小怪随机掉落0到此值，包含两端
    [SerializeField, Min(0)] private int goldGrowthPerLevel = 2;//后续每关增加的随机掉落上限
    [SerializeField, Min(1)] private int goldGrowthStartLevel = 3;//从第三关起默认逐关增加上限，第一、二关仍为10
    [SerializeField, Min(0)] private int bossGold = 40;//每个存档每关Boss首次被击杀时固定掉落的金币数量
    [SerializeField] private LayerMask groundLayers = 64;//掉落金币放在敌人脚下真正的地面上
    [SerializeField, Min(0.1f)] private float groundSearchDistance = 100f;//允许空中被击杀的敌人把金币落到下方地面
    private PlayerManager players;//只由当前操作角色的本体碰撞箱拾取
    private SaveData data;//同一轮内保留所属存档引用，切换角色不会改变金币归属
    private Transform coinRoot;//金币不挂在任何人物下面，换人不会销毁未拾取金币
    private readonly List<GoldCoin> coins = new();//空闲金币对象复用，不为每枚金币重复创建管理器
    private int level;//Boss首杀标记按真实关卡编号记录
    private bool running;//终局先关入账，再取消所有未完成拾取
    public static GoldManager Current { get; private set; }//关卡和死亡奖励统一使用当前管理器

    private void Awake() => Current = this;

    public void BeginRun(PlayerManager playerManager, int currentLevel)//关卡初始化后开始一轮，直接运行关卡仅创建内存数据
    {
        EndRun();
        Current = this;
        players = playerManager;
        level = Mathf.Max(1, currentLevel);
        data = GameSession.EnsureSession().EnsureRuntimeData();
        running = players != null;
        if (coinPrefab == null)
        {
            GameObject asset = Resources.Load<GameObject>("Gold/Coin");
            if (asset != null) coinPrefab = asset.GetComponent<GoldCoin>();
        }
    }

    public void Drop(Transform enemy, bool isBoss)//一名敌人不论掉落多少数值，都只生成一个金币对象
    {
        if (enemy == null || data == null || !running || !BattleRun.IsRunning) return;//已结束的战斗不能再消耗首杀标记；同归于尽的同步反伤仍在结束前登记
        int amount;
        if (isBoss)
        {
            if (!data.MarkBossDefeated(level)) return;//在死亡时就记录首杀，避免退出重进后反复刷首杀奖励
            amount = Mathf.Max(0, bossGold);
        }
        else
        {
            if (!CanCollect || dropChance <= 0f || (dropChance < 1f && Random.value >= dropChance)) return;
            int extraLevels = Mathf.Max(0, level - Mathf.Max(1, goldGrowthStartLevel) + 1);
            long upper = (long)Mathf.Max(0, maxGold) + (long)extraLevels * Mathf.Max(0, goldGrowthPerLevel);
            int limit = (int)System.Math.Min(int.MaxValue - 1L, upper);//为Random.Range的包含上限保留+1空间
            amount = Random.Range(0, limit + 1);
        }
        if (!CanCollect || amount <= 0 || coinPrefab == null) return;//同归于尽仍记录首杀，但终局绝不生成或入账金币
        Collider2D enemyBody = enemy.GetComponent<Collider2D>();
        Vector2 origin = enemyBody != null ? (Vector2)enemyBody.bounds.center : (Vector2)enemy.position;
        RaycastHit2D ground = Physics2D.Raycast(origin, Vector2.down, groundSearchDistance, groundLayers);
        if (ground.collider == null) return;//没有实际落脚地面时不制造悬浮或场景外的金币
        GoldCoin coin = GetCoin();
        if (coin != null) coin.Spawn(this, ground.point, amount);//金币的动画、寿命与吸附参数均由预制体自身维护
    }

    private GoldCoin GetCoin()//只在所有缓存都忙碌时才扩充对象池
    {
        foreach (GoldCoin coin in coins)
            if (coin != null && !coin.gameObject.activeSelf) return coin;
        if (coinRoot == null)
        {
            coinRoot = new GameObject("GoldCoins").transform;
            coinRoot.SetParent(transform, true);
        }
        GoldCoin created = Instantiate(coinPrefab, coinRoot);
        created.gameObject.SetActive(false);
        coins.Add(created);
        return created;
    }

    internal bool CanCollect//每次接触与最终入账都检查，死亡当帧不能赶在结算前收下尚未完成的金币
    {
        get
        {
            if (!running || !BattleRun.IsRunning || players == null || !isActiveAndEnabled) return false;
            PlayerController current = players.CurrentPlayer;
            if (current == null || !current.gameObject.activeInHierarchy) return false;
            Health health = current.GetComponent<Health>();
            return health != null && !health.Dead;
        }
    }

    internal PlayerController Collector => CanCollect ? players.CurrentPlayer : null;//吸附中换人时继续追踪新的当前角色

    internal void CompletePickup(GoldCoin coin)//先让对象真正消失，再增加金币；终局取消不能走到这里
    {
        if (coin == null || !CanCollect || data == null) { coin?.Cancel(); return; }
        int amount = coin.Amount;
        coin.Cancel();//隐藏渲染器并停用对象，完成拾取表现才算成功
        if (!CanCollect || amount <= 0) return;
        data.AddGold(amount);
        GameSession.instance?.NotifyDataChanged();//真实入账后刷新已打开的技能购买面板，不把吸附中的金币算进去
    }

    public void EndRun()//胜利、失败、退出与场景关闭都丢弃所有地面或吸附中的金币
    {
        running = false;
        foreach (GoldCoin coin in coins)
            if (coin != null) coin.Cancel();
    }

    private void OnDisable()
    {
        EndRun();
        if (Current == this) Current = null;
    }
}

//关卡共用流程，具体关卡可以重写各步骤，序列化字段沿用原关卡配置。
using UnityEngine;
using UnityEngine.SceneManagement;
using zmxy.GamePlay;

public abstract class LevelManager : MonoBehaviour
{
    [SerializeField] protected UIManager ui;
    [SerializeField] protected PlayerManager playerManager;//角色创建与切换的统一入口
    [SerializeField] protected LevelEnemyManager enemyManager;//小怪对象池和Boss实例的管理者
    [SerializeField] protected int curWave = 0;
    [SerializeField] protected CameraManager CM;
    [SerializeField] protected int[] leftEnemyMax, rightEnemyMax;
    [SerializeField] protected float enemyDelay,enemyMax;//敌人出现的间隔和最多同时出现的个数
    protected int leftEnemy = 0, rightEnemy = 0;
    protected float lastLeftTime = -1f, lastRightTime = -1f;
    [SerializeField] protected bool playerInNextWave = false;
    [SerializeField] protected Transform tran;
    protected float createY => tran.transform.position.y;
    protected PlayerController curPlayer => playerManager.CurrentPlayer;//每次读取最新角色，不缓存旧角色引用
    [SerializeField] protected float firstEnemyDelay;
    [SerializeField]protected float waveStartTime = -1f;
    protected bool createEnemy = false;
    [SerializeField] protected int maxWave;
    protected bool bossStart = false, bossBorn = false;
    protected float bossStartTime = -1f;
    [SerializeField] protected float bossBornDelay;
    [SerializeField] protected Transform exitLocation;
    protected Transform curBoss => enemyManager.CurBoss;//Boss实例由敌人管理器持有
    [SerializeField] protected ExitManager exit;
    protected ExitManager curExit;
    protected bool bossOver = false;
    protected int enemyExtraOrder => enemyManager.EnemyExtraOrder;
    protected bool initialized;//对象池建立后才处理换人通知
    private PlayerManager subscribedPlayerManager;//记录实际订阅对象，自动查找和重新启用时不会重复订阅
    public int WaveCount => maxWave;//供关卡创建工具核对波次配置

    protected virtual void Awake() => ResolveSceneReferences();

    protected virtual void OnEnable()
    {
        ResolveSceneReferences();
        BindPlayerManager();
    }

    protected virtual void OnDisable()
    {
        if (subscribedPlayerManager != null) subscribedPlayerManager.CurrentPlayerChanged -= OnPlayerChanged;
        subscribedPlayerManager = null;
    }

    protected virtual void Start()
    {
        ResolveSceneReferences();
        BindPlayerManager();
        if (!ValidateConfiguration(out string error))
        {
            StopForInvalidConfiguration(error);
            return;
        }
        InitPlayer();
        if (curPlayer == null || curPlayer.GetComponent<Health>() == null)
        {
            StopForInvalidConfiguration($"{GetType().Name}未能创建有效角色，请检查PlayerManager的角色预制体与Health组件");
            return;
        }
        int level = BattleRun.GetSceneLevel(SceneManager.GetActiveScene().name);//临时统计绑定当前实际关卡，而不是沿用上次进入的编号
        BattleRun.Begin(level, playerManager);
        UpdateWaveProgress();
        GoldManager.Current?.BeginRun(playerManager, level);
        InitEnemies();
        if (curBoss == null)
        {
            StopForInvalidConfiguration($"{GetType().Name}未能创建Boss，请检查敌人管理器的Boss预制体和出生点");
            return;
        }
        InitExit();
        initialized = true;
        OnPlayerChanged(curPlayer);
    }
    protected virtual void FixedUpdate()
    {
        if (!initialized || curPlayer == null) return;
        Health health = curPlayer.GetComponent<Health>();//换人后仍检查当前角色，不缓存已经换出的角色血量
        if (health == null)
        {
            StopForInvalidConfiguration($"{GetType().Name}的当前角色缺少Health组件");
            return;
        }
        if (health.Dead || !curPlayer.gameObject.activeSelf)
        {
            playerManager.FailCurrentPlayer();
            return;
        }
        if (bossOver)
        {
            CheckGameOver();
            return;
        }
        if (bossBorn)
        {
            CheckBossOver();
            return;
        }
        if (bossStart)
        {
            CheckBossBorn();
            return;
        }
        if (!createEnemy) 
        {
            CheckEnemyBorn();
            return;
        }
        int curNum = enemyManager.AliveNum();
        if (curNum >= enemyMax) return;//同时存在的敌人过多
        if (leftEnemy+rightEnemy >= leftEnemyMax[curWave] + rightEnemyMax[curWave])//这一波的全部敌人已经出动
        {
            if (curNum == 0) TryNextWave();
            return;
        }
        if ((lastLeftTime == -1 || Time.time - lastLeftTime > enemyDelay)
            && leftEnemy < leftEnemyMax[curWave])
            TryCreateEnemy(false);
        if ((lastRightTime == -1 || Time.time - lastRightTime > enemyDelay)
            && rightEnemy < rightEnemyMax[curWave])
            TryCreateEnemy(true);
    }
    protected virtual void TryCreateEnemy(bool r)
    {
        if (enemyManager.AliveNum() >= enemyMax) return;
        if (!enemyManager.TryCreateEnemy(r, createY)) return;
        if (r)
        {
            lastRightTime = Time.time;
            rightEnemy++;
        }
        else
        {
            lastLeftTime = Time.time;
            leftEnemy++;
        }
    }


    protected virtual void StartEnemy()
    {
        createEnemy = true;
        waveStartTime = -1;
    }
    protected virtual void TryNextWave()
    {
        if (curWave == maxWave-1)//开启boss战
        {
            bossStart = true;
            bossStartTime = Time.time;
            return;
        }
        leftEnemy = rightEnemy = 0;
        curWave++;
        UpdateWaveProgress();
        createEnemy = false;
        playerInNextWave = false;
        CM.SetEdge(-curWave);
    }
    protected virtual void CheckBossOver()
    {
        if (!curBoss.gameObject.activeSelf)
        {
            curExit.gameObject.SetActive(true);
            bossOver = true;
        }
    }
    protected virtual void CheckBossBorn()
    {
        if (Time.time - bossStartTime >= bossBornDelay)
        {
            curBoss.gameObject.SetActive(true);
            bossBorn = true;
            BattleRun.SetBoss(curBoss.GetComponent<Health>());
        }
    }
    protected virtual void CheckEnemyBorn()
    {
        if (!playerInNextWave) playerInNextWave = CM.CheckNextWave(curWave);
        else if (waveStartTime == -1f) waveStartTime = Time.time;
        else if (Time.time - waveStartTime > firstEnemyDelay) StartEnemy();
    }
    protected virtual void CheckGameOver()
    {
        if (curPlayer.PressedInteract && curExit.PlayerIn)
        {
            BattleRun.Finish(true);
            GoldManager.Current?.EndRun();//尚未完成吸附的金币不能在结算后补入存档
            SceneManager.LoadScene("VictoryScene");
        }
    }
    protected virtual void InitPlayer()
    {
        playerManager.InitPlayer(tran);
        playerInNextWave = true;
    }
    protected virtual void InitEnemies() => enemyManager.InitEnemies(CM, curPlayer.transform, playerManager);
    protected virtual void InitExit()
    {
        curExit = Instantiate(exit);
        curExit.SetPlayerManager(playerManager);//出口预制体不保存场景角色引用，只接受当前角色身体进入
        curExit.transform.position = exitLocation.position;
        curExit.GetComponentInChildren<SpriteRenderer>(true).sortingOrder += enemyExtraOrder;
        playerManager.SetSortingOrder(enemyExtraOrder);
        curExit.gameObject.SetActive(false);
    }
    //换人后同步追击目标，UI沿用现有头像切换和资源订阅，不修改场景布局。
    protected virtual void OnPlayerChanged(PlayerController player)
    {
        if (!initialized || player == null) return;
        BattleRun.SetCurrentPlayer(player);
        enemyManager.SetTarget(player.transform);
        if (ui != null)
        {
            ui.BindPlayer(player);
        }
    }

    public void ResolveSceneReferences()//共享预制体只保存内部引用，场景之间的管理器在启动时连接；已填写的引用保持原样
    {
        if (ui == null) ui = FindFirstObjectByType<UIManager>();
        if (playerManager == null) playerManager = FindFirstObjectByType<PlayerManager>();
        if (CM == null && Camera.main != null) CM = Camera.main.GetComponent<CameraManager>();
        if (enemyManager == null) enemyManager = GetComponent<LevelEnemyManager>();
        if (enemyManager == null) enemyManager = GetComponentInChildren<LevelEnemyManager>(true);
        if (enemyManager == null) enemyManager = FindFirstObjectByType<LevelEnemyManager>();
    }

    private void BindPlayerManager()//OnEnable可能早于自动查找成功，Start补绑时也只保留一份订阅
    {
        if (subscribedPlayerManager == playerManager) return;
        if (subscribedPlayerManager != null) subscribedPlayerManager.CurrentPlayerChanged -= OnPlayerChanged;
        subscribedPlayerManager = playerManager;
        if (subscribedPlayerManager != null) subscribedPlayerManager.CurrentPlayerChanged += OnPlayerChanged;
    }

    public bool ValidateConfiguration(out string error)//创建关卡和启动关卡共用检查，缺项时停止流程而不访问空引用
    {
        if (playerManager == null || enemyManager == null)
            error = $"{GetType().Name}缺少PlayerManager或LevelEnemyManager";
        else if (Camera.main == null)
            error = $"{GetType().Name}找不到主摄像机，请给关卡相机设置MainCamera标签";
        else if (CM == null)
            error = $"{GetType().Name}缺少CameraManager，请在主摄像机上挂载CameraManager";
        else if (!CM.ValidateConfiguration(out error))
            return false;
        else if (maxWave <= 0 || curWave < 0 || curWave >= maxWave)
            error = $"{GetType().Name}的maxWave必须大于0，curWave必须位于0到maxWave-1之间";
        else if (leftEnemyMax == null || rightEnemyMax == null || leftEnemyMax.Length != maxWave || rightEnemyMax.Length != maxWave)
            error = $"{GetType().Name}左右波次敌人数量数组的长度必须等于maxWave（{maxWave}）";
        else if (CM.WaveCount < maxWave)
            error = $"{GetType().Name}有{maxWave}波小怪，但相机仅配置了{CM.WaveCount}组边界";
        else if (enemyMax <= 0f)
            error = $"{GetType().Name}的enemyMax必须大于0，否则不会生成小怪";
        else if (tran == null || exitLocation == null || exit == null)
            error = $"{GetType().Name}缺少角色出生点tran、出口位置exitLocation或出口预制体exit";
        else if (exit.GetComponentInChildren<SpriteRenderer>(true) == null || exit.GetComponentInChildren<SpriteManager>(true) == null ||
            exit.GetComponent<BoxCollider2D>() == null || !exit.GetComponent<BoxCollider2D>().isTrigger)
            error = $"{GetType().Name}的出口预制体需要SpriteRenderer、SpriteManager和开启Is Trigger的本体BoxCollider2D";
        else
        {
            for (int i = 0; i < maxWave; i++)
            {
                if (leftEnemyMax[i] < 0 || rightEnemyMax[i] < 0)
                {
                    error = $"{GetType().Name}第{i + 1}波的小怪数量不能小于0";
                    return false;
                }
            }
            if (!enemyManager.ValidateConfiguration(out error)) return false;
            error = null;
            return true;
        }
        return false;
    }

    private void StopForInvalidConfiguration(string error)//配置出错时退出初始化并退订事件，避免每帧重复报错
    {
        Debug.LogError(error, this);
        initialized = false;
        enabled = false;
    }

    private void UpdateWaveProgress()//按当前波全部计划生成的小怪数统计，不用活跃对象数把死亡动画算回存活
    {
        int total = (leftEnemyMax != null && curWave >= 0 && curWave < leftEnemyMax.Length ? leftEnemyMax[curWave] : 0)
            + (rightEnemyMax != null && curWave >= 0 && curWave < rightEnemyMax.Length ? rightEnemyMax[curWave] : 0);
        BattleRun.SetWave(curWave, maxWave, total);
    }
}

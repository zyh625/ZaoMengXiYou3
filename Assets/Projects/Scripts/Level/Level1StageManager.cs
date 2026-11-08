using UnityEngine;

//第一关：向上闯关。与其他关卡按波次刷怪不同，鸟怪只按时间间隔生成（Boss战期间也继续），
//唯一限制是同时存活的数量上限 enemyMax；玩家登上顶层平台后Boss出场。
//Boss、出口与胜利结算沿用通用流程。
public sealed class Level1StageManager : StageManager
{
    [Header("向上闯关")]
    [Tooltip("玩家脚底到达该高度（顶层平台）时开始Boss战")]
    [SerializeField] private Transform bossTrigger;
    [Tooltip("登顶后镜头自动上升到 Boss 战场的速度（单位/秒）")]
    [SerializeField, Min(0.1f)] private float cameraRiseSpeed = 3f;
    private const int ProgressSteps = 10;//失败结算按攀爬高度估算进度，分10档
    private float nextSpawnTime = -1f;
    private bool spawnRight;
    private int climbStep = -1;

    protected override void FixedUpdate()
    {
        if (!initialized || curPlayer == null) return;
#if UNITY_EDITOR
        if (IsBattlePractice) return;
#endif
        Health health = curPlayer.GetComponent<Health>();
        if (health == null) return;
        if (health.Dead || !curPlayer.gameObject.activeSelf)
        {
            playerManager.FailCurrentPlayer();
            return;
        }

        SpawnOnTimer();//不论是否在Boss战都继续刷鸟
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
            if (!CM.TopLockReached) bossStartTime = Time.time;//和旧版一样：镜头升到 Boss 战场后才开始计 Boss 出场的等待
            CheckBossBorn();
            return;
        }
        UpdateClimbProgress();
        if (!Reached(bossTrigger)) return;
        CM.LockToTop(cameraRiseSpeed);//登上顶层：镜头自动升到 Boss 战场并固定
        if (HasBossPhase)
        {
            bossStart = true;
            bossStartTime = Time.time;
        }
        else
        {
            curExit.gameObject.SetActive(true);
            bossOver = true;
        }
    }

    private void SpawnOnTimer()
    {
        if (nextSpawnTime < 0f) nextSpawnTime = Time.time + firstEnemyDelay;
        if (Time.time < nextSpawnTime || enemyManager.AliveNum() >= RuntimeEnemyMax) return;//满员时等有鸟死亡后立即补
        if (!enemyManager.TryCreateEnemy(spawnRight, createY)) return;
        spawnRight = !spawnRight;//左右交替，鸟怪管理器把出生点放到玩家上方
        nextSpawnTime = Time.time + RuntimeEnemyDelay;
    }

    private void UpdateClimbProgress()//没有波次，用爬升高度代替失败时的完成度
    {
        if (bossTrigger == null || tran == null) return;
        float total = bossTrigger.position.y - tran.position.y;
        if (total <= 0f) return;
        int step = Mathf.Clamp(Mathf.FloorToInt((Feet() - tran.position.y) / total * ProgressSteps), 0, ProgressSteps - 1);
        if (step <= climbStep) return;
        climbStep = step;
        BattleRun.SetWave(step, ProgressSteps, 1);
    }

    private float Feet()
    {
        BoxCollider2D body = curPlayer.GetComponent<BoxCollider2D>();
        return body != null ? body.bounds.min.y : curPlayer.transform.position.y;
    }

    private bool Reached(Transform marker) => marker == null || Feet() >= marker.position.y;
}

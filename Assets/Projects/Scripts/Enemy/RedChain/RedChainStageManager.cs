using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RedChainStageManager : StageManager
{
    protected override void Start()
    {
        if (!(enemyManager is RedChainStageEnemyManager enemies) || enemies.HasBoss) { base.Start(); return; }
        ResolveSceneReferences();
        bossUI?.PrepareForBattle();
        if (!ValidateConfiguration(out string error)) { Debug.LogError(error, this); enabled = false; return; }
        InitPlayer();
        if (curPlayer == null) { enabled = false; return; }
        BattleRun.Begin(BattleRun.GetSceneLevel(SceneManager.GetActiveScene().name), playerManager);
        BattleRun.SetWave(curWave, maxWave, leftEnemyMax[curWave] + rightEnemyMax[curWave]);
        GoldManager.Current?.BeginRun(playerManager, 5);
        InitEnemies();
        InitExit();
        initialized = true;
        OnPlayerChanged(curPlayer);
    }

    protected override void TryNextWave()
    {
        if (curWave == maxWave - 1 && enemyManager is RedChainStageEnemyManager enemies && !enemies.HasBoss)
        {
            createEnemy = false;
            enabled = false; //只结束当前小怪演示，不冒充击败未实现的广目天王、不开胜利出口。
            return;
        }
        base.TryNextWave();
    }
}

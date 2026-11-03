using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GreenHammerStageManager : StageManager
{
    protected override void Start()
    {
        if(!(enemyManager is GreenHammerStageEnemyManager enemies) || enemies.HasBoss){base.Start();return;}
        ResolveSceneReferences();bossUI?.PrepareForBattle();
        if(!ValidateConfiguration(out string error)){Debug.LogError(error,this);enabled=false;return;}
        InitPlayer();if(curPlayer==null){enabled=false;return;}
        BattleRun.Begin(BattleRun.GetSceneLevel(SceneManager.GetActiveScene().name),playerManager);
        BattleRun.SetWave(curWave,maxWave,leftEnemyMax[curWave]+rightEnemyMax[curWave]);
        GoldManager.Current?.BeginRun(playerManager,6);InitEnemies();InitExit();initialized=true;OnPlayerChanged(curPlayer);
    }
    protected override void TryNextWave()
    {
        if(curWave==maxWave-1 && enemyManager is GreenHammerStageEnemyManager enemies && !enemies.HasBoss)
        {
            createEnemy=false;enabled=false;return; //本次仅交付小怪，不把缺失Boss当作已击败、不开胜利出口。
        }
        base.TryNextWave();
    }
}

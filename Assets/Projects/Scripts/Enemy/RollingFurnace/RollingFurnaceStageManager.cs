// Local adaptation only: missing Level7 boss is a waiting state, never an automatic victory.
public sealed class RollingFurnaceStageManager : StageManager
{
    protected override bool HasBossPhase => enemyManager is RollingFurnaceStageEnemyManager enemies && enemies.HasBoss;
    protected override void TryNextWave()
    {
        if (curWave == maxWave - 1 && !HasBossPhase) { createEnemy = false; return; }
        base.TryNextWave();
    }
}

//教程只有普通敌人池，其他关卡仍要求Boss与出生点。
public sealed class TutorialEnemyManager : StageEnemyManager
{
    protected override bool RequiresBoss => false;
}

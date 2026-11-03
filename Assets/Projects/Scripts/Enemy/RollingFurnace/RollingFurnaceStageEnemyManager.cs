// Level7 currently has no boss prefab. Still validate all pool entries using the common implementation.
public sealed class RollingFurnaceStageEnemyManager : StageEnemyManager
{
    public bool HasBoss => boss != null;
    protected override bool RequiresBoss => HasBoss;
}

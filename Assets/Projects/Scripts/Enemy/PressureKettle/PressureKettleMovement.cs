public sealed class PressureKettleMovement : EnemyMovement
{
    public void MoveInRange(float delta, bool retreat, bool slow) =>
        MoveAlong((delta >= 0f ? 1f : -1f) * (retreat ? -1f : 1f), slow);
}

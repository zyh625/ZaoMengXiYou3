using UnityEngine;

public sealed class Level1BirdStageEnemyManager : StageEnemyManager
{
    [SerializeField, Min(0f)] private float spawnHeight = 5f;
    [SerializeField, Min(0f)] private float spawnSideDistance = 3f;

    protected override Vector2 GetWaveSpawnPosition(EnemyController enemy, bool right, Vector2 position)
    {
        if (enemy.GetComponent<Level1EnemyMovement>() is not { HasTarget: true } movement)
            return position;

        Vector2 player = movement.TargetPosition;
        float left = CM.EdgeL - CM.CameraHalf + 0.5f;
        float rightEdge = CM.EdgeR + CM.CameraHalf - 0.5f;
        float offset = Random.Range(spawnSideDistance * 0.25f, spawnSideDistance);
        float x = Mathf.Clamp(player.x + offset * (right ? 1f : -1f), left, rightEdge);
        return new Vector2(x, player.y + spawnHeight);
    }

    protected override void PrepareWaveEnemy(EnemyController enemy) =>
        enemy.GetComponent<BirdEnemyCombat>()?.PrepareSpawn();
}

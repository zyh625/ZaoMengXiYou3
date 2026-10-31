using UnityEngine;

public sealed class RedChainMovement : EnemyMovement
{
    public bool FaceRight => GetComponentInChildren<SpriteRenderer>(true) is SpriteRenderer renderer && !renderer.flipX;
}

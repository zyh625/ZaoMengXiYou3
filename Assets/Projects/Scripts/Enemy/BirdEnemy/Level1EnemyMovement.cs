using UnityEngine;

public sealed class Level1EnemyMovement : EnemyMovement
{
    public bool HasTarget => Target != null;
    public Vector2 TargetPosition => Target != null ? Target.position : transform.position;
    public Vector2 TargetCenter//以玩家身体中心为准，不受角色根节点位置差异影响
    {
        get
        {
            if (Target == null) return transform.position;
            Collider2D body = Target.GetComponent<Collider2D>();
            return body != null ? (Vector2)body.bounds.center : (Vector2)Target.position;
        }
    }

    public Vector2 Velocity => body.linearVelocity;

    public void Fly(Vector2 velocity)
    {
        // Gravity remains available for knockback, suction release and death.
        body.linearVelocity = velocity - Physics2D.gravity * (body.gravityScale * Time.fixedDeltaTime);
    }
}

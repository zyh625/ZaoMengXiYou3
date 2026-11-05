using UnityEngine;

public sealed class ConstellationWeaverMovement : EnemyMovement
{
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    public void KeepRange(float delta, bool slow) => MoveAlong(Mathf.Sign(delta) * (Mathf.Abs(delta) < BalanceRuntime.Number(this, "ConstellationWeaverMovement.KeepRange.1", 3f) ? -1f : 1f), slow);
    public bool SlideTo(float x)
    {
        if (IsExternallyControlled || GetComponent<Health>().Status.BlocksMovement) { Stop(); return false; }
        float dx = x - body.position.x;
        ContactFilter2D filter = new(); filter.SetLayerMask(1 << 6); filter.useTriggers = false;
        int count = BodyCollider.Cast(Vector2.right * Mathf.Sign(dx), filter, hits, Mathf.Abs(dx));
        for (int i = 0; i < count; i++) if (Vector2.Dot(hits[i].normal, Vector2.right * Mathf.Sign(dx)) < -BalanceRuntime.Number(this, "ConstellationWeaverMovement.SlideTo.1", 0.1f)) { Stop(); return false; }
        body.MovePosition(new Vector2(x, body.position.y));
        return true;
    }
}

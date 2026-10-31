using UnityEngine;
using zmxy.GamePlay;

public sealed class RedBoomMovement : EnemyMovement
{
    [SerializeField, Min(0f)] private float blinkCooldown = 6f;
    [SerializeField, Min(0f)] private float blinkDistance = 4f;
    private readonly RaycastHit2D[] casts = new RaycastHit2D[24];
    private readonly Collider2D[] overlaps = new Collider2D[24];
    private float blinkAt;
    private int blinkLife = -1;
    private CameraManager cameraManager;
    private bool customBounds;
    private Vector2 bounds;
    public float BlinkReadyAt => blinkAt;
    public void MoveInRange(float delta, bool retreat, bool slow) => MoveAlong((delta > 0f ? 1f : -1f) * (retreat ? -1f : 1f), slow);
    public void SetBlinkBounds(float minX, float maxX) { bounds = new Vector2(minX,maxX); customBounds = true; }

    public bool TryBlink(float delta)
    {
        Health health = GetComponent<Health>();
        if (health != null && health.LifeVersion != blinkLife) { blinkLife = health.LifeVersion; blinkAt = 0f; }
        if (Time.time < blinkAt || Mathf.Abs(delta) >= 3f || health == null || health.Dead || health.Attacked
            || IsExternallyControlled || health.Status != null && health.Status.BlocksMovement || !IsOnGroundNow()) return false;
        Collider2D own = BodyCollider;
        if (own == null) return false;
        Vector2 direction = delta >= 0f ? Vector2.left : Vector2.right;
        Vector2 destination = body.position + direction * blinkDistance;
        Bounds shape = own.bounds;
        Vector2 center = (Vector2)shape.center + direction * blinkDistance;
        Vector2 limits = new(float.NegativeInfinity,float.PositiveInfinity);
        if (customBounds) limits = bounds;
        else
        {
            if (cameraManager == null && Camera.main != null) cameraManager = Camera.main.GetComponent<CameraManager>();
            if (cameraManager != null && cameraManager.CameraHalf > 0f)
                limits = new Vector2(cameraManager.EdgeL-cameraManager.CameraHalf, cameraManager.EdgeR+cameraManager.CameraHalf);
        }
        if (center.x - shape.extents.x < limits.x || center.x + shape.extents.x > limits.y) return false;
        ContactFilter2D filter = new(); filter.SetLayerMask((1 << 6) | (1 << 11)); filter.useTriggers = false;
        int count = own.Cast(direction,filter,casts,blinkDistance+0.025f);
        for (int i=0;i<count;i++)
            if (Vector2.Dot(casts[i].normal,direction)<-0.01f && casts[i].distance<blinkDistance+0.025f) return false;
        count = Physics2D.OverlapBox(center, (Vector2)shape.size-Vector2.one*0.04f, 0f, filter, overlaps);
        for (int i=0;i<count;i++) if (overlaps[i]!=own && overlaps[i].attachedRigidbody!=body) return false;
        for (int side=-1;side<=1;side+=2)
        {
            RaycastHit2D ground = Physics2D.Raycast(new Vector2(center.x+side*(shape.extents.x-0.04f),shape.min.y+0.15f),Vector2.down,0.35f,1 << 6);
            if (ground.collider == null || ground.normal.y < 0.6f || Mathf.Abs(ground.point.y-shape.min.y)>0.15f) return false;
        }
        transform.position = new Vector3(destination.x,destination.y,transform.position.z);
        body.position = destination;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        blinkAt = Time.time + blinkCooldown;
        return true;
    }
}

using UnityEngine;

//输入和玩家状态机照常运行，只在它们之后覆盖这 0.35 秒的横向速度。
[DefaultExecutionOrder(1000)]
public sealed class RedChainPull : MonoBehaviour
{
    public const float Duration = 0.35f;
    private const float Skin = 0.025f;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Health health, sourceHealth;
    private RedChainCombat source;
    private int sourceLife, playerLife;
    private float elapsed, velocity, destinationX, lastVelocity;
    private bool pulling, ending;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    public bool IsPulling => pulling && !ending;

    public void Begin(RedChainCombat owner)
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        health = GetComponent<Health>();
        if (owner == null || body == null || bodyCollider == null || health == null || !health.CanReceiveControl(ControlStrength.Strong)) return;
        Health ownerHealth = owner.GetComponent<Health>();
        if (ownerHealth == null || ownerHealth.Dead) return;
        Collider2D ownBox = bodyCollider;
        Collider2D enemyBox = owner.GetComponent<Collider2D>();
        float side = body.position.x >= owner.transform.position.x ? 1f : -1f;
        float half = ownBox != null ? ownBox.bounds.extents.x : 0.5f;
        float faceX = enemyBox != null ? enemyBox.bounds.center.x + side * (enemyBox.bounds.extents.x + half + 0.1f)
            : owner.transform.position.x + side * 1.5f;
        float centerOffset = ownBox != null ? ownBox.bounds.center.x - body.position.x : 0f;
        float nextDestination = faceX - centerOffset;
        //已经站在身前时不反向推开玩家，更不能穿过武卒。
        float distance = side * (body.position.x - nextDestination);
        if (distance <= 0f) return;
        Cancel();
        source = owner;
        sourceHealth = ownerHealth;
        sourceLife = sourceHealth.LifeVersion;
        playerLife = health.LifeVersion;
        destinationX = nextDestination;
        velocity = -side * distance / Duration;
        elapsed = 0f;
        pulling = true;
        ending = false;
    }

    private void FixedUpdate()
    {
        if (!pulling) return;
        if (ending || source == null || !source.isActiveAndEnabled || sourceHealth == null || sourceHealth.Dead
            || sourceHealth.LifeVersion != sourceLife || health == null || health.Dead || health.LifeVersion != playerLife
            || health.Status != null && health.Status.BlocksMovement)
        { Cancel(); return; }
        float dt = Mathf.Min(Time.fixedDeltaTime, Duration - elapsed);
        float step = Mathf.Min(Mathf.Abs(velocity) * dt, Mathf.Abs(destinationX - body.position.x));
        Vector2 direction = new(Mathf.Sign(velocity), 0f);
        ContactFilter2D filter = new();
        filter.SetLayerMask(1 << 6);
        filter.useTriggers = false;
        //只扫掠物理本体，玩家照常打开的攻击/技能触发箱不能把自己提前卡住。
        int count = bodyCollider.Cast(direction, filter, hits, step + Skin);
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            if (Vector2.Dot(hits[i].normal, direction) >= -0.01f) continue; //贴地移动不把脚下地板误作墙。
            float safe = Mathf.Max(0f, hits[i].distance - Skin);
            if (safe < step) { step = safe; blocked = true; }
        }
        lastVelocity = direction.x * step / Time.fixedDeltaTime;
        body.linearVelocity = new Vector2(lastVelocity, body.linearVelocityY);
        elapsed += dt;
        ending = blocked || elapsed >= Duration - 0.0001f || step <= 0.0001f;
    }

    public void Cancel()
    {
        if (pulling && body != null && Mathf.Abs(body.linearVelocityX - lastVelocity) < 0.001f)
            body.linearVelocity = new Vector2(0f, body.linearVelocityY);
        pulling = ending = false;
        source = null;
    }

    private void OnDisable() => Cancel();
}

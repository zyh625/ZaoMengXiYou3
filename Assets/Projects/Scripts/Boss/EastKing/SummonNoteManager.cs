using UnityEngine;

//蓝色音符先飞到空中随机横坐标，再垂直落地；玩家先碰到则只恢复法力，不生成敌人。
public class SummonNoteManager : EastNoteManager
{
    [SerializeField, Min(0.01f)] private float ascendSpeed = 20f;//飞向空中随机位置的速度
    [SerializeField, Min(0.01f)] private float descendSpeed = 24f;//垂直落向地面的速度
    [SerializeField, Min(0.1f)] private float apexHeight = 8f;//相对于释放点及地面的最小升空高度
    [SerializeField, Min(0f)] private float arenaMargin = 1f;//随机落点与场地边缘的最小间隔
    [SerializeField, Min(0f)] private float manaRecovery = 20f;//玩家截获后恢复固定法力，不超过角色上限
    [SerializeField] private LayerMask groundLayers = 1 << 6;//只检测地板，不在人物和攻击箱上生成小怪
    [SerializeField, Min(1f)] private float groundSearchDepth = 100f;//支持不同关卡地面高度的向下查找距离
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];//过滤触发箱的非分配地面检测
    private Vector2 apex, groundPoint;//保存本枚音符的随机位置，飞行中不随Boss和玩家移动
    private bool descending;//只有飞到空中端点后才进入垂直下落
    protected override bool IsHostile => false;

    protected override void OnLaunched(Vector2 position, bool right)
    {
        float halfWidth = Mathf.Max(0f, (arenaBounds.y - arenaBounds.x) * 0.5f);
        float margin = Mathf.Min(Mathf.Max(0f, arenaMargin), halfWidth);
        float x = Random.Range(arenaBounds.x + margin, arenaBounds.y - margin);
        float fallbackGround = position.y;
        Collider2D sourceBody = owner != null ? owner.GetComponent<Collider2D>() : null;
        if (sourceBody != null) fallbackGround = sourceBody.bounds.min.y;
        float top = Mathf.Max(position.y, fallbackGround) + Mathf.Max(0.1f, apexHeight);
        groundPoint = new Vector2(x, FindGroundY(x, top + 1f, fallbackGround));
        apex = new Vector2(x, Mathf.Max(top, groundPoint.y + apexHeight));
        descending = false;
    }

    private float FindGroundY(float x, float top, float fallback)//随机点下方查真实地板，缺少地板的测试布局沿用Boss脚底高度
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;
        int count = Physics2D.Raycast(new Vector2(x, top), Vector2.down, filter, groundHits,
            Mathf.Max(1f, groundSearchDepth));
        float highest = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
            if (groundHits[i].collider != null && groundHits[i].normal.y > 0.5f)
                highest = Mathf.Max(highest, groundHits[i].point.y);
        return float.IsNegativeInfinity(highest) ? fallback : highest;
    }

    protected override void FixedUpdate()
    {
        if (!CanTick()) return;
        Vector2 from = transform.position;
        Vector2 destination = descending ? new Vector2(groundPoint.x, groundPoint.y + HitHalfExtents.y) : apex;
        float speed = descending ? descendSpeed : ascendSpeed;
        Vector2 to = Vector2.MoveTowards(from, destination, Mathf.Max(0.01f, speed) * Time.fixedDeltaTime);
        FaceDirection(to - from);
        if (SweepPlayer(from, to, out Health player, out _))
        {
            OnPlayerContact(player);//先检查本段是否被玩家接住，再结算本段末端的落地
            return;
        }
        SetPosition(to);
        if ((to - destination).sqrMagnitude > 0.000001f) return;
        if (!descending)
        {
            descending = true;
            return;
        }
        enemyManager?.TryCreateSummonedEnemy(groundPoint);
        StopEffect();//无有效敌人池时也正常消失，不在原地每物理帧重复召唤
    }

    protected override void OnPlayerContact(Health health)
    {
        PlayerResources resources = health != null ? health.GetComponent<PlayerResources>() : null;
        if (resources != null) resources.RecoverMana(Mathf.Max(0f, manaRecovery));
        StopEffect();
    }

    protected override void OnDisable()
    {
        descending = false;
        apex = groundPoint = Vector2.zero;
        base.OnDisable();
    }
}

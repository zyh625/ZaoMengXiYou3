using UnityEngine;

//鸟怪：平时在玩家头顶区域随机盘旋；玩家位于左下或右下方且攻击冷却结束时俯冲。
//俯冲用突击动画的时长：前半段冲到玩家位置，后半段飞回出发点，速度按距离自动换算。
public sealed class BirdEnemyController : EnemyController
{
    [SerializeField] private SpriteData dash;
    [SerializeField] private SpriteRenderer crown;
    [Header("盘旋")]
    [SerializeField, Min(0f)] private float chaseSpeed = 3f;//盘旋时的最大飞行速度
    [SerializeField, Min(0.1f)] private float steering = 6f;//转向加速度，越小飞行轨迹越圆滑
    [SerializeField, Min(0f)] private float hoverHalfWidth = 3f;//盘旋区域相对玩家的左右半宽
    [SerializeField] private Vector2 hoverHeight = new(2.2f, 3.4f);//盘旋区域高于玩家身体中心的范围
    [SerializeField] private Vector2 retargetTime = new(1.2f, 2.6f);//多久换一个盘旋点
    [Header("俯冲")]
    [SerializeField, Min(0f)] private float minDrop = 0.8f;//玩家至少低于鸟这么多才会俯冲
    [SerializeField, Min(0f)] private float minSide = 0.4f;//玩家至少偏左/偏右这么多，正下方不攻击
    [SerializeField, Range(10f, 85f)] private float minAngle = 20f, maxAngle = 75f;//俯冲方向与水平线的夹角范围
    [SerializeField, Min(0.5f)] private float maxReach = 4.5f;//最远俯冲距离
    [SerializeField] private Vector2 hitWindow = new(0.3f, 0.65f);//攻击框在整段俯冲中打开的进度区间（0.5为到达玩家）
    private Level1EnemyMovement flight;
    private BirdEnemyCombat combat;
    private bool diving;
    private float diveStarted, diveDuration, nextRetarget;
    private Vector2 diveOrigin, diveTarget, hoverOffset, hoverVelocity;
    protected override bool UsesAnger => false;

    protected override void Awake()
    {
        base.Awake();
        flight = GetComponent<Level1EnemyMovement>();
        combat = GetComponent<BirdEnemyCombat>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        diving = false;
        hoverVelocity = Vector2.zero;
        PickHoverPoint();
        crown.sortingOrder = SM.GetComponent<SpriteRenderer>().sortingOrder + 1;
        crown.gameObject.SetActive(combat.IsElite);
    }

    public override void SetSpawnSide(bool rightSide) => SM.ChangeSpriteFace(!rightSide);

    protected override bool ProcessSpecialState()
    {
        if (EH.Status.BlocksActions || EH.Attacked || (state == EnemyState.Attacked && !SM.IsFinished))
        {
            CancelDive();//冰冻或受击打断俯冲，由通用流程处理击退和受击动画
            return false;
        }
        if (flight.ProcessExternalControl())
        {
            CancelDive();
            EH.AttackedEnd();
            return true;
        }
        if (diving)
        {
            UpdateDive();
            return true;
        }
        if (!flight.HasTarget)
        {
            hoverVelocity = Vector2.zero;
            flight.Fly(Vector2.zero);
            SetState(EnemyState.Idle);
            return true;
        }

        Vector2 player = flight.TargetCenter;
        SM.ChangeSpriteFace(player.x >= transform.position.x);
        EC.CheckAttack();
        if (EC.AttackReady() && !EH.Status.BlocksMovement && InDiveCone(player))
        {
            StartDive(player);
            return true;
        }
        Hover(player);
        SetState(EnemyState.Walk);
        return true;
    }

    private void Hover(Vector2 player)
    {
        if (EH.Status.BlocksMovement)
        {
            EH.Status.EnforcePosition();
            return;
        }
        Vector2 point = player + hoverOffset;
        Vector2 toPoint = point - (Vector2)transform.position;
        if (Time.time >= nextRetarget || toPoint.sqrMagnitude < 0.09f) PickHoverPoint();
        float speed = chaseSpeed * RebirthScaling.Current.Movement * (EH.Slow ? 0.5f : 1f);
        Vector2 desired = Vector2.ClampMagnitude(toPoint * 1.5f, speed);//靠近目标点时自然减速
        hoverVelocity = Vector2.MoveTowards(hoverVelocity, desired, steering * Time.fixedDeltaTime);
        flight.Fly(hoverVelocity);
    }

    private void PickHoverPoint()
    {
        hoverOffset = new Vector2(Random.Range(-hoverHalfWidth, hoverHalfWidth), Random.Range(hoverHeight.x, hoverHeight.y));
        nextRetarget = Time.time + Random.Range(retargetTime.x, retargetTime.y);
    }

    private bool InDiveCone(Vector2 player)//只攻击左下方或右下方的玩家
    {
        Vector2 delta = player - (Vector2)transform.position;
        float drop = -delta.y, side = Mathf.Abs(delta.x);
        if (drop < minDrop || side < minSide || delta.magnitude > maxReach) return false;
        float angle = Mathf.Atan2(drop, side) * Mathf.Rad2Deg;
        return angle >= minAngle && angle <= maxAngle;
    }

    private void StartDive(Vector2 player)
    {
        bool right = player.x >= transform.position.x;
        EC.HasAtatck(right);
        diving = true;
        diveOrigin = transform.position;
        diveTarget = player;//起手时锁定位置，俯冲途中不再追踪
        diveStarted = Time.time;
        diveDuration = dash != null && dash.fps > 0f && dash.sprites != null && dash.sprites.Length > 0
            ? dash.sprites.Length / dash.fps : 0.6f;
        SM.ChangeSpriteFace(right);
        SetState(EnemyState.Attacking);//播放突击动画
        UpdateDive();
    }

    private void UpdateDive()
    {
        float progress = Mathf.Clamp01((Time.time - diveStarted) / diveDuration);
        if (progress >= hitWindow.x && progress <= hitWindow.y) EC.AttackBegin();
        else EC.AttackEnd();

        //sin(π·t)：0→1→0，前半段冲向玩家、后半段原路返回，折返点速度为零，过渡平滑。
        float next = Mathf.Clamp01(progress + Time.fixedDeltaTime / diveDuration);
        Vector2 position = diveOrigin + (diveTarget - diveOrigin) * Mathf.Sin(Mathf.PI * next);
        flight.Fly((position - (Vector2)transform.position) / Time.fixedDeltaTime);

        if (progress < 1f) return;
        diving = false;
        AttackOver(true);
        hoverVelocity = Vector2.zero;
        flight.Fly(Vector2.zero);
        PickHoverPoint();
        SetState(EnemyState.Walk);
    }

    private void CancelDive()
    {
        if (!diving) return;
        diving = false;
        hoverVelocity = flight.Velocity;//受击后从当前速度平滑接回盘旋
    }

    protected override void HandleAttack(int attack) => SM.ChangeState(dash);
}

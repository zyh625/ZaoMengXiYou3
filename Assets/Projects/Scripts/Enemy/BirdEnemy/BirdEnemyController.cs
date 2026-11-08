using UnityEngine;

//鸟怪，行为与重构前的 Bird.cs 一致，只是接入了通用敌人框架（受击、冰冻、对象池、轮回倍率与后面关卡相同）：
//· 飞行：每个物理步大概率朝玩家身体中心飞、小概率随机方向，速度慢，所以会在玩家身边抖动着贴近。
//· 攻击：身体碰到玩家且冷却结束时，锁定方向快速突刺；突刺动画前半段冲出、后半段退回，结束时回到起点。
public sealed class BirdEnemyController : EnemyController
{
    [SerializeField] private SpriteData dash;
    [SerializeField] private SpriteRenderer crown;
    [Header("飞行")]
    [SerializeField, Min(0f)] private float flySpeed = 1.5f;
    [SerializeField, Range(0f, 1f)] private float chaseChance = 0.6f;//每步朝玩家飞的概率，其余随机方向
    [Header("突刺")]
    [SerializeField, Min(0f)] private float lungeSpeed = 16f;
    [SerializeField, Min(1f)] private float lungeFps = 60f;//原版每帧换一张突刺图，约60帧每秒
    [SerializeField] private Vector2Int hitFrames = new(4, 13);//攻击框打开的帧区间（含首不含尾）
    private Level1EnemyMovement flight;
    private Collider2D body;
    private bool lunging;
    private float lungeStarted, lungeDuration;
    private Vector2 lungeOrigin, lungeDirection;
    protected override bool UsesAnger => false;
    private BirdEnemyCombat combat;

    protected override void Awake()
    {
        base.Awake();
        flight = GetComponent<Level1EnemyMovement>();
        combat = GetComponent<BirdEnemyCombat>();
        body = GetComponent<Collider2D>();
        if (dash != null) dash.fps = lungeFps;//动画与位移同步，整段突刺约0.27秒
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        lunging = false;
        crown.sortingOrder = SM.GetComponent<SpriteRenderer>().sortingOrder + 1;
        crown.gameObject.SetActive(combat.IsElite);
    }

    public override void SetSpawnSide(bool rightSide) => SM.ChangeSpriteFace(!rightSide);

    protected override bool ProcessSpecialState()
    {
        if (EH.Status.BlocksActions || EH.Attacked || (state == EnemyState.Attacked && !SM.IsFinished))
        {
            CancelLunge();//原版受击时回到突刺起点，再由通用流程处理击退和受击动画
            return false;
        }
        if (flight.ProcessExternalControl())
        {
            CancelLunge();
            EH.AttackedEnd();
            return true;
        }
        if (lunging)
        {
            UpdateLunge();
            return true;
        }
        if (!flight.HasTarget)
        {
            flight.Fly(Vector2.zero);
            SetState(EnemyState.Idle);
            return true;
        }

        Vector2 player = flight.TargetCenter;
        SM.ChangeSpriteFace(player.x >= transform.position.x);
        EC.CheckAttack();
        if (EC.AttackReady() && !EH.Status.BlocksMovement && TouchingTarget())
        {
            StartLunge(player);
            return true;
        }
        Wander(player);
        SetState(EnemyState.Walk);
        return true;
    }

    private void Wander(Vector2 player)
    {
        if (EH.Status.BlocksMovement)
        {
            EH.Status.EnforcePosition();
            return;
        }
        Vector2 dir = Random.value < chaseChance ? player - (Vector2)transform.position : Random.insideUnitCircle;
        float speed = flySpeed * RebirthScaling.Current.Movement * (EH.Slow ? 0.5f : 1f);
        flight.Fly(dir.sqrMagnitude > 0.0001f ? dir.normalized * speed : Vector2.zero);
    }

    private bool TouchingTarget()//身体与玩家碰撞箱重叠
    {
        Collider2D target = flight.TargetBody;
        if (body == null || target == null) return false;
        ColliderDistance2D d = body.Distance(target);
        return d.isValid && d.isOverlapped;
    }

    private void StartLunge(Vector2 player)
    {
        bool right = player.x >= transform.position.x;
        EC.HasAtatck(right);
        lunging = true;
        lungeOrigin = transform.position;
        lungeDirection = (player - lungeOrigin).sqrMagnitude > 0.0001f ? (player - lungeOrigin).normalized : (right ? Vector2.right : Vector2.left);
        lungeStarted = Time.time;
        int frames = dash != null && dash.sprites != null && dash.sprites.Length > 0 ? dash.sprites.Length : 16;
        lungeDuration = frames / lungeFps;
        SM.ChangeSpriteFace(right);
        SetState(EnemyState.Attacking);//播放突刺动画
        UpdateLunge();
    }

    private void UpdateLunge()
    {
        int frames = Mathf.Max(2, Mathf.RoundToInt(lungeDuration * lungeFps));
        float elapsed = Time.time - lungeStarted;
        int frame = Mathf.FloorToInt(elapsed * lungeFps);
        if (frame >= hitFrames.x && frame < hitFrames.y) EC.AttackBegin();
        else EC.AttackEnd();
        if (elapsed >= lungeDuration)
        {
            FinishLunge();
            return;
        }
        bool outward = frame < frames / 2;//前一半帧冲向玩家，后一半帧退回
        flight.Fly((outward ? 1f : -1f) * lungeSpeed * lungeDirection);
    }

    private void FinishLunge()
    {
        lunging = false;
        EC.AttackEnd();
        flight.Fly(Vector2.zero);
        flight.Teleport(lungeOrigin);//原版结束时准确回到起点，不留下帧间误差
        AttackOver(true);
        SetState(EnemyState.Walk);
    }

    private void CancelLunge()
    {
        if (!lunging) return;
        lunging = false;
        EC.AttackEnd();
        flight.Teleport(lungeOrigin);
    }

    protected override void HandleAttack(int attack) => SM.ChangeState(dash);
}

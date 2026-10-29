using UnityEngine;

//白色音符也是其余音符共用的飞行、扫掠碰撞与对象池回收入口。
public class EastNoteManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//复用已有帧动画播放器，单张音符不必配置动画
    [SerializeField] private SpriteData flight;//音符自己的循环动画，不引用Boss本体动作
    [SerializeField, Min(0.01f)] protected float flySpeed = 18f;//白色高速音符的世界坐标飞行速度
    [SerializeField, Min(0.01f)] protected float lifetime = 6f;//漏接与追踪音符均有最长存在时间
    [SerializeField, Min(0f)] private float damage = 10f;//基础伤害在发射时乘上Boss当时的攻击倍率
    [SerializeField] private bool knockback = true;//伤害与击退分别设置，蓝绿音符不走伤害入口
    [SerializeField] private bool sourceFacesRight = true;//原图朝向，翻转只改变音符本身
    [SerializeField, Range(0.01f, 1f)] private float hitWidthRatio = 0.65f;//辉光不全部计入接触范围
    [SerializeField, Range(0.01f, 1f)] private float hitHeightRatio = 0.65f;
    [SerializeField, Min(0.01f)] private float minimumHitSize = 0.12f;//无图片或极小图片时仍保留稳定的手动检测范围
    protected EnemyCombat owner;//伤害与增幅的真实来源，不以池对象作为攻击者
    protected PlayerManager players;//每一步重新读取上场角色，换人后不会追向旧角色
    protected LevelEnemyManager enemyManager;//召唤由关卡登记、追踪及奖励，不直接Instantiate小怪
    protected SpriteRenderer sr;//保留预制体既有缩放、颜色与材质
    protected Vector2 arenaBounds;//本次发射保存的左右可见场地边界
    protected Vector2 direction;//白色音符只在发射时决定此向量
    protected float expiresAt;//以游戏时间计时，暂停游戏时不会继续消耗寿命
    protected bool running;//停用中的池成员不执行任何飞行和命中
    private Health sourceHealth;//Boss死亡后停止残留音符，避免胜利动画期间继续召怪或伤人
    private int sourceLifeVersion;//池重生后的Boss不接管上一条生命的音符
    private float flightDamage;//一枚音符不随Boss之后获得或失去增幅反复改伤害
    public bool IsFlying => running;//供关卡检查与运行验证读取
    protected Transform DamageSource => owner != null ? owner.transform : null;
    protected virtual bool IsHostile => true;//治疗和召唤音符允许无敌中的玩家正常接取
    protected virtual AttackEffect[] HitEffects => null;//红色音符通过完整伤害结算附加普通眩晕

    private void ResolveVisual()//停用模板创建时Awake可能尚未执行，因此Launch也能安全补齐引用
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (SM == null) SM = GetComponent<SpriteManager>();
    }

    public void Launch(EnemyCombat source, PlayerManager playerManager, LevelEnemyManager enemies,
        Vector2 position, bool right, Vector2 bounds)//由音符池注入当前场景的运行引用
    {
        ResolveVisual();
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        owner = source;
        sourceHealth = source != null ? source.GetComponent<Health>() : null;
        sourceLifeVersion = sourceHealth != null ? sourceHealth.LifeVersion : -1;
        players = playerManager;
        enemyManager = enemies;
        arenaBounds = new Vector2(Mathf.Min(bounds.x, bounds.y), Mathf.Max(bounds.x, bounds.y));
        expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
        flightDamage = Mathf.Max(0f, damage) * (source != null ? source.DamageMultiplier : 1f);
        direction = right ? Vector2.right : Vector2.left;
        if (TryGetPlayer(out _, out Collider2D body))
        {
            Vector2 aim = (Vector2)body.bounds.center - position;
            if (aim.sqrMagnitude > 0.000001f) direction = aim.normalized;
        }
        if (sr != null)
        {
            sr.enabled = true;
            FaceDirection(direction);
            SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (sourceRenderer != null)
            {
                sr.sortingLayerID = sourceRenderer.sortingLayerID;
                sr.sortingOrder = sourceRenderer.sortingOrder + 1;
            }
        }
        if (SM != null)
        {
            if (flight != null && flight.sprites != null && flight.sprites.Length > 0) SM.ChangeState(flight);
            else SM.Pause();
        }
        running = true;
        OnLaunched(position, right);
        if (!BattleRun.IsRunning || owner == null || sourceHealth == null || sourceHealth.Dead) StopEffect();
    }

    protected virtual void OnLaunched(Vector2 position, bool right) { }//各颜色只扩展轨迹与效果，公共状态只初始化一次

    protected virtual void FixedUpdate()
    {
        if (!CanTick()) return;
        Vector2 from = transform.position;
        Vector2 to = from + direction * (Mathf.Max(0.01f, flySpeed) * Time.fixedDeltaTime);
        if (SweepPlayer(from, to, out Health player, out _))
        {
            OnPlayerContact(player);
            return;
        }
        SetPosition(to);
        if (to.x < arenaBounds.x - 3f || to.x > arenaBounds.y + 3f) StopEffect();
    }

    protected bool CanTick()//所有派生音符共同遵守终局、寿命及施法者生命边界
    {
        if (!running) return false;
        if (!BattleRun.IsRunning || Time.time >= expiresAt || owner == null || !owner.isActiveAndEnabled
            || sourceHealth == null || sourceHealth.Dead || sourceHealth.LifeVersion != sourceLifeVersion)
        {
            StopEffect();
            return false;
        }
        return true;
    }

    protected bool TryGetPlayer(out Health health, out Collider2D body)//只认实际上场角色的本体，不命中备用角色或攻击箱
    {
        health = null;
        body = null;
        PlayerController player = players != null ? players.CurrentPlayer : null;
        if (player == null || !player.isActiveAndEnabled) return false;
        health = player.GetComponent<Health>();
        body = player.GetComponent<Collider2D>();
        return health != null && !health.Dead && body != null && body.enabled;
    }

    protected bool SweepPlayer(Vector2 from, Vector2 to, out Health health, out float fraction)//高速白音符和蓝音符也扫描整段位移，避免穿过玩家
    {
        fraction = 0f;
        if (!TryGetPlayer(out health, out Collider2D body)) return false;
        if (IsHostile && (!health.GetComponent<PlayerController>().CanReceiveHit || health.IsInvincible)) return false;
        return SweepBounds(from, to, body.bounds, out fraction);
    }

    protected bool SweepBounds(Vector2 from, Vector2 to, Bounds target, out float fraction)//同一段轨迹比较玩家与治疗目标谁先接触
    {
        fraction = 0f;
        Vector2 size = HitHalfExtents * 2f;
        target.Expand(new Vector3(size.x, size.y, 1f));
        Vector2 offset = sr != null ? (Vector2)sr.bounds.center - (Vector2)transform.position : Vector2.zero;
        Vector3 start = new(from.x + offset.x, from.y + offset.y, target.center.z);
        if (target.Contains(start)) return true;
        Vector2 delta = to - from;
        if (delta.sqrMagnitude <= 0.000001f) return false;
        if (!target.IntersectRay(new Ray(start, delta.normalized), out float distance) || distance > delta.magnitude) return false;
        fraction = Mathf.Clamp01(distance / delta.magnitude);
        return true;
    }

    protected Vector2 HitHalfExtents//按当前音符显示尺寸计算，保留用户给预制体设定的大小
    {
        get
        {
            Vector2 visual = sr != null ? (Vector2)sr.bounds.size : Vector2.zero;
            return new Vector2(Mathf.Max(minimumHitSize, visual.x * hitWidthRatio) * 0.5f,
                Mathf.Max(minimumHitSize, visual.y * hitHeightRatio) * 0.5f);
        }
    }

    protected virtual void OnPlayerContact(Health health)//普通音符命中后消失，不像天兵的箭一样持续穿透
    {
        health.TakeDamage(new DamageInfo(DamageSource, flightDamage, knockback, false, HitEffects));
        StopEffect();
    }

    protected void FaceDirection(Vector2 value)//追踪只更新水平翻转，音符本身不因俯冲倒着显示
    {
        if (sr != null && Mathf.Abs(value.x) > 0.0001f) sr.flipX = (value.x > 0f) != sourceFacesRight;
    }

    protected void SetPosition(Vector2 position)//统一保留预制体原本Z轴深度
        => transform.position = new Vector3(position.x, position.y, transform.position.z);

    public void StopEffect()//停用自动归池，命中后附加给人物的眩晕和增幅不随音符一起清除
    {
        running = false;
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    protected virtual void OnDisable()//外部关卡回收也执行同样的引用清理，池复用不携带旧目标
    {
        running = false;
        owner = null;
        sourceHealth = null;
        players = null;
        enemyManager = null;
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
    }
}

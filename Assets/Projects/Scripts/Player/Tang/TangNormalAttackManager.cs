using UnityEngine;

//唐僧普通攻击的独立能量波，由PlayerManager池化管理，发出后不依赖当前操作角色。
public class TangNormalAttackManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//只控制能量波自己的播放器
    [SerializeField] private SpriteData flight;//前八帧循环飞行
    [SerializeField] private SpriteData explosion;//后八帧命中后播放一遍
    [SerializeField] private Rect[] flightBounds;//各飞行帧的有效图片范围，使用相对pivot的本地单位
    [SerializeField, Min(0f)] private float damage = 10f;//每枚能量波只对第一个有效敌人造成一次普通攻击伤害
    [SerializeField, Min(0.01f)] private float flySpeed = 10f;//水平飞行速度
    [SerializeField, Min(0f)] private float maxDistance = 12f;//从发射位置开始计算的最大飞行距离

    private enum Phase { Idle, Flying, Exploding }
    private Phase phase;//爆炸阶段不再移动或重复判定伤害
    private SpriteRenderer sr;//翻转和显示范围共用预制体已有的渲染器
    private Transform caster;//伤害、反伤与经验始终归属发出本枚能量波的唐僧
    private PlayerCombat combat;//沿用普通攻击的攻击倍率和命中积累无双入口
    private Vector2 origin;//飞行全程固定使用发射时的世界高度
    private bool faceRight;//发射后不再跟随人物转向
    private float travelled;//只记录本枚能量波已经飞过的距离
    private readonly SpriteData flightPlayback = new() { loop = true };//复用播放配置，不因对象池重用持续分配动画对象
    private readonly SpriteData explosionPlayback = new() { loop = false };//爆炸使用独立的单次播放配置

    private void OnEnable()
    {
        ResolveVisual();
        phase = Phase.Idle;
        HideVisual();
    }

    public void Launch(Transform source, Vector2 position, bool right)//由统一管理器放置，保留预制体已设置的缩放
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ResolveVisual();
        caster = source;
        combat = caster != null ? caster.GetComponent<PlayerCombat>() : null;
        if (caster == null || SM == null || sr == null || !BattleRun.IsRunning || maxDistance <= 0f
            || !PreparePlayback(flight, flightPlayback) || !PreparePlayback(explosion, explosionPlayback))
        {
            StopEffect();
            return;
        }

        origin = position;
        faceRight = right;
        travelled = 0f;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        sr.flipX = !right;
        sr.enabled = true;
        phase = Phase.Flying;
        SM.ChangeState(flightPlayback);
    }

    private void FixedUpdate()
    {
        if (phase == Phase.Idle) return;
        if (!BattleRun.IsRunning || caster == null)
        {
            StopEffect();
            return;
        }
        if (phase != Phase.Flying) return;

        Vector2 from = new(transform.position.x, origin.y);
        float step = Mathf.Min(Mathf.Max(0.01f, flySpeed) * Time.fixedDeltaTime,
            Mathf.Max(0f, maxDistance - travelled));//最后一个物理步也不越过最大距离
        Vector2 direction = faceRight ? Vector2.right : Vector2.left;
        Vector2 to = from + direction * step;
        EnemyMovement target = FindFirstHit(from, to, out float hitDistance);//先选最近目标，再结算伤害，避免死亡修改活动集合
        if (target != null)
        {
            Vector2 impact = from + direction * hitDistance;
            transform.position = new Vector3(impact.x, origin.y, transform.position.z);
            Hit(target);
            return;
        }

        transform.position = new Vector3(to.x, origin.y, transform.position.z);
        travelled += step;
        if (travelled >= maxDistance) StopEffect();
    }

    private EnemyMovement FindFirstHit(Vector2 from, Vector2 to, out float hitDistance)//矩形沿整段路径扫掠，高速飞行也不会跳过薄碰撞箱
    {
        hitDistance = float.PositiveInfinity;
        EnemyMovement target = null;
        Bounds area = sr.bounds;
        if (TangSkillEffectUtility.TryBounds(transform, sr, flightBounds, SM.CurFrame, out Bounds frameArea))
            area = frameArea;
        Vector2 offset = (Vector2)area.center - (Vector2)transform.position;//非居中pivot和翻转后的图片范围照常参与检测
        Vector2 start = from + offset;
        Vector2 delta = to - from;
        float length = delta.magnitude;
        Vector3 direction = length > 0f ? new Vector3(delta.x / length, delta.y / length, 0f) : Vector3.zero;

        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies)
        {
            if (enemy == null || !enemy.CanReceiveHit || enemy.BodyCollider == null
                || !enemy.BodyCollider.enabled || !enemy.BodyCollider.gameObject.activeInHierarchy) continue;
            if (enemy.GetComponent<Health>() is Health health && health.IgnoresAttack(DamageKind.NormalAttack)) continue;
            Bounds bounds = enemy.BodyCollider.bounds;
            bounds.Expand(new Vector3(area.size.x, area.size.y, 1f));//扩大目标等价于用能量波完整矩形扫过目标
            Vector3 rayStart = new(start.x, start.y, bounds.center.z);
            float distance;
            if (bounds.Contains(rayStart)) distance = 0f;
            else if (length <= 0f || !bounds.IntersectRay(new Ray(rayStart, direction), out distance) || distance > length) continue;
            if (distance >= hitDistance) continue;
            hitDistance = Mathf.Max(0f, distance);
            target = enemy;
        }
        return target;
    }

    private void Hit(EnemyMovement enemy)//先离开飞行状态，死亡、反伤等同步事件不会使本枚攻击再次命中
    {
        phase = Phase.Exploding;
        Health health = enemy.GetComponent<Health>();
        if (health == null)
        {
            StopEffect();
            return;
        }
        float previousBlood = health.CurBlood;
        health.TakeDamage(combat != null ? combat.CreateAttackDamage(damage, true, false, source: caster, kind: DamageKind.NormalAttack)
            : new DamageInfo(caster, damage, true, false, kind: DamageKind.NormalAttack));//正常水平击退，攻击倍率和暴击只在实际命中时计算一次
        if (!BattleRun.IsRunning)
        {
            StopEffect();//反伤导致当前角色死亡时，不在已经结束的战斗中继续播放或命中
            return;
        }
        if (combat != null && health.CurBlood < previousBlood) combat.UpdateBoost();
        SM.ChangeState(explosionPlayback);
    }

    private void LateUpdate()//在播放器的Update之后判断最后一帧，爆炸只完整播放一遍
    {
        if (phase == Phase.Idle) return;
        if (!BattleRun.IsRunning || caster == null || phase == Phase.Exploding && SM.IsFinished)
            StopEffect();
    }

    private static bool PreparePlayback(SpriteData source, SpriteData playback)//循环规则由当前阶段固定，Inspector误改不会让爆炸永不结束
    {
        if (source == null || source.sprites == null || source.sprites.Length == 0) return false;
        playback.sprites = source.sprites;
        playback.fps = Mathf.Max(1f, source.fps);
        return true;
    }

    private void ResolveVisual()//预制体沿用原SpriteManager和SpriteRenderer，不创建替代渲染器
    {
        if (SM == null) SM = GetComponent<SpriteManager>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
    }

    private void HideVisual()
    {
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
    }

    public void StopEffect()//停用后归还PlayerManager对象池，换人本身不会调用此方法
    {
        phase = Phase.Idle;
        caster = null;
        combat = null;
        HideVisual();
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        phase = Phase.Idle;
        caster = null;
        combat = null;
        HideVisual();
    }
}

using System.Collections.Generic;
using UnityEngine;

//敌方直线穿透箭：每枚箭对同一个角色只结算一次，命中后仍飞到场地边界才归还对象池。
public class NormalArrowManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//可选帧动画播放器，静态飞行图片无需另建动画
    [SerializeField] private SpriteData flight;//留空时保持预制体当前精灵
    [SerializeField, Min(0f)] private float damage = 10f;//每支箭基础伤害，发射时保存攻击者当时的增益倍率
    [SerializeField, Min(0.01f)] private float flySpeed = 10f;//水平速度，不随远程天兵之后转向改变
    [SerializeField] private bool sourceFacesRight;//现有箭图朝左，因此默认关闭；统一朝右图片时可开启
    [SerializeField] private bool knockback = true;//穿透与是否击退独立配置，不靠伤害数值表达方向效果
    [SerializeField, Range(0.01f, 1f)] private float hitWidthRatio = 0.25f;//只使用箭头附近的范围，尾部破风特效不会提前击中
    [SerializeField, Range(0.01f, 1f)] private float hitHeightRatio = 0.3f;
    private SpriteRenderer sr;//保留预制体已设定的尺寸和材质
    private EnemyCombat owner;//命中积累怒气与伤害归属，射手死亡不提前删除已经射出的箭
    private PlayerManager players;//每一步读取当前上场角色，换人不遗留旧目标
    private Vector2 flightBounds;//当前场地的左右外边界
    private float flightY, flightDamage;//发射后锁定高度及伤害，不追踪玩家
    private bool flying, faceRight;
    private readonly HashSet<Health> hitPlayers = new();//穿透期间同一个角色只命中一次，回收再发射时清空
    public bool IsFlying => flying;
    protected Transform DamageSource => owner != null ? owner.transform : null;

    private void ResolveVisual()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (SM == null) SM = GetComponent<SpriteManager>();
    }
    public void Launch(EnemyCombat source, PlayerManager playerManager, Vector2 position, bool right, Vector2 bounds)
    {
        ResolveVisual();
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        owner = source;
        players = playerManager;
        faceRight = right;
        flightY = position.y;
        flightBounds = new Vector2(Mathf.Min(bounds.x, bounds.y), Mathf.Max(bounds.x, bounds.y));
        flightDamage = Mathf.Max(0f, damage) * (source != null ? source.DamageMultiplier : 1f);
        hitPlayers.Clear();
        if (sr == null || !BattleRun.IsRunning)
        {
            StopEffect();
            return;
        }
        sr.flipX = right != sourceFacesRight;
        sr.enabled = true;
        SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (sourceRenderer != null)
        {
            sr.sortingLayerID = sourceRenderer.sortingLayerID;
            sr.sortingOrder = sourceRenderer.sortingOrder + 1;
        }
        if (SM != null)
        {
            if (flight != null && flight.sprites != null && flight.sprites.Length > 0) SM.ChangeState(flight);
            else SM.Pause();
        }
        flying = true;
    }
    private void FixedUpdate()
    {
        if (!flying) return;
        if (!BattleRun.IsRunning)
        {
            StopEffect();
            return;
        }
        Vector2 from = new(transform.position.x, flightY);
        Vector2 to = from + (faceRight ? Vector2.right : Vector2.left) * (Mathf.Max(0.01f, flySpeed) * Time.fixedDeltaTime);
        CheckPlayerHit(from, to);//扫过完整位移，快速箭也不会跳过较薄的玩家碰撞箱
        if (!gameObject.activeSelf) return;
        transform.position = new Vector3(to.x, flightY, transform.position.z);
        Bounds visual = sr.bounds;
        if (visual.max.x < flightBounds.x || visual.min.x > flightBounds.y) StopEffect();
    }
    private void CheckPlayerHit(Vector2 from, Vector2 to)
    {
        PlayerController player = players != null ? players.CurrentPlayer : null;
        if (player == null || !player.isActiveAndEnabled) return;
        Health health = player.GetComponent<Health>();
        Collider2D body = player.GetComponent<Collider2D>();
        if (health == null || health.Dead || hitPlayers.Contains(health) || body == null || !body.enabled) return;
        Bounds image = sr.bounds;
        Vector2 size = new(Mathf.Max(0.01f, image.size.x * hitWidthRatio), Mathf.Max(0.01f, image.size.y * hitHeightRatio));
        Vector2 headOffset = (Vector2)image.center - (Vector2)transform.position
            + (faceRight ? Vector2.right : Vector2.left) * (image.size.x * (1f - hitWidthRatio) * 0.5f);
        Bounds target = body.bounds;
        target.Expand(new Vector3(size.x, size.y, 1f));
        Vector2 delta = to - from;
        Vector3 start = new(from.x + headOffset.x, from.y + headOffset.y, target.center.z);
        Vector3 direction = delta.sqrMagnitude > 0f ? new Vector3(delta.x, delta.y, 0f).normalized : Vector3.zero;
        if (!target.Contains(start) && (!target.IntersectRay(new Ray(start, direction), out float distance) || distance > delta.magnitude)) return;
        hitPlayers.Add(health);//无敌挡住本次接触后也不会在同一枚箭穿过期间逐帧尝试
        if (health.IsInvincible) return;
        OnHit(health);
    }
    protected virtual void OnHit(Health health)//毒箭只扩展这一次命中，飞行与回收不重复实现
    {
        float previousBlood = health.CurBlood;
        health.TakeDamage(new DamageInfo(DamageSource, flightDamage, knockback));
        Health sourceHealth = owner != null ? owner.GetComponent<Health>() : null;
        if (health.CurBlood < previousBlood && owner != null && owner.isActiveAndEnabled
            && sourceHealth != null && !sourceHealth.Dead) owner.UpdateAngry();
    }
    public void StopEffect()//SetActive(false)后自动成为共享对象池的空闲实例
    {
        flying = false;
        if (sr != null) sr.enabled = false;
        if (SM != null) SM.Pause();
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
    protected virtual void OnDisable()
    {
        flying = false;
        hitPlayers.Clear();
        owner = null;
        players = null;
        if (sr != null) sr.enabled = false;
        if (SM != null) SM.Pause();
    }
}

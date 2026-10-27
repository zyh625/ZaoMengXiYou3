using System.Collections.Generic;
using UnityEngine;

public class SkullManager : MonoBehaviour//沙僧的麻痹药剂预制体
{
    [SerializeField] private SpriteManager SM;
    [SerializeField] private SpriteData animation;//骷髅头的循环帧动画
    private int usedTime = 0;//骷髅头弹到敌人的次数
    private float lastTime = -1f;//上次使用的时间
    [SerializeField, Min(0)] private int maxUsedTime = 0;//0表示只受持续时间限制，大于0才限制总弹跳次数
    [SerializeField] private float maxExistTime = 5f;
    [SerializeField, Min(0f)] private float searchRange = 6f;//第一目标只在释放方向的这个范围内寻找
    [SerializeField, Min(0f)] private float bounceRange = 5f;//每次弹跳重新搜索的半径
    [SerializeField, Min(0.01f)] private float flySpeed = 10f;//追踪目标的飞行速度
    [SerializeField, Min(0f)] private float hitRadius = 0.2f;//骷髅头触碰检测的额外半径
    [SerializeField, Min(0f)] private float stunDuration = 2f;//每次弹跳的眩晕时间
    [SerializeField, Min(0f)] private float minimumBounceTime = 0.12f;//两个碰撞箱重叠时仍保留可见弹跳时间，避免数个物理步直接耗尽次数
    private float nextHitAt;//限制命中节奏，不把已命中的其他敌人排除出后续索敌
    [SerializeField, Min(0f)] private float poisonDuration = 5f;//中毒的统一保留时间，命中刷新
    [SerializeField, Min(0f)] private float poisonDamagePerStack = 1f;//每层毒每秒造成的伤害
    private readonly Dictionary<EnemyMovement, int> hitCounts = new();//本枚骷髅独立统计各敌人被弹次数
    private readonly Dictionary<EnemyMovement, int> hitLifeVersions = new();//同一个池对象的新生命重新从零计算弹跳次数
    private readonly List<EnemyMovement> candidates = new();//同次数同距离的候选随机选择
    private EnemyMovement target, lastTarget;//上一目标不会被立即重复选中，单敌只生效一次
    private int targetLifeVersion = -1, lastTargetLifeVersion = -1;//追踪和排除都针对当时那条生命，而不是池对象引用
    private Transform caster;//伤害归属保留施法者，换人不会取消毒素
    private SpriteRenderer sr;//显示方向只翻转骷髅头自己的渲染器
    private bool running, faceRight;
    private float activePoisonDuration, activePoisonDamage;//本次释放使用的毒参数

    private void OnEnable()
    {
        ResolveVisual();
        running = false;
        usedTime = 0;
        lastTime = -1f;
        hitCounts.Clear();
        hitLifeVersions.Clear();
        target = lastTarget = null;
        targetLifeVersion = lastTargetLifeVersion = -1;
    }

    public void Launch(Transform source, Vector2 position, bool right, float duration = -1f, float damagePerStack = -1f)//由PlayerManager统一放置和启用
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ResolveVisual();
        caster = source;
        faceRight = right;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        activePoisonDuration = duration >= 0f ? duration : poisonDuration;
        activePoisonDamage = damagePerStack >= 0f ? damagePerStack : poisonDamagePerStack;
        usedTime = 0;
        lastTime = Time.time;
        hitCounts.Clear();
        hitLifeVersions.Clear();
        lastTarget = null;
        lastTargetLifeVersion = -1;
        running = true;
        nextHitAt = Time.time;
        ShaEffectUtility.Play(SM, animation, sr, right);
        SetTarget(FindTarget(true));
        if (target == null || maxExistTime <= 0f) StopEffect();
    }

    private void FixedUpdate()
    {
        if (!running) return;
        if (Time.time - lastTime >= maxExistTime || maxUsedTime > 0 && usedTime >= maxUsedTime)
        {
            StopEffect();
            return;
        }
        if (!ShaEffectUtility.IsAlive(target) || target.LifeVersion != targetLifeVersion)
            SetTarget(FindTarget(usedTime == 0));//仅死亡、停用或重生会丢弃追踪，无敌不使弹跳链直接中止
        if (target == null)
        {
            StopEffect();
            return;
        }
        Vector2 position = transform.position;
        Vector2 next = Vector2.MoveTowards(position, ShaEffectUtility.Center(target), flySpeed * Time.fixedDeltaTime);//靠近目标时不会越过后反复振荡
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        if (Time.time >= nextHitAt && ShaEffectUtility.SegmentHits(target, position, next, hitRadius)) HitTarget();
    }

    private void HitTarget()
    {
        if (!ShaEffectUtility.IsAlive(target) || target.LifeVersion != targetLifeVersion)
        {
            SetTarget(FindTarget(usedTime == 0));
            return;
        }
        EnemyMovement hit = target;//先登记本次弹跳，免控目标同样消耗一次弹跳次数
        int count = HitCount(hit);
        hitCounts[hit] = count + 1;
        hitLifeVersions[hit] = hit.LifeVersion;
        usedTime++;
        Health health = hit.GetComponent<Health>();
        if (ShaEffectUtility.CanBeHit(hit))//索敌资格与命中效果分开：无敌仍可弹向它，但不会穿透无敌施加眩晕或毒素
        {
            hit.Stun(this, stunDuration);
            health.ApplyPoison(caster, activePoisonDuration, activePoisonDamage);
        }
        lastTarget = hit;
        lastTargetLifeVersion = hit.LifeVersion;
        nextHitAt = Time.time + minimumBounceTime;
        SetTarget(maxUsedTime <= 0 || usedTime < maxUsedTime ? FindTarget(false) : null);
        if (target == null) StopEffect();
    }

    private EnemyMovement FindTarget(bool initial)//次数最少优先；同次数取最近；同距离随机
    {
        candidates.Clear();
        int bestCount = int.MaxValue;
        float bestDistance = float.PositiveInfinity;
        Vector2 position = transform.position;
        float range = initial ? searchRange : bounceRange;
        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies)
        {
            if (!ShaEffectUtility.IsAlive(enemy)
                || enemy == lastTarget && enemy.LifeVersion == lastTargetLifeVersion) continue;
            Vector2 delta = ShaEffectUtility.Center(enemy) - position;
            if (initial && delta.x * (faceRight ? 1f : -1f) < 0f) continue;
            float distance = delta.sqrMagnitude;
            if (distance > range * range) continue;
            int count = HitCount(enemy);
            if (count < bestCount || count == bestCount && distance < bestDistance - 0.0001f)
            {
                bestCount = count;
                bestDistance = distance;
                candidates.Clear();
                candidates.Add(enemy);
            }
            else if (count == bestCount && Mathf.Abs(distance - bestDistance) <= 0.0001f)
                candidates.Add(enemy);
        }
        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }

    private int HitCount(EnemyMovement enemy)//旧生命留下的次数不能影响同对象的新敌人
        => hitLifeVersions.TryGetValue(enemy, out int version) && version == enemy.LifeVersion
            && hitCounts.TryGetValue(enemy, out int count) ? count : 0;

    private void SetTarget(EnemyMovement enemy)//选中时固定版本，飞行途中重生不得沿用旧追踪
    {
        target = enemy;
        targetLifeVersion = enemy != null ? enemy.LifeVersion : -1;
    }

    private void ResolveVisual()
    {
        if (SM == null) SM = GetComponentInChildren<SpriteManager>(true);
        if (SM != null && sr == null) sr = SM.GetComponent<SpriteRenderer>();
    }

    public void StopEffect()//可由管理器回收，关闭不影响已经施加给敌人的毒素
    {
        running = false;
        target = lastTarget = null;
        targetLifeVersion = lastTargetLifeVersion = -1;
        hitCounts.Clear();
        hitLifeVersions.Clear();
        ShaEffectUtility.Hide(SM, sr);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        running = false;
        target = lastTarget = null;
        targetLifeVersion = lastTargetLifeVersion = -1;
        hitCounts.Clear();
        hitLifeVersions.Clear();
        ShaEffectUtility.Hide(SM, sr);
    }
}

//三个独立效果共用命中和动画辅助；手动扫掠同样遵守受击箱开关，不依赖刚体是否休眠。
internal static class ShaEffectUtility
{
    internal static bool IsAlive(EnemyMovement enemy)//弹跳基本条件只要求这是场上存活的敌人；已弹过不等于永久排除
        => enemy != null && enemy.isActiveAndEnabled && enemy.GetComponent<Health>() != null && !enemy.GetComponent<Health>().Dead;

    internal static bool CanBeHit(EnemyMovement enemy)//新命中统一尊重受击箱开关、短暂无敌和死亡状态
        => enemy != null && enemy.CanReceiveHit;

    internal static Bounds GetBounds(EnemyMovement enemy) => enemy.BodyCollider != null
        ? enemy.BodyCollider.bounds : new Bounds(enemy.transform.position, Vector3.one * 0.5f);

    internal static Vector2 Center(EnemyMovement enemy) => GetBounds(enemy).center;

    internal static bool SegmentHits(EnemyMovement enemy, Vector2 from, Vector2 to, float radius)//扫掠线段避免高速能量波穿过薄碰撞箱
    {
        Bounds bounds = GetBounds(enemy);
        bounds.Expand(Mathf.Max(0f, radius) * 2f);
        Vector3 start = new(from.x, from.y, bounds.center.z);
        if (bounds.Contains(start)) return true;
        Vector2 delta = to - from;
        if (delta.sqrMagnitude <= 0.0000001f) return false;
        return bounds.IntersectRay(new Ray(start, new Vector3(delta.x, delta.y, 0f).normalized), out float distance)
            && distance <= delta.magnitude;
    }

    internal static void Play(SpriteManager sm, SpriteData data, SpriteRenderer renderer, bool right)
    {
        if (sm == null) return;
        if (!sm.gameObject.activeSelf) sm.gameObject.SetActive(true);
        if (renderer != null) renderer.enabled = true;
        sm.Resume();
        sm.ChangeSpriteFace(right);
        if (data != null && data.sprites != null && data.sprites.Length > 0)
            sm.ChangeState(new SpriteData { sprites = data.sprites, fps = data.fps, loop = true });
    }

    internal static void Hide(SpriteManager sm, SpriteRenderer renderer)
    {
        if (sm != null) sm.Pause();
        if (renderer != null) renderer.enabled = false;
    }
}

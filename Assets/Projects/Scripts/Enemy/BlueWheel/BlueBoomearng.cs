using System.Collections.Generic;
using UnityEngine;

//蓝色天兵的独立飞轮：去程和回程各命中同一角色一次，中断后只沿当前水平方向离场。
public class BlueBoomearng : MonoBehaviour
{
    private enum FlightMode { Outbound, WaitingReturn, Returning, WaitingCatch, Free }
    [SerializeField] private SpriteManager SM;//播放飞轮自己的循环帧，不依赖持有者本体动画。
    [SerializeField] private SpriteData flight;
    [SerializeField, Min(0f)] private float damage = 10f;//每次发射保存伤害倍率，发出后不随持有者的增幅变化。
    [SerializeField, Min(0.01f)] private float flySpeed = 10f;
    [SerializeField, Min(0.1f)] private float outboundDistance = 9f;//隐身投掷的自动折返距离。
    [SerializeField, Min(0.01f)] private float returnSpeed = 14f;//无本体收回动画时的回程速度。
    [SerializeField, Min(1f)] private float maxLifetime = 20f;//异常配置或无边界场景的回收兜底。
    [SerializeField] private bool knockback = true;
    [SerializeField] private bool sourceFacesRight = true;
    [SerializeField, Range(0.01f, 1f)] private float hitWidthRatio = 0.65f, hitHeightRatio = 0.65f;
    [SerializeField, Min(0.01f)] private float minimumHitSize = 0.15f;
    private SpriteRenderer sr;
    private BlueWheelCombat owner;
    private Health ownerHealth;
    private int ownerLifeVersion;//同一个池成员重生不能取回或反弹到上一条生命留下的轮。
    private PlayerManager players;
    private Vector2 bounds, direction, returnFrom, lastDirection;
    private float launchAt, expiresAt, travelled, flightDamage, returnStarted, returnDuration;
    private bool flying, started, hiddenCast, catchOnArrival;
    private FlightMode mode;
    private readonly HashSet<Health> hitPlayers = new();//去、回两段分别去重，换人后读取实际上场角色。
    public bool IsFlying => flying;
    public float FlySpeed => Mathf.Max(0.01f, flySpeed);
    public bool BelongsTo(BlueWheelCombat source) => flying && owner == source && OwnerStillValid;
    private bool OwnerStillValid => owner != null && owner.isActiveAndEnabled && ownerHealth != null
        && !ownerHealth.Dead && ownerHealth.LifeVersion == ownerLifeVersion;
    private Vector2 TargetPosition => owner != null ? (hiddenCast ? (Vector2)owner.transform.position : owner.CatchPosition)
        : (Vector2)transform.position;

    public void Launch(BlueWheelCombat source, PlayerManager party, Vector2 position, bool right,
        Vector2 arenaBounds, bool hidden, float delay)//由场景池提供引用，预制体只保存飞行与伤害配置。
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (SM == null) SM = GetComponent<SpriteManager>();
        owner = source;
        ownerHealth = source != null ? source.GetComponent<Health>() : null;
        ownerLifeVersion = ownerHealth != null ? ownerHealth.LifeVersion : -1;
        players = party;
        hiddenCast = hidden;
        bounds = new Vector2(Mathf.Min(arenaBounds.x, arenaBounds.y), Mathf.Max(arenaBounds.x, arenaBounds.y));
        direction = lastDirection = right ? Vector2.right : Vector2.left;
        launchAt = Time.time + Mathf.Max(0f, delay);
        expiresAt = launchAt + Mathf.Max(1f, maxLifetime);
        travelled = 0f;
        flightDamage = Mathf.Max(0f, damage) * (source != null ? source.DamageMultiplier : 1f);
        mode = FlightMode.Outbound;
        catchOnArrival = false;
        hitPlayers.Clear();
        SetPosition(position);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        flying = true;
        started = delay <= 0f;
        if (sr != null)
        {
            sr.flipX = right != sourceFacesRight;
            sr.enabled = started;
            SpriteRenderer body = source != null ? source.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (body != null) { sr.sortingLayerID = body.sortingLayerID; sr.sortingOrder = body.sortingOrder + 1; }
        }
        if (SM != null)
        {
            if (flight != null && flight.sprites != null && flight.sprites.Length > 0) SM.ChangeState(flight);
            else SM.Pause();
        }
        SetPosition(ClampToBounds(transform.position, out _));//场边生成也保留完整轮形，不在回程前提前回池。
        if (!BattleRun.IsRunning || !OwnerStillValid) StopEffect();
    }
    private void FixedUpdate()
    {
        if (!flying) return;
        if (!BattleRun.IsRunning || Time.time >= expiresAt) { StopEffect(); return; }
        if (mode != FlightMode.Free && !OwnerStillValid) MakeFree();
        if (!flying) return;
        if (!started)
        {
            if (Time.time < launchAt) return;
            started = true;
            if (hiddenCast && owner != null) SetPosition(owner.transform.position);
            if (sr != null) sr.enabled = true;
        }
        Vector2 from = transform.position;
        if (mode == FlightMode.WaitingCatch)
        {
            SetPosition(TargetPosition);
            return;
        }
        if (mode == FlightMode.WaitingReturn) return;//普通投掷在边缘保持旋转，等本体开始收回动画。
        Vector2 to;
        if (mode == FlightMode.Returning)
        {
            float t = Mathf.Clamp01((Time.time - returnStarted + Time.fixedDeltaTime) / returnDuration);
            to = Vector2.Lerp(returnFrom, TargetPosition, t);
            CheckPlayerHit(from, to);
            if (!flying) return;
            if (mode != FlightMode.Returning) return;//反伤令持有者死亡时已改为离场，不再覆盖其方向。
            SetPosition(to);
            SaveDirection(from, to);
            if (t >= 1f)
            {
                if (catchOnArrival) StopEffect();
                else mode = FlightMode.WaitingCatch;
            }
            return;
        }
        float step = FlySpeed * Time.fixedDeltaTime;
        if (hiddenCast && mode == FlightMode.Outbound)
            step = Mathf.Min(step, Mathf.Max(0f, Mathf.Max(0.1f, outboundDistance) - travelled));
        to = from + direction * step;
        bool reachedEdge = false;
        if (mode == FlightMode.Outbound) to = ClampToBounds(to, out reachedEdge);
        travelled += step;
        CheckPlayerHit(from, to);
        if (!flying) return;
        SetPosition(to);
        SaveDirection(from, to);
        if (mode == FlightMode.Free)
        {
            if (OutsideBounds()) StopEffect();
        }
        else if (mode == FlightMode.Outbound)
        {
            if (hiddenCast && (reachedEdge || travelled >= Mathf.Max(0.1f, outboundDistance)))
                BeginReturn(Vector2.Distance(to, TargetPosition) / Mathf.Max(0.01f, returnSpeed), true);
            else if (reachedEdge) mode = FlightMode.WaitingReturn;
        }
    }
    public void BeginReturn(float seconds, bool autoCatch)//普通回程由本体收回帧定时，隐身回程到本体即可回池。
    {
        if (!flying || !started || mode != FlightMode.Outbound && mode != FlightMode.WaitingReturn || !OwnerStillValid) return;
        mode = FlightMode.Returning;
        returnFrom = transform.position;
        returnStarted = Time.time;
        returnDuration = Mathf.Max(Time.fixedDeltaTime, seconds);
        catchOnArrival = autoCatch;
        hitPlayers.Clear();
    }
    public void Catch(BlueWheelCombat source)//只收回真正属于这一轮、已经返程的飞轮，不回收其他池使用者。
    {
        if (!BelongsTo(source) || mode != FlightMode.Returning && mode != FlightMode.WaitingCatch) return;
        SetPosition(TargetPosition);
        StopEffect();
    }
    public void Abandon(BlueWheelCombat source)
    {
        if (!flying || owner != source) return;
        MakeFree();
    }
    private void MakeFree()
    {
        if (!started) { StopEffect(); return; }//尚未从隐身本体发出的延迟轮直接取消。
        mode = FlightMode.Free;
        direction = new Vector2(Mathf.Abs(lastDirection.x) > 0.001f ? Mathf.Sign(lastDirection.x) : Mathf.Sign(direction.x), 0f);
        if (direction.x == 0f) direction = Vector2.right;
        //已去程/回程命中过的角色继续保持去重，离场不再凭空多结算一次。
    }
    private void SaveDirection(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude > 0.000001f) lastDirection = delta.normalized;
        if (sr != null && Mathf.Abs(lastDirection.x) > 0.001f) sr.flipX = (lastDirection.x > 0f) != sourceFacesRight;
    }
    private bool OutsideBounds()
    {
        if (sr == null) return transform.position.x < bounds.x || transform.position.x > bounds.y;
        return sr.bounds.max.x < bounds.x || sr.bounds.min.x > bounds.y;
    }
    private Vector2 ClampToBounds(Vector2 position, out bool reachedEdge)//按整张轮形夹在场内，不只比较中心点。
    {
        float half = sr != null ? sr.bounds.extents.x : 0f;
        float offset = sr != null ? sr.bounds.center.x - transform.position.x : 0f;
        float left = bounds.x + half - offset;
        float right = bounds.y - half - offset;
        if (left > right) left = right = (bounds.x + bounds.y) * 0.5f - offset;
        reachedEdge = direction.x > 0f ? position.x >= right : position.x <= left;
        position.x = Mathf.Clamp(position.x, left, right);
        return position;
    }
    private void CheckPlayerHit(Vector2 from, Vector2 to)//扫过整段位移，快速飞轮也不能跳过玩家的较薄碰撞箱。
    {
        PlayerController player = players != null ? players.CurrentPlayer : null;
        if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) return;
        Health health = player.GetComponent<Health>();
        Collider2D body = player.GetComponent<Collider2D>();
        if (health == null || health.Dead || body == null || !body.enabled || hitPlayers.Contains(health)) return;
        Vector2 size = sr != null ? (Vector2)sr.bounds.size : Vector2.one * minimumHitSize;
        size = new Vector2(Mathf.Max(minimumHitSize, size.x * hitWidthRatio), Mathf.Max(minimumHitSize, size.y * hitHeightRatio));
        Bounds target = body.bounds;
        target.Expand(new Vector3(size.x, size.y, 1f));
        Vector2 offset = sr != null ? (Vector2)sr.bounds.center - (Vector2)transform.position : Vector2.zero;
        Vector3 start = new(from.x + offset.x, from.y + offset.y, target.center.z);
        Vector2 delta = to - from;
        if (!target.Contains(start) && (delta.sqrMagnitude <= 0.000001f
            || !target.IntersectRay(new Ray(start, delta.normalized), out float distance) || distance > delta.magnitude)) return;
        hitPlayers.Add(health);//护盾、切人无敌挡住也消耗本段接触机会，不逐帧重试。
        float before = health.CurBlood;
        Transform source = OwnerStillValid ? owner.transform : transform;//上一生命留下的轮不能把反伤送给新出生的池成员。
        health.TakeDamage(new DamageInfo(source, flightDamage, knockback));
        if (health.CurBlood < before && OwnerStillValid) owner.UpdateAngry();
    }
    private void SetPosition(Vector2 position) => transform.position = new Vector3(position.x, position.y, transform.position.z);
    public void StopEffect()//停用即归池，下一次Launch完整重置去回程和命中记录。
    {
        flying = false;
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
    private void OnDisable()
    {
        flying = started = false;
        owner = null; ownerHealth = null; players = null;
        hitPlayers.Clear();
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
    }
}

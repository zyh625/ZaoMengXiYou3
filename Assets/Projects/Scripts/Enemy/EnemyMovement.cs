using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    //统一登记当前启用的敌人，吸附范围可在每个有效帧重新检索。
    private static readonly HashSet<EnemyMovement> activeEnemies = new();
    public static IEnumerable<EnemyMovement> ActiveEnemies => activeEnemies;

    //吸附与释放后的下落都优先于敌人自身行动，落地后才归还控制。
    private enum ExternalControlState { None, Suction, Falling }
    private ExternalControlState externalControl;
    private Object suctionOwner;
    private Vector2 suctionTarget;
    private float suctionSpeed, savedGravity, fallBeganAt;
    private bool gravitySaved;
    private Health health;
    private ActorStatus status;//通用冰冻只锁位移，原眩晕和携带与它共用位置约束来源
    private EnemyController controller;
    private Collider2D bodyCollider;//缓存本体碰撞箱，技能按真实脚底筛选地面目标
    public Collider2D BodyCollider => bodyCollider;
    public int LifeVersion => health != null ? health.LifeVersion : 0;//效果记录命中时同时保存版本，避免把新出生的池成员当作旧目标
    public bool CanReceiveHit => isActiveAndEnabled && health != null && !health.Dead && !health.IsInvincible
        && (controller == null || controller.CanReceiveHit);//手动命中统一检查存活、来源无敌和既有受击箱开关
    private readonly Dictionary<Object, float> stuns = new();//不同来源眩晕分别到期，投射物消失不提前取消已经命中的眩晕
    private readonly List<Object> expiredStuns = new();//复用过期键列表，避免每帧创建临时集合
    private Vector2 stunnedPosition;//普通眩晕锁定世界坐标，后续攻击不能改变位置
    private Object carryOwner;//回旋镖携带拥有独立来源，只允许自己的效果移动和释放目标
    private Vector2 carryTarget;//携带过程中由投射物更新的世界坐标
    private bool freezeWasActive;//最后一个冻结来源结束后，只恢复一次竖直下落
    private bool freezeConstraintsSaved;//物理步内也锁住位置，避免被冻结的敌人被碰撞挤走
    private RigidbodyConstraints2D savedConstraints;//冻结结束时恢复原本的旋转和位置约束
    public bool IsStunned => health != null && !health.Boost && HasActiveStun();//不把过期或免控中的来源算作有效眩晕
    public bool IsCarried => carryOwner != null && health != null && !health.Dead;//携带属于强控制，普通霸体不能让已经被抓住的敌人脱离
    private readonly Dictionary<Object, QuicksandMotion> quicksands = new();//多个流沙分别维护自己的减速和拉力
    private readonly struct QuicksandMotion//横向移动统一缩放一次，拉力是固定速度而不是加速度
    {
        public readonly float Ratio, PullVelocity;
        public readonly float? CenterX;//可选中心用于限制最终总横速，避免自主移动叠加拉力后越过中心
        public QuicksandMotion(float ratio, float pullVelocity, float? centerX)
        {
            Ratio = Mathf.Clamp01(ratio);
            PullVelocity = pullVelocity;
            CenterX = centerX;
        }
    }
    private float appliedFieldVelocity, previousFieldResult;//下一物理步先移除上次拉力，防止固定拉力逐帧叠加
    protected Rigidbody2D body;
    [SerializeField] private float moveSpeed, backSpeed, upSpeed, slowSpeed,boostRatio;//各种速度以及愤怒加速
    private float curMoveSpeed, curSlowSpeed;
    [SerializeField] private float minNormalY;
    [SerializeField] private LayerMask groundLayer;
    private ContactFilter2D groundFilter;
    private ContactPoint2D[] contacts = new ContactPoint2D[16];
    private bool isGround;
    public bool IsGround => isGround;
    public bool LandedThisFrame = false;
    private Transform player;
    public float DeltaX => player.position.x - transform.position.x;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        status = health != null ? health.Status : GetComponent<ActorStatus>();
        controller = GetComponent<EnemyController>();
        bodyCollider = GetComponent<BoxCollider2D>();
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
    }
    private void OnEnable()
    {
        activeEnemies.Add(this);
        curMoveSpeed = moveSpeed;
        curSlowSpeed = slowSpeed;
        if (health != null) health.DebuffsCleared += ClearControlDebuffs;
    }
    //按来源施加普通晕眩；霸体可拒绝，无敌拒绝所有控制，死亡始终优先。
    public void Stun(Object source, float duration = 2f)
    {
        if (source == null || duration <= 0f || !CanApplyControl()) return;
        if (!HasActiveStun() && !IsCarried) stunnedPosition = body.position;
        float expiresAt = Time.time + duration;
        if (stuns.TryGetValue(source, out float previousExpiry)) expiresAt = Mathf.Max(expiresAt, previousExpiry);
        stuns[source] = expiresAt;
        freezeWasActive = true;
        SaveGravity();
        FreezeBody();
        controller?.HoldControlPose();
    }

    public bool HasStun(Object source)//仅查询本来源的普通晕眩，冰冻使用ActorStatus独立的强控入口
        => source != null && isActiveAndEnabled && health != null && !health.Dead && !health.Boost
            && stuns.TryGetValue(source, out float expiresAt) && Time.time < expiresAt;

    public void EndStun(Object source)//碎裂或打断只解除自己的冻结，麻痹药剂等其他来源继续计时
    {
        if (source == null || !stuns.Remove(source)) return;
        if (freezeWasActive && !HasActiveStun() && !IsCarried && body != null)
            ResumeAfterFreeze();
    }
    //回程携带是强控制，不被普通霸体拒绝；冰块已有位置锁时仍由冰冻优先。
    public void BeginCarry(Object owner, Vector2 position)
    {
        if (owner == null || !CanApplyControl(ControlStrength.Strong) || MovementBlocked || (carryOwner != null && carryOwner != owner)) return;
        carryOwner = owner;
        carryTarget = position;
        freezeWasActive = true;
        SaveGravity();
        ApplyCarryPosition();
        controller?.HoldControlPose();
    }
    public void UpdateCarryTarget(Object owner, Vector2 position)//只有当前携带者能更新终点
    {
        if (carryOwner != owner || owner == null) return;
        if (health != null && health.Dead)
        {
            EndCarry(owner);
            return;
        }
        carryTarget = position;
        ApplyCarryPosition();
    }
    public void EndCarry(Object owner)//另一条回旋镖结束不会释放当前携带者的目标
    {
        if (carryOwner != owner || owner == null) return;
        carryOwner = null;
        stunnedPosition = body.position;
        if (!HasActiveStun()) ResumeAfterFreeze();
    }
    //流沙脚本每次刷新来源即可，重复设置不会叠加同一来源的速度。
    public void SetQuicksand(Object source, float slowRatio, float pullVelocity, float? centerX = null)
    {
        if (source == null || !isActiveAndEnabled || (health != null && health.Dead)) return;
        quicksands[source] = new QuicksandMotion(slowRatio, pullVelocity, centerX);
    }
    public void RemoveQuicksand(Object source)//离开领域时只清理该领域的影响
    {
        if (source != null) quicksands.Remove(source);
    }
    //只读查询不更新落地边沿标记，多个技能查询不会吞掉LandedThisFrame。
    public bool IsOnGroundNow() => body != null && CheckGround();
    private bool CanApplyControl(ControlStrength strength = ControlStrength.Ordinary)//还要遵守敌人现有受击箱的无敌开关
        => body != null && CanReceiveHit && health.CanReceiveControl(strength);
    private bool HasActiveStun()//来源可能已销毁，命中后的眩晕仍保留到自己的结束时刻
    {
        foreach (KeyValuePair<Object, float> stun in stuns)
            if (Time.time < stun.Value) return true;
        return false;
    }
    private void RemoveExpiredStuns()//移除计时结束的来源，避免长关卡持续积累失效键
    {
        expiredStuns.Clear();
        foreach (KeyValuePair<Object, float> stun in stuns)
            if (Time.time >= stun.Value) expiredStuns.Add(stun.Key);
        foreach (Object source in expiredStuns) stuns.Remove(source);
    }
    private void ApplyCarryPosition()//携带只认当前投射物给出的坐标，不保留任何旧的击退速度
    {
        FreezeBody();
        if (MovementBlocked)
        {
            status.EnforcePosition();//冰冻或融化中的人物不能被已有携带来源拉离冰块
            return;
        }
        body.position = carryTarget;
        stunnedPosition = carryTarget;
    }
    private void FreezeBody()//直接设置坐标仍可携带移动，但物理解算不会再推开被冻结的目标
    {
        if (status != null) status.AcquireMotionLock(this);//不同控制只释放自己的来源，不把别人的FreezePosition误存成初始约束
        else if (!freezeConstraintsSaved)
        {
            savedConstraints = body.constraints;
            freezeConstraintsSaved = true;
        }
        if (status == null) body.constraints |= RigidbodyConstraints2D.FreezePosition;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
    }
    private void RestoreFreezeConstraints()//冻结结束与对象池禁用都必须释放临时位置约束
    {
        if (status != null) status.ReleaseMotionLock(this);
        if (!freezeConstraintsSaved) return;
        if (body != null) body.constraints = savedConstraints;
        freezeConstraintsSaved = false;
    }
    private void SaveGravity()//所有冻结和吸附共用一次保存，嵌套受控不会把零重力误当作原始值
    {
        if (gravitySaved) return;
        savedGravity = body.gravityScale;
        gravitySaved = true;
    }
    private void ResumeAfterFreeze()//还有吸附来源则交还吸附，否则清零速度并开始竖直下落
    {
        freezeWasActive = false;
        RestoreFreezeConstraints();
        controller?.ReleaseControlPose();
        if (externalControl == ExternalControlState.Suction && suctionOwner != null)
        {
            body.gravityScale = 0f;
            ApplySuctionVelocity();
        }
        else BeginSuctionFall();
    }
    private void ClearControlDebuffs() => ClearExternalControl();//驱散同时解除普通晕眩、抓取、吸附与流沙，恢复重力和本来源位置锁
    //同一次吸附只打断敌人一次；不同释放者不能改写正在生效的吸附。
    public void BeginSuction(Object owner, Vector2 target, float speed)
    {
        if (owner == null || !CanApplyControl(ControlStrength.Strong) || MovementBlocked) return;//吸附按强控制处理，霸体仍可被吸，但无敌拒绝
        if (externalControl == ExternalControlState.Suction && suctionOwner != owner) return;
        if (externalControl == ExternalControlState.None && !freezeWasActive && controller != null)
            controller.InterruptForSuction();
        SaveGravity();
        externalControl = ExternalControlState.Suction;
        suctionOwner = owner;
        suctionTarget = target;
        suctionSpeed = Mathf.Max(0f, speed);
        body.gravityScale = 0f;
        if (!HasActiveStun() && !IsCarried) ApplySuctionVelocity();
    }
    //目标随释放者移动，立即刷新速度以避免角色执行顺序影响本次物理更新。
    public void UpdateSuctionTarget(Object owner, Vector2 target)
    {
        if (externalControl != ExternalControlState.Suction || suctionOwner != owner) return;
        if (health != null && health.Dead)
        {
            ClearExternalControl();
            return;
        }
        suctionTarget = target;
        if (!HasActiveStun() && !IsCarried) ApplySuctionVelocity();
    }
    //完整释放收束到最后有效帧的终点；打断只停吸，当前位置开始自由落下。
    public void EndSuction(Object owner, bool complete, Vector2 finalTarget)
    {
        if (externalControl != ExternalControlState.Suction || suctionOwner != owner) return;
        if (health != null && health.Dead)
        {
            ClearExternalControl();
            return;
        }
        if (HasActiveStun() || IsCarried || MovementBlocked)
        {
            externalControl = ExternalControlState.None;
            suctionOwner = null;
            if (!HasActiveStun() && !IsCarried) RestoreGravity();//冰块自身不接管重力，已结束的吸附必须及时还原
            return;//冻结优先，吸附结束不能瞬移或恢复仍被眩晕占用的重力
        }
        if (complete) body.position = finalTarget;
        BeginSuctionFall();
    }
    //由控制器在死亡判定后调用，统一阻止普攻、技能、击退及特殊冲刺覆盖控制。
    public bool ProcessExternalControl()
    {
        if (health != null && health.Dead)
        {
            ClearExternalControl();
            return false;
        }
        RemoveExpiredStuns();
        if (health != null && health.Boost)
        {
            stuns.Clear();//霸体只移除普通晕眩；携带、抓取和吸附继续由原拥有者控制
        }
        if (carryOwner != null && carryOwner is Behaviour carry && !carry.isActiveAndEnabled) carryOwner = null;
        if (carryOwner != null)
        {
            ApplyCarryPosition();
            controller?.HoldControlPose();
            return true;
        }
        if (HasActiveStun())
        {
            if (MovementBlocked) status.EnforcePosition();
            else body.position = stunnedPosition;
            FreezeBody();
            controller?.HoldControlPose();
            return true;
        }
        if (freezeWasActive) ResumeAfterFreeze();
        if (MovementBlocked && externalControl == ExternalControlState.Falling)
        {
            externalControl = ExternalControlState.None;//融化中的空中敌人可以攻击，不能一直困在等待落地的分支
            RestoreGravity();
        }
        if (externalControl == ExternalControlState.None) return false;
        if (externalControl == ExternalControlState.Suction)
        {
            if (suctionOwner != null)
            {
                ApplySuctionVelocity();
                return true;
            }
            BeginSuctionFall();
        }
        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
        //位置收束后的本次接触信息可能仍属于旧位置，至少等一次物理更新再判落地。
        if (Time.fixedTime > fallBeganAt)
        {
            CheckIsGround();
            if (isGround) externalControl = ExternalControlState.None;
        }
        return true;
    }
    //死亡和对象池禁用时立即恢复原重力，避免下次启用保留吸附状态。
    public void ClearExternalControl()
    {
        bool wasControlled = externalControl != ExternalControlState.None || gravitySaved || freezeWasActive;
        stuns.Clear();
        carryOwner = null;
        freezeWasActive = false;
        RestoreFreezeConstraints();
        quicksands.Clear();
        RemoveAppliedFieldVelocity();
        RestoreGravity();
        if (wasControlled && body != null) body.linearVelocity = Vector2.zero;
        externalControl = ExternalControlState.None;
        suctionOwner = null;
        controller?.ReleaseControlPose();
    }
    //速度限制在下一物理步恰好抵达目标的范围内，避免越过目标后反复振荡。
    private void ApplySuctionVelocity()
    {
        if (MovementBlocked)
        {
            status.EnforcePosition();
            return;
        }
        body.linearVelocity = Vector2.ClampMagnitude(
            (suctionTarget - body.position) / Time.fixedDeltaTime, suctionSpeed);
    }
    //清除吸附产生的速度，随后只保留重力带来的竖直下落。
    private void BeginSuctionFall()
    {
        RestoreGravity();
        body.linearVelocity = Vector2.zero;
        suctionOwner = null;
        externalControl = ExternalControlState.Falling;
        fallBeganAt = Time.fixedTime;
        isGround = false;
        LandedThisFrame = false;
    }
    //吸附只临时接管重力，不改变敌人原本配置的重力系数。
    private void RestoreGravity()
    {
        if (!gravitySaved) return;
        if (body != null) body.gravityScale = savedGravity;
        gravitySaved = false;
    }
    protected virtual float MoveSpeedMultiplier => 1f;//子类增益复用移动入口，保持小怪原有速度与减速逻辑
    public void Move(bool slow)
    {
        if (MovementBlocked)
        {
            status.EnforcePosition();
            return;
        }
        Vector2 velocity = body.linearVelocity;
        velocity.x = (slow ? curSlowSpeed : curMoveSpeed) * MoveSpeedMultiplier * (DeltaX > 0f ? 1f : -1f);
        body.linearVelocity= velocity;
    }
    public void AttackedMove(bool r,bool up)
        => AttackedMove(r, true, up);//保留旧入口，新的受击数据分别控制两个方向

    public void AttackedMove(bool r, bool back, bool up)
    {
        if (HasActiveStun() || IsCarried || MovementBlocked) return;//真正破冰后状态已移除，该次攻击才正常产生击退
        Vector2 velocity = body.linearVelocity;
        velocity.x = back ? backSpeed * (r ? 1f : -1f) : 0f;
        velocity.y += up ? upSpeed : 0f;
        body.linearVelocity = velocity;
    }
    public void Stop()
    {
        if (MovementBlocked)
        {
            status.EnforcePosition();
            return;
        }
        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
    }
    public void CheckIsGround()
    {
        bool wasGround = isGround;
        isGround = CheckGround();
        LandedThisFrame = isGround && !wasGround;
    }
    private bool CheckGround()
    {
        float y = body.linearVelocityY;
        if (Mathf.Abs(y) > 0.1f)
            return false;
        int count = body.GetContacts(groundFilter, contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y >= minNormalY)
                return true; //接触面的法向量超出最小y向量
        }
        return false;
    }
    public int AirState()//-1下落，0在地面，1上升
    {
        if (isGround) return 0;
        return body.linearVelocityY > 0.1f ? 1 : -1;
    }
    private void OnDisable()
    {
        activeEnemies.Remove(this);
        if (health != null) health.DebuffsCleared -= ClearControlDebuffs;
        ClearExternalControl();
        if (body != null) body.linearVelocity = Vector2.zero;
        isGround = true;
    }
    public void SetTarget(Transform tran) => player = tran;
    public void Boost()
    {
        curMoveSpeed*=boostRatio;
        curSlowSpeed*=boostRatio;
    }
    public void ReturnSpeed()
    {
        curSlowSpeed = slowSpeed;
        curMoveSpeed = moveSpeed;
    }
    //控制器在自身判定前后分别调用，所有提前return也经过同一轮流沙速度处理。
    public void BeginMotionStep() => RemoveAppliedFieldVelocity();
    public void EndMotionStep()
    {
        if (body == null || !isActiveAndEnabled || (health != null && health.Dead)
            || MovementBlocked || freezeWasActive || externalControl != ExternalControlState.None || !IsOnGroundNow()) return;
        float pull = 0f;
        float ratio = 1f;
        bool hasField = false;
        foreach (KeyValuePair<Object, QuicksandMotion> field in quicksands)
        {
            if (field.Key == null) continue;
            hasField = true;
            pull += field.Value.PullVelocity;
            ratio *= field.Value.Ratio;
        }
        if (!hasField) return;
        float original = body.linearVelocityX;//自主走动、突刺等统一只缩放一次，不在持久速度上重复相乘
        float velocity = original * ratio + pull;
        foreach (KeyValuePair<Object, QuicksandMotion> field in quicksands)
        {
            if (field.Key == null || !field.Value.CenterX.HasValue) continue;
            float center = bodyCollider != null ? bodyCollider.bounds.center.x : body.position.x;//与领域筛选使用同一碰撞箱中心，兼容本体碰撞箱的水平偏移
            float distance = field.Value.CenterX.Value - center;
            float limit = distance / Time.fixedDeltaTime;
            if (distance > 0f && velocity > limit) velocity = limit;
            else if (distance < 0f && velocity < limit) velocity = limit;
            else if (Mathf.Abs(distance) < 0.001f && field.Value.Ratio <= 0.001f) velocity = 0f;
        }
        body.linearVelocity = new Vector2(velocity, body.linearVelocityY);
        appliedFieldVelocity = velocity - original;
        previousFieldResult = body.linearVelocityX;
    }
    private void RemoveAppliedFieldVelocity()//物理碰撞若已改写横速，不再减去旧拉力以免产生反向弹动
    {
        if (body != null && !Mathf.Approximately(appliedFieldVelocity, 0f)
            && Mathf.Abs(body.linearVelocityX - previousFieldResult) < 0.001f)
            body.linearVelocity = new Vector2(body.linearVelocityX - appliedFieldVelocity, body.linearVelocityY);
        appliedFieldVelocity = 0f;
    }
    private bool MovementBlocked => status != null && status.BlocksMovement;//固体冰冻与融化均禁止所有位移，动作和伤害由控制器分别处理
}

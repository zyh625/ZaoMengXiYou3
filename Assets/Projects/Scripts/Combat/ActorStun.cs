using System;
using UnityEngine;

//玩家的普通眩晕：霸体和无双能驱散，冰冻仍由ActorStatus独立管理。
[DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class ActorStun : MonoBehaviour
{
    private Health health;//统一判定无敌、死亡和普通霸体
    private Rigidbody2D body;//只约束位置和速度，不改写冰冻等效果使用的重力与约束配置
    private ActorStatus status;//强控制同时存在时优先遵守它的位置锁
    private float stunUntil;//按游戏时间计时，暂停不会消耗剩余时间
    private Vector2 heldPosition;//首次眩晕时的位置，重复命中只延长时间
    public event Action Changed;//控制器收到变化后立即中断旧动作或恢复输入
    public bool IsStunned => isActiveAndEnabled && health != null && !health.Dead && !health.Boost && Time.time < stunUntil;

    private void Awake()//动态挂载和预制体挂载使用同一初始化入口
    {
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        status = health.Status;
    }
    private void OnEnable()//所有驱散共用Health事件，未来无双新增来源也不漏清眩晕
    {
        health.DebuffsCleared += Clear;
        health.Died += OnDied;
    }
    public bool Apply(UnityEngine.Object source, float duration)//普通眩晕不越过无敌、死亡或霸体，返回是否真正生效
    {
        if (source == null || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)
            || !isActiveAndEnabled || !health.CanReceiveControl()) return false;
        if (!IsStunned) heldPosition = body.position;
        stunUntil = Mathf.Max(stunUntil, Time.time + duration);
        Changed?.Invoke();
        HoldPosition();
        return true;
    }
    private void Update()//到期或获得普通霸体时结束，死亡由事件即时清理
    {
        if (stunUntil > 0f && !IsStunned) Clear();
    }
    private void FixedUpdate() => HoldPosition();//阻止物理步中的旧攻击或跳跃速度继续位移
    private void LateUpdate() => HoldPosition();//物理重力产生的本帧偏移在渲染前收束，不修改其他状态的刚体设置
    private void HoldPosition()//冰冻和融化的位置约束优先，避免多来源控制互相恢复旧的刚体约束
    {
        if (!IsStunned || body == null) return;
        if (status != null && status.BlocksMovement) status.EnforcePosition();
        else body.position = heldPosition;
        body.linearVelocity = Vector2.zero;
    }
    public void Clear()//驱散、死亡和退场均不积压到下次登场
    {
        if (stunUntil <= 0f) return;
        stunUntil = 0f;
        Changed?.Invoke();
    }
    private void OnDied(DamageInfo damage) => Clear();//死亡不等待眩晕计时结束
    private void OnDisable()//只退订自己的来源，不改变其他控制状态
    {
        health.DebuffsCleared -= Clear;
        health.Died -= OnDied;
        Clear();
    }
}

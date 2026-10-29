using System;
using UnityEngine;

public class PurpleHeavenlyCombat : EnemyCombat
{
    [SerializeField, Min(0f)] private float hitGap = 0.5f;//相邻两次直接受伤短于此间隔时准备弹反，中毒跳伤不参与连击判断
    [SerializeField, Min(0.01f)] private float parryDuration = 0.8f;//举刀后等待下一次攻击的有效时间
    [SerializeField, Min(0f)] private float parryRecovery = 0.12f;//成功挡下一击后短暂保留举刀姿势，保证挡刀过程可见

    private Health health;//通过实际伤害结算判断连击，霸体和冰冻期间受伤也不会漏掉
    private float lastHitTime = float.NegativeInfinity;//每次弹反后重新计算下一组连续攻击
    private float parryUntil;//准备弹反与成功挡刀后的收势共用结束时刻
    private bool parryActive, parryArmed;//姿势持续和可格挡次数分别记录，一次举刀只能挡下一击
    public bool ParryActive => parryActive;
    public bool ParryArmed => parryArmed;
    public event Action ParryStarted;//控制器只在开始时切一次动作，不逐帧重置动画
    public event Action ParryEnded;//到期或成功挡刀收势完成后归还普通行为

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Health>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (health == null) health = GetComponent<Health>();
        ResetParry();
        if (health == null) return;
        health.DamageResolved += OnDamageResolved;
        health.DamageBlocked += TryParryDamage;
        health.Died += OnDied;
    }

    private void OnDamageResolved(DamageInfo damage, float actualDamage)//附加效果已经施加完后再驱散，不能让同一击的冰冻重新覆盖弹反
    {
        if (!isActiveAndEnabled || health == null || health.Dead || parryActive
            || actualDamage <= 0f || damage.IsPeriodic || damage.IsReflected) return;
        float now = Time.time;
        bool consecutive = now - lastHitTime < Mathf.Max(0f, hitGap);
        lastHitTime = now;
        if (!consecutive) return;

        parryActive = parryArmed = true;
        parryUntil = now + Mathf.Max(0.01f, parryDuration);
        lastHitTime = float.NegativeInfinity;
        AttackEnd();
        AttackOver();//打断本次出招，不把主动弹反算成攻击落空
        ClearAttackRequests();
        health.ClearDebuffs();//统一清除受击、中毒、冰冻和其他已登记的异常状态
        UpdateAngry();//一次触发只增加一次怒气，不因重复碰撞或播放挡刀姿势重复增加
        ParryStarted?.Invoke();
    }

    private bool TryParryDamage(DamageInfo damage)//拒绝整次下一击，包含该击的伤害、击退、击飞和附加效果
    {
        if (!isActiveAndEnabled || health == null || health.Dead || !parryArmed
            || Time.time >= parryUntil || damage.Amount <= 0f || damage.IsPeriodic || damage.IsReflected) return false;
        parryArmed = false;
        parryUntil = Time.time + Mathf.Max(0f, parryRecovery);
        return true;
    }

    private void Update()//计时独立于受击和冰冻的行为分支，暂停时随游戏缩放时间一起停止
    {
        if (!parryActive || health == null || health.Dead || Time.time < parryUntil) return;
        parryActive = parryArmed = false;
        ParryEnded?.Invoke();
    }

    private void OnDied(DamageInfo damage) => ResetParry();//死亡不能被举刀状态或结束通知改回待机

    private void ResetParry()//对象池再次启用时不继承上次生命的连击时间和待格挡次数
    {
        parryActive = parryArmed = false;
        parryUntil = 0f;
        lastHitTime = float.NegativeInfinity;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.DamageResolved -= OnDamageResolved;
            health.DamageBlocked -= TryParryDamage;
            health.Died -= OnDied;
        }
        ResetParry();
    }
}

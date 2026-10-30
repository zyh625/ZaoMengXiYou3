using UnityEngine;

public class BlueShieldController : EnemyController
{
    [SerializeField] private SpriteData Attack;//唯一一招近战攻击，对应原有攻击箱与检查范围。
    [SerializeField] private SpriteData ShieldWalk, ShieldAttacked;//正常行走和受击仍使用基类Walk、Attacked配置。
    private BlueShieldCombat combat;//盾牌冷却与真实伤害判定由战斗组件负责。
    private BlueShieldMovement movement;//通过共用移动入口停下，不修改重力或外部控制速度。
    private SpriteData shieldIdle;//举盾待机取行走第一帧，不在玩家身边原地踏步。
    private bool shieldImpact;//一次盾正面受击动画的持有状态，强控制或死亡立即接管。

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<BlueShieldCombat>();
        movement = GetComponent<BlueShieldMovement>();
        if (ShieldWalk != null && ShieldWalk.sprites != null && ShieldWalk.sprites.Length > 0)
            shieldIdle = new SpriteData { sprites = new[] { ShieldWalk.sprites[0] }, loop = true, fps = 1f };
    }

    protected override void OnEnable()
    {
        shieldImpact = false;
        base.OnEnable();
        if (combat == null) return;
        combat.ShieldChanged += OnShieldChanged;
        combat.ShieldImpacted += OnShieldImpacted;
    }

    protected override SpriteData ResolveStateAnimation(EnemyState next, SpriteData fallback)//只改变本角色的表现，不复制通用状态机。
    {
        if (combat == null || !combat.Shielded) return fallback;
        if (next == EnemyState.Walk && HasFrames(ShieldWalk)) return ShieldWalk;
        if (next == EnemyState.Idle && HasFrames(shieldIdle)) return shieldIdle;
        if (next == EnemyState.Attacked && shieldImpact && HasFrames(ShieldAttacked)) return ShieldAttacked;
        return fallback;
    }

    private static bool HasFrames(SpriteData data)//缺动画配置时沿用基类，避免把播放器切到空数据。
        => data != null && data.sprites != null && data.sprites.Length > 0;

    protected override void HandleAttack(int attack)
    {
        combat.LowerShield();//进入有效近战招式前先卸盾，从此刻开始盾牌冷却。
        shieldImpact = false;
        SM.ChangeState(Attack);
    }

    private void OnShieldChanged()//只在真正切形态的瞬间重选动画，同一状态不会逐帧重启。
    {
        if (combat != null && !combat.Shielded) shieldImpact = false;//盾硬直中遭背击时立即交回普通受击分支。
        if (!isActiveAndEnabled || EH == null || EH.Dead || shieldImpact) return;
        if (state == EnemyState.Walk || state == EnemyState.Idle)
        {
            EnemyState previous = state;
            SetState(previous == EnemyState.Walk ? EnemyState.Idle : EnemyState.Walk);
            SetState(previous);//通过原状态入口刷新对应形态，不直接修改状态字段。
        }
    }

    private void OnShieldImpacted()
    {
        if (EH == null || EH.Dead || movement == null) return;
        if (movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions) return;
        AttackOver();//先关闭尚存攻击窗口，随后才标记新的盾硬直。
        shieldImpact = true;
        movement.Stop();
        SetState(EnemyState.Attacked);
    }

    protected override bool ProcessSpecialState()//死亡之后、普通行动之前，只在没有外部控制时持有盾受击动作。
    {
        if (combat == null || movement == null) return false;
        if (movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions)
        {
            shieldImpact = false;
            combat.LowerShield();//冻结、携带和吸附时交回通用控制，不能被盾硬直抢占。
            return false;
        }
        if (!shieldImpact) return false;
        EH.AttackedEnd();//扣血发生在准备通知之后；完全减伤时也要清除普通受击请求。
        movement.Stop();
        if (!SM.IsFinished) return true;
        shieldImpact = false;
        SetState(EnemyState.Idle);
        return false;
    }

    private void LateUpdate()//普通受击结束且本轮没有转为攻击后，再举盾接续普通移动。
    {
        if (combat == null || movement == null || EH == null || EH.Dead || !combat.RaiseRequested) return;
        if (state != EnemyState.Idle && state != EnemyState.Walk) return;
        if (movement.IsExternallyControlled || !movement.IsOnGroundNow()
            || EH.Status != null && EH.Status.BlocksActions) return;
        combat.TryRaiseShield();
    }

    protected override void SpecialAttackOver()//冻结、晕眩或携带接管的同一帧立即卸盾，不等待下次物理更新。
    {
        shieldImpact = false;
        if (combat != null && movement != null && (movement.IsExternallyControlled
            || EH != null && EH.Status != null && EH.Status.BlocksActions)) combat.LowerShield();
    }

    protected override void OnDisable()
    {
        if (combat != null)
        {
            combat.ShieldChanged -= OnShieldChanged;
            combat.ShieldImpacted -= OnShieldImpacted;
        }
        shieldImpact = false;
        base.OnDisable();
    }
}

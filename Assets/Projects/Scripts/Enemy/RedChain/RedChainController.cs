using UnityEngine;

//只占用赤链武卒自己的出招和收招窗口；死亡、普通受击与强控制仍走 EnemyController。
public sealed class RedChainController : EnemyController
{
    [SerializeField] private SpriteData AttackOne, AttackTwo, AttackThree;
    [SerializeField, Min(0f)] private float recoveryDuration = 1.2f;
    private RedChainCombat combat;
    private RedChainMovement movement;
    private float resumeAt, recoveryUntil;
    private bool casting, waiting;

    public bool IsCasting => casting;
    public bool IsRecovering => Time.time < recoveryUntil;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => !IsRecovering && combat.SelectRequest(movement.DeltaX);

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<RedChainCombat>();
        movement = GetComponent<RedChainMovement>();
    }

    protected override void OnEnable()
    {
        casting = waiting = false;
        recoveryUntil = 0f;
        EH.SetSuperArmor(this, false);
        base.OnEnable();
        EH.Died += OnDied;
    }

    //本体大小只由预制体 Visual 的缩放（原图朝左，x 为负）和各动作图片的 PPU 决定，运行时不再缩放。

    protected override void HandleAttack(int attack)
    {
        SpriteData data = attack == 0 ? AttackOne : attack == 1 ? AttackTwo : AttackThree;
        casting = true;
        EH.SetSuperArmor(this, true);
        combat.BeginCast();
        SM.ChangeState(data);
        //保留原图的落地/伸链帧；补足前摇后才开始播放，数值不随美术帧数改变。
        float hold = Mathf.Max(0f, combat.Windup - combat.OpenFrame / Mathf.Max(1f, data.fps));
        resumeAt = Time.time + hold;
        waiting = hold > 0f;
        if (waiting) SM.Pause();
    }

    protected override bool ProcessSpecialState()
    {
        if (movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions)
        {
            if (casting) AttackOver();
            return false;
        }
        if (casting)
        {
            movement.Stop();
            if (waiting && Time.time < resumeAt) return true;
            if (waiting) { waiting = false; SM.Resume(); }
            combat.SampleFrame(SM.CurFrame);
            if (SM.IsFinished) FinishCast();
            return true;
        }
        if (!IsRecovering) return false;
        //收招可正常受击，不能吞掉基类的受击动画与击退。
        if (EH.Attacked || state == EnemyState.Attacked && !SM.IsFinished) return false;
        movement.Stop();
        SetState(EnemyState.Idle);
        return true;
    }

    private void LateUpdate()
    {
        //动画在 Update 到末帧的同一帧撤销霸体，不能多保护一个物理步。
        if (casting && SM.IsFinished && !EH.Dead) FinishCast();
    }

    private void FinishCast()
    {
        combat.SampleFrame(SM.CurFrame);
        AttackOver(true);
        movement.Stop();
        SetState(EnemyState.Idle);
    }

    protected override void SpecialAttackOver()
    {
        if (EH != null) EH.SetSuperArmor(this, false);
        if (casting) recoveryUntil = Time.time + recoveryDuration;
        casting = waiting = false;
        combat?.ClearAttackRequests();
    }

    private void OnDied(DamageInfo damage) => AttackOver();

    protected override void OnDisable()
    {
        if (EH != null) { EH.Died -= OnDied; EH.SetSuperArmor(this, false); }
        if (combat != null) AttackOver();
        casting = waiting = false;
        recoveryUntil = 0f;
        base.OnDisable();
    }
}

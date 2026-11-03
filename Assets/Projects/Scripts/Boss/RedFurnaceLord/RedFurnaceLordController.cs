using UnityEngine;

public sealed class RedFurnaceLordController : EnemyController
{
    // Idle / Walk are serialized by EnemyController; Hit / Death are resolved here.
    [SerializeField] private SpriteData PestleSmash, FurnaceBreath, ChainSweep, FurnaceOpen, Hit, Death;
    private RedFurnaceLordCombat combat;
    private RedFurnaceLordMovement movement;
    private int pending;
    private bool showingCore;
    private int shownCore;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => false;
    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<RedFurnaceLordCombat>();
        movement = GetComponent<RedFurnaceLordMovement>();
    }
    protected override SpriteData ResolveStateAnimation(EnemyState next, SpriteData fallback) =>
        next == EnemyState.Attacked ? Hit : next == EnemyState.Die ? Death : fallback;
    protected override bool ProcessSpecialState()
    {
        if (combat.Target != null) movement.SetTarget(combat.Target.transform);
        if (combat.IsCoreOpen)
        {
            if (combat.ControlInterrupted) { combat.EndCore(true); return false; }
            movement.Stop();
            if (!showingCore || shownCore != combat.CoreStarts)
            {
                showingCore = true; shownCore = combat.CoreStarts;
                SetState(EnemyState.Idle); SetState(EnemyState.Skill);
            }
            combat.SampleCoreFrame(SM.CurFrame);
            return true; // The non-looping open clip keeps its final pose for the remainder.
        }
        if (showingCore) { showingCore = false; SetState(EnemyState.Idle); }
        return false;
    }
    protected override bool SkillReady => state != EnemyState.Skill && combat.ReadyAttack >= 0
        && (state != EnemyState.Attacked || SM.IsFinished) && movement.IsOnGroundNow();
    protected override void SkillSet() => pending = combat.ReadyAttack;
    protected override void HandleSkill()
    {
        if (combat.IsCoreOpen) { SM.ChangeState(FurnaceOpen); return; }
        if (!combat.BeginAttack(pending)) { SetState(EnemyState.Idle); return; }
        SM.ChangeSpriteFace(combat.AttackFaceRight);
        SM.ChangeState(pending == 0 ? PestleSmash : pending == 1 ? FurnaceBreath : ChainSweep);
    }
    protected override void CheckSkill()
    {
        if (state != EnemyState.Skill || combat.IsCoreOpen) return;
        if (combat.ControlInterrupted || EH.Attacked && !EH.Boost)
        { combat.CancelAttack(); SM.Resume(); SetState(EnemyState.Idle); return; }
        combat.SampleFrame(SM.CurFrame);
        if (combat.HoldBreathPose(SM.CurFrame)) SM.Pause(); else SM.Resume();
    }
    protected override void SkillOver()
    {
        if (combat.IsCoreOpen) return;
        combat.CancelAttack(); SetState(EnemyState.Idle);
    }
    protected override void SpecialAttackOver() => combat?.CancelAttack();
    private void LateUpdate()
    {
        if (EH.Dead) return;
        if (combat.IsCoreOpen && showingCore && shownCore == combat.CoreStarts) combat.SampleCoreFrame(SM.CurFrame);
        else CheckSkill();
    }
    protected override void OnDisable()
    {
        combat?.CancelAttack(); showingCore = false; shownCore = 0;
        base.OnDisable();
    }
}

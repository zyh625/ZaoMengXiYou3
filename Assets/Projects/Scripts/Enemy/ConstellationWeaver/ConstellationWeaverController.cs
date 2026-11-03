using UnityEngine;

public sealed class ConstellationWeaverController : EnemyController
{
    [SerializeField] private SpriteData TwinAnchor, ThreeStarBind, SilkReposition;
    private ConstellationWeaverCombat combat;
    private ConstellationWeaverMovement movement;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => false;
    protected override void Awake() { base.Awake(); combat = GetComponent<ConstellationWeaverCombat>(); movement = GetComponent<ConstellationWeaverMovement>(); }
    protected override bool ProcessSpecialState()
    {
        if (EH.Attacked || movement.IsExternallyControlled || EH.Status.BlocksActions || state == EnemyState.Attacked && !SM.IsFinished) return false;
        if (state == EnemyState.Skill) { combat.MotionStep(); return false; }
        var target = combat.Target;
        if (target == null) { movement.Stop(); SetState(EnemyState.Idle); return true; }
        movement.CheckIsGround(); if (!movement.IsGround) return false;
        float delta = target.transform.position.x - transform.position.x;
        SM.ChangeSpriteFace(delta > 0f);
        if (combat.InRange)
        {
            if (combat.ReadySkill >= 0) return false;
            movement.Stop(); SetState(EnemyState.Idle); return true;
        }
        if (EH.Status.BlocksMovement) { movement.Stop(); SetState(EnemyState.Idle); return true; }
        SetState(EnemyState.Walk); movement.KeepRange(delta, EH.Slow); return true;
    }
    protected override bool SkillReady => state != EnemyState.Skill && combat.ReadySkill >= 0 && (state != EnemyState.Attacked || SM.IsFinished);
    protected override void SkillSet() => combat.BeginCast(combat.ReadySkill);
    protected override void HandleSkill()
    { SM.ChangeSpriteFace(combat.FaceRight); SM.ChangeState(combat.CurrentSkill == 0 ? TwinAnchor : combat.CurrentSkill == 1 ? ThreeStarBind : SilkReposition); }
    protected override void CheckSkill()
    {
        if (state != EnemyState.Skill) return;
        if (EH.Attacked || EH.Dead || movement.IsExternallyControlled || EH.Status.BlocksActions)
        { combat.CancelCast(); SetState(EnemyState.Idle); return; }
        combat.SampleFrame(SM.CurFrame);
        if (combat.CurrentSkill < 0) { SetState(EnemyState.Idle); return; }
        if (combat.HoldingFrame) SM.Pause(); else SM.Resume();
    }
    // The base invokes AttackOver immediately AFTER SkillSet, so this hook must not cancel a newly begun cast.
    protected override void SpecialAttackOver() { if (state == EnemyState.Skill) combat?.CancelCast(); }
    protected override void SkillOver() { combat.CancelCast(); movement.Stop(); SetState(EnemyState.Idle); }
    private void LateUpdate() { if (state == EnemyState.Skill) CheckSkill(); }
    protected override void OnDisable() { combat?.CancelCast(); base.OnDisable(); }
}

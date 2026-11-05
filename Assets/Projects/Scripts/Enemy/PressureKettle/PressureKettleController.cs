using UnityEngine;

public sealed class PressureKettleController : EnemyController
{
    [SerializeField] private SpriteData SteamJet, PressureBomb, PressureRelease;
    private PressureKettleCombat combat;
    private PressureKettleMovement movement;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => false;
    protected override void Awake()
    {
        base.Awake(); combat = GetComponent<PressureKettleCombat>(); movement = GetComponent<PressureKettleMovement>();
    }
    protected override bool ProcessSpecialState()
    {
        if (EH.Attacked || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions
            || state == EnemyState.Attacked && !SM.IsFinished) return false;
        // The 16-fps pose ends before the one-second jet. Keep this cast occupied
        // until its existing effect finishes; damage/death/control still interrupt above.
        if (state == EnemyState.Skill)
            return SM.IsFinished && combat.CurrentSkill == 0 && combat.ActiveSteam != null
                && combat.ActiveSteam.gameObject.activeSelf;
        movement.CheckIsGround();
        if (!movement.IsGround) return false;
        if (combat.ReadySkill == 2) return false;
        PlayerController target = combat.Target;
        if (target == null) { movement.Stop(); SetState(EnemyState.Idle); return true; }
        float delta = target.transform.position.x - transform.position.x;
        SM.ChangeSpriteFace(delta >= 0f);
        if (combat.InRange)
        {
            if (combat.ReadySkill >= 0) return false;
            movement.Stop(); SetState(EnemyState.Idle); return true;
        }
        if (EH.Status != null && EH.Status.BlocksMovement)
        { movement.Stop(); SetState(EnemyState.Idle); return true; }
        SetState(EnemyState.Walk);
        movement.MoveInRange(delta, Mathf.Abs(delta) < BalanceRuntime.Number(this, "PressureKettleController.ProcessSpecialState.1", 3.5f), EH.Slow);
        return true;
    }
    protected override bool SkillReady => state != EnemyState.Skill && combat.ReadySkill >= 0 && !EH.Attacked
        && !movement.IsExternallyControlled && (state != EnemyState.Attacked || SM.IsFinished);
    protected override void SkillSet() => combat.BeginCast(combat.ReadySkill);
    protected override void HandleSkill()
    {
        SM.ChangeSpriteFace(combat.FaceRight);
        SM.ChangeState(combat.CurrentSkill == 0 ? SteamJet : combat.CurrentSkill == 1 ? PressureBomb : PressureRelease);
    }
    protected override void CheckSkill()
    {
        if (state != EnemyState.Skill) return;
        if (EH.Attacked || EH.Dead || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions)
        { combat.CancelCast(); SetState(EnemyState.Idle); return; }
        combat.SampleFrame(SM.CurFrame);
    }
    protected override void SpecialAttackOver() { if (state == EnemyState.Skill) combat?.CancelCast(); }
    protected override void SkillOver() { combat.CancelCast(); movement.Stop(); SetState(EnemyState.Idle); }
    private void LateUpdate() { if (state == EnemyState.Skill) CheckSkill(); }
    protected override void OnDisable() { combat?.CancelCast(); base.OnDisable(); }
}

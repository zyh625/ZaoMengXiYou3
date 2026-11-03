using UnityEngine;

public sealed class ResonantStarOwlController : EnemyController
{
    [SerializeField] private SpriteData SoundRings, HighLowChime, ResonanceBand;
    private ResonantStarOwlCombat combat;
    private ResonantStarOwlMovement movement;
    private SpriteRenderer visual;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => false;
    protected override void Awake()
    {
        base.Awake(); combat = GetComponent<ResonantStarOwlCombat>();
        movement = GetComponent<ResonantStarOwlMovement>(); visual = SM.GetComponent<SpriteRenderer>();
    }
    protected override bool ProcessSpecialState()
    {
        movement.StepFlight();
        if (EH.Attacked || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions
            || state == EnemyState.Attacked && !SM.IsFinished) return false;
        if (state == EnemyState.Skill) return false;
        if (movement.Falling) { movement.Stop(); SetState(EnemyState.Idle); return true; }
        var target = combat.Target;
        if (target == null) { movement.Hover(0f, EH.Slow); SetState(EnemyState.Idle); return true; }
        float dx = target.transform.position.x - transform.position.x;
        SM.ChangeSpriteFace(dx >= 0f);
        if (combat.ReadySkill >= 0) return false;
        float direction = Mathf.Abs(dx) < 4f ? -Mathf.Sign(dx) : Mathf.Abs(dx) > 7f ? Mathf.Sign(dx) : 0f;
        movement.Hover(direction, EH.Slow, Mathf.Abs(dx) < 3f);
        SetState(direction == 0f ? EnemyState.Idle : EnemyState.Walk); return true;
    }
    protected override bool SkillReady => state != EnemyState.Skill && combat.ReadySkill >= 0
        && !movement.Falling && !movement.IsExternallyControlled && !EH.Attacked
        && (state != EnemyState.Attacked || SM.IsFinished);
    protected override void SkillSet() => combat.BeginCast(combat.ReadySkill);
    protected override void HandleSkill()
    {
        SM.ChangeSpriteFace(combat.FaceRight);
        SM.ChangeState(Clip); SM.Pause();
    }
    private SpriteData Clip => combat.CurrentSkill == 0 ? SoundRings : combat.CurrentSkill == 1 ? HighLowChime : ResonanceBand;
    protected override void CheckSkill()
    {
        if (state != EnemyState.Skill) return;
        if (EH.Attacked || EH.Dead || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions)
        { combat.CancelCast(); SetState(EnemyState.Idle); return; }
        movement.Hover(0f, EH.Slow);
        if (combat.CurrentSkill < 0) { SetState(EnemyState.Idle); return; }
        visual.sprite = Clip.sprites[combat.AnimationFrame];
        combat.SampleCast();
        if (combat.Finished) SkillOver();
    }
    protected override void SpecialAttackOver() { if (state == EnemyState.Skill) combat.CancelCast(); }
    protected override void SkillOver() { combat.FinishCast(); SetState(EnemyState.Idle); }
    private void LateUpdate()
    {
        if (attackedBox != null) attackedBox.enabled = true;
        if (state == EnemyState.Skill) CheckSkill();
    }
    protected override void OnDisable() { combat?.CancelCast(); base.OnDisable(); }
}

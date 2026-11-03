using UnityEngine;

public sealed class StarShearGuardController : EnemyController
{
    [SerializeField] private SpriteData OffbeatScissor, UpwardScissor, BackstepCut;
    private StarShearGuardCombat combat;
    private StarShearGuardMovement movement;
    private bool casting, gapHeld, gapVisited;
    private float resumeAt;
    public bool IsCasting => casting;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => !casting && combat.SelectRequest(movement.DeltaX);
    protected override void Awake()
    { base.Awake(); combat = GetComponent<StarShearGuardCombat>(); movement = GetComponent<StarShearGuardMovement>(); }
    protected override void OnEnable()
    { casting = gapHeld = gapVisited = false; base.OnEnable(); EH.Died += OnDied; }
    protected override void HandleAttack(int attack)
    {
        casting = true; gapHeld = gapVisited = false; combat.BeginCast();
        SM.ChangeState(attack == 0 ? OffbeatScissor : attack == 1 ? UpwardScissor : BackstepCut);
    }
    protected override bool ProcessSpecialState()
    {
        if (!casting) return false;
        if (movement.IsExternallyControlled || (EH.Status?.BlocksActions ?? false) || (EH.Attacked && !EH.Boost))
        { AttackOver(); return false; }
        AdvanceCast();
        if (casting) movement.TickStep(EH.Slow);
        return true;
    }
    private void AdvanceCast()
    {
        combat.SampleFrame(SM.CurFrame);
        if (!casting || EH.Dead) return;
        if (combat.Attack == 0)
        {
            if (combat.FirstClosed && !gapVisited)
            {
                gapVisited = gapHeld = true; resumeAt = Time.time + combat.GapHold(OffbeatScissor.fps); SM.Pause();
            }
            if (gapHeld && Time.time >= resumeAt) { gapHeld = false; SM.Resume(); }
            // Even a skipped animation frame cannot shorten the damage-free gap.
            if (!gapHeld && SM.CurFrame >= 10 && !combat.SecondOpened && Time.time < combat.GapUntil)
            { gapHeld = true; resumeAt = combat.GapUntil; SM.Pause(); }
        }
        if (SM.IsFinished && combat.WindowsClosed && Time.time >= combat.RecoveryUntil)
        { combat.FinishVisual(); AttackOver(true); movement.Stop(); SetState(EnemyState.Idle); }
    }
    private void LateUpdate()
    {
        if (casting && !EH.Dead && !movement.IsExternallyControlled && !(EH.Status?.BlocksActions ?? false)
            && !(EH.Attacked && !EH.Boost)) AdvanceCast();
    }
    protected override void SpecialAttackOver()
    {
        casting = false; if (gapHeld) SM.Resume(); gapHeld = gapVisited = false; combat?.CancelCast();
    }
    private void OnDied(DamageInfo _) => AttackOver();
    protected override void OnDisable()
    { if (EH != null) EH.Died -= OnDied; if (combat != null) AttackOver(); base.OnDisable(); }
}

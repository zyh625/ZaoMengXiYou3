using UnityEngine;

public sealed class ClampGuardController : EnemyController
{
    [SerializeField] private SpriteData AttackOne, AttackTwo, AttackThree;
    private ClampGuardCombat combat;
    private ClampGuardMovement movement;
    private bool casting, held;
    public bool IsCasting => casting;
    public bool FacingRight => !SM.GetComponent<SpriteRenderer>().flipX;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => !casting && combat.SelectRequest(movement.DeltaX);

    protected override void Awake()
    {
        base.Awake(); combat = GetComponent<ClampGuardCombat>(); movement = GetComponent<ClampGuardMovement>();
    }
    protected override void OnEnable()
    {
        casting = held = false; EH.SetSuperArmor(this, false);
        base.OnEnable(); EH.Died += Died;
    }
    protected override void HandleAttack(int attack)
    {
        casting = true; held = false; combat.BeginCast(); EH.SetSuperArmor(this, true);
        SM.ChangeSpriteFace(combat.AttackFaceRight);
        SM.ChangeState(attack == 0 ? AttackOne : attack == 1 ? AttackTwo : AttackThree);
    }
    protected override bool ProcessSpecialState()
    {
        if (!casting) return false;
        if (movement.IsExternallyControlled || (EH.Status?.BlocksActions ?? false) || EH.Attacked && !EH.Boost)
        { AttackOver(); return false; }
        movement.Stop(); AdvanceCast(); return true;
    }
    private void AdvanceCast()
    {
        combat.SampleFrame(SM.CurFrame);
        if (!casting || EH.Dead) return;
        // The 1.5-second lock outlasts the illustrated grab; hold F10, then finish recovery.
        bool holding = combat.Attack == 1 && SM.CurFrame >= 9 && combat.HasTether;
        if (holding && !held) { held = true; SM.Pause(); }
        else if (!holding && held) { held = false; SM.Resume(); }
        if (combat.WindowClosed && !combat.HasTether) EH.SetSuperArmor(this, false);
        if (SM.IsFinished && !combat.HasTether)
        { AttackOver(true); movement.Stop(); SetState(EnemyState.Idle); }
    }
    private void LateUpdate()
    {
        if (casting && !EH.Dead && !movement.IsExternallyControlled && !(EH.Status?.BlocksActions ?? false)) AdvanceCast();
    }
    protected override void SpecialAttackOver()
    {
        casting = false;
        if (held) SM.Resume();
        held = false; EH?.SetSuperArmor(this, false); combat?.CancelCast();
    }
    private void Died(DamageInfo _) => AttackOver();
    protected override void OnDisable()
    {
        if (EH != null) { EH.Died -= Died; EH.SetSuperArmor(this, false); }
        if (combat != null) AttackOver();
        casting = held = false; base.OnDisable();
    }
}

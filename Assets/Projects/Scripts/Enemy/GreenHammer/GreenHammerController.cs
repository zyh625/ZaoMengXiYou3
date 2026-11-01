using UnityEngine;

public sealed class GreenHammerController : EnemyController
{
    [SerializeField] private SpriteData HammerSweep,LeapSlam,GroundPound;
    [SerializeField] private float recoveryDuration=1.4f;
    private GreenHammerCombat combat;
    private GreenHammerMovement movement;
    private bool casting,closed,landingHeld;
    private float recoveryUntil;
    public bool IsCasting=>casting;
    public bool IsRecovering=>Time.time<recoveryUntil;
    public float RecoveryUntil=>recoveryUntil;
    protected override bool UsesAnger=>false;
    protected override bool CanStartNormalAttack=>!casting && !IsRecovering && combat.SelectRequest(movement.DeltaX);
    protected override void Awake(){base.Awake();combat=GetComponent<GreenHammerCombat>();movement=GetComponent<GreenHammerMovement>();}
    protected override void OnEnable()
    {
        casting=closed=landingHeld=false;recoveryUntil=0f;movement.ResetLeap();EH.SetSuperArmor(this,false);
        base.OnEnable();EH.Died+=Died;
    }
    protected override void HandleAttack(int attack)
    {
        casting=true;closed=landingHeld=false;EH.SetSuperArmor(this,true);combat.BeginCast();
        SM.ChangeSpriteFace(combat.AttackFaceRight);SM.ChangeState(attack==0?HammerSweep:attack==1?LeapSlam:GroundPound);
    }
    protected override bool ProcessSpecialState()
    {
        movement.RestoreGravityAfterControl();
        if(movement.IsExternallyControlled || EH.Status!=null && EH.Status.BlocksActions)
        {if(casting)AttackOver();return false;}
        if(casting)
        {
            if(EH.Attacked && !EH.Boost){AttackOver();return false;}
            if(movement.IsLeaping)movement.StepLeap();else movement.Stop();
            if(movement.LeapTimedOut){AttackOver();SetState(EnemyState.Idle);return true;}
            AdvanceCast();return true;
        }
        if(IsRecovering)
        {
            if(EH.Attacked || state==EnemyState.Attacked && !SM.IsFinished)return false;
            movement.Stop();SetState(EnemyState.Idle);return true;
        }
        return false;
    }
    private void AdvanceCast()
    {
        if(!casting)return;
        if(combat.Attack==1 && combat.LeapLaunched && !movement.HasLanded && SM.CurFrame>=9){landingHeld=true;SM.Pause();}
        if(landingHeld && movement.HasLanded){landingHeld=false;SM.Resume();}
        combat.SampleFrame(SM.CurFrame);
        if(!casting || EH.Dead)return;
        if(!closed && combat.WindowClosed)
        {closed=true;EH.SetSuperArmor(this,false);recoveryUntil=Time.time+recoveryDuration;}
        if(SM.IsFinished && !movement.IsLeaping && !combat.HasPendingSegments)
        {AttackOver(true);movement.Stop();SetState(EnemyState.Idle);}
    }
    private void LateUpdate()
    {
        movement.RestoreGravityAfterControl();
        if(casting && !EH.Dead && !movement.IsExternallyControlled && !(EH.Status?.BlocksActions??false))AdvanceCast();
    }
    protected override void SpecialAttackOver()
    {
        EH?.SetSuperArmor(this,false);
        if(casting && !closed)recoveryUntil=Time.time+recoveryDuration;
        casting=landingHeld=false;combat?.CancelCast();
    }
    private void Died(DamageInfo info)=>AttackOver();
    protected override void OnDisable()
    {
        if(EH!=null){EH.Died-=Died;EH.SetSuperArmor(this,false);}if(combat!=null)AttackOver();
        casting=closed=landingHeld=false;recoveryUntil=0f;movement?.RestoreGravityAfterControl();base.OnDisable();
    }
}

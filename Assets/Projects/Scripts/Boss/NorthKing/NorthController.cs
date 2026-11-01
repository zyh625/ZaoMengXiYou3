using UnityEngine;

public sealed class NorthController : BossController
{
    [SerializeField] private SpriteData UmbrellaBash,UmbrellaVortex,RibVolley,RainCurtain,Guard,VortexSpin,VortexRecovery;
    private NorthCombat combat;
    private NorthMovement movement;
    private NorthCombat.GuardPhase shown;
    private int pending;
    private bool spinning,recovering,landingHold;
    protected override bool CanStartNormalAttack=>false;
    protected override void Awake(){base.Awake();combat=GetComponent<NorthCombat>();movement=GetComponent<NorthMovement>();}
    protected override bool ProcessSpecialState()
    {
        if(combat.IsSpecial)
        {
            if(EH.Status!=null && EH.Status.BlocksActions || movement.IsExternallyControlled)return false;
            movement.Stop();EH.AttackedEnd();SM.ChangeSpriteFace(combat.GuardFaceRight);
            if(state!=EnemyState.Skill || shown!=combat.Phase){SetState(EnemyState.Idle);SetState(EnemyState.Skill);shown=combat.Phase;}
            return true;
        }
        if(shown!=NorthCombat.GuardPhase.Normal){shown=NorthCombat.GuardPhase.Normal;SetState(EnemyState.Idle);}
        if(state==EnemyState.Skill && !EH.Attacked && !movement.IsExternallyControlled && !(EH.Status!=null && EH.Status.BlocksActions))
        {movement.Stop();CheckSkill();if(combat.Channeling || movement.IsRainJumping)return true;}
        return false;
    }
    protected override bool SkillReady=>state!=EnemyState.Skill && combat.ReadyAttack>=0 && !EH.Attacked
        && (state!=EnemyState.Attacked || SM.IsFinished) && movement.IsOnGroundNow();
    protected override void SkillSet()=>pending=combat.ReadyAttack;
    protected override void HandleSkill()
    {
        if(combat.IsSpecial)
        {SM.ChangeSpriteFace(combat.GuardFaceRight);if(combat.IsGuarding)SM.ChangeState(Guard);else SetIdlePose();return;}
        if(!combat.BeginAttack(pending)){SetState(EnemyState.Idle);return;}
        spinning=recovering=landingHold=false;SM.ChangeSpriteFace(combat.AttackFaceRight);
        SM.ChangeState(pending==0?UmbrellaBash:pending==1?UmbrellaVortex:pending==2?RibVolley:RainCurtain);
    }
    private void SetIdlePose()
    {
        //破伞明确回到基类Idle动画；Skill状态仅占用三秒硬直，禁止走动和再次起手。
        SetState(EnemyState.Idle);state=EnemyState.Skill;
    }
    protected override void CheckSkill()
    {
        if(combat.IsSpecial || state!=EnemyState.Skill)return;
        if(EH.Dead || EH.Attacked || movement.IsExternallyControlled || EH.Status!=null && EH.Status.BlocksActions)
        {combat.CancelAttack();if(!EH.Dead)SetState(EnemyState.Idle);return;}
        combat.SampleFrame(SM.CurFrame);
        if(combat.CurrentSkill==3)
        {
            if(movement.IsRainJumping && SM.CurFrame>=11 && !landingHold){landingHold=true;SM.Pause();}
            else if(landingHold && !movement.IsRainJumping){landingHold=false;SM.Resume();}
        }
        if(combat.CurrentSkill!=1)return;
        if(combat.Channeling && !spinning && SM.CurFrame>=12){spinning=true;SM.ChangeState(VortexSpin);}
        else if(spinning && !combat.Channeling && !recovering){recovering=true;SM.ChangeState(VortexRecovery);}
    }
    protected override void SpecialAttackOver()=>combat?.CancelAttack();
    protected override void SkillOver(){combat.CancelAttack();SetState(EnemyState.Idle);}
    private void LateUpdate(){if(state==EnemyState.Skill && !EH.Dead)CheckSkill();}
    protected override void OnDisable(){combat?.CancelAttack();spinning=recovering=landingHold=false;shown=NorthCombat.GuardPhase.Normal;base.OnDisable();}
}

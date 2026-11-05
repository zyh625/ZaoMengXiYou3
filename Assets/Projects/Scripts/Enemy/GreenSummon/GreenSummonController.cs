using UnityEngine;

public sealed class GreenSummonController : EnemyController
{
    [SerializeField] private SpriteData SummonedHammerStrike,BannerWave,WarBanner;
    private GreenSummonCombat combat;
    private GreenSummonMovement movement;
    protected override bool UsesAnger=>false;
    protected override bool CanStartNormalAttack=>false;
    protected override void Awake(){base.Awake();combat=GetComponent<GreenSummonCombat>();movement=GetComponent<GreenSummonMovement>();}
    protected override bool ProcessSpecialState()
    {
        if(EH.Attacked || movement.IsExternallyControlled || EH.Status!=null && EH.Status.BlocksActions)
        {if(movement.IsRetreating)movement.CancelRetreat();return false;}
        if(state==EnemyState.Attacked && !SM.IsFinished)return false;
        PlayerController target=combat.Target;
        if(target==null){combat.CancelCast();movement.CancelRetreat();movement.Stop();SetState(EnemyState.Idle);return true;}
        float delta=target.transform.position.x-transform.position.x;
        if(movement.IsRetreating)
        {
            if(EH.Status!=null && EH.Status.BlocksMovement)movement.CancelRetreat();else movement.StepRetreat();
            SM.ChangeSpriteFace(delta>0f);SetState(movement.IsRetreating?EnemyState.Walk:EnemyState.Idle);return true;
        }
        movement.CheckIsGround();if(!movement.IsGround)return false;
        if(Mathf.Abs(delta)<BalanceRuntime.Number(this, "GreenSummonController.ProcessSpecialState.1", 3f) && movement.TryRetreat(delta))
        {combat.CancelCast();SM.ChangeSpriteFace(delta>0f);SetState(EnemyState.Walk);return true;}
        if(state==EnemyState.Skill)return false;
        SM.ChangeSpriteFace(delta>0f);
        if((Mathf.Abs(delta) >= BalanceRuntime.Number(this, "GreenSummonController.ProcessSpecialState.Range1.Minimum", 4f) && Mathf.Abs(delta) <= BalanceRuntime.Number(this, "GreenSummonController.ProcessSpecialState.Range1.Maximum", 7f)))
        {if(combat.ReadySkill>=0)return false;movement.Stop();SetState(EnemyState.Idle);return true;}
        if(EH.Status!=null && EH.Status.BlocksMovement){movement.Stop();SetState(EnemyState.Idle);return true;}
        SetState(EnemyState.Walk);movement.MoveInRange(delta,Mathf.Abs(delta)<BalanceRuntime.Number(this, "GreenSummonController.ProcessSpecialState.2", 4f),EH.Slow);return true;
    }
    protected override bool SkillReady=>state!=EnemyState.Skill && combat!=null && combat.ReadySkill>=0 && combat.InRange
        && !movement.IsRetreating && !EH.Attacked && (state!=EnemyState.Attacked || SM.IsFinished);
    protected override void SkillSet()=>combat.BeginCast(combat.ReadySkill);
    protected override void HandleSkill()
    {SM.ChangeSpriteFace(combat.FaceRight);SM.ChangeState(combat.CurrentSkill==0?SummonedHammerStrike:combat.CurrentSkill==1?BannerWave:WarBanner);}
    protected override void CheckSkill()
    {
        if(state!=EnemyState.Skill)return;
        if(EH.Dead || EH.Attacked || movement.IsExternallyControlled || EH.Status!=null && EH.Status.BlocksActions)
        {combat.CancelCast();if(!EH.Dead)SetState(EnemyState.Idle);return;}
        combat.SampleFrame(SM.CurFrame);
    }
    protected override void SpecialAttackOver(){if(state==EnemyState.Skill)combat?.CancelCast();}
    protected override void SkillOver(){combat.CancelCast();movement.Stop();SetState(EnemyState.Idle);}
    private void LateUpdate(){if(state==EnemyState.Skill && !EH.Dead)CheckSkill();}
    protected override void OnDisable(){combat?.CancelCast();movement?.CancelRetreat();base.OnDisable();}
}

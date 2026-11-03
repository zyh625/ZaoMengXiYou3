using UnityEngine;

public class LateStageController : EnemyController
{
    [SerializeField] private SpriteData[] skillAnimations;
    protected LateStageCombat combat;
    private LateStageMovement move;private int pending,phaseVersion=-1;
    private bool showingSpecial;
    protected override bool UsesAnger=>false;
    protected override bool CanStartNormalAttack=>false;
    protected override void Awake(){base.Awake();combat=GetComponent<LateStageCombat>();move=GetComponent<LateStageMovement>();}
    protected override bool ProcessSpecialState()
    {
        move.Prepare();var target=combat.Target;if(target!=null)move.SetTarget(target.transform);
        if(combat.SpecialActive)
        {
            if(move.IsExternallyControlled)move.ProcessExternalControl();
            move.Stop();
            if(!showingSpecial||phaseVersion!=combat.SpecialVersion)
            {showingSpecial=true;phaseVersion=combat.SpecialVersion;SM.Resume();SetState(EnemyState.Idle);
                if(combat.SpecialClip>=0&&combat.SpecialClip<skillAnimations.Length)SM.ChangeState(skillAnimations[combat.SpecialClip]);}
            if(combat.Frozen)SM.Pause();else SM.Resume();
            return true;
        }
        if(showingSpecial){showingSpecial=false;SM.Resume();SetState(EnemyState.Idle);}
        if(combat.Recovering && !EH.Attacked && (state!=EnemyState.Attacked||SM.IsFinished)){move.Stop();SetState(EnemyState.Idle);return true;}
        if(move.Hovering && combat.Skill<0 && !combat.Frozen && !EH.Attacked && (state!=EnemyState.Attacked||SM.IsFinished))
        {
            if(combat.ReadySkill>=0)return false;
            if(target==null)return true;
            float d=target.transform.position.x-transform.position.x;
            float direction=Mathf.Abs(d)>7?Mathf.Sign(d):Mathf.Abs(d)<4?-Mathf.Sign(d):0;
            move.Chase(direction,EH.Slow);SM.ChangeSpriteFace(d>0);SetState(direction==0?EnemyState.Idle:EnemyState.Walk);return true;
        }
        return false;
    }
    protected override bool SkillReady=>state!=EnemyState.Skill && !combat.Frozen && (!EH.Attacked||EH.Boost)
        && (state!=EnemyState.Attacked||SM.IsFinished) && combat.ReadySkill>=0;
    protected override void SkillSet()=>pending=combat.ReadySkill;
    protected override void HandleSkill()
    {if(!combat.BeginSkill(pending)){SetState(EnemyState.Idle);return;}SM.ChangeSpriteFace(combat.AttackFaceRight);SM.ChangeState(skillAnimations[pending]);}
    protected override void CheckSkill()
    {
        if(state!=EnemyState.Skill)return;
        if(combat.Frozen||EH.Attacked&&!EH.Boost){combat.CancelSkill();SM.Resume();SetState(EnemyState.Idle);return;}
        combat.SampleFrame(SM.CurFrame+1);
        if(combat.HoldFrame(SM.CurFrame+1))SM.Pause();else SM.Resume();
    }
    protected override void SkillOver()
    {if(combat.HoldFrame(16))return;combat.FinishSkill();SM.Resume();SetState(EnemyState.Idle);}
    protected override void SpecialAttackOver()=>combat?.CancelSkill();
    private void LateUpdate(){if(!EH.Dead&&!TrainingPassive)CheckSkill();}
    protected override void OnDisable(){combat?.CancelSkill();showingSpecial=false;phaseVersion=-1;base.OnDisable();}
}

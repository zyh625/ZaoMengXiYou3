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
        //技能动画已离开 Skill 状态但技能本身没结束（例如换位后的收招硬直把状态改成了待机）：立即结算，否则移动会一直被锁住而原地踏步
        if(state!=EnemyState.Skill && combat.Skill>=0)combat.FinishSkill();
        //动画比保持窗口短、已经播完：停在最后一帧等保持时间结束，再交给通用流程收招，不能先进入行走
        if(state==EnemyState.Skill && combat.Skill>=0 && SM.IsFinished && !combat.Frozen && !(EH.Attacked&&!EH.Boost) && combat.HoldFrame(16))
        {move.Stop();combat.SampleFrame(16);return true;}
        //收招硬直只在技能结束后生效；技能进行中的硬直（换位落地）由技能自己的保持时间处理
        if(combat.Recovering && combat.Skill<0 && !EH.Attacked && (state!=EnemyState.Attacked||SM.IsFinished)){move.Stop();SetState(EnemyState.Idle);return true;}
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

using UnityEngine;

public sealed class WestController : BossController
{
    [SerializeField] private SpriteData DragonClaw,PearlToss,DragonBreath,PearlOrbit,Ward,HitReaction;
    private WestCombat combat;
    private WestMovement movement;
    private WestCombat.WardPhase shownPhase;
    private int pending;
    private float resumeAt;
    private bool waiting;
    protected override bool UsesAnger=>false;
    protected override bool CanStartNormalAttack=>false;
    protected override void Awake(){base.Awake();combat=GetComponent<WestCombat>();movement=GetComponent<WestMovement>();}
    //本体大小只由预制体 Visual 的缩放和各动作图片的 PPU 决定，运行时不再按动作重新缩放。
    protected override bool ProcessSpecialState()
    {
        if(combat.IsSpecial)
        {
            if(EH.Status!=null && EH.Status.BlocksActions || movement.IsExternallyControlled)return false;
            if(!combat.IsWarding && EH.Attacked)return false;
            movement.Stop();
            if(state!=EnemyState.Skill || shownPhase!=combat.Phase)
            {SetState(EnemyState.Idle);SetState(EnemyState.Skill);shownPhase=combat.Phase;}
            return true; //Ward动画播完保持末帧，三秒易伤期间同样不交回普通出招。
        }
        shownPhase=WestCombat.WardPhase.Normal;
        if(state==EnemyState.Skill && combat.Channeling){movement.Stop();combat.SampleFrame(SM.CurFrame);return true;}
        return false;
    }
    protected override bool SkillReady=>state!=EnemyState.Skill && combat.ReadyAttack>=0
        && (state!=EnemyState.Attacked || SM.IsFinished) && movement.IsOnGroundNow();
    protected override void SkillSet()=>pending=combat.ReadyAttack;
    protected override void HandleSkill()
    {
        if(combat.IsSpecial)
        {SpriteData pose=combat.IsWarding?Ward:HitReaction;SM.ChangeState(pose);return;}
        if(!combat.BeginAttack(pending)){SetState(EnemyState.Idle);return;}
        SpriteData clip=pending==0?DragonClaw:pending==1?PearlToss:pending==2?DragonBreath:PearlOrbit;
        SM.ChangeSpriteFace(combat.AttackFaceRight);SM.ChangeState(clip);
        float hold=Mathf.Max(0f,combat.Windup-combat.ReleaseFrame/clip.fps);resumeAt=Time.time+hold;waiting=hold>0f;if(waiting)SM.Pause();
    }
    protected override void CheckSkill()
    {
        if(combat.IsSpecial)return;
        if(state==EnemyState.Skill && (EH.Attacked || movement.IsExternallyControlled || EH.Status!=null && EH.Status.BlocksActions))
        {combat.CancelAttack();SetState(EnemyState.Idle);return;}
        if(state!=EnemyState.Skill)return;
        if(waiting){if(Time.time<resumeAt)return;waiting=false;SM.Resume();}
        combat.SampleFrame(SM.CurFrame);
    }
    protected override void SpecialAttackOver()=>combat?.CancelAttack();
    protected override void SkillOver(){combat.CancelAttack();SetState(EnemyState.Idle);}
    private void LateUpdate(){if(state==EnemyState.Skill)CheckSkill();}
    protected override void OnDisable(){combat?.CancelAttack();waiting=false;shownPhase=WestCombat.WardPhase.Normal;base.OnDisable();}
}

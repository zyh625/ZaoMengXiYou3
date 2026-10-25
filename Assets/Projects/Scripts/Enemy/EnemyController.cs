using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField]
    private SpriteData Idle, Walk,Attacked, Die;
    protected SpriteManager SM;
    private EnemyMovement EM;
    protected Health EH;
    protected EnemyCombat EC;
    [SerializeField] protected EnemyState state;
    [SerializeField] private float stopDistance;
    [SerializeField] protected BoxCollider2D attackedBox;
    [SerializeField] private int specialAttack;
    public float enemyHalf => GetComponent<BoxCollider2D>().bounds.size.x / 2;
    protected virtual void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>(true);
        EH = GetComponent<Health>();
        EM = GetComponent<EnemyMovement>();
        EC = GetComponent<EnemyCombat>();
    }
    protected virtual void OnEnable()
    {
        SetState(EnemyState.Idle);
        SM.ChangeState(Idle);
    }
    protected virtual void SpecialAttackOver() { }//特殊攻击结束
    protected virtual void CheckSpecialAttack() { }//检查当前是否为特殊攻击
    protected virtual void SpecialAttackSet(float dis) { }//特殊攻击设置
    protected virtual bool SkillBoost=> false;//增益型技能，不能与愤怒叠加
    protected virtual void SkillSet() { }//技能设置
    protected virtual void CheckSkillBoostLose() { }//检查当前增益型技能是否失效
    protected virtual void CheckSkill() { }//检查当前技能阶段对应技能效果
    protected virtual bool SkillReady => false;//当前是否可以释放技能
    private void FixedUpdate()
    {
        if (DieProcessing()) return;
        if (EH.Dead)
        {
            EM.Stop(); AttackOver();
            SetState(EnemyState.Die);
            return;
        }
        if (EC.AngryRequest&&!SkillBoost)
            ObtainAngry();
        if (EC.Angry && EC.AngryEnd)
            LoseAngry();
        CheckSkill();
        if (SkillReady)
        {
            SkillSet(); AttackOver();EM.Stop();
            SetState(EnemyState.Skill);
            return;
        }
        if (SkillProcessing())
        {
            CheckSkill();//技能效果
            return;
        }
        CheckSkillBoostLose();
        if (!EH.Boost && EH.Attacked)//当前无免控且受击
        {
            EH.AttackedEnd(); AttackOver();
            EC.UpdateAngry();
            EM.AttackedMove(EH.AttackedRight, EH.AttackedUp);
            SetState(EnemyState.Attacked);
            return;
        }
        if (AttackedProcessing()) return;
        EM.CheckIsGround();
        if (EM.AirState() != 0)//空中什么也做不了
        {
            AttackOver();
            SetState(EnemyState.Idle);
            return;
        }
        if (AttackProcessing())//攻击期间
        {
            if (EC.AttackOpen(SM.CurFrame)) EC.AttackBegin();
            if (EC.AttackShut(SM.CurFrame)) EC.AttackEnd();
            if (EC.Attack == specialAttack) CheckSpecialAttack();//特殊攻击，无敌帧等
            return;
        }
        float dis = EM.DeltaX;
        EC.CheckAttack();
        if (EC.AttackReady())//满足攻击条件
        {
            EC.HasAtatck(dis > 0f); EM.Stop(); SpecialAttackSet(dis);
            SM.ChangeSpriteFace(dis > 0f);
            SetState(EnemyState.Attacking);
            return;
        }
        SM.ChangeSpriteFace(dis > 0f);
        EC.ChangeCheckBox(dis > 0f);
        if (Mathf.Abs(dis) <= stopDistance)//靠近玩家停止
        {
            EM.Stop();
            SetState(EnemyState.Idle);
            return;
        }
        SetState(EnemyState.Walk);
        EM.Move(EH.Slow);
    }
    protected virtual void HandleAttack(int attack) { }
    protected virtual void HandleSkill() { }
    private void SetState(EnemyState next)
    {
        if (next == state && state!=EnemyState.Attacking
            && state != EnemyState.Attacked) return;
        state = next;
        switch (state)
        {
            case EnemyState.Idle: SM.ChangeState(Idle); break;
            case EnemyState.Walk: SM.ChangeState(Walk); break;
            case EnemyState.Attacking:HandleAttack(EC.Attack);break;
            case EnemyState.Attacked: SM.ChangeState(Attacked); break;
            case EnemyState.Die: SM.ChangeState(Die); break;
            case EnemyState.Skill:HandleSkill();break;
        }
    }
    private bool DieProcessing()//死亡过程中
    {
        if (state != EnemyState.Die) return false;
        if (SM.IsFinished) gameObject.SetActive(false);
        return true;
    }
    private bool AttackedProcessing()//受击过程
    {
        if (EH.Boost || state != EnemyState.Attacked) return false;
        if (SM.IsFinished)
        {
            EM.Stop();
            return false;
        }
        return true;
    }
    protected virtual void SpecialOver() { }
    private bool AttackProcessing()//攻击过程
    {
        if (EC.Attack == -1) return false;
        if (SM.IsFinished)
        {
            AttackOver();
            SpecialOver();
            return false;
        }
        return true;
    }
    protected void AttackOver()//攻击结束
    {
        EC.AttackEnd();
        EC.AttackOver();
        SpecialAttackOver();
    }
    private void ObtainAngry()//愤怒增益
    {
        EC.AngryRequest = false;
        EC.Boost();
        SM.SetRed();
        SM.SetAngry(true);
        EM.Boost();
    }
    private void LoseAngry()//愤怒结束
    {
        EC.AngryRequest = false;//愤怒过程积累怒气无效
        EC.BoostOver();
        SM.SetAngry(false);
        EM.ReturnSpeed();
    }
    private bool SkillProcessing()
    {
        if(state!=EnemyState.Skill) return false;
        if (SM.IsFinished)
        {
            SkillOver();
            return false;
        }
        return true;
    }
    protected virtual void SkillOver() { }
}

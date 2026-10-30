using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField]
    private SpriteData Idle, Walk,Attacked, Die;
    protected SpriteManager SM;
    private EnemyMovement EM;
    private ActorStatus status;//固体冰冻禁用行为，融化阶段只继续攻击而不移动
    protected Health EH;
    protected EnemyCombat EC;
    [SerializeField] protected EnemyState state;
    [SerializeField] private float stopDistance;
    [SerializeField] protected BoxCollider2D attackedBox;
    public bool CanReceiveHit => attackedBox == null || (attackedBox.enabled && attackedBox.gameObject.activeInHierarchy);//手动技能检测同样遵守旧受击箱的无敌开关
    [SerializeField] private int specialAttack;
    [SerializeField, Min(0)] private int stunnedFrame;//眩晕与携带固定显示的受击帧，默认受击动画第一帧
    private bool controlPoseHeld;//同一段控制只打断并切换一次动画，防止每帧重置受击
    public float enemyHalf => GetComponent<BoxCollider2D>().bounds.size.x / 2;
    protected virtual void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>(true);
        EH = GetComponent<Health>();
        status = EH != null ? EH.Status : GetComponent<ActorStatus>();
        EM = GetComponent<EnemyMovement>();
        EC = GetComponent<EnemyCombat>();
        EnemyHealthBar.EnsureFor(this);//小怪自动复用受伤后显示、淡出的血条；Boss保持自己的专属界面。
    }
    protected virtual void OnEnable()
    {
        controlPoseHeld = false;
        SetState(EnemyState.Idle);
        SM.ChangeState(ResolveStateAnimation(EnemyState.Idle, Idle));
        if (status != null)
        {
            status.Changed += OnStatusChanged;
            OnStatusChanged();
        }
    }
    protected virtual void OnDisable()//对象池复用时不重复接收上一轮生命的状态通知
    {
        if (status != null) status.Changed -= OnStatusChanged;
    }
    public virtual void SetSpawnSide(bool rightSide)//从右侧出生时朝左，从左侧出生时朝右；特殊敌人可重写
    {
        SM.ChangeSpriteFace(!rightSide);
        EC.ChangeCheckBox(rightSide);
    }
    protected virtual void SpecialAttackOver() { }//特殊攻击结束
    protected virtual void CheckSpecialAttack() { }//检查当前是否为特殊攻击
    protected virtual void SpecialAttackSet(float dis) { }//特殊攻击设置
    protected virtual bool SkillBoost=> false;//增益型技能，不能与愤怒叠加
    protected virtual bool UsesAnger => true;//小怪保留通用愤怒；Boss由专属控制器选择自己的特殊状态
    protected virtual void SkillSet() { }//技能设置
    protected virtual void CheckSkillBoostLose() { }//检查当前增益型技能是否失效
    protected virtual void CheckSkill() { }//检查当前技能阶段对应技能效果
    protected virtual bool SkillReady => false;//当前是否可以释放技能
    protected virtual bool ProcessSpecialState() => false;//弹反等专属动作在死亡之后、冰冻与普通行为之前取得控制
    private void FixedUpdate()
    {
        EM.BeginMotionStep();//剥离上个物理步的流沙拉力，自身行为仍按原有优先级执行
        CheckStatePriority();
        EM.EndMotionStep();//统一补充当前领域的固定拉速，避免提前返回漏掉领域效果
    }
    private void CheckStatePriority()//保留原有判定顺序，移动附加效果不挤入每个分支
    {
        if (DieProcessing()) return;
        if (EH.Dead)
        {
            EM.ClearExternalControl();//死亡仍优先于吸附，立即恢复原重力。
            EM.Stop(); AttackOver();
            SetState(EnemyState.Die);
            return;
        }
        if (ProcessSpecialState()) return;
        if (status != null && status.BlocksActions)
        {
            HoldControlPose();
            EH.AttackedEnd();
            status.EnforcePosition();
            return;//冻结期间不进入怒气、技能、普攻或移动判定
        }
        if (EM.ProcessExternalControl())
        {
            EH.AttackedEnd();//保留伤害结算，不积压释放后的击退请求。
            return;
        }
        if (UsesAnger && EC.AngryRequest&&!SkillBoost)
            ObtainAngry();
        if (UsesAnger && EC.Angry && EC.AngryEnd)
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
            bool back = EH.AttackedBack, up = EH.AttackedUp;//两个方向分别保存，清除请求后不丢失本次命中效果
            EH.AttackedEnd(); AttackOver();
            if (UsesAnger) EC.UpdateAngry();
            EM.AttackedMove(EH.AttackedRight, back, up);//反伤两个方向均为false，只停水平移动并播放受击动画
            SetState(EnemyState.Attacked);
            return;
        }
        if (AttackedProcessing()) return;
        EM.CheckIsGround();
        if (EM.AirState() != 0 && !(status != null && status.IsMelting))//融化锁在空中时仍允许攻击，其他空中情形保持原规则
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
        if (status != null && status.BlocksMovement)
        {
            EM.Stop();
            SetState(EnemyState.Idle);
            return;//没有攻击机会时保持站立，不用走路动画在冰块里原地踏步
        }
        SetState(EnemyState.Walk);
        EM.Move(EH.Slow);
    }
    protected virtual void HandleAttack(int attack) { }
    protected virtual SpriteData ResolveStateAnimation(EnemyState next, SpriteData fallback) => fallback;//同一行为状态可按敌人的持盾等形态选择动画，不改变共用判定顺序。
    protected virtual void HandleSkill() { }
    //吸附首次接管时结束攻击与特殊冲刺，防止攻击箱在受控期间继续生效。
    public void InterruptForSuction()
    {
        AttackOver();
        EH.AttackedEnd();
        SetState(EnemyState.Idle);
    }
    //第一次冻结先关闭攻击，再定格受击姿势；后续命中只扣血，控制结束前不切换动画。
    public void HoldControlPose()
    {
        if (EH.Dead) return;
        if (controlPoseHeld)
        {
            SM.Pause();//冰块阶段变化不能意外解除另一来源眩晕或携带的暂停
            return;
        }
        controlPoseHeld = true;
        AttackOver();
        EH.AttackedEnd();
        SetState(EnemyState.Attacked);
        if (Attacked != null && Attacked.sprites != null && Attacked.sprites.Length > 0)
        {
            SpriteRenderer renderer = SM.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = Attacked.sprites[Mathf.Clamp(stunnedFrame, 0, Attacked.sprites.Length - 1)];
        }
        SM.Pause();
    }
    //最后一个冻结来源结束时恢复播放器，死亡则紧接着由死亡分支切换死亡帧动画。
    public void ReleaseControlPose()
    {
        if (!EH.Dead && ((status != null && status.BlocksActions) || EM.IsStunned || EM.IsCarried))
        {
            HoldControlPose();
            return;
        }
        if (!controlPoseHeld) return;
        controlPoseHeld = false;
        SM.Resume();
        if (!EH.Dead) SetState(EnemyState.Idle);
    }
    private void OnStatusChanged()//收到冻结当帧就关闭攻击箱和特殊冲刺，不能等下一轮FixedUpdate
    {
        if (EH.Dead) return;
        if (status != null && status.BlocksActions)
        {
            HoldControlPose();
            EC.ClearAttackRequests();
            EC.AngryRequest = false;
            EM.Stop();
            status.EnforcePosition();
        }
        else ReleaseControlPose();
    }
    protected void SetState(EnemyState next)
    {
        if (next == state && state!=EnemyState.Attacking
            && state != EnemyState.Attacked) return;
        state = next;
        switch (state)
        {
            case EnemyState.Idle: SM.ChangeState(ResolveStateAnimation(state, Idle)); break;
            case EnemyState.Walk: SM.ChangeState(ResolveStateAnimation(state, Walk)); break;
            case EnemyState.Attacking:HandleAttack(EC.Attack);break;
            case EnemyState.Attacked: SM.ChangeState(ResolveStateAnimation(state, Attacked)); break;
            case EnemyState.Die: SM.ChangeState(ResolveStateAnimation(state, Die)); break;
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
            AttackOver(true);
            SpecialOver();
            return false;
        }
        return true;
    }
    protected void AttackOver(bool completed = false)//完整出招才结算躲开，受击和控制打断不算未命中
    {
        EC.AttackEnd();
        EC.AttackOver(completed);
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
            return state == EnemyState.Skill && !SM.IsFinished;//子类续接新的收回等技能阶段时保留本帧占用，避免立刻被行走覆盖。
        }
        return true;
    }
    protected virtual void SkillOver() { }
}

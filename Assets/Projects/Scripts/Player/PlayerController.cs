using UnityEngine;
using zmxy.GamePlay;
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("动作帧素材")]
    [SerializeField] private SpriteData Idle;
    [SerializeField] private SpriteData Walk;
    [SerializeField] private SpriteData Run;
    [SerializeField] private SpriteData JumpOne;
    [SerializeField] private SpriteData JumpTwo;
    [SerializeField] private SpriteData Attacked;
    [SerializeField] private SpriteData Die;
    [SerializeField] private SpriteData Drop;
    [SerializeField] private SpriteData Rise;
    [SerializeField] private SpriteData Land;
    [SerializeField] private SpriteData RiseAttack;
    [Header("引用其他组件")]
    protected SpriteManager SM;
    protected InputReader IR;
    private PlayerMovement PM;
    private PlayerCombat PC;
    protected PlayerInteract PI;
    private Health PH;
    private CameraManager CM;
    private PlayerUI UI;
    [SerializeField] protected BoxCollider2D checkAttacked;
    private float deltaHalf;//人物相对于相机中心的最大偏移距离
    public float DeltaHalf => deltaHalf;
    
    [SerializeField]protected PlayerState state;//当前状态,初始为待机
    private int stateAction = -1;//记录合并状态内的具体招式编号，区分下一招、二段跳和重复进入同一动作
    public PlayerState State() => state;
    public bool PressedInteract => PI.IsInteracting();
    protected virtual void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>();
        IR = GetComponent<InputReader>();
        PM= GetComponent<PlayerMovement>();
        PC = GetComponent<PlayerCombat>();
        PI= GetComponent<PlayerInteract>();
        PH = GetComponent<Health>();
        UI = GetComponent<PlayerUI>();
        CM = Camera.main.GetComponent<CameraManager>();
        deltaHalf = CM.CameraHalf - GetComponent<BoxCollider2D>().bounds.size.x / 2;
    }
    private void Start()
    {
        state = PlayerState.Idle;
        SM.ChangeState(Idle);
    }
    private void OnEnable()
    {
        IR.JumpPressed += PM.OnJump;
        IR.MovePressed += PM.OnMove;
        IR.AttackPressed += PC.OnAttack;
        IR.InteractPressed += PI.OnInteract;
        IR.SkillOnePressed += PC.OnSkill;
        IR.SkillTwoPressed += PC.OnSkill;
        IR.SkillThreePressed += PC.OnSkill;
        IR.QuickDownPressed += PM.OnQuickDown;
    }
    private void OnDisable()
    {
        IR.JumpPressed -= PM.OnJump;
        IR.MovePressed -= PM.OnMove;
        IR.AttackPressed -= PC.OnAttack;
        IR.InteractPressed -= PI.OnInteract;
        IR.SkillOnePressed -= PC.OnSkill;
        IR.SkillTwoPressed -= PC.OnSkill;
        IR.SkillThreePressed -= PC.OnSkill;
        IR.QuickDownPressed -= PM.OnQuickDown;
    }
    protected virtual void UpdateMovement() => PM.CheckNormalHorizonMove();
    protected virtual void StartSkill() { }
    protected virtual void SpecialSkill() { }
    //默认使用当前朝向；有施法朝向锁定的角色可改用施法开始时的方向。
    protected virtual bool SkillFaceRight => PM.FaceRight;
    //在技能阶段更新、攻击箱开关之后处理角色自己的无敌帧。
    protected virtual void CheckImmune() { }
    protected virtual bool TryComboAttack() { return false; }
    protected void FixedUpdate()//检查状态按优先级,高不可被低打断，所有角色共用这一条执行顺序
    {
        if (DieProcessing()) return;
        if (PH.Dead||PH.Attacked)//可连续受击
        {
            AttackOver(); ComboOver(); RiseOver();SkillOver(); PM.AllowJump(false);//打断所有
            PH.AttackedEnd();
            if (PH.Dead) PM.Stop();
            else
            {
                UI.UpdateHP(PH.CurBlood, PH.BeginBlood);
                PM.AttackedMove(PH.AttackedRight);
            }
            SetState(PH.Dead ? PlayerState.Die : PlayerState.Attacked);
            return;
        }
        CM.UpdateCamera(PM.bodyX - CM.CameraCenter);
        PM.LimitedRange(CM.CameraCenter - deltaHalf, CM.CameraCenter + deltaHalf);
        if (AttackedProcessing()) return;
        if (PC.Skilled!=-1&& !IsSkilling)
        {
            StartSkill();
            AttackOver(); ComboOver();RiseOver();PM.AllowJump(false);//打断普攻、组合技和空中攻击,不响应跳跃
            SetState(PlayerState.Skill);
            return;
        }
        PM.CheckIsGround();
        PM.CheckQuickDown();
        UpdateMovement();
        if (SkillProcessing())
        {
            if (PC.SkillOpen(SM.CurFrame, PC.Skilled)) PC.OpenSkillBox(SkillFaceRight);
            if (PC.SkillShut(SM.CurFrame, PC.Skilled)) PC.ShutSkillBox();
            CheckImmune();
            return;
        }
        SM.ChangeSpriteFace(PM.FaceRight);
        PM.CheckJump();
        int airState = PM.AirState();
        if (PM.JumpedThisFrame)//跳跃，优先级中上
        {
            PM.JumpedThisFrame = false;
            AttackOver(); ComboOver(); RiseOver();//打断普攻、组合技和空中攻击
            SetState(PlayerState.Jump);
            return;
        }
        if (JumpProcessing) return;
        if (RiseAttackProcessing())
        {
            if (PC.RiseOpen(SM.CurFrame)) PC.OpenRiseBox(PM.FaceRight);
            if (PC.RiseShut(SM.CurFrame)) PC.ShutRiseBox();
            return;
        }
        if (airState!=0)//下落或上升过程,优先级中等,空中可攻击
        {
            AttackOver(); ComboOver();//打断普攻和组合技
            PC.CheckAttack();
            if (PC.AttackThisFrame) SetState(PlayerState.RiseAttack);
            else SetState(airState > 0 ? PlayerState.Rise : PlayerState.Drop);
            return;
        }
        if(PM.LandedThisFrame)//落地前摇，不响应攻击
        {
            AttackOver();ComboOver();//打断普攻和组合技
            SetState(!PM.IsMoving ? PlayerState.Land : PM.RunRequest 
                ? PlayerState.Run : PlayerState.Walk);
            return;
        }
        if (state == PlayerState.Land && !SM.IsFinished) return;//落地优先级中等
        if (ComboProcessing())
        {
            if (PC.ComboOpen(SM.CurFrame,PC.Comboed)) PC.OpenComboBox(PM.FaceRight);
            if (PC.ComboShut(SM.CurFrame,PC.Comboed)) PC.ShutComboBox();
            return;
        }
        PC.CheckAttack();
        if (PM.IsMoving)//移动、组合技戳棍优先级中下
        {
            AttackOver();//打断普攻
            if (TryComboAttack()) return;
            SetState(PM.RunRequest ? PlayerState.Run : PlayerState.Walk);
            return;
        }
        if (PC.AttackThisFrame)//普攻优先级中下下
        {
            AttackOver();//打断普攻
            if (TryComboAttack()) return;
            PC.AttackThisFrame = false;
            PC.NormalAttack();
            SetState(PlayerState.NormalAttack);
            return;
        }
        if (AttackProcessing())
        {
            if (PC.AttackOpen(SM.CurFrame, PC.Attacked)) PC.OpenAttackBox(PM.FaceRight);
            if (PC.AttackShut(SM.CurFrame, PC.Attacked)) PC.ShutAttackBox();
            return;
        }
        SetState(PlayerState.Idle);//待机优先级最次
    }
    protected virtual void HandleJump() => SM.ChangeState(PM.HasJumped == 1 ? JumpOne : JumpTwo);
    protected virtual void HandleNormalAttack() => SM.ChangeState(PC.GetAction($"NormalAttack{PC.Attacked}").animation);
    protected virtual void HandleComboAttack() => SM.ChangeState(PC.GetAction($"ComboAttack{PC.Comboed}").animation);
    protected virtual void HandleSkill() => SM.ChangeState(PC.GetAction($"Skill{PC.Skilled}").animation);

    //状态类别相同但编号不同时仍要切换动画，保持重构前不同攻击、不同跳跃各自切换的行为。
    private int GetStateAction(PlayerState next)
    {
        switch (next)
        {
            case PlayerState.Jump: return PM.HasJumped;
            case PlayerState.NormalAttack: return PC.Attacked;
            case PlayerState.ComboAttack: return PC.Comboed;
            case PlayerState.Skill: return PC.Skilled;
            default: return -1;
        }
    }

    protected void SetState(PlayerState next)
    {
        int nextAction = GetStateAction(next);//即将进入的状态所对应的具体动作编号
        if (state == next && next != PlayerState.Attacked && stateAction == nextAction) return;
        state = next;
        stateAction = nextAction;
        switch (state)
        {
            case PlayerState.Idle:SM.ChangeState(Idle);break;
            case PlayerState.Walk:SM.ChangeState(Walk); break;
            case PlayerState.Run:SM.ChangeState(Run);break;
            case PlayerState.Jump:HandleJump();break;
            case PlayerState.NormalAttack:HandleNormalAttack();break;
            case PlayerState.ComboAttack:HandleComboAttack();break;
            case PlayerState.Attacked:SM.ChangeState(Attacked);break;
            case PlayerState.Die:SM.ChangeState(Die);break;
            case PlayerState.Skill:HandleSkill();break;
            case PlayerState.Drop:SM.ChangeState(Drop);break;
            case PlayerState.Rise:SM.ChangeState(Rise);break;
            case PlayerState.Land:SM.ChangeState(Land);break;
            case PlayerState.RiseAttack:SM.ChangeState(RiseAttack);break;
        }
    }
    private bool JumpProcessing => state == PlayerState.Jump && !SM.IsFinished;//空中动作过程
    private bool RiseAttackProcessing()
    {
        if (SM.IsFinished && state == PlayerState.RiseAttack)
        {
            RiseOver();
            return false;
        }
        return state == PlayerState.RiseAttack;
    }
    private void RiseOver()
    {
        PC.ShutRiseBox();
    }
    private bool AttackProcessing()//普通攻击过程
    {
        if (SM.IsFinished && IsAttacking)
        {
            AttackOver();
            return false;
        }
        return IsAttacking;
    }
    private bool IsAttacking => state == PlayerState.NormalAttack;
    private void AttackOver()
    {
        PC.ShutAttackBox();
        PC.Attacked = -1;
    }
    private bool SkillProcessing()
    {
        if (!IsSkilling) return false;
        SpecialSkill();
        if (SM.IsFinished)
        {
            SkillOver();
            PM.HasSkilled();
            PM.AllowJump(true);
            return false;
        }
        return true;
    }
    private bool IsSkilling => state == PlayerState.Skill;
    protected virtual void SkillOver()
    {
        PC.ShutSkillBox();
        PC.Skilled = -1;
        PC.SkillEnd();
        checkAttacked.gameObject.SetActive(true);
    }
    protected virtual bool ComboEnd => false;
    private bool ComboProcessing()
    {
        if (state != PlayerState.ComboAttack) return false;
        if (ComboEnd)
        {
            ComboOver();
            return false;
        }
        return true;
    }
    private void ComboOver()
    {
        PC.ShutComboBox();
        PC.Comboed = -1;
    }
    private bool DieProcessing()
    {
        if (state != PlayerState.Die) return false;
        if (SM.IsFinished)
        {
            gameObject.SetActive(false);
            //游戏失败
        }
        return true;
    }
    private bool AttackedProcessing()
    {
        if (state != PlayerState.Attacked) return false;
        if (SM.IsFinished)
        {
            PC.Skilled = -1;//清空期间低优先级的请求
            PM.Stop();
            PM.AllowJump(true);
            return false;
        }
        return true;
    }

}

using System.Collections.Generic;
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    [Header("动作帧素材")]
    [SerializeField] private SpriteData Idle;
    [SerializeField] private SpriteData Walk;
    [SerializeField] private SpriteData Run;
    [SerializeField] private SpriteData JumpOne;
    [SerializeField] private SpriteData JumpTwo;
    [SerializeField] private SpriteData AttackOne;
    [SerializeField] private SpriteData AttackTwo;
    [SerializeField] private SpriteData AttackThree;
    [SerializeField] private SpriteData SkillOne;
    [SerializeField] private SpriteData SkillTwo;
    [SerializeField] private SpriteData SkillThree;
    [SerializeField] private SpriteData Attacked;
    [SerializeField] private SpriteData Die;
    [SerializeField] private SpriteData ComboAttackOne;
    [SerializeField] private SpriteData ComboAttackTwo;
    [SerializeField] private SpriteData Drop;
    [SerializeField] private SpriteData Rise;
    [SerializeField] private SpriteData Land;
    [SerializeField] private SpriteData RiseAttack;
    [Header("引用其他组件")]
    private SpriteManager SM;
    private InputReader IR;
    private PlayerMovement PM;
    private PlayerCombat PC;
    private PlayerInteract PI;
    private Dictionary<PlayerState, StateRule> rules;
    
    [SerializeField]private PlayerState state;//当前状态,初始为待机
    public PlayerState State() => state;
    void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>();
        IR = GetComponent<InputReader>();
        PM= GetComponent<PlayerMovement>();
        PC = GetComponent<PlayerCombat>();
        PI= GetComponent<PlayerInteract>();
        rules = GetComponent<StatePriority>().rules;
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
    }
    private void OnDisable()
    {
        IR.JumpPressed -= PM.OnJump;
        IR.MovePressed -= PM.OnMove;
        IR.AttackPressed -= PC.OnAttack;
        IR.InteractPressed -= PI.OnInteract;
    }
    private void FixedUpdate()//检查状态按优先级,高不可被低打断
    {
        PM.CheckIsGround();
        SM.ChangeSpriteFace(PM.FaceRight);
        PM.CheckJump();
        int airState = PM.AirState();
        PM.CheckMove(PlayerState.ComboAttackTwo == state, airState != 0);
        if (PM.JumpedThisFrame)//跳跃，优先级中上
        {
            PM.JumpedThisFrame = false;
            SetState(PM.HasJumped == 1 ? PlayerState.JumpOne : PlayerState.JumpTwo);
            return;
        }
        if (AirProcessing()) return;
        if (airState!=0)//下落或上升过程,优先级中等,只有上升可攻击
        {
            if (airState < 0)
            {
                SetState(PlayerState.Drop);
                return;
            }
            PC.CheckAttack();
            SetState( PC.AttackThisFrame ? PlayerState.RiseAttack : PlayerState.Rise);
            return;
        }
        if(PM.LandedThisFrame)//落地前摇，不响应攻击
        {
            SetState(!PM.IsMoving ? PlayerState.Land : PM.RunRequest 
                ? PlayerState.Run : PlayerState.Walk);
            return;
        }
        if (state == PlayerState.Land && !SM.IsFinished) return;//落地优先级中等
        if (state == PlayerState.ComboAttackTwo && IR.AttackHeld&&PM.IsMoving)
            return;//按住攻击和移动就可以一直循环直到被高优先级的状态打断
        if (state == PlayerState.ComboAttackOne && !SM.IsFinished) return;
        PC.CheckAttack();
        if (PM.IsMoving)//移动、组合技戳棍优先级中下
        {
            SetState(PC.AttackThisFrame?PlayerState.ComboAttackTwo:
                PM.RunRequest ? PlayerState.Run : PlayerState.Walk);
            return;
        }
        if (PC.AttackThisFrame)//上挑优先级中下，普攻中下下
        {
            if (PI.IsInteracting())
                SetState(PlayerState.ComboAttackOne);
            else
                SetState(PC.CurAttack == 0 ? PlayerState.AttackOne : PC.CurAttack == 1
                ? PlayerState.AttackTwo : PlayerState.AttackThree);
            return;
        }
        if (AttackProcessing()) return;
        SetState(PlayerState.Idle);//待机优先级最次
    }

    private void SetState(PlayerState next)
    {
        if (state == next) return;
        state = next;
        switch (state)
        {
            case PlayerState.Idle:SM.ChangeState(Idle);break;
            case PlayerState.Walk:SM.ChangeState(Walk); break;
            case PlayerState.Run:SM.ChangeState(Run);break;
            case PlayerState.JumpOne:SM.ChangeState(JumpOne);break;
            case PlayerState.JumpTwo:SM.ChangeState(JumpTwo);break;
            case PlayerState.AttackOne:SM.ChangeState(AttackOne);break;
            case PlayerState.AttackTwo:SM.ChangeState(AttackTwo);break;
            case PlayerState.AttackThree:SM.ChangeState(AttackThree);break;
            case PlayerState.Attacked:SM.ChangeState(Attacked);break;
            case PlayerState.ComboAttackOne:SM.ChangeState(ComboAttackOne);break;
            case PlayerState.ComboAttackTwo:SM.ChangeState(ComboAttackTwo);break;
            case PlayerState.Die:SM.ChangeState(Die);break;
            case PlayerState.SkillOne:SM.ChangeState(SkillOne);break;
            case PlayerState.SkillTwo:SM.ChangeState(SkillTwo);break;
            case PlayerState.SkillThree:SM.ChangeState(SkillThree);break;
            case PlayerState.Drop:SM.ChangeState(Drop);break;
            case PlayerState.Rise:SM.ChangeState(Rise);break;
            case PlayerState.Land:SM.ChangeState(Land);break;
            case PlayerState.RiseAttack:SM.ChangeState(RiseAttack);break;
        }
    }
    private bool AirProcessing()//空中动作过程
    {
        return (state == PlayerState.JumpTwo ||
            state == PlayerState.JumpOne || state == PlayerState.RiseAttack)
               && !SM.IsFinished;
    }
    private bool AttackProcessing()//普通攻击过程
    {
        if (SM.IsFinished)
            return false;
        return IsAttacking();
    }
    private bool IsAttacking()
    {
        return state == PlayerState.AttackOne ||
            state == PlayerState.AttackTwo ||
            state == PlayerState.AttackThree;
    }
}

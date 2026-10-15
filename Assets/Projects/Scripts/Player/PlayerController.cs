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
    
    private PlayerState state;//当前状态,初始为待机
    public PlayerState State() => state;
    void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>();
        IR = GetComponent<InputReader>();
        PM= GetComponent<PlayerMovement>();
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
    }
    private void OnDisable()
    {
        IR.JumpPressed -= PM.OnJump;
        IR.MovePressed -= PM.OnMove;
    }
    private void FixedUpdate()//检查状态按优先级,空中动作>攻击>地面动作
    {
        SM.ChangeSpriteFace(PM.FaceRight);
        PM.Step();
        if (PM.JumpedThisFrame)//跳跃前摇，不响应攻击
        {
            SetState(PM.HasJumped==1?PlayerState.JumpOne:PlayerState.JumpTwo);
            PM.JumpedThisFrame = false;
            return;
        }
        int airState = PM.AirState();
        if (airState!=0)//下落或上升过程，之后添加上升攻击
        {
            bool jumpStart = state == PlayerState.JumpTwo || state == PlayerState.JumpOne;
            if (jumpStart && !SM.IsFinished) return;
            SetState(airState > 0 ? PlayerState.Rise : PlayerState.Drop);
            return;
        }
        if(PM.LandedThisFrame)//落地前摇，不响应攻击
        {
            SetState(!PM.IsMoving ? PlayerState.Land : PM.RunRequest 
                ? PlayerState.Run : PlayerState.Walk);
            return;
        }
        //if(SM.IsFinished)//播放完，等添加了攻击再写
        SetState(!PM.IsMoving ? PlayerState.Idle : PM.RunRequest
                ? PlayerState.Run : PlayerState.Walk);
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

}

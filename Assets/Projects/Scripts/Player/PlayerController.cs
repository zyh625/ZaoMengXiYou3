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
    private void FixedUpdate()
    {
        SM.ChangeSpriteFace(PM.FaceRight);
        switch (PM.Step())
        {
            case 0:break;
            case 1:SetState(PlayerState.Walk); break;
            case 2:SetState(PlayerState.JumpOne);break;
            case 3:SetState(PlayerState.JumpTwo);break;
        }
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
        }
    }

}

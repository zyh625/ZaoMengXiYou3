using System.Collections.Generic;
using Unity.Mathematics;
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
    private Health PH;
    private CameraManager CM;
    private PlayerUI UI;
    [SerializeField] private BoxCollider2D checkAttacked;
    private float deltaHalf;//人物相对于相机中心的最大偏移距离
    public float DeltaHalf => deltaHalf;
    
    [SerializeField]private PlayerState state;//当前状态,初始为待机
    public PlayerState State() => state;
    public bool PressedInteract => PI.IsInteracting();
    void Awake()
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
    private void FixedUpdate()//检查状态按优先级,高不可被低打断
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
        if (PC.Skilled!=-1&& !IsSkilling())
        {
            PM.Skilled();
            int skill = PC.Skilled;
            AttackOver(); ComboOver();RiseOver();PM.AllowJump(false);//打断普攻、组合技和空中攻击,不响应跳跃
            SetState(skill == 1 ? PlayerState.SkillOne : skill == 2 ?
                PlayerState.SkillTwo : PlayerState.SkillThree);
            return;
        }
        PM.CheckIsGround();
        PM.CheckMove(PlayerState.ComboAttackTwo == state, PC.Skilled,
    PC.SkillThreeCur);
        if (SkillProcessing())
        {
            if (PC.SkillOpen(SM.CurFrame, PC.Skilled)) PC.OpenSkillBox(PM.SkillFaceRight);
            if (PC.SkillShut(SM.CurFrame, PC.Skilled)) PC.ShutSkillBox();
            if (PC.Skilled != 2) CheckImmune();
            return;
        }
        SM.ChangeSpriteFace(PM.FaceRight);
        PM.CheckJump();
        int airState = PM.AirState();
        if (PM.JumpedThisFrame)//跳跃，优先级中上
        {
            PM.JumpedThisFrame = false;
            AttackOver(); ComboOver(); RiseOver();//打断普攻、组合技和空中攻击
            SetState(PM.HasJumped == 1 ? PlayerState.JumpOne : PlayerState.JumpTwo);
            return;
        }
        if (AirProcessing()) return;
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
            if (PC.AttackThisFrame)
            {
                PC.AttackThisFrame = false;
                PC.Comboed = 2;
                SetState(PlayerState.ComboAttackTwo);
            }
            else SetState(PM.RunRequest ? PlayerState.Run : PlayerState.Walk);
            return;
        }
        if (PC.AttackThisFrame)//上挑优先级中下，普攻中下下
        {
            AttackOver();//打断普攻
            PC.AttackThisFrame = false;
            if (PI.IsInteracting())
            {
                PC.Comboed = 1;
                SetState(PlayerState.ComboAttackOne);
            }
            else
            {
                PC.NormalAttack();
                SetState(PC.Attacked == 1 ? PlayerState.AttackOne : PC.Attacked == 2
                ? PlayerState.AttackTwo : PlayerState.AttackThree);
            }
            return;
        }
        if (AttackProcessing())
        {
            if (PC.AttackOpen(SM.CurFrame,PC.Attacked)) PC.OpenAttackBox(PM.FaceRight);
            if (PC.AttackShut(SM.CurFrame,PC.Attacked)) PC.ShutAttackBox();
            return;
        }
        SetState(PlayerState.Idle);//待机优先级最次
    }

    private void SetState(PlayerState next)
    {
        if (state == next && next != PlayerState.Attacked) return;
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
            state == PlayerState.JumpOne) && !SM.IsFinished;
    }
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
        if (SM.IsFinished && IsAttacking())
        {
            AttackOver();
            return false;
        }
        return IsAttacking();
    }   
    private bool IsAttacking()
    {
        return state == PlayerState.AttackOne ||
            state == PlayerState.AttackTwo ||
            state == PlayerState.AttackThree;
    }
    private void AttackOver()
    {
        PC.ShutAttackBox();
        PC.Attacked = -1;
    }
    private bool SkillProcessing()
    {
        if (IsSkilling())
        {
            if (PC.Skilled == 3)
            {
                PC.CheckSkillThree(SM.CurFrame);
                if (PC.SkillThreeCur == 2)
                {
                    SM.Pause();
                    if (PM.AirState() == 0)
                    {
                        SM.Resume();
                        PC.SkillThreeCur = 3;
                    }
                }
            }
            if (SM.IsFinished)
            {
                SkillOver();
                PM.HasSkilled();
                PM.AllowJump(true);
                return false;
            }
            return true;
        }
        return false;
    }
    private bool IsSkilling()
    {
        return state == PlayerState.SkillOne ||
            state == PlayerState.SkillTwo ||
            state == PlayerState.SkillThree;
    }
    private void SkillOver()
    {
        PC.SkillThreeCur = 0;
        PC.ShutSkillBox();
        PC.Skilled = -1;
        PC.SkillEnd();
        checkAttacked.gameObject.SetActive(true);
    }
    private bool ComboProcessing()
    {
        if ((SM.IsFinished && PlayerState.ComboAttackOne==state)
            ||(PlayerState.ComboAttackTwo==state&&(!IR.AttackHeld||!PM.IsMoving)))
        {
            ComboOver();
            return false;
        }
        return state == PlayerState.ComboAttackOne || state == PlayerState.ComboAttackTwo;
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
            PC.Skilled = -1;//清空期间所有低优先级的请求
            PM.Stop();
            PM.AllowJump(true);
            return false;
        }
        return true;
    }
    private void CheckImmune()
    {
        int immune = PC.CheckInvincible(SM.CurFrame);
        if (immune == 0 || immune == 3) return;
        if (immune == 1)
            checkAttacked.gameObject.SetActive(false);
        else
            checkAttacked.gameObject.SetActive(true);
    }
}

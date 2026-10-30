using UnityEngine;
using zmxy.GamePlay;
[RequireComponent(typeof(BoxCollider2D), typeof(PlayerBoost))]
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
    private PlayerBoost PB;//无双开启只高于受击处理，不覆盖死亡
    private PlayerResources resources;//每次状态判断完成后统一处理持续气力，所有提前return都会覆盖
    private ActorStatus status;//共用冰冻阶段决定是否封锁操作、是否只保留攻击而锁住位移
    private ActorStun stun;//普通眩晕由统一组件计时，控制器仍负责动作切换顺序
    private bool stunPoseHeld;//只在进入或解除眩晕时重设动作，不逐帧重播受击动画
    private bool solidFreezeHeld, statusMovementLocked;//仅在阶段切换时中断动作，避免融化中的新攻击被每帧重置
    private int thawCombo = -1, thawComboFrame = -1;//持续组合技每完成一轮动作也只推进一次融化
    private CameraManager CM;
    [SerializeField] protected BoxCollider2D checkAttacked;
    private float deltaHalf;//人物相对于相机中心的最大偏移距离
    public float DeltaHalf => deltaHalf;
    
    [SerializeField]protected PlayerState state;//当前状态,初始为待机
    private int stateAction = -1;//记录合并状态内的具体招式编号，区分下一招、二段跳和重复进入同一动作
    public PlayerState State() => state;
    public bool CanReceiveHit => checkAttacked == null || (checkAttacked.enabled && checkAttacked.gameObject.activeInHierarchy);//手动扫掠投射物也遵守技能受击箱无敌
    public SpriteManager BodySpriteManager//明确查找本体播放器，新增护罩、水柱等子物体不会抢走人物动画
    {
        get
        {
            if (SM != null) return SM;
            Transform visual = transform.Find("Visual");
            SM = visual != null ? visual.GetComponent<SpriteManager>() : GetComponent<SpriteManager>();
            if (SM == null) SM = GetComponentInChildren<SpriteManager>(true);
            return SM;
        }
    }
    public bool PressedInteract => PI.IsInteracting();
    protected virtual void Awake()
    {
        SM = BodySpriteManager;
        IR = GetComponent<InputReader>();
        PM= GetComponent<PlayerMovement>();
        PC = GetComponent<PlayerCombat>();
        PI= GetComponent<PlayerInteract>();
        PH = GetComponent<Health>();
        status = PH.Status;
        stun = GetComponent<ActorStun>();
        if (stun == null) stun = gameObject.AddComponent<ActorStun>();//已有四角色预制体无需逐个新增同一眩晕组件
        PB = GetComponent<PlayerBoost>();
        resources = GetComponent<PlayerResources>();
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
        IR.BoostPressed += PB.RequestBoost;
        if (status != null) status.Changed += OnStatusChanged;
        if (stun != null) stun.Changed += OnStunChanged;
        OnStatusChanged();
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
        IR.BoostPressed -= PB.RequestBoost;
        if (status != null) status.Changed -= OnStatusChanged;
        if (stun != null) stun.Changed -= OnStunChanged;
        if (stunPoseHeld && SM != null) SM.Resume();
        stunPoseHeld = false;
        if (solidFreezeHeld && SM != null) SM.Resume();
        solidFreezeHeld = statusMovementLocked = false;
    }

    //交出或接收控制权时结束旧动作，保证切回角色后从可控制的待机状态开始。
    public void ResetForSwitch()
    {
        if (stun != null) stun.Clear();//正常换人不会保留旧眩晕姿势或暂停播放器
        AttackOver();
        ComboOver();
        RiseOver();
        SkillOver();
        PC.ClearRequests();
        PM.ClearControlInput();
        PI.ClearRequest();
        PH.AttackedEnd();
        SM.Resume();
        state = PlayerState.Idle;
        stateAction = -1;
        SM.ChangeState(Idle);
    }
    protected virtual void UpdateMovement() => PM.CheckNormalHorizonMove();
    protected virtual void StartSkill() { }
    protected virtual void SpecialSkill() { }
    //在实际进入施法前再次校验角色特有的条件，避免沿用已经失效的输入请求。
    protected virtual bool CanStartSkill() => true;
    //仅自然播放结束时调用；受击、死亡和换人仍通过SkillOver中断。
    protected virtual void CompleteSkill() { }
    //默认使用当前朝向；有施法朝向锁定的角色可改用施法开始时的方向。
    protected virtual bool SkillFaceRight => PM.FaceRight;
    //在技能阶段更新、攻击箱开关之后处理角色自己的无敌帧。
    protected virtual void CheckImmune() { }
    protected virtual bool TryComboAttack() { return false; }
    //需要高于跳跃执行的组合技由角色单独处理，普通组合技仍沿用原来的判断位置。
    protected virtual bool TryPriorityComboAttack() => false;
    protected void FixedUpdate()
    {
        CheckStatePriority();
        CheckContinuousThaw();//持续戳棍等循环攻击按实际动作轮次加速融化，不按物理帧重复累计
        UpdateVigor();//状态流程中的提前返回不再漏掉持续消耗或恢复
    }

    private void OnStunChanged()//普通眩晕立即关闭攻击与施法，结束后同步当前仍按住的方向
    {
        if (PH == null || SM == null || !isActiveAndEnabled) return;
        if (!PH.Dead && stun != null && stun.IsStunned)
        {
            if (status != null && status.BlocksActions) return;//冰冻先保留自己的冻结姿势
            if (!stunPoseHeld)
            {
                stunPoseHeld = true;
                AttackOver(); ComboOver(); RiseOver(); SkillOver();
                PC.ClearRequests(); PM.ClearControlInput(); PI.ClearRequest();
                PH.AttackedEnd(); PM.AllowJump(false);
                SetState(PlayerState.Attacked);
            }
            SM.Pause();
            return;
        }
        if (!stunPoseHeld) return;
        stunPoseHeld = false;
        if (PH.Dead || (status != null && status.BlocksActions)) return;
        SM.Resume();
        PH.AttackedEnd();
        PM.AllowJump(true);
        PM.CheckIsGround();
        int air = PM.AirState();
        SetState(air > 0 ? PlayerState.Rise : air < 0 ? PlayerState.Drop : PlayerState.Idle);
        PM.SyncMoveInput(IR.MoveValue);
    }

    private void OnStatusChanged()//冻结到来立即关闭旧攻击，进入融化或正常解除后再允许本体动画继续
    {
        if (status == null || PH == null || SM == null || !isActiveAndEnabled) return;
        bool solid = status.BlocksActions;
        bool locked = status.BlocksMovement;
        if (PH.Dead)
        {
            if (solidFreezeHeld) SM.Resume();
            solidFreezeHeld = false;
            statusMovementLocked = locked;
            return;
        }
        if (solid && !solidFreezeHeld)
        {
            solidFreezeHeld = true;
            stunPoseHeld = false;//强冻结接管动作后，若它先结束，剩余眩晕需要重新进入受击定格
            SpriteRenderer renderer = SM.GetComponent<SpriteRenderer>();
            Sprite frozenPose = renderer != null ? renderer.sprite : null;//保留被冰住瞬间的姿势，冻结期间不继续挥动旧动作
            AttackOver(); ComboOver(); RiseOver(); SkillOver();
            PC.ClearRequests();
            PM.ClearControlInput();
            PM.AllowJump(false);
            PI.ClearRequest();
            PB.ClearRequest();
            PH.AttackedEnd();
            state = PlayerState.Idle;
            stateAction = -1;
            SM.ChangeState(Idle);//融化开始后可正常从待机切入新的攻击，而不残留已取消的Skill状态
            if (renderer != null && frozenPose != null) renderer.sprite = frozenPose;
            SM.Pause();
        }
        else if (!solid && solidFreezeHeld)
        {
            solidFreezeHeld = false;
            SM.Resume();
            PM.AllowJump(true);
            PB.ClearRequest();//冻结前同帧提交的无双不能在融化时自动补开
            PM.SyncMoveInput(IR.MoveValue);
        }
        if (!solid && statusMovementLocked && !locked)
            PM.SyncMoveInput(IR.MoveValue);//正常融化完成或被击碎后，按住的移动键立即恢复，不要求松开重按
        statusMovementLocked = locked;
    }

    private void CheckContinuousThaw()//同一招式的循环动画换回首帧才算下一次攻击，防御循环可由Combat.advancesThaw排除
    {
        if (status == null || !status.IsMelting || state != PlayerState.ComboAttack || !PC.ContinuousCombo)
        {
            thawCombo = thawComboFrame = -1;
            return;
        }
        if (thawCombo == PC.Comboed && thawComboFrame >= 0 && SM.CurFrame < thawComboFrame)
            PC.NotifyAttackStarted($"ComboAttack{PC.Comboed}");
        thawCombo = PC.Comboed;
        thawComboFrame = SM.CurFrame;
    }

    private void CheckStatePriority()//检查状态按优先级,高不可被低打断，所有角色共用这一条执行顺序
    {
        if (DieProcessing()) return;
        if (!PH.Dead && PB.ConsumeRequest() && state == PlayerState.Attacked)
        {
            PC.ClearRequests();//与受击自然结束一样，丢弃受击期间未生效的低优先级输入
            PM.Stop();
            PM.AllowJump(true);
            PM.CheckIsGround();
            int recoveredAirState = PM.AirState();//脱离受击动画，继续当前地面或空中行动
            if (recoveredAirState > 0) SetState(PlayerState.Rise);
            else if (recoveredAirState < 0) SetState(PlayerState.Drop);
            else SetState(!PM.IsMoving ? PlayerState.Idle : PM.RunRequest ? PlayerState.Run : PlayerState.Walk);
        }
        if (!PH.Dead && status != null && status.BlocksActions)
        {
            OnStatusChanged();
            SM.Pause();
            status.EnforcePosition();
            return;//先允许成功开启无双解除异常；未能开启时仍禁止冰冻中的其他行为
        }
        if (!PH.Dead && stun != null && stun.IsStunned)
        {
            OnStunChanged();
            PH.AttackedEnd();//眩晕中追加伤害只扣血，不在结束后补播旧受击
            return;
        }
        if (stunPoseHeld) OnStunChanged();//霸体或计时刚结束时先恢复播放器，再进入普通状态判断
        if (PH.Dead||PH.Attacked)//可连续受击
        {
            bool back = PH.AttackedBack, up = PH.AttackedUp;//先保存本次独立效果，结束受击请求会清除它们
            bool attackedRight = PH.AttackedRight;//伤害来源只决定方向，击退与击飞各自决定是否生效
            AttackOver(); ComboOver(); RiseOver();SkillOver(); PM.AllowJump(false);//打断所有
            PH.AttackedEnd();
            if (PH.Dead) PM.Stop();
            else
            {
                PM.AttackedMove(attackedRight, back, up);
            }
            SetState(PH.Dead ? PlayerState.Die : PlayerState.Attacked);
            return;
        }
        CM.UpdateCamera(PM.bodyX - CM.CameraCenter, PM.bodyY);
        PM.LimitedRange(CM.CameraCenter - deltaHalf, CM.CameraCenter + deltaHalf);
        if (AttackedProcessing()) return;
        if (PC.Skilled!=-1&& !IsSkilling)
        {
            if (!CanStartSkill() || !PC.TryBeginSkill()) PC.Skilled = -1;
            else
            {
                StartSkill();
                AttackOver(); ComboOver();RiseOver();PM.AllowJump(false);//打断普攻、组合技和空中攻击,不响应跳跃
                SetState(PlayerState.Skill);
                return;
            }
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
        if (TryPriorityComboAttack()) return;//技能之后、跳跃与空中状态之前处理反甲等动作
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
    private void UpdateVigor()//技能、死亡、走路和普通攻击保持气力；跑跳组合技分别结算
    {
        if (resources == null || resources.VigorLocked || PH.Dead || state == PlayerState.Die || !gameObject.activeInHierarchy
            || (status != null && status.BlocksActions)) return;
        float deltaTime = Time.fixedDeltaTime;//持续动作按物理时间消耗，不受渲染帧数影响
        if (state == PlayerState.Skill) return;
        if (state == PlayerState.Attacked)
        {
            resources.RecoverVigor(resources.AttackedVigorRecovery * deltaTime);
            return;
        }
        if (state == PlayerState.ComboAttack)
        {
            bool continuous = PC.ContinuousCombo;//一次性组合技若同时保持跑步，仍需结算这段实际跑步
            if (!PC.DrainComboVigor(deltaTime))
            {
                ComboOver();
                PC.ClearAttackRequests();//耗尽退出后不补放组合期间缓存的普攻
                PM.AllowJump(true);
                PM.CheckNormalHorizonMove();
                RestoreMovementState();
                return;
            }
            if (continuous) return;
        }
        if (PM.IsMoving && resources.RunVigorPerSecond > 0f && resources.CurrentVigor <= 0f)
            PM.CancelRun();//跳跃或一次性组合技刚好花完气力时，也不能遗留一帧跑速
        if (PM.IsMoving && PM.RunRequest)
        {
            bool paid = resources.DrainVigor(resources.RunVigorPerSecond * deltaTime);//空中的快速水平移动也算跑步
            if (!paid || (resources.RunVigorPerSecond > 0f && resources.CurrentVigor <= 0f))
            {
                PM.CancelRun();
                if (state == PlayerState.Run) SetState(PlayerState.Walk);
            }
            return;
        }
        if (state == PlayerState.Idle && !PM.IsMoving)
            resources.RecoverVigor(resources.IdleVigorRecovery * deltaTime);
    }

    private void RestoreMovementState()//持续组合技耗尽后恢复正常地面或空中状态，不遗留组合技动画
    {
        int airState = PM.AirState();
        if (airState > 0) SetState(PlayerState.Rise);
        else if (airState < 0) SetState(PlayerState.Drop);
        else SetState(!PM.IsMoving ? PlayerState.Idle : PM.RunRequest ? PlayerState.Run : PlayerState.Walk);
    }
    protected virtual void HandleJump() => SM.ChangeState(PM.HasJumped == 1 ? JumpOne : JumpTwo);
    protected virtual void HandleNormalAttack() => SM.ChangeState(PC.GetAction($"NormalAttack{PC.Attacked}").animation);
    protected virtual void HandleRiseAttack() => SM.ChangeState(RiseAttack);//空中攻击进入独立入口，子类可在此重置本轮投射物的发射机会
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
        //普通与空中攻击进入入口都代表新一次攻击，即使只有同一招，也必须重新播放本轮动画。
        if (state == next && next != PlayerState.Attacked && next != PlayerState.NormalAttack
            && next != PlayerState.RiseAttack && stateAction == nextAction) return;
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
            case PlayerState.RiseAttack:HandleRiseAttack();break;
        }
        if (next == PlayerState.NormalAttack) PC.NotifyAttackStarted($"NormalAttack{PC.Attacked}");
        else if (next == PlayerState.ComboAttack) PC.NotifyAttackStarted($"ComboAttack{PC.Comboed}");
        else if (next == PlayerState.Skill) PC.NotifyAttackStarted($"Skill{PC.Skilled}");
        else if (next == PlayerState.RiseAttack) PC.NotifyAttackStarted("RiseAttack1");
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
    protected void RiseOver()
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
    protected void AttackOver()
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
            CompleteSkill();
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
    protected virtual void ComboOver()
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

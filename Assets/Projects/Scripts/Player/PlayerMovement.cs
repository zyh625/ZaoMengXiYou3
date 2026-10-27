using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 5f, runSpeed = 8f, backSpeed;//走路、跑步、被击退的速度
    [SerializeField] private float flySlow = 0.7f, quickDown = 1.3f;//空中的水平移速降低，按S下降加快
    [SerializeField] private float upSpeed = 10f;//跳跃获得的速度
    [SerializeField] private int maxJumpTime = 2;//最多连跳的次数
    private bool jumpRequest;
    private int hasJumped = 0;//最多二连跳
    public int HasJumped => hasJumped;
    private bool runRequest;//是否申请转换为跑步
    public bool RunRequest => runRequest && !MovementBlocked && (resources == null || resources.VigorLocked || resources.RunVigorPerSecond <= 0f || resources.CurrentVigor > 0f);//冻结和融化不被当成实际跑动
    [SerializeField, Min(0.2f)] private float doubleMoveGap = 0.3f;//两次按下移动键事件间隔
    private float lastLeft = -1f;//上次按下A键的时间
    public float LastLeft => lastLeft;
    private float lastRight = -1f;//上次按下D键的时间
    public float LastRight => lastRight;
    protected bool right = true;//当前是否朝向右边
    public bool FaceRight => right;
    protected int moveDirect = 0;
    public bool IsMoving => moveDirect != 0 && !MovementBlocked;
    public bool WantsMove => moveDirect != 0;//融化时仍可用移动键选择攻击方向和组合技，但不会真的移动
    protected Rigidbody2D body;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField]private float minNormalY;//与竖直向上的最小夹角
    private ContactFilter2D groundFilter;
    private ContactPoint2D[] contacts = new ContactPoint2D[16];//接触点
    private bool isGround = true;//当前是否在地面上
    public bool LandedThisFrame = false;
    public bool JumpedThisFrame = false;
    public float bodyX => body.position.x;
    private bool quickDowning = false;
    private float beginGravity;
    private bool isQuickDowning = false;
    [SerializeField]private int backStartIdx, backEndIdx;//被击退对应的下标
    private bool allowJump = true;
    private readonly Dictionary<Object, float> moveMultipliers = new();//移速增益按来源叠加，结束无双不会影响其他来源
    private Health health;//接收统一清除负面效果的通知
    private PlayerResources resources;//跑步和成功跳跃分别结算气力
    public bool MovementBlocked => health != null && health.Status != null && health.Status.BlocksMovement;//所有公共和角色专属位移共用这一限制
    protected bool HoldStatusPosition()//特殊技能直接改坐标前调用，最终通用状态还会再作一次位置钳制
    {
        if (!MovementBlocked) return false;
        health.Status.EnforcePosition();
        return true;
    }
    public void AllowJump(bool flag)
    {
        if (!flag) jumpRequest = false;
        allowJump = flag;
    }
    public virtual void CheckComboMove(int combo) => CheckNormalHorizonMove();
    public virtual void CheckSkillMove(int skill, int state) => CheckNormalHorizonMove();
    public virtual void Skilled() { }
    public virtual void HasSkilled() { }

    private void Awake()
    {
        body=GetComponent<Rigidbody2D>();
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
        beginGravity = body.gravityScale;
        health = GetComponent<Health>();
        resources = GetComponent<PlayerResources>();
        if (health != null) health.DebuffsCleared += ClearMoveDebuffs;
    }
    private void OnEnable()
    {
        body.gravityScale = beginGravity;
        hasJumped = 0;
        jumpRequest = false;
        allowJump = true;
    }
    private void OnDisable()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
        isGround = true;
        hasJumped = 0;
        moveMultipliers.Clear();
    }
    private void OnDestroy()
    {
        if (health != null) health.DebuffsCleared -= ClearMoveDebuffs;
    }

    public void SetMoveMultiplier(Object source, float multiplier)//大于1加速，小于1减速，不直接改写基础移动速度
    {
        if (source != null) moveMultipliers[source] = Mathf.Max(0f, multiplier);
    }
    public void RemoveMoveMultiplier(Object source)
    {
        if (source != null) moveMultipliers.Remove(source);
    }
    private float MoveMultiplier
    {
        get
        {
            float multiplier = 1f;//保持地面、跑步和空中各自原来的基础速度
            foreach (KeyValuePair<Object, float> item in moveMultipliers)
                if (item.Key != null) multiplier *= item.Value;
            return multiplier;
        }
    }
    private void ClearMoveDebuffs()//清除减速来源，但保留已经生效的加速
    {
        List<Object> removed = new();//只在驱散时整理，不在每帧分配列表
        foreach (KeyValuePair<Object, float> item in moveMultipliers)
            if (item.Key == null || item.Value < 1f) removed.Add(item.Key);
        foreach (Object source in removed) moveMultipliers.Remove(source);
    }
    public void OnQuickDown()
    {
        if (MovementBlocked) return;
        if (isGround) return;
        quickDowning=true;
    }
    public void ForceQuickDown()//空中防御动作立即停止上升，沿用原本快速下落倍率与落地恢复逻辑
    {
        if (HoldStatusPosition()) return;
        if (CheckGround()) return;
        isGround = false;
        body.linearVelocity = new Vector2(body.linearVelocityX, Mathf.Min(0f, body.linearVelocityY));
        OnQuickDown();
        CheckQuickDown();
    }
    public void OnJump() => jumpRequest = allowJump && !MovementBlocked;
    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (health != null && health.Status != null && health.Status.BlocksActions) return;
        float value = ctx.ReadValue<float>();
        int dir = value > 0.5f ? 1 : value < -0.5f ? -1 : 0;
        if (moveDirect == dir) return;
        if (dir == 0)//等价于松开按键
        {
            runRequest = false;
            moveDirect = 0;
            lastRight = right ? (float)ctx.time : -1f;
            lastLeft = right ? -1f : (float)ctx.time;
            return;
        }
        runRequest = !MovementBlocked && ((dir > 0 && lastRight != -1 && ctx.time - lastRight <= doubleMoveGap)
            || (dir < 0 && lastLeft != -1 && ctx.time - lastLeft <= doubleMoveGap));
        moveDirect = dir;
        right = dir > 0;
    }
    public void SyncMoveInput(float value)//解除冻结或位移锁时读取当前真实按键，不沿用冻结期间的双击跑步手势
    {
        moveDirect = value > 0.5f ? 1 : value < -0.5f ? -1 : 0;
        if (moveDirect != 0) right = moveDirect > 0;
        runRequest = false;
        lastLeft = lastRight = -1f;
    }
    protected void UpdateDirect()
    {
        if (moveDirect != 0)
            right = moveDirect > 0;
    }
    protected void MoveWithInput(float speed)
    {
        UpdateDirect();
        if (HoldStatusPosition()) return;
        Vector2 velocity = body.linearVelocity;
        velocity.x = moveDirect == 0 ? 0f :
            (right ? speed : -speed) * MoveMultiplier * (isGround ? 1f : flySlow);
        body.linearVelocity = velocity;
    }
    public void CheckNormalHorizonMove()=>
        MoveWithInput(RunRequest ? runSpeed : walkSpeed);
    public void CancelRun()//持续消耗不足时立即降为走路，空中也保留原有竖直速度
    {
        runRequest = false;
        CheckNormalHorizonMove();
    }
    protected void SetHorizonSpeed(float speed)
    {
        if (HoldStatusPosition()) return;
        body.linearVelocity = new(speed, body.linearVelocityY);
    }
    public void CheckQuickDown()
    {
        if (MovementBlocked)
        {
            quickDowning = false;
            return;
        }
        if (!isGround && quickDowning && !isQuickDowning)
        {
            body.gravityScale *= quickDown;
            quickDowning = false;
            isQuickDowning = true;
        }
    }
    public void LimitedRange(float minX, float maxX)
    {
        if (HoldStatusPosition()) return;
        body.position = new Vector2(Mathf.Clamp(body.position.x, minX, maxX), body.position.y);
    }
    public void CheckIsGround()
    {
        bool wasGround = isGround;
        isGround = CheckGround();
        LandedThisFrame = isGround && !wasGround;
        if (LandedThisFrame) hasJumped = 0;
    }
    //输入和施法校验使用实时接触结果，不改写控制器本次的落地标记。
    public bool IsOnGroundNow() => CheckGround();
    public void CheckJump()
    {
        JumpedThisFrame = false;
        if (MovementBlocked)
        {
            jumpRequest = false;
            return;
        }
        if (jumpRequest)
        {
            Vector2 velocity = body.linearVelocity;
            jumpRequest = false;
            if (hasJumped < maxJumpTime && (resources == null || resources.TrySpendVigor(resources.JumpVigorCost)))
            {
                QuickDownRecover();
                velocity.y = upSpeed;
                hasJumped++;
                JumpedThisFrame = true;
                isGround = false;
            }
            body.linearVelocity = velocity;
            if (JumpedThisFrame && resources != null && !resources.VigorLocked && resources.CurrentVigor <= 0f && resources.RunVigorPerSecond > 0f)
                CancelRun();//本次跳跃可花完剩余气力，但不能继续免费跑步
        }
    }
    private bool CheckGround()
    {
        float y = body.linearVelocityY;
        if (Mathf.Abs(y) > 0.1f)
            return false;
        int count = body.GetContacts(groundFilter, contacts);
        for(int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y >= minNormalY)
            {
                QuickDownRecover();
                return true; //接触面的法向量超出最小y向量
            }
        }
        return false;
    }
    public int AirState()//-1下落，0在地面，1上升
    {
        if (isGround) return 0;
        return body.linearVelocityY > 0.1f ? 1 : -1;
    }

    public void Stop()
    {
        if (HoldStatusPosition()) return;
        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
    }
    [SerializeField, Min(0f)] private float attackedUpSpeed = 5f;//竖直受击力度独立于角色自己的跳跃速度
    public void AttackedMove(bool r) => AttackedMove(r, true, false);//保留原有只击退的入口
    public void AttackedMove(bool r, bool back, bool up)
    {
        if (HoldStatusPosition()) return;
        Vector2 velocity = body.linearVelocity;
        velocity.x = back ? backSpeed * (r ? 1f : -1f) : 0f;
        if (up)
        {
            QuickDownRecover();
            isGround = false;
            velocity.y += attackedUpSpeed;
        }
        body.linearVelocity = velocity;
    }
    private void QuickDownRecover()
    {
        quickDowning = false;
        isQuickDowning = false;
        body.gravityScale = beginGravity;
    }
    public void ClearJump() => jumpRequest = false;

    //切换时清除旧输入和瞬时标记，避免切回后继续跑动、补跳或沿用快速下落。
    public void ClearControlInput()
    {
        moveDirect = 0;
        runRequest = false;
        lastLeft = lastRight = -1f;
        jumpRequest = false;
        allowJump = true;
        LandedThisFrame = JumpedThisFrame = false;
        QuickDownRecover();
    }

    //新角色启用后，接过旧角色的移动意图、朝向、空中速度与已用跳跃次数。
    public void CopyStateFrom(PlayerMovement previous)
    {
        right = previous.right;
        moveDirect = previous.moveDirect;
        runRequest = previous.runRequest;
        hasJumped = previous.hasJumped;
        isGround = previous.isGround;
        body.linearVelocity = previous.body.linearVelocity;
    }
}

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
    public bool RunRequest => runRequest;
    [SerializeField, Min(0.2f)] private float doubleMoveGap = 0.3f;//两次按下移动键事件间隔
    private float lastLeft = -1f;//上次按下A键的时间
    public float LastLeft => lastLeft;
    private float lastRight = -1f;//上次按下D键的时间
    public float LastRight => lastRight;
    protected bool right = true;//当前是否朝向右边
    public bool FaceRight => right;
    protected int moveDirect = 0;
    public bool IsMoving => moveDirect != 0;
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
    }
    public void OnQuickDown()
    {
        if (isGround) return;
        quickDowning=true;
    }
    public void OnJump() => jumpRequest = allowJump;
    public void OnMove(InputAction.CallbackContext ctx)
    {
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
        runRequest = (dir > 0 && lastRight != -1 && ctx.time - lastRight <= doubleMoveGap)
            || (dir < 0 && lastLeft != -1 && ctx.time - lastLeft <= doubleMoveGap);
        moveDirect = dir;
        right = dir > 0;
    }
    protected void UpdateDirect()
    {
        if (moveDirect != 0)
            right = moveDirect > 0;
    }
    protected void MoveWithInput(float speed)
    {
        UpdateDirect();
        Vector2 velocity = body.linearVelocity;
        velocity.x = moveDirect == 0 ? 0f :
            (right ? speed : -speed) * (isGround ? 1f : flySlow);
        body.linearVelocity = velocity;
    }
    public void CheckNormalHorizonMove()=>
        MoveWithInput(runRequest ? runSpeed : walkSpeed);
    protected void SetHorizonSpeed(float speed) => body.linearVelocity = new
        (speed, body.linearVelocityY);
    public void CheckQuickDown()
    {
        if (!isGround && quickDowning && !isQuickDowning)
        {
            body.gravityScale *= quickDown;
            quickDowning = false;
            isQuickDowning = true;
        }
    }
    public void LimitedRange(float minX, float maxX) => body.position = new Vector2(
        Mathf.Clamp(body.position.x, minX, maxX), body.position.y);
    public void CheckIsGround()
    {
        bool wasGround = isGround;
        isGround = CheckGround();
        LandedThisFrame = isGround && !wasGround;
        if (LandedThisFrame) hasJumped = 0;
    }
    public void CheckJump()
    {
        JumpedThisFrame = false;
        if (jumpRequest)
        {
            Vector2 velocity = body.linearVelocity;
            body.gravityScale = beginGravity;
            jumpRequest = false;
            if (hasJumped < maxJumpTime)
            {
                QuickDownRecover();
                velocity.y = upSpeed;
                hasJumped++;
                JumpedThisFrame = true;
                isGround = false;
            }
            body.linearVelocity = velocity;
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

    public void Stop() => body.linearVelocity = new Vector2(0f, body.linearVelocityY);
    public void AttackedMove(bool r) => body.linearVelocity = new
        Vector2(backSpeed * (r ? 1f : -1f), body.linearVelocityY);
    private void QuickDownRecover()
    {
        quickDowning = false;
        isQuickDowning = false;
        body.gravityScale = beginGravity;
    }
    public void ClearJump() => jumpRequest = false;
}

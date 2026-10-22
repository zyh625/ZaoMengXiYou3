using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float walkSpeed = 5f, runSpeed = 8f,
        comboSpeed = 2f, skillOneSpeed, backSpeed;
    [SerializeField] private float flySlow = 0.7f, quickDown = 1.3f;
    [SerializeField] private float skillThreeHorizon,
        skillThreeUp,skillThreeDown;//空斩的移动阶段的速度
    [SerializeField] private float upSpeed = 10f;
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
    private bool right = true;//当前是否朝向右边
    public bool FaceRight => right;
    private int moveDirect = 0;
    public bool IsMoving => moveDirect != 0;
    private Rigidbody2D body;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField]private float minNormalY;//与竖直向上的最小夹角
    private ContactFilter2D groundFilter;
    private ContactPoint2D[] contacts = new ContactPoint2D[16];//接触点
    private bool isGround = true;//当前是否在地面上
    public bool LandedThisFrame = false;
    public bool JumpedThisFrame = false;
    public float bodyX => body.position.x;
    private bool skillFaceRight;
    public bool SkillFaceRight => skillFaceRight;
    private bool quickDowning = false;
    private float beginGravity;
    [SerializeField]private int backStartIdx, backEndIdx;//被击退对应的下标
    private void Start()
    {
        body=GetComponent<Rigidbody2D>();
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
        beginGravity = body.gravityScale;
    }
    public void OnQuickDown()
    {
        if (isGround) return;
        quickDowning=true;
    }
    public void OnJump()
    {
        jumpRequest = true;
    }
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
    public void CheckMove(bool combo,int skill,int skillThreeState)
    {
        if (moveDirect != 0f) right = moveDirect > 0;
        float dt = Time.deltaTime;
        Vector2 velocity = body.linearVelocity;
        if (!isGround && quickDowning)
        {
            body.gravityScale *= quickDown;
            quickDowning = false;
        }
        if (skill == 1)
            velocity.x = (skillFaceRight ? 1f : -1f) * skillOneSpeed;
        else if (skill == 2)
            velocity = Vector2.zero;
        else if (skill == 3)
        {
            velocity = Vector2.zero;
            SkillThreeMove(skillThreeState);
        }
        else if (moveDirect == 0f)
        {
            velocity.x = 0f;
            body.linearVelocity = velocity;
            return;
        }
        else
            velocity.x = (right ? 1f : -1f) * (combo ? comboSpeed :
                runRequest ? runSpeed : walkSpeed) * (isGround ? 1f : flySlow);
        body.linearVelocity=velocity;
    }
    public void LimitedRange(float minX, float maxX) => body.position = new Vector2(
        Mathf.Clamp(body.position.x, minX, maxX), body.position.y);
    private void SkillThreeMove(int state)
    {
        if (state == 1 || state == 3) return;
        Vector2 dir = new((skillFaceRight ? 1f : -1f) * skillThreeHorizon,
            state == 0 ? skillThreeUp : -skillThreeDown);
        body.position += Time.fixedDeltaTime * dir;
    }
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
            if (hasJumped < 2)
            {
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
                return true; //接触面的法向量超出最小y向量
        }
        return false;
    }
    public int AirState()//-1下落，0在地面，1上升
    {
        if (isGround) return 0;
        return body.linearVelocityY > 0.1f ? 1 : -1;
    }
    private void OnDisable()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
        isGround = true;
        hasJumped = 0;
    }
    public void Skilled() => skillFaceRight = right;
    public void HasSkilled() => right = skillFaceRight;
    public void Stop() => body.linearVelocity = new Vector2(0f, body.linearVelocityY);
    public void AttackedMove(bool r) => body.linearVelocity = new
        Vector2(backSpeed * (r ? 1f : -1f), body.linearVelocityY);
}

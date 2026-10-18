using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 5f, runSpeed = 8f, comboSpeed = 2f;
    [SerializeField] private float flySlow = 0.7f;
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
    private BoxCollider2D playerBox;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField]private float minNormalY;//与竖直向上的最小夹角
    private ContactFilter2D groundFilter;
    private ContactPoint2D[] contacts = new ContactPoint2D[16];//接触点
    private bool isGround = true;//当前是否在地面上
    public bool LandedThisFrame = false;
    public bool JumpedThisFrame = false;
    private void Start()
    {
        body=GetComponent<Rigidbody2D>();
        playerBox = GetComponent<BoxCollider2D>();
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
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
    public void CheckMove(bool combo,bool fly)
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = (moveDirect == 0f ? 0f :
            (right ? 1f : -1f) * (combo ? comboSpeed :
            runRequest ? runSpeed : walkSpeed)) * (fly ? flySlow : 1f);
        body.linearVelocity=velocity;
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
        if (jumpRequest)
        {
            Vector2 velocity = body.linearVelocity;
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
        if (y > 0.1f)
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
}

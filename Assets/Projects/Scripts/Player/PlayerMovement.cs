using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 5f, runSpeed = 8f;
    [SerializeField] private float upSpeed = 10f;
    private bool jumpRequest;
    private int hasJumped = 0;//最多二连跳
    private bool moveHeld = false;
    private bool runRequest;//是否申请转换为跑步
    [SerializeField, Min(0.2f)] private float doubleMoveGap = 0.5f;//两次按下移动键事件间隔
    private float lastLeft = -1f;//上次按下A键的时间
    public float LastLeft => lastLeft;
    private float lastRight = -1f;//上次按下D键的时间
    public float LastRight => lastRight;
    private bool right = true;//当前是否朝向右边
    public bool FaceRight => right;
    private int moveDirect = 0;
    private Rigidbody2D body;
    private void Start()
    {
        body=GetComponent<Rigidbody2D>();
    }
    public void OnJump()
    {
        jumpRequest = true;
    }
    public void OnMove(InputAction.CallbackContext ctx)
    {
        float value = ctx.ReadValue<float>();
        int dir = value > 0.5f ? 1 : value < -0.5f ? -1 : 0;
        if (moveDirect == dir)
        {
            moveHeld = true;
            return;//等价于长按
        }
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
    public int Step()
    {
        int priHasJumped = hasJumped;
        Vector2 velocity = body.linearVelocity;
        velocity.x = moveDirect == 0f ? 0f :
            ((right ? 1f : -1f) * (runRequest ? runSpeed : walkSpeed));
        if (jumpRequest)
        {
            velocity.y = hasJumped < 2 ? upSpeed : 0f;
            hasJumped++;
            jumpRequest = false;
        } 
        body.linearVelocity=velocity;
        int curState = 0;
        if (moveDirect != 0f && !moveHeld) curState = 1;
        if (priHasJumped < hasJumped) curState = hasJumped == 1 ? 2 : 3;
        return curState;
    }

}

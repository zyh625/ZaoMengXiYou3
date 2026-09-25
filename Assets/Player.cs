using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
public class Player : MonoBehaviour
{
    public AnimationData[] monkeys;//角色的所有动作的帧动画
    public GameObject monkeyPrefab;//预先创建好的角色实例
    public static Transform tran;//角色位置
    public SpriteRenderer sr;//角色精灵
    public Rigidbody2D rb;//角色的重力系统

    public static float HalfSr { get; private set; }//所有脚本可用但不可修改

    public int act = 0;//0表示静止，1表示行走，2表示跑步，3-6分别表示1到4段攻击，7表示普通跳跃，8表示二连跳跃，
    public int curFrame = 0;//当前帧序列
    public float delayTime = 0;
    public bool face = true;//角色朝向，开始向右

    float lastPressTime = -1;
    float doublePressTime = 0.3f;
    Vector2 input;//检测是否按下A、D键
    Vector2 priInput;//用于判断上次按下的按键是否和这次一样
    float speed = 0.003f;
    int jumped = 0;//检测跳跃次数
    private int nextAttack = 3;//下一次攻击动作，按3、4、5、6循环
    void Awake()
    {
        tran = monkeyPrefab.transform;
        sr = monkeyPrefab.GetComponent<SpriteRenderer>();
        rb = monkeyPrefab.GetComponent<Rigidbody2D>();
        HalfSr = sr.bounds.size.x / 2;
    }
    
    void Update()
    {
        if (input.x != 0)
        {
            Move(input.x > 0, speed);
        }
    }
    public void OnMove(InputAction.CallbackContext ctx)
    {
        input = ctx.ReadValue<Vector2>();
        if (ctx.started)
        {
            InitMonkey(1);
            if (lastPressTime!=-1&&Time.time - lastPressTime <= doublePressTime&&priInput.x==input.x)
            {
                InitMonkey(2);
                speed = 0.005f;
            }
            lastPressTime = Time.time;
            priInput = input;
        }
        if (ctx.canceled)
        {
            InitMonkey(0);
            speed = 0.003f;
        }
    }
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (rb.linearVelocity.y ==0)
        {
            jumped = 0;
        }
        if (ctx.performed)
        {
            if (jumped == 2) return;
            Jump();
            InitMonkey(7 + jumped);
            jumped++;
        }
    }
    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;//松开K键才算一次攻击

        InitMonkey(nextAttack);
        nextAttack = nextAttack == 6 ? 3 : nextAttack + 1;
    }
    public void UpdateMonkey()
    {
        if (act > 6) return;
        delayTime += Time.deltaTime;
        if (delayTime >= 0.1f && act >= 3 && act <= 6)
        {
            InitDelay();
            if (curFrame == 0) InitMonkey(0);
        }
        if (delayTime >= 0.2)
        {
            InitDelay();
        }

    }
    public void InitDelay()
    {
        delayTime = 0;
        sr.sprite = monkeys[act].frames[curFrame];
        curFrame = (curFrame + 1) % monkeys[act].frames.Length;
    }
    public void Move(bool r, float speed)//角色水平移动
    {
        if (r != face)
        {
            sr.flipX = !r;
            face = r;
        }
        Vector3 pos = tran.position;
        pos.x += (r ? 1 : -1) * speed;
        pos.x = Mathf.Clamp(pos.x, CameraManager.left, CameraManager.right);
        tran.position = pos;
    }
    public void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 10f);
    }
    public void InitMonkey(int k)//切换角色状态，当前帧序列变为0
    {
        act = k;
        curFrame = 0;
        delayTime = 0f;
    }
}

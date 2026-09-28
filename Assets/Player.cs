using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
public class Player : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;
    private bool isGround;//判断角色当前是否接触地面、
    private ContactFilter2D groundFilter;
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[16];

    public AnimationData[] monkeys;//角色的所有动作的帧动画
    public static Transform tran;//角色位置
    private SpriteRenderer sr;//角色精灵
    private Rigidbody2D rb;//角色的重力系统
    public GameObject[] AttackHit = new GameObject[2];//角色的左右攻击碰撞箱

    public static float HalfSr { get; private set; }//所有脚本可用但不可修改

    public int act = 0;//0表示静止，1表示行走，2表示跑步，3-6分别表示1到4段攻击，7表示起跳过程，8表示落地过程，9表示起跳到空中,10表示二连跳，11表示受击，12空中定格帧动画,13空中攻击
    public int curFrame = 0;//当前帧序列
    public float delayTime = 0;
    public bool face = true;//角色朝向，开始向右
    public static GameObject Attacked,Attacked1;//鸟怪聚集地

    float lastPressTime = -1;
    float doublePressTime = 0.3f;
    Vector2 input;//检测是否按下A、D键
    Vector2 priInput;//用于判断上次按下的按键是否和这次一样
    float speed = 2.9f;
    int jumped = 0;//检测跳跃次数
    private float lastAttack = -1f;//上次攻击的事件
    private int priAttack = 3;//上次攻击的招式
    public static float attack = 10f;//攻击力
    private bool isFlying = false;//是否处于跳跃状态
    void Awake()
    {
        Attacked = transform.Find("Attacked").gameObject;
        Attacked1 = transform.Find("Attacked(1)").gameObject;
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
        tran = transform;
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        HalfSr = sr.bounds.size.x / 2;
        
    }
    private void FixedUpdate()
    {
        bool wasGround = isGround;
        isGround = CheckGround();
        if (!wasGround && isGround && isFlying && act != 7)//由未落地到落地的瞬间，之前按下了K键而且当前不是起跳下蹲的过程
        {
            jumped = 0;
            isFlying = false;
            RestoreGroundAction();
        }
        if (!isGround && !isFlying && act != 7)//从平台掉下
        {
            isFlying = true;
            jumped = 0;//还是有两次跳跃机会
            InitMonkey(12);
        }
    }
    private void RestoreGroundAction()//已经回到地面，继续之前的动作
    {
        if (Mathf.Abs(input.x) < 0.01f)//没有移动，落地回到待机状态
        {
            InitMonkey(0);
        }
        else
        {
            InitMonkey(speed > 2.9f ? 2 : 1);//检查是走路还是跑步
        }
    }
    private bool CheckGround()//检测是否当前时刻触地
    {
        if (rb.linearVelocity.y > 0.1f) return false;//如果正在向上
        int count = rb.GetContacts(groundFilter, groundContacts);//角色于所有地板的接触点个数
        for(int i = 0; i < count; i++)
        {
            if (groundContacts[i].normal.y >= 0.65f)//接受一定程度的坡度
                return true;
        }
        return false;
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
            if (!isFlying)InitMonkey(1);
            if (lastPressTime!=-1&&Time.time - lastPressTime <= doublePressTime&&priInput.x==input.x)
            {
                if(!isFlying)InitMonkey(2);
                speed = 4f;
            }
            lastPressTime = Time.time;
            priInput = input;
        }
        if (ctx.canceled)
        {
            if(!isFlying)InitMonkey(0);
            speed = 2.9f;
        }
    }
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || jumped >= 2) return;
        if (isGround)
        {
            jumped = 1;
            isFlying = true;
            InitMonkey(7);
        }
        else if (jumped == 1)
        {
            jumped = 2;
            isFlying = true;
            InitMonkey(10);
        }
    }
    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            if (isFlying)
            {
                JumpAttack();
                return;
            }
            if (lastAttack == -1 || Time.time - lastAttack > 1.5f)//长时间未攻击，从第一招开始
                priAttack = 3;
            else
                priAttack = (priAttack == 6) ? 3 : priAttack + 1;
            InitMonkey(priAttack);
            InitAttackBox(true); //开启攻击判定
            lastAttack = Time.time;
        }
    }
    public void UpdateMonkey()
    {
        if (act == 12) return;
        delayTime += Time.deltaTime;
        if (delayTime >= 0.04f && ((act >= 3 && act <= 6)||act==13))//攻击更新
        {
            InitDelay();
            if (curFrame == 0) InitMonkey(isFlying ? 12 : 0);
        }
        if (delayTime >= 0.02f&&(act==7||act==9||act==10))//起跳或二连跳动画，播放一遍
        {
            InitDelay();
            if (curFrame == 0)
            {
                if (act == 7) Jump();
                InitMonkey(act == 7 ? 9 : 12);
                return;
            }
        }
        if (delayTime >= 0.2)
            InitDelay();

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
        pos.x += (r ? 1 : -1) * speed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, CameraManager.left, CameraManager.right);
        tran.position = pos;
    }
    public void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 10f);
    }
    public void InitMonkey(int k)//切换角色状态，当前帧序列变为0
    {
        if ((k < 3 || k > 6) && k != 13) InitAttackBox(false);//非攻击状态直接取消判定
        if (k == 10) Jump();//空中二连跳直接获得向上的速度
        act = k;
        curFrame = 0;
        delayTime = 0f;
        sr.sprite = monkeys[k].frames[0];
    }
    public void InitAttackBox(bool b)//关闭碰撞箱
    {
        if (b)
        {
            AttackHit[face ? 1 : 0].SetActive(true);
            if (act == 5) AttackHit[face ? 0 : 1].SetActive(true);
            else AttackHit[face ? 0 : 1].SetActive(false);
            return;
        }
        AttackHit[0].SetActive(false);
        AttackHit[1].SetActive(false);
    }
    private void JumpAttack()//跳跃过程中攻击
    {
        InitMonkey(13);
        InitAttackBox(true);
    }
}

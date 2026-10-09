using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class Player : MonoBehaviour
{
    private int myLevel = 1;//当前等级
    [SerializeField] private float jumpSpeed = 7f;//起跳瞬间的速度
    [SerializeField] private LayerMask groundLayer;
    private bool isGround;//判断角色当前是否接触地面、
    private ContactFilter2D groundFilter;
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[16];

    public AnimationData[] monkeys;//角色的所有动作的帧动画
    public static Transform tran;//角色位置
    private SpriteRenderer sr;//角色精灵
    private Rigidbody2D rb;//角色的重力系统
    public static Collider2D col;//角色自身的碰撞箱

    public static float HalfSr { get; private set; }//所有脚本可用但不可修改

    public int act = 0;//0表示静止，1表示行走，2表示跑步，3-6分别表示1到4段攻击，7表示起跳过程，8表示落地过程，9表示起跳到空中,10表示二连跳，11表示受击，12空中定格帧动画,13空中攻击，14死亡
    public int curFrame = 0;//当前帧序列
    public float delayTime = 0;
    public bool face = true;//角色朝向是否向右
    public static GameObject Attacked,Attacked1;//鸟怪聚集地
    public static Collider2D box, box1;//聚集地的碰撞箱

    float lastPressTime = -1;
    float doublePressTime = 0.3f;
    Vector2 input;//检测是否按下A、D键
    Vector2 priInput;//用于判断上次按下的按键是否和这次一样
    float speed = 2.9f;
    int jumped = 0;//检测跳跃次数
    private float lastAttack = -1f;//上次攻击的事件
    private int priAttack = 3;//上次攻击的招式
    public static float attack = 10f;//当前攻击力
    [SerializeField]private float beginAttack = 10f;//初始攻击力
    private bool isFlying = false;//是否处于跳跃状态
    private bool nextAttack = false;//短时间内是否按下第二次攻击
    private float boostFull = 100f;//无双能量最大值
    private float boostEnergy = 0f;//无双状态的能量条
    private bool boost = false;//是否开启无双
    public static bool attackedDirect;//被攻击时击退方向是否为右
    private float beginBlood = 200f;//初始血量
    private float blood = 200f;//角色血量
    private float backSpeed = 3f;//被击退的移速
    private bool isAttacking = false;//处在有效攻击帧
    private float mp = 150f;
    private float beginMP = 150f;
    public float xp = 0;
    public float beginXP = 100f;
    public GameObject[] AttackHit;
    private Collider2D[] attackBox = new Collider2D[2];
    public StatusBarUI HP, MP, XP;
    [SerializeField] private Image Boost;
    [SerializeField] private Image BoostFull;
    [SerializeField] private float deltaBoost = 10f;//每次攻击增加的能量
    [SerializeField] private float boostTime = 10f;//无双时间持续多久
    [SerializeField] private float boostReduce = 0.01f;//能量逐渐衰减
    [SerializeField] private Exit exit;
    private Collider2D exitCol;
    void Awake()
    {
        PlayerLoad();
        exitCol = exit.GetComponent<Collider2D>();
        attack = beginAttack;
        col = GetComponent<Collider2D>();
        for (int i = 0; i < 2; i++) attackBox[i] = AttackHit[i].GetComponent<Collider2D>();
        Attacked = transform.Find("Attacked").gameObject;box=Attacked.GetComponent<Collider2D>();
        Attacked1 = transform.Find("Attacked(1)").gameObject;box1=Attacked1.GetComponent<Collider2D>();
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
        tran = transform;
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        HalfSr = sr.bounds.size.x / 2;
        HP.UpdateValue(blood, beginBlood);
        MP.UpdateValue(mp, beginMP);
        XP.UpdateValue(xp, beginXP);
    }
    
    private void FixedUpdate()
    {
        bool wasGround = isGround;//前一瞬间是否触地
        isGround = CheckGround();//当前是否触地
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
        if (boost||(boostEnergy < boostFull && boostEnergy >= 0.1f)) UpdateBoost(false);
        if (act == 11 && (curFrame >= 5 && curFrame <= 11)&&!boost)//被击退
            Move(attackedDirect, backSpeed);
        if (act != 11 && input.x != 0)
            Move(input.x > 0, (boost ? 1.25f : 1f) * speed);//无双加速
        if (isAttacking&&act!=5)//招式三左右两侧都攻击，不需要更新
            InitAttackBox(true);//重复开关，角色可能在攻击过程中转向
    }
    public void OnMove(InputAction.CallbackContext ctx)
    {
        input = ctx.ReadValue<Vector2>();
        if (act == 11) return;
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
        if (!ctx.performed || jumped >= 2 || act == 7 || act == 11) return;
        if (isGround||jumped==0)
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
        if (act == 11) return;//受击状态不能攻击
        if (ctx.performed)
        {
            RequestAttack();
        }
    }
    public void OnBoost(InputAction.CallbackContext ctx)
    {
        if (ctx.performed&&boostEnergy>=100f)//开启无双
        {
            attack *= 2.5f;
            UpdateBoost(false);
            boost = true;
            if (act==11)//当前处于受击状态，直接恢复
            {
                if (isFlying) InitMonkey(12);
                else RestoreGroundAction();
            }
        }
    }
    public void OnDrawOut(InputAction.CallbackContext ctx)//胜利后按下“W”键，退出当前关卡
    {
        if (ctx.performed && exit.gameObject.activeSelf &&col.Distance(exitCol).isOverlapped)//符合退出的条件
        {
            PlayerSave();
            SceneManager.LoadScene("VictoryScene");
            return;
        }
    }
    public void UpdateMonkey()
    {
        if (act == 12) return;
        delayTime += Time.deltaTime;
        if (delayTime >= 0.04f && IsAttacking())//攻击更新
        {
            InitDelay();
            if (curFrame == 5) InitAttackBox(true);//第6帧开启碰撞箱
            else if (curFrame == 12) InitAttackBox(false);
            if (nextAttack && (curFrame >= 12 || curFrame == 0))
            {
                NextAttack();
                return;
            }
            if (curFrame == 0)
            {
                nextAttack = false;
                RestoreAct();
            }
            return;
        }
        if (delayTime >= 0.02f&&(act==7||act==9||act==10))//起跳或二连跳动画，播放一遍
        {
            InitDelay();
            if (curFrame == 0)
            {
                if (act == 7)
                {
                    Jump(); InitMonkey(9);
                }
                else RestoreAct();
            }
            return;
        }
        if (delayTime >= 0.02f && act == 14)//死亡帧动画
        {
            InitDelay();
            if (curFrame == 0)//角色死亡删除角色
            {
                gameObject.SetActive(false);
                //播放失败动画，返回主菜单
                return;
            }
        }
        if (delayTime >= 0.04f && act == 11)//受击动画
        {
            InitDelay();
            if (curFrame == 0)
                RestoreAct();
            return;
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
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);//速度偏低，加速度偏低，让滞空时间较长
    }
    public void InitMonkey(int k)//切换角色状态，当前帧序列变为0
    {
        act = k;
        curFrame = 0;
        delayTime = 0f;
        sr.sprite = monkeys[k].frames[0];
        InitAttackBox(false);
        if (k == 10) Jump();//空中二连跳直接获得向上的速度
    }
    public void InitAttackBox(bool b)//控制攻击碰撞箱
    {
        isAttacking = b;
        attackBox[face ? 1 : 0].gameObject.SetActive(b);//控制当前朝向的碰撞箱
        attackBox[face ? 0 : 1].gameObject.SetActive(b && (act == 5 ? b : !b));//控制对侧碰撞箱
    }
    private void RequestAttack()//检测是否短时间内连按，最多响应两次，1.5秒未攻击重新出招
    {
        if (!IsAttacking())
        {
            nextAttack = false;
            if (isFlying) InitMonkey(13);
            else
            {
                if (lastAttack == -1 || Time.time - lastAttack > 1.5f)//长时间未攻击，从第一招开始
                    priAttack = 3;
                else
                    priAttack = (priAttack == 6) ? 3 : priAttack + 1;
                InitMonkey(priAttack);
                lastAttack = Time.time;
            }
            return;
        }
        if (curFrame >= 5)//攻击阶段，可存储一次攻击
            nextAttack = true;
        if (curFrame >= 12 && nextAttack)
            NextAttack();
    }
    private void NextAttack()//进行下一次攻击
    {
        if (act == 13) InitMonkey(13);//空中连按，重新播放空斩的帧动画
        else InitMonkey(act == 6 ? 3 : act + 1);
        nextAttack = false;
    }
    private bool IsAttacking()//检查是否处于攻击状态
    {
        return (act >= 3 && act <= 6) || act == 13;
    }
    private void RestoreAct()
    {
        if (isFlying) InitMonkey(12);
        else RestoreGroundAction();
    }
    public void TakeDamage(float a)//检测到被攻击了
    {
        if (act == 14) return;//当前已经死亡，正在播放帧动画，直接返回
        blood = Mathf.Max(blood - a, 0);
        HP.UpdateValue(blood, beginBlood);
        if (blood <= 0f)
        {
            InitMonkey(14);
            return;
        }
        if(!boost)InitMonkey(11);//非无双状态，受击动画
    }
    public void UpdateBoost(bool flag)//更新无双状态
    {
        if (flag)
        {
            if(boost)return;
            boostEnergy = Mathf.Min(boostFull, boostEnergy + deltaBoost);
            if (boostEnergy == boostFull)
                BoostFull.gameObject.SetActive(true);//无双准备就绪
        }
        else//一直都在缓慢减少
        {
            if (boost) boostEnergy = Mathf.Max(0f, boostEnergy - boostTime * Time.deltaTime);//无双状态能量衰减加快
            else boostEnergy = Mathf.Max(0f, boostEnergy - boostReduce * Time.deltaTime);
            if (boostEnergy < 0.1f)
            {
                attack = beginAttack;
                boost = false;//取消无双状态
            }
        }
        Boost.fillAmount = boostEnergy / boostFull;
    }
    private void PlayerSave()
    {
        SaveData data = new()
        {
            exp = xp
        };
        SaveSystem.Save(data);
    }
    private void PlayerLoad()
    {
        SaveData data = SaveSystem.Load();
        if (data == null)
            xp = 0;
        else
            xp = data.exp;
    }
}

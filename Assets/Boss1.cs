using UnityEngine;

public class Boss1 : MonoBehaviour,IDamageable
{
    private SpriteRenderer sr;//boss的精灵
    public AnimationData[] birdFrames;
    public GameObject attackOneBox;//boss的攻击碰撞箱
    public GameObject Ball;//光球
    private Collider2D attackOneCol;
    [SerializeField] private float idelF = 0.05f;//待机频率
    [SerializeField] private float moveF = 0.05f;//移动频率
    [SerializeField] private float attack1F = 0.05f;//招式一频率
    [SerializeField] private float attack2F = 0.05f;//招式二频率
    [SerializeField] private float attackedF = 0.05f;//受击频率
    [SerializeField] private float dieF = 0.05f;//死亡频率
    [SerializeField] private float changeF = 0.05f;//切换形态频率
    [SerializeField] private float flyF = 0.05f;//飞行频率
    [SerializeField] private float flyAttackF = 0.05f;//飞行攻击频率
    [SerializeField] private float flyAttackedF = 0.05f;//飞行受击频率
    [SerializeField] private float flyDieF = 0.05f;//飞行死亡频率
    public static bool alive = false;//是否出动
    private bool r = false;//当前朝向右边
    private bool face = false;//前一瞬间朝向右边
    private float curTime = 0f;//当前帧延时时长
    private int curFrame = 0;//当前帧序列
    private float waitAttack = 6f;//普攻攻击间隔，远程攻击*1.5
    public static float attackOne = 18f;//招式一攻击力
    private float lastAttack = -1f;//上一次攻击时间
    private float blood = 1000f;//血量
    private float beginIdel = 0;//开始待机的时间
    private float waitIdel = 2f;//最长待机时间
    [SerializeField] private float moveSpeed = 3f;//移速
    [SerializeField] private float attackSpeed = 8f;//招式一向前突刺和复原的速度
    private Vector2 attackDirect= Vector2.zero;
    private int act = 0; //0待机，1行走，2攻击招式一，3攻击招式二，4站立受击，5站立死亡，6切换飞行，7飞行，8飞行攻击，9飞行受击，10飞行死亡
    [SerializeField] private float attackOneDis = 5f;//近战触发距离
    private Player player;
    private Vector2 ballBeginPlace;//刚开始光球相对于boss的位置
    private float attackOneY;//近战触发y值
    private bool hasHit = false;//是否攻击到
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ballBeginPlace = Ball.transform.localPosition;
        player=Player.tran.GetComponent<Player>();
        attackOneCol = attackOneBox.GetComponent<Collider2D>();
        attackOneDis = attackOneCol.bounds.size.x * 2;//近战攻击水平检测范围是碰撞箱的两倍
        attackOneY = attackOneCol.bounds.size.y/2;
        sr = GetComponent<SpriteRenderer>();
    }
    void Update()
    {
        curTime += Time.deltaTime;
        switch (act)
        {
            case 0:Act0();break;
            case 1:Act1();break;
            case 2:Act2();break;
            case 3:Act3();break;
            case 4:Act4();break;
            case 5:Act5();break;
            case 6:Act6();break;
            case 7:Act7();break;
            case 8:Act8();break;
            case 9:Act9();break;
            case 10:Act10();break;
            default:break;
        }
    }
    private void Act0()//待机
    {
        if (Time.time - beginIdel >= waitIdel)
            CheckState();
        else if (curTime >= idelF)
            InitDelay();
    }
    private void Act1()//行走
    {
        Vector2 dir = new Vector2((Player.tran.position.x<=transform.position.x)?-1f:1f, 0);
        transform.position += moveSpeed * Time.deltaTime * (Vector3)dir;
        if (CheckAttack())
            return;
        if (curTime >= moveF)
            InitDelay();
    }
    private void Act2()//招式一
    {
        if (curFrame <= 4)//向着玩家突刺
            transform.position += attackSpeed * Time.deltaTime * (Vector3)attackDirect;
        else if (curFrame >= 11)//回到起始点
        {
            transform.position -= attackSpeed * Time.deltaTime * (Vector3)attackDirect;
            hasHit = false;
        }
        else//判断是否攻击到玩家
        {
            Physics2D.SyncTransforms();
            if (!hasHit && attackOneCol.Distance(Player.box).isOverlapped)
            {
                hasHit = true;//已经攻击过了
                player.TakeDamage(attackOne);
                Player.attackedDirect = r;//boss朝哪边攻击，玩家被击退向哪边
            }
        }
        if (curTime >= attack1F)
        {
            InitDelay();
            if (curFrame == 5)//开始攻击箱
                InitBox(true);
            else if (curFrame == 11)//关闭攻击箱
            {
                InitBox(false);
                lastAttack = Time.time;
            }
            else if (curFrame == 0)//攻击完全结束
                InitBoss(0);
        }
    }
    private void Act3()//招式二
    {
        if (curTime >= attack2F)
        {
            InitDelay();
            if (curFrame == 11)//发射光球
            {
                InitBall();
                lastAttack = Time.time;
            }
            else if (curFrame == 0)//攻击结束
                InitBoss(0);
        }
    }
    private void Act4() { }
    private void Act5() { }
    private void Act6() { }
    private void Act7() { }
    private void Act8() { }
    private void Act9() { }
    private void Act10() { }
    private void CheckState()//待机一段时间后检查是否切换状态
    {
        if (CheckAttack())//检测到攻击
            return;
        InitBoss(1);//切换至行走
    }

    private void InitBall()//初始化攻击光球
    {
        Vector2 pos = ballBeginPlace;
        pos.x *= r ? -1 : 1;//初始boss朝向左
        Ball.transform.localPosition = pos;
        Ball.SetActive(true);
    }
    private void InitDelay()
    {
        r = transform.position.x <= Player.tran.position.x;
        if (r != face)
        {
            sr.flipX = !r;
            face = r;
        }
        sr.sprite = birdFrames[act].frames[curFrame];
        curTime = 0f;
        curFrame = (curFrame + 1) % birdFrames[act].frames.Length;
    }
    private void InitBoss(int a)
    {
        if (a == 0)
            beginIdel = Time.time;
        else if (a == 2)
            attackDirect.x = r ? 1f : -1f;
        act = a;
        curFrame = 0;
        curTime = 0f;
        sr.sprite = birdFrames[act].frames[curFrame];
    }
    private void InitBox(bool flag)//控制鸟怪的攻击碰撞箱的朝向
    {
        Vector2 offset = attackOneCol.offset;
        offset.x = Mathf.Abs(offset.x) * (r ? 1 : -1);
        attackOneCol.offset = offset;
    }
    private bool CheckAttack()//是否符合招式一、二的出招条件
    {
        if ((lastAttack == -1 || Time.time - lastAttack > waitAttack)&& CheckY()&&Mathf.Abs(transform.position.x - Player.tran.position.x) <=attackOneDis)
        {
            InitBoss(2);//招式一
            return true;
        }
        if ((lastAttack == -1 || Time.time - lastAttack > 1.5f*waitAttack) &&CheckY() && Mathf.Abs(transform.position.x - Player.tran.position.x) > attackOneDis)
        {
            if (Random.Range(0f, 5f) <= 2.5f)//一半概率远程攻击，一半概率行走
                return false;
            InitBoss(3);//招式二
            return true;
        }
        return false;
    }
    private bool CheckY()
    {
        return transform.position.y + attackOneY > Player.tran.position.y && Player.tran.position.y > transform.position.y - attackOneY;
    }
    public void TakeDamage(float damage)
    {
        blood-=damage;
        if(blood <= 0)
            act = act < 5 ? 5 : 10;
    }
}

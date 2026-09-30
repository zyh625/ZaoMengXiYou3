using UnityEngine;

public class Boss1 : MonoBehaviour
{
    private SpriteRenderer sr;//boss的精灵
    public AnimationData[] birdFrames;
    public GameObject attackBox;//boss的攻击碰撞箱
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
    private float curTime = 0f;
    private int curFrame = 0;
    private float waitAttack = 4f;//普攻攻击间隔，远程攻击*2
    public static float attack = 18f;//攻击力，远程攻击*0.75
    private float lastAttack = -1f;//上一次攻击时间
    private float blood = 1000f;//血量
    private int act = 0; //0待机，1行走，2攻击招式一，3攻击招式二，4站立受击，5站立死亡，6切换飞行，7飞行，8飞行攻击，9飞行受击，10飞行死亡
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }
    private void OnTriggerStay2D(Collider2D other)//近战检测
    {
        if (!other.CompareTag("Player") || (act != 0 && act != 1)) return;
        if (lastAttack >= 0f && Time.time - lastAttack <= waitAttack) return;
        InitBoss(2);
        lastAttack = Time.time;
    }

    // Update is called once per frame
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
        if (CheckAttack())
            InitBoss(2);
        if (curTime >= idelF)
            InitDelay();
    }
    private void Act1()//行走
    {
        if (CheckAttack())
            InitBoss(2);
        if (curTime >= moveF)
            InitDelay();
    }
    private void Act2()//招式一
    {
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
    private void Act3() { }
    private void Act4() { }
    private void Act5() { }
    private void Act6() { }
    private void Act7() { }
    private void Act8() { }
    private void Act9() { }
    private void Act10() { }

    private void InitDelay()
    {
        sr.sprite = birdFrames[act].frames[curFrame];
        curTime = 0f;
        curFrame = (curFrame + 1) % birdFrames[act].frames.Length;
    }
    private void InitBoss(int a)
    {
        act = a;
        curFrame = 0;
        curTime = 0f;
        sr.sprite = birdFrames[act].frames[curFrame];
    }
    private void InitBox(bool flag)//控制鸟怪的攻击碰撞箱的开关
    {
        if (attackBox.transform.localPosition.x < 0 ^ r) //朝向于碰撞箱方向相反
            attackBox.transform.localPosition = -attackBox.transform.localPosition;
        attackBox.SetActive(flag);
    }
    private bool CheckAttack()
    {

        return false;
    }
}

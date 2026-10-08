using System.Collections;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;


public class Bird : MonoBehaviour, IDamageable
{
    private int curFrame = 0;
    private float time = 0;
    private int act = 0;
    private bool r = true;//sprite是否朝右
    private bool face = true;//前一瞬间是否朝右
    private SpriteRenderer sr;//鸟怪的精灵
    public AnimationData[] birdFrames;//0飞行，1攻击，2受击，3死亡
    private Collider2D birdCol;//鸟怪自身的碰撞箱
    [SerializeField] private Image HP;
    private float beginBlood = 50f;//初始血量
    private float blood = 50f;
    private float waitTime = 6f;//攻击冷却时间
    private float lastAttack = -1f;
    private Vector2 startPlace;//攻击起始点
    private Vector2 direct;//攻击目标点的单位方向向量
    private float speed = 16f;//攻击的移动速度
    public GameObject attackBox;//攻击碰撞箱
    private Collider2D attackCol;
    public static float attack = 2f;//鸟怪攻击力
    [SerializeField] private float backSpeed = 5f;//被击退的速度
    private Player player;
    private bool damageActive = false;//是否处于有效攻击帧
    private bool hasHit = false;//是否已经命中
    void Start()
    {
        attackCol = attackBox.GetComponent<Collider2D>();
        player = Player.tran.GetComponent<Player>();
        birdCol = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
    }
    private void CheckHit()
    {
        if (hasHit) return;//没到有效攻击帧或已经攻击了
        Physics2D.SyncTransforms();
        ColliderDistance2D result = attackCol.Distance(Player.col);
        if (!result.isValid || !result.isOverlapped) return;//无效重叠或未重叠
        hasHit = true;
        damageActive = false;
        Player.attackedDirect = r;//击退方向与攻击方向一致
        player.TakeDamage(attack);
    }
    public void InitBird(Vector2 pos)//初始化鸟怪
    {
        transform.position = pos;
        blood = 50f;
        act = 0;
        curFrame = 0;
        time = 0f;
        gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;
        if (act == 0)//追击玩家
            Act0();
        else if (act == 1)//攻击玩家
            Act1();
        else if (act == 2)//受击，只播放一遍
            Act2();
        else if (act == 3)//死亡。只播放一遍
            Act3();
        if (damageActive)
            CheckHit();
    }
    private void InitBird()//更新鸟怪帧动画
    {
        r = transform.position.x <= Player.tran.position.x;
        if (r != face)
        {
            sr.flipX = !r;
            face = r;
        }
        sr.sprite = birdFrames[act].frames[curFrame];
        curFrame = (curFrame + 1) % birdFrames[act].frames.Length;
        time = 0f;
    }
    public void TakeDamage(float damage)
    {
        if (act == 3) return;
        Debug.Log("birdAttacked");
        blood = Mathf.Max(blood - damage, 0);
        HP.fillAmount = blood / beginBlood;
        if (blood <= 0f)
        {
            InitEnemy(3);
            return;
        }
        InitEnemy(2);
    } 
    void Die()
    {
        Level1Enemy.curBirds--;
        gameObject.SetActive(false);
    }
    private void InitEnemy(int a)//转变状态
    {
        if (act == 1)
            transform.position = (Vector3)startPlace;//回到攻击的起始位置
        act = a;
        curFrame = 0;
        time = 0;
        sr.sprite = birdFrames[act].frames[0];
        if (act == 0)
        {
            lastAttack = Time.time;//攻击冷却开始
            hasHit = false;
        }
    }
    private void Act0()//飞行
    {
        float r = Random.Range(0f, 10f);
        Vector2 dir;
        if (r <= 6)//大概率追击玩家
        {
            Vector2 v1 = Player.box.bounds.center - transform.position;
            Vector2 v2 = Player.box1.bounds.center - transform.position;
            dir = ((v1.magnitude < v2.magnitude) ? v1 : v2);
        }
        else//小概率随机飞行
        {
            dir = new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f));
        }
        dir.Normalize();
        transform.position += 1.5f * Time.deltaTime * (Vector3)dir;
        Physics2D.SyncTransforms();
        if ((lastAttack == -1 || Time.time - lastAttack >= waitTime) && (birdCol.Distance(Player.box).isOverlapped || birdCol.Distance(Player.box1).isOverlapped))//有重叠
        {
            InitEnemy(1);
            startPlace = transform.position;
            direct = (Vector2)Player.tran.position - startPlace;
            direct.Normalize();
        }
        if (time >= 0.05f) InitBird();
    }
    private void Act1()//攻击
    {
        transform.position += ((curFrame <= 7) ? 1 : -1) * speed * Time.deltaTime * (Vector3)direct;//前8帧冲向玩家，后8帧退回原地
        if (time >= 0.01f)
        {
            InitBird();
            if (curFrame == 0) InitEnemy(0);
            else if (curFrame == 4)//开启攻击碰撞箱
                InitBox(true);
            else if (curFrame == 13)//攻击结束
                InitBox(false);
        }
    }
    private void Act2()//受击
    {
        if (curFrame >= 5 && curFrame <= 11)
        {
            Vector3 pos = transform.position;
            pos.x += backSpeed * Time.deltaTime * (Player.tran.position.x < pos.x ? 1 : -1);
            transform.position = pos;
        }
        if (time >= 0.03f)
        {
            InitBird();
            if (curFrame == 0) InitEnemy(0);
        }
    }
    private void Act3()//死亡
    {
        if (time >= 0.02f)
        {
            InitBird();
            if (curFrame == 0) Die();
        }
    }
    private void InitBox(bool flag)//控制鸟怪的攻击碰撞箱的开关
    {
        damageActive = flag;
        Vector2 pos=attackCol.offset;
        pos.x = Mathf.Abs(pos.x) * (r ? 1f : -1f);
        attackCol.offset= pos;
    }
}

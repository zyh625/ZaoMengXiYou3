using System.Collections;
using System.Security.Cryptography;
using UnityEngine;


public class Bird : MonoBehaviour
{
    private int curFrame = 0;
    private float time = 0;
    private int act = 0;
    private bool r = true;
    private bool face = true;
    private SpriteRenderer sr;//鸟怪的精灵
    public AnimationData[] birdFrames;//0飞行，1攻击，2受击，3死亡
    private float blood = 50f;
    private float waitTime = 10f;//攻击冷却时间
    private float lastAttack = -1f;
    private Vector2 startPlace;//攻击起始点
    private Vector2 direct;//攻击目标点的单位方向向量
    private float speed = 10f;//移动速度
    public GameObject player;//攻击到的玩家
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("ReadyAttack")&&act!=1&&(lastAttack==-1||Time.time-lastAttack>waitTime)&&Player.tran.position.y+Player.HalfSr<=transform.position.y)
        {
            InitEnemy(1);
            startPlace= transform.position;
            direct = (Vector2)Player.tran.position - startPlace;
            direct.Normalize();
        }
    }
    public void InitBird(Vector2 pos)
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
    }
    private void InitBird()
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
        blood -= damage;
        if (blood <= 0f)
        {
            InitEnemy(3);
            Die();
            return;
        }
        InitEnemy(2);
    } 
    void Die()
    {
        gameObject.SetActive(false);
    }
    private void InitEnemy(int a)//转变状态
    {
        if (act == 1)
        {
            transform.position = (Vector3)startPlace;//回到攻击的起始位置
        }
        act = a;
        curFrame = 0;
        time = 0;
        sr.sprite = birdFrames[act].frames[0];
    }
    private void Act0()//飞行
    {
        float r = Random.Range(0f, 10f);
        Vector2 dir;
        if (r <= 6)//大概率追击玩家
        {
            Vector2 v1 = Player.Attacked.transform.position - transform.position;
            Vector2 v2 = Player.Attacked1.transform.position - transform.position;
            dir = ((v1.magnitude < v2.magnitude) ? v1 : v2);
        }
        else//小概率随机飞行
        {
            Debug.Log("fuck");
            dir = new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f));
        }
        dir.Normalize();
        transform.position += 1.5f * Time.deltaTime * (Vector3)dir;
        if (time >= 0.05f) InitBird();
    }
    private void Act1()//攻击
    {
        transform.position += ((curFrame <= 7) ? 1 : -1) * speed * Time.deltaTime * (Vector3)direct;//前8帧冲向玩家，后8帧退回原地
        if (time >= 0.01f)
        {
            InitBird();
            if (curFrame == 0) InitEnemy(0);
            else if (curFrame == 7)
            {
                Player.wasAttacked = true;
                Player.attackedDirect = new Vector2(direct.x >= 0 ? 1 : -1, 0);
            }
        }
    }
    private void Act2()//受击
    {
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
}

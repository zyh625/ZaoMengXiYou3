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
    private Collider2D col;//鸟怪的碰撞箱
    public AnimationData[] birdFrames;//0飞行，1攻击，2受击，3死亡
    private float blood = 50f;
    private float waitTime = 10f;//攻击冷却时间
    private float lastAttack = -1f;
    private Vector3 attackStart;
    private Vector3 attackTarget;
    private float attackTimer;

    private const float AttackFrameTime = 0.1f;
    private const int PrepareFrames = 3;
    private const int ForwardFrames = 5;
    private const int ReturnFrames = 5;
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("ReadyAttack")&&act!=1&&(lastAttack==-1||Time.time-lastAttack>waitTime)&&Player.tran.position.y+Player.HalfSr<=transform.position.y)
        {
            Debug.Log("readytoattack");
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
        else if (act == 1)
            Act1();
        else if (act == 2)//受击，只播放一遍
            Act2();
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
        if (act == 2 && curFrame == 0)
            InitEnemy(0);
    }
    public void TakeDamage(float damage)
    {
        blood -= damage;
        if (blood <= 0f)
        {
            Die();
            return;
        }
        InitEnemy(2);
    } 
    void Die()
    {
        gameObject.SetActive(false);
    }
    private void InitEnemy(int a)
    {
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
            dir = new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f));
        }
        dir.Normalize();
        transform.position += 1.5f * Time.deltaTime * (Vector3)dir;
        if (time >= 0.05f) InitBird();
    }
    private void Act1()//攻击
    {

    }
    private void Act2()//受击
    {

        if (time >= 0.01f) InitBird();
    }
}

using System.Collections;
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
    private float waitTime = 2f;//攻击冷却时间
    private bool attackR = true;//攻击的一瞬间角色是否在右侧
    private float lastAttack = -1f;
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")&&act!=1&&(lastAttack==-1||Time.time-lastAttack>waitTime)&&Player.tran.position.y+Player.HalfSr<=transform.position.y)
        {
            lastAttack= Time.time;
            //act = 1;
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
        if (act == 0)//追击玩家
        {
            float r = Random.Range(0f, 10f);
            Vector2 dir;
            if (r <= 7)//大概率追击玩家
                dir = Player.tran.position - transform.position;
            else//小概率随机飞行
                dir = new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f));
            dir.Normalize();
            transform.position += (Vector3)dir * 1.5f * Time.deltaTime;
        }
        time += Time.deltaTime;
        if (time > 0.5f)//更新帧动画
        {
            r = transform.position.x <= Player.tran.position.x;
            if (r != face)
            {
                sr.flipX = !r;
                face = r;
            }
            sr.sprite = birdFrames[act].frames[curFrame];
            if (act == 1)
            {

            }
            curFrame = (curFrame + 1) % birdFrames[act].frames.Length;
            time = 0f;
        }
    }
    public void TakeDamage(float damage)
    {
        //act = 2;
        blood -= damage;
        Debug.Log(blood);
        if (blood <= 0f)
        {
            Die();
        }
    } 
    void Die()
    {
        gameObject.SetActive(false);
    }
    private void BirdAttack()
    {
        Vector2 dir = Player.tran.position - transform.position;
        dir.Normalize();
        transform.position += (Vector3)dir * 2f * Time.deltaTime;

    }
}

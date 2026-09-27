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
            lastAttack= Time.time;

            // 固定本次攻击的起点和目标点。
            // 玩家之后移动，不改变这次突进路线。
            attackStart = transform.position;
            attackTarget = Player.tran.position;
            attackTarget.z = attackStart.z;
            attackTimer = 0f;
            time = 0f;
            curFrame = 0;
            act = 1;

            // 攻击过程中保持朝向，避免经过玩家时突然翻面。
            face = attackTarget.x >= attackStart.x;
            sr.flipX = !face;

            sr.sprite = birdFrames[1].frames[0];
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
        }
        else if (act == 1)
        {
            UpdateAttack();
        }
        else {
            time += Time.deltaTime;
            if (time >= 0.2f) InitBird();
        }
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
    private void UpdateAttack()
    {
        attackTimer += Time.deltaTime;

        float prepareTime = PrepareFrames * AttackFrameTime; // 0.3 秒
        float forwardTime = ForwardFrames * AttackFrameTime; // 0.5 秒
        float returnTime = ReturnFrames * AttackFrameTime;   // 0.5 秒

        float forwardEnd = prepareTime + forwardTime; // 0.8 秒
        float returnEnd = forwardEnd + returnTime;    // 1.3 秒

        // 1. 位置每个游戏帧更新，不再每 0.1 秒瞬移一次。
        if (attackTimer < prepareTime)
        {
            transform.position = attackStart;
        }
        else if (attackTimer < forwardEnd)
        {
            float t = (attackTimer - prepareTime) / forwardTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position =
                Vector3.Lerp(attackStart, attackTarget, t);
        }
        else if (attackTimer < returnEnd)
        {
            float t = (attackTimer - forwardEnd) / returnTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position =
                Vector3.Lerp(attackTarget, attackStart, t);
        }
        else
        {
            transform.position = attackStart;
        }

        // 2. 图片仍然每 0.1 秒切换一次。
        Sprite[] frames = birdFrames[1].frames;

        curFrame = Mathf.Min(
            Mathf.FloorToInt(attackTimer / AttackFrameTime),
            frames.Length - 1
        );

        sr.sprite = frames[curFrame];

        // 3. 动作返回完成，并且攻击图片播放完毕，再恢复飞行。
        float totalTime = Mathf.Max(
            returnEnd,
            frames.Length * AttackFrameTime
        );

        if (attackTimer >= totalTime)
        {
            transform.position = attackStart;

            act = 0;
            curFrame = 0;
            time = 0f;

            // 显示飞行动画第一帧，并更新帧索引。
            InitBird();
        }
    }
}

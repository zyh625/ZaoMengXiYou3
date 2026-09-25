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
    private float blood = 100f;
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }
    public void InitBird(Vector2 pos)
    {
        transform.position = pos;
        blood = 100f;
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
            Vector2 dir = Player.tran.position - transform.position;
            dir.Normalize();
            transform.position += (Vector3)dir * 1.8f * Time.deltaTime;
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
            curFrame = (curFrame + 1) % birdFrames[act].frames.Length;
            time = 0f;
        }
    }
    public void TakeDamage(float damage)
    {
        blood -= damage;
        if (blood <= 0f)
        {
            Die();
        }
    } 
    void Die()
    {
        gameObject.SetActive(false);
    }
}

using System.Xml;
using UnityEngine;
[System.Serializable]
public class BirdAct
{
    public Sprite[] frames;
}
public class Bird
{
    public GameObject bird;
    public Transform tran;
    public int curFrame = 0;
    public float time = 0;
    public int act=0;
    public bool r = true;
    public bool face = true;
}
public class Level1Enemy : MonoBehaviour
{
    public GameObject birdEnemy;//第一关小怪预制体
    public GameObject boss;//第一关boss
    public BirdAct[] birdFrames;//0飞行，1攻击，2受击，3死亡

    private Bird[] birds;//鸟怪实例
    private int curIdx = 0;//小怪当前可用的实例下标
    private float creat = 0f;//生成鸟怪的时间间隔
    public float flyDis = 0.03f;//飞行横坐标距离
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        birds = new Bird[10];//最多同时出现10只小怪
        for (int i = 0; i < 10; i++) birds[i] = new Bird();
    }
    void Update()
    {
        UpdateBird();
    }
    public void InitBird(Vector2 v)
    {
        if (birds[curIdx].bird== null)
        {
            birds[curIdx].bird = Instantiate(birdEnemy,v,Quaternion.identity);//实例化,无旋转
            birds[curIdx].tran = birds[curIdx].bird.transform;
            curIdx = (curIdx + 1) % 10;
        }
    }
    public void UpdateBird()
    {
        creat += Time.deltaTime;
        if (creat >= 6f)//每6秒生成一个鸟怪
        {
            Vector2 pos = Player.tran.position;
            pos.x += Random.Range(-3f, 3f);
            pos.y += 5f;
            InitBird(pos);
            creat = 0f;
        }
        for(int i = 0; i < 10; i++)
        {
            if (birds[i].bird != null)
            {
                if (birds[i].act == 0)
                {
                    Vector2 dir = Player.tran.position - birds[i].tran.position;
                    dir.Normalize();
                    birds[i].tran.position += (Vector3)dir * 2f * Time.deltaTime;
                }
                birds[i].time += Time.deltaTime;
                if (birds[i].time > 0.5f)//更新帧动画
                {
                    birds[i].r = birds[i].tran.position.x <= Player.tran.position.x;
                    if (birds[i].r != birds[i].face)
                    {
                        birds[i].bird.GetComponent<SpriteRenderer>().flipX = !birds[i].r;
                        birds[i].face = birds[i].r;
                    }
                    birds[i].bird.GetComponent<SpriteRenderer>().sprite = birdFrames[birds[i].act].frames[birds[i].curFrame];
                    birds[i].curFrame = (birds[i].curFrame + 1) % birdFrames[birds[i].act].frames.Length;
                    birds[i].time = 0f;
                }
            }
        }
    }
}

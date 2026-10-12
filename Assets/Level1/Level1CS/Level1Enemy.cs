using System.Xml;
using UnityEngine;

public class Level1Enemy : MonoBehaviour
{
    public Bird bird;
    private Bird[] birds;//鸟怪实例
    private float creat = 0f;
    public static int curBirds = 0;//当前有几只小鸟
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        birds = new Bird[10];//最多同时出现10只小怪
        for (int i = 0; i < 10; i++)
        {
            birds[i] = Instantiate(bird);
            birds[i].gameObject.SetActive(false);
        }
    }
    void Update()
    {
        CreatBird();
    }
    public void CreatBird()
    {
        if (curBirds == 10 || (curBirds >= 4 && Boss1.alive)) return;//Boss出现后鸟怪生成数量上限下降
        creat += Time.deltaTime;
        if (creat >= 8f)//每8秒在玩家上方随机生成一个鸟怪
        {
            Vector2 pos = Player.tran.position;
            pos.x += Random.Range(-3f, 3f);
            pos.y += 5f;
            for(int i = 0; i < birds.Length; i++)
            {
                if (!birds[i].gameObject.activeSelf)//找到一直未被使用的鸟怪
                {
                    curBirds++;
                    birds[i].InitBird(pos);break;
                }
            }
            creat = 0f;
        }
    }
}

using System.Xml;
using UnityEngine;

public class Level1Enemy : MonoBehaviour
{
    public Bird bird;
    private Bird[] birds;//鸟怪实例
    private float creat = 0f;
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
                    birds[i].InitBird(pos);break;
                }
            }
            creat = 0f;
        }
    }
}

using UnityEngine;

public class Boss1 : MonoBehaviour
{
    public static Boss1 Instance { get; private set; }
    public AnimationData[] birdFrames;//0待机，1行走，2攻击招式一，3攻击招式二，4站立受击，5站立死亡，6切换飞行，7飞行，8飞行攻击，9飞行受击，10飞行死亡
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}

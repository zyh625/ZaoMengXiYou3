using UnityEngine;

public class Boss1 : MonoBehaviour
{
    public static Boss1 Instance { get; private set; }
   /* private int curFrame = 0;
    private float time = 0;
    private int act = 0;
    private bool r = true;
    private bool face = true;
    private SpriteRenderer sr;//boos的精灵
    public AnimationData[] birdFrames;//0待机，1行走，2攻击，3受击，4死亡
    private float blood = 2000f;*/
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

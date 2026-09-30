using UnityEngine;

public class Boss1 : MonoBehaviour
{
    private SpriteRenderer sr;//boss的精灵
    public AnimationData[] birdFrames;
    
    [SerializeField] private float idelF = 0.05f;//待机频率
    [SerializeField] private float moveF = 0.05f;//移动频率
    [SerializeField] private float attack1F = 0.05f;//招式一频率
    [SerializeField] private float attack2F = 0.05f;//招式二频率
    [SerializeField] private float attackedF = 0.05f;//受击频率
    [SerializeField] private float dieF = 0.05f;//死亡频率
    [SerializeField] private float changeF = 0.05f;//切换形态频率
    [SerializeField] private float flyF = 0.05f;//飞行频率
    [SerializeField] private float flyAttackF = 0.05f;//飞行攻击频率
    [SerializeField] private float flyAttackedF = 0.05f;//飞行受击频率
    [SerializeField] private float flyDieF = 0.05f;//飞行死亡频率
    private float curTime = 0f;
    private int curFrame = 0;
    private int act; //0待机，1行走，2攻击招式一，3攻击招式二，4站立受击，5站立死亡，6切换飞行，7飞行，8飞行攻击，9飞行受击，10飞行死亡
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        curTime += Time.deltaTime;
        switch (act)
        {
            case 1:
                break;
        }
    }
    private void Act0()//待机
    {
        if (curTime >= idelF)
            InitDelay();
    }
    private void Act1()//行走
    {
        if (curTime >= moveF)
            InitDelay();
    }
    private void Act2()//招式一
    {
        if (curTime >= attack1F)
        {
            InitDelay();
            if(curFrame==5)
        }
    }

    private void InitDelay()
    {
        sr.sprite = birdFrames[act].frames[curFrame];
        curTime = 0f;
        curFrame = (curFrame + 1) % birdFrames[act].frames.Length;
    }
}

using Unity.IntegerTime;
using UnityEngine;

public class Exit : MonoBehaviour
{
    [SerializeField] private Sprite[] sprites;//出口的帧序列
    [SerializeField] private float changeF;
    private float curTime = 0f;
    private int curFrame = 0;
    private SpriteRenderer sr;
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }
    void Update()
    {
        curTime += Time.deltaTime;
        if(curTime>= changeF)
        {
            curTime = 0f;
            curFrame = (curFrame + 1) % sprites.Length;
            sr.sprite = sprites[curFrame];
        }
    }

}

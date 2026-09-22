using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.VisualScripting;
[System.Serializable]
public class AnimationData
{
    public Sprite[] frames;
}
public class GameManager : MonoBehaviour
{
    public AnimationData[] monkeys;//角色的所有动作的帧动画
    public GameObject monkeyPrefab;//预先创建好的角色实例

    private Transform tran;//角色位置
    private SpriteRenderer sr;//角色精灵
    public Rigidbody2D rb;//角色的重力系统

    public int act = 0;//0表示静止，1表示行走，2表示跑步，3表示普通跳跃，4表示二连跳跃，5表示攻击
    public float moveRate = 10;
    public int curFrame = 0;//当前帧序列
    public float delayTime = 0;
    public bool face = true;//角色朝向，开始向右
    public float left, right, top, bottom;//游戏边界
    void Start()
    {
        tran = monkeyPrefab.transform;
        sr = monkeyPrefab.GetComponent<SpriteRenderer>();
        rb = monkeyPrefab.GetComponent<Rigidbody2D>();
        float halfSr = sr.bounds.size.x / 2;
        float halfCm = Camera.main.orthographicSize * Camera.main.aspect;
        left = halfSr - halfCm;
        right = -left;
    }
    void Update()
    {
        UpdateMonkey();
    }
    public void Move(bool r,float speed)//角色水平移动
    {
        if (r != face)
        {
            sr.flipX = !r;
            face = r;
        }
        Vector3 pos = tran.position;
        pos.x += (r ? 1 : -1) * speed;
        pos.x = Mathf.Clamp(pos.x, left, right);
        tran.position = pos;
    }
    public void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 10f);
    }
    public void UpdateMonkey()
    {
        if (act >2||act==0) return;
        delayTime += Time.deltaTime;
        if (delayTime >= 0.5)
        {
            delayTime = 0;
            sr.sprite = monkeys[act].frames[curFrame];
            curFrame = (curFrame + 1) % monkeys[act].frames.Length;
        }
        
    }
}

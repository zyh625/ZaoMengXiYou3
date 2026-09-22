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

    public GameObject[] ground;//地面和最高层的地板
    public Camera cam;//相机
    private Transform camTran;
    private float camY;
    private float deltaY;//相机能达到的最高高度
    private float topY;

    public int act = 0;//0表示静止，1表示行走，2表示跑步，3表示普通跳跃，4表示二连跳跃，5表示攻击
    public int curFrame = 0;//当前帧序列
    public float delayTime = 0;
    public bool face = true;//角色朝向，开始向右
    public float left, right, top, bottom;//游戏边界
    void Start()
    {
        tran = monkeyPrefab.transform;
        sr = monkeyPrefab.GetComponent<SpriteRenderer>();
        rb = monkeyPrefab.GetComponent<Rigidbody2D>();
        camTran = cam.transform;
        camY = camTran.position.y;
        deltaY = ground[1].transform.position.y + camY - ground[0].transform.position.y;
        Debug.Log(deltaY);
        float halfSr = sr.bounds.size.x / 2;
        float halfCm = Camera.main.orthographicSize * Camera.main.aspect;
        left = halfSr - halfCm;
        right = -left;
        topY = ground[1].transform.position.y + ground[1].GetComponent<SpriteRenderer>().bounds.size.y / 2 + halfSr;
    }
    void Update()
    {
        UpdateMonkey();
        UpdateBg();
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
        if (act >2) return;
        delayTime += Time.deltaTime;
        if (delayTime >= 0.5)
        {
            delayTime = 0;
            sr.sprite = monkeys[act].frames[curFrame];
            curFrame = (curFrame + 1) % monkeys[act].frames.Length;
        }
        
    }
    public void UpdateBg()
    {
        if (camY >= deltaY) return;
        if (tran.position.y > ground[1].transform.position.y&&camY<deltaY)
        {
            camY += 0.01f;
            Vector3 pos = camTran.position;
            pos.y = camY;
            camTran.position = pos;
            return;
        }
        if (tran.position.y >camY)//角色保持在相机中心
        {
            camY = tran.position.y;
            Vector3 pos = camTran.position;
            pos.y = camY;
            camTran.position = pos;
        }
    }
}

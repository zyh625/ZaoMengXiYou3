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
    public Canvas canvas;
    public GameObject monkeyPrefab;
    private Transform tran;
    public int act = 0;//0表示静止，1表示行走，2表示跑步，3表示跳跃，4表示攻击
    public float left, right;
    public float moveRate = 10;
    public int curFrame = 0;//当前帧序列
    public float delayTime = 0;
    public bool face = true;//角色朝向，开始向右
    void Start()
    {
        tran = monkeyPrefab.transform;
        left = monkeyPrefab.GetComponent<RectTransform>().rect.width / 2;
        right = canvas.GetComponent<RectTransform>().rect.width - left;
    }
    void Update()
    {
        UpdateMonkey();
    }
    public void Move(bool r,float speed)//角色水平移动
    {
        if (r ^ face)
            monkeyPrefab.GetComponent<Image>().rectTransform.localScale = new Vector3(r ? 1 : -1, 1, 1);//面朝移动方向
        face = r;
        Vector3 pos = tran.position;
        pos.x += (r ? 1 : -1) * speed;
        pos.x = Mathf.Max(Mathf.Min(pos.x, right), left);
        tran.position = pos;
    }
    public void Jump()
    {

    }
    public void UpdateMonkey()
    {
        if (act != 0) return;
        delayTime += Time.deltaTime;
        if (delayTime >= 0.5)
        {
            delayTime = 0;
            monkeyPrefab.GetComponent<Image>().sprite = monkeys[act].frames[curFrame];
            curFrame = (curFrame + 1) % monkeys[act].frames.Length;
        }
        
    }
}

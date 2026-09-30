using System.Collections;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public Boss1 bossPrefab;//boss的预制体
    private Vector2 bossPos;//boos出现的位置
    public static float left, right, top, bottom;//游戏边界
    public static float deltaY, maxY, minY, topY;//相机中心到第一层的距离，相机能到达的最大高度，相机起始位置，角色
    public GameObject[] ground;//地面和最高层的地板
    public Camera cam;//相机
    private Transform camTran;
    private bool bossReady = false;//已经准备开始boss战
    private bool enterBoss = false;//是否进入boss的战斗场地
    [SerializeField] private float riseSpeed = 3f;//上移速度
    private float camY;//角色当前坐标y值
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        camTran = cam.transform;
        camY = camTran.position.y;
        deltaY = camY - ground[0].transform.position.y;
        maxY = deltaY + ground[1].transform.position.y;
        minY = camTran.position.y;
        float halfCm = Camera.main.orthographicSize
             * Camera.main.aspect;
        left = Player.HalfSr - halfCm;
        right = -left;
        topY = ground[1].transform.position.y + ground[1].GetComponent<SpriteRenderer>().bounds.size.y / 2 + Player.HalfSr;
        bossPos = ground[2].transform.position;
        bossPos.y += ground[2].GetComponent<SpriteRenderer>().bounds.size.y / 2 + bossPrefab.GetComponent<SpriteRenderer>().bounds.size.y / 2;
    }

    public void UpdateBg()
    {
        if (bossReady) return;
        if (Player.tran.position.y >= topY)
            enterBoss = true;
        if (enterBoss)
        {
            camY = Mathf.MoveTowards(camY, maxY, riseSpeed * Time.deltaTime);
            Vector3 pos = camTran.position;
            pos.y = camY;
            camTran.position = pos;
            if (camY >= maxY)
            {
                bossReady = true;
                StartCoroutine(BeginBoss());
            }
            return;
        }
        Trace();
    }
    public void Trace()//相机跟随角色
    {
        camY = Mathf.Clamp(Player.tran.position.y, minY, maxY);
        Vector3 pos = camTran.position;
        pos.y = camY;
        camTran.position = pos;
    }
    private IEnumerator BeginBoss()
    {
        yield return new WaitForSeconds(6f);//等待6秒boss现身
        Boss1.alive = true;
        Instantiate(bossPrefab, bossPos, Quaternion.identity);
        Debug.Log("boss");
    }
}

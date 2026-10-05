using System;
using UnityEngine;

public class Ball : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float boomF = 0.02f;
    [SerializeField] private float traceF = 0.05f;
    private float attack = 20f;
    public Sprite[] boom;//爆炸帧动画
    public Sprite[] trace;//追踪帧动画
    private bool trail = true;//是否在追踪
    private float curTime=0;
    private int curFrame = 0;
    private Vector2 direct;
    private SpriteRenderer sr;
    private bool hasHit = false;
    private float left, right;
    private Player player;
    void Awake()//只执行一次
    {
        player = Player.tran.GetComponent<Player>();
        sr = GetComponent<SpriteRenderer>();
    }
    void OnEnable()//每次开启都重置
    {
        direct= Player.tran.position - transform.position;
        direct.Normalize();
        hasHit = false;
        sr.sprite = trace[0];
        trail = true;
        curFrame = 0;
        curTime = 0;
    }
    void Start()
    {
        left = CameraManager.left - sr.bounds.size.x;
        right = CameraManager.right + sr.bounds.size.x;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (!other.CompareTag("Player")) return;
        player.TakeDamage(attack);
        hasHit = true;
        trail = false;
        curTime = 0;
        curFrame = 0;
    }
    void Update()
    {
        curTime += Time.deltaTime;
        if (trail)
        {
            transform.position += moveSpeed * Time.deltaTime * (Vector3)direct;
            if (curTime >= traceF)
            {
                if (transform.position.x < left || transform.position.x > right||transform.position.y<CameraManager.bottom||transform.position.y>CameraManager.top)
                    gameObject.SetActive(false);//超出边界
                curTime = 0;
                sr.sprite = trace[curFrame];
                curFrame = (curFrame + 1) % trace.Length;
            }
        }
        else
        {
            if (curTime >= boomF)
            {
                curTime = 0;
                sr.sprite = boom[curFrame];
                curFrame = (curFrame + 1) % boom.Length;
                if (curFrame == 0)
                    gameObject.SetActive(false);
            }
        }
    }
}

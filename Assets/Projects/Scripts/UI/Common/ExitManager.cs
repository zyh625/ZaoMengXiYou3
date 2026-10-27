using System.Collections.Generic;
using UnityEngine;

public class ExitManager : MonoBehaviour
{
    [SerializeField] private SpriteData sprites;
    private SpriteManager SM;
    private bool playerIn = false;
    private BoxCollider2D box;//出口自身的触发范围，用于排除换出角色留下的过期接触记录
    private PlayerManager playerManager;//由关卡创建出口时注入，预制体不保存场景引用
    private readonly HashSet<Collider2D> touchingBodies = new();//分别登记身体，旧角色离开不会覆盖新角色已经进入的状态

    public bool PlayerIn
    {
        get
        {
            PlayerController player = playerManager != null ? playerManager.CurrentPlayer : null;
            BoxCollider2D body = player != null ? player.GetComponent<BoxCollider2D>() : null;//只认人物本体的身体碰撞箱
            playerIn = body != null && body.enabled && body.gameObject.activeInHierarchy &&
                touchingBodies.Contains(body) && box != null && box.bounds.Intersects(body.bounds);
            return playerIn;
        }
    }

    private void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>(true);
        box = GetComponent<BoxCollider2D>();
    }

    private void OnEnable()
    {
        if (SM != null) SM.ChangeState(sprites);
    }

    private void OnDisable()
    {
        touchingBodies.Clear();
        playerIn = false;
    }

    public void SetPlayerManager(PlayerManager players)//不同关卡创建同一个出口预制体时绑定各自的角色系统
    {
        if (playerManager == players) return;
        playerManager = players;
        touchingBodies.Clear();
        playerIn = false;
    }

    private void OnTriggerEnter2D(Collider2D other) => TrackPlayerBody(other);
    private void OnTriggerStay2D(Collider2D other) => TrackPlayerBody(other);
    private void OnTriggerExit2D(Collider2D collision) => touchingBodies.Remove(collision);

    private void TrackPlayerBody(Collider2D other)//攻击箱、受击箱和敌人的碰撞都不会激活出口
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null || other != player.GetComponent<BoxCollider2D>()) return;
        touchingBodies.Add(other);
    }
}

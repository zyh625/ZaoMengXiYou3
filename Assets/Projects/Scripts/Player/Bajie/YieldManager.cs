using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]//角色完成移动和跳跃判断后，再更新领域效果
public class YieldManager : MonoBehaviour
{
    [SerializeField] private SpriteData yield;
    [SerializeField] private float duration;
    [SerializeField]private SpriteManager SM;
    [SerializeField] private PlayerManager playerManager;//当前角色由换人管理器统一提供
    [SerializeField, Min(0f)] private float groundTolerance = 0.25f;//脚底与领域地面线的最大高度差
    [SerializeField, Range(0f, 1f)] private float damageMultiplier = 0.5f;//领域内承受的伤害倍率
    private SpriteRenderer yieldRenderer;//用领域实际图像宽度判断生效范围
    private PlayerController protectedPlayer;//当前获得领域效果的角色
    private PlayerMovement protectedMovement;//缓存当前角色的移动判断
    private Rigidbody2D protectedBody;//跳跃或被挑飞后立即失去地面领域效果
    private BoxCollider2D protectedCollider;//使用角色碰撞箱的脚底高度
    private Health protectedHealth;//领域减伤单独交给生命组件处理
    private SpriteManager protectedSprite;//描边跟随角色当前帧图像
    private bool protectionApplied;//只在获得和失去效果时更新生命组件
    private PlayerManager boundPlayerManager;//记录已经订阅的管理器，运行时注入和重新启用不会重复订阅
    private static readonly Dictionary<SpriteManager, HashSet<YieldManager>> outlineSources = new();//同一角色可同时接受多个领域的黄色描边
    private SpriteManager outlinedSprite;//记录本领域实际登记过的描边对象，回收时只撤销自己的来源
    public bool YieldOpened => Yield && isActiveAndEnabled && SM != null &&
        SM.gameObject.activeInHierarchy && yieldRenderer != null && yieldRenderer.enabled;
    private float lastYield= -1f;
    private bool Yield = false;
    private void OnEnable()
    {
        ClearProtection();//池中重新启用先清除上一轮记录，不把旧角色的领域来源带入新一轮
        ResolveVisual();
        SetVisualActive(false);
        Yield = false;
        lastYield = -1f;
        BindPlayerManager(playerManager);
    }
    //禁用或退出场景时一并移除旧角色的描边和减伤。
    private void OnDisable()
    {
        BindPlayerManager(null);
        CloseYield();
    }
    public void SetPlayerManager(PlayerManager players)//预制体不保存场景引用，由所属关卡在实例化后注入
    {
        ClearProtection();
        playerManager = players;
        if (isActiveAndEnabled) BindPlayerManager(playerManager);
        if (Yield) RefreshProtection();
    }

    private void BindPlayerManager(PlayerManager players)//只维护一份换人通知，停用时保留配置但退订事件
    {
        if (boundPlayerManager != null) boundPlayerManager.CurrentPlayerChanged -= OnPlayerChanged;
        boundPlayerManager = players;
        if (boundPlayerManager != null) boundPlayerManager.CurrentPlayerChanged += OnPlayerChanged;
    }
    public void OpenYield(float x)//按下技能三
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ResolveVisual();
        if (SM == null || yieldRenderer == null || yield == null || yield.sprites == null || yield.sprites.Length == 0)
        {
            StopEffect();//配置不完整也归还对象池，不留下持续占用的无效实例
            return;
        }
        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
        SetVisualActive(true);
        yield.loop = true;
        SM.ChangeState(yield);
        lastYield = Time.time;
        Yield = true;
        RefreshProtection();
        if (duration <= 0f) StopEffect();//零持续时间不留下直到下一物理步的临时减伤
    }
    private void FixedUpdate()
    {
        if (Yield && Time.time - lastYield >= duration)
        {
            StopEffect();
            return;
        }
        if (Yield) RefreshProtection();
    }

    //渲染前再同步一次，换人、离开或跳起的当帧不会留下黄色描边。
    private void LateUpdate()
    {
        if (Yield) RefreshProtection();
    }

    //场景引用优先；备用查找允许领域图像初始关闭。
    private void ResolveVisual()
    {
        if (SM == null) SM = GetComponentInChildren<SpriteManager>(true);
        if (SM != null && yieldRenderer == null) yieldRenderer = SM.GetComponent<SpriteRenderer>();
    }

    private void SetVisualActive(bool active)//显示组件可以与管理器同物体；根物体的回收统一交给StopEffect
    {
        if (SM != null)
        {
            if (active)
            {
                if (!SM.gameObject.activeSelf) SM.gameObject.SetActive(true);//兼容旧的子Visual结构
                SM.Resume();
            }
            else SM.Pause();
        }
        if (yieldRenderer != null) yieldRenderer.enabled = active;
    }

    //换人后撤掉旧角色效果，并重新判断新角色是否站在领域上。
    private void OnPlayerChanged(PlayerController player) => RefreshProtection();

    //领域只管理当前角色，不依赖八戒是否仍在场上。
    private void RefreshProtection()
    {
        PlayerController player = playerManager == null ? null : playerManager.CurrentPlayer;
        if (protectedPlayer != player)
        {
            ClearProtection();
            protectedPlayer = player;
            protectedMovement = player == null ? null : player.GetComponent<PlayerMovement>();
            protectedBody = player == null ? null : player.GetComponent<Rigidbody2D>();
            protectedCollider = player == null ? null : player.GetComponent<BoxCollider2D>();
            protectedHealth = player == null ? null : player.GetComponent<Health>();
            protectedSprite = player == null ? null : player.BodySpriteManager;
        }

        bool standing = YieldOpened && protectedPlayer != null && protectedPlayer.gameObject.activeInHierarchy
            && protectedMovement != null && protectedMovement.AirState() == 0
            && protectedBody != null && Mathf.Abs(protectedBody.linearVelocityY) <= 0.1f
            && protectedCollider != null && yieldRenderer != null && protectedHealth != null && !protectedHealth.Dead;
        if (standing)
        {
            Bounds bounds = yieldRenderer.bounds;
            float centerX = protectedCollider.bounds.center.x;
            standing = centerX >= bounds.min.x && centerX <= bounds.max.x
                && Mathf.Abs(protectedCollider.bounds.min.y - SM.transform.position.y) <= groundTolerance;
        }
        if (!standing)
        {
            ClearProtection();
            return;
        }
        if (!protectionApplied)
        {
            protectedHealth.SetDamageMultiplier(this, damageMultiplier);
            protectionApplied = true;
        }
        RefreshOutline();
    }

    private void RefreshOutline()//所有仍然生效的领域共同持有描边，一个领域退出不能关闭另一个领域的提示
    {
        if (protectedSprite == null || protectedSprite.GetComponent<SpriteOutlineController>() == null) return;
        if (outlinedSprite != protectedSprite)
        {
            ClearOutline();
            if (!outlineSources.TryGetValue(protectedSprite, out HashSet<YieldManager> sources))
            {
                sources = new HashSet<YieldManager>();
                outlineSources.Add(protectedSprite, sources);
            }
            sources.Add(this);
            outlinedSprite = protectedSprite;
        }
        protectedSprite.SetYellow();
        protectedSprite.SetAngry(true);
    }

    private void ClearOutline()//最后一个领域来源离开时才关闭角色描边，换人和池回收同样走这里
    {
        SpriteManager previousSprite = outlinedSprite;
        outlinedSprite = null;
        if (ReferenceEquals(previousSprite, null)) return;
        if (outlineSources.TryGetValue(previousSprite, out HashSet<YieldManager> sources))
        {
            sources.Remove(this);
            if (sources.Count > 0) return;
            outlineSources.Remove(previousSprite);
        }
        if (previousSprite != null) previousSprite.SetAngry(false);
    }

    //仅清理领域自己施加的增益，不改动其他角色状态。
    private void ClearProtection()
    {
        if (protectionApplied && protectedHealth != null) protectedHealth.RemoveDamageMultiplier(this);
        ClearOutline();
        protectionApplied = false;
    }

    //清除这一轮领域自身的数据，OnDisable调用时不递归切换根物体。
    private void CloseYield()
    {
        ClearProtection();
        Yield = false;
        lastYield = -1f;
        SetVisualActive(false);
        protectedPlayer = null;
        protectedMovement = null;
        protectedBody = null;
        protectedCollider = null;
        protectedHealth = null;
        protectedSprite = null;
    }

    public void StopEffect()//领域结束后停用整实例归还对象池，不影响其他仍生效的领域
    {
        CloseYield();
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}

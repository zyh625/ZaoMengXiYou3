using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]//先停用场景中的备用角色，避免它在相机初始化前执行Awake
[RequireComponent(typeof(GoldManager))]
public class PlayerManager : MonoBehaviour
{
    [SerializeField] private WukongController WuKong;
    [SerializeField] private BajieController Bajie;
    [SerializeField] private ShaController Sha;//沙僧加入前后切换名单，允许复用场景实例或填预制体
    [SerializeField] private TangController Tang;//唐僧与其他角色一样创建一次，换出后保留自己的资源和冷却
    [SerializeField] private PlayerId[] switchOrder = { PlayerId.WuKong, PlayerId.BaJie, PlayerId.Sha, PlayerId.Tang };//E沿此顺序循环，Q反向；以后可从玩家设置传入新顺序
    [SerializeField] private YieldManager yieldManager;//可填场景领域实例或Field预制体，换人时保持独立存在
    [Header("独立技能特效预制体")]
    [SerializeField] private SkullManager skull;//麻痹药剂，由管理器按需创建并复用
    [SerializeField] private BoomerangManager boomerang;//回风摄魂，独立于施法角色存活
    [SerializeField] private QuicksandManager quicksand;//流沙领域，换人不会移除
    [SerializeField] private TangNormalAttackManager tangNormalAttack;//唐僧普攻能量波，与独立技能效果共用创建和回收入口
    [SerializeField] private Vector2 tangNormalAttackSpawnOffset = new(2.2f, 0.3f);//相对唐僧根节点的世界坐标偏移，朝左时只反转X
    [SerializeField] private Vector2 skullSpawnOffset = new(1.6f, -0.12f);//按沙僧第11帧手部位置设置，相对碰撞箱中心，朝左时水平取反
    [SerializeField] private Vector2 boomerangSpawnOffset = new(1.5f, -0.4f);//按第12帧持杖高度设置，回旋镖沿这个生成高度往返
    [SerializeField, Min(0f)] private float poisonDuration = 5f, poisonDamagePerStack = 1f;//两种施毒技能共用同一中毒参数
    [SerializeField] private LayerMask effectGroundLayers = 64;//领域只投放到实际地面
    [SerializeField, Min(0.1f)] private float groundSearchDistance = 30f;//空中施法时寻找脚下地面的最大距离
    private Transform effectsRoot;//所有独立技能效果的共同容器，不随角色停用
    private readonly List<SkullManager> skulls = new();//分别复用各技能的空闲实例
    private readonly List<BoomerangManager> boomerangs = new();
    private readonly List<QuicksandManager> quicksands = new();
    private readonly List<TangNormalAttackManager> tangNormalAttacks = new();//每次攻击可拥有独立飞行实例，命中或超距停用后复用
    private WukongController wukong;
    private BajieController bajie;
    private ShaController sha;//只保留一个实例，换出后保留血量、法力与技能冷却
    private TangController tang;//唐僧实例由同一个管理器启用和停用，不单独订阅换人输入
    private readonly List<PlayerController> players = new();//根据switchOrder整理已配置角色，不依赖固定角色编号位置
    private int curPlayer = 1;
    private bool initialized;//角色只创建一次，避免重复初始化生成多组玩家
    private int lastSwitchFrame = -1;//同一帧只响应一次换人，防止新角色重复读取同一次Q/E
    private InputReader currentInput;//只订阅当前角色的换人请求
    private Health currentHealth;//失败只监听当前角色，换出后退订旧角色
    [SerializeField] private string defeatScene = "DefeatScene";//当前角色死亡时进入的失败场景
    [SerializeField, Min(0f)] private float switchInvincibleDuration = 0.3f;//角色成功切入后短暂免疫整次攻击
    private bool defeatRequested, defeatLoaded;//先完成本次伤害和反伤，再在本帧切换失败场景

    public PlayerController CurrentPlayer => curPlayer > 0 && curPlayer <= players.Count ? players[curPlayer - 1] : null;//关卡和敌人使用的唯一当前角色
    public IReadOnlyList<PlayerController> Players => players;//结算读取四人真实资源，不启用备用角色的输入与动作
    public IReadOnlyList<PlayerId> SwitchOrder => Array.AsReadOnly(switchOrder ?? Array.Empty<PlayerId>());//设置界面可读取当前顺序，通过SetSwitchOrder提交调整
    public event Action<PlayerController> CurrentPlayerChanged;//切换完成后通知关卡更新追击目标等引用
    public static string LastBattleScene { get; private set; }//直接运行关卡时，失败界面也能找到正确的重试场景

    private void Awake()
    {
        if (GetComponent<GoldManager>() == null) gameObject.AddComponent<GoldManager>();//兼容旧关卡中尚未补挂的管理器，新增关卡由RequireComponent自动带上
        //允许直接复用场景里的角色；在初始化前先停用，避免场景八戒同时响应输入。
        if (WuKong != null && WuKong.gameObject.scene.IsValid()) WuKong.gameObject.SetActive(false);
        if (Bajie != null && Bajie.gameObject.scene.IsValid()) Bajie.gameObject.SetActive(false);
        if (Sha != null && Sha.gameObject.scene.IsValid()) Sha.gameObject.SetActive(false);
        if (Tang != null && Tang.gameObject.scene.IsValid()) Tang.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (initialized)
        {
            BindInput(CurrentPlayer);
            BindHealth(CurrentPlayer);
        }
    }

    private void OnDisable()
    {
        BindInput(null);
        BindHealth(null);
    }

    //由关卡在Start中传入出生点，此时场景相机已完成Awake。
    public void InitPlayer(Transform tran)
    {
        if (initialized) return;
        if (WuKong == null || Bajie == null || tran == null)
        {
            Debug.LogError("PlayerManager缺少悟空、八戒或出生点引用", this);
            return;
        }

        GameObject characters = new("Characters");//停用的创建容器，防止非当前角色提前启用输入
        characters.transform.SetParent(transform, false);
        characters.SetActive(false);
        wukong = WuKong.gameObject.scene.IsValid() ? WuKong : Instantiate(WuKong, characters.transform);
        bajie = Bajie.gameObject.scene.IsValid() ? Bajie : Instantiate(Bajie, characters.transform);
        if (Sha != null) sha = Sha.gameObject.scene.IsValid() ? Sha : Instantiate(Sha, characters.transform);
        if (Tang != null) tang = Tang.gameObject.scene.IsValid() ? Tang : Instantiate(Tang, characters.transform);
        players.Add(wukong);
        players.Add(bajie);
        if (sha != null)
        {
            players.Add(sha);
            sha.GetComponent<ShaCombat>().SetEffectManager(this);//特效引用由场景管理器注入，人物预制体不保存场景对象
        }
        if (tang != null)
        {
            players.Add(tang);
            tang.GetComponent<TangCombat>().SetEffectManager(this);//角色只报告发射时机，不自行创建或持有场景投射物
        }
        ApplySwitchOrder(switchOrder);//漏填、重复或未配置的角色不会使实际可切换名单丢失成员
        if (yieldManager != null)
        {
            if (!yieldManager.gameObject.scene.IsValid()) yieldManager = Instantiate(yieldManager);//其他关卡可直接使用预制体
            yieldManager.transform.SetParent(GetEffectsRoot(), true);//领域与投射物由同一个管理器持有
            yieldManager.SetPlayerManager(this);//预制体不能保存场景引用，创建角色时统一注入
        }
        bajie.GetComponent<BajieCombat>().SetYieldManager(yieldManager);
        foreach (PlayerController player in players)
        {
            player.transform.SetParent(characters.transform, true);
            player.gameObject.SetActive(false);
        }
        characters.SetActive(true);

        curPlayer = players.IndexOf(wukong) + 1;//调整队列顺序不会改变本关初始角色仍为悟空
        wukong.transform.position = tran.position;
        wukong.gameObject.SetActive(true);
        wukong.ResetForSwitch();
        initialized = true;
        BindInput(wukong);
        BindHealth(wukong);
        CurrentPlayerChanged?.Invoke(wukong);
    }

    public void SwitchPri() => SwitchRelative(-1);//Q向前循环，保留各角色自己的冷却时间
    public void SwitchNext() => SwitchRelative(1);//E向后循环
    private void SwitchRelative(int direction)//只有已初始化的实际角色参与切换
    {
        if (players.Count == 0) return;
        SwitchPlayer((curPlayer - 1 + direction + players.Count) % players.Count + 1);
    }

    public bool SetSwitchOrder(IReadOnlyList<PlayerId> order)//以后设置界面传入完整新队列即可；重排不换人、不重置资源或冷却
    {
        if (!initialized || order == null || order.Count != players.Count) return false;
        HashSet<PlayerId> seen = new();//仅在玩家提交设置时校验，战斗中的Q/E仍直接按下标切换
        foreach (PlayerId id in order)
        {
            if (!seen.Add(id) || !players.Exists(player => player.GetComponent<PlayerResources>().Id == id)) return false;
        }
        ApplySwitchOrder(order);
        return true;
    }

    private void ApplySwitchOrder(IReadOnlyList<PlayerId> order)//保留当前角色引用，避免重排下标后把界面或敌人目标误指向别人
    {
        PlayerController current = CurrentPlayer;
        List<PlayerController> ordered = new(players.Count);//整理一次配置，不在每帧重新查找角色
        if (order != null)
            foreach (PlayerId id in order)
            {
                PlayerController player = players.Find(candidate => candidate.GetComponent<PlayerResources>().Id == id);
                if (player != null && !ordered.Contains(player)) ordered.Add(player);
            }
        foreach (PlayerController player in players)
            if (!ordered.Contains(player)) ordered.Add(player);
        players.Clear();
        players.AddRange(ordered);
        switchOrder = new PlayerId[players.Count];
        for (int i = 0; i < players.Count; i++) switchOrder[i] = players[i].GetComponent<PlayerResources>().Id;
        if (current != null) curPlayer = players.IndexOf(current) + 1;
    }

    //先交接移动数据，再清理和隐藏旧角色；整个过程在一次输入回调内完成。
    private void SwitchPlayer(int nextPlayer)
    {
        if (!initialized || defeatRequested || lastSwitchFrame == Time.frameCount || nextPlayer < 1 || nextPlayer > players.Count) return;
        PlayerController previous = CurrentPlayer;//切换前仍在场上的角色
        PlayerController next = players[nextPlayer - 1];//即将接收控制权的角色
        if (previous == null || next == null || previous == next || !previous.gameObject.activeInHierarchy) return;
        if (previous.GetComponent<Health>().Status?.BlocksMovement == true) return;//冰冻和融化期间不能通过换人跳过位移限制或提前移除冰块
        if (previous.GetComponent<Health>().Dead || next.GetComponent<Health>().Dead) return;

        lastSwitchFrame = Time.frameCount;
        float footY = previous.GetComponent<BoxCollider2D>().bounds.min.y;//保持脚底高度，避免不同碰撞箱切换时陷进地板
        next.transform.position = previous.transform.position;
        next.gameObject.SetActive(true);//首次启用会先完成Awake，之后才能调用刚体和控制器的方法
        next.ResetForSwitch();
        next.GetComponent<PlayerMovement>().CopyStateFrom(previous.GetComponent<PlayerMovement>());
        Physics2D.SyncTransforms();
        Vector3 position = next.transform.position;//根据新旧碰撞箱高度对齐脚底
        position.y += footY - next.GetComponent<BoxCollider2D>().bounds.min.y;
        next.transform.position = position;
        Physics2D.SyncTransforms();
        next.BodySpriteManager.ChangeSpriteFace(next.GetComponent<PlayerMovement>().FaceRight);

        previous.ResetForSwitch();
        ResetSwitchResources(previous);
        ResetSwitchResources(next);
        next.GetComponent<Health>().GrantInvincibility(this, switchInvincibleDuration);//启用及位置交接完成后再授予，避免被OnEnable重置
        previous.GetComponent<Health>().PreserveForSwitch();
        curPlayer = nextPlayer;
        BattleRun.SetCurrentPlayer(next);//切换当下结清上一角色使用时长，不能用下一帧的角色代替
        BindInput(next);
        BindHealth(next);
        previous.gameObject.SetActive(false);
        CurrentPlayerChanged?.Invoke(next);
    }

    private void ResetSwitchResources(PlayerController player)//换出和切入双方都归一，切回时不会沿用旧气力或无双
    {
        player.GetComponent<PlayerResources>()?.ResetVigor();
        player.GetComponent<PlayerBoost>()?.ResetBoost();
    }

    //始终只有当前角色能发起切换；切换、禁用管理器时及时退订旧输入。
    private void BindInput(PlayerController player)
    {
        if (currentInput != null)
        {
            currentInput.SwitchPriPressed -= SwitchPri;
            currentInput.SwitchNextPressed -= SwitchNext;
        }
        currentInput = player == null ? null : player.GetComponent<InputReader>();
        if (currentInput != null)
        {
            currentInput.SwitchPriPressed += SwitchPri;
            currentInput.SwitchNextPressed += SwitchNext;
        }
    }

    private void BindHealth(PlayerController player)//不靠角色停用或死亡动画结束来判断失败
    {
        if (currentHealth != null) currentHealth.Died -= OnCurrentPlayerDied;
        currentHealth = player == null ? null : player.GetComponent<Health>();
        if (currentHealth != null) currentHealth.Died += OnCurrentPlayerDied;
    }

    private void OnCurrentPlayerDied(DamageInfo damage) => FailCurrentPlayer();

    public void FailCurrentPlayer()//关卡也可以使用同一个失败入口，避免重复切场景
    {
        if (defeatRequested) return;
        BattleRun.Finish(false);//死亡立即冻结本轮数据，失败场景的停留时间不再计入闯关耗时
        GetComponent<GoldManager>()?.EndRun();//取消尚未完成的吸附，结束后不能补记金币
        defeatRequested = true;
        BindInput(null);
    }

    private void LateUpdate()
    {
        if (!defeatRequested || defeatLoaded) return;
        defeatLoaded = true;
        LastBattleScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(defeatScene);//死亡当帧结束战斗，不等待人物动画，也不能切人续命
    }

    //初始化时统一提高所有可换角色的显示顺序，不在每次切换时累加。
    public void SetSortingOrder(int offset)
    {
        foreach (PlayerController player in players)
            player.BodySpriteManager.GetComponent<SpriteRenderer>().sortingOrder += offset;
    }

    private Transform GetEffectsRoot()//容器只创建一次；不放进任何人物的子层级
    {
        if (effectsRoot != null) return effectsRoot;
        GameObject effects = new("Effects");
        effects.transform.SetParent(transform, false);
        effectsRoot = effects.transform;
        return effectsRoot;
    }

    private T GetEffect<T>(T prefab, List<T> pool) where T : MonoBehaviour
    {
        if (prefab == null)
        {
            Debug.LogError($"PlayerManager未配置{typeof(T).Name}预制体", this);
            return null;
        }
        foreach (T effect in pool)
            if (effect != null && !effect.gameObject.activeSelf) return effect;
        T created = Instantiate(prefab, GetEffectsRoot());
        created.gameObject.SetActive(false);
        pool.Add(created);
        return created;
    }

    private static Vector2 EffectPosition(Transform caster, Vector2 offset, bool right)//以实际人物碰撞箱中心定位，兼容角色大小差异
    {
        BoxCollider2D body = caster.GetComponent<BoxCollider2D>();
        Vector2 center = body != null ? (Vector2)body.bounds.center : (Vector2)caster.position;
        return center + new Vector2(right ? offset.x : -offset.x, offset.y);
    }

    private static void SetEffectSorting(MonoBehaviour effect, Transform caster)//与当前人物使用同一排序层，并把独立特效显示在人物前方
    {
        SpriteRenderer visual = caster.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (visual == null) visual = caster.GetComponentInChildren<SpriteRenderer>(true);
        SpriteRenderer effectVisual = effect.GetComponentInChildren<SpriteRenderer>(true);
        if (visual == null || effectVisual == null) return;
        effectVisual.sortingLayerID = visual.sortingLayerID;
        effectVisual.sortingOrder = visual.sortingOrder + 1;
    }

    public void SpawnSkull(Transform caster, bool right)
    {
        if (caster == null || defeatRequested) return;
        SkullManager effect = GetEffect(skull, skulls);
        if (effect == null) return;
        SetEffectSorting(effect, caster);
        effect.Launch(caster, EffectPosition(caster, skullSpawnOffset, right), right, poisonDuration, poisonDamagePerStack);
    }

    public void SpawnBoomerang(Transform caster, bool right)
    {
        if (caster == null || defeatRequested) return;
        BoomerangManager effect = GetEffect(boomerang, boomerangs);
        if (effect == null) return;
        SetEffectSorting(effect, caster);
        effect.Launch(caster, EffectPosition(caster, boomerangSpawnOffset, right), right);
    }

    public void SpawnQuicksand(Transform caster)//脚下有地面才生成，领域本身不会跟着角色移动
    {
        if (caster == null || defeatRequested) return;
        BoxCollider2D body = caster.GetComponent<BoxCollider2D>();
        Vector2 origin = body != null ? (Vector2)body.bounds.center : (Vector2)caster.position;
        RaycastHit2D ground = Physics2D.Raycast(origin, Vector2.down, groundSearchDistance, effectGroundLayers);
        if (ground.collider == null) return;
        QuicksandManager effect = GetEffect(quicksand, quicksands);
        if (effect == null) return;
        SetEffectSorting(effect, caster);
        effect.Launch(caster, ground.point, poisonDuration, poisonDamagePerStack);
    }

    public void SpawnTangNormalAttack(Transform caster, bool right)//一轮普攻仅由唐僧Combat在有效帧调用一次
    {
        if (caster == null || !caster.gameObject.activeInHierarchy || defeatRequested) return;
        TangNormalAttackManager effect = GetEffect(tangNormalAttack, tangNormalAttacks);
        if (effect == null) return;
        SetEffectSorting(effect, caster);
        Vector2 position = (Vector2)caster.position + new Vector2(
            right ? tangNormalAttackSpawnOffset.x : -tangNormalAttackSpawnOffset.x, tangNormalAttackSpawnOffset.y);
        effect.Launch(caster, position, right);//Launch只设置位置与朝向，不改变预制体已经调好的缩放
    }
}

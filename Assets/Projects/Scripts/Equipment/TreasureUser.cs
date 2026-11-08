using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

//法宝的使用：装备后法宝悬浮在角色脑后；当前角色按 H 使用。使用后法宝隐藏，冷却结束前不能卸下或更换。
//五件法宝的效果数值都在 TreasureRules；这里负责效果本身和特效。由 PlayerStats 自动挂到每个角色上。
[DefaultExecutionOrder(50)]//在角色移动之后执行，御剑时能覆盖竖直速度
public sealed class TreasureUser : MonoBehaviour
{
    //---------- 四个角色的冷却：静态保存，换人后仍在计时；换场景清零 ----------
    private static readonly float[] readyAt = new float[4];
    public static bool IsLocked(PlayerId id) => Time.time < readyAt[Mathf.Clamp((int)id, 0, 3)];
    public static float Remaining(PlayerId id) => Mathf.Max(0f, readyAt[Mathf.Clamp((int)id, 0, 3)] - Time.time);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        System.Array.Clear(readyAt, 0, readyAt.Length);
        SceneManager.sceneLoaded -= ClearCooldowns;
        SceneManager.sceneLoaded += ClearCooldowns;
    }
    private static void ClearCooldowns(Scene scene, LoadSceneMode mode) => System.Array.Clear(readyAt, 0, readyAt.Length);

    private PlayerController player;
    private PlayerResources resources;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerStats stats;
    private Health health;
    private Rigidbody2D body;
    private SpriteRenderer bodyRender;
    private static PlayerManager party;

    private SpriteRenderer follower, followerGlow;
    private Vector3 followPos;
    private bool followPlaced;
    private EquipmentItem item;
    private EquipmentDatabase.Item def;

    //当前正在生效的法宝效果
    private bool active;
    private TreasureKind kind;
    private float startedAt, endsAt, nextMote, healPerSecond, shieldWindow;
    private float savedGravity, flightBaseY;
    private bool shieldUp;
    private readonly List<FxInstance> loops = new();
    private SpriteRenderer swordRender;

    private PlayerId Id => resources != null ? resources.Id : PlayerId.WuKong;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        resources = GetComponent<PlayerResources>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        if (player != null && player.BodySpriteManager != null) bodyRender = player.BodySpriteManager.GetComponent<SpriteRenderer>();
        if (bodyRender == null) bodyRender = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        EquipmentService.Changed += RefreshItem;
        if (health != null) { health.DamageBlocked += BlockWithUmbrella; health.Died += OnDied; }
        RefreshItem();
        followPlaced = false;
    }

    private void OnDisable()//换下场、死亡、离开关卡：正在生效的效果立即结束，冷却照常计时
    {
        EquipmentService.Changed -= RefreshItem;
        if (health != null) { health.DamageBlocked -= BlockWithUmbrella; health.Died -= OnDied; }
        EndEffect();
        if (follower != null) follower.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (follower != null) Destroy(follower.gameObject);
        if (swordRender != null) Destroy(swordRender.gameObject);
    }

    private void OnDied(DamageInfo damage) => EndEffect();

    private void RefreshItem()
    {
        SaveData data = GameSession.instance != null ? GameSession.instance.data : null;
        item = data != null ? EquipmentService.Equipped(data, Id, EquipSlot.Treasure) : null;
        def = item != null ? EquipmentDatabase.Instance?.Find(item.id) : null;
        if (!TreasureRules.Is(def)) { item = null; def = null; }
    }

    private bool InBattle => BattleRun.IsRunning || TrainingSession.Active;
    private bool IsCurrent
    {
        get
        {
            if (party == null) party = FindAnyObjectByType<PlayerManager>();
            return party != null && party.CurrentPlayer == player;
        }
    }

    private void Update()
    {
        UpdateFollower();
        if (active) UpdateEffect();
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame && IsCurrent) TryUse();
    }

    //---------- 脑后的法宝 ----------
    private void UpdateFollower()
    {
        bool show = def != null && def.icon != null && !IsLocked(Id) && InBattle && bodyRender != null && bodyRender.enabled
                    && health != null && !health.Dead;
        if (!show)
        {
            if (follower != null && follower.gameObject.activeSelf) follower.gameObject.SetActive(false);
            followPlaced = false;
            return;
        }
        if (follower == null)
        {
            follower = new GameObject("TreasureFollower").AddComponent<SpriteRenderer>();
            followerGlow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            followerGlow.transform.SetParent(follower.transform, false);
            followerGlow.sprite = FxTextures.Glow;
        }
        if (!follower.gameObject.activeSelf) follower.gameObject.SetActive(true);
        follower.sprite = def.icon;
        Vector2 dim = def.icon.bounds.size;
        float size = FollowerSize;
        float scale = size / Mathf.Max(0.01f, Mathf.Max(dim.x, dim.y));//最长边约为身高的 45%
        follower.transform.localScale = Vector3.one * scale;
        followerGlow.transform.localScale = Vector3.one * (size * 1.7f / scale);
        Color c = TreasureRules.Color;
        followerGlow.color = new Color(c.r, c.g, c.b, 0.35f + 0.15f * Mathf.Sin(Time.time * 3f));
        follower.sortingLayerID = followerGlow.sortingLayerID = bodyRender.sortingLayerID;
        follower.sortingOrder = bodyRender.sortingOrder - 1;//在头的后面
        followerGlow.sortingOrder = bodyRender.sortingOrder - 2;

        Bounds b = bodyRender.bounds;
        float dir = movement != null && !movement.FaceRight ? -1f : 1f;
        //头部斜后上方，和身体留出空隙
        Vector3 target = new(b.center.x - dir * (b.extents.x * 0.9f + size * 0.55f), b.max.y + size * 0.15f + 0.1f * Mathf.Sin(Time.time * 2.6f), transform.position.z);
        followPos = followPlaced ? Vector3.Lerp(followPos, target, 1f - Mathf.Exp(-12f * Time.deltaTime)) : target;//稍微滞后地跟着，有漂浮感
        followPlaced = true;
        follower.transform.position = followPos;
    }

    //---------- 使用 ----------
    private void TryUse()
    {
        if (def == null || !InBattle || GamePause.IsPaused || health == null || health.Dead || active) return;
        if (IsLocked(Id)) { EquipmentToast.Message($"法宝冷却中，还需 {Mathf.CeilToInt(Remaining(Id))} 秒", Color.white); return; }
        PlayerState state = player != null ? player.State() : PlayerState.Idle;
        if (state == PlayerState.Die || state == PlayerState.Attacked || health.Status != null && health.Status.BlocksActions) return;

        kind = TreasureRules.Kind(def);
        int lv = TreasureRules.Level(item);
        readyAt[(int)Id] = Time.time + TreasureRules.Cooldown(kind, lv);
        startedAt = Time.time;
        endsAt = Time.time + TreasureRules.Duration(kind, lv);
        active = true;
        UseFlash();
        switch (kind)
        {
            case TreasureKind.Leaf: StartLeaf(lv); break;
            case TreasureKind.Gourd: StartGourd(lv); break;
            case TreasureKind.Sword: StartSword(lv); break;
            case TreasureKind.Umbrella: StartUmbrella(lv); break;
            default: RingBell(lv); active = false; break;//铃铛是瞬发的
        }
    }

    private void UseFlash()//法宝从脑后飞出放大消失，提示已经使用
    {
        if (def.icon == null) return;
        Vector3 at = follower != null && follower.gameObject.activeInHierarchy ? follower.transform.position : Center + Vector3.up * 0.6f;
        FxSpec s = Fx.Make(null, at, FollowerSize, Color.white);
        s.sprite = def.icon; s.life = 0.45f; s.growTo = 2.2f; s.fadeOut = 0.35f; s.drift = new Vector2(0f, 1.2f);
        Fx.SortLike(ref s, bodyRender, 6);
        Fx.Spawn(s);
        FxSpec glow = Fx.Make("sun", at, FollowerSize * 2.2f, TreasureRules.Color);
        glow.life = 0.35f; glow.fadeOut = 0.25f;
        Fx.SortLike(ref glow, bodyRender, 5);
        Fx.Spawn(glow);
    }

    private float FollowerSize => Mathf.Max(0.9f, BodyHeight * 0.45f);

    private Vector3 Center => bodyRender != null ? bodyRender.bounds.center : transform.position;
    private Vector3 Feet => bodyRender != null ? new Vector3(bodyRender.bounds.center.x, bodyRender.bounds.min.y, 0f) : transform.position;
    private float BodyHeight => bodyRender != null ? bodyRender.bounds.size.y : 1.5f;

    private FxInstance Loop(string clip, Vector3 localOffset, float size, Color color, int order, float squash = 1f)//跟随角色的循环特效，效果结束时统一停止
    {
        FxSpec s = Fx.Make(clip, localOffset, size, color);
        s.follow = transform; s.loop = true; s.fadeIn = 0.15f; s.squashY = squash;
        Fx.SortLike(ref s, bodyRender, order);
        FxInstance fx = Fx.Spawn(s);
        if (fx != null) loops.Add(fx);
        return fx;
    }

    private Vector3 LocalOf(Vector3 world) => world - transform.position;

    //1 叶子：缓慢回血，脚下绿色法阵，绿色光点上升
    private void StartLeaf(int lv)
    {
        float maxHp = stats != null && stats.MaxHP > 0f ? stats.MaxHP : health.BeginBlood;
        healPerSecond = maxHp * TreasureRules.LeafHeal(lv) / Mathf.Max(0.1f, TreasureRules.Duration(kind, lv));
        Loop("rune_green", LocalOf(Feet) + Vector3.up * 0.05f, BodyHeight * 1.3f, new Color(1f, 1f, 1f, 0.9f), -3, 0.35f);
    }

    //2 葫芦：饮下爆出火光，持续期间增伤、减速，身后红色火焰
    private void StartGourd(int lv)
    {
        combat?.SetAttackMultiplier(this, 1f + TreasureRules.GourdDamage(lv));
        movement?.SetMoveMultiplier(this, 1f - TreasureRules.GourdSlow);
        FxSpec burst = Fx.Make("explosion", Center, BodyHeight * 1.4f, Color.white);
        Fx.SortLike(ref burst, bodyRender, 4);
        Fx.Spawn(burst);
        Loop("sun", LocalOf(Center), BodyHeight * 1.5f, new Color(1f, 0.35f, 0.15f, 0.55f), -3);
    }

    //3 剑：脚下出现发光的剑，御剑飞行；W/K 上升、S 下降；攻击、放技能或被打中立即落地
    private void StartSword(int lv)
    {
        if (body == null) { active = false; return; }
        savedGravity = body.gravityScale > 0f ? body.gravityScale : 1f;
        flightBaseY = body.position.y;
        movement?.SetMoveMultiplier(this, (1f + TreasureRules.SwordSpeed(lv)) / 0.7f);//空中移速本来只有地面的 0.7，这里先补回来再加速
        body.linearVelocity = new Vector2(body.linearVelocityX, 3f);//起飞时轻轻往上一抬
        FxSpec flash = Fx.Make("gold_spark", Feet, 1.6f, new Color(0.6f, 0.9f, 1f));
        flash.life = 0.3f; flash.growTo = 1.6f;
        Fx.SortLike(ref flash, bodyRender, 3);
        Fx.Spawn(flash);
        FxLibrary.Clip glow = FxLibrary.Get("sword_glow");
        if (glow == null) return;
        if (swordRender == null) swordRender = new GameObject("FlyingSword").AddComponent<SpriteRenderer>();
        swordRender.sprite = glow.frames[0];
        swordRender.gameObject.SetActive(true);
    }

    private void UpdateSwordVisual()
    {
        if (swordRender == null || !swordRender.gameObject.activeSelf) return;
        bool right = movement == null || movement.FaceRight;
        Vector2 dim = swordRender.sprite.bounds.size;
        float len = Mathf.Max(1.4f, (bodyRender != null ? bodyRender.bounds.size.x : 1f) * 1.6f);
        swordRender.transform.localScale = Vector3.one * (len / Mathf.Max(0.01f, Mathf.Max(dim.x, dim.y)));
        swordRender.transform.rotation = Quaternion.Euler(0f, 0f, right ? -90f : 90f);//剑尖朝前
        swordRender.transform.position = Feet + new Vector3(0f, 0.02f + 0.04f * Mathf.Sin(Time.time * 8f), 0f);
        swordRender.sortingLayerID = bodyRender != null ? bodyRender.sortingLayerID : 0;
        swordRender.sortingOrder = (bodyRender != null ? bodyRender.sortingOrder : 0) + 1;
        if (Time.time >= nextMote)//剑尾拖出蓝色光点
        {
            nextMote = Time.time + 0.06f;
            FxSpec s = Fx.Make(null, Feet + new Vector3((right ? -1f : 1f) * len * 0.45f, Random.Range(-0.05f, 0.08f), 0f), Random.Range(0.18f, 0.32f), new Color(0.55f, 0.9f, 1f, 0.9f));
            s.sprite = FxTextures.Spark; s.life = 0.35f; s.fadeOut = 0.3f; s.drift = new Vector2(right ? -1.5f : 1.5f, 0f); s.growTo = 0.4f;
            Fx.SortLike(ref s, bodyRender, 0);
            Fx.Spawn(s);
        }
    }

    private void FixedUpdate()
    {
        if (!active || kind != TreasureKind.Sword || body == null) return;
        Keyboard k = Keyboard.current;
        bool up = k != null && (k.wKey.isPressed || k.kKey.isPressed || k.upArrowKey.isPressed);
        bool down = k != null && (k.sKey.isPressed || k.downArrowKey.isPressed);
        float vy = up ? 5f : down ? -5f : 0f;
        if (body.position.y >= flightBaseY + 4f) vy = Mathf.Min(vy, 0f);//最多比起飞处高 4 个单位，不飞出关卡
        if (Time.time - startedAt < 0.2f) vy = Mathf.Max(vy, 3f);
        body.gravityScale = 0f;
        body.linearVelocity = new Vector2(body.linearVelocityX, vy);
    }

    //4 伞：撑开护罩，挡下一次攻击；开伞很短时间内挡下算完美格挡，冷却立即结束
    private void StartUmbrella(int lv)
    {
        shieldUp = true;
        shieldWindow = TreasureRules.UmbrellaPerfectWindow(lv);
        Loop("umbrella_bubble", LocalOf(Center) + Vector3.up * BodyHeight * 0.12f, BodyHeight * 1.55f, new Color(1f, 1f, 1f, 0.85f), 3);
        FxSpec ring = Fx.Make("ring", Center, BodyHeight * 0.8f, new Color(0.5f, 1f, 0.8f, 0.9f));
        ring.life = 0.4f; ring.growTo = 2.2f; ring.fadeOut = 0.3f;
        Fx.SortLike(ref ring, bodyRender, 4);
        Fx.Spawn(ring);
    }

    private bool BlockWithUmbrella(DamageInfo damage)
    {
        if (!active || kind != TreasureKind.Umbrella || !shieldUp || damage.IsPeriodic || damage.Amount <= 0f) return false;
        shieldUp = false;
        bool perfect = Time.time - startedAt <= shieldWindow;
        FxSpec hex = Fx.Make("hex_break", Center, BodyHeight * 1.4f, new Color(0.7f, 1f, 0.85f));
        Fx.SortLike(ref hex, bodyRender, 5);
        Fx.Spawn(hex);
        if (perfect)
        {
            readyAt[(int)Id] = Time.time + TreasureRules.UmbrellaPerfectCooldown;
            FxSpec flash = Fx.Make("gold_burst", Center, BodyHeight * 1.8f, Color.white);
            flash.life = 0.35f; flash.growFrom = 0.7f; flash.growTo = 1.3f; flash.fadeOut = 0.25f;
            Fx.SortLike(ref flash, bodyRender, 6);
            Fx.Spawn(flash);
            EquipmentToast.Message("完美格挡！", new Color(1f, 0.85f, 0.3f));
        }
        EndEffect();
        return true;//整次攻击被挡下：不扣血、不击退、不附加异常
    }

    //5 铃铛：震荡波向外扩散，范围内不在霸体的敌人眩晕；Boss 时间减半
    private void RingBell(int lv)
    {
        float radius = TreasureRules.BellRadius(lv), duration = TreasureRules.Duration(kind, lv);
        Vector3 center = Center;
        for (int i = 0; i < 2; i++)
        {
            FxSpec ring = Fx.Make("ring", center, radius * 0.3f, new Color(1f, 0.85f, 0.35f, 0.95f - 0.35f * i));
            ring.life = 0.45f + 0.15f * i; ring.growTo = 6.7f - i * 1.5f; ring.fadeOut = 0.3f;
            Fx.SortLike(ref ring, bodyRender, 5);
            Fx.Spawn(ring);
        }
        FxSpec flash = Fx.Make("sun", center + Vector3.up * BodyHeight * 0.4f, 1.6f, new Color(1f, 0.9f, 0.5f));
        flash.life = 0.4f; flash.fadeOut = 0.3f;
        Fx.SortLike(ref flash, bodyRender, 6);
        Fx.Spawn(flash);

        foreach (EnemyMovement enemy in FindObjectsByType<EnemyMovement>())
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            Health h = enemy.GetComponent<Health>();
            if (h == null || h.Dead) continue;
            Collider2D col = enemy.GetComponent<Collider2D>();
            Vector2 at = col != null ? (Vector2)col.bounds.center : (Vector2)enemy.transform.position;
            if (Vector2.Distance(at, center) > radius) continue;
            bool boss = enemy.GetComponent<BossCombat>() != null;
            float time = duration * (boss ? TreasureRules.BellBossFactor : 1f);
            enemy.Stun(this, time);//霸体、无敌会自动拒绝
            if (!enemy.HasStun(this)) continue;
            float top = col != null ? col.bounds.max.y - enemy.transform.position.y : 1.5f;
            FxSpec stars = Fx.Make("stars", new Vector3(0f, top + 0.15f, 0f), 0.9f, new Color(1f, 0.95f, 0.6f));
            stars.follow = enemy.transform; stars.loop = true; stars.life = time; stars.fadeOut = 0.2f;
            SpriteRenderer er = enemy.GetComponentInChildren<SpriteRenderer>();
            Fx.SortLike(ref stars, er, 10);
            Fx.Spawn(stars);
        }
    }

    //---------- 持续效果 ----------
    private void UpdateEffect()
    {
        if (health == null || health.Dead) { EndEffect(); return; }
        if (Time.time >= endsAt) { EndEffect(); return; }
        switch (kind)
        {
            case TreasureKind.Leaf:
                if (InBattle) health.Heal(healPerSecond * Time.deltaTime);
                if (Time.time >= nextMote)
                {
                    nextMote = Time.time + 0.18f;
                    Bounds b = bodyRender != null ? bodyRender.bounds : new Bounds(transform.position, Vector3.one);
                    FxSpec s = Fx.Make(null, new Vector3(b.center.x + Random.Range(-b.extents.x, b.extents.x), b.min.y + Random.Range(0f, b.size.y * 0.4f), 0f),
                        Random.Range(0.18f, 0.3f), new Color(0.55f, 1f, 0.55f, 0.95f));
                    s.sprite = FxTextures.Spark; s.life = 0.9f; s.fadeIn = 0.1f; s.fadeOut = 0.4f; s.drift = new Vector2(0f, 1.4f);
                    Fx.SortLike(ref s, bodyRender, 2);
                    Fx.Spawn(s);
                }
                break;
            case TreasureKind.Sword:
                PlayerState state = player != null ? player.State() : PlayerState.Idle;
                if (state is PlayerState.NormalAttack or PlayerState.Skill or PlayerState.ComboAttack or PlayerState.RiseAttack
                    or PlayerState.Attacked or PlayerState.Die) { EndEffect(); return; }
                UpdateSwordVisual();
                break;
        }
    }

    private void EndEffect()
    {
        if (!active) return;
        active = false;
        shieldUp = false;
        combat?.RemoveAttackMultiplier(this);
        movement?.RemoveMoveMultiplier(this);
        if (kind == TreasureKind.Sword && body != null) body.gravityScale = savedGravity;
        if (swordRender != null) swordRender.gameObject.SetActive(false);
        foreach (FxInstance fx in loops) if (fx != null) fx.Stop(0.25f);
        loops.Clear();
    }
}

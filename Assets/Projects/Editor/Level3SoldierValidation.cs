#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

//仅供显式执行的第三关回归检查使用；不加入游戏流程，不读取或写入玩家存档文件。
[InitializeOnLoad]
public static class Level3SoldierValidation
{
    private const string ActiveKey = "Codex.Level3SoldierValidation.Active";
    private const string StartedKey = "Codex.Level3SoldierValidation.Started";
    private const string ReportFolder = "Library/Codex/Level3Soldiers-20261029";
    private const string RequestPath = ReportFolder + "/run-validation.request";//仅显式创建此文件时，在现有编辑器启动一次验证
    private static readonly List<string> passed = new();
    private static readonly List<string> failures = new();
    private static readonly List<string> errors = new();
    private static readonly List<string> warnings = new();
    private static IEnumerator routine;
    private static float waitUntil;
    private static int nextFrame;
    private static bool hooked, finished;

    [Serializable]
    private sealed class Report
    {
        public bool success;
        public string unityVersion;
        public string completedAt;
        public string[] passed, failures, runtimeErrors, warnings;
    }

    static Level3SoldierValidation()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= CheckRequest;
        EditorApplication.update += CheckRequest;
        if (SessionState.GetBool(ActiveKey, false)) Hook();
    }

    public static void Run()//batchmode入口：进入真实Play Mode后执行，结束时用退出码及JSON报告结果
    {
        BeginValidation(true);
    }

    [MenuItem("造梦西游/验证第三关天兵")]
    public static void RunInCurrentScene()//现有编辑器只使用当前场景，不打开、保存或覆盖用户尚未保存的场景内容
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false)) return;
        if (SceneManager.GetActiveScene().path != "Assets/Projects/Scenes/Level3Scene.unity")
        {
            Debug.LogWarning("第三关天兵验证只在当前已打开的Level3Scene中运行，请先打开该场景。");
            return;
        }
        BeginValidation(false);
    }

    private static void CheckRequest()//不自动运行；只有显式请求文件存在且编辑器空闲时消费这一次请求
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
            || SessionState.GetBool(ActiveKey, false) || !File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        RunInCurrentScene();
    }

    private static void BeginValidation(bool openScene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false)) return;
        passed.Clear();
        failures.Clear();
        errors.Clear();
        warnings.Clear();
        finished = false;
        routine = null;
        waitUntil = 0f;
        nextFrame = 0;
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(StartedKey, EditorApplication.timeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(ReportFolder);
        Hook();
        if (openScene) EditorSceneManager.OpenScene("Assets/Projects/Scenes/Level3Scene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Hook()
    {
        if (hooked) return;
        hooked = true;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Hook();
            routine = Execute();
            nextFrame = Time.frameCount + 2;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && !finished)
        {
            failures.Add("Play Mode exited before validation completed");
            Finish();
        }
    }

    private static void Tick()
    {
        if (finished || !SessionState.GetBool(ActiveKey, false)) return;
        if (double.TryParse(SessionState.GetString(StartedKey, "0"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double started)
            && EditorApplication.timeSinceStartup - started > 90d)
        {
            failures.Add("Validation exceeded 90 seconds");
            Finish();
            return;
        }
        if (!EditorApplication.isPlaying || routine == null || Time.time < waitUntil || Time.frameCount < nextFrame) return;
        try
        {
            if (!routine.MoveNext()) { Finish(); return; }
            waitUntil = routine.Current is float seconds ? Time.time + seconds : Time.time;
            nextFrame = Time.frameCount + 1;
        }
        catch (Exception exception)
        {
            failures.Add(exception.ToString());
            Finish();
        }
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(condition + "\n" + stackTrace);
        else if (type == LogType.Warning && !warnings.Contains(condition)) warnings.Add(condition);//重复的同一条编辑器警告只记录一次
    }

    private static IEnumerator Execute()
    {
        GamePause.Reset();
        yield return 0.25f;//先让实际Level3Scene完成Start、角色创建和第一轮物理更新
        PlayerManager players = Object.FindFirstObjectByType<PlayerManager>();
        Require(players != null && players.CurrentPlayer != null, "Level3Scene creates its current player");
        Check(BattleRun.IsRunning, "Level3Scene starts a battle without entering a result scene");
        Check(players.Players.Count == 4, "Level3Scene initializes all four playable characters");
        foreach (LevelManager manager in Object.FindObjectsByType<LevelManager>(FindObjectsSortMode.None)) manager.enabled = false;
        foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) enemy.enabled = false;
        foreach (InputReader input in Object.FindObjectsByType<InputReader>(FindObjectsInactive.Include, FindObjectsSortMode.None)) input.enabled = false;
        GoldManager.Current?.EndRun();//验证过程中不产生金币入账，也不进入胜利/失败保存入口

        GameObject meleeAsset = Asset("Assets/Projects/Prefabs/Enemy/PurpleHeavenly.prefab");
        GameObject rangedAsset = Asset("Assets/Projects/Prefabs/Enemy/PurpleRanged.prefab");
        GameObject normalAsset = Asset("Assets/Projects/Prefabs/SeparateEffort/NormalArrow.prefab");
        GameObject specialAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/SpecialArrow.prefab");
        if (specialAsset == null) specialAsset = Asset("Assets/Projects/Prefabs/SeparateEffort/SprcialArrow.prefab");
        ValidateSprites(meleeAsset.GetComponent<PurpelController>(), new[] { "Idle", "Walk", "Attacked", "Die", "AttackOne", "AttackTwo" });
        ValidateSprites(rangedAsset.GetComponent<PurpleController>(), new[] { "Idle", "Walk", "Attacked", "Die", "Attack" });
        SpriteData parry = Get<SpriteData>(meleeAsset.GetComponent<PurpelController>(), "Parry");
        Check(parry != null && parry.sprites != null && parry.sprites.Length > 0 && parry.sprites[0] != null, "Melee parry has its own assigned sprite");
        Check(Mathf.Approximately(Get<float>(rangedAsset.GetComponent<PurpleRangedCombat>(), "poisonArrowChance"), 0.2f), "Ranged prefab poison-arrow chance defaults to 20 percent");

        PlayerController player = players.CurrentPlayer;
        PreparePlayer(player);
        Health playerHealth = player.GetComponent<Health>();
        Vector3 playerPosition = player.transform.position;

        //通过真实PlayerManager来回换人，验证毒层、离场停表、剩余时间和自然结束。
        playerHealth.ClearDebuffs();
        playerHealth.ApplyPoison(player.transform, 0.8f, 1f, 3);
        yield return 0.1f;
        float beforeSwitch = Get<float>(playerHealth, "poisonUntil") - Time.time;
        players.SwitchNext();
        Require(players.CurrentPlayer != player, "PlayerManager switches to another character");
        PreparePlayer(players.CurrentPlayer);
        float benchedBlood = playerHealth.CurBlood;
        yield return 0.25f;
        Check(playerHealth.PoisonStacks == 3 && Mathf.Approximately(playerHealth.CurBlood, benchedBlood), "Benched poison keeps three stacks and pauses damage");
        players.SwitchPri();
        Require(players.CurrentPlayer == player, "PlayerManager returns to the original character");
        PreparePlayer(player);
        float resumedRemaining = Get<float>(playerHealth, "poisonUntil") - Time.time;
        Check(playerHealth.PoisonStacks == 3 && Mathf.Abs(resumedRemaining - beforeSwitch) < 0.08f,
            "Switching back resumes the original remaining poison duration instead of refreshing it");
        yield return 0.85f;
        Check(playerHealth.PoisonStacks == 0, "Resumed poison expires normally");
        playerHealth.RemoveInvincibility(players);

        //近战实际伤害接口：第二击包含毒、眩晕和冰冻，结算后必须被弹反驱散。
        GameObject melee = Spawn(meleeAsset, playerPosition + Vector3.right * 7f, player.transform);
        PurpelController meleeController = melee.GetComponent<PurpelController>();
        PurpleHeavenlyCombat meleeCombat = melee.GetComponent<PurpleHeavenlyCombat>();
        Set(meleeCombat, "attackDelay", new[] { 1000f, 1000f });
        meleeCombat.ResetAllAttackCooldowns();
        Set(meleeController, "stopDistance", 1000f);
        Health meleeHealth = melee.GetComponent<Health>();
        Set(meleeHealth, "beginBlood", 1000f);
        Set(meleeHealth, "curBlood", 1000f);

        //愤怒伤害在真正命中时按倍率读取，重新打开攻击箱不能丢失加成，也不能把倍率乘两次。
        BoxCollider2D[] meleeAttackBoxes = Get<BoxCollider2D[]>(meleeCombat, "AttackBoxes");
        AttackBox firstAttack = meleeAttackBoxes[0].GetComponent<AttackBox>();
        float baseAttackDamage = firstAttack.CreateDamageInfo(melee.transform).Amount;
        Require(baseAttackDamage > 0f, "Melee prefab has positive configured attack damage");
        float angerRatio = Get<float>(meleeCombat, "angryRatio");
        meleeCombat.Boost();
        float firstBoostedDamage = firstAttack.CreateDamageInfo(melee.transform).Amount;
        Check(meleeCombat.Angry && Mathf.Approximately(firstBoostedDamage, baseAttackDamage * angerRatio),
            "Melee anger applies its configured damage multiplier exactly once",
            $"angry={meleeCombat.Angry}, base={baseAttackDamage}, actual={firstBoostedDamage}, ratio={angerRatio}, combatMultiplier={meleeCombat.DamageMultiplier}");
        meleeAttackBoxes[0].gameObject.SetActive(true);
        meleeAttackBoxes[0].gameObject.SetActive(false);
        meleeAttackBoxes[0].gameObject.SetActive(true);
        Check(Mathf.Approximately(firstAttack.CreateDamageInfo(melee.transform).Amount, baseAttackDamage * angerRatio),
            "Melee anger damage persists after an attack box is disabled and re-enabled");
        meleeAttackBoxes[0].gameObject.SetActive(false);
        meleeCombat.BoostOver();
        Check(Mathf.Approximately(firstAttack.CreateDamageInfo(melee.transform).Amount, baseAttackDamage), "Ending melee anger restores the original attack damage");

        //只移动运行实例来站在原有地面上；检测范围和伤害范围完全使用用户预制体原来的大小。
        Collider2D playerBody = player.GetComponent<Collider2D>();
        RaycastHit2D ground = Physics2D.Raycast(playerBody.bounds.center, Vector2.down, 100f, LayerMask.GetMask("Ground"));
        Require(ground.collider != null, "Melee contact validation finds the scene's existing ground");
        player.transform.position += Vector3.up * (ground.point.y - playerBody.bounds.min.y);
        Physics2D.SyncTransforms();
        playerPosition = player.transform.position;
        EnemyCheckAttack[] meleeChecks = Get<EnemyCheckAttack[]>(meleeCombat, "CheckAttackLogic");
        int[] openFrames = Get<int[]>(meleeCombat, "AttackOpenIdx");
        int[] shutFrames = Get<int[]>(meleeCombat, "AttackShutIdx");
        SpriteManager meleeSprites = melee.GetComponentInChildren<SpriteManager>();
        SpriteRenderer meleeRenderer = meleeSprites.GetComponent<SpriteRenderer>();
        for (int attack = 0; attack < 2; attack++)
        {
            float[] delays = { 1000f, 1000f };
            delays[attack] = 2f;
            Set(meleeCombat, "attackDelay", delays);
            meleeCombat.ResetAllAttackCooldowns();
            meleeCombat.ChangeCheckBox(true);
            BoxCollider2D range = meleeChecks[attack].GetComponent<BoxCollider2D>();
            Collider2D meleeBody = melee.GetComponent<Collider2D>();
            Vector3 shift = new(playerBody.bounds.center.x - range.bounds.center.x, ground.point.y - meleeBody.bounds.min.y - 0.001f, 0f);
            melee.transform.position += shift;
            Physics2D.SyncTransforms();
            Get<float[]>(meleeCombat, "lastAttack")[attack] = Time.time - 2.01f;
            float readyDeadline = Time.time + 0.5f;
            while (meleeCombat.Attack != attack && Time.time < readyDeadline) yield return null;
            Check(meleeChecks[attack].PlayerIn, $"Melee attack {attack + 1} range receives actual player collision callbacks");
            Check(meleeCombat.Attack == attack, $"Melee AI selects attack {attack + 1} when its configured range and cooldown are satisfied");
            SpriteData selectedAnimation = Get<SpriteData>(meleeController, attack == 0 ? "AttackOne" : "AttackTwo");
            Check(selectedAnimation != null && Array.IndexOf(selectedAnimation.sprites, meleeRenderer.sprite) >= 0,
                $"Melee attack {attack + 1} plays the corresponding assigned sprite animation");
            float beforeContact = playerHealth.CurBlood;
            bool observedOpen = false, observedInsideWindow = false;
            float endDeadline = Time.time + 1.5f;
            while (meleeCombat.Attack == attack && Time.time < endDeadline)
            {
                if (meleeAttackBoxes[attack].gameObject.activeSelf)
                {
                    observedOpen = true;
                    if (meleeSprites.CurFrame >= openFrames[attack] && meleeSprites.CurFrame < shutFrames[attack]) observedInsideWindow = true;
                }
                yield return null;
            }
            Check(observedOpen && observedInsideWindow, $"Melee attack {attack + 1} opens its original hitbox during the configured action window");
            Check(playerHealth.CurBlood < beforeContact, $"Melee attack {attack + 1} actually damages the player's hurtbox");
            Check(meleeCombat.Attack == -1 && !meleeAttackBoxes[attack].gameObject.activeSelf,
                $"Melee attack {attack + 1} closes its hitbox and completes normally");
            playerHealth.AttackedEnd();
        }

        melee.transform.position += Vector3.right * 30f;
        Set(meleeCombat, "attackDelay", new[] { 0f, 0f });
        meleeCombat.ResetAllAttackCooldowns();
        Physics2D.SyncTransforms();
        yield return 0.08f;
        meleeCombat.CheckAttack();
        bool outsideRanges = !meleeChecks[0].PlayerIn && !meleeChecks[1].PlayerIn && !meleeCombat.AttackReady();
        Check(outsideRanges, "Melee AI does not attack when the player is outside both original ranges",
            outsideRanges ? null : RangeTrace(meleeCombat, meleeChecks, player));
        GameObject outsider = new("ValidationNonPlayerCollider");
        outsider.layer = LayerMask.NameToLayer("Player");
        outsider.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        outsider.AddComponent<BoxCollider2D>().isTrigger = true;
        outsider.transform.position = meleeChecks[0].GetComponent<BoxCollider2D>().bounds.center;
        Physics2D.SyncTransforms();
        yield return 0.08f;
        meleeCombat.CheckAttack();
        bool outsiderIgnored = !meleeChecks[0].PlayerIn && !meleeCombat.AttackReady();
        Check(outsiderIgnored, "A non-player collider on the Player layer cannot trigger melee attacks",
            outsiderIgnored ? null : RangeTrace(meleeCombat, meleeChecks, player));
        Object.Destroy(outsider);
        melee.transform.position = playerPosition + Vector3.right * 7f;
        Set(meleeCombat, "attackDelay", new[] { 1000f, 1000f });
        meleeCombat.ResetAllAttackCooldowns();
        Invoke(meleeController, "SetState", EnemyState.Idle);
        Physics2D.SyncTransforms();

        meleeHealth.TakeDamage(new DamageInfo(player.transform, 10f, false, kind: DamageKind.NormalAttack));
        var freezeEffect = ScriptableObject.CreateInstance<Level3ValidationHitEffect>();
        meleeHealth.TakeDamage(new DamageInfo(player.transform, 10f, false, false, new AttackEffect[] { freezeEffect }, kind: DamageKind.Skill));
        Check(meleeCombat.ParryArmed && meleeCombat.ParryActive, "Two direct hits within 0.5 seconds arm melee parry");
        Check(!meleeHealth.Attacked && meleeHealth.PoisonStacks == 0 && !meleeHealth.Status.IsFrozen && !melee.GetComponent<EnemyMovement>().IsStunned,
            "Parry clears hitstun, poison, stun and newly applied freeze");
        Check(Get<EnemyState>(meleeController, "state") == EnemyState.Skill, "Parry replaces the interrupted action with the guard pose");
        float guardedBlood = meleeHealth.CurBlood;
        meleeHealth.TakeDamage(new DamageInfo(player.transform, 10f, false, false, new AttackEffect[] { freezeEffect }, kind: DamageKind.Skill));
        Check(Mathf.Approximately(meleeHealth.CurBlood, guardedBlood) && !meleeCombat.ParryArmed
            && meleeHealth.PoisonStacks == 0 && !meleeHealth.Status.IsFrozen, "The next attack is blocked once including its status effects");
        meleeHealth.TakeDamage(new DamageInfo(player.transform, 1f, false, kind: DamageKind.NormalAttack));
        Check(Mathf.Approximately(meleeHealth.CurBlood, guardedBlood - 1f), "Parry does not block a second following attack");
        yield return 0.2f;
        Check(!meleeCombat.ParryActive, "Successful guard recovery ends and returns normal control");
        Set(meleeHealth, "curBlood", 1f);
        meleeHealth.TakeDamage(new DamageInfo(player.transform, 2f, false, kind: DamageKind.Skill));
        Check(meleeHealth.Dead && !meleeCombat.ParryArmed, "Lethal damage wins over parry activation");
        Object.Destroy(freezeEffect);
        Object.Destroy(melee);

        //远程真实动画到第十帧才出箭，同一轮不能重复生成；0与1概率边界使随机结果可确定验证。
        GameObject ranged = Spawn(rangedAsset, playerPosition - Vector3.right * 7f, player.transform);
        PurpleController rangedController = ranged.GetComponent<PurpleController>();
        PurpleRangedCombat rangedCombat = ranged.GetComponent<PurpleRangedCombat>();
        EnemyProjectileManager pool = EnemyProjectileManager.GetOrCreate();
        Set(pool, "cameraManager", null);//此组隔离发射帧检查使用发射点周围边界，避免测试站位刚好在相机外导致箭同帧回收
        pool.ReleaseAll();
        Set(rangedCombat, "phaseCooldown", 1000f);
        Set(rangedCombat, "shootCooldown", 1000f);
        rangedCombat.UpdateCheckFace(true);
        BoxCollider2D arrowCheck = Get<EnemyCheckAttack>(rangedCombat, "arrowCheck").GetComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();
        ranged.transform.position += player.GetComponent<Collider2D>().bounds.center - arrowCheck.bounds.center;
        Physics2D.SyncTransforms();
        Check(!rangedCombat.CanShoot, "Ranged shooter respects its shot cooldown even with player inside ArrowCheck");
        Set(rangedCombat, "shootCooldown", 0f);
        Check(rangedCombat.CanShoot, "ArrowCheck recognizes the current player's body inside its configured range");
        Set(rangedCombat, "poisonArrowChance", 0f);
        Set(rangedCombat, "shootCooldown", 1000f);
        rangedCombat.BeginShot(true);
        Invoke(rangedController, "SetState", EnemyState.Skill);
        SpriteManager rangedSprites = ranged.GetComponentInChildren<SpriteManager>();
        float shotDeadline = Time.time + 3f;
        bool earlyArrow = false;
        while (Flying(pool) == 0 && Time.time < shotDeadline)
        {
            yield return null;
            if (rangedSprites.CurFrame < 9 && Flying(pool) > 0) earlyArrow = true;
        }
        Check(!earlyArrow && Flying(pool) == 1 && rangedSprites.CurFrame >= 9, "Real shooting animation emits one arrow at frame 10 and none earlier");
        int shotCount = Flying(pool);
        rangedCombat.CheckShot(9);
        Check(Flying(pool) == shotCount, "Repeated frame-10 checks do not duplicate the arrow");
        rangedController.enabled = false;
        rangedCombat.CancelShot();
        pool.ReleaseAll();
        rangedCombat.BeginShot(false);
        Set(rangedCombat, "poisonArrowChance", 1f);
        rangedCombat.CheckShot(8);
        Check(Flying(pool) == 0, "Frame 9 cannot emit an arrow early");
        rangedCombat.CheckShot(9);
        bool poisonSpawn = false;
        foreach (NormalArrowManager arrow in pool.GetComponentsInChildren<NormalArrowManager>(true))
            if (arrow.IsFlying && arrow is SpecialArrowManager) poisonSpawn = true;
        Check(poisonSpawn, "A 100 percent poison chance selects the poison-arrow subclass");
        rangedCombat.CancelShot();
        pool.ReleaseAll();

        //虚化要求两个间隔同时满足，期间只拒绝普通攻击类别。
        Set(rangedCombat, "phaseCooldown", 0.15f);
        Set(rangedCombat, "phaseDuration", 0.4f);
        Set(rangedCombat, "lastShotTime", Time.time - 1f);
        Set(rangedCombat, "lastPhaseEnd", Time.time);
        yield return 0.04f;
        Check(!rangedCombat.IsPhased, "Phasing waits for the previous phase-end cooldown");
        Set(rangedCombat, "lastPhaseEnd", Time.time - 1f);
        Set(rangedCombat, "lastShotTime", Time.time);
        yield return 0.04f;
        Check(!rangedCombat.IsPhased, "Phasing also waits for the previous shot cooldown");
        Set(rangedCombat, "lastShotTime", Time.time - 1f);
        Set(rangedCombat, "lastPhaseEnd", Time.time - 1f);
        yield return 0.04f;
        Health rangedHealth = ranged.GetComponent<Health>();
        SpriteRenderer rangedRenderer = Get<SpriteRenderer>(rangedCombat, "sr");
        Check(rangedCombat.IsPhased && rangedRenderer.color.a < 0.75f, "Phasing begins after both intervals and makes the sprite translucent");
        float phaseBlood = rangedHealth.CurBlood;
        rangedHealth.TakeDamage(new DamageInfo(player.transform, 10f, false, kind: DamageKind.NormalAttack));
        Check(Mathf.Approximately(rangedHealth.CurBlood, phaseBlood), "Phasing rejects a player normal-attack damage category");
        rangedHealth.TakeDamage(new DamageInfo(player.transform, 2f, false, kind: DamageKind.Skill));
        Check(rangedHealth.CurBlood < phaseBlood, "Phasing still accepts skill damage");
        rangedHealth.ApplyPoison(player.transform, 0.2f, 1f, 3);
        ranged.GetComponent<EnemyMovement>().Stun(player, 0.1f);
        Check(rangedHealth.PoisonStacks == 3 && ranged.GetComponent<EnemyMovement>().IsStunned, "Phasing still accepts poison and ordinary stun");
        yield return 0.45f;
        Check(!rangedCombat.IsPhased && Mathf.Approximately(rangedRenderer.color.a, 1f), "Phasing expires and restores full opacity");
        ranged.GetComponent<EnemyMovement>().ClearExternalControl();

        //穿透箭实际扫掠当前玩家：命中一次后继续飞行，超边界停用，再由对象池复用。
        playerHealth.ClearDebuffs();
        playerHealth.RemoveInvincibility(players);
        NormalArrowManager normal = Object.Instantiate(normalAsset).GetComponent<NormalArrowManager>();
        LaunchAtPlayer(normal, rangedCombat, players, true);
        float arrowBlood = playerHealth.CurBlood;
        yield return 0.25f;
        Check(Mathf.Approximately(playerHealth.CurBlood, arrowBlood - 10f) && normal.IsFlying, "Normal arrow damages once and continues through the player");
        float afterArrow = playerHealth.CurBlood;
        yield return 0.2f;
        Check(Mathf.Approximately(playerHealth.CurBlood, afterArrow), "The same penetrating arrow does not repeatedly damage the same player");
        normal.transform.position += Vector3.right * 200f;
        yield return 0.06f;
        Check(!normal.IsFlying && !normal.gameObject.activeSelf, "Arrow crossing its world boundary becomes inactive");
        SpecialArrowManager poison = Object.Instantiate(specialAsset).GetComponent<SpecialArrowManager>();
        LaunchAtPlayer(poison, rangedCombat, players, false);
        yield return 0.25f;
        Check(playerHealth.PoisonStacks == 3 && poison.IsFlying, "Poison arrow penetrates and adds exactly three poison stacks");
        poison.StopEffect();
        playerHealth.ClearDebuffs();
        pool.ReleaseAll();
        NormalArrowManager normalTemplate = normalAsset.GetComponent<NormalArrowManager>();
        pool.Fire(normalTemplate, rangedCombat, (Vector2)player.transform.position + Vector2.left * 20f, true);
        NormalArrowManager borrowed = FirstFlying(pool);
        Require(borrowed != null, "Projectile pool provides an active normal arrow");
        borrowed.StopEffect();
        pool.Fire(normalTemplate, rangedCombat, (Vector2)player.transform.position + Vector2.left * 20f, true);
        Check(FirstFlying(pool) == borrowed, "Projectile pool reuses the returned arrow instance");
        pool.ReleaseAll();
        Object.Destroy(normal.gameObject);
        Object.Destroy(poison.gameObject);
        Object.Destroy(ranged);
        BattleRun.Cancel();
        yield return 0.05f;
        Check(errors.Count == 0, "Level3Scene and gameplay validation produced no runtime errors or exceptions");
    }

    private static GameObject Spawn(GameObject asset, Vector3 position, Transform target)
    {
        GameObject instance = Object.Instantiate(asset, position, Quaternion.identity);
        instance.SetActive(true);
        instance.GetComponent<EnemyMovement>().SetTarget(target);
        Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
        if (body != null) { body.linearVelocity = Vector2.zero; body.constraints = RigidbodyConstraints2D.FreezeAll; }
        return instance;
    }

    private static void PreparePlayer(PlayerController player)
    {
        Health health = player.GetComponent<Health>();
        Set(health, "beginBlood", 10000f);
        Set(health, "curBlood", 10000f);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        player.GetComponent<InputReader>().enabled = false;
    }

    private static void LaunchAtPlayer(NormalArrowManager arrow, EnemyCombat source, PlayerManager players, bool right)
    {
        Set(arrow, "flySpeed", 20f);
        Set(arrow, "damage", 10f);
        Bounds target = players.CurrentPlayer.GetComponent<Collider2D>().bounds;
        Vector2 limits = new(target.center.x - 30f, target.center.x + 30f);
        arrow.Launch(source, players, Vector2.zero, right, limits);
        Bounds image = arrow.GetComponent<SpriteRenderer>().bounds;
        float headOffset = image.center.x - arrow.transform.position.x + (right ? 1f : -1f) * image.size.x * 0.375f;
        Vector2 location = new(target.center.x - (right ? 2f : -2f) - headOffset,
            target.center.y - (image.center.y - arrow.transform.position.y));
        arrow.Launch(source, players, location, right, limits);
    }

    private static int Flying(EnemyProjectileManager pool)
    {
        int count = 0;
        foreach (NormalArrowManager arrow in pool.GetComponentsInChildren<NormalArrowManager>(true)) if (arrow.IsFlying) count++;
        return count;
    }

    private static NormalArrowManager FirstFlying(EnemyProjectileManager pool)
    {
        foreach (NormalArrowManager arrow in pool.GetComponentsInChildren<NormalArrowManager>(true)) if (arrow.IsFlying) return arrow;
        return null;
    }

    private static void ValidateSprites(Component controller, string[] fields)
    {
        Require(controller != null, "Soldier prefab has its intended controller");
        foreach (string field in fields)
        {
            SpriteData data = Get<SpriteData>(controller, field);
            bool valid = data != null && data.sprites != null && data.sprites.Length == 16;
            if (valid)
                for (int i = 0; i < data.sprites.Length; i++)
                {
                    Sprite sprite = data.sprites[i];
                    if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f) { valid = false; break; }
                    for (int j = 0; j < i; j++)
                        if (data.sprites[j] != null && sprite.texture == data.sprites[j].texture && sprite.rect.Overlaps(data.sprites[j].rect)) valid = false;
                }
            Check(valid, controller.GetType().Name + "." + field + " has sixteen valid non-overlapping rectangular sprites");
        }
    }

    private static GameObject Asset(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(asset != null, "Prefab is imported: " + path);
        return asset;
    }

    private static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] arguments)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (method == null) continue;
            method.Invoke(target, arguments);
            return;
        }
        throw new MissingMethodException(target.GetType().Name, name);
    }

    private static string RangeTrace(EnemyCombat combat, EnemyCheckAttack[] checks, PlayerController player)//仅失败时记录实际几何和缓存，便于区分游戏缺陷与测试站位问题
    {
        string trace = $"enemy={combat.transform.position}, body={combat.GetComponent<Rigidbody2D>().position}, player={player.transform.position}, attack={combat.Attack}, ready={combat.AttackReady()}";
        for (int i = 0; i < checks.Length; i++)
        {
            Collider2D box = checks[i].GetComponent<Collider2D>();
            trace += $"; range{i}: playerIn={checks[i].PlayerIn}, center={box.bounds.center}, size={box.bounds.size}";
            foreach (Collider2D recorded in Get<HashSet<Collider2D>>(checks[i], "players"))
                if (recorded != null) trace += $", cached={recorded.name}/layer{recorded.gameObject.layer}/active{recorded.gameObject.activeInHierarchy}/overlap{box.Distance(recorded).isOverlapped}";
        }
        return trace;
    }

    private static void Check(bool condition, string description, string detail = null)
    {
        if (condition) { passed.Add(description); Debug.Log("[Level3 validation PASS] " + description); }
        else
        {
            string failure = string.IsNullOrEmpty(detail) ? description : description + " -- " + detail;
            failures.Add(failure);
            Debug.Log("[Level3 validation FAIL] " + failure);
        }
    }
    private static void Require(bool condition, string description)
    {
        Check(condition, description);
        if (!condition) throw new InvalidOperationException(description);
    }

    private static void Finish()
    {
        if (finished) return;
        finished = true;
        SessionState.SetBool(ActiveKey, false);
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= Tick;
        hooked = false;
        Directory.CreateDirectory(ReportFolder);
        Report report = new()
        {
            success = failures.Count == 0 && errors.Count == 0,
            unityVersion = Application.unityVersion,
            completedAt = DateTimeOffset.UtcNow.ToString("O"),
            passed = passed.ToArray(), failures = failures.ToArray(), runtimeErrors = errors.ToArray(), warnings = warnings.ToArray()
        };
        File.WriteAllText(Path.Combine(ReportFolder, "validation-report.json"), JsonUtility.ToJson(report, true));
        Debug.Log($"[Level3 validation] passed={passed.Count}, failed={failures.Count}, errors={errors.Count}; {ReportFolder}/validation-report.json");
        if (Application.isBatchMode) EditorApplication.Exit(report.success ? 0 : 1);
        else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();//图形编辑器保留打开状态，退出Play Mode自动还原测试运行对象
    }
}

//同一击先施加异常，再让实际弹反逻辑在DamageResolved中驱散，覆盖结算顺序的回归风险。
public sealed class Level3ValidationHitEffect : AttackEffect
{
    public override void Apply(Health target, DamageInfo damage)
    {
        target.ApplyPoison(damage.Source, 1f, 1f, 2);
        target.GetComponent<EnemyMovement>()?.Stun(this, 1f);
        target.Status.ApplyFreeze(this, 1f);
    }
}
#endif

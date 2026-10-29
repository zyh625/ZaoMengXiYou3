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
public static class EastKingValidation
{
    private const string ActiveKey = "Codex.EastKingValidation.Active";
    private const string StartedKey = "Codex.EastKingValidation.Started";
    private const string ReportFolder = "Library/Codex/EastKing-20261029";
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

    static EastKingValidation()
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

    [MenuItem("造梦西游/验证持国天王")]
    public static void RunInCurrentScene()//现有编辑器只使用当前场景，不打开、保存或覆盖用户尚未保存的场景内容
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false)) return;
        if (SceneManager.GetActiveScene().path != "Assets/Projects/Scenes/Level3Scene.unity")
        {
            Debug.LogWarning("持国天王验证只在当前已打开的Level3Scene中运行，请先打开该场景。");
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
            && EditorApplication.timeSinceStartup - started > 120d)
        {
            failures.Add("Validation exceeded 120 seconds");
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
        yield return 0.25f;//先让真实场景完成玩家、敌人池、Boss和血条的创建，不经过结算与保存入口
        PlayerManager players = Object.FindFirstObjectByType<PlayerManager>();
        Require(players != null && players.CurrentPlayer != null, "Level3Scene initializes its playable party");
        Require(BattleRun.IsRunning, "Level3Scene starts a battle without loading a result scene");
        StageManager level = null;
        foreach (StageManager candidate in Object.FindObjectsByType<StageManager>(FindObjectsSortMode.None))
            if (Get<bool>(candidate, "initialized")) { level = candidate; break; }
        Require(level != null, "Level3Scene uses the shared stage flow");
        LevelEnemyManager enemies = Get<LevelEnemyManager>(level, "enemyManager");
        Require(enemies != null && enemies.CurBoss != null, "Level3Scene creates its configured boss instance");
        Require(enemies.CurBoss.GetComponent<EastController>() != null, "Level3Scene references EastKing rather than the level-two Titan");
        BossUI ui = Get<BossUI>(level, "bossUI");
        Require(ui != null, "Level3Scene has a boss health panel");
        Check(ui.GetComponent<TitanBossUI>() == null, "EastKing health panel does not depend on Titan rage");
        Check(!ui.gameObject.activeSelf && Get<Health>(ui, "bossHealth") == enemies.CurBoss.GetComponent<Health>(),
            "Actual boss health is bound while its UI starts hidden");
        foreach (LevelManager manager in Object.FindObjectsByType<LevelManager>(FindObjectsSortMode.None)) manager.enabled = false;
        foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            enemy.enabled = false;
        foreach (PlayerController member in players.Players) PreparePlayer(member);
        GoldManager.Current?.EndRun();//任何测试死亡都不能给金币入账，不接触存档文件

        //调用真正的现身流程，再保持Boss战分支；换人仍走LevelManager的真实目标订阅。
        Set(level, "bossStartTime", Time.time - Get<float>(level, "bossBornDelay") - 1f);
        Invoke(level, "CheckBossBorn");
        level.enabled = true;
        Check(enemies.CurBoss.gameObject.activeSelf && Get<bool>(level, "bossBorn") && ui.gameObject.activeSelf,
            "Boss appearance enables EastKing and the shared health bar");
        enemies.CurBoss.GetComponent<EastController>().enabled = false;
        Health sceneBossHealth = enemies.CurBoss.GetComponent<Health>();
        float sceneBossBefore = sceneBossHealth.CurBlood;
        sceneBossHealth.TakeDamage(new DamageInfo(players.CurrentPlayer.transform, 1f, false, false));
        ImageCheck(ui, sceneBossHealth, "Boss health bar follows the actual EastKing health");
        sceneBossHealth.Heal(sceneBossBefore);

        GameObject bossAsset = Asset("Assets/Projects/Prefabs/Enemy/EastKing.prefab");
        ValidateBossSprites(bossAsset.GetComponent<EastController>());
        EastNoteManager[] noteAssets = Get<EastNoteManager[]>(bossAsset.GetComponent<EastCombat>(), "notePrefabs");
        Require(noteAssets != null && noteAssets.Length == 4, "Boss has exactly four assigned note slots");
        Require(noteAssets[0] != null && noteAssets[0].GetType() == typeof(EastNoteManager)
            && noteAssets[1] is StunNoteManager && noteAssets[2] is SummonNoteManager && noteAssets[3] is BuffNoteManager,
            "White, red, blue and green slots reference their intended note behaviours");
        Check(Get<BoxCollider2D[]>(bossAsset.GetComponent<EastCombat>(), "AttackBoxes").Length == 0,
            "Ranged boss has no leftover melee attack boxes");

        PlayerController player = players.CurrentPlayer;
        Health playerHealth = player.GetComponent<Health>();
        Vector3 playerStart = player.transform.position;
        Vector2 playerCenter = player.GetComponent<Collider2D>().bounds.center;
        GameObject boss = Spawn(bossAsset, playerStart + Vector3.right * 8f, player.transform);
        EastCombat combat = boss.GetComponent<EastCombat>();
        EastController controller = boss.GetComponent<EastController>();
        Health bossHealth = boss.GetComponent<Health>();
        controller.enabled = false;
        Set(combat, "noteCooldowns", new[] { 100f, 100f, 100f, 100f });
        Set(combat, "minimumAttackInterval", 0.2f);
        Set(combat, "nextAttackTime", Time.time);
        combat.UpdateAngry(); combat.Boost();
        Check(!combat.Angry && !combat.AngryRequest, "Boss does not inherit ordinary-enemy anger");
        EastNotePool pool = EastNotePool.GetOrCreate();
        pool.ReleaseAll();

        Vector3 farPosition = playerStart + Vector3.left * 100f;
        MovePlayer(player, farPosition);
        Check(combat.GetReadyAttack() == 0, "A ready note does not require the player to be in range");
        MovePlayer(player, playerStart);
        for (int index = 0; index < 4; index++)
        {
            Set(combat, "nextAttackTime", Time.time);
            Set(controller, "state", EnemyState.Idle);
            Set(controller, "pendingAttack", index);
            Invoke(controller, "SetState", EnemyState.Skill);
            Require(combat.IsCasting && combat.CurrentNoteIndex == index, "Note " + index + " begins through the controller when its own cooldown is ready");
            string animationName = new[] { "NormalAttack", "StunAttack", "SummonAttack", "BuffAttack" }[index];
            Check(Get<SpriteData>(controller.GetComponentInChildren<SpriteManager>(), "current") == Get<SpriteData>(controller, animationName),
                "Controller selects the intended casting animation for note " + index);
            Check(!combat.BeginAttack((index + 1) % 4, false), "Note " + index + " cannot overlap another casting action");
            combat.CheckNote(8);
            Check(Flying(pool) == 0, "Note " + index + " is not emitted before animation frame ten");
            combat.CheckNote(9);
            EastNoteManager first = FirstFlying(pool);
            Check(Flying(pool) == 1 && first != null && first.GetType() == noteAssets[index].GetType(),
                "Animation frame ten emits exactly one note of type " + index);
            Check(first != null && ((Vector2)first.transform.position - (Vector2)boss.transform.position).sqrMagnitude < 0.0001f,
                "Note " + index + " starts at the boss transform centre");
            combat.CheckNote(9); combat.CheckNote(15);
            Check(Flying(pool) == 1, "Repeated frame checks do not duplicate note " + index);
            combat.CancelAttack();
            Check(combat.GetReadyAttack() == -1, "Common recovery interval blocks immediate recasting after note " + index);
            pool.ReleaseAll();
        }
        SpriteData finalCast = Get<SpriteData>(controller, "BuffAttack");
        yield return finalCast.sprites.Length / Mathf.Max(1f, finalCast.fps) + 0.08f;
        Invoke(controller, "SkillProcessing");
        Check(Get<EnemyState>(controller, "state") == EnemyState.Idle && !combat.IsCasting,
            "A completed casting animation returns through the shared skill flow to idle");
        Set(combat, "nextAttackTime", Time.time);
        Check(combat.GetReadyAttack() == -1, "Four independent cooldowns remain consumed after one full cycle");
        float[] lastTimes = Get<float[]>(combat, "lastNoteTimes");
        lastTimes[0] = Time.time - 101f;
        Check(combat.GetReadyAttack() == 0, "Only the independently recovered note becomes ready");
        Require(combat.BeginAttack(0, true), "A recovered note can begin another action");
        combat.CheckNote(8);
        Invoke(controller, "SpecialAttackOver");
        combat.CheckNote(9);
        Check(!combat.IsCasting && Flying(pool) == 0, "Shared interruption discards a note that has not reached its firing frame");
        lastTimes[1] = Time.time - 101f;
        Check(combat.GetReadyAttack() == -1, "Another independently ready note still waits for shared recovery");
        yield return 0.25f;
        Check(combat.GetReadyAttack() == 1, "A different ready note becomes available when shared recovery really expires");
        lastTimes[0] = Time.time - 101f; Set(combat, "nextAttackTime", Time.time);
        Require(combat.BeginAttack(0, true), "A new action can start after the interrupted cooldown recovers");
        bossHealth.Status.ApplyFreeze(combat, 1f);
        combat.CheckNote(9);
        Check(!combat.IsCasting && Flying(pool) == 0, "Freezing before frame ten prevents delayed emission");
        bossHealth.ClearDebuffs(); bossHealth.AttackedEnd();

        //白音符的方向只在出生时确定；移动目标不能把它变成追踪弹。
        EastNoteManager white = SpawnNote(noteAssets[0], combat, players, enemies, playerCenter + Vector2.left * 6f);
        Set(white, "flySpeed", 2f);
        Vector2 whiteDirection = Get<Vector2>(white, "direction");
        MovePlayer(player, playerStart + Vector3.up * 3f);
        Invoke(white, "FixedUpdate");
        Check(Vector2.Dot(whiteDirection, Get<Vector2>(white, "direction")) > 0.99999f,
            "White note keeps its original flight vector when the player moves");
        white.StopEffect(); MovePlayer(player, playerStart);
        white.Launch(combat, players, enemies, playerCenter + Vector2.left * 2f, true, new Vector2(playerCenter.x - 30f, playerCenter.x + 30f));
        Set(white, "flySpeed", 500f);
        float healthBefore = playerHealth.CurBlood;
        Invoke(white, "FixedUpdate");
        Check(playerHealth.CurBlood < healthBefore && !white.IsFlying,
            "A fast white note sweeps through the player and applies damage once");
        playerHealth.AttackedEnd();

        //红音符每步重瞄、正常到期，并通过完整伤害接口触发可驱散的普通眩晕。
        StunNoteManager red = (StunNoteManager)SpawnNote(noteAssets[1], combat, players, enemies, playerCenter + new Vector2(7f, 3f));
        Vector2 redBefore = Get<Vector2>(red, "direction");
        MovePlayer(player, playerStart + Vector3.up * 6f);
        Invoke(red, "FixedUpdate");
        Check(Vector2.Dot(redBefore, Get<Vector2>(red, "direction")) < 0.99f, "Red note re-aims at the player's changed position");
        red.StopEffect(); MovePlayer(player, playerStart);
        Set(red, "lifetime", 0.12f);
        red.Launch(combat, players, enemies, playerCenter + Vector2.right * 10f, false, new Vector2(playerCenter.x - 30f, playerCenter.x + 30f));
        yield return 0.18f;
        Check(!red.IsFlying, "An uncollected tracking note expires at its configured lifetime");
        Set(red, "lifetime", 6f);
        red.Launch(combat, players, enemies, playerCenter, false, new Vector2(playerCenter.x - 30f, playerCenter.x + 30f));
        Invoke(red, "FixedUpdate");
        ActorStun playerStun = player.GetComponent<ActorStun>();
        Check(playerStun != null && playerStun.IsStunned && !red.IsFlying, "A red-note hit applies ordinary player stun and returns the note");
        playerHealth.ClearDebuffs(); playerHealth.AttackedEnd();
        playerHealth.SetSuperArmor(combat, true);
        red.Launch(combat, players, enemies, playerCenter, false, new Vector2(playerCenter.x - 30f, playerCenter.x + 30f));
        Invoke(red, "FixedUpdate");
        Check(playerStun != null && !playerStun.IsStunned, "Ordinary super armour rejects the red note's stun");
        playerHealth.SetSuperArmor(combat, false);
        Require(playerStun != null && playerStun.Apply(combat, 2f), "Player can be stunned again after armour ends");
        playerHealth.ApplyPoison(combat.transform, 2f, 1f, 1);
        PlayerBoost boost = player.GetComponent<PlayerBoost>();
        Set(boost, "boostEnergy", boost.MaxEnergy);
        boost.RequestBoost();
        Check(boost.ConsumeRequest() && !playerStun.IsStunned && playerHealth.PoisonStacks == 0,
            "Actual boost activation clears red-note stun together with poison");
        boost.ResetBoost(); playerHealth.AttackedEnd();
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        Require(playerStun.Apply(combat, 0.12f), "Short ordinary stun is applied before a simultaneous freeze");
        Require(playerHealth.Status.ApplyFreeze(combat, 1f), "Strong freeze can coexist with ordinary stun");
        yield return 0.18f;
        Check(!playerStun.IsStunned && playerHealth.Status.IsFrozen
            && (playerBody.constraints & RigidbodyConstraints2D.FreezePosition) == RigidbodyConstraints2D.FreezePosition,
            "Stun expiry does not remove an overlapping freeze's motion lock");
        playerHealth.Status.CancelFreeze(combat);
        Check(playerBody.constraints == RigidbodyConstraints2D.FreezeRotation, "The last freeze restores the original rigidbody constraints");
        PreparePlayer(player); MovePlayer(player, playerStart);

        //蓝音符路径优先检测玩家；成功接住只恢复蓝量，不登记小怪。
        PlayerResources resources = player.GetComponent<PlayerResources>();
        resources.TrySpendMana(40f);
        float manaBefore = resources.CurrentMana;
        int aliveBefore = enemies.AliveNum();
        SummonNoteManager blue = (SummonNoteManager)SpawnNote(noteAssets[2], combat, players, enemies, playerCenter);
        float manaRecovery = Get<float>(blue, "manaRecovery");
        Invoke(blue, "FixedUpdate");
        Check(!blue.IsFlying && Mathf.Approximately(resources.CurrentMana, Mathf.Min(resources.MaxMana, manaBefore + manaRecovery))
            && enemies.AliveNum() == aliveBefore, "Catching a blue note restores mana and prevents the summon");
        HashSet<EnemyController> existing = ActiveEnemies();
        Vector2 landing = playerCenter + Vector2.right * 5f;
        Set(blue, "ascendSpeed", 10000f); Set(blue, "descendSpeed", 10000f);
        blue.Launch(combat, players, enemies, landing + Vector2.up, true, new Vector2(landing.x, landing.x));
        Vector2 groundPoint = Get<Vector2>(blue, "groundPoint");
        Invoke(blue, "FixedUpdate"); Invoke(blue, "FixedUpdate");
        EnemyController summoned = NewEnemy(existing);
        Require(!blue.IsFlying && summoned != null && enemies.AliveNum() == aliveBefore + 1,
            "A blue note reaching the ground creates one registered soldier");
        summoned.enabled = false;
        Check(summoned is PurpelController || summoned is PurpleController, "A summoned enemy is one of this stage's two soldier types");
        Collider2D summonedBody = summoned.GetComponent<Collider2D>();
        Check(Mathf.Abs(summonedBody.bounds.min.y - groundPoint.y) < 0.03f, "Summoned soldier feet align to the note's ground point");
        EnemyRewards rewards = summoned.GetComponent<EnemyRewards>();
        Check(rewards != null && Get<PlayerManager>(rewards, "playerManager") == players,
            "Summoned soldier receives the same reward registration as ordinary enemies");
        players.SwitchNext();
        yield return null;
        PreparePlayer(players.CurrentPlayer);
        Check(Get<Transform>(summoned.GetComponent<EnemyMovement>(), "player") == players.CurrentPlayer.transform,
            "A summoned soldier follows the actual newly switched character");
        PlayerResources rewardResources = players.CurrentPlayer.GetComponent<PlayerResources>();
        float experienceBefore = rewardResources.Experience;
        float expectedExperience = rewards != null ? Get<float>(rewards, "experienceReward") : 0f;
        summoned.GetComponent<Health>().TakeDamage(new DamageInfo(players.CurrentPlayer.transform, 100000f, false, false));
        Check(Mathf.Approximately(rewardResources.Experience, experienceBefore + expectedExperience),
            "Defeating a summoned soldier awards its configured experience once");
        rewardResources.SetExperience(experienceBefore);//只还原本次验证的临时经验，不触发保存
        summoned.gameObject.SetActive(false);
        players.SwitchPri(); yield return null;
        player = players.CurrentPlayer; PreparePlayer(player);
        playerHealth = player.GetComponent<Health>(); playerBody = player.GetComponent<Rigidbody2D>();
        MovePlayer(player, playerStart); playerCenter = player.GetComponent<Collider2D>().bounds.center;
        playerHealth.RemoveInvincibility(players);

        //绿音符先治疗其他天兵；重复来自同一Boss的增幅不乘两次，到期恢复原攻击。
        GameObject soldierAsset = Asset("Assets/Projects/Prefabs/Enemy/PurpleHeavenly.prefab");
        GameObject recipient = Spawn(soldierAsset, boss.transform.position + Vector3.left * 2f, player.transform);
        recipient.GetComponent<EnemyController>().enabled = false;
        Health recipientHealth = recipient.GetComponent<Health>();
        EnemyCombat recipientCombat = recipient.GetComponent<EnemyCombat>();
        Set(recipientHealth, "beginBlood", 100f); Set(recipientHealth, "curBlood", 30f);
        BuffNoteManager green = (BuffNoteManager)SpawnNote(noteAssets[3], combat, players, enemies, (Vector2)boss.transform.position);
        Check(Get<Health>(green, "recipient") == recipientHealth, "Green note prioritizes another living soldier over the boss");
        Set(green, "ascendSpeed", 10000f); Set(green, "deliverySpeed", 10000f); Set(green, "buffDuration", 0.2f);
        float enemyHeal = Get<float>(green, "enemyHealing"), multiplier = Get<float>(green, "attackMultiplier");
        Invoke(green, "FixedUpdate"); Invoke(green, "FixedUpdate");
        Check(!green.IsFlying && Mathf.Approximately(recipientHealth.CurBlood, 30f + enemyHeal)
            && Mathf.Approximately(recipientCombat.DamageMultiplier, multiplier),
            "Green note arrival heals the soldier and applies its configured attack multiplier");
        green.Launch(combat, players, enemies, boss.transform.position, true, new Vector2(playerCenter.x - 30f, playerCenter.x + 30f));
        Invoke(green, "FixedUpdate"); Invoke(green, "FixedUpdate");
        Check(Mathf.Approximately(recipientCombat.DamageMultiplier, multiplier), "Repeated green notes from the same boss refresh instead of multiplying the buff");
        yield return 0.28f;
        Check(Mathf.Approximately(recipientCombat.DamageMultiplier, 1f), "Enemy attack returns to its original value after the green-note buff expires");
        recipient.gameObject.SetActive(false);
        Set(bossHealth, "curBlood", 500f);
        green.Launch(combat, players, enemies, boss.transform.position, true, new Vector2(playerCenter.x - 30f, playerCenter.x + 30f));
        Check(Get<Health>(green, "recipient") == bossHealth, "Green note targets the caster when no other soldier is alive");
        Invoke(green, "FixedUpdate"); Invoke(green, "FixedUpdate");
        Check(Mathf.Approximately(bossHealth.CurBlood, 500f + enemyHeal)
            && Mathf.Approximately(combat.DamageMultiplier, multiplier), "Unintercepted solo green note heals and buffs the boss");
        combat.RemoveAttackBuff(combat);
        Set(playerHealth, "curBlood", 9000f);
        float playerHealing = Get<float>(green, "playerHealing"), bossBeforeCatch = bossHealth.CurBlood;
        green.Launch(combat, players, enemies, playerCenter, true, new Vector2(playerCenter.x - 30f, playerCenter.x + 30f));
        Invoke(green, "FixedUpdate");
        Check(!green.IsFlying && Mathf.Approximately(playerHealth.CurBlood, 9000f + playerHealing)
            && Mathf.Approximately(bossHealth.CurBlood, bossBeforeCatch) && Mathf.Approximately(combat.DamageMultiplier, 1f),
            "Intercepting a green note heals the player and prevents enemy healing and buffing");

        //同种音符可以同时存在，回收后的成员会被复用而非覆盖仍在飞行的实例。
        pool.ReleaseAll();
        Vector2 poolPosition = playerCenter + Vector2.right * 3f;
        pool.Fire(noteAssets[0], combat, poolPosition, false);
        EastNoteManager pooledFirst = FirstFlying(pool);
        pool.Fire(noteAssets[0], combat, poolPosition, false);
        Check(Flying(pool) == 2, "Two notes of the same prefab coexist in the shared pool");
        pooledFirst.StopEffect();
        pool.Fire(noteAssets[0], combat, poolPosition, false);
        Check(pooledFirst.IsFlying && Flying(pool) == 2, "A returned note is reused without replacing the other live note");
        pool.ReleaseAll();
        Check(Flying(pool) == 0, "Releasing the note pool returns every active colour");

        lastTimes[0] = Time.time - 101f; Set(combat, "nextAttackTime", Time.time);
        Require(combat.BeginAttack(0, true), "A final pre-fire action is available for the death interruption check");
        bossHealth.TakeDamage(new DamageInfo(player.transform, 100000f, false, false));
        combat.CheckNote(9);
        Check(bossHealth.Dead && !combat.IsCasting && Flying(pool) == 0, "Death cancels an unfinished note and no posthumous emission occurs");
        Check(sceneBossHealth != null && !sceneBossHealth.Dead && SceneManager.GetActiveScene().name == "Level3Scene",
            "Validation leaves the real battle alive and never visits an auto-save result scene");
    }

    private static void PreparePlayer(PlayerController player)
    {
        player.enabled = false;
        InputReader input = player.GetComponent<InputReader>();
        if (input != null) input.enabled = false;
        Health health = player.GetComponent<Health>();
        health.ClearDebuffs(); health.AttackedEnd();
        Set(health, "beginBlood", 10000f); Set(health, "curBlood", 10000f);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private static void MovePlayer(PlayerController player, Vector3 location)
    {
        player.transform.position = location;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = location; body.linearVelocity = Vector2.zero; }
        Physics2D.SyncTransforms();
    }

    private static GameObject Spawn(GameObject prefab, Vector3 location, Transform target)
    {
        GameObject instance = Object.Instantiate(prefab, location, Quaternion.identity);
        if (!instance.activeSelf) instance.SetActive(true);
        instance.GetComponent<EnemyMovement>().SetTarget(target);
        Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
        if (body != null) { body.linearVelocity = Vector2.zero; body.constraints = RigidbodyConstraints2D.FreezeAll; }
        Health health = instance.GetComponent<Health>();
        Set(health, "beginBlood", 1000f); Set(health, "curBlood", 1000f);
        return instance;
    }

    private static EastNoteManager SpawnNote(EastNoteManager prefab, EastCombat source, PlayerManager players,
        LevelEnemyManager enemies, Vector2 location)
    {
        EastNoteManager instance = Object.Instantiate(prefab);
        if (!instance.gameObject.activeSelf) instance.gameObject.SetActive(true);
        Bounds player = players.CurrentPlayer.GetComponent<Collider2D>().bounds;
        instance.Launch(source, players, enemies, location, true, new Vector2(player.center.x - 40f, player.center.x + 40f));
        return instance;
    }

    private static HashSet<EnemyController> ActiveEnemies()
    {
        HashSet<EnemyController> result = new();
        foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (enemy.gameObject.activeInHierarchy) result.Add(enemy);
        return result;
    }

    private static EnemyController NewEnemy(HashSet<EnemyController> before)
    {
        foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (!before.Contains(enemy) && enemy.gameObject.activeInHierarchy) return enemy;
        return null;
    }

    private static int Flying(EastNotePool pool)
    {
        int count = 0;
        foreach (EastNoteManager note in pool.GetComponentsInChildren<EastNoteManager>(true)) if (note.IsFlying) count++;
        return count;
    }

    private static EastNoteManager FirstFlying(EastNotePool pool)
    {
        foreach (EastNoteManager note in pool.GetComponentsInChildren<EastNoteManager>(true)) if (note.IsFlying) return note;
        return null;
    }

    private static void ImageCheck(BossUI ui, Health health, string description)
    {
        UnityEngine.UI.Image fill = Get<UnityEngine.UI.Image>(ui, "bloodFull");
        Check(fill != null && Mathf.Abs(fill.fillAmount - health.CurBlood / health.BeginBlood) < 0.0001f, description);
    }

    private static void ValidateBossSprites(EastController controller)
    {
        Require(controller != null, "Boss prefab uses EastController");
        foreach (string field in new[] { "Idle", "Walk", "Attacked", "Die", "NormalAttack", "StunAttack", "SummonAttack", "BuffAttack" })
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
            Check(valid, "EastKing " + field + " has sixteen valid non-overlapping rectangular sprites");
            Check(data != null && data.fps > 0f && data.loop == (field == "Idle" || field == "Walk"),
                "EastKing " + field + " has a positive frame rate and the intended loop setting");
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

    private static void Check(bool condition, string description, string detail = null)
    {
        if (condition) { passed.Add(description); Debug.Log("[EastKing validation PASS] " + description); }
        else
        {
            string failure = string.IsNullOrEmpty(detail) ? description : description + " -- " + detail;
            failures.Add(failure);
            Debug.Log("[EastKing validation FAIL] " + failure);
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
        Debug.Log($"[EastKing validation] passed={passed.Count}, failed={failures.Count}, errors={errors.Count}; {ReportFolder}/validation-report.json");
        if (Application.isBatchMode) EditorApplication.Exit(report.success ? 0 : 1);
        else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();//图形编辑器保留打开状态，退出Play Mode自动还原测试运行对象
    }
}

#endif

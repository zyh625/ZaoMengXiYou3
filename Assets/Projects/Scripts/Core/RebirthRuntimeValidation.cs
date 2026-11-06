#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

//原生Play Mode验收探针，不进入Player构建；所有存档均重定向到独立QA目录。
public sealed class RebirthRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder report = new();
    private static string Audit => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/RebirthAudit"));
    private string originalPath;
    private void Awake() => DontDestroyOnLoad(gameObject);
    private IEnumerator Start()
    {
        Directory.CreateDirectory(Audit); originalPath = SaveSystem.path;
        SaveSystem.path = Path.Combine(Audit, "SaveFixtures"); Directory.CreateDirectory(SaveSystem.path);
        IEnumerator run = Scenario();
        while (true)
        {
            object wait;
            try { if (!run.MoveNext()) break; wait = run.Current; }
            catch (Exception error)
            {
                report.AppendLine("FAIL: " + error); Flush(); SaveSystem.path = originalPath;
                Debug.LogError("REBIRTH_RUNTIME_FAIL: " + error); EditorApplication.isPlaying = false; yield break;
            }
            yield return wait;
        }
        report.AppendLine("ALL PASS"); Flush(); SaveSystem.path = originalPath;
        Debug.Log("REBIRTH_RUNTIME_PASS"); EditorApplication.isPlaying = false;
    }
    private void Flush() => File.WriteAllText(Path.Combine(Audit, "runtime-validation.txt"), report.ToString());
    private void Check(bool value, string text)
    {
        if (!value) throw new InvalidOperationException(text);
        report.AppendLine("PASS: " + text); Flush();
    }
    private static bool Near(double a, double b, double tolerance = 0.03) => Math.Abs(a - b) < tolerance;
    private static FieldInfo Field(object obj, string name)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(obj.GetType().Name, name);
    }
    private static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method != null) return method.Invoke(obj, args);
        }
        throw new MissingMethodException(obj.GetType().Name, name);
    }
    private static int Progress() => (int)typeof(BattleRun).GetMethod("CalculateProgress", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    private static string PlayersJSON(SaveData save) => string.Join("|", save.players.Select(p => JsonUtility.ToJson(p)));
    private static string[] AliveNames() => FindObjectsByType<EnemyController>().Select(e => e.name).ToArray();
    private void CheckUI(bool rebirth, bool next)
    {
        VictoryControl control = FindAnyObjectByType<VictoryControl>();
        Check(control != null && control.CanRebirth == rebirth
            && Get<Button>(control, "rebirthButton").gameObject.activeSelf == rebirth
            && Get<Button>(control, "nextLevelButton").gameObject.activeSelf == next,
            $"VictoryScene level {GameSession.instance.battle.curLevel}, victory {GameSession.instance.battle.victory}: rebirth={rebirth}, next={next}");
    }
    private void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera camera = Camera.main; if (camera == null) return;
        RenderTexture rt = new(1600, 900, 24); RenderTexture previous = camera.targetTexture;
        Canvas[] canvases = FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        Camera[] cameras = canvases.Select(c => c.worldCamera).ToArray(); float[] distances = canvases.Select(c => c.planeDistance).ToArray();
        camera.targetTexture = rt;
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; canvases[i].planeDistance = camera.nearClipPlane + 0.5f; }
        Canvas.ForceUpdateCanvases();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt });
        RenderTexture active = RenderTexture.active; RenderTexture.active = rt; Texture2D png = new(1600, 900, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0); png.Apply(); File.WriteAllBytes(Path.Combine(Audit, name + ".png"), png.EncodeToPNG());
        RenderTexture.active = active; camera.targetTexture = previous;
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
        rt.Release(); Destroy(rt); Destroy(png); report.AppendLine("IMAGE: " + name + ".png"); Flush();
    }

    private void CompleteTutorial(LevelManager level, LevelEnemyManager manager, PlayerController hero)
    {
        level.enabled = false; hero.GetComponent<Health>().TrainingPreventHealthLoss = true;
        int[] left = Get<int[]>(level, "leftEnemyMax"), right = Get<int[]>(level, "rightEnemyMax");
        for (int wave = 0; wave < left.Length; wave++)
        {
            Set(level, "curWave", wave); Set(level, "leftEnemy", 0); Set(level, "rightEnemy", 0); Call(level, "UpdateWaveProgress");
            foreach (bool side in new[] { false, true })
            {
                for (int i = 0; i < (side ? right[wave] : left[wave]); i++)
                {
                    Call(level, "TryCreateEnemy", side);
                    EnemyController enemy = FindObjectsByType<EnemyController>().Single(); enemy.enabled = false;
                    enemy.GetComponent<Health>().TakeDamage(new DamageInfo(hero.transform, 1000000f, false)); enemy.gameObject.SetActive(false);
                }
            }
            Check(manager.AliveNum() == 0 && Progress() == Mathf.FloorToInt(100f / left.Length * (wave + 1)),
                $"tutorial wave {wave + 1} clears its scaled total and uses a 100-percent wave budget");
            Call(level, "TryNextWave");
        }
    }

    private IEnumerator Scenario()
    {
        GameSession session = GameSession.EnsureSession(); session.ClearSelectedFile();
        Check(SaveData.CreatNew("new").rebirth == 0 && session.Rebirth == 0, "new save and unselected session report rebirth zero");
        const string oldJSON = "{\"name\":\"old\",\"gameLevel\":6,\"gold\":12345,\"players\":[{\"id\":0,\"playerLevel\":8,\"exp\":7,\"skillLevels\":[2,3,4,5]}]}";
        File.WriteAllText(Path.Combine(SaveSystem.path, "save0.json"), oldJSON);
        session.SelectFile(0, "old");
        Check(session.Rebirth == 0 && session.data.gold == 12345 && session.data.gameLevel == 6
            && session.data.GetPlayer(PlayerId.WuKong).skillLevels.Length == 4, "legacy JSON loads as rebirth zero without losing progress, gold or extended skill data");
        SaveData negative = JsonUtility.FromJson<SaveData>("{\"rebirth\":-12}"); negative.Normalize();
        Check(negative.rebirth == 0 && negative.players.Count == 4, "Normalize corrects negative rebirth and fills old character records");
        foreach (int n in new[] { 0, 1, 10, 11 })
        {
            RebirthScaling.Multipliers m = RebirthScaling.For(n); int capped = Math.Min(n, 10);
            Check(Near(m.HP, 1 + 0.35 * capped) && Near(m.Damage, 1 + 0.15 * capped)
                && Near(m.Gold, 1 + 0.25 * capped) && Near(m.Experience, 1 + 0.2 * capped)
                && Near(m.Quantity, 1 + 0.08 * capped) && Near(m.AttackCooldown, Math.Max(0.75, 1 - 0.03 * capped))
                && Near(m.SpawnDelay, Math.Max(0.70, 1 - 0.03 * capped)) && Near(m.Movement, 1 + 0.02 * capped)
                && m.ExtraAlive == (n == 0 ? 0 : 1), $"all nine formulas match at rebirth {n}");
            int[] input = { 0, 10, 25 }; int[] scaled = m.EnemyCounts(input);
            Check(input[1] == 10 && !ReferenceEquals(input, scaled) && scaled[2] == m.EnemyCount(25), $"wave arrays copy bases and use ceiling integer counts at rebirth {n}");
        }
        session.data.rebirth = 10;
        Check(RebirthScaling.ForLevel(7).N == 0 && RebirthScaling.ForLevel(8).N == 0 && RebirthScaling.ForLevel(9).N == 0
            && RebirthScaling.Current.N == 0, "levels seven to nine, menus and MapScene retain baseline rules");
        TrainingSession.Begin(); Check(RebirthScaling.ForLevel(6).N == 0, "training suppresses rebirth even with a selected tenth-round save"); TrainingSession.End();

        Dictionary<int, (int[] left, int[] right, float delay, float cap)> baselines = new();
        foreach (int n in new[] { 0, 1, 10, 11 })
        {
            session.data.rebirth = n;
            foreach (int levelIndex in n == 11 ? new[] { 6 } : new[] { 1, 2, 3, 4, 5, 6 })
            {
                session.battle = new BattleData { curLevel = levelIndex };
                yield return SceneManager.LoadSceneAsync("Level" + levelIndex + "Scene"); yield return null;
                LevelManager level = FindObjectsByType<LevelManager>().FirstOrDefault(m => m.isActiveAndEnabled);
                float initDeadline = Time.realtimeSinceStartup + 3f;
                while (level != null && !level.IsInitialized && Time.realtimeSinceStartup < initDeadline) yield return null;
                Check(level != null && level.IsInitialized, $"level {levelIndex} initializes at rebirth {n}");
                level.enabled = false; LevelEnemyManager manager = level.EnemyManager;
                PlayerManager party = FindAnyObjectByType<PlayerManager>(); PlayerController hero = party.CurrentPlayer;
                hero.GetComponent<Health>().TrainingPreventHealthLoss = true;
                int[] left = Get<int[]>(level, "leftEnemyMax"), right = Get<int[]>(level, "rightEnemyMax");
                if (n == 0) baselines[levelIndex] = ((int[])left.Clone(), (int[])right.Clone(), level.RuntimeEnemyDelay, level.RuntimeEnemyMax);
                var b = baselines[levelIndex]; var m = RebirthScaling.For(n);
                m.ScaleWaves(b.left, b.right, out int[] expectedLeft, out int[] expectedRight);
                Check(left.SequenceEqual(expectedLeft) && right.SequenceEqual(expectedRight)
                    && left.Sum() + right.Sum() == m.EnemyCount(b.left.Sum() + b.right.Sum())
                    && Near(level.RuntimeEnemyDelay, b.delay * m.SpawnDelay) && Near(level.RuntimeEnemyMax, b.cap + m.ExtraAlive),
                    $"level {levelIndex} wave quantities, spawn delay and live cap match rebirth {n}");
                Check(Progress() == 0, $"level {levelIndex} starts with zero wave progress at rebirth {n}");
                if (manager.CurBoss != null)
                {
                    Health boss = manager.CurBoss.GetComponent<Health>(); Health bossPrefab = manager.BossPrefab.GetComponent<Health>();
                    Check(Near(boss.BeginBlood, bossPrefab.BeginBlood * m.HP) && Near(manager.CurBoss.GetComponent<EnemyCombat>().DamageMultiplier, m.Damage),
                        $"level {levelIndex} boss HP and damage scale once at rebirth {n}");
                }
                else Check(levelIndex == 1, $"only the first tutorial has no boss at rebirth {n}");
                StageEnemyManager stage = manager as StageEnemyManager;
                if (stage != null)
                {
                    StageEnemyManager.EnemyPool[] configs = Get<StageEnemyManager.EnemyPool[]>(stage, "enemyPools");
                    float[] weights = configs.Select(c => c.weight).ToArray();
                    foreach (StageEnemyManager.EnemyPool config in configs.Where(c => c.prefab != null && c.poolSize > 0))
                    {
                        for (int i = 0; i < configs.Length; i++) configs[i].weight = configs[i] == config ? 1f : 0f;
                        Check(stage.TryCreateEnemy(false, hero.transform.position.y), $"{config.prefab.name} spawns at rebirth {n}");
                        EnemyController enemy = FindObjectsByType<EnemyController>().Single(e => e.transform != manager.CurBoss);
                        Health health = enemy.GetComponent<Health>(); EnemyCombat combat = enemy.GetComponent<EnemyCombat>(); enemy.enabled = false;
                        float expected = config.prefab.GetComponent<Health>().BeginBlood * m.HP;
                        Check(Near(health.BeginBlood, expected) && Near(health.CurBlood, expected) && Near(combat.DamageMultiplier, m.Damage),
                            $"{config.prefab.name} enters full health at scaled HP {expected:0.##} and damage x{m.Damage:0.##} (N={n})");
                        EnemyMovement move = enemy.GetComponent<EnemyMovement>(); move.SetTarget(hero.transform);
                        move.Move(false); Check(Near(Math.Abs(enemy.GetComponent<Rigidbody2D>().linearVelocityX), Get<float>(move, "moveSpeed") * m.Movement),
                            $"{config.prefab.name} movement multiplier is read without modifying base speed (N={n})");
                        EnemyCheckAttack[] checks = Get<EnemyCheckAttack[]>(combat, "CheckAttackLogic");
                        if (checks.Length > 0 && checks[0].gameObject.activeInHierarchy)
                        {
                            BoxCollider2D checkBox = checks[0].GetComponent<BoxCollider2D>(); checkBox.offset = Vector2.zero;
                            checks[0].transform.position = hero.transform.position; Physics2D.SyncTransforms();
                            Call(checks[0], "OnTriggerEnter2D", hero.GetComponent<Collider2D>());
                            float[] last = Get<float[]>(combat, "lastAttack"); float delay = Get<float[]>(combat, "curAttackDelay")[0] * m.AttackCooldown;
                            last[0] = Time.time - delay + 0.02f; combat.CheckAttack(); bool early = !combat.AttackRequest[0];
                            last[0] = Time.time - delay - 0.02f; combat.CheckAttack();
                            Check(early && combat.AttackRequest[0], $"{config.prefab.name} base attack request opens only after scaled cooldown (N={n})");
                        }
                        if (levelIndex == 1 && n == 0)
                        { enemy.transform.position = hero.transform.position + Vector3.right * 5f; Physics2D.SyncTransforms(); Capture("level1-tutorial"); }
                        health.TakeDamage(new DamageInfo(hero.transform, 1f, false)); enemy.gameObject.SetActive(false);
                        stage.TryCreateEnemy(false, hero.transform.position.y);
                        EnemyController reused = FindObjectsByType<EnemyController>().Single(e => e.transform != manager.CurBoss);
                        Check(reused == enemy && Near(reused.GetComponent<Health>().CurBlood, expected) && Near(combat.DamageMultiplier, m.Damage),
                            $"{config.prefab.name} pool reuse restores full scaled HP without multiplying again (N={n})");
                        reused.gameObject.SetActive(false);
                    }
                    for (int i = 0; i < configs.Length; i++) configs[i].weight = weights[i];
                    if (levelIndex == 3)
                    {
                        Check(stage.TryCreateSummonedEnemy(hero.transform.position), $"boss summon spawns at rebirth {n}");
                        EnemyController summon = FindObjectsByType<EnemyController>().Single(e => e.transform != manager.CurBoss);
                        var config = configs.First(c => summon.name.StartsWith(c.prefab.name));
                        Check(Near(summon.GetComponent<Health>().BeginBlood, config.prefab.GetComponent<Health>().BeginBlood * 0.65f * m.HP)
                            && Near(summon.GetComponent<EnemyCombat>().DamageMultiplier, 0.7f * m.Damage), $"summon reduction and rebirth factors compose from prefab bases (N={n})");
                        summon.gameObject.SetActive(false);
                    }
                }
                if (levelIndex == 1)
                {
                    CompleteTutorial(level, manager, hero);
                    Check(Progress() == 100 && Get<ExitManager>(level, "curExit").gameObject.activeSelf,
                        $"all tutorial waves reach 100 percent and open the exit without a boss (N={n})");
                    Check(left.Sum() + right.Sum() == (n == 0 ? 24 : n == 1 ? 26 : 44), $"tutorial total is ceiling(24 x quantity), without per-side rounding inflation (N={n})");
                }
                if (levelIndex == 6)
                {
                    float[] rewards = { 20f * m.Experience, 240f * m.Experience };
                    NorthCombat north = manager.CurBoss.GetComponent<NorthCombat>(); manager.CurBoss.gameObject.SetActive(true);
                    manager.CurBoss.GetComponent<EnemyController>().enabled = false;
                    Set(north, "nextAttack", 0f);
                    Check(north.BeginAttack(0) && Near(Get<float[]>(north, "readyAt")[0] - Time.time, 4f * m.AttackCooldown), $"NorthKing custom skill cooldown scales at rebirth {n}");
                    north.CancelAttack(); Check(Near(north.NextAttackAt - Time.time, 2.2f * m.AttackCooldown), $"boss public attack interval scales at rebirth {n}");
                    manager.CurBoss.gameObject.SetActive(false);
                    //实际完整小怪波次：每次出池、扣血死亡、奖励、退池、推进都走生产入口。
                    float beforeWavesXP = hero.GetComponent<PlayerResources>().Experience;
                    for (int wave = 0; wave < left.Length; wave++)
                    {
                        Set(level, "curWave", wave); Set(level, "leftEnemy", 0); Set(level, "rightEnemy", 0); Call(level, "UpdateWaveProgress");
                        foreach (bool side in new[] { false, true })
                        {
                            int total = side ? right[wave] : left[wave];
                            for (int i = 0; i < total; i++)
                            {
                                Call(level, "TryCreateEnemy", side);
                                EnemyController enemy = FindObjectsByType<EnemyController>().Single(e => e.transform != manager.CurBoss); enemy.enabled = false;
                                enemy.GetComponent<Health>().TakeDamage(new DamageInfo(hero.transform, 1000000f, false)); enemy.gameObject.SetActive(false);
                            }
                        }
                        Check(Get<int>(level, "leftEnemy") == left[wave] && Get<int>(level, "rightEnemy") == right[wave]
                            && Progress() == Mathf.FloorToInt(70f / left.Length * (wave + 1)), $"scaled wave {wave + 1} spawned and killed exactly its planned total (N={n})");
                        Call(level, "TryNextWave");
                    }
                    Check(Near(hero.GetComponent<PlayerResources>().Experience - beforeWavesXP, (left.Sum() + right.Sum()) * 20f * m.Experience, 0.2),
                        $"every scaled wave kill awards scaled experience exactly once (N={n}, actual={hero.GetComponent<PlayerResources>().Experience-beforeWavesXP}, expected={(left.Sum()+right.Sum())*20f*m.Experience})");
                    Check(Get<bool>(level, "bossStart") && manager.AliveNum() == 0, $"scaled waves reach boss phase without leftovers (N={n})");
                    //保证落币在真实地面，首杀基础40金币。
                    GoldManager gold = party.GetComponent<GoldManager>(); session.data.defeatedBossLevels.Remove(6);
                    manager.CurBoss.position = hero.transform.position + Vector3.right * 3f; manager.CurBoss.gameObject.SetActive(true);
                    manager.CurBoss.GetComponent<EnemyController>().enabled = false;
                    float experience = hero.GetComponent<PlayerResources>().Experience;
                    int beforeGold = session.data.gold; gold.Drop(manager.CurBoss, true);
                    GoldCoin coin = gold.GetComponentsInChildren<GoldCoin>(true).FirstOrDefault(c => c.gameObject.activeSelf && c.Amount == m.GoldAmount(40));
                    Check(coin != null && coin.Amount == m.GoldAmount(40), $"boss gold amount scales once before pickup to {m.GoldAmount(40)} (N={n})");
                    Call(gold, "CompletePickup", coin);
                    Check(session.data.gold == beforeGold + m.GoldAmount(40) && !coin.gameObject.activeSelf, $"scaled gold is credited only on completed pickup (N={n})");
                    manager.CurBoss.GetComponent<Health>().TakeDamage(new DamageInfo(hero.transform, 1000000f, false));
                    Check(Near(hero.GetComponent<PlayerResources>().Experience - experience, 240f * m.Experience, 0.1), $"boss experience reward is scaled exactly once (N={n})");
                    Check(manager.CurBoss.GetComponent<Health>().Dead && Progress() == 100, $"scaled boss death produces 100-percent progress (N={n})");
                    manager.CurBoss.gameObject.SetActive(false); Call(level, "CheckBossOver");
                    Check(Get<ExitManager>(level, "curExit").gameObject.activeSelf, $"exit opens after scaled boss death (N={n})");
                    Check(BattleRun.Finish(true).victory && BattleRun.LastResult.progress == 100, $"scaled battle victory freezes correct settlement (N={n})");
                    report.AppendLine($"DATA N={n}: GreenHammer HP={150 * m.HP:0.##}, sweep={19 * m.Damage:0.##}, XP={rewards[0]:0.##}; NorthKing HP={1400 * m.HP:0.##}, bash={25 * m.Damage:0.##}, XP={rewards[1]:0.##}; bossGold={m.GoldAmount(40)}; waveTotal={left.Sum() + right.Sum()}; delay={level.RuntimeEnemyDelay:0.###}; cap={level.RuntimeEnemyMax:0.##}"); Flush();
                }
            }
        }

        //保存失败绝不消耗结算，也不清空角色养成。
        session.data.rebirth = 0; session.data.gameLevel = 6; session.data.gold = 54321;
        foreach (PlayerId id in Enum.GetValues(typeof(PlayerId)))
        { PlayerData p = session.data.GetPlayer(id); p.playerLevel = 8; p.exp = 7f; p.skillLevels = new[] { 2, 3, 4, 5 }; }
        string preserved = PlayersJSON(session.data); session.battle = new BattleData { curLevel = 6, victory = true };
        BattleData failedResult = session.battle; string fixtures = SaveSystem.path;
        string blocked = Path.Combine(Audit, "blocked-save-path"); File.WriteAllText(blocked, "QA fixture"); SaveSystem.path = blocked;
        Check(!session.TryBeginRebirth(failedResult) && session.Rebirth == 0 && session.battle == failedResult && PlayersJSON(session.data) == preserved,
            "save failure leaves rebirth, battle result and character progression intact for retry"); SaveSystem.path = fixtures;
        foreach (var test in new[] { (5, true), (6, false), (7, true) })
        {
            session.battle = new BattleData { curLevel = test.Item1, victory = test.Item2 };
            yield return SceneManager.LoadSceneAsync("VictoryScene"); yield return null;
            CheckUI(false, test.Item1 != 6);
        }
        session.data.gameLevel = 6; session.battle = new BattleData { curLevel = 6, victory = true };
        yield return SceneManager.LoadSceneAsync("VictoryScene"); yield return null; CheckUI(true, false);
        Check(session.data.gameLevel == 6, "winning level six keeps unlock progress at six");
        Button rebirth = Get<Button>(FindAnyObjectByType<VictoryControl>(), "rebirthButton");
        Check(rebirth.onClick.GetPersistentEventCount() == 1 && rebirth.onClick.GetPersistentMethodName(0) == "EnterRebirth", "serialized rebirth button calls the runtime transition");
        Capture("victory-rebirth");
        rebirth.onClick.Invoke(); yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "Level1Scene" && session.Rebirth == 1 && session.battle.curLevel == 1
            && BattleRun.CurrentResult == session.battle && BattleRun.LastResult == null && Progress() == 0,
            "actual button click saves rebirth one, clears previous statistics and starts a fresh Level1 battle");
        SaveData disk = SaveSystem.Load(0);
        Check(disk.rebirth == 1 && disk.gold == 54321 && disk.gameLevel == 6 && PlayersJSON(disk) == preserved
            && session.data.gold == 54321 && PlayersJSON(session.data) == preserved, "disk and memory preserve all four characters, extended skill levels, gold and unlocks after rebirth");
        PlayerManager retainedParty = FindAnyObjectByType<PlayerManager>();
        Check(retainedParty != null && retainedParty.Players.Count == 4 && retainedParty.CurrentPlayer.GetComponent<PlayerResources>().Level == 8,
            "reborn modern Level1 retains all four unlocked characters and their level");
        Check(FindAnyObjectByType<StageManager>() != null && FindAnyObjectByType<PlayerManager>() != null,
            "Level1 runs on the shared stage flow and PlayerManager");

        //新档从悟空开始，真正清完第一关并通过出口交互结算，随后逐关验证加入次序。
        File.WriteAllText(Path.Combine(SaveSystem.path, "save1.json"), JsonUtility.ToJson(SaveData.CreatNew("NewCampaign")));
        session.SelectFile(1, "NewCampaign");
        Check(session.data.unlocked == 1 && !session.TryLearnSkill(PlayerId.BaJie, 1) && !session.TryLearnSkill(PlayerId.Tang, 1),
            "fresh save unlocks only Wukong and cannot purchase locked companions' skills");
        yield return SceneManager.LoadSceneAsync("Level1Scene"); yield return null; yield return null;
        StageManager tutorial = FindAnyObjectByType<StageManager>(); tutorial.enabled = false;
        PlayerManager freshParty = FindAnyObjectByType<PlayerManager>(); PlayerController firstHero = freshParty.CurrentPlayer;
        Check(freshParty.Players.Count == 1 && firstHero.GetComponent<PlayerResources>().Id == PlayerId.WuKong,
            "fresh first level creates only Wukong through the shared PlayerManager");
        freshParty.SwitchNext(); Check(freshParty.CurrentPlayer == firstHero, "Q/E cannot select a locked companion");
        SkillManager skills = FindAnyObjectByType<SkillManager>(FindObjectsInactive.Include); skills.gameObject.SetActive(true); yield return null;
        Check(skills.GetComponentsInChildren<Button>(true).Where(b => b.name is "bajie" or "tang" or "sha").All(b => !b.interactable),
            "locked companions' skill tabs are visibly disabled"); skills.gameObject.SetActive(false); GamePause.Resume();
        Health firstHealth = firstHero.GetComponent<Health>(); firstHealth.TakeDamage(new DamageInfo(null, firstHealth.BeginBlood * 0.5f, false));
        int state = (int)typeof(BattleRun).GetMethod("AverageHealth", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(state < 100 && state == Mathf.FloorToInt(firstHealth.CurBlood / firstHealth.BeginBlood * 100f), "first-level health settlement averages the unlocked party only"); firstHealth.AttackedEnd();
        CompleteTutorial(tutorial, tutorial.EnemyManager, firstHero);
        ExitManager portal = Get<ExitManager>(tutorial, "curExit");
        BoxCollider2D heroBody = firstHero.GetComponent<BoxCollider2D>();
        firstHero.transform.position += portal.GetComponent<BoxCollider2D>().bounds.center - heroBody.bounds.center;
        Physics2D.SyncTransforms(); Call(portal, "OnTriggerEnter2D", heroBody); firstHero.GetComponent<PlayerInteract>().OnInteract();
        Check(portal.PlayerIn, "tutorial exit recognizes the real active player's body"); Call(tutorial, "CheckGameOver");
        yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "VictoryScene" && session.data.gameLevel == 2 && session.data.IsPlayerUnlocked(PlayerId.BaJie)
            && !session.data.IsPlayerUnlocked(PlayerId.Tang) && !session.data.IsPlayerUnlocked(PlayerId.Sha),
            "actual Level1 exit interaction settles victory and unlocks Bajie, preserving later locks");
        yield return SceneManager.LoadSceneAsync("Level2Scene"); yield return null; yield return null;
        Check(FindAnyObjectByType<PlayerManager>().Players.Select(p => p.GetComponent<PlayerResources>().Id).ToHashSet().SetEquals(new[] { PlayerId.WuKong, PlayerId.BaJie }),
            "level two starts with exactly Wukong and Bajie");
        session.battle = BattleRun.Finish(true); yield return SceneManager.LoadSceneAsync("VictoryScene"); yield return null;
        Check(session.data.IsPlayerUnlocked(PlayerId.Tang) && !session.data.IsPlayerUnlocked(PlayerId.Sha), "winning level two unlocks Tang next");
        yield return SceneManager.LoadSceneAsync("Level3Scene"); yield return null; yield return null;
        Check(FindAnyObjectByType<PlayerManager>().Players.Count == 3 && FindAnyObjectByType<PlayerManager>().Players.Any(p => p.GetComponent<PlayerResources>().Id == PlayerId.Tang),
            "level three has Wukong, Bajie and Tang");
        session.battle = BattleRun.Finish(true); yield return SceneManager.LoadSceneAsync("VictoryScene"); yield return null;
        Check(session.data.unlocked == 15 && SaveSystem.Load(1).unlocked == 15, "winning level three unlocks Sha and saves all four companions");
        //重新选回轮回测试档，确认跨存档独立。
        session.SelectFile(0, "old");
        session.data.rebirth = 3;
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return null;
        Map map = FindAnyObjectByType<Map>(); Text mapLabel = Get<Text>(map, "rebirthLabel");
        Check(mapLabel.text == "第 3 轮回" && Get<Button[]>(map, "buttons").Take(6).All(b => b.gameObject.activeSelf), "MapScene displays current rebirth and keeps all six unlocked entries");
        mapLabel.font.RequestCharactersInTexture("再入轮回第", 24);
        Check("再入轮回第".All(c => mapLabel.font.HasCharacter(c)), "runtime Chinese font contains every character in rebirth labels and button");
        Capture("map-rebirth");
        SaveSystem.TrySave(); yield return SceneManager.LoadSceneAsync("MenuScene"); yield return null;
        TMPro.TextMeshProUGUI[] slotLabels = Get<TMPro.TextMeshProUGUI[]>(FindAnyObjectByType<MenuButton>(), "fileInformantion");
        Check(slotLabels.Length == 6 && slotLabels[0].text.Contains("第 3 轮回") && !slotLabels[5].text.Contains("轮回"), "save slots display stored rebirth and hide zero in one text");
        FindAnyObjectByType<MenuButton>().ContinueGame(); Capture("save-slots");
        session.ClearSelectedFile(); yield return SceneManager.LoadSceneAsync("MapScene"); yield return null;
        Check(Get<Text>(FindAnyObjectByType<Map>(), "rebirthLabel").text == "第 0 轮回", "unselected MapScene displays rebirth zero even if old data remains in memory");
    }
}
#endif

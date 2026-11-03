#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using zmxy.GamePlay;
using Object = UnityEngine.Object;

// Real prefabs, SpriteManager clocks and Physics2D; this probe is excluded from players.
public sealed class PressureKettleRuntimeValidation : MonoBehaviour
{
    public bool IsolatedCalibration;
    public string AuditDirectory = "Logs/PressureKettleAudit";
    private readonly StringBuilder result = new();
    private const string EnemyPath = "Assets/Projects/Prefabs/Enemy/PressureKettle.prefab";
    private const string FxRoot = "Assets/Projects/Prefabs/SeparateEffort/PressureKettle";
    private IEnumerator Start()
    {
        IEnumerator scenario = Scenario();
        while (true)
        {
            object wait;
            try { if (!scenario.MoveNext()) break; wait = scenario.Current; }
            catch (Exception e) { result.AppendLine("FAIL: " + e); Finish(false); yield break; }
            yield return wait;
        }
        result.AppendLine("ALL PASS"); Finish(true);
    }
    private void Finish(bool pass)
    {
        File.WriteAllText(Path.GetFullPath(Path.Combine(AuditDirectory, "runtime-validation.txt")), result.ToString());
        if (pass) Debug.Log("PRESSURE_KETTLE_RUNTIME_PASS"); else Debug.LogError(result.ToString());
        Time.timeScale = 1f; EditorApplication.isPlaying = false;
    }
    private void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); result.AppendLine("PASS: " + message); }
    private static bool Near(float a, float b, float tolerance = 0.03f) => Mathf.Abs(a - b) <= tolerance;
    private static void Edit(Object c, string name, Action<SerializedProperty> write) { var so = new SerializedObject(c); write(so.FindProperty(name)); so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Place(GameObject actor, float x)
    {
        BoxCollider2D box = actor.GetComponent<BoxCollider2D>();
        actor.transform.position = new Vector3(x, 0.025f - (box.offset.y - box.size.y / 2f) * actor.transform.lossyScale.y);
        Rigidbody2D body = actor.GetComponent<Rigidbody2D>(); body.position = actor.transform.position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
    }
    private static void ResetEnemy(GameObject enemy, PlayerController hero)
    {
        enemy.SetActive(false); enemy.SetActive(true);
        enemy.GetComponent<PressureKettleCombat>().BindTarget(hero); enemy.GetComponent<EnemyMovement>().SetTarget(hero.transform); Place(enemy, 0f);
    }
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(0.5f);
        if (!IsolatedCalibration)
        {
            LevelManager level = FindObjectsByType<LevelManager>().FirstOrDefault(l => l.isActiveAndEnabled);
            Check(level != null && level.IsInitialized, "Level7Scene initializes");
            Check(FindObjectsByType<PressureKettleController>(FindObjectsInactive.Include).Length == 10, "real StageEnemyManager prewarms 10 PressureKettle");
        }
        foreach (GameObject root in gameObject.scene.GetRootGameObjects()) if (root != gameObject) root.SetActive(false);
        BattleRun.Begin(7, null, false);
        GameObject floor = new("PKValidationFloor") { layer = 6 }; floor.AddComponent<BoxCollider2D>().size = new Vector2(100f, 1f); floor.transform.position = new Vector3(0f, -0.5f);
        GameObject cameraRoot = new("PKValidationCamera"); cameraRoot.SetActive(false); cameraRoot.tag = "MainCamera";
        Camera camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12f; camera.transform.position = new Vector3(0f, 2f, -10f);
        CameraManager cm = cameraRoot.AddComponent<CameraManager>(); GameObject left = new("Left"), right = new("Right"); left.transform.position = Vector3.left * 40; right.transform.position = Vector3.right * 40;
        Edit(cm, "cameraEdgeLeft", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = left.transform; });
        Edit(cm, "cameraEdgeRight", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = right.transform; }); cameraRoot.SetActive(true);
        GameObject player = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab")); Place(player, 3.8f);
        PlayerController hero = player.GetComponent<PlayerController>(); Health hp = player.GetComponent<Health>();
        GameObject enemy = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath));
        var controller = enemy.GetComponent<PressureKettleController>(); controller.enabled = false;
        var combat = enemy.GetComponent<PressureKettleCombat>(); var movement = enemy.GetComponent<PressureKettleMovement>(); Health eh = enemy.GetComponent<Health>();
        ResetEnemy(enemy, hero);
        var pool = PressureKettleEffectPool.GetOrCreate();
        var bombPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxRoot + "Bomb.prefab").GetComponent<PressureKettleEffect>();
        GameObject armor = new("ValidationArmor"); hp.SetSuperArmor(armor, true);
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        yield return new WaitForSeconds(0.15f);
        Check(eh.BeginBlood == 96f && combat.Pressure == 0f, "96 HP, clean pressure on pooled spawn");
        Check(combat.BeginCast(0) && Near(combat.Pressure, 8f), "attack adds exactly 8 pressure");
        int steamKnockbacks = 0;
        var tickTimes = new System.Collections.Generic.List<float>();
        Action<DamageInfo, float> countSteam = (damage, actual) => { if (damage.Knockback) steamKnockbacks++; tickTimes.Add(Time.time); };
        hp.DamageReceived += countSteam;
        float before = hp.CurBlood, start = Time.time;
        combat.SampleFrame(15); Check(combat.ActiveSteam == null, "no steam before animation windup");
        yield return new WaitForSeconds(combat.Windup + 0.03f);
        combat.SampleFrame(4); Check(combat.ActiveSteam == null, "steam is gated by release frame 5");
        combat.SampleFrame(5); PressureKettleEffect steam = combat.ActiveSteam;
        Check(steam != null && steam.TickCount == 0 && hp.CurBlood == before, "steam opens with no immediate extra tick");
        for (int i = 0; i < 100; i++) combat.SampleFrame(15);
        yield return new WaitForSeconds(1.12f);
        Check(steam.TickCount == 4 && !steam.gameObject.activeSelf && Near(before - hp.CurBlood, 12f), "cone ticks exactly four times: 3 damage every 0.25 seconds, total 12");
        Check(tickTimes.Count == 4 && tickTimes.Skip(1).Select((t, i) => Near(t - tickTimes[i], 0.25f)).All(ok => ok), "steam tick spacing is 0.25 seconds within one physics step");
        hp.DamageReceived -= countSteam;
        Check(steamKnockbacks == 4, "all four steam hits request standard Health knockback");
        Check(Near(combat.CooldownReadyAt(0) - start, 6f), "steam cooldown exactly 6 seconds"); combat.CancelCast();

        hp.Heal(1000f); ResetEnemy(enemy, hero); Place(player, -3.8f);
        Check(combat.BeginCast(0), "left-facing steam can start"); yield return new WaitForSeconds(combat.Windup + 0.03f); combat.SampleFrame(5); steam = combat.ActiveSteam; before = hp.CurBlood;
        yield return new WaitForSeconds(1.1f); Check(steam.TickCount == 4 && Near(before - hp.CurBlood, 12f), "mirrored cone hits toward screen left for total 12"); combat.CancelCast();
        hp.Heal(1000f); ResetEnemy(enemy, hero); Place(player, 5f);
        before = hp.CurBlood;
        var bomb = pool.Bomb(bombPrefab, combat, hero);
        eh.TakeDamage(new DamageInfo(hero.transform, 1000f, false));
        enemy.SetActive(false); ResetEnemy(enemy, hero); // same instance now has a different life
        float peak = bomb.transform.position.y, deadline = Time.time + 3f;
        while (!bomb.HasLanded && Time.time < deadline) { peak = Mathf.Max(peak, bomb.transform.position.y); yield return null; }
        Check(bomb.HasLanded && peak > 1.5f && hp.CurBlood == before, "ballistic bomb survives shooter death and pooled respawn without flight damage");
        deadline = Time.time + 1.5f;
        while (!bomb.HasExploded && Time.time < deadline) yield return null;
        Check(bomb.ExplosionCount == 1 && Near(bomb.ExplodedAt - bomb.LandedAt, 0.6f, 0.05f) && Near(before - hp.CurBlood, 12f), "bomb waits 0.6 seconds after ground contact; one 12-damage explosion");
        yield return new WaitForSeconds(0.8f); Check(bomb.ExplosionCount == 1 && Near(before - hp.CurBlood, 12f), "bomb never deals repeat damage during explosion/fade");
        var reused = pool.Bomb(bombPrefab, combat, hero); Check(reused == bomb && !reused.HasExploded && !reused.HasLanded, "bomb pool reuses clean state"); pool.ReleaseAll();

        ResetEnemy(enemy, hero); Place(player, 2f); hp.Heal(1000f);
        for (int i = 0; i < 13; i++) eh.TakeDamage(new DamageInfo(hero.transform, 1f, false, isPeriodic: true));
        Check(combat.Pressure == 100f && combat.ReleasePending && combat.ReadySkill == 2, "13 actual received hits cap pressure at 100 and override close-range retreat");
        Check(combat.BeginCast(2), "automatic full-pressure release starts");
        before = hp.CurBlood; start = Time.time;
        yield return new WaitForSeconds(combat.Windup + 0.03f); combat.SampleFrame(7);
        for (int i = 0; i < 100; i++) combat.SampleFrame(15);
        Check(combat.ReleaseCount == 1 && Near(before - hp.CurBlood, 11f) && combat.Pressure == 0f && !combat.ReleasePending, "release deals 11 once and clears pressure/pending latch");
        Check(combat.IsOverheated && Near(combat.CooldownReadyAt(2) - start, 12f), "release has 12-second cooldown and starts overheat"); combat.CancelCast();
        eh.SetDamageMultiplier(armor, 0.5f); before = eh.CurBlood;
        eh.TakeDamage(new DamageInfo(hero.transform, 10f, false, isPeriodic: true)); Check(Near(before - eh.CurBlood, 7f), "overheat x1.4 composes with another source x0.5");
        yield return new WaitForSeconds(2.8f); Check(combat.IsOverheated, "overheat remains before its three-second deadline");
        yield return new WaitForSeconds(0.25f); before = eh.CurBlood;
        eh.TakeDamage(new DamageInfo(hero.transform, 10f, false, isPeriodic: true));
        Check(!combat.IsOverheated && Near(before - eh.CurBlood, 5f) && combat.ReleaseCount == 1, "at 3 seconds only overheat source is removed; full-pressure release does not repeat"); eh.RemoveDamageMultiplier(armor);
        Time.timeScale = 4f; yield return new WaitForSeconds(9.2f); Time.timeScale = 1f;
        Check(combat.ReleaseCount == 1 && !combat.ReleasePending && combat.ReadySkill != 2, "full-pressure trigger stays cleared after its 12-second cooldown");

        ResetEnemy(enemy, hero); eh.TakeDamage(new DamageInfo(hero.transform, 60f, false, isPeriodic: true));
        Check(eh.ratio < 0.4f && combat.ReleasePending && combat.BeginCast(2), "crossing below 40 percent HP requests release");
        yield return new WaitForSeconds(combat.Windup + 0.03f); combat.SampleFrame(7); combat.CancelCast();
        Time.timeScale = 4f; yield return new WaitForSeconds(12.2f); Time.timeScale = 1f;
        Check(combat.ReleaseCount == 1 && !combat.ReleasePending && combat.ReadySkill != 2 && !combat.IsOverheated, "remaining below 40 percent after cooldown never retriggers low-health release");
        ResetEnemy(enemy, hero); before = eh.CurBlood; eh.TakeDamage(new DamageInfo(hero.transform, 10f, false, isPeriodic: true));
        Check(Near(before - eh.CurBlood, 10f) && combat.ReleaseCount == 0, "pool disable/respawn removes vulnerability and resets both latches");

        ResetEnemy(enemy, hero); Place(player, 2.5f); controller.enabled = true; yield return new WaitForSeconds(0.2f);
        Check(enemy.GetComponent<Rigidbody2D>().linearVelocityX < -0.7f, "AI retreats below 3.5 units at speed 0.8");
        Place(player, 9f); yield return new WaitForSeconds(0.15f); Check(enemy.GetComponent<Rigidbody2D>().linearVelocityX > 0.7f, "AI approaches beyond 7 units");
        Place(player, 5f); yield return new WaitForSeconds(0.2f); Check(Mathf.Abs(enemy.GetComponent<Rigidbody2D>().linearVelocityX) < 0.01f, "AI stops to cast at middle distance");
        deadline = Time.time + 2.5f; while (combat.LastBomb == null && Time.time < deadline) yield return null;
        Check(combat.LastBomb != null, "real PressureBomb animation releases the pooled projectile");
        controller.enabled = false; pool.ReleaseAll(); ResetEnemy(enemy, hero); Place(player, 3.8f); controller.enabled = true;
        deadline = Time.time + 3f; while (combat.ActiveSteam == null && Time.time < deadline) yield return null;
        steam = combat.ActiveSteam; Check(steam != null, "real 16-fps SteamJet animation emits from Muzzle");
        before = hp.CurBlood; bool heldFinalPose = false;
        SpriteManager sm = enemy.GetComponentInChildren<SpriteManager>();
        while (steam.gameObject.activeSelf && Time.time < deadline)
        {
            heldFinalPose |= sm.IsFinished && combat.CurrentSkill == 0;
            yield return null;
        }
        Check(heldFinalPose && steam.TickCount == 4 && Near(before - hp.CurBlood, 12f), "16-fps pose holds after animation end until all four steam ticks finish");
        yield return new WaitForFixedUpdate(); yield return null;
        Check(combat.CurrentSkill != 0, "steam cast exits after tick four instead of remaining locked");
        controller.enabled = false; pool.ReleaseAll(); ResetEnemy(enemy, hero); Place(player, 2f);
        eh.TakeDamage(new DamageInfo(hero.transform, 60f, false, isPeriodic: true)); controller.enabled = true;
        deadline = Time.time + 2.5f; while (combat.ReleaseCount == 0 && Time.time < deadline) yield return null;
        Check(combat.ReleaseCount == 1 && combat.IsOverheated, "real controller releases automatically at retreat distance through PressureRelease animation");
        eh.TakeDamage(new DamageInfo(hero.transform, 1000f, false)); Check(!combat.IsOverheated, "death immediately removes own overheat multiplier");

        controller.enabled = false; ResetEnemy(enemy, hero);
        for (int i = 0; i < 12; i++) eh.TakeDamage(new DamageInfo(hero.transform, 1f, false, isPeriodic: true));
        eh.TakeDamage(new DamageInfo(hero.transform, 50f, false, isPeriodic: true));
        Check(combat.Pressure == 100f && eh.ratio < 0.4f && combat.BeginCast(2), "simultaneous full-pressure and low-health requests coalesce");
        yield return new WaitForSeconds(combat.Windup + 0.03f); combat.SampleFrame(7); combat.CancelCast();
        Check(combat.ReleaseCount == 1 && !combat.ReleasePending, "coalesced requests produce exactly one release");
        enemy.SetActive(false); ResetEnemy(enemy, hero);
        before = eh.CurBlood; eh.TakeDamage(new DamageInfo(hero.transform, 10f, false, isPeriodic: true));
        Check(Near(before - eh.CurBlood, 10f), "despawning during overheat leaves no multiplier on the next life");
        hp.Heal(1000f);
        for (int i = 0; i < 13; i++) eh.TakeDamage(new DamageInfo(hero.transform, 1f, false, isPeriodic: true));
        hp.SetReflection(armor, 100f);
        Check(combat.BeginCast(2), "release starts against a reflecting player");
        yield return new WaitForSeconds(combat.Windup + 0.03f); combat.SampleFrame(7);
        hp.RemoveReflection(armor);
        Check(eh.Dead && !combat.IsOverheated && combat.CurrentSkill == -1, "lethal reflection inside release cannot reapply overheat after death cleanup");
    }
}
#endif

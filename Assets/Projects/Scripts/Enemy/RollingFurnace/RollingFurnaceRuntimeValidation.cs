#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using zmxy.GamePlay;

public sealed class RollingFurnaceRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder report = new();
    private static string Report => Path.GetFullPath("output/RollingFurnaceAudit/runtime.txt");
    private void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); report.AppendLine("PASS: " + message); File.WriteAllText(Report, report.ToString()); }
    private static bool Near(float a, float b, float tolerance = .04f) => Mathf.Abs(a - b) < tolerance;
    private IEnumerator Start()
    {
        var scenario = Scenario();
        while (true)
        {
            object wait;
            try { if (!scenario.MoveNext()) break; wait = scenario.Current; }
            catch (Exception ex)
            { report.AppendLine("FAIL: " + ex); File.WriteAllText(Report, report.ToString()); Debug.LogException(ex); EditorApplication.isPlaying = false; yield break; }
            yield return wait;
        }
        report.AppendLine("ALL PASS"); File.WriteAllText(Report, report.ToString());
        Debug.Log("ROLLING_FURNACE_RUNTIME_PASS"); EditorApplication.isPlaying = false;
    }
    private static void Place(GameObject go, float x, float ground = 0f)
    {
        var box = go.GetComponent<BoxCollider2D>();
        float feet = (box.offset.y - box.size.y * .5f) * go.transform.lossyScale.y;
        Vector3 p = new(x, ground + .015f - feet, 0); go.transform.position = p;
        if (go.TryGetComponent<Rigidbody2D>(out var body)) { body.position = p; body.linearVelocity = Vector2.zero; }
        Physics2D.SyncTransforms();
    }
    private static void Reset(GameObject enemy, GameObject hero, float playerX = 20f)
    {
        hero.GetComponent<Health>().ClearDebuffs(); hero.GetComponent<Health>().Heal(2000); Place(hero, playerX);
        enemy.SetActive(false); enemy.SetActive(true); enemy.GetComponent<EnemyMovement>().SetTarget(hero.transform);
        Place(enemy, 0); enemy.GetComponent<EnemyController>().SetSpawnSide(false);
    }
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(.5f);
        var stage = FindAnyObjectByType<RollingFurnaceStageManager>();
        Check(stage != null && stage.IsInitialized, "Level7 starts through its local StageManager");
        Check(FindObjectsByType<RollingFurnaceController>(FindObjectsInactive.Include).Length == 10, "Level7 prewarms exactly ten RollingFurnace instances");
        var manager = FindAnyObjectByType<RollingFurnaceStageEnemyManager>();
        var pools = new SerializedObject(manager).FindProperty("enemyPools");
        Check(pools.arraySize >= 1 && manager.GetEnemyPrefabs().Any(p => p is RollingFurnaceController), "scene enemyPools references the generated prefab");
        // Test geometry lives only in this unsaved Play Mode session. Existing scene assets are untouched.
        foreach (var root in gameObject.scene.GetRootGameObjects()) if (root != gameObject) root.SetActive(false);
        var floor = new GameObject("ValidationFloor") { layer = 6 }; floor.transform.position = Vector3.down * .5f;
        floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
        var cameraRoot = new GameObject("ValidationCamera"); cameraRoot.SetActive(false); cameraRoot.tag = "MainCamera";
        var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12; camera.transform.position = new Vector3(0, 3, -10);
        var cm = cameraRoot.AddComponent<CameraManager>(); var data = new SerializedObject(cm);
        foreach (string name in new[] { "cameraEdgeLeft", "cameraEdgeRight" })
        {
            var edge = new GameObject(name); edge.transform.position = Vector3.right * (name.EndsWith("Left") ? -40 : 40);
            var array = data.FindProperty(name); array.arraySize = 1; array.GetArrayElementAtIndex(0).objectReferenceValue = edge.transform;
        }
        data.ApplyModifiedPropertiesWithoutUndo(); cameraRoot.SetActive(true);
        var hero = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));
        var hp = hero.GetComponent<Health>(); hp.SetBaseBlood(2000); hp.Heal(2000);
        var enemy = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/RollingFurnace.prefab"));
        var controller = enemy.GetComponent<RollingFurnaceController>(); var combat = enemy.GetComponent<RollingFurnaceCombat>();
        var movement = enemy.GetComponent<RollingFurnaceMovement>(); var health = enemy.GetComponent<Health>(); var body = enemy.GetComponent<Rigidbody2D>();
        Reset(enemy, hero); yield return new WaitForSeconds(.15f);
        Check(health.BeginBlood == 144 && Near(new SerializedObject(movement).FindProperty("moveSpeed").floatValue, 1), "HP144 / move speed1.0");
        float before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 40, false));
        yield return new WaitForFixedUpdate(); yield return null;
        Check(Near(before - health.CurBlood, 40) && combat.Vent == RollingFurnaceCombat.VentPhase.Closed, "40 actual accumulated damage triggers ShellVent");
        float ventAt = Time.time; before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 2), "closed shell reduces incoming damage80%");
        float x = body.position.x; yield return new WaitForSeconds(1.8f);
        Check(combat.Vent == RollingFurnaceCombat.VentPhase.Closed && Near(body.position.x, x), "closed shell stays immobile and protected for two seconds");
        float deadline = Time.time + .5f;
        while (combat.Vent != RollingFurnaceCombat.VentPhase.Open && Time.time < deadline) yield return null;
        Check(combat.Vent == RollingFurnaceCombat.VentPhase.Open && Near(Time.time - ventAt, 2, .09f) && !health.Boost, "two seconds transitions into the open-seam window without armor");
        before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 15), "open seam increases incoming damage50%");
        float openAt = Time.time; deadline = Time.time + 1.3f;
        while (combat.Vent != RollingFurnaceCombat.VentPhase.None && Time.time < deadline) yield return null;
        Check(combat.Vent == RollingFurnaceCombat.VentPhase.None && Near(Time.time - openAt, 1, .09f), "vulnerability expires after one second");
        before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 10) && !combat.VentReady, "normal damage restored and vent twelve-second cooldown remains active");
        Reset(enemy, hero); yield return new WaitForSeconds(.15f);
        health.TakeDamage(new DamageInfo(hero.transform, 40, false)); yield return new WaitForFixedUpdate(); yield return null;
        Check(combat.Vent == RollingFurnaceCombat.VentPhase.Closed, "fresh pooled life can enter vent again");
        var source = new GameObject("IndependentReduction"); health.SetDamageMultiplier(source, .5f);
        before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 1), "shell multiplier composes with an independent source");
        combat.EndVent(); before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 5), "vent cleanup leaves the other source intact");
        Reset(enemy, hero); before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 10) && !health.Boost, "pool reuse clears every temporary multiplier and armor source");

        Reset(enemy, hero); yield return new WaitForSeconds(.15f); float gravity = body.gravityScale;
        var wall = new GameObject("ValidationWall") { layer = 6 }; wall.transform.position = new Vector3(4f, 2, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 4);
        Check(controller.BeginRoll(1), "roll starts on request"); Check(health.Boost, "roll startup has super armor");
        Place(hero, 1.8f); before = hp.CurBlood; deadline = Time.time + 2f;
        while (controller.Action != RollingFurnaceController.FurnaceAction.WallStun && Time.time < deadline)
        {
            if (movement.Rolling) Check(Near(body.gravityScale, 0) && (Near(Mathf.Abs(body.linearVelocityX), 6) || body.linearVelocityX == 0), "rolling uses zero gravity and constant6u/s");
            yield return new WaitForFixedUpdate();
        }
        Check(controller.Action == RollingFurnaceController.FurnaceAction.WallStun && Near(before - hp.CurBlood, 20), "wall stops first leg and contact damage is exactly20 once");
        float stunAt = Time.time; float wallX = body.position.x; before = health.CurBlood;
        health.TakeDamage(new DamageInfo(hero.transform, 1, false)); Check(!health.Boost && Near(before - health.CurBlood, 1), "wall stun is an unarmored output window");
        Place(hero, wallX - 1.5f); before = hp.CurBlood; yield return new WaitForSeconds(.85f);
        Check(controller.Action == RollingFurnaceController.FurnaceAction.WallStun && Near(wallX, body.position.x), "wall stun holds position for a full second");
        deadline = Time.time + .4f;
        while (controller.Action == RollingFurnaceController.FurnaceAction.WallStun && Time.time < deadline) yield return null;
        Check(Near(Time.time - stunAt, 1, .08f) && movement.Rolling && body.linearVelocityX <= 0, "after one second roll rebounds once");
        deadline = Time.time + 2f;
        while (controller.Action != RollingFurnaceController.FurnaceAction.None && Time.time < deadline) yield return null;
        Check(Near(before - hp.CurBlood, 10) && Near(body.gravityScale, gravity), "rebound hits once for10, then restores gravity");
        Check(!combat.RollReady && Near(movement.Remaining, 0, .03f), "complete roll travels8u total and keeps6s cooldown");

        wall.SetActive(false); Reset(enemy, hero); yield return new WaitForSeconds(.15f);
        camera.orthographicSize = 2; Check(controller.BeginRoll(1), "camera-boundary roll starts"); deadline = Time.time + 2f;
        while (controller.Action != RollingFurnaceController.FurnaceAction.WallStun && Time.time < deadline) yield return null;
        Check(controller.Action == RollingFurnaceController.FurnaceAction.WallStun, "visible camera edge also triggers rebound stun");
        enemy.SetActive(false); Check(Near(body.gravityScale, gravity), "disabling during rolling restores gravity"); camera.orthographicSize = 12;

        Reset(enemy, hero, 2.3f); yield return new WaitForSeconds(.15f);
        // Selection may already have started the slam; both paths exercise the same production state machine.
        if (controller.Action == RollingFurnaceController.FurnaceAction.None) Check(controller.BeginSlam(), "slam starts");
        before = hp.CurBlood; float peak = enemy.transform.position.y; deadline = Time.time + 3f;
        while (controller.Action != RollingFurnaceController.FurnaceAction.SlamRecovery && Time.time < deadline)
        { peak = Mathf.Max(peak, enemy.transform.position.y); yield return null; }
        Check(controller.Action == RollingFurnaceController.FurnaceAction.SlamRecovery && peak > .5f, "slam actually leaves ground and reaches physical landing");
        Check(Near(before - hp.CurBlood, 18), "F12 landing performs one radius2.5 damage sample for18");
        var waves = FindObjectsByType<RollingFurnaceShockwave>(FindObjectsInactive.Include);
        Check(waves.Count(w => w.gameObject.activeSelf) == 1 && waves.Length == 10, "landing emits one shockwave from a ten-item shared pool");
        yield return new WaitForSeconds(.7f); Check(Near(before - hp.CurBlood, 18) && !combat.SlamReady, "lingering shockwave never deals a second hit and8s cooldown remains");
        var pool = enemy.GetComponentInChildren<RollingFurnaceShockwavePool>(); yield return new WaitForSeconds(.4f);
        var first = pool.Emit(Vector2.zero, 5); first.gameObject.SetActive(false); var reused = pool.Emit(Vector2.zero, 5);
        Check(first == reused, "shockwave objects are reused instead of instantiated on each landing");
        Reset(enemy, hero); yield return new WaitForSeconds(.15f); controller.BeginRoll(1);
        deadline = Time.time + 1f;
        while (!movement.Rolling && Time.time < deadline) yield return null;
        var control = new GameObject("ExternalControlSource");
        movement.SetQuicksand(control, .1f, -3f); yield return new WaitForFixedUpdate(); yield return null;
        Check(Near(body.linearVelocityX, 6) && Near(body.gravityScale, 0), "roll keeps constant speed even inside a slowing ground field");
        movement.RemoveQuicksand(control);
        movement.BeginCarry(control, body.position + Vector2.up);
        Check(controller.Action == RollingFurnaceController.FurnaceAction.None && !health.Boost, "strong carry interrupts rolling and removes armor immediately");
        movement.EndCarry(control); yield return null;
        Check(Near(body.gravityScale, gravity), "strong-control release restores pre-roll gravity instead of a saved zero");
        Reset(enemy, hero); yield return new WaitForSeconds(.15f); health.TakeDamage(new DamageInfo(hero.transform, 40, false));
        yield return new WaitForFixedUpdate(); yield return null;
        health.Status.ApplyFreeze(control, .3f); yield return null;
        Check(combat.Vent == RollingFurnaceCombat.VentPhase.None, "strong freeze cancels vent and its reduction source");
        health.Status.CancelFreeze(control);
        Reset(enemy, hero, 4f); yield return new WaitForSeconds(.1f);
        Check(controller.Action == RollingFurnaceController.FurnaceAction.RollWindup, "range3-8 gives roll priority over slam overlap");
        health.TakeDamage(new DamageInfo(hero.transform, 1000, false)); yield return new WaitForFixedUpdate();
        Check(health.Dead && !health.Boost && Near(body.gravityScale, gravity) && enemy.GetComponentsInChildren<RollingFurnaceAttackBox>(true).All(b => !b.gameObject.activeSelf), "death cancels motion, modifiers and every attack box");
        yield return new WaitForSeconds(1.6f); Check(!enemy.activeSelf, "sixteen-frame death finishes and returns the enemy to its pool");
    }
}
#endif

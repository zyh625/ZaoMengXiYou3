#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using zmxy.GamePlay;

public sealed class StarShearGuardRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder report = new();
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string Report => Path.GetFullPath("output/StarShearGuardAudit/runtime.txt");
    private void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); report.AppendLine("PASS: " + message); File.WriteAllText(Report, report.ToString()); }
    private static bool Near(float a, float b, float tolerance = .035f) => Mathf.Abs(a - b) < tolerance;
    private IEnumerator Start()
    {
        var run = Scenario();
        while (true)
        {
            object wait;
            try { if (!run.MoveNext()) break; wait = run.Current; }
            catch (Exception e)
            { report.AppendLine("FAIL: " + e); File.WriteAllText(Report, report.ToString()); Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
            yield return wait;
        }
        report.AppendLine("ALL PASS"); File.WriteAllText(Report, report.ToString());
        Debug.Log("STARSHEARGUARD_RUNTIME_PASS"); EditorApplication.isPlaying = false;
    }
    private static void Place(GameObject go, float x, float floorY = 0)
    {
        var box = go.GetComponent<BoxCollider2D>(); float feet = (box.offset.y - box.size.y / 2) * go.transform.lossyScale.y;
        Vector3 p = new Vector3(x, floorY + .025f - feet, 0); go.transform.position = p;
        var body = go.GetComponent<Rigidbody2D>(); body.position = p; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
    }
    private static void Reset(GameObject enemy, GameObject hero, float x, bool live = false)
    {
        hero.GetComponent<Health>().ClearDebuffs(); hero.GetComponent<Health>().Heal(2000);
        enemy.SetActive(false); enemy.SetActive(true);
        var controller = enemy.GetComponent<StarShearGuardController>(); controller.enabled = live;
        enemy.GetComponent<EnemyMovement>().SetTarget(hero.transform);
        controller.SetSpawnSide(false); Place(enemy, 0); Place(hero, x);
    }
    private static void Force(GameObject enemy, int index, bool live = false, bool right = true)
    {
        var combat = enemy.GetComponent<StarShearGuardCombat>(); combat.ClearAttackRequests(); combat.AttackRequest[index] = true; combat.HasAtatck(right);
        if (live) typeof(EnemyController).GetMethod("SetState", Flags).Invoke(enemy.GetComponent<StarShearGuardController>(), new object[] { EnemyState.Attacking });
        else combat.BeginCast();
    }
    private IEnumerator Scenario()
    {
        var floor = new GameObject("ValidationFloor") { layer = 6 }; floor.transform.position = Vector3.down * .5f;
        floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
        var cameraRoot = new GameObject("ValidationCamera"); cameraRoot.SetActive(false); cameraRoot.tag = "MainCamera";
        var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12; camera.transform.position = new Vector3(0, 3, -10);
        var cm = cameraRoot.AddComponent<CameraManager>(); var cameraData = new SerializedObject(cm);
        foreach (string name in new[] { "cameraEdgeLeft", "cameraEdgeRight" })
        {
            var edge = new GameObject(name); edge.transform.position = Vector3.right * (name.EndsWith("Left") ? -40 : 40);
            var array = cameraData.FindProperty(name); array.arraySize = 1; array.GetArrayElementAtIndex(0).objectReferenceValue = edge.transform;
        }
        cameraData.ApplyModifiedPropertiesWithoutUndo(); cameraRoot.SetActive(true);
        var hero = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));
        var hp = hero.GetComponent<Health>(); hp.SetBaseBlood(2000); hp.Heal(2000);
        var enemy = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/StarShearGuard.prefab"));
        var combat = enemy.GetComponent<StarShearGuardCombat>(); var controller = enemy.GetComponent<StarShearGuardController>();
        var health = enemy.GetComponent<Health>(); var movement = enemy.GetComponent<StarShearGuardMovement>();
        var boxes = enemy.GetComponentsInChildren<StarShearGuardAttackBox>(true).OrderBy(b => new SerializedObject(b).FindProperty("windowIndex").intValue).ToArray();
        movement.SetTarget(hero.transform); controller.SetSpawnSide(false); Place(enemy, 0); Place(hero, 1.2f);
        Check(health.BeginBlood == 170 && new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 24, "HP170 / XP24");
        Check(Near(new SerializedObject(movement).FindProperty("moveSpeed").floatValue, 1), "movement speed1.0");
        Reset(enemy, hero, 1.2f); float before = hp.CurBlood; Force(enemy, 0); combat.SampleFrame(3);
        Check(hp.CurBlood == before, "F4 windup has no hitbox"); combat.SampleFrame(4);
        for (int i = 0; i < 30; i++) { combat.SampleFrame(4); boxes[0].SampleNow(); }
        Check(Near(before - hp.CurBlood, 12), "F5 high cut deals12 exactly once under repeated samples");
        combat.SampleFrame(8); float firstClose = Time.time; combat.SampleFrame(10);
        Check(!combat.SecondOpened && !boxes[0].gameObject.activeSelf && !boxes[1].gameObject.activeSelf, "F9 high closes, premature F11 cannot bypass0.35s gap");
        yield return new WaitForSeconds(.36f);
        Place(hero, 1.2f); combat.SampleFrame(10);
        for (int i = 0; i < 30; i++) { combat.SampleFrame(10); boxes[1].SampleNow(); }
        Check(Near(before - hp.CurBlood, 22) && Time.time - firstClose >= .35f, "F11 low cut independently deals10 once after gap; combined22");
        Check(combat.EffectsEmitted == 1, "offbeat creates exactly one visual for both cuts");
        combat.SampleFrame(14); Check(combat.RecoveryUntil == 0, "successful second cut has no miss recovery");
        float stepStart = enemy.transform.position.x;
        for (int i = 0; i < 10; i++)
        { movement.BeginMotionStep(); movement.TickStep(false); movement.EndMotionStep(); yield return new WaitForFixedUpdate(); }
        Check(Near(enemy.transform.position.x - stepStart, .25f), "second cut advances only a0.25u follow step");

        Reset(enemy, hero, 1.2f); before = hp.CurBlood; Force(enemy, 0); combat.SampleFrame(4); combat.SampleFrame(8);
        Place(hero, -1.2f); yield return new WaitForSeconds(.36f); combat.SampleFrame(10); combat.SampleFrame(14);
        Check(Near(before - hp.CurBlood, 12) && Near(combat.RecoveryUntil - Time.time, .45f, .005f), "first hit plus second miss still imposes full0.45s recovery");
        Reset(enemy, hero, -1.2f); before = hp.CurBlood; Force(enemy, 0); combat.SampleFrame(4); boxes[0].SampleNow(); combat.SampleFrame(8);
        yield return new WaitForSeconds(.36f); combat.SampleFrame(10); boxes[1].SampleNow(); combat.SampleFrame(14);
        Check(hp.CurBlood == before, "going behind evades both independent scissor windows");

        Reset(enemy, hero, 1.1f); Place(hero, 1.1f, 1.55f); before = hp.CurBlood; Force(enemy, 1); combat.SampleFrame(5);
        for (int i = 0; i < 30; i++) { combat.SampleFrame(5); boxes[2].SampleNow(); }
        Check(Near(before - hp.CurBlood, 18), "F6 upward box hits a low-air target above head once for18");
        combat.SampleFrame(10); Check(!boxes[2].gameObject.activeSelf, "F11 upward closes");
        Reset(enemy, hero, -1.1f); Place(hero, -1.1f, 1.55f); before = hp.CurBlood; Force(enemy, 1); combat.SampleFrame(5); boxes[2].SampleNow();
        Check(hp.CurBlood == before, "upward cannot hit behind facing right");
        Reset(enemy, hero, 1.1f); before = hp.CurBlood; Force(enemy, 1, false, false); combat.SampleFrame(5); boxes[2].SampleNow();
        Check(hp.CurBlood == before, "upward cannot hit behind facing left");

        Reset(enemy, hero, 10); Force(enemy, 2); float gravity = enemy.GetComponent<Rigidbody2D>().gravityScale; float startX = enemy.transform.position.x;
        for (int i = 0; i < 22; i++)
        { movement.BeginMotionStep(); movement.TickStep(false); movement.EndMotionStep(); yield return new WaitForFixedUpdate(); }
        movement.Stop();
        Check(Near(startX - enemy.transform.position.x, 2.5f, .06f) && Near(enemy.GetComponent<Rigidbody2D>().gravityScale, gravity), "backstep travels2.5u through shared movement without changing gravity");
        Check(!health.IsInvincible && !health.Boost && controller.CanReceiveHit, "backstep retains hurtbox, no invincibility or armor");
        before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 9, false));
        Check(Near(before - health.CurBlood, 9), "backstep takes full incoming damage without reduction"); health.AttackedEnd();
        Place(hero, enemy.transform.position.x + 1.2f); before = hp.CurBlood; combat.SampleFrame(9);
        for (int i = 0; i < 30; i++) { combat.SampleFrame(9); boxes[3].SampleNow(); }
        Check(Near(before - hp.CurBlood, 12) && combat.EffectsEmitted == 1, "F10 backstep counter-cut deals12 and emits one visual");
        combat.SampleFrame(13); Check(!boxes[3].gameObject.activeSelf, "F14 backstep closes");

        Reset(enemy, hero, 1.2f); Force(enemy, 0); combat.SampleFrame(8); yield return new WaitForSeconds(.36f); combat.SampleFrame(15);
        Check(combat.WindowsClosed && combat.EffectsEmitted == 1, "skipped frames still sample each crossed cut only once");
        Reset(enemy, hero, 20, true); Force(enemy, 0, true);
        float firstEnd = -1, secondBegin = -1, secondEnd = -1; float deadline = Time.time + 4f;
        while (controller.IsCasting && Time.time < deadline)
        {
            if (combat.FirstClosed && firstEnd < 0) firstEnd = Time.time;
            if (combat.SecondOpened && secondBegin < 0) secondBegin = Time.time;
            if (combat.WindowsClosed && secondEnd < 0) secondEnd = Time.time;
            yield return null;
        }
        Check(!controller.IsCasting && secondBegin - firstEnd >= .33f && secondBegin - firstEnd < .45f, "live16fps animation has0.35s empty interval within frame sampling tolerance");
        Check(secondEnd > 0 && Time.time - secondEnd >= .43f, "live second miss blocks new actions for0.45s");

        Reset(enemy, hero, 10, true); Force(enemy, 2, true); yield return new WaitForFixedUpdate();
        before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 7, false)); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Check(Near(before - health.CurBlood, 7) && !controller.IsCasting && !movement.Stepping && boxes.All(b => !b.gameObject.activeSelf), "ordinary hit interrupts backstep via base hit reaction and closes every hitbox");
        Reset(enemy, hero, 1.2f, true); Force(enemy, 0, true); health.Status.ApplyFreeze(this, 1);
        Check(!controller.IsCasting && boxes.All(b => !b.gameObject.activeSelf), "freeze immediately cancels cast through base status hook"); health.ClearDebuffs();

        Reset(enemy, hero, 2.1f); combat.AttackRequest[0] = combat.AttackRequest[1] = combat.AttackRequest[2] = true;
        Check(combat.SelectRequest(2.1f) && combat.AttackRequest[0], "2.1u prioritizes offbeat");
        combat.AttackRequest[0] = combat.AttackRequest[1] = combat.AttackRequest[2] = true;
        Check(combat.SelectRequest(4) && combat.AttackRequest[1], "4u selects upward");
        combat.AttackRequest[0] = combat.AttackRequest[1] = combat.AttackRequest[2] = true;
        Check(combat.SelectRequest(.5f) && combat.AttackRequest[2], "point blank prioritizes retreat");
        health.TakeDamage(new DamageInfo(hero.transform, 120, false)); health.AttackedEnd();
        combat.AttackRequest[0] = combat.AttackRequest[1] = combat.AttackRequest[2] = true;
        Check(combat.SelectRequest(4) && combat.AttackRequest[2], "low health prioritizes retreat");
        Reset(enemy, hero, 1.2f); combat.CheckAttack(); Check(!combat.AttackReady(), "pool reactivation restores initial cooldowns");
        Force(enemy, 1); combat.SampleFrame(5);
        yield return new WaitForSeconds(1.05f);
        var upperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/StarShearGuardUpwardScissorFX.prefab").GetComponent<IndependentSkillEffect>();
        var otherRenter = StarShearGuardEffectPool.Emit(1, upperPrefab, hero.transform, Vector3.zero, true);
        combat.CancelCast();
        Check(otherRenter.IsPlaying, "completed visual can be rented by another caster without old caster canceling it"); otherRenter.StopEffect();
        for (int i = 0; i < 3; i++)
        {
            string action = new[] { "OffbeatScissor", "UpwardScissor", "BackstepCut" }[i];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/StarShearGuard" + action + "FX.prefab").GetComponent<IndependentSkillEffect>();
            var fx = StarShearGuardEffectPool.Emit(i, prefab, enemy.transform, Vector3.zero, true);
            yield return new WaitForSeconds(1.05f);
            Check(!fx.gameObject.activeSelf, action + " returns after16-frame lifecycle");
            var reused = StarShearGuardEffectPool.Emit(i, prefab, enemy.transform, Vector3.zero, false);
            Check(reused == fx && !reused.GetComponent<SpriteRenderer>().flipX, action + " uses its own cache and resets direction");
            reused.StopEffect();
        }
        Reset(enemy, hero, 1.2f, true); Force(enemy, 0, true);
        health.TakeDamage(new DamageInfo(hero.transform, 10000, false));
        Check(!controller.IsCasting && boxes.All(b => !b.gameObject.activeSelf), "death cancels all attack windows immediately");
        enemy.SetActive(false); enemy.SetActive(true); movement.SetTarget(hero.transform); controller.enabled = false;
        Check(!controller.IsCasting && !health.IsInvincible && !health.Boost && controller.CanReceiveHit && !movement.Stepping, "pooled next life has clean cast/motion/defense state");
        enemy.SetActive(false); hero.SetActive(false);
    }
}
#endif

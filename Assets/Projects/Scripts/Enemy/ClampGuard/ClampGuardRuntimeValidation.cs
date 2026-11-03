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

public sealed class ClampGuardRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder report = new();
    private static string Report => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/ClampGuardAudit/runtime.txt"));
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); report.AppendLine("PASS: " + message); File.WriteAllText(Report, report.ToString()); }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < .035f;
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
        Debug.Log("CLAMPGUARD_RUNTIME_PASS"); EditorApplication.isPlaying = false;
    }
    private static void Place(GameObject go, float x, float y = 0)
    {
        var box = go.GetComponent<BoxCollider2D>(); float feet = (box.offset.y - box.size.y / 2) * go.transform.lossyScale.y;
        var p = new Vector3(x, y + .025f - feet, 0); go.transform.position = p;
        var body = go.GetComponent<Rigidbody2D>(); body.position = p; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
    }
    private static void Reset(GameObject enemy, GameObject hero, float x)
    {
        hero.GetComponent<Health>().ClearDebuffs(); hero.GetComponent<Health>().Heal(2000);
        enemy.SetActive(false); enemy.SetActive(true); enemy.GetComponent<EnemyMovement>().SetTarget(hero.transform);
        enemy.GetComponent<EnemyController>().SetSpawnSide(false); Place(enemy, 0); Place(hero, x);
    }
    private static void Force(GameObject enemy, int index)
    {
        var combat = enemy.GetComponent<ClampGuardCombat>(); combat.ClearAttackRequests(); combat.AttackRequest[index] = true; combat.HasAtatck(true);
        typeof(EnemyController).GetMethod("SetState", Flags).Invoke(enemy.GetComponent<ClampGuardController>(), new object[] { EnemyState.Attacking });
        enemy.GetComponentInChildren<SpriteManager>().Pause();
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
        var hp = hero.GetComponent<Health>(); hp.SetBaseBlood(2000); hp.Heal(2000); Place(hero, 2);
        var enemy = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/ClampGuard.prefab"));
        var combat = enemy.GetComponent<ClampGuardCombat>(); var controller = enemy.GetComponent<ClampGuardController>();
        var health = enemy.GetComponent<Health>(); var movement = enemy.GetComponent<ClampGuardMovement>();
        movement.SetTarget(hero.transform); controller.SetSpawnSide(false); Place(enemy, 0);
        var boxes = enemy.GetComponentsInChildren<ClampGuardAttackBox>(true).OrderBy(b => b.name).ToArray();
        Check(health.BeginBlood == 138 && new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 22, "HP138 / XP22");
        Check(Near(new SerializedObject(movement).FindProperty("moveSpeed").floatValue, .9f), "movement speed0.9");
        Check(ClampGuardCombat.EligibleMask(2.5f, 7) == 5 && ClampGuardCombat.EligibleMask(2.7f, 7) == 4
            && ClampGuardCombat.EligibleMask(3f, 7) == 6 && ClampGuardCombat.EligibleMask(5.1f, 7) == 0, "distance eligibility boundaries");
        health.SetBaseBlood(2000); health.Heal(2000);
        float before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 8), "front hit reduces damage20%");
        Place(hero, -2); before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 12.5f), "rear hit increases damage25%");
        var other = new GameObject("IndependentArmorSource"); health.SetDamageMultiplier(other, .5f);
        Place(hero, 2); before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 4), "front armor multiplies another source without replacing it");
        Place(hero, -2); before = health.CurBlood; health.TakeDamage(new DamageInfo(hero.transform, 10, false));
        Check(Near(before - health.CurBlood, 6.25f), "rear vulnerability does not retain front armor and preserves other source");
        before = health.CurBlood; health.TakeDamage(new DamageInfo(null, 10, false, false, null, false, true));
        Check(Near(before - health.CurBlood, 5), "periodic/source-less damage does not inherit directional modifier");
        health.RemoveDamageMultiplier(other);

        Reset(enemy, hero, 1.6f); before = hp.CurBlood; Force(enemy, 0); combat.SampleFrame(4);
        for (int i = 0; i < 50; i++) { combat.SampleFrame(4); boxes[0].SampleNow(); }
        Check(Near(before - hp.CurBlood, 16) && hero.GetComponent<ActorStun>().IsStunned, "clamp hits once for16 and applies ordinary1.2s stun");
        yield return new WaitForSeconds(1.25f);
        Check(!hero.GetComponent<ActorStun>().IsStunned, "ordinary stun expires after1.2s");
        Reset(enemy, hero, 1.6f); hp.SetSuperArmor(this, true); before = hp.CurBlood; Force(enemy, 0); combat.SampleFrame(4);
        Check(Near(before - hp.CurBlood, 16) && !hero.GetComponent<ActorStun>().IsStunned, "super armor rejects ordinary clamp control while taking damage");

        Reset(enemy, hero, 4f); hp.SetSuperArmor(this, true); before = hp.CurBlood; float gravity = hero.GetComponent<Rigidbody2D>().gravityScale;
        Force(enemy, 1); combat.SampleFrame(5); var grab = hero.GetComponent<ClampGuardGrab>();
        Check(combat.HasTether && grab.IsHolding(combat), "4.5u grab accepts ordinary armored player as strong control");
        for (int i = 0; i < 50; i++) boxes[1].SampleNow();
        yield return new WaitForSeconds(.55f);
        Check(Near(before - hp.CurBlood, 3) && grab.TicksApplied == 1, "grab first tick only at0.5s");
        yield return new WaitForSeconds(1.04f);
        Check(Near(before - hp.CurBlood, 9) && grab.TicksApplied == 3 && grab.ThrowsApplied == 1 && !combat.HasTether
            && hero.transform.position.x < enemy.transform.position.x && Near(hero.GetComponent<Rigidbody2D>().gravityScale, gravity), "three grab ticks over1.5s, one rear throw and restored gravity");
        hp.SetSuperArmor(this, false);

        Reset(enemy, hero, 4); Force(enemy, 1); combat.SampleFrame(5); before = hp.CurBlood;
        health.Status.ApplyFreeze(this, 2);
        Check(!combat.HasTether && !controller.IsCasting && !grab.IsHolding(combat), "strong freeze immediately interrupts caster and releases victim");
        yield return new WaitForSeconds(.55f); Check(hp.CurBlood == before, "interrupted grab cannot leave a late damage tick");
        health.ClearDebuffs();
        Reset(enemy, hero, 4); Force(enemy, 1); combat.SampleFrame(5);
        typeof(PlayerController).GetField("state", Flags).SetValue(hero.GetComponent<PlayerController>(), PlayerState.Skill);
        hero.GetComponent<WukongMovement>().CheckSkillMove(1, 0);
        typeof(ClampGuardGrab).GetMethod("LateUpdate", Flags).Invoke(grab, null);
        Check(!combat.HasTether && hero.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude > .25f, "actual Wukong dash velocity breaks lock without erasing displacement");
        typeof(PlayerController).GetField("state", Flags).SetValue(hero.GetComponent<PlayerController>(), PlayerState.Idle);
        Reset(enemy, hero, 4); Force(enemy, 1); combat.SampleFrame(5); hp.ClearDebuffs();
        Check(!combat.HasTether && !grab.IsHolding(combat), "dispel clears only tether movement source");
        Reset(enemy, hero, 4); Force(enemy, 1); combat.SampleFrame(5); int life = health.LifeVersion;
        enemy.SetActive(false); enemy.SetActive(true); movement.SetTarget(hero.transform);
        Check(!grab.IsHolding(combat) && health.LifeVersion != life && !controller.IsCasting, "pool reactivation cannot retain old grab or cast");
        var nonPlayer = new GameObject("NonPlayerBossProbe"); nonPlayer.AddComponent<Rigidbody2D>(); nonPlayer.AddComponent<BoxCollider2D>(); nonPlayer.AddComponent<Health>();
        Check(!nonPlayer.AddComponent<ClampGuardGrab>().Begin(combat), "non-player enemies and bosses are not lock targets");
        nonPlayer.SetActive(false);

        Reset(enemy, hero, 2); before = hp.CurBlood; Force(enemy, 2); combat.SampleFrame(7);
        for (int i = 0; i < 50; i++) { combat.SampleFrame(7); boxes[2].SampleNow(); }
        Check(Near(before - hp.CurBlood, 12) && combat.FlamesEmitted == 1, "flame F8 samples once for12 and emits exactly one visual");
        var pool = FindAnyObjectByType<ClampGuardVentFlamePool>();
        var effects = pool.GetComponentsInChildren<IndependentSkillEffect>(true);
        Check(effects.Length == 1 && effects[0].IsPlaying, "independent visual is rented from pool");
        yield return new WaitForSeconds(1.05f); Check(Near(before - hp.CurBlood, 15), "burn first second adds exactly3");
        Check(!effects[0].gameObject.activeSelf, "flame visual auto-returns after its16-frame lifecycle");
        yield return new WaitForSeconds(1.05f); Check(Near(before - hp.CurBlood, 18), "burn two seconds totals6 with no duplicate application");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/ClampGuardVentFlame.prefab").GetComponent<IndependentSkillEffect>();
        var reused = ClampGuardVentFlamePool.Emit(prefab, Vector3.zero, false);
        Check(reused == effects[0] && !reused.GetComponent<SpriteRenderer>().flipX, "player/enemy visual API reuses same instance and resets direction"); pool.ReleaseAll();

        Reset(enemy, hero, 20); Force(enemy, 2);
        var corner = new GameObject("ConeCornerProbe") { layer = 3 }; corner.transform.SetParent(hero.transform, false);
        var cornerBox = corner.AddComponent<BoxCollider2D>(); cornerBox.isTrigger = true; cornerBox.size = Vector2.one * .1f;
        corner.transform.position = new Vector3(.7f, 2.1f, 0); Physics2D.SyncTransforms(); before = hp.CurBlood; combat.SampleFrame(7);
        Check(hp.CurBlood == before, "cone rejects targets in rectangular broad-phase corners"); Destroy(corner); pool.ReleaseAll();

        Reset(enemy, hero, 3.5f); controller.enabled = false; yield return new WaitForSeconds(.08f);
        var last = (float[])typeof(EnemyCombat).GetField("lastAttack", Flags).GetValue(combat);
        for (int i = 0; i < 3; i++) last[i] = Time.time - 10;
        combat.CheckAttack(); Check(combat.AttackRequest.All(v => v), "all independent cooldowns can become ready");
        combat.ClearAttackRequests(); combat.AttackRequest[1] = true; combat.HasAtatck(true); combat.AttackEnd(); combat.AttackOver(); combat.CheckAttack();
        Check(combat.AttackRequest[0] && !combat.AttackRequest[1] && combat.AttackRequest[2], "using grab consumes only its9s cooldown");
        var delays = (float[])typeof(EnemyCombat).GetField("attackDelay", Flags).GetValue(combat);
        Check(Near(delays[0], 3.5f) && Near(delays[1], 9) && Near(delays[2], 7), "exact cooldowns3.5/9/7 seconds");
        controller.enabled = true;
        Reset(enemy, hero, 4); before = hp.CurBlood; Force(enemy, 1); enemy.GetComponentInChildren<SpriteManager>().Resume();
        yield return new WaitForSeconds(1.15f);
        Check(controller.IsCasting && combat.HasTether && enemy.GetComponentInChildren<SpriteManager>().CurFrame == 9,
            "natural animation holds F10 while1.5s tether remains active: casting=" + controller.IsCasting
            + " tether=" + combat.HasTether + " frame=" + enemy.GetComponentInChildren<SpriteManager>().CurFrame
            + " ticks=" + grab.TicksApplied + " hero=" + hero.transform.position + " source=" + enemy.transform.position
            + " playerState=" + hero.GetComponent<PlayerController>().State() + " external=" + movement.IsExternallyControlled);
        yield return new WaitForSeconds(1.75f);
        Check(!controller.IsCasting && !combat.HasTether && grab.ThrowsApplied == 1 && Near(before - hp.CurBlood, 9),
            "natural grab resumes recovery and returns to base state machine");
        Reset(enemy, hero, 2); before = hp.CurBlood; Force(enemy, 2); enemy.GetComponentInChildren<SpriteManager>().Resume();
        yield return new WaitForSeconds(.82f);
        Check(combat.FlamesEmitted == 1 && Near(before - hp.CurBlood, 12), "natural AttackThree fires onF8");
        yield return new WaitForSeconds(2.2f);
        Check(!controller.IsCasting && Near(before - hp.CurBlood, 18), "natural flame animation finishes with exactly12+6 damage");
        Reset(enemy, hero, 2); before = hp.CurBlood; Force(enemy, 2); combat.SampleFrame(16); combat.SampleFrame(20);
        Check(combat.FlamesEmitted == 1 && combat.WindowClosed && Near(before - hp.CurBlood, 12),
            "skipping whole frame window catches up exactly one flame sample");
        pool.ReleaseAll();
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class ConstellationWeaverRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder report = new();
    private void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); report.AppendLine("PASS: " + label); }
    private static T Field<T>(object source, string name)
    {
        for (Type type = source.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return (T)field.GetValue(source);
        }
        throw new MissingFieldException(name);
    }
    private static void Place(GameObject go, float x)
    {
        go.transform.position = new Vector3(x, 0.02f); var body = go.GetComponent<Rigidbody2D>();
        body.position = go.transform.position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
    }
    private IEnumerator Start()
    {
        var run = Scenario();
        while (true)
        {
            object wait;
            try { if (!run.MoveNext()) break; wait = run.Current; }
            catch (Exception ex) { report.AppendLine("FAIL: " + ex); Finish(); yield break; }
            yield return wait;
        }
        report.AppendLine("ALL PASS"); Finish();
    }
    private void Finish()
    {
        string audit = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/ConstellationWeaverAudit"));
        Directory.CreateDirectory(audit);
        File.WriteAllText(Path.Combine(audit,"runtime-validation.txt"), report.ToString());
        Debug.Log(report.ToString()); EditorApplication.isPlaying = false;
    }
    private IEnumerator Scenario()
    {
        var floor = new GameObject("WeaverValidationFloor") { layer = 6 };
        floor.transform.position = new Vector3(0, -0.5f); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
        var cameraRoot = new GameObject("WeaverValidationCamera"); cameraRoot.SetActive(false); cameraRoot.tag = "MainCamera";
        var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10;
        camera.transform.position = new Vector3(0, 2, -10);
        var cm = cameraRoot.AddComponent<zmxy.GamePlay.CameraManager>();
        var left = new GameObject("Left"); left.transform.position = Vector3.left * 40;
        var right = new GameObject("Right"); right.transform.position = Vector3.right * 40;
        var serializedCamera = new SerializedObject(cm);
        foreach (string key in new[] { "cameraEdgeLeft", "cameraEdgeRight" })
        {
            var edge = serializedCamera.FindProperty(key); edge.arraySize = 1;
            edge.GetArrayElementAtIndex(0).objectReferenceValue = key == "cameraEdgeLeft" ? left.transform : right.transform;
        }
        serializedCamera.ApplyModifiedPropertiesWithoutUndo(); cameraRoot.SetActive(true); cm.enabled = false;
        var player = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));
        var hero = player.GetComponent<PlayerController>();
        var hp = player.GetComponent<Health>(); hp.SetBaseBlood(1000f); hp.Heal(1000f);
        var enemy = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/ConstellationWeaver.prefab"));
        enemy.GetComponent<ConstellationWeaverController>().enabled = false;
        var combat = enemy.GetComponent<ConstellationWeaverCombat>(); var movement = enemy.GetComponent<ConstellationWeaverMovement>();
        Action reset = () => { enemy.SetActive(false); enemy.SetActive(true); combat.BindTarget(hero); movement.SetTarget(hero.transform); Place(enemy, 0); Place(player, 4); };
        reset(); yield return new WaitForFixedUpdate();
        Check(combat.BeginCast(0), "TwinAnchor starts"); combat.SampleFrame(3); Check(!combat.HasSilk, "no anchors before frame 5");
        combat.SampleFrame(4); Check(combat.HasSilk, "frame 5 releases two anchors and one silk"); combat.CancelCast();
        var anchors = Field<StarAnchor[]>(combat, "anchors");
        Check(anchors[0].GetComponent<Health>().CurBlood == 30 && anchors[1].GetComponent<Health>().CurBlood == 30, "anchors have 30 HP");
        var rival = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/ConstellationWeaver.prefab"));
        rival.GetComponent<ConstellationWeaverController>().enabled = false;
        rival.GetComponent<ConstellationWeaverCombat>().BindTarget(hero);
        Check(!rival.GetComponent<ConstellationWeaverCombat>().BeginCast(0), "second spider cannot create a second on-screen silk");
        Destroy(rival);
        Place(player, 3); float before = hp.CurBlood; yield return new WaitForSeconds(0.12f);
        Check(Mathf.Abs(before - hp.CurBlood - 8) < 0.01f, "silk deals exactly 8 once with multiple colliders");
        before = hp.CurBlood;
        Place(player, 8); yield return new WaitForSeconds(0.04f); Place(player, 3); yield return new WaitForSeconds(0.04f);
        Check(hp.CurBlood == before, "reentry within 0.5 seconds is suppressed");
        yield return new WaitForSeconds(0.52f); Check(hp.CurBlood == before, "continuous contact does not retrigger");
        var multipliers = Field<System.Collections.Generic.Dictionary<UnityEngine.Object, float>>(player.GetComponent<PlayerMovement>(), "moveMultipliers");
        Check(multipliers.ContainsValue(0.6f), "40 percent slow applied");
        Place(player, 8); yield return new WaitForSeconds(0.5f); Check(!multipliers.ContainsValue(0.6f), "silk slow expires after one second");
        Place(player, 3); before = hp.CurBlood; yield return new WaitForSeconds(0.06f);
        Check(Mathf.Abs(before - hp.CurBlood - 8) < 0.01f, "reentry after cooldown deals one hit");
        var attack = new GameObject("WeaverValidationPlayerAttack") { layer = 8 }; attack.SetActive(false);
        attack.transform.position = anchors[0].transform.position;
        var attackShape = attack.AddComponent<BoxCollider2D>(); attackShape.isTrigger = true; attackShape.size = Vector2.one * 0.3f;
        var attackBox = attack.AddComponent<PlayerAttackBox>();
        var serializedAttack = new SerializedObject(attackBox); serializedAttack.FindProperty("Attack").floatValue = 30f;
        serializedAttack.ApplyModifiedPropertiesWithoutUndo(); attack.SetActive(true);
        yield return new WaitForSeconds(0.04f); Destroy(attack);
        Check(!combat.HasSilk && !anchors[1].gameObject.activeSelf && movement.IsStunned, "anchor death immediately removes whole silk and stuns owner");
        Check(!combat.BeginCast(2), "reposition unavailable after anchor destroyed");
        yield return new WaitForSeconds(0.7f); Check(movement.IsStunned, "break stun still active at 0.7 seconds");
        yield return new WaitForSeconds(0.15f); Check(!movement.IsStunned, "break stun ends at 0.8 seconds");
        reset(); yield return new WaitForFixedUpdate();
        Check(combat.BeginCast(1), "ThreeStarBind starts"); var nodes = Field<Vector2[]>(combat, "nodes"); Vector2 locked = nodes[1];
        Place(player, 10); Check(nodes[1] == locked, "nodes remain locked when target moves"); Place(player, nodes[0].x);
        before = hp.CurBlood; combat.SampleFrame(5); Check(Mathf.Abs(before - hp.CurBlood - 6) < 0.01f, "first knot deals exactly 6");
        before = hp.CurBlood; combat.SampleFrame(5); Check(hp.CurBlood == before, "same knot never pulses twice");
        combat.SampleFrame(7); Check(combat.HoldingFrame, "second node waits for 0.6 second interval");
        yield return new WaitForSeconds(0.61f); combat.SampleFrame(7);
        yield return new WaitForSeconds(0.61f); combat.SampleFrame(10); combat.CancelCast();
        yield return new WaitForSeconds(0.35f); Check(!multipliers.ContainsValue(0.6f), "knot slow expires after 1.5 seconds");
        reset(); yield return new WaitForFixedUpdate(); combat.BeginCast(0); combat.SampleFrame(4); combat.CancelCast();
        Check(combat.BeginCast(2), "reposition requires intact silk"); combat.SampleFrame(4);
        float startX = enemy.transform.position.x;
        for (int i = 0; i < 7; i++) { combat.MotionStep(); yield return new WaitForFixedUpdate(); }
        Check(enemy.transform.position.x > startX && enemy.transform.position.x < 5.4f, "reposition moves visibly over multiple physics steps");
        enemy.GetComponent<Health>().TakeDamage(new DamageInfo(player.transform, 1, false)); combat.SampleFrame(7);
        Check(combat.CurrentSkill == -1, "taking damage interrupts reposition");
        reset(); yield return new WaitForFixedUpdate(); combat.BeginCast(0); combat.SampleFrame(4); combat.CancelCast(); combat.BeginCast(2); combat.SampleFrame(4);
        for (int i = 0; i < 24; i++) { combat.MotionStep(); yield return new WaitForFixedUpdate(); }
        Check(movement.IsStunned, "completed reposition has one second recovery");
        Check(Mathf.Abs(enemy.transform.position.x - 5.4f) < 0.05f, "reposition reaches the opposite anchor");
        yield return new WaitForSeconds(1.05f); Check(!movement.IsStunned, "reposition recovery expires");
        reset(); yield return new WaitForFixedUpdate(); combat.BeginCast(0); combat.SampleFrame(4); combat.CancelCast();
        yield return new WaitForSeconds(10.1f); Check(!combat.HasSilk, "silk expires after ten seconds");
        reset(); yield return new WaitForFixedUpdate();
        var controller = enemy.GetComponent<ConstellationWeaverController>(); controller.enabled = true;
        yield return new WaitForSeconds(0.65f);
        Check(combat.HasSilk, "real controller and animation release TwinAnchor without manual frame sampling");
        enemy.GetComponent<Health>().TakeDamage(new DamageInfo(player.transform, 1, false));
        yield return new WaitForSeconds(0.06f);
        Check(combat.CurrentSkill == -1, "real controller interrupts casting on damage");
        Destroy(enemy); Destroy(player); Destroy(floor);
    }
}
#endif

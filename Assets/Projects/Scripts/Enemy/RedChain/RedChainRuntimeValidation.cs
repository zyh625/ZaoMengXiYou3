#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using zmxy.GamePlay;
using Object = UnityEngine.Object;

//仅编辑器验收探针，不编译到游戏；所有测试对象都只存在于 Play Mode。
public sealed class RedChainRuntimeValidation : MonoBehaviour
{
    private const string ResultPath = "Temp/RedChainAudit/runtime-validation.txt";
    private readonly System.Text.StringBuilder results = new();
    private IEnumerator Start()
    {
        IEnumerator scenario = Scenario();
        while (true)
        {
            object instruction;
            try { if (!scenario.MoveNext()) break; instruction = scenario.Current; }
            catch (Exception exception)
            {
                results.AppendLine("FAIL: " + exception);
                File.WriteAllText(ResultPath, results.ToString());
                Debug.LogError("REDCHAIN_RUNTIME_FAIL: " + exception);
                EditorApplication.isPlaying = false;
                yield break;
            }
            yield return instruction;
        }
        results.AppendLine("ALL PASS");
        File.WriteAllText(ResultPath, results.ToString());
        Debug.Log("REDCHAIN_RUNTIME_PASS");
        EditorApplication.isPlaying = false;
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.AppendLine("PASS: " + message);
    }

    private static void Property(Object component, string name, Action<SerializedProperty> write)
    {
        SerializedObject so = new(component);
        write(so.FindProperty(name));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Place(GameObject actor, float x)
    {
        BoxCollider2D body = actor.GetComponent<BoxCollider2D>();
        float feet = (body.offset.y - body.size.y / 2f) * actor.transform.lossyScale.y;
        actor.transform.position = new Vector3(x, 0.025f - feet, 0f);
        actor.GetComponent<Rigidbody2D>().position = actor.transform.position;
        actor.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static void Cast(RedChainController controller, int attack, bool right = true)
    {
        RedChainCombat combat = controller.GetComponent<RedChainCombat>();
        combat.ClearAttackRequests();
        combat.AttackRequest[attack] = true;
        combat.HasAtatck(right);
        typeof(EnemyController).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(controller, new object[] { EnemyState.Attacking });
    }

    private IEnumerator Scenario()
    {
        //先让真实第五关执行 Start，验证玩家、镜头和十个小怪池都能初始化。
        yield return new WaitForSeconds(0.3f);
        RedChainStageManager stage = FindAnyObjectByType<RedChainStageManager>();
        Check(stage != null && stage.IsInitialized, "Level5 real scene initializes");
        Check(FindObjectsByType<RedChainController>(FindObjectsInactive.Include).Length == 10, "Level5 prewarms 10 RedChain enemies");
        foreach (GameObject root in gameObject.scene.GetRootGameObjects()) if (root != gameObject) root.SetActive(false);

        GameObject floor = new("ValidationFloor") { layer = 6 };
        BoxCollider2D floorBox = floor.AddComponent<BoxCollider2D>();
        floorBox.size = new Vector2(100f, 1f);
        floor.transform.position = new Vector3(0f, -0.5f);
        GameObject cameraRoot = new("ValidationCamera");
        cameraRoot.SetActive(false);
        cameraRoot.tag = "MainCamera";
        Camera camera = cameraRoot.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 12f;
        camera.transform.position = new Vector3(0f, 2f, -10f);
        CameraManager cm = cameraRoot.AddComponent<CameraManager>();
        GameObject left = new("ValidationLeft"), right = new("ValidationRight");
        left.transform.position = new Vector3(-40f,0f);
        right.transform.position = new Vector3(40f,0f);
        Property(cm, "cameraEdgeLeft", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = left.transform; });
        Property(cm, "cameraEdgeRight", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = right.transform; });
        cameraRoot.SetActive(true);
        GameObject player = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));
        Place(player,4f);
        Health playerHealth = player.GetComponent<Health>();
        PlayerMovement input = player.GetComponent<PlayerMovement>();
        GameObject enemy = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/RedChain.prefab"));
        RedChainController controller = enemy.GetComponent<RedChainController>();
        RedChainCombat combat = enemy.GetComponent<RedChainCombat>();
        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        Health health = enemy.GetComponent<Health>();
        Rigidbody2D enemyBody = enemy.GetComponent<Rigidbody2D>();
        movement.SetTarget(player.transform);
        Place(enemy,0f);
        foreach (EnemyCheckAttack detector in enemy.GetComponentsInChildren<EnemyCheckAttack>()) detector.GetComponent<Collider2D>().enabled = false;
        yield return new WaitForSeconds(0.1f);

        Check(health.BeginBlood == 135f, "HP exactly 135");
        combat.AttackRequest[0] = combat.AttackRequest[1] = combat.AttackRequest[2] = true;
        Check(combat.SelectRequest(7f) && combat.AttackRequest[1] && !combat.AttackRequest[0] && !combat.AttackRequest[2], "distance >6 prioritizes chain");
        bool sweepChosen = false, groundChosen = false;
        for (int i=0;i<64;i++)
        {
            combat.AttackRequest[0] = combat.AttackRequest[1] = combat.AttackRequest[2] = true;
            combat.SelectRequest(4f);
            sweepChosen |= combat.AttackRequest[0]; groundChosen |= combat.AttackRequest[2];
            if (combat.AttackRequest[1]) throw new InvalidOperationException("Mid range selected chain");
        }
        Check(sweepChosen && groundChosen, "mid range chooses both sweep and slam");
        combat.ClearAttackRequests();
        Check(!combat.SelectRequest(7f), "cooling down does not start a move");

        float playerBefore = playerHealth.CurBlood;
        Cast(controller,0);
        Check(!combat.AttackOpen(5) && combat.AttackOpen(6) && combat.AttackShut(9), "sweep frame window is 6 through 8");
        Check(health.Boost && controller.IsCasting, "armor starts at windup");
        float enemyBefore = health.CurBlood;
        health.TakeDamage(new DamageInfo(player.transform,1f));
        movement.Stun(player,1f);
        Check(health.CurBlood == enemyBefore-1f && controller.IsCasting && !movement.IsStunned, "armor allows damage and rejects ordinary stun");
        bool opened=false;
        float deadline=Time.time+3f;
        while(controller.IsCasting && Time.time<deadline)
        {
            opened |= enemy.transform.Find("Combat/AttackOne").gameObject.activeSelf;
            yield return null;
        }
        Check(opened && !enemy.transform.Find("Combat/AttackOne").gameObject.activeSelf, "sweep opens and closes by animation frame");
        Check(Mathf.Approximately(playerBefore-playerHealth.CurBlood,17f), "sweep deals exactly 17 once");
        Check(!health.Boost && controller.IsRecovering, "armor ends immediately and recovery starts");
        health.TakeDamage(new DamageInfo(player.transform,1f,false));
        yield return new WaitForFixedUpdate();
        Check(!health.Boost && controller.GetComponentInChildren<SpriteManager>().CurFrame < 16, "normal hurt reaction remains available in recovery");
        yield return new WaitForSeconds(0.9f);
        Check(!controller.IsCasting && Mathf.Abs(enemyBody.linearVelocityX)<0.01f && controller.IsRecovering, "cannot move or restart during 1.2 second recovery");
        yield return new WaitForSeconds(0.35f);
        Check(!controller.IsRecovering, "recovery expires after 1.2 seconds");

        Place(enemy,0f); Place(player,7f);
        input.SyncMoveInput(1f);
        playerBefore=playerHealth.CurBlood;
        Cast(controller,1);
        combat.SampleFrame(8);
        Check(enemy.transform.Find("Combat/AttackTwo").gameObject.activeSelf, "chain box opens at frame 8");
        combat.SampleFrame(10);
        Check(!enemy.transform.Find("Combat/AttackTwo").gameObject.activeSelf, "chain box closes at frame 10");
        RedChainPull pull=player.GetComponent<RedChainPull>();
        Check(pull!=null && pull.IsPulling && input.WantsMove, "chain starts forced motion without clearing move input");
        Check(Mathf.Approximately(playerBefore-playerHealth.CurBlood,14f), "chain deals exactly 14 once");
        yield return new WaitForSeconds(0.37f);
        float front=enemy.GetComponent<Collider2D>().bounds.max.x+player.GetComponent<Collider2D>().bounds.extents.x+0.1f;
        Check(!pull.IsPulling && Mathf.Abs(player.transform.position.x-front)<0.4f, "0.35 second pull ends in front of enemy");
        Check(input.WantsMove, "player movement input survives the entire pull");
        input.SyncMoveInput(0f);

        enemy.SetActive(false); enemy.SetActive(true);
        movement.SetTarget(player.transform);
        Place(enemy,0f);Place(player,7f);
        GameObject wall=new("ValidationWall") { layer=6 };
        wall.transform.position=new Vector3(3.5f,2f);
        BoxCollider2D wallBox=wall.AddComponent<BoxCollider2D>();wallBox.size=new Vector2(0.6f,4f);
        GameObject activeAttack=new("ValidationActivePlayerAttack") { layer=8 };
        activeAttack.transform.SetParent(player.transform,false);
        BoxCollider2D playerAttack=activeAttack.AddComponent<BoxCollider2D>();
        playerAttack.isTrigger=true;playerAttack.size=new Vector2(5f,3f);playerAttack.offset=new Vector2(-3f,0f);
        Physics2D.SyncTransforms();
        Cast(controller,1);combat.SampleFrame(8);
        yield return new WaitForSeconds(0.4f);
        Check(player.GetComponent<Collider2D>().bounds.min.x>=wallBox.bounds.max.x-0.01f, "pull cannot enter or cross terrain");
        Check(player.transform.position.x<7f, "terrain sweep permits travel even while a player attack trigger overlaps the wall");
        wall.SetActive(false);
        activeAttack.SetActive(false);

        enemy.SetActive(false); enemy.SetActive(true); movement.SetTarget(player.transform);
        Place(enemy,0f); Place(player,-7f);
        playerBefore=playerHealth.CurBlood;
        Cast(controller,1,false); combat.SampleFrame(8); combat.SampleFrame(10);
        Check(pull.IsPulling && Mathf.Approximately(playerBefore-playerHealth.CurBlood,14f), "left chain hits and starts its pull");
        yield return new WaitForSeconds(0.37f);
        float leftFront=enemy.GetComponent<Collider2D>().bounds.min.x-player.GetComponent<Collider2D>().bounds.extents.x-0.1f;
        Check(!pull.IsPulling && Mathf.Abs(player.transform.position.x-leftFront)<0.4f,
            $"left facing chain also pulls to the correct front side (x={player.transform.position.x:F3}, front={leftFront:F3}, pulling={pull.IsPulling})");

        enemy.SetActive(false);enemy.SetActive(true);movement.SetTarget(player.transform);
        Place(enemy,0f);Place(player,3f);
        playerBefore=playerHealth.CurBlood;
        Cast(controller,2);combat.SampleFrame(8);
        Check(enemy.transform.Find("Combat/AttackThree").gameObject.activeSelf, "slam box opens at landing frame 8");
        combat.SampleFrame(8);combat.SampleFrame(9);
        RedChainShockWave[] waves=FindObjectsByType<RedChainShockWave>();
        Check(waves.Length==1, "landing frame emits exactly one pooled shockwave");
        Check(Mathf.Approximately(playerBefore-playerHealth.CurBlood,20f), "slam deals exactly 20 once");
        Check(!enemy.transform.Find("Combat/AttackThree").gameObject.activeSelf, "slam closes its attack box");
        RedChainShockWave firstWave=waves[0];
        yield return new WaitForSeconds(1.6f);
        Check(!firstWave.gameObject.activeSelf, "shockwave returns to pool after 1.5 seconds");
        enemy.GetComponentInChildren<RedChainShockWaveEmitter>().Emit(combat);
        Check(FindObjectsByType<RedChainShockWave>().Single()==firstWave, "next impact reuses pooled effect");

        enemy.SetActive(false);enemy.SetActive(true);movement.SetTarget(player.transform);
        Place(enemy,0f);Place(player,7f);Cast(controller,1);combat.SampleFrame(8);
        yield return new WaitForFixedUpdate();
        enemy.SetActive(false);
        yield return new WaitForFixedUpdate();
        Check(!health.Boost && !pull.IsPulling, "pool despawn clears armor and active drag");
        enemy.SetActive(true);movement.SetTarget(player.transform);
        Check(!health.Boost && !controller.IsRecovering && combat.Attack==-1 && health.CurBlood==135f, "respawn has full HP and no stale action state");
        Cast(controller,0);health.TakeDamage(new DamageInfo(player.transform,1000f));
        Check(health.Dead && !health.Boost && !enemy.transform.Find("Combat/AttackOne").gameObject.activeSelf, "death immediately removes armor and closes attack windows");
    }
}
#endif

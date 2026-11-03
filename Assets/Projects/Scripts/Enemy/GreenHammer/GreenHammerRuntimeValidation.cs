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
using Object=UnityEngine.Object;

//编辑器Play Mode探针；场地与碰撞体变更只作用于临时测试对象。
public sealed class GreenHammerRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder result=new();
    private static string Report=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/GreenHammerAudit/runtime-validation.txt"));
    private IEnumerator Start()
    {
        IEnumerator run=Scenario();
        while(true)
        {
            object wait;
            try{if(!run.MoveNext())break;wait=run.Current;}
            catch(Exception e){result.AppendLine("FAIL: "+e);File.WriteAllText(Report,result.ToString());Debug.LogError("GREENHAMMER_RUNTIME_FAIL: "+e);EditorApplication.isPlaying=false;yield break;}
            yield return wait;
        }
        result.AppendLine("ALL PASS");File.WriteAllText(Report,result.ToString());Debug.Log("GREENHAMMER_RUNTIME_PASS");EditorApplication.isPlaying=false;
    }
    private void Check(bool ok,string message)
    {if(!ok)throw new InvalidOperationException(message);result.AppendLine("PASS: "+message);File.WriteAllText(Report,result.ToString());}
    private static bool Near(float a,float b,float tolerance=0.025f)=>Mathf.Abs(a-b)<=tolerance;
    private static void Place(GameObject go,float x)
    {
        BoxCollider2D collider=go.GetComponent<BoxCollider2D>();float feet=(collider.offset.y-collider.size.y/2f)*go.transform.lossyScale.y;
        go.transform.position=new Vector3(x,0.025f-feet,0f);Rigidbody2D body=go.GetComponent<Rigidbody2D>();body.position=go.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    private static void Reset(GameObject enemy,PlayerController player,float playerX)
    {
        enemy.SetActive(false);enemy.SetActive(true);enemy.GetComponent<EnemyMovement>().SetTarget(player.transform);Place(enemy,0f);Place(player.gameObject,playerX);
    }
    private static void Force(GameObject enemy,int attack)
    {
        GreenHammerCombat combat=enemy.GetComponent<GreenHammerCombat>();combat.ClearAttackRequests();combat.AttackRequest[attack]=true;combat.HasAtatck(true);
        typeof(EnemyController).GetMethod("SetState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(enemy.GetComponent<GreenHammerController>(),new object[]{EnemyState.Attacking});
    }
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(0.3f);
        LevelManager stage=FindAnyObjectByType<LevelManager>();Check(stage!=null && stage.enabled && stage.IsInitialized,"Level6 placeholder is replaced by a working enabled stage");
        Check(stage.EnemyManager is GreenHammerStageEnemyManager && FindObjectsByType<GreenHammerController>(FindObjectsInactive.Include).Length==10,"Level6 prewarms exactly ten GreenHammer enemies");
        SerializedProperty pools=new SerializedObject(stage.EnemyManager).FindProperty("enemyPools");bool registered=false;
        for(int i=0;i<pools.arraySize;i++){SerializedProperty p=pools.GetArrayElementAtIndex(i);if(p.FindPropertyRelative("prefab").objectReferenceValue is GreenHammerController)registered=p.FindPropertyRelative("poolSize").intValue==10 && Near(p.FindPropertyRelative("weight").floatValue,0.55f);}
        Check(registered,"scene pool is registered with size 10 and weight 0.55");
        foreach(GameObject root in gameObject.scene.GetRootGameObjects())if(root!=gameObject)root.SetActive(false);
        GameObject floor=new("ValidationFloor"){layer=6};floor.transform.position=Vector3.down*0.5f;BoxCollider2D floorBox=floor.AddComponent<BoxCollider2D>();floorBox.size=new Vector2(100f,1f);
        GameObject cameraRoot=new("ValidationCamera");cameraRoot.SetActive(false);cameraRoot.tag="MainCamera";Camera camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=15f;camera.transform.position=new Vector3(0f,2f,-10f);
        CameraManager cm=cameraRoot.AddComponent<CameraManager>();SerializedObject cameraData=new(cm);
        foreach(string field in new[]{"cameraEdgeLeft","cameraEdgeRight"}){GameObject edge=new(field);edge.transform.position=Vector3.right*(field=="cameraEdgeLeft"?-40f:40f);SerializedProperty array=cameraData.FindProperty(field);array.arraySize=1;array.GetArrayElementAtIndex(0).objectReferenceValue=edge.transform;}
        cameraData.ApplyModifiedPropertiesWithoutUndo();cameraRoot.SetActive(true);
        GameObject hero=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));PlayerController player=hero.GetComponent<PlayerController>();Health hp=hero.GetComponent<Health>();hp.SetBaseBlood(2000f);hp.Heal(2000f);
        hero.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation|RigidbodyConstraints2D.FreezePositionX;Place(hero,3f);
        GameObject enemy=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/GreenHammer.prefab"));GreenHammerController controller=enemy.GetComponent<GreenHammerController>();GreenHammerCombat combat=enemy.GetComponent<GreenHammerCombat>();GreenHammerMovement movement=enemy.GetComponent<GreenHammerMovement>();Health health=enemy.GetComponent<Health>();Rigidbody2D body=enemy.GetComponent<Rigidbody2D>();
        movement.SetTarget(hero.transform);Place(enemy,0f);GreenHammerAttackBox[] boxes=enemy.GetComponentsInChildren<GreenHammerAttackBox>(true);
        Check(health.BeginBlood==150f && Near(new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue,20f),"HP 150 and XP 20");
        Check(enemy.GetComponents<ActorStatus>().Length==1 && controller.CanReceiveHit && !combat.Angry,"one ActorStatus, always available hurtbox, no anger scaling");
        Check(GreenHammerCombat.EligibleMask(2f,7)==2 && GreenHammerCombat.EligibleMask(4f,7)==7 && GreenHammerCombat.EligibleMask(7f,7)==4 && GreenHammerCombat.EligibleMask(8.1f,7)==0,"selection respects sweep 2.5-6, leap at most 6, ground 3-8");
        int selected=0;for(int n=0;n<100;n++){for(int i=0;i<3;i++)combat.AttackRequest[i]=true;combat.SelectRequest(4f);for(int i=0;i<3;i++)if(combat.AttackRequest[i])selected|=1<<i;}
        Check(selected==7,"all three ready skills can be chosen randomly");combat.ClearAttackRequests();combat.AttackRequest[0]=combat.AttackRequest[2]=true;combat.SelectRequest(4f);
        Check(combat.AttackRequest[0] && !combat.AttackRequest[2],"sweep has priority when not all three skills are ready");combat.ClearAttackRequests();
        float deadline=Time.time+5f,before=hp.CurBlood;
        while(!controller.IsCasting && Time.time<deadline)yield return null;
        Check(controller.IsCasting && combat.Attack==0 && health.Boost,"normal AI starts sweep with super armor after its independent cooldown");
        health.TakeDamage(new DamageInfo(hero.transform,1f,true));movement.Stun(this,1f);
        Check(Near(health.CurBlood,149f) && !health.Attacked && !movement.IsStunned && controller.IsCasting,"ordinary damage deducts HP without interrupting armored cast or applying stun");
        deadline=Time.time+0.8f;while(!boxes[0].gameObject.activeSelf && Time.time<deadline)yield return null;
        Check(boxes[0].gameObject.activeSelf && Near(before-hp.CurBlood,19f),"sweep opens at frame 5 and deals 19 with knockback");
        for(int i=0;i<100;i++){combat.SampleFrame(6);boxes[0].SampleNow();}
        Check(Near(before-hp.CurBlood,19f),"repeated sweep samples and trigger callbacks cannot repeat its damage");
        deadline=Time.time+0.7f;while(health.Boost && Time.time<deadline)yield return null;
        Check(!health.Boost && !boxes[0].gameObject.activeSelf && controller.IsRecovering && Near(controller.RecoveryUntil-Time.time,1.4f,0.05f),"frame 9 closes sweep, removes armor and starts exactly 1.4 seconds recovery");
        float recovery=controller.RecoveryUntil;health.TakeDamage(new DamageInfo(hero.transform,1f,false));Check(health.Attacked,"ordinary hurt reacts normally during the output window");
        while(Time.time<recovery-0.02f){if(combat.Attack>=0 && !controller.IsCasting || Mathf.Abs(body.linearVelocityX)>0.01f)throw new InvalidOperationException("movement or new cast during recovery");yield return null;}
        Check(Near(controller.RecoveryUntil,recovery) && !controller.IsCasting,"hurt does not extend recovery or allow a new startup");yield return new WaitForSeconds(0.05f);Check(!controller.IsRecovering,"recovery ends after 1.4 seconds");

        Reset(enemy,player,5f);Force(enemy,1);deadline=Time.time+0.6f;
        while(!movement.IsLeaping && Time.time<deadline)yield return null;
        Check(movement.IsLeaping && body.gravityScale==0f && body.linearVelocityY>0f && body.linearVelocityX>0f,"frame 4 starts leap with zero gravity and horizontal/vertical launch velocity");
        float peak=enemy.transform.position.y;before=hp.CurBlood;deadline=Time.time+1f;
        while(!movement.HasLanded && Time.time<deadline){peak=Mathf.Max(peak,enemy.transform.position.y);yield return null;}
        deadline=Time.time+0.3f;while(hp.CurBlood==before && Time.time<deadline)yield return null;
        Check(movement.HasLanded && !movement.IsLeaping && Near(body.gravityScale,0.9f) && peak>0.2f && enemy.transform.position.x>1f && enemy.transform.position.x<=3.1f,"short leap lands safely and restores gravity before its impact");
        Check(Near(before-hp.CurBlood,26f) && boxes[1].gameObject.activeSelf,"landing opens one radius 3 attack for exactly 26");for(int i=0;i<100;i++)combat.SampleFrame(10);
        Check(Near(before-hp.CurBlood,26f),"repeated landing frames cannot repeat 26 damage");deadline=Time.time+0.6f;while(health.Boost && Time.time<deadline)yield return null;
        Check(!health.Boost && !boxes[1].gameObject.activeSelf && controller.IsRecovering,"frame 12 closes leap attack and removes its armor");

        Reset(enemy,player,5f);Force(enemy,1);deadline=Time.time+0.5f;while(!movement.IsLeaping && Time.time<deadline)yield return null;
        health.Status.ApplyFreeze(this,2f);yield return null;
        Check(!controller.IsCasting && !movement.IsLeaping && !health.Boost && Near(body.gravityScale,0.9f),"strong freeze cancels leap and restores its original gravity");health.Status.CancelFreeze(this);
        Reset(enemy,player,5f);Force(enemy,1);deadline=Time.time+0.5f;while(!movement.IsLeaping && Time.time<deadline)yield return null;
        movement.BeginCarry(this,new Vector2(enemy.transform.position.x,2f));Check(movement.IsCarried && !movement.IsLeaping && !controller.IsCasting,"strong carry can interrupt leap without leaving an active attack");
        movement.EndCarry(this);yield return new WaitForSeconds(0.05f);Check(!movement.IsCarried && Near(body.gravityScale,0.9f),"carry release restores nonzero gravity in the common falling phase");
        yield return new WaitForSeconds(1f);Check(!movement.IsExternallyControlled && movement.IsOnGroundNow(),"interrupted leap falls to ground instead of remaining suspended");
        Reset(enemy,player,5f);Force(enemy,1);deadline=Time.time+0.5f;while(!movement.IsLeaping && Time.time<deadline)yield return null;health.TakeDamage(new DamageInfo(hero.transform,1000f,false));yield return null;
        Check(health.Dead && !movement.IsLeaping && !health.Boost && Near(body.gravityScale,0.9f),"death in midair clears the attack, armor and temporary zero gravity");
        Reset(enemy,player,5f);Force(enemy,1);deadline=Time.time+0.5f;while(!movement.IsLeaping && Time.time<deadline)yield return null;enemy.SetActive(false);enemy.SetActive(true);movement.SetTarget(hero.transform);Place(enemy,0f);
        Check(!controller.IsCasting && !controller.IsRecovering && !movement.IsLeaping && Near(body.gravityScale,0.9f) && health.CurBlood==150f,"pooled reuse restores full HP, normal gravity and clean state");

        Reset(enemy,player,2f);hp.Heal(2000f);before=hp.CurBlood;Force(enemy,2);deadline=Time.time+1f;while(combat.SegmentsEmitted<1 && Time.time<deadline)yield return null;
        Check(combat.SegmentsEmitted==1 && Near(before-hp.CurBlood,18f) && boxes[2].gameObject.activeSelf,"ground frame 9 opens the first independent 18 damage segment");
        Place(hero,4f);deadline=Time.time+0.4f;while(combat.SegmentsEmitted<2 && Time.time<deadline)yield return null;
        Check(combat.SegmentsEmitted==2 && Near(before-hp.CurBlood,36f),"second segment advances 2 units and deals its own 18 once");
        Place(hero,6f);deadline=Time.time+0.4f;while(combat.SegmentsEmitted<3 && Time.time<deadline)yield return null;
        Check(combat.SegmentsEmitted==3 && Near(before-hp.CurBlood,54f),"third segment advances another 2 units and total damage is 54");
        Check(Near(combat.SegmentTime(1)-combat.SegmentTime(0),0.15f,0.035f) && Near(combat.SegmentTime(2)-combat.SegmentTime(1),0.15f,0.035f),"three damage segments remain spaced by 0.15 seconds");
        GreenHammerShockwavePool pool=GreenHammerShockwavePool.GetOrCreate();GreenHammerShockwave[] waves=pool.GetComponentsInChildren<GreenHammerShockwave>(true);
        Check(waves.Length==3 && waves.Select(w=>w.GetComponent<BoxCollider2D>()).Distinct().Count()==3 && waves.All(w=>!w.GetComponent<BoxCollider2D>().enabled),"pool uses distinct segment trigger bodies and closes each instant damage sample");
        Check(waves.All(w=>AssetDatabase.GetAssetPath(w.GetComponentInChildren<SpriteRenderer>().sprite)=="Assets/Art/Enemies/Level6/GreenHammer/Effects/GreenHammerShockwave.png"),"all three ground segments play the dedicated jade shockwave art");
        Check(waves.All(w=>w.GetComponentInChildren<SpriteRenderer>().flipX),"right-facing shockwaves mirror the left-facing source artwork");
        for(int i=0;i<100;i++)combat.SampleFrame(15);Place(hero,2f);yield return new WaitForSeconds(0.12f);Place(hero,4f);yield return new WaitForSeconds(0.12f);Place(hero,6f);yield return new WaitForSeconds(0.12f);
        Check(Near(before-hp.CurBlood,54f) && combat.SegmentsEmitted==3,"staying in or reentering visible waves never repeats ground damage");
        Check(!boxes[2].gameObject.activeSelf && !health.Boost && controller.IsRecovering,"ground attack shuts after the third segment and releases armor");
        yield return new WaitForSeconds(1f);Check(waves.All(w=>!w.gameObject.activeSelf),"all three shockwave visuals return to the pool after their lifetime");
        GreenHammerShockwave wavePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/GreenHammerShockwave.prefab").GetComponent<GreenHammerShockwave>();
        combat.AttackFaceRight=false;GreenHammerShockwave reused=pool.Spawn(wavePrefab,combat,Vector2.zero,2f,false,1f);Check(waves.Contains(reused),"shockwave pool reuses its existing instances");
        Check(!reused.GetComponentInChildren<SpriteRenderer>().flipX,"pooled reuse resets effect direction for a left-facing attack");pool.ReleaseAll();

        controller.enabled=false;Place(enemy,0f);Place(hero,26f);GameObject corner=new("CornerProbe"){layer=3};corner.transform.SetParent(hero.transform,false);BoxCollider2D cornerBox=corner.AddComponent<BoxCollider2D>();cornerBox.isTrigger=true;cornerBox.size=Vector2.one*0.1f;
        corner.transform.position=new Vector3(2.7f,2.7f);boxes[1].gameObject.SetActive(true);Physics2D.SyncTransforms();before=hp.CurBlood;boxes[1].SampleNow();
        Check(hp.CurBlood==before,"radius 3 impact rejects targets only inside the box corners");corner.transform.position=new Vector3(2f,1f);Physics2D.SyncTransforms();boxes[1].SampleNow();
        Check(Near(before-hp.CurBlood,26f),"radius 3 impact accepts targets inside the circle");boxes[1].gameObject.SetActive(false);corner.SetActive(false);Object.Destroy(corner);

        controller.enabled=true;Reset(enemy,player,3.5f);controller.enabled=false;yield return new WaitForSeconds(10.1f);combat.CheckAttack();
        Check(combat.AttackRequest.All(r=>r),"sweep, leap and ground independently become ready after their own cooldowns");
        combat.ClearAttackRequests();combat.AttackRequest[0]=true;combat.HasAtatck(true);combat.AttackEnd();combat.AttackOver();combat.CheckAttack();
        Check(!combat.AttackRequest[0] && combat.AttackRequest[1] && combat.AttackRequest[2],"using sweep consumes only its own four second cooldown");yield return new WaitForSeconds(3.8f);combat.CheckAttack();Check(!combat.AttackRequest[0],"sweep is still cooling down before four seconds");
        yield return new WaitForSeconds(0.25f);combat.CheckAttack();Check(combat.AttackRequest[0],"sweep becomes ready at its four second cooldown");
        controller.enabled=true;Reset(enemy,player,20f);Force(enemy,2);combat.SampleFrame(16);combat.SampleFrame(16);Check(combat.SegmentsEmitted==1 && combat.HasPendingSegments,"skipping animation frames does not collapse three impacts into one instant");
        yield return new WaitForSeconds(0.4f);Check(combat.SegmentsEmitted==3 && combat.SegmentTime(1)-combat.SegmentTime(0)>=0.149f && combat.SegmentTime(2)-combat.SegmentTime(1)>=0.149f,"late frame sampling preserves three separate timed impacts");
        health.TakeDamage(new DamageInfo(hero.transform,1000f));pool.ReleaseAll();
        Reset(enemy,player,3f);health.TakeDamage(new DamageInfo(hero.transform,149f,false,false,null,false,true));hp.SetReflection(this,1f);Force(enemy,0);
        combat.SampleFrame(5);
        Check(health.Dead && combat.Attack==-1 && !controller.IsCasting && !health.Boost,"reflection killing the caster during impact cancels safely without accessing a stale attack index");hp.RemoveReflection(this);
    }
}
#endif

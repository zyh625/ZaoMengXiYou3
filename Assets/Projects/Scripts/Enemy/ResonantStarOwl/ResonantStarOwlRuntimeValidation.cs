#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using zmxy.GamePlay;
using Object = UnityEngine.Object;

public sealed class ResonantStarOwlRuntimeValidation : MonoBehaviour
{
    private readonly System.Text.StringBuilder report = new();
    private const string Fx = "Assets/Projects/Prefabs/SeparateEffort/ResonantStarOwl";
    private void Check(bool value,string message) { if(!value) throw new Exception(message); report.AppendLine("PASS: "+message); }
    private IEnumerator Start()
    {
        var scenario=Scenario();
        while(true)
        {
            object wait;
            try { if(!scenario.MoveNext()) break; wait=scenario.Current; }
            catch(Exception e) { report.AppendLine("FAIL: "+e); Finish(1); yield break; }
            yield return wait;
        }
        report.AppendLine("ALL PASS"); Finish(0);
    }
    private void Finish(int code)
    {
        File.WriteAllText("Logs/ResonantStarOwlAudit/runtime-validation.txt",report.ToString());
        Debug.Log(report.ToString()); EditorApplication.Exit(code);
    }
    private static void Set(Object obj,string field,Action<SerializedProperty> write)
    { var so=new SerializedObject(obj);write(so.FindProperty(field));so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Center(GameObject actor,Vector2 center)
    {
        var rb=actor.GetComponent<Rigidbody2D>(); var box=actor.GetComponent<BoxCollider2D>();
        actor.transform.position += (Vector3)(center-(Vector2)box.bounds.center);
        rb.position=actor.transform.position;rb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    private IEnumerator Scenario()
    {
        BattleRun.Begin(8,null,false);
        var floor=new GameObject("OwlTestFloor"){layer=6};floor.AddComponent<BoxCollider2D>().size=new Vector2(100f,1f);floor.transform.position=Vector3.down*0.5f;
        var cam=new GameObject("OwlTestCamera");cam.SetActive(false);cam.tag="MainCamera";
        var camera=cam.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=30f;cam.transform.position=new Vector3(0,2,-10);
        var cm=cam.AddComponent<CameraManager>();var left=new GameObject("Left");left.transform.position=Vector3.left*40f;
        var right=new GameObject("Right");right.transform.position=Vector3.right*40f;
        Set(cm,"cameraEdgeLeft",p=>{p.arraySize=1;p.GetArrayElementAtIndex(0).objectReferenceValue=left.transform;});
        Set(cm,"cameraEdgeRight",p=>{p.arraySize=1;p.GetArrayElementAtIndex(0).objectReferenceValue=right.transform;});cam.SetActive(true);
        var player=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));
        var pc=player.GetComponent<PlayerController>();
        var pm=player.GetComponent<PlayerMovement>();var hp=player.GetComponent<Health>();hp.SetBaseBlood(10000f);hp.Heal(10000f);
        hp.SetSuperArmor(this,true);var prb=player.GetComponent<Rigidbody2D>();prb.gravityScale=0f;prb.constraints=RigidbodyConstraints2D.FreezeAll;
        var owl=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ResonantStarOwlSetupPath));
        var ctl=owl.GetComponent<ResonantStarOwlController>();ctl.enabled=false;
        var combat=owl.GetComponent<ResonantStarOwlCombat>();var movement=owl.GetComponent<ResonantStarOwlMovement>();var eh=owl.GetComponent<Health>();
        combat.BindTarget(pc);movement.SetTarget(player.transform);movement.StepFlight();
        yield return null;
        Check(eh.BeginBlood==100f,"HP 100");
        var pool=ResonantStarOwlEffectPool.GetOrCreate();
        ResonantStarOwlEffect Prefab(string kind)=>AssetDatabase.LoadAssetAtPath<GameObject>(Fx+kind+".prefab").GetComponent<ResonantStarOwlEffect>();
        void ResetCast(int skill)
        {
            owl.SetActive(false);owl.SetActive(true);combat.BindTarget(pc);movement.StepFlight();
            Check(combat.BeginCast(skill),"begin skill "+skill);
        }
        ResetCast(0);Center(player,new Vector2(0f,2.2f));float before=hp.CurBlood;
        pool.Emit(Prefab("SoundRings"),combat,0,0);combat.FinishCast();
        yield return new WaitForSeconds(1.3f);
        Check(hp.CurBlood==before,"ring inner safe zone stays safe throughout expansion");
        ResetCast(0);Center(player,new Vector2(4f,2.2f));before=hp.CurBlood;
        for(int i=0;i<3;i++){pool.Emit(Prefab("SoundRings"),combat,0,i);yield return new WaitForSeconds(0.4f);}
        combat.FinishCast();yield return new WaitForSeconds(1.3f);
        Check(Mathf.Abs(before-hp.CurBlood-24f)<0.01f,"three annuli hit once each, total 24, no repeat contact damage");
        Check(!ResonantStarOwlEffect.IntersectsAnnulus(new Bounds(Vector3.zero,Vector3.one),Vector2.zero,2f,2.4f),"annulus excludes solid inner disk");
        ResetCast(1);Center(player,new Vector2(4f,0.55f));before=hp.CurBlood;
        pool.Emit(Prefab("HighLowChime"),combat,1,0);pool.Emit(Prefab("HighLowChime"),combat,1,1);
        Check(Mathf.Abs(before-hp.CurBlood-10f)<0.01f,"low chime hits ground layer once; high layer misses");combat.FinishCast();pool.ReleaseAll();
        ResetCast(1);Center(player,new Vector2(4f,2.6f));before=hp.CurBlood;
        pool.Emit(Prefab("HighLowChime"),combat,1,0);pool.Emit(Prefab("HighLowChime"),combat,1,1);
        Check(Mathf.Abs(before-hp.CurBlood-10f)<0.01f,"high chime hits upper layer once; low layer misses");combat.FinishCast();pool.ReleaseAll();
        ResetCast(2);Center(player,new Vector2(4f,0.55f));before=hp.CurBlood;
        var band=pool.Emit(Prefab("ResonanceBand"),combat,2,0);
        yield return new WaitForSeconds(0.3f);Check(hp.CurBlood==before,"ground passage under band takes no damage");
        Center(player,new Vector2(4f,2.5f));typeof(PlayerMovement).GetField("hasJumped",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pm,0);
        yield return new WaitForSeconds(0.1f);Check(hp.CurBlood==before,"band excludes airborne actors that are not jumping");
        typeof(PlayerMovement).GetField("hasJumped",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pm,1);
        prb.constraints=RigidbodyConstraints2D.FreezePositionX|RigidbodyConstraints2D.FreezeRotation;prb.linearVelocityY=2f;
        yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(before-hp.CurBlood-6f)<0.01f && prb.linearVelocityY<=-1f,"band deals 6 and stops upward jump");
        yield return new WaitForSeconds(0.2f);Check(Mathf.Abs(before-hp.CurBlood-6f)<0.01f,"band deduplicates lingering jumping player");
        yield return new WaitForSeconds(1.5f);Check(!band.gameObject.activeSelf,"band expires after two seconds");combat.FinishCast();pool.ReleaseAll();
        ResetCast(0);Center(player,new Vector2(5f,0.6f));var times=new List<float>();var frames=new List<int>();
        Action<int,int> capture=(skill,part)=>{times.Add(Time.time);frames.Add(combat.AnimationFrame);};combat.SegmentReleased+=capture;
        float deadline=Time.time+1.5f;while(Time.time<deadline){combat.SampleCast();yield return null;}
        Check(times.Count==3 && Mathf.Abs(times[1]-times[0]-0.4f)<0.06f && Mathf.Abs(times[2]-times[1]-0.4f)<0.06f,"cast emits exactly three rings spaced 0.4 seconds");
        Check(frames[0]==5&&frames[1]==8&&frames[2]==11,"ring release animation indexes 5,8,11 (frames 6,9,12)");combat.FinishCast();pool.ReleaseAll();
        ResetCast(1);times.Clear();frames.Clear();deadline=Time.time+1.3f;
        while(Time.time<deadline){combat.SampleCast();yield return null;}
        Check(times.Count==2&&Mathf.Abs(times[1]-times[0]-0.5f)<0.06f&&frames[0]==5&&frames[1]==10,"chime low then high, 0.5 seconds at frames 6 and 11");combat.SegmentReleased-=capture;combat.FinishCast();pool.ReleaseAll();
        ResetCast(0);combat.CancelCast();Center(player,new Vector2(20f,0.6f));ctl.enabled=true;
        yield return new WaitForSeconds(2f);var rb=owl.GetComponent<Rigidbody2D>();
        Check(rb.position.y>1.8f&&rb.gravityScale==0f&&rb.linearVelocityX>0f,$"live controller hovers and approaches without falling: pos={rb.position}, velocity={rb.linearVelocity}, gravity={rb.gravityScale}, hp={eh.CurBlood}, target={combat.Target}, falling={movement.Falling}");
        Center(player,new Vector2(rb.position.x+2f,0.6f));yield return new WaitForSeconds(0.2f);
        Check(rb.linearVelocityX<0f&&rb.linearVelocityY>0f,"close target causes backward and upward retreat");
        var wall=new GameObject("OwlTestWall"){layer=6};wall.transform.position=new Vector3(rb.position.x+1.5f,2f);
        var wallBox=wall.AddComponent<BoxCollider2D>();wallBox.size=new Vector2(0.5f,4f);Physics2D.SyncTransforms();
        Center(player,new Vector2(20f,0.6f));yield return new WaitForSeconds(1.5f);
        Check(owl.GetComponent<BoxCollider2D>().bounds.max.x < wallBox.bounds.min.x+0.02f,"flight sweep stops body before terrain boundary");
        wall.SetActive(false);
        eh.TakeDamage(new DamageInfo(player.transform,1000f,false));yield return new WaitForSeconds(0.2f);
        Check(rb.gravityScale>0f&&rb.linearVelocityY<0f,"death enables physical fall");
        owl.SetActive(false);owl.SetActive(true);movement.StepFlight();Check(!movement.Falling&&rb.gravityScale==0f,"pooled life restores hover and gravity zero");
    }
    private const string ResonantStarOwlSetupPath="Assets/Projects/Prefabs/Enemy/ResonantStarOwl.prefab";
}
#endif


#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using zmxy.GamePlay;
using Object=UnityEngine.Object;

//仅编辑器Play Mode验收；测试对象、血量和场地均不写回资产或存档。
public sealed class WestKingRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder result=new();
    private static string Report=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/WestKingAudit/runtime-validation.txt"));
    private IEnumerator Start()
    {
        IEnumerator run=Scenario();
        while(true)
        {
            object wait;
            try{if(!run.MoveNext())break;wait=run.Current;}
            catch(Exception e){result.AppendLine("FAIL: "+e);File.WriteAllText(Report,result.ToString());Debug.LogError("WESTKING_RUNTIME_FAIL: "+e);EditorApplication.isPlaying=false;yield break;}
            yield return wait;
        }
        result.AppendLine("ALL PASS");File.WriteAllText(Report,result.ToString());Debug.Log("WESTKING_RUNTIME_PASS");EditorApplication.isPlaying=false;
    }
    private void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);result.AppendLine("PASS: "+message);}
    private static bool Near(float a,float b,float tolerance=0.02f)=>Mathf.Abs(a-b)<=tolerance;
    private static void Damage(Health hp,float amount,Transform source,bool periodic=true)=>hp.TakeDamage(new DamageInfo(source,amount,false,false,null,false,periodic,DamageKind.Skill));
    private static void Place(GameObject go,float x)
    {
        BoxCollider2D box=go.GetComponent<BoxCollider2D>();float feet=(box.offset.y-box.size.y/2f)*go.transform.lossyScale.y;
        go.transform.position=new Vector3(x,0.02f-feet,0f);Rigidbody2D body=go.GetComponent<Rigidbody2D>();body.position=go.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    private static void Reset(GameObject boss,PlayerController player)
    {boss.SetActive(false);boss.SetActive(true);boss.GetComponent<WestCombat>().BindTarget(player);boss.GetComponent<EnemyMovement>().SetTarget(player.transform);Place(boss,0f);}
    private static void BreakAll(WestCombat combat,Transform source)
    {foreach(WestKingOrb orb in combat.Effects.Orbs)if(orb!=null && orb.Alive)Damage(orb.GetComponent<Health>(),1000f,source);}
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(0.3f);
        LevelManager stage=FindAnyObjectByType<LevelManager>();
        Check(stage!=null && stage.enabled && stage.IsInitialized,"Level5 enabled manager initializes with WestKing");
        Check(stage.EnemyManager.CurBoss!=null && stage.EnemyManager.CurBoss.GetComponent<WestCombat>()!=null,"real scene creates WestKing at existing boss spawn");
        Check(FindObjectsByType<RedChainController>(FindObjectsInactive.Include).Length==10 && FindObjectsByType<RedBoomController>(FindObjectsInactive.Include).Length==10,"both existing mob pools remain 10 members");
        Check(stage.BeginBattlePractice(null,true),"normal Boss practice wiring activates WestKing");
        WestBossUI panel=FindAnyObjectByType<WestBossUI>();Check(panel!=null && panel.gameObject.activeInHierarchy,"WestBossUI is visible for actual Boss instance");
        SerializedObject ui=new(panel);Image blood=(Image)ui.FindProperty("bloodFull").objectReferenceValue,special=(Image)ui.FindProperty("specialState").objectReferenceValue;
        Health real=stage.EnemyManager.CurBoss.GetComponent<Health>();Damage(real,330f,transform);
        Check(Near(blood.fillAmount,0.7f) && special.fillAmount>0.99f,"Boss health bar and ward countdown update from real scene events");
        WestCombat realCombat=real.GetComponent<WestCombat>();
        Check(realCombat.ActiveOrbs==3 && realCombat.Effects.Orbs.All(o=>o.GetComponent<Health>().BeginBlood==80f && o.GetComponent<EnemyMovement>().CanReceiveHit),"real arena has three attackable 80 HP phantoms");
        float lastX=float.NegativeInfinity;
        foreach(WestKingOrb orb in realCombat.Effects.Orbs){Check(orb.transform.position.x>lastX && orb.transform.position.x>realCombat.Arena.x && orb.transform.position.x<realCombat.Arena.y,"orb occupies ordered left / middle / right arena segment");lastX=orb.transform.position.x;}
        foreach(GameObject root in gameObject.scene.GetRootGameObjects())if(root!=gameObject)root.SetActive(false);

        GameObject floor=new("ValidationFloor"){layer=6};floor.transform.position=Vector3.down*0.5f;floor.AddComponent<BoxCollider2D>().size=new Vector2(100f,1f);
        GameObject cameraRoot=new("ValidationCamera");cameraRoot.SetActive(false);cameraRoot.tag="MainCamera";
        Camera camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=15f;camera.transform.position=new Vector3(0f,2f,-10f);
        CameraManager cm=cameraRoot.AddComponent<CameraManager>();SerializedObject cmData=new(cm);
        foreach(string field in new[]{"cameraEdgeLeft","cameraEdgeRight"})
        {
            GameObject edge=new(field);edge.transform.position=Vector3.right*(field=="cameraEdgeLeft"?-40f:40f);
            SerializedProperty array=cmData.FindProperty(field);array.arraySize=1;array.GetArrayElementAtIndex(0).objectReferenceValue=edge.transform;
        }
        cmData.ApplyModifiedPropertiesWithoutUndo();cameraRoot.SetActive(true);
        GameObject hero=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));Place(hero,5f);
        PlayerController player=hero.GetComponent<PlayerController>();Health hp=hero.GetComponent<Health>();hp.SetBaseBlood(3000f);hp.Heal(3000f);
        GameObject boss=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/WestKing.prefab"));WestController controller=boss.GetComponent<WestController>();controller.enabled=false;
        WestCombat combat=boss.GetComponent<WestCombat>();WestMovement movement=boss.GetComponent<WestMovement>();Health health=boss.GetComponent<Health>();
        combat.BindTarget(player);movement.SetTarget(player.transform);combat.SetArenaBounds(-12f,12f);Place(boss,0f);yield return new WaitForSeconds(0.15f);
        Check(health.BeginBlood==1100f && !combat.Angry,"HP 1100 and UsesAnger disabled");
        Damage(health,329f,hero.transform);Check(!combat.IsSpecial,"ward does not trigger above 70 percent");
        Damage(health,1f,hero.transform);Check(combat.IsWarding && combat.WardStarts==1 && combat.ActiveOrbs==3,"first 70 percent crossing triggers one 12 second ward");
        float before=health.CurBlood;Damage(health,10f,hero.transform,false);
        Check(Near(before-health.CurBlood,3f) && health.Boost && !health.IsInvincible && !health.Attacked,"ward is exactly 70 percent reduction with ordinary super armor and active hurtbox");
        GameObject reducer=new("OtherReduction");health.SetDamageMultiplier(reducer,0.5f);before=health.CurBlood;Damage(health,10f,hero.transform);
        Check(Near(before-health.CurBlood,1.5f),"ward multiplies with another source without replacing it");
        movement.Stun(this,2f);Check(!movement.IsStunned && combat.IsWarding,"ordinary stun cannot interrupt ward");
        health.Status.ApplyFreeze(this,10f);float remaining=combat.WardRemaining;yield return new WaitForSeconds(0.3f);
        Check(Near(combat.WardRemaining,remaining) && combat.WardTimerPaused,"strong freeze pauses the ward countdown");health.Status.CancelFreeze(this);
        float deadline=Time.time+4.3f;WestKingOrb warning=null;
        while(Time.time<deadline && warning==null){foreach(WestKingOrb orb in combat.Effects.Orbs)if(orb.IsWarning){warning=orb;break;}if(warning==null)yield return null;}
        Check(warning!=null && combat.WardRemaining<=8.02f,$"a random living phantom warns after 4 seconds (remaining={combat.WardRemaining}, phase={combat.Phase}, paused={combat.WardTimerPaused}, alive={combat.ActiveOrbs}, target={combat.Target})");
        before=hp.CurBlood;float warned=Time.time;while(Time.time<warned+0.75f){if(warning.Shots!=0 || hp.CurBlood!=before)throw new InvalidOperationException("phantom ray fired before warning completed");yield return null;}
        Check(warning.Shots==0,"warning has not dealt damage before 0.8 seconds");
        deadline=Time.time+0.3f;while(warning.Shots==0 && Time.time<deadline)yield return null;
        Check(warning.Shots==1 && Near(before-hp.CurBlood,12f),"phantom ray deals 12 once after its warning");
        yield return new WaitForSeconds(2.05f);Check(Near(before-hp.CurBlood,18f),"phantom ray burn deals 6 over the following 2 seconds");
        WestKingOrb[] orbs=combat.Effects.Orbs;Damage(orbs[0].GetComponent<Health>(),80f,hero.transform);Damage(orbs[1].GetComponent<Health>(),80f,hero.transform);
        Check(combat.IsWarding && combat.ActiveOrbs==1,"two broken phantoms do not open exposure");Damage(orbs[2].GetComponent<Health>(),80f,hero.transform);
        Check(combat.Phase==WestCombat.WardPhase.Exposed && Near(combat.ExposureRemaining,3f) && !health.Boost,"all three broken phantoms open a 3 second exposure window");
        before=health.CurBlood;Damage(health,10f,hero.transform);Check(Near(before-health.CurBlood,7.5f),"exposure is 1.5 times damage and preserves the other 0.5 reduction");
        controller.enabled=true;yield return new WaitForSeconds(0.2f);Check(combat.ReadyAttack==-1 && Near(boss.GetComponent<Rigidbody2D>().linearVelocityX,0f),"exposed Boss cannot move or start attacks");
        yield return new WaitForSeconds(2.9f);controller.enabled=false;
        before=health.CurBlood;Damage(health,10f,hero.transform);Check(!combat.IsSpecial && Near(before-health.CurBlood,5f),"exposure expires and only its own multiplier is removed");
        health.RemoveDamageMultiplier(reducer);health.Heal(1100f);Damage(health,330f,hero.transform);
        Check(!combat.IsSpecial && combat.WardStarts==1,"healing and crossing 70 percent again cannot retrigger the first ward");
        Damage(health,440f,hero.transform);Check(combat.IsWarding && combat.WardStarts==2,"first 30 percent crossing triggers the second ward");
        float oldHP=hp.CurBlood;hero.SetActive(false);GameObject replacement=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/Bajie.prefab"));Place(replacement,5f);
        player=replacement.GetComponent<PlayerController>();hp=replacement.GetComponent<Health>();hp.SetBaseBlood(3000f);hp.Heal(3000f);combat.BindTarget(player);movement.SetTarget(player.transform);
        int penalties=0;bool knocked=false;Action<DamageInfo,float> observe=(d,amount)=>{if(Near(d.Amount,35f)){penalties++;knocked=d.Knockback;}};
        hp.DamageReceived+=observe;Func<DamageInfo,bool> shield=d=>!Near(d.Amount,35f);hp.DamageBlocked+=shield;before=hp.CurBlood;
        yield return new WaitForSeconds(12.15f);
        Check(!combat.IsSpecial && combat.TimeoutHits==1 && penalties==1 && knocked && Near(before-hp.CurBlood,35f),"timeout deals exactly one 35 damage knockback to the current replacement character");
        Check(Near(hero.GetComponent<Health>().CurBlood,oldHP) && combat.ActiveOrbs==0,"timeout does not hit the old character and removes every phantom");
        yield return new WaitForSeconds(0.2f);Check(penalties==1,"timeout cannot settle a second time");hp.DamageBlocked-=shield;hp.DamageReceived-=observe;
        before=health.CurBlood;Damage(health,10f,replacement.transform);Check(Near(before-health.CurBlood,10f) && !health.Boost,"timeout completely removes ward reduction and armor");
        health.Heal(1100f);Damage(health,780f,replacement.transform);Check(combat.WardStarts==2 && !combat.IsSpecial,"neither threshold can trigger a third ward in the same life");

        Reset(boss,player);Place(replacement,5f);yield return new WaitForSeconds(0.15f);
        Check(combat.BeginAttack(0),"claw attack can start");combat.SampleFrame(11);Check(!boss.GetComponentInChildren<WestClawBox>(true).gameObject.activeSelf,"claw remains shut before 0.7 second windup");
        before=hp.CurBlood;yield return new WaitForSeconds(0.71f);combat.SampleFrame(11);
        Check(Near(before-hp.CurBlood,25f),"claw deals exactly 25 at release");for(int i=0;i<100;i++)combat.SampleFrame(11);
        Check(Near(before-hp.CurBlood,25f),"claw release and trigger callbacks share one hit record");combat.SampleFrame(14);
        Check(!boss.GetComponentInChildren<WestClawBox>(true).gameObject.activeSelf,"claw shuts at frame 14");combat.CancelAttack();
        Check(!combat.BeginAttack(1) && Near(combat.NextAttackAt-Time.time,2.2f),"every completed or cancelled attack enforces 2.2 seconds recovery");
        Place(replacement,5f);yield return new WaitForSeconds(2.25f);Check(combat.BeginAttack(1),"pearl toss can start after recovery");
        yield return new WaitForSeconds(0.61f);before=hp.CurBlood;combat.SampleFrame(9);combat.CancelAttack();
        WestKingPearl pearl=FindObjectsByType<WestKingPearl>().First(p=>p.gameObject.activeSelf);deadline=Time.time+4f;
        while(pearl.gameObject.activeSelf && Time.time<deadline)yield return null;
        Check(pearl.Bounces==2 && pearl.Explosions==1 && Near(before-hp.CurBlood,54f),"toss bounces twice for 16 each then explodes once for 22");
        Check(combat.BeginAttack(3),"orbit attack can start");yield return new WaitForSeconds(0.41f);combat.SampleFrame(6);combat.CancelAttack();
        pearl=FindObjectsByType<WestKingPearl>().First(p=>p.IsOrbiting);
        Action orbit=(Action)Delegate.CreateDelegate(typeof(Action),pearl,typeof(WestKingPearl).GetMethod("Orbit",BindingFlags.NonPublic|BindingFlags.Instance));orbit();
        Vector3 delta=pearl.transform.position-replacement.GetComponent<BoxCollider2D>().bounds.center;replacement.transform.position+=delta;replacement.GetComponent<Rigidbody2D>().position=replacement.transform.position;Physics2D.SyncTransforms();
        before=hp.CurBlood;int oldHits=pearl.OrbitHits;for(int i=0;i<100;i++)orbit();
        Check(pearl.OrbitHits==oldHits+1 && Near(before-hp.CurBlood,14f),"orbit contact deals 14 and repeated samples respect 0.5 second internal cooldown");
        yield return new WaitForSeconds(5.05f);Check(!pearl.gameObject.activeSelf,"orbit pearl recycles after 5 seconds");
        Place(replacement,5f);Check(combat.BeginAttack(2),"breath attack can start");yield return new WaitForSeconds(1.01f);combat.SampleFrame(12);
        WestKingFlame flame=FindObjectsByType<WestKingFlame>().First(f=>f.gameObject.activeSelf);before=hp.CurBlood;
        Action tick=(Action)Delegate.CreateDelegate(typeof(Action),flame,typeof(WestKingFlame).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance));for(int i=0;i<10;i++)tick();long allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)tick();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Check(allocated==0,"1000 flame updates between ticks allocate zero managed bytes");
        yield return new WaitForSeconds(6.1f);
        Check(flame.StreamTicks==4 && flame.GroundTicks==8 && Near(before-hp.CurBlood,120f) && !flame.gameObject.activeSelf,"breath ticks four times then ground fire ticks eight times for 10 every 0.5 seconds");combat.CancelAttack();

        Reset(boss,player);Damage(health,800f,replacement.transform);Check(combat.WardStarts==1,"a single hit crossing both thresholds starts only one ward immediately");BreakAll(combat,replacement.transform);
        yield return new WaitForSeconds(3.1f);Check(combat.WardStarts==2 && combat.IsWarding && combat.ActiveOrbs==3,"queued second threshold waits until first exposure ends");
        Damage(health,10000f,replacement.transform);Check(health.Dead && combat.ActiveOrbs==0 && !combat.IsSpecial,"Boss death clears ward state and all pooled phantoms");

        Reset(boss,player);Place(replacement,5f);hp.Heal(3000f);
        replacement.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation|RigidbodyConstraints2D.FreezePositionX;
        controller.enabled=true; //固定靶的横向位置，避免爪击击退使下一招脱离测试距离。
        WestClawBox claw=boss.GetComponentInChildren<WestClawBox>(true);deadline=Time.time+1.5f;
        while(!claw.gameObject.activeSelf && Time.time<deadline)yield return null;
        Check(claw.gameObject.activeSelf,"full AI and SpriteManager open DragonClaw on its real release frame");
        deadline=Time.time+5f;WestKingPearl thrown=null;
        while(thrown==null && Time.time<deadline){thrown=FindObjectsByType<WestKingPearl>().FirstOrDefault(p=>!p.IsOrbiting && p.gameObject.activeSelf);if(thrown==null)yield return null;}
        Check(thrown!=null,"full AI releases PearlToss after the claw recovery");
        deadline=Time.time+7f;WestKingFlame channel=null;
        while(channel==null && Time.time<deadline){channel=FindObjectsByType<WestKingFlame>().FirstOrDefault(f=>f.gameObject.activeSelf && !f.IsPulse && combat.CurrentSkill==2 && !f.IsGroundFire);if(channel==null)yield return null;}
        Check(channel!=null && combat.Channeling,$"full AI releases DragonBreath and holds the cast for its channel (skill={combat.CurrentSkill}, released={combat.Released}, delta={movement.DeltaX})");
        deadline=Time.time+2.2f;while(!channel.IsGroundFire && Time.time<deadline)yield return null;
        Check(channel.StreamTicks==4,"real breath recovery preserves the final fourth damage tick");
        Place(replacement,3.5f);deadline=Time.time+4f;WestKingPearl revolving=null;
        while(revolving==null && Time.time<deadline){revolving=FindObjectsByType<WestKingPearl>().FirstOrDefault(p=>p.IsOrbiting);if(revolving==null)yield return null;}
        Check(revolving!=null,"full AI releases PearlOrbit against a close target");
        controller.enabled=false;combat.CancelAttack();combat.Effects.ReleaseAll();
    }
}
#endif

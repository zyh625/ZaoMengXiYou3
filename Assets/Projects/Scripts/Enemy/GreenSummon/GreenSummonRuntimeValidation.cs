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

//原生Play Mode验收，测试夹具只存在于运行期，不写玩家存档或测试场景。
public sealed class GreenSummonRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder result=new();
    private static string Report=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/GreenSummonAudit/runtime-validation.txt"));
    private IEnumerator Start()
    {
        IEnumerator run=Scenario();
        while(true)
        {
            object wait;
            try{if(!run.MoveNext())break;wait=run.Current;}
            catch(Exception e){result.AppendLine("FAIL: "+e);File.WriteAllText(Report,result.ToString());Debug.LogError("GREENSUMMON_RUNTIME_FAIL: "+e);EditorApplication.isPlaying=false;yield break;}
            yield return wait;
        }
        result.AppendLine("ALL PASS");File.WriteAllText(Report,result.ToString());Debug.Log("GREENSUMMON_RUNTIME_PASS");EditorApplication.isPlaying=false;
    }
    private void Check(bool ok,string message)
    {if(!ok)throw new InvalidOperationException(message);result.AppendLine("PASS: "+message);File.WriteAllText(Report,result.ToString());}
    private static bool Near(float a,float b,float tolerance=0.03f)=>Mathf.Abs(a-b)<=tolerance;
    private static void Place(GameObject actor,float x)
    {
        BoxCollider2D shape=actor.GetComponent<BoxCollider2D>();float feet=(shape.offset.y-shape.size.y/2f)*actor.transform.lossyScale.y;
        actor.transform.position=new Vector3(x,0.025f-feet,0f);Rigidbody2D body=actor.GetComponent<Rigidbody2D>();body.position=actor.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    private static float[] Cooldowns(GreenSummonCombat combat)=>(float[])typeof(GreenSummonCombat).GetField("readyAt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(combat);
    private static void Ready(GreenSummonCombat combat,int skill){combat.CancelCast();for(int i=0;i<3;i++)Cooldowns(combat)[i]=i==skill?0f:Time.time+100f;combat.GetComponent<Health>().AttackedEnd();}
    private static void ResetCaster(GameObject actor,PlayerController target,float x=0f)
    {
        actor.GetComponent<GreenSummonController>().enabled=false;actor.SetActive(false);actor.SetActive(true);
        actor.GetComponent<GreenSummonCombat>().BindTarget(target);actor.GetComponent<EnemyMovement>().SetTarget(target.transform);Place(actor,x);
    }
    private static T Prefab<T>(string name) where T:Component=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/"+name+".prefab").GetComponent<T>();
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(0.3f);
        LevelManager stage=FindAnyObjectByType<LevelManager>();Check(stage!=null && stage.enabled && stage.IsInitialized,"real Level6 stage is enabled and initializes");
        Check(FindObjectsByType<GreenHammerController>(FindObjectsInactive.Include).Length==10 && FindObjectsByType<GreenSummonController>(FindObjectsInactive.Include).Length==10,"scene prewarms ten GreenHammer and ten GreenSummon instances");
        SerializedProperty pools=new SerializedObject(stage.EnemyManager).FindProperty("enemyPools");bool hammerRegistered=false,summonRegistered=false;
        for(int i=0;i<pools.arraySize;i++)
        {
            SerializedProperty p=pools.GetArrayElementAtIndex(i);Object actor=p.FindPropertyRelative("prefab").objectReferenceValue;
            if(actor is GreenHammerController)hammerRegistered=p.FindPropertyRelative("poolSize").intValue==10 && Near(p.FindPropertyRelative("weight").floatValue,0.55f);
            if(actor is GreenSummonController)summonRegistered=p.FindPropertyRelative("poolSize").intValue==10 && Near(p.FindPropertyRelative("weight").floatValue,0.45f);
        }
        Check(hammerRegistered && summonRegistered,"Level6 preserves GreenHammer 10 / 0.55 and adds GreenSummon 10 / 0.45");
        Check(FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length==1,"Level6 enables exactly one EventSystem");
        foreach(GameObject root in gameObject.scene.GetRootGameObjects())if(root!=gameObject)root.SetActive(false);BattleRun.Begin(6,null);
        GameObject floor=new("ValidationFloor"){layer=6};floor.transform.position=Vector3.down*0.5f;BoxCollider2D floorBox=floor.AddComponent<BoxCollider2D>();floorBox.size=new Vector2(100f,1f);
        GameObject cameraRoot=new("ValidationCamera");cameraRoot.SetActive(false);cameraRoot.tag="MainCamera";Camera camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=15f;camera.transform.position=new Vector3(0f,2f,-10f);
        CameraManager cm=cameraRoot.AddComponent<CameraManager>();SerializedObject cameraData=new(cm);
        foreach(string field in new[]{"cameraEdgeLeft","cameraEdgeRight"})
        {GameObject edge=new(field);edge.transform.position=Vector3.right*(field=="cameraEdgeLeft"?-40f:40f);SerializedProperty a=cameraData.FindProperty(field);a.arraySize=1;a.GetArrayElementAtIndex(0).objectReferenceValue=edge.transform;}
        cameraData.ApplyModifiedPropertiesWithoutUndo();cameraRoot.SetActive(true);
        GameObject hero=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));PlayerController player=hero.GetComponent<PlayerController>();Health hp=hero.GetComponent<Health>();hp.SetBaseBlood(2000f);hp.Heal(2000f);hero.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation|RigidbodyConstraints2D.FreezePositionX;Place(hero,5f);
        GameObject enemy=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/GreenSummon.prefab"));GreenSummonController controller=enemy.GetComponent<GreenSummonController>();GreenSummonCombat combat=enemy.GetComponent<GreenSummonCombat>();GreenSummonMovement movement=enemy.GetComponent<GreenSummonMovement>();Health health=enemy.GetComponent<Health>();Rigidbody2D body=enemy.GetComponent<Rigidbody2D>();ResetCaster(enemy,player);
        GreenSummonEffectPool pool=GreenSummonEffectPool.GetOrCreate();GreenSummonWarBanner flagPrefab=Prefab<GreenSummonWarBanner>("GreenSummonWarBanner");GreenSummonHammer hammerPrefab=Prefab<GreenSummonHammer>("GreenSummonHammer");GreenSummonWave wavePrefab=Prefab<GreenSummonWave>("GreenSummonWave");
        Check(health.BeginBlood==88f && new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue==20f,"HP 88 and experience 20");
        Check(enemy.GetComponents<ActorStatus>().Length==1 && controller.CanReceiveHit && !combat.Angry && !health.Boost,"one ActorStatus, ordinary hurt response and always enabled hurtbox");
        Check(combat.ReadySkill==2 && combat.BeginCast(2),"first priority reserves WarBanner before hammer and wave");
        GameObject other=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/GreenSummon.prefab"));ResetCaster(other,player,15f);GreenSummonCombat otherCombat=other.GetComponent<GreenSummonCombat>();
        Check(otherCombat.ReadySkill==0 && !otherCombat.BeginCast(2),"a reserved friendly banner prevents another caster from starting a duplicate banner");
        combat.CancelCast();Check(!pool.HasFriendlyBanner,"canceling an uncommitted banner releases its reservation");other.SetActive(false);

        ResetCaster(enemy,player);controller.enabled=true;float castStart=Time.time,deadline=Time.time+1.8f;
        while(!pool.GetComponentsInChildren<GreenSummonWarBanner>().Any() && Time.time<deadline)yield return null;
        GreenSummonWarBanner flag=pool.GetComponentsInChildren<GreenSummonWarBanner>().FirstOrDefault();
        Check(flag!=null && Time.time-castStart>=0.9f && Time.time-castStart<1.5f,"native AI plays the 16 fps insertion and plants at its final frame");
        Vector2 planted=flag.transform.position;controller.enabled=false;combat.CancelCast();
        Check(Near(planted.x,enemy.transform.position.x+0.18f) && Near(planted.y,enemy.transform.position.y+0.95f),"banner root is released at the configured hand anchor");
        Check(flag.GetComponent<Health>().BeginBlood==60f && flag.GetComponent<EnemyMovement>().CanReceiveHit && flag.GetComponent<EnemyRewards>()==null,"independent banner has attackable HP 60 and grants no kill reward");
        Place(enemy,-2f);yield return new WaitForSeconds(0.15f);Check(((Vector2)flag.transform.position-planted).sqrMagnitude<0.0001f,"planted flag stays in world space after the caster moves");

        GameObject ally=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/GreenHammer.prefab"));ally.GetComponent<GreenHammerController>().enabled=false;ally.GetComponent<EnemyMovement>().SetTarget(player.transform);ally.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;Place(ally,3f);
        EnemyCombat allyCombat=ally.GetComponent<EnemyCombat>();Health allyHealth=ally.GetComponent<Health>();GameObject vulnerabilitySource=new("IndependentVulnerability");allyCombat.ApplyAttackBuff(this,1.3f,100f);allyHealth.SetDamageMultiplier(this,0.5f);allyHealth.SetDamageMultiplier(vulnerabilitySource,1.2f);
        yield return new WaitForSeconds(0.15f);Check(Near(allyCombat.DamageMultiplier,1.3f*1.15f),"allies inside five units get exactly a source-owned 15 percent attack buff");
        float before=allyHealth.CurBlood;allyHealth.TakeDamage(new DamageInfo(hero.transform,20f,false,false,null,false,true));Check(Near(before-allyHealth.CurBlood,10.8f),"banner ten percent reduction multiplies independently with existing reduction and vulnerability");
        Place(ally,9f);yield return new WaitForSeconds(0.15f);before=allyHealth.CurBlood;allyHealth.TakeDamage(new DamageInfo(hero.transform,20f,false,false,null,false,true));
        Check(Near(allyCombat.DamageMultiplier,1.3f) && Near(before-allyHealth.CurBlood,12f),"leaving the aura removes only banner sources and keeps other attack/reduction/vulnerability sources");
        Place(ally,3f);yield return new WaitForSeconds(0.15f);Check(Near(allyCombat.DamageMultiplier,1.495f),"reentering the aura refreshes one source without accumulating stacks");
        Health flagHealth=flag.GetComponent<Health>();flagHealth.TakeDamage(new DamageInfo(hero.transform,10f,false));float originalUntil=flag.ExpiresAt;
        GreenSummonWarBanner refreshed=pool.Plant(flagPrefab,combat,new Vector2(22f,4f));
        Check(refreshed==flag && flagHealth.CurBlood==50f && (Vector2)flag.transform.position==planted && flag.ExpiresAt>originalUntil,"same caster refreshes only its existing flag lifetime, without healing, moving or multiplying it");
        flagHealth.TakeDamage(new DamageInfo(hero.transform,60f,false));Check(!flag.gameObject.activeSelf && !pool.HasFriendlyBanner && Near(allyCombat.DamageMultiplier,1.3f),"destroying the flag immediately removes its attack source and pool ownership");
        before=allyHealth.CurBlood;allyHealth.TakeDamage(new DamageInfo(hero.transform,20f,false,false,null,false,true));Check(Near(before-allyHealth.CurBlood,12f),"destroying the flag removes its reduction but preserves independent multipliers");
        flag=pool.Plant(flagPrefab,combat,new Vector2(0.18f,0.975f));Check(flag==refreshed && flag.GetComponent<Health>().CurBlood==60f,"flag pool reuses a clean full-health object after destruction");
        deadline=flag.ExpiresAt;while(Time.time<deadline-0.04f)yield return null;Check(flag.gameObject.activeSelf,"banner remains active for the full eight-second duration");yield return new WaitForSeconds(0.12f);
        Check(!flag.gameObject.activeSelf && !pool.HasFriendlyBanner && Near(allyCombat.DamageMultiplier,1.3f),"eight-second expiry removes all banner attack sources");
        before=allyHealth.CurBlood;allyHealth.TakeDamage(new DamageInfo(hero.transform,10f,false,false,null,false,true));Check(Near(before-allyHealth.CurBlood,6f),"expiry also removes the banner reduction and preserves the other sources");
        allyCombat.RemoveAttackBuff(this);allyHealth.RemoveDamageMultiplier(this);allyHealth.RemoveDamageMultiplier(vulnerabilitySource);ally.SetActive(false);

        ResetCaster(enemy,player);Place(hero,5f);Ready(combat,0);Check(combat.BeginCast(0),"hammer begins a warning at the target's initial ground position");GreenSummonHammer hammer=combat.Warning;Vector2 initial=hammer.Position;
        Check(Near(initial.x,5f) && Near(initial.y,0f) && Near(hammer.StrikeAt-Time.time,1.2f),"warning duration is exactly 1.2 seconds and its center is on the ground");
        Check(otherCombat.gameObject.activeSelf==false && GreenSummonHammer.HasWarningAt(initial),"active warnings reserve their target area");
        combat.SampleFrame(8);Check(!combat.Released,"hammer is not committed before the command frame 9");combat.SampleFrame(9);before=hp.CurBlood;Place(hero,12f);
        yield return new WaitForSeconds(1.1f);Check(!hammer.HasStruck && hp.CurBlood==before && hammer.Position==initial,"warning does not track a moving player or strike early");
        string fallingSprite=hammer.GetComponentInChildren<SpriteRenderer>().sprite.name;int fallingFrame=int.Parse(fallingSprite.Substring(fallingSprite.LastIndexOf('_')+1));Check(fallingFrame is >=3 and <=7,"supplied hammer descent frames play before the 1.2 second impact");yield return new WaitForSeconds(0.18f);
        Check(hammer.HasStruck && hp.CurBlood==before,"walking out of the fixed radius 2.5 warning avoids its damage");yield return new WaitForSeconds(0.6f);Check(!hammer.gameObject.activeSelf,"finished hammer animation recycles automatically");
        combat.CancelCast();Ready(combat,0);Place(hero,5f);Check(combat.BeginCast(0),"a recycled hammer can warn again");hammer=combat.Warning;combat.SampleFrame(9);before=hp.CurBlood;
        yield return new WaitForSeconds(1.28f);Check(hammer.HasStruck && Near(before-hp.CurBlood,22f),"remaining inside the warning takes exactly one 22 damage strike");
        for(int i=0;i<100;i++){combat.SampleFrame(15);hammer.Commit(1f);}yield return new WaitForSeconds(0.2f);Check(Near(before-hp.CurBlood,22f),"repeated animation samples and multiple player hurtboxes cannot repeat the hammer damage");pool.ReleaseAll();combat.CancelCast();
        Ready(combat,0);combat.BeginCast(0);hammer=combat.Warning;health.TakeDamage(new DamageInfo(hero.transform,1f,false));combat.SampleFrame(5);
        Check(!hammer.gameObject.activeSelf && combat.CurrentSkill<0,"ordinary hurt cancels an uncommitted hammer warning");health.AttackedEnd();
        Ready(combat,0);combat.BeginCast(0);hammer=combat.Warning;combat.SampleFrame(9);before=hp.CurBlood;enemy.SetActive(false);yield return new WaitForSeconds(1.28f);
        Check(hammer.HasStruck && Near(before-hp.CurBlood,22f),"a committed falling hammer resolves after its caster returns to the pool");pool.ReleaseAll();

        ResetCaster(enemy,player);Place(hero,5f);Ready(combat,1);combat.BeginCast(1);combat.SampleFrame(6);Check(!combat.Released,"wave waits until sweep frame 7");combat.SampleFrame(7);
        GreenSummonWave wave=pool.GetComponentsInChildren<GreenSummonWave>().First();before=hp.CurBlood;for(int i=0;i<100;i++)combat.SampleFrame(15);yield return new WaitForSeconds(0.6f);
        Check(wave.HitPlayer && !wave.gameObject.activeSelf && Near(before-hp.CurBlood,14f),"one wave deals 14 to its first player and disappears even after repeated release samples");combat.CancelCast();
        Place(hero,5f);wave=pool.Fire(wavePrefab,combat,new Vector2(0.65f,1.15f),true);before=hp.CurBlood;enemy.SetActive(false);yield return new WaitForSeconds(0.6f);
        Check(wave.HitPlayer && Near(before-hp.CurBlood,14f),"scene-owned wave continues to hit after the shooter is disabled");
        ResetCaster(enemy,player);GameObject wall=new("WaveWall"){layer=6};wall.transform.position=new Vector3(2.5f,1.5f);wall.AddComponent<BoxCollider2D>().size=new Vector2(0.25f,3f);Physics2D.SyncTransforms();before=hp.CurBlood;
        wave=pool.Fire(wavePrefab,combat,new Vector2(0.65f,1.15f),true);yield return new WaitForSeconds(0.6f);
        Check(!wave.HitPlayer && !wave.gameObject.activeSelf && hp.CurBlood==before,"wave sweeps against solid terrain and cannot damage through a wall");wall.SetActive(false);Place(hero,20f);
        wave=pool.Fire(wavePrefab,combat,new Vector2(0.65f,1.15f),true);yield return new WaitForSeconds(1.3f);Check(!wave.gameObject.activeSelf && !wave.HitPlayer,"wave returns at its twelve-unit maximum travel distance");

        GameObject secondHero=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/Bajie.prefab"));PlayerController secondPlayer=secondHero.GetComponent<PlayerController>();Health secondHp=secondHero.GetComponent<Health>();secondHp.SetBaseBlood(2000f);secondHp.Heal(2000f);secondHero.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation|RigidbodyConstraints2D.FreezePositionX;Place(hero,4f);Place(secondHero,6f);before=hp.CurBlood;float beforeSecond=secondHp.CurBlood;
        wave=pool.Fire(wavePrefab,combat,new Vector2(0.65f,1.15f),true);yield return new WaitForSeconds(0.8f);Check(Near(before-hp.CurBlood,14f) && secondHp.CurBlood==beforeSecond,"wave stops at the first of two players rather than piercing both");
        Place(hero,5f);Ready(combat,0);combat.BindTarget(player);combat.BeginCast(0);hammer=combat.Warning;combat.SampleFrame(9);before=hp.CurBlood;beforeSecond=secondHp.CurBlood;
        hp.PreserveForSwitch();hero.SetActive(false);combat.BindTarget(secondPlayer);Place(secondHero,12f);yield return new WaitForSeconds(1.28f);
        Check(hp.CurBlood==before && secondHp.CurBlood==beforeSecond && hammer.Position.x==5f,"switching characters neither retargets a pending hammer nor hurts the inactive character");pool.ReleaseAll();hero.SetActive(true);Place(hero,5f);secondHero.SetActive(false);ResetCaster(enemy,player);

        Ready(combat,2);combat.BeginCast(2);combat.CancelCast();Ready(combat,0);combat.BeginCast(0);combat.SampleFrame(9);combat.CancelCast();Ready(combat,1);combat.BeginCast(1);
        Check(Near(Cooldowns(combat)[1]-Time.time,6f),"wave has its own six-second cooldown");combat.CancelCast();pool.ReleaseAll();
        ResetCaster(enemy,player);combat.BeginCast(2);float bannerUntil=Cooldowns(combat)[2];combat.CancelCast();combat.BeginCast(0);float hammerUntil=Cooldowns(combat)[0];combat.SampleFrame(9);combat.CancelCast();combat.BeginCast(1);
        Check(Near(bannerUntil-Time.time,16f) && Near(hammerUntil-Time.time,8f) && Near(Cooldowns(combat)[1]-Time.time,6f) && combat.ReadySkill<0,"banner sixteen, hammer eight and wave six seconds are independent");combat.CancelCast();pool.ReleaseAll();

        ResetCaster(enemy,player);Place(hero,2f);yield return new WaitForSeconds(0.15f);movement.SetRetreatBounds(-20f,20f);float retreatStart=Time.time;Check(movement.TryRetreat(2f),"close pressure starts a safe three-unit retreat");float retreatReady=movement.RetreatReadyAt;
        while(movement.IsRetreating && Time.time<retreatStart+0.6f){movement.StepRetreat();yield return new WaitForFixedUpdate();}
        Check(Near(enemy.transform.position.x,-3f,0.04f) && Near(body.linearVelocityX,0f) && Near(retreatReady-retreatStart,6f),"retreat moves exactly three units horizontally and starts the confirmed six-second cooldown");
        Place(enemy,0f);Check(!movement.TryRetreat(2f),"retreat cannot restart before its six-second cooldown");
        ResetCaster(enemy,player);yield return new WaitForSeconds(0.12f);wall.transform.position=new Vector3(-1.5f,1.5f);wall.SetActive(true);Physics2D.SyncTransforms();Check(!movement.TryRetreat(2f),"retreat rejects a solid wall anywhere along its path");wall.SetActive(false);
        ally.SetActive(true);Place(ally,-1.8f);Check(!movement.TryRetreat(2f),"retreat rejects another enemy occupying its path");ally.SetActive(false);
        ResetCaster(enemy,player);yield return new WaitForSeconds(0.12f);movement.SetRetreatBounds(-2f,20f);Check(!movement.TryRetreat(2f),"retreat rejects destinations beyond the current arena boundary");movement.SetRetreatBounds(-20f,20f);
        floorBox.size=new Vector2(1.5f,1f);Physics2D.SyncTransforms();Check(!movement.TryRetreat(2f),"retreat rejects a destination without supporting ground");floorBox.size=new Vector2(100f,1f);Physics2D.SyncTransforms();
        GameObject originPlatform=new("OriginPlatform"){layer=6},destinationPlatform=new("DestinationPlatform"){layer=6};originPlatform.transform.position=new Vector3(0f,-0.5f);destinationPlatform.transform.position=new Vector3(-3f,-0.5f);originPlatform.AddComponent<BoxCollider2D>().size=destinationPlatform.AddComponent<BoxCollider2D>().size=new Vector2(1.5f,1f);
        floor.SetActive(false);Physics2D.SyncTransforms();yield return new WaitForSeconds(0.08f);Check(!movement.TryRetreat(2f),"retreat rejects a gap in the path even when its destination has a floor");floor.SetActive(true);originPlatform.SetActive(false);destinationPlatform.SetActive(false);Physics2D.SyncTransforms();
        ResetCaster(enemy,player);yield return new WaitForSeconds(0.12f);health.Status.ApplyFreeze(this,1f);Check(!movement.TryRetreat(2f),"strong freeze prevents retreat without changing shared control behavior");health.Status.CancelFreeze(this);

        ResetCaster(enemy,player);Place(hero,2f);for(int i=0;i<3;i++)Cooldowns(combat)[i]=Time.time+20f;typeof(GreenSummonMovement).GetField("readyAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(movement,Time.time+20f);typeof(GreenSummonMovement).GetField("life",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(movement,health.LifeVersion);controller.enabled=true;yield return new WaitForSeconds(0.12f);
        result.AppendLine("INFO: range AI velocity="+body.linearVelocity+", grounded="+movement.IsOnGroundNow()+", position="+enemy.transform.position+", player="+hero.transform.position+", frozen="+health.Status.BlocksMovement+", external="+movement.IsExternallyControlled+", state="+new SerializedObject(controller).FindProperty("state").enumValueIndex);File.WriteAllText(Report,result.ToString());
        //采样点在Physics2D解算后，默认地面摩擦会将指令速度0.8减到约0.73；数值配置单独精确校验。
        Check(body.linearVelocityX is <-0.65f and >=-0.8f && new SerializedObject(movement).FindProperty("moveSpeed").floatValue==0.8f,"normal AI retreats below four units with configured speed 0.8 and normal ground friction");Place(hero,10f);yield return new WaitForSeconds(0.12f);Check(body.linearVelocityX is >0.65f and <=0.8f,"normal AI approaches above seven units");Place(hero,5f);yield return new WaitForSeconds(0.12f);Check(Near(body.linearVelocityX,0f) && combat.CurrentSkill<0,"normal AI holds position at four to seven units while all skills cool down");controller.enabled=false;

        ResetCaster(enemy,player);Place(hero,5f);controller.enabled=true;deadline=Time.time+2f;while(!pool.HasFriendlyBanner && Time.time<deadline)yield return null;
        deadline=Time.time+2f;while(combat.CurrentSkill!=0 && Time.time<deadline)yield return null;
        Check(combat.CurrentSkill==0 && combat.Warning!=null && enemy.GetComponentInChildren<SpriteRenderer>().sprite.name.StartsWith("SummonedHammerStrike_"),"native AI rotates from banner into the bound hammer animation");
        deadline=Time.time+2f;while(combat.CurrentSkill!=1 && Time.time<deadline)yield return null;
        Check(combat.CurrentSkill==1 && enemy.GetComponentInChildren<SpriteRenderer>().sprite.name.StartsWith("BannerWave_"),"native AI rotates from hammer into the bound wave animation");
        deadline=Time.time+1.2f;while(!combat.Released && Time.time<deadline)yield return null;
        Check(combat.Released && pool.GetComponentsInChildren<GreenSummonWave>().Any(),"actual SpriteManager release frame emits the native wave");controller.enabled=false;pool.ReleaseAll();

        ResetCaster(enemy,player);Place(hero,5f);Ready(combat,0);combat.BeginCast(0);combat.SampleFrame(9);health.TakeDamage(new DamageInfo(hero.transform,80f,false,false,null,false,true));hp.SetReflection(this,1f);yield return new WaitForSeconds(1.28f);
        Check(health.Dead && combat.CurrentSkill<0,"reflection can kill the caster inside hammer resolution without a repeated strike or stale cast");hp.RemoveReflection(this);pool.ReleaseAll();
        ResetCaster(enemy,player);flag=pool.Plant(flagPrefab,combat,new Vector2(0.18f,0.975f));health.TakeDamage(new DamageInfo(hero.transform,1000f,false));yield return new WaitForSeconds(0.1f);
        Check(health.Dead && flag.gameObject.activeSelf && pool.HasFriendlyBanner,"a released independent banner survives the caster's death until its own destruction or expiry");pool.ReleaseAll();
        ResetCaster(enemy,player);flag=pool.Plant(flagPrefab,combat,new Vector2(0.18f,0.975f));wave=pool.Fire(wavePrefab,combat,new Vector2(0.65f,1.15f),true);hammer=pool.Warn(hammerPrefab,combat,new Vector2(5f,0f));hammer.Commit(1f);BattleRun.Cancel();yield return new WaitForSeconds(0.08f);
        Check(!flag.gameObject.activeSelf && !wave.gameObject.activeSelf && !hammer.gameObject.activeSelf && !pool.HasFriendlyBanner && Near(combat.DamageMultiplier,1f),"battle end recycles all effects and removes every banner-owned buff source");
    }
}
#endif

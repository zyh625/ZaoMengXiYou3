#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using zmxy.GamePlay;

//编辑器探针：正式场景先跑四波→Boss→出口；后续夹具只在运行时创建，不保存场景/玩家档案。
public sealed class NorthKingRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder result=new();
    private static string Report=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/NorthKingAudit/runtime-validation.txt"));
    private IEnumerator Start()
    {
        IEnumerator run=Scenario();while(true)
        {
            object wait;try{if(!run.MoveNext())break;wait=run.Current;}
            catch(Exception e){result.AppendLine("FAIL: "+e);File.WriteAllText(Report,result.ToString());Debug.LogError("NORTHKING_RUNTIME_FAIL: "+e);EditorApplication.isPlaying=false;yield break;}
            yield return wait;
        }
        result.AppendLine("ALL PASS");File.WriteAllText(Report,result.ToString());Debug.Log("NORTHKING_RUNTIME_PASS");EditorApplication.isPlaying=false;
    }
    private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);result.AppendLine("PASS: "+text);File.WriteAllText(Report,result.ToString());}
    private static bool Near(float a,float b,float tolerance=0.04f)=>Mathf.Abs(a-b)<=tolerance;
    private static FieldInfo Field(Type type,string name){for(;type!=null;type=type.BaseType){FieldInfo f=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(f!=null)return f;}throw new MissingFieldException(name);}
    private static T Get<T>(object target,string name)=>(T)Field(target.GetType(),name).GetValue(target);
    private static void Set(object target,string name,object value)=>Field(target.GetType(),name).SetValue(target,value);
    private static void Place(GameObject go,float x,float ground=0f)
    {
        BoxCollider2D shape=go.GetComponent<BoxCollider2D>();float feet=(shape.offset.y-shape.size.y/2f)*go.transform.lossyScale.y;
        go.transform.position=new Vector3(x,ground+0.025f-feet,0f);Rigidbody2D body=go.GetComponent<Rigidbody2D>();body.position=go.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    private static void ResetBoss(GameObject boss,PlayerController player)
    {
        boss.GetComponent<NorthController>().enabled=false;boss.SetActive(false);boss.SetActive(true);boss.GetComponent<NorthCombat>().BindTarget(player);boss.GetComponent<NorthCombat>().SetArenaBounds(-10f,10f);boss.GetComponent<EnemyMovement>().SetTarget(player.transform);Place(boss,0f);
    }
    private static void Ready(NorthCombat combat,int skill)
    {combat.CancelAttack();Set(combat,"nextAttack",0f);float[] ready=Get<float[]>(combat,"readyAt");for(int i=0;i<4;i++)ready[i]=i==skill?0f:Time.time+100f;combat.GetComponent<Health>().AttackedEnd();}
    private static void Force(GameObject boss,int skill)
    {
        NorthCombat combat=boss.GetComponent<NorthCombat>();Ready(combat,skill);NorthController controller=boss.GetComponent<NorthController>();controller.enabled=true;Set(controller,"pending",skill);
        typeof(EnemyController).GetMethod("SetState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new object[]{EnemyState.Skill});
    }
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(0.35f);
        LevelManager stage=FindAnyObjectByType<LevelManager>();Check(stage!=null && stage.enabled && stage.IsInitialized,"real Level6 initializes its enabled stage with the new Boss");
        Check(stage.WaveCount==4 && stage.EnemyManager.BossPrefab.GetComponent<NorthCombat>()!=null,"four-wave stage points to NorthKing");
        Check(FindObjectsByType<GreenHammerController>(FindObjectsInactive.Include).Length==10 && FindObjectsByType<GreenSummonController>(FindObjectsInactive.Include).Length==10,"both existing ten-member enemy pools are preserved");
        NorthBossUI ui=FindAnyObjectByType<NorthBossUI>(FindObjectsInactive.Include);Check(ui!=null,"scene uses NorthBossUI rather than another Boss UI");
        Image blood=(Image)new SerializedObject(ui).FindProperty("bloodFull").objectReferenceValue,special=(Image)new SerializedObject(ui).FindProperty("specialState").objectReferenceValue;
        Check(blood!=null && special!=null,"Boss HP and existing SpecialState fill images are assigned");
        NorthCombat realBoss=stage.EnemyManager.CurBoss.GetComponent<NorthCombat>();Check(!realBoss.gameObject.activeSelf && realBoss.GetComponent<Health>().BeginBlood==1400f,"NorthKing is prepared offstage at HP 1400");
        Check(FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length==1,"Level6 has exactly one enabled EventSystem");
        PlayerManager party=FindAnyObjectByType<PlayerManager>();PlayerController actualPlayer=party.CurrentPlayer;actualPlayer.GetComponent<Health>().SetBaseBlood(50000f);actualPlayer.GetComponent<Health>().Heal(50000f);
        Set(stage,"firstEnemyDelay",0.02f);Set(stage,"enemyDelay",0.02f);Set(stage,"bossBornDelay",0.02f);
        CameraManager actualCamera=Camera.main.GetComponent<CameraManager>();SerializedObject cameraData=new(actualCamera);Dictionary<EnemyMovement,int> seen=new();HashSet<int> waves=new();int spawns=0;float deadline=Time.time+20f;
        while(!Get<bool>(stage,"bossBorn") && Time.time<deadline)
        {
            int wave=Get<int>(stage,"curWave");waves.Add(wave);Transform edge=(Transform)cameraData.FindProperty("cameraEdgeLeft").GetArrayElementAtIndex(wave).objectReferenceValue;
            actualCamera.FocusBattlePractice(wave,edge.position.x+0.1f);RaycastHit2D ground=Physics2D.Raycast(new Vector2(edge.position.x+0.5f,20f),Vector2.down,60f,1<<6);
            if(ground.collider!=null)Place(actualPlayer.gameObject,edge.position.x+0.5f,ground.point.y);
            //只加速探针：仍由正式Stage出生、计数、跨波与唤醒Boss；跳过小怪死亡动画，不向档案发经验/金币。
            var active=EnemyMovement.ActiveEnemies.ToArray();foreach(EnemyMovement enemy in active)
            {
                if(enemy==null || enemy.GetComponent<NorthCombat>()!=null || enemy.GetComponent<EnemyController>()==null)continue;
                Health hp=enemy.GetComponent<Health>();if(!seen.TryGetValue(enemy,out int life) || life!=hp.LifeVersion){seen[enemy]=hp.LifeVersion;spawns++;}
                enemy.GetComponent<EnemyRewards>().SetPlayerManager(null);hp.TakeDamage(new DamageInfo(actualPlayer.transform,100000f,false,false,null,false,true));enemy.gameObject.SetActive(false);
            }
            yield return null;
        }
        Check(waves.Count==4 && spawns==43,"formal stage spawns all 43 configured enemies across four waves");
        Check(Get<bool>(stage,"bossBorn") && realBoss.gameObject.activeSelf && ui.gameObject.activeSelf,"clearing the last wave wakes NorthKing and shows its bound UI");
        Check(Near(blood.fillAmount,1f) && Near(special.fillAmount,0f),"Boss entry shows full HP and an empty guard meter");
        Check(blood.gameObject.activeInHierarchy && special.gameObject.activeInHierarchy,"Boss HP and guard fill are active through their UI hierarchy");
        Check(realBoss.Arena.x<realBoss.transform.position.x && realBoss.Arena.y>realBoss.transform.position.x,"Boss arena bounds enclose the existing grounded spawn");
        realBoss.GetComponent<EnemyRewards>().SetPlayerManager(null);realBoss.GetComponent<Health>().TakeDamage(new DamageInfo(actualPlayer.transform,100000f,false));deadline=Time.time+2f;while(realBoss.gameObject.activeSelf && Time.time<deadline)yield return null;yield return new WaitForSeconds(0.08f);
        ExitManager realExit=Get<ExitManager>(stage,"curExit");Check(!realBoss.gameObject.activeSelf && Get<bool>(stage,"bossOver") && realExit.gameObject.activeSelf,"Boss death animation completes and the formal stage opens the victory exit");
        Check(Near(blood.fillAmount,0f),"Boss blood bar remains at zero after death");
        actualPlayer.GetComponent<PlayerMovement>().SyncMoveInput(1f);deadline=Time.time+6f;while(!realExit.PlayerIn && Time.time<deadline)yield return null;actualPlayer.GetComponent<PlayerMovement>().SyncMoveInput(0f);
        Check(realExit.PlayerIn,"current character can enter the grounded exit trigger for normal interaction");

        foreach(GameObject root in gameObject.scene.GetRootGameObjects())if(root!=gameObject)root.SetActive(false);BattleRun.Begin(6,null);
        GameObject floor=new("ProbeFloor"){layer=6};floor.transform.position=Vector3.down*0.5f;floor.AddComponent<BoxCollider2D>().size=new Vector2(100f,1f);
        GameObject camRoot=new("ProbeCamera");camRoot.SetActive(false);camRoot.tag="MainCamera";Camera cam=camRoot.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=15f;cam.transform.position=new Vector3(0f,2f,-10f);CameraManager cm=camRoot.AddComponent<CameraManager>();SerializedObject cmData=new(cm);
        foreach(string f in new[]{"cameraEdgeLeft","cameraEdgeRight"}){GameObject edge=new(f);edge.transform.position=Vector3.right*(f=="cameraEdgeLeft"?-40f:40f);var a=cmData.FindProperty(f);a.arraySize=1;a.GetArrayElementAtIndex(0).objectReferenceValue=edge.transform;}cmData.ApplyModifiedPropertiesWithoutUndo();camRoot.SetActive(true);
        GameObject hero=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));PlayerController player=hero.GetComponent<PlayerController>();Health playerHealth=hero.GetComponent<Health>();playerHealth.SetBaseBlood(20000f);playerHealth.Heal(20000f);Rigidbody2D playerBody=hero.GetComponent<Rigidbody2D>();playerBody.constraints=RigidbodyConstraints2D.FreezeRotation|RigidbodyConstraints2D.FreezePositionX;Place(hero,5f);
        GameObject boss=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/NorthKing.prefab"));NorthCombat combat=boss.GetComponent<NorthCombat>();NorthController controller=boss.GetComponent<NorthController>();NorthMovement movement=boss.GetComponent<NorthMovement>();NorthEffects effects=boss.GetComponent<NorthEffects>();Health health=boss.GetComponent<Health>();ResetBoss(boss,player);NorthEffectPool pool=NorthEffectPool.GetOrCreate();
        ui.PrepareForBattle();ui.BindBoss(health);ui.SetVisible(true);
        Check(health.BeginBlood==1400f && new SerializedObject(boss.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue==240f && new SerializedObject(boss.GetComponent<EnemyRewards>()).FindProperty("isBoss").boolValue,"prefab HP1400, XP240 and isBoss are exact");
        Check(!combat.Angry && boss.GetComponents<ActorStatus>().Length==1,"Boss uses no anger and has exactly one ActorStatus");
        health.TakeDamage(new DamageInfo(hero.transform,419f,false,false,null,false,true));Check(combat.GuardStarts==0,"guard does not start above seventy percent");health.TakeDamage(new DamageInfo(hero.transform,1f,false,false,null,false,true));
        Check(combat.IsGuarding && combat.GuardStarts==1 && combat.Durability==150f && Near(combat.GuardRemaining,6f),"first seventy percent crossing starts a six-second 150-durability guard exactly once");
        Check(combat.GuardFaceRight && health.Boost && Near(special.fillAmount,1f),"guard locks its facing, grants ordinary armor and fills the durability UI");
        controller.enabled=true;yield return new WaitForSeconds(1.05f);SpriteManager manager=boss.GetComponentInChildren<SpriteManager>();Check(combat.IsGuarding && manager.IsFinished && manager.GetComponent<SpriteRenderer>().sprite.name=="Guard_16","Guard plays sixteen frames once and holds its last pose while guarding");controller.enabled=false;
        health.SetDamageMultiplier(this,0.5f);health.SetReflection(this,0.1f);float before=health.CurBlood,beforePlayer=playerHealth.CurBlood;Transform reflectedSource=null;
        Action<DamageInfo,float> reflection=(info,amount)=>{if(info.IsReflected)reflectedSource=info.Source;};playerHealth.DamageReceived+=reflection;
        health.TakeDamage(new DamageInfo(hero.transform,50f,false));Check(Near(before-health.CurBlood,2.5f) && combat.Durability==100f,"front hit takes ten percent times an independent reduction and consumes full raw umbrella durability");
        Check(Near(beforePlayer-playerHealth.CurBlood,15f) && reflectedSource==boss.transform,"Health reflection combines the Boss twenty percent source with other sources and attributes it to the Boss");
        Check(Near(special.fillAmount,100f/150f) && Near(blood.fillAmount,health.ratio),"durability and HP UI update from the bound runtime instance");
        Place(hero,-5f);before=health.CurBlood;beforePlayer=playerHealth.CurBlood;health.TakeDamage(new DamageInfo(hero.transform,20f,false));
        Check(combat.GuardFaceRight && Near(before-health.CurBlood,10f) && combat.Durability==100f,"walking behind the locked umbrella bypasses its reduction without consuming durability");
        Check(Near(beforePlayer-playerHealth.CurBlood,2f),"back hits receive only the independent reflection source");
        Place(hero,5f);before=health.CurBlood;beforePlayer=playerHealth.CurBlood;health.TakeDamage(new DamageInfo(hero.transform,100f,false));
        Check(combat.Phase==NorthCombat.GuardPhase.Exposed && combat.Durability==0f && Near(before-health.CurBlood,5f),"breaking hit still takes only ten percent before entering the three-second exposed phase");
        Check(Near(beforePlayer-playerHealth.CurBlood,30f),"breaking hit completes its Health reflection before the guard source is removed");
        controller.enabled=true;yield return null;Check(boss.GetComponentInChildren<SpriteRenderer>().sprite.name.StartsWith("Idle_") && !health.Boost,"umbrella break returns to Idle and releases its armor");
        before=health.CurBlood;beforePlayer=playerHealth.CurBlood;health.TakeDamage(new DamageInfo(hero.transform,10f,false));Check(Near(before-health.CurBlood,7.5f) && Near(beforePlayer-playerHealth.CurBlood,1f),"exposure adds fifty percent damage while retaining other reduction/reflection sources");
        float exposedAt=Time.time;while(Time.time<exposedAt+2.8f){CheckStopped();yield return null;}
        Check(combat.Phase==NorthCombat.GuardPhase.Exposed,"exposure remains for three seconds without movement or a new attack");yield return new WaitForSeconds(0.3f);controller.enabled=false;
        Check(!combat.IsSpecial && Near(special.fillAmount,0f) && combat.Durability==0f,"exposure ends and clears the durability state and UI");
        before=health.CurBlood;health.TakeDamage(new DamageInfo(hero.transform,10f,false,false,null,false,true));Check(Near(before-health.CurBlood,5f),"finishing exposure removes only the Boss vulnerability source");health.RemoveDamageMultiplier(this);health.RemoveReflection(this);
        health.Heal(1400f);health.TakeDamage(new DamageInfo(hero.transform,420f,false,false,null,false,true));yield return null;Check(combat.GuardStarts==1 && !combat.IsSpecial,"healing and recrossing seventy percent do not reset the first trigger");
        health.TakeDamage(new DamageInfo(hero.transform,420f,false,false,null,false,true));Check(combat.IsGuarding && combat.GuardStarts==2,"first forty percent crossing starts the second independent guard");
        yield return new WaitForSeconds(0.1f);float remaining=combat.GuardRemaining;int volleys=combat.GuardVolleys;health.Status.ApplyFreeze(this,2f);yield return new WaitForSeconds(0.4f);
        Check(combat.GuardTimerPaused && Near(combat.GuardRemaining,remaining) && combat.GuardVolleys==volleys,"solid freeze pauses guard duration and its two-second volley clock");health.Status.CancelFreeze(this);
        deadline=Time.time+6.3f;while(combat.IsGuarding && Time.time<deadline)yield return null;
        Check(!combat.IsSpecial && combat.GuardStarts==2 && combat.GuardVolleys==3 && effects.RibsEmitted==9,"unbroken guard emits three front-only three-rib fans at two/four/six seconds and times out without exposure");
        Check(combat.Durability==0f && !health.Boost && Near(special.fillAmount,0f),"guard timeout removes durability, shield UI and ordinary armor");before=health.CurBlood;beforePlayer=playerHealth.CurBlood;health.TakeDamage(new DamageInfo(hero.transform,10f,false));
        Check(Near(before-health.CurBlood,10f) && playerHealth.CurBlood==beforePlayer,"timeout leaves neither residual reduction nor reflection");health.Heal(1400f);health.TakeDamage(new DamageInfo(hero.transform,850f,false,false,null,false,true));yield return null;Check(combat.GuardStarts==2 && !combat.IsSpecial,"both threshold triggers remain latched for the entire life");pool.ReleaseAll();playerHealth.DamageReceived-=reflection;

        ResetBoss(boss,player);Place(hero,5f);health.TakeDamage(new DamageInfo(hero.transform,900f,false,false,null,false,true));Check(combat.GuardStarts==1 && combat.IsGuarding,"one large hit crossing both thresholds starts only the first guard");
        health.TakeDamage(new DamageInfo(hero.transform,150f,false));yield return new WaitForSeconds(3.2f);Check(combat.GuardStarts==2 && combat.IsGuarding,"second crossed threshold waits for the first three-second exposure to finish");health.TakeDamage(new DamageInfo(hero.transform,150f,false));yield return new WaitForSeconds(3.2f);Check(combat.GuardStarts==2 && !combat.IsSpecial,"queued threshold guards never overlap or repeat");
        ResetBoss(boss,player);health.TakeDamage(new DamageInfo(hero.transform,420f,false,false,null,false,true));playerHealth.SetReflection(this,1f);before=health.CurBlood;beforePlayer=playerHealth.CurBlood;health.TakeDamage(new DamageInfo(hero.transform,10f,false));Check(Near(before-health.CurBlood,1f) && Near(beforePlayer-playerHealth.CurBlood,2f),"reflected umbrella damage does not bounce through the player's reflection into a loop");playerHealth.RemoveReflection(this);pool.ReleaseAll();
        ResetBoss(boss,player);Place(hero,4f);Check(combat.ReadyAttack==0,"ordinary AI selects bash at three to five units");Place(hero,2f);Check(combat.ReadyAttack==1,"ordinary AI prioritizes vortex at close range");Place(hero,7f);Check(combat.ReadyAttack==2,"ordinary AI selects a fan at middle range");Set(combat,"latched",3);health.TakeDamage(new DamageInfo(hero.transform,900f,false,false,null,false,true));Check(combat.ReadyAttack==3,"low-health AI prioritizes rain when no special state is pending");

        ResetBoss(boss,player);Place(hero,3f);Ready(combat,0);Check(combat.BeginAttack(0),"normal bash can begin at its own cooldown");NorthBashBox bash=boss.GetComponentInChildren<NorthBashBox>(true);combat.SampleFrame(4);Check(!bash.gameObject.activeSelf,"bash remains closed before frame five");beforePlayer=playerHealth.CurBlood;combat.SampleFrame(5);Check(bash.gameObject.activeSelf && Near(beforePlayer-playerHealth.CurBlood,25f),"bash opens at frame five for one 25 damage hit");for(int i=0;i<100;i++){combat.SampleFrame(6);bash.Sample();}Check(Near(beforePlayer-playerHealth.CurBlood,25f),"bash trigger and repeated samples share one-hit deduplication");combat.SampleFrame(9);Check(!bash.gameObject.activeSelf,"bash closes at frame nine");combat.CancelAttack();Check(Near(combat.NextAttackAt-Time.time,2.2f) && !combat.BeginAttack(2),"normal recovery enforces the two-point-two-second attack interval");
        ResetBoss(boss,player);Place(hero,20f);Ready(combat,2);combat.BeginAttack(2);combat.SampleFrame(6);Check(effects.RibsEmitted==0,"volley waits for frame seven");combat.SampleFrame(7);Check(effects.RibsEmitted==5 && pool.GetComponentsInChildren<NorthKingRib>().Length==5,"volley releases five pooled straight ribs");for(int i=0;i<100;i++)combat.SampleFrame(15);Check(effects.RibsEmitted==5,"volley release is not repeated by later samples");pool.ReleaseAll();combat.CancelAttack();
        NorthKingRib ribPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/NorthKingRib.prefab").GetComponent<NorthKingRib>();Place(hero,5f);beforePlayer=playerHealth.CurBlood;NorthKingRib rib=pool.Rib(ribPrefab,combat,new Vector2(0.85f,1.5f),Vector2.right);boss.SetActive(false);yield return new WaitForSeconds(0.7f);
        Check(rib.HitPlayer && !rib.gameObject.activeSelf && Near(beforePlayer-playerHealth.CurBlood,10f),"rib hits once for ten and survives shooter deactivation");ResetBoss(boss,player);GameObject wall=new("ProbeWall"){layer=6};wall.transform.position=new Vector3(2f,1.5f);wall.AddComponent<BoxCollider2D>().size=new Vector2(0.2f,3f);Physics2D.SyncTransforms();beforePlayer=playerHealth.CurBlood;rib=pool.Rib(ribPrefab,combat,new Vector2(0.85f,1.5f),Vector2.right);yield return new WaitForSeconds(0.7f);Check(!rib.HitPlayer && playerHealth.CurBlood==beforePlayer,"rib cannot hit through solid terrain");wall.SetActive(false);
        ResetBoss(boss,player);Place(hero,25f);Force(boss,3);deadline=Time.time+0.6f;while(!movement.IsRainJumping && Time.time<deadline)yield return null;
        Check(movement.IsRainJumping && boss.GetComponent<Rigidbody2D>().linearVelocityY>0f,"rain animation frame three starts a real upward hop");deadline=Time.time+2f;while(effects.RainEmitted==0 && Time.time<deadline)yield return null;
        NorthKingRain[] rain=pool.GetComponentsInChildren<NorthKingRain>();Check(movement.RainLanded && effects.RainEmitted==6 && rain.Length==6,"actual landing releases six rain drops once");
        Check(rain.Select(r=>r.Landing.x).Distinct().Count()==6 && rain.All(r=>r.Landing.x>=-10f && r.Landing.x<=10f),"six rain landing points cover distinct supported parts of the arena");for(int i=0;i<100;i++)combat.SampleFrame(15);Check(effects.RainEmitted==6,"rain landing frame cannot repeatedly emit another six drops");controller.enabled=false;pool.ReleaseAll();
        NorthKingRain rainPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/NorthKingRain.prefab").GetComponent<NorthKingRain>();ResetBoss(boss,player);Place(hero,5f);beforePlayer=playerHealth.CurBlood;NorthKingRain drop=pool.Rain(rainPrefab,combat,new Vector2(5f,0f));yield return new WaitForSeconds(0.9f);Check(drop.Impacted && Near(beforePlayer-playerHealth.CurBlood,12f),"a rain drop deals one twelve damage ground radius-one impact");yield return new WaitForSeconds(0.4f);Check(Near(beforePlayer-playerHealth.CurBlood,12f),"lingering rain impact visuals do not repeat damage");pool.ReleaseAll();
        ResetBoss(boss,player);Place(hero,2f);boss.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation|RigidbodyConstraints2D.FreezePositionX;beforePlayer=playerHealth.CurBlood;Force(boss,1);deadline=Time.time+1.3f;while(!combat.Channeling && Time.time<deadline)yield return null;Check(combat.Channeling,"vortex begins at frame four");yield return new WaitForSeconds(1f);Check(combat.Channeling && boss.GetComponentInChildren<SpriteRenderer>().sprite.name.StartsWith("UmbrellaVortex_"),"vortex spin frames continue beyond the one-second body clip");yield return new WaitForSeconds(1.3f);Check(!combat.Channeling && Near(beforePlayer-playerHealth.CurBlood,40f),"two-second vortex contact deals five eight-damage hits with a point-four-second cooldown");controller.enabled=false;
        yield return new WaitForSeconds(0.6f);Place(hero,5f);NorthVortexPull pull=hero.GetComponent<NorthVortexPull>();if(pull==null)pull=hero.AddComponent<NorthVortexPull>();playerHealth.AttackedEnd();playerBody.linearVelocity=new Vector2(5f,8f);pull.Apply(Vector2.zero);Check(Near(playerBody.linearVelocityX,2.5f) && Near(playerBody.linearVelocityY,8f),"vortex adds a resistible pull to current movement and preserves jump velocity");
        playerBody.linearVelocity=new Vector2(5f,0f);playerHealth.Status.ApplyFreeze(this,1f);pull.Apply(Vector2.zero);Check(Near(playerBody.linearVelocityX,0f),"vortex does not force movement while the player is frozen");playerHealth.Status.CancelFreeze(this);playerBody.linearVelocityX=5f;playerHealth.TakeDamage(new DamageInfo(boss.transform,1f,false));pull.Apply(Vector2.zero);Check(Near(playerBody.linearVelocityX,5f),"vortex does not override player hurt motion");
        ResetBoss(boss,player);Ready(combat,0);combat.BeginAttack(0);float[] cooldowns=Get<float[]>(combat,"readyAt");Check(Near(cooldowns[0]-Time.time,4f),"bash cooldown is four seconds");combat.CancelAttack();Ready(combat,1);combat.BeginAttack(1);Check(Near(cooldowns[1]-Time.time,9f),"vortex cooldown is nine seconds");combat.CancelAttack();Ready(combat,2);combat.BeginAttack(2);Check(Near(cooldowns[2]-Time.time,8f),"volley cooldown is eight seconds");combat.CancelAttack();Ready(combat,3);combat.BeginAttack(3);Check(Near(cooldowns[3]-Time.time,12f),"rain cooldown is twelve seconds");combat.CancelAttack();
        ResetBoss(boss,player);health.TakeDamage(new DamageInfo(hero.transform,420f,false,false,null,false,true));health.TakeDamage(new DamageInfo(hero.transform,100000f,false,false,null,false,true));Check(health.Dead && !combat.IsSpecial && combat.Durability==0f && !health.Boost,"death during guard clears every owned shield state");ResetBoss(boss,player);Check(health.CurBlood==1400f && combat.GuardStarts==0 && combat.Durability==0f && !combat.IsSpecial,"pooled Boss reuse starts a clean new life");
        rib=pool.Rib(ribPrefab,combat,new Vector2(0.85f,1.5f),Vector2.right);drop=pool.Rain(rainPrefab,combat,new Vector2(5f,0f));BattleRun.Cancel();yield return new WaitForSeconds(0.08f);Check(!rib.gameObject.activeSelf && !drop.gameObject.activeSelf,"scene pool recycles remaining effects at battle end");
        void CheckStopped(){if(combat.CurrentSkill>=0 || Mathf.Abs(boss.GetComponent<Rigidbody2D>().linearVelocityX)>0.01f)throw new InvalidOperationException("new cast or movement inside exposure");}
    }
}
#endif

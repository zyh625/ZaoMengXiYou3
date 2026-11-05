#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//仅验收探针使用UNITY_EDITOR；场景和玩法组件全部为普通运行时代码。
public sealed class TrainingRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder result=new();
    private static string Audit=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/TrainingAudit"));
    private static string Report=>Path.Combine(Audit,"runtime-validation.txt");
    private void Awake()=>DontDestroyOnLoad(gameObject);
    private IEnumerator Start()
    {
        Directory.CreateDirectory(Audit);IEnumerator run=Scenario();while(true)
        {
            object wait;try{if(!run.MoveNext())break;wait=run.Current;}
            catch(Exception e){result.AppendLine("FAIL: "+e);File.WriteAllText(Report,result.ToString());Debug.LogError("TRAINING_RUNTIME_FAIL: "+e);EditorApplication.isPlaying=false;yield break;}
            yield return wait;
        }
        result.AppendLine("ALL PASS");File.WriteAllText(Report,result.ToString());Debug.Log("TRAINING_RUNTIME_PASS");EditorApplication.isPlaying=false;
    }
    private void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);result.AppendLine("PASS: "+message);File.WriteAllText(Report,result.ToString());}
    private static bool Near(double a,double b,double error=0.04)=>Math.Abs(a-b)<=error;
    private static void Set(object o,string field,object value){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(o,value);return;}}throw new MissingFieldException(field);}
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(0.15f);TrainingEntry entry=FindAnyObjectByType<TrainingEntry>();Check(entry!=null && entry.GetComponent<Button>().onClick.GetPersistentEventCount()==1,"MapScene has a runtime training button independent of the level unlock buttons");
        GameSession session=GameSession.EnsureSession();SaveData original=SaveData.CreatNew("TrainingFixture");original.gold=23456;original.gameLevel=6;original.GetPlayer(PlayerId.WuKong).exp=99f;foreach(PlayerData p in original.players){p.playerLevel=7;p.skillLevels=new[]{3,3,3};}
        original.Normalize();//夹具与真实SelectFile读档一样，先迁移轮回/伙伴解锁等新增字段，再冻结训练隔离快照。
        session.data=original;session.maxLevel=6;session.curFile=777;Set(session,"fileSelected",true);BattleData originalBattle=new(){curLevel=6,costTime=321,progress=72};session.battle=originalBattle;string json=JsonUtility.ToJson(original);BattleData priorResult=BattleRun.LastResult,priorCurrent=BattleRun.CurrentResult;
        string oldPath=SaveSystem.path;string saveDir=Path.Combine(Audit,"FixtureSaves");Directory.CreateDirectory(saveDir);SaveSystem.path=saveDir;string file=Path.Combine(saveDir,"save777.json");File.WriteAllText(file,json);DateTime stamp=File.GetLastWriteTimeUtc(file);
        entry.GetComponent<Button>().onClick.Invoke();float deadline=Time.realtimeSinceStartup+12f;TrainingManager training=null;
        while(Time.realtimeSinceStartup<deadline){training=FindAnyObjectByType<TrainingManager>();if(training!=null && training.Ready && training.Target!=null)break;yield return null;}yield return new WaitForSeconds(0.15f);
        Check(SceneManager.GetActiveScene().name=="TrainingScene" && training!=null && training.Ready,"runtime map button loads and initializes TrainingScene");
        Check(TrainingSession.Active && BattleRun.IsRunning && BattleRun.CurrentResult==priorCurrent && BattleRun.LastResult==priorResult,"training enables full combat effects without creating or overwriting BattleRun statistics");
        Check(training.Catalog.Count==15 && training.Catalog.enemies.All(e=>e.prefab!=null),"resource catalog contains all fifteen requested valid enemy prefabs");
        Check(training.Players.Players.Count==4 && training.Players.TrainingMode,"four characters use the existing PlayerManager with runtime training mode");
        Check(FindObjectsByType<LevelManager>().Length==0 && FindObjectsByType<TrainingHUD>().Length==1,"training scene uses its own runtime manager and HUD, with no formal level manager");
        Check(TrainingSession.SuppressedReads==0 && TrainingSession.SuppressedWrites==0,"entering training does not call save loading or saving");
        Check(!SaveSystem.TrySave() && SaveSystem.Load(777)==null,"save and load requests are refused before filesystem IO during training");SaveSystem.Delete(777);
        Check(File.Exists(file) && File.ReadAllText(file)==json && File.GetLastWriteTimeUtc(file)==stamp,"training save/delete attempts leave selected save bytes and timestamp unchanged");
        PlayerController hero=training.Players.CurrentPlayer;PlayerResources resource=hero.GetComponent<PlayerResources>();PlayerCombat combat=hero.GetComponent<PlayerCombat>();Health health=hero.GetComponent<Health>();
        Check(resource.Level==1 && resource.GetSkillLevel(1)==1 && original.GetPlayer(PlayerId.WuKong).playerLevel==7,"training uses independent learned level-one skills without reading or altering the selected player's data");
        float mana=resource.CurrentMana,vigor=resource.CurrentVigor;Check(resource.TrySpendMana(100000f) && resource.TrySpendVigor(100000f) && resource.DrainVigor(100000f) && resource.CurrentMana==mana && resource.CurrentVigor==vigor,"infinite mana and vigor accept all three spending paths without subtracting resources");
        training.SetInfiniteResources(false);Check(resource.TrySpendMana(10f) && Near(resource.CurrentMana,mana-10f) && resource.TrySpendVigor(10f) && Near(resource.CurrentVigor,vigor-10f),"turning infinite resources off restores ordinary spending");Check(!resource.TrySpendMana(100000f) && !resource.TrySpendVigor(100000f),"finite mode refuses unaffordable mana and one-shot vigor costs");training.SetInfiniteResources(true);
        training.SetNoCooldown(false);combat.Skilled=-1;combat.OnSkill(1);Check(combat.TryBeginSkill() && combat.GetSkillCooldownRemaining(1)>0f,"normal skill use starts its configured cooldown");training.SetNoCooldown(true);Check(combat.GetSkillCooldownRemaining(1)==0f,"no-cooldown switch immediately clears an existing cooldown");combat.Skilled=-1;combat.OnSkill(1);Check(combat.TryBeginSkill() && combat.GetSkillCooldownRemaining(1)==0f,"no-cooldown mode accepts another skill immediately");training.SetNoCooldown(false);combat.Skilled=-1;combat.OnSkill(1);Check(combat.TryBeginSkill() && combat.GetSkillCooldownRemaining(1)>0f,"disabling no-cooldown restores the next skill's ordinary cooldown");training.SetNoCooldown(true);training.ResetPlayer();yield return new WaitForSeconds(0.1f);
        hero=training.Players.CurrentPlayer;health=hero.GetComponent<Health>();float hp=health.CurBlood;training.SetInvulnerable(true);health.TakeDamage(new DamageInfo(training.Target.transform,17f,true));Check(health.CurBlood==hp && health.Attacked,"training invulnerability prevents HP loss while preserving the hurt/knockback request");yield return new WaitForSeconds(0.08f);Check(hero.State()==PlayerState.Attacked,"invulnerable player still enters the actual hurt animation");training.ResetPlayer();training.SetInvulnerable(false);health.TakeDamage(new DamageInfo(training.Target.transform,7f,false));Check(Near(health.CurBlood,hp-7f),"turning invulnerability off restores ordinary incoming damage");training.SetInvulnerable(true);training.ResetPlayer();yield return new WaitForSeconds(0.08f);training.SetNoCooldown(false);
        PlayerManager party=training.Players;
        var order=party.SwitchOrder.ToArray();
        PlayerId startingId=party.CurrentPlayer.GetComponent<PlayerResources>().Id;
        for(int i=0;i<order.Length;i++)
        {
            if(order[i]==startingId)continue;
            PlayerId outgoing=party.CurrentPlayer.GetComponent<PlayerResources>().Id;
            party.SwitchSlot(i);
            PlayerController current=party.CurrentPlayer;
            Check(current.GetComponent<PlayerResources>().Id==order[i],"a ready character can enter while others cool down");
            float expected=BalanceRuntime.Load("Common/PartyAndGold").Get($"PlayerManager.SwitchCooldown.{outgoing}",20f);
            expected=float.IsFinite(expected)?Mathf.Max(0f,expected):20f;
            Check(Near(party.GetSwitchCooldownRemaining(outgoing,out float ratio),expected)
                && Near(ratio,expected>0f?1f:0f),"the outgoing character starts their configured switch cooldown");
            Check(party.GetSwitchCooldownRemaining(order[i],out _)==0f,"the incoming character stays ready");
            Check(current.GetComponent<PlayerResources>().TrainingInfiniteResources && current.GetComponent<PlayerCombat>().TrainingNoCooldown && current.GetComponent<Health>().TrainingPreventHealthLoss,"training options follow the active character");
            yield return new WaitForSeconds(0.06f);
            if(party.GetSwitchCooldownRemaining(outgoing,out _)>0f)
            {
                party.SwitchSlot(System.Array.IndexOf(order,outgoing));
                Check(party.CurrentPlayer==current,"a cooling character cannot be switched back in");
            }
        }
        training.SetNoCooldown(true);
        PlayerId lastCurrent = party.CurrentPlayer.GetComponent<PlayerResources>().Id;
        PlayerId targetOther = order.First(id => id != lastCurrent);
        Check(party.GetSwitchCooldownRemaining(targetOther, out _) == 0f, "enabling no cooldown immediately clears switch cooldowns");
        party.SwitchSlot(Array.IndexOf(order, targetOther));
        Check(party.CurrentPlayer.GetComponent<PlayerResources>().Id == targetOther, "no cooldown allows switching immediately without switch cooldown");

        training.SetInfiniteBoost(true);
        for (int i = 0; i < order.Length; i++)
        {
            party.SwitchSlot(i);
            PlayerController cur = party.CurrentPlayer;
            PlayerId pid = cur.GetComponent<PlayerResources>().Id;
            PlayerBoost pb = cur.GetComponent<PlayerBoost>();
            Check(pb != null && pb.Active && pb.Energy == pb.MaxEnergy, "active character has infinite boost enabled");
            switch (pid)
            {
                case PlayerId.WuKong:
                    Check(cur.GetComponent<PlayerCombat>().CriticalChance >= 0.3f, "WuKong boost provides extra critical chance");
                    break;
                case PlayerId.BaJie:
                    Check(Near(BalanceRuntime.Number(pb, "PlayerBoost.bajieDamageMultiplier", 0.5f), 0.5f), "BaJie boost provides damage reduction");
                    break;
                case PlayerId.Sha:
                    Check(cur.GetComponent<Health>().Boost, "Sha boost provides super armor");
                    break;
                case PlayerId.Tang:
                    Check(cur.GetComponent<PlayerCombat>().AttackMultiplier >= 1.2f, "Tang boost provides attack multiplier");
                    break;
            }
        }
        training.SetInfiniteBoost(false);
        Check(!party.CurrentPlayer.GetComponent<PlayerBoost>().Active, "disabling infinite boost ends boost state");

        hero = party.CurrentPlayer; combat = hero.GetComponent<PlayerCombat>();
        training.SetMode(TrainingManager.EnemyMode.Dummy);yield return new WaitForSeconds(0.1f);Health target=training.TargetHealth;Check(training.Target.TrainingPassive && !training.Target.GetComponent<EnemyCombat>().enabled,"dummy mode pauses AI while keeping the controller enabled for hurt and death animation");
        training.Statistics.Reset();combat.SetCriticalChanceBonus(this,1f);DamageInfo critical=combat.CreateAttackDamage(10f,false);target.TakeDamage(critical);combat.RemoveCriticalChanceBonus(this);target.TakeDamage(new DamageInfo(hero.transform,15f,false));
        Check(critical.IsCritical && training.Statistics.Hits==2 && training.Statistics.CriticalHits==1 && Near(training.Statistics.TotalDamage,35f) && Near(training.Statistics.MaximumHit,20f),"damage panel counts actual critical metadata, hits, total damage and maximum hit");
        Check(Near(training.Statistics.CriticalRate,0.5f) && Near(training.Statistics.Dps,35d/3d),"critical rate and fixed trailing-three-second DPS match known inputs");
        target.TakeDamage(new DamageInfo(hero.transform,10f,false,false,null,true));target.TakeDamage(new DamageInfo(hero.transform,10f,false,false,null,false,true));target.TakeDamage(new DamageInfo(training.Target.transform,10f,false));Check(training.Statistics.Hits==2 && Near(training.Statistics.TotalDamage,35f),"reflection, periodic ticks and non-player sources are excluded from all damage metrics");
        target.TakeDamage(new DamageInfo(hero.transform,100000f,false));Check(target.CurBlood==1f && !target.Dead && training.Statistics.TotalDamage>100000f,"infinite dummy clamps HP to one and continues reporting meaningful untruncated damage");
        float x=training.Target.transform.position.x;bool facing=training.Target.GetComponentInChildren<SpriteRenderer>().flipX;target.TakeDamage(new DamageInfo(hero.transform,1f,true));yield return new WaitForSeconds(0.08f);Check(training.Target.GetComponentInChildren<SpriteManager>().CurFrame>=0 && Near(training.Target.transform.position.x,x) && training.Target.GetComponentInChildren<SpriteRenderer>().flipX==facing,"dummy hurt feedback does not move or turn the target");
        yield return new WaitForSeconds(3.1f);Check(Near(training.Statistics.Dps,0d) && training.Statistics.Hits==4,"DPS ages out after three seconds while combat totals and hit count remain");
        training.SetMode(TrainingManager.EnemyMode.Respawn);yield return new WaitForSeconds(0.1f);EnemyController instance=training.Target;int life=target.LifeVersion;target.TakeDamage(new DamageInfo(hero.transform,100000f,false));
        Check(target.Dead && training.Statistics.KillTime>=3f,"time-to-kill runs from the first player hit through target death");yield return new WaitForSeconds(1.5f);Check(target.Dead && training.Target==instance,"respawn mode keeps the same defeated instance before the two-second delay");yield return new WaitForSeconds(0.7f);
        Check(!target.Dead && target.CurBlood==target.BeginBlood && target.LifeVersion==life+1 && training.Target==instance,"two-second respawn restores full HP using the exact same pooled instance");Check(training.Statistics.Hits==0 && training.Statistics.KillTime<0f,"new target life resets its battle statistics");
        for(int i=0;i<2;i++){target.TakeDamage(new DamageInfo(hero.transform,100000f,false));yield return new WaitForSeconds(2.2f);}
        Check(training.Target==instance && FindObjectsByType<EnemyController>(FindObjectsInactive.Include).Count(e=>e.gameObject.scene==SceneManager.GetActiveScene())==1,"repeated infinite revival creates no extra enemy instances");
        target.TakeDamage(new DamageInfo(hero.transform,100000f,false));training.SetMode(TrainingManager.EnemyMode.Combat);training.SelectEnemy(1);yield return new WaitForSeconds(0.2f);Check(training.Selected==1 && training.Target.name.StartsWith("TigerEnemy") && training.Statistics.Hits==0,"switching selection during a pending revival cannot reactivate or reset the wrong target");
        for(int i=0;i<training.Catalog.Count;i++)
        {
            training.SelectEnemy(i);yield return new WaitForSeconds(0.12f);Check(training.Target!=null && training.TargetHealth.CurBlood==training.TargetHealth.BeginBlood && training.Statistics.Hits==0,"catalog enemy "+training.Catalog.enemies[i].displayName+" spawns with clean target and panel state");
        }
        Check(FindObjectsByType<EnemyController>(FindObjectsInactive.Include).Count(e=>e.gameObject.scene==SceneManager.GetActiveScene())==1,"enemy selection destroys old actors rather than accumulating inactive copies");
        TrainingHitboxDebug debug=FindAnyObjectByType<TrainingHitboxDebug>();debug.Visible=true;yield return new WaitForSeconds(0.6f);Check(debug.RegisteredCount>0 && debug.GetComponentsInChildren<LineRenderer>().Any(l=>l.enabled),"training-only hitbox/check-box drawing registers and displays collider outlines");debug.Visible=false;yield return null;Check(debug.GetComponentsInChildren<LineRenderer>().All(l=>!l.enabled),"hitbox visibility toggle hides every cached outline");
        training.ToggleAI();yield return new WaitForSeconds(0.08f);Check(training.Mode==TrainingManager.EnemyMode.Dummy && training.Target.TrainingPassive,"pause AI button enters the same passive dummy mode");training.ToggleAI();yield return new WaitForSeconds(0.08f);Check(training.Mode==TrainingManager.EnemyMode.Combat && !training.Target.TrainingPassive,"continue AI button returns to full combat mode");
        training.SetInvulnerable(false);hero=training.Players.CurrentPlayer;health=hero.GetComponent<Health>();health.TakeDamage(new DamageInfo(training.Target.transform,100000f,false));yield return new WaitForSeconds(2.2f);Check(!training.Players.CurrentPlayer.GetComponent<Health>().Dead && SceneManager.GetActiveScene().name=="TrainingScene","player death revives in training without defeat scene or settlement");
        Check(ReferenceEquals(session.data,original) && JsonUtility.ToJson(original)==json && session.battle==originalBattle && session.maxLevel==6 && session.curFile==777 && session.HasSelectedFile,"gold, experience, learned skills, selected slot and level progress remain identical after all training actions");
        Check(BattleRun.CurrentResult==priorCurrent && BattleRun.LastResult==priorResult,"enemy kills and player death never enter formal BattleRun result statistics");
        training.ExitTraining();deadline=Time.realtimeSinceStartup+8f;while(SceneManager.GetActiveScene().name!="MapScene" && Time.realtimeSinceStartup<deadline)yield return null;yield return new WaitForSeconds(0.15f);
        Check(SceneManager.GetActiveScene().name=="MapScene" && !TrainingSession.Active,"exit returns to MapScene and closes all training-only behavior");Check(JsonUtility.ToJson(original)==json && session.battle==originalBattle && File.ReadAllText(file)==json && File.GetLastWriteTimeUtc(file)==stamp,"returning to the map leaves in-memory progress and on-disk save unchanged");
        GameObject resourceProbe=new("FormalResourceDefaults");PlayerResources formal=resourceProbe.AddComponent<PlayerResources>();Check(!formal.TrainingInfiniteResources && formal.Level==7 && formal.GetSkillLevel(1)==3 && formal.TrySpendMana(10f) && Near(formal.CurrentMana,formal.MaxMana-10f),"outside training, fresh resources read the normal session and use ordinary finite costs");Destroy(resourceProbe);
        Check(FindObjectsByType<TrainingManager>().Length==0 && FindObjectsByType<TrainingHitboxDebug>().Length==0,"training manager, actors and debug drawing unload completely on exit");SaveSystem.path=oldPath;
    }
}
#endif

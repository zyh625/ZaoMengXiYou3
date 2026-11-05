#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
public sealed class BalanceRuntimeValidation:MonoBehaviour
{
    readonly StringBuilder report=new();
    void Check(bool condition,string label){report.AppendLine((condition?"PASS ":"FAIL ")+label);File.WriteAllText("output/BalanceAudit/runtime.txt",report.ToString());if(!condition)throw new Exception(label);}
    IEnumerator Start()
    {
        var steps=Run();while(true){bool more;try{more=steps.MoveNext();}catch(Exception e){report.AppendLine(e.ToString());File.WriteAllText("output/BalanceAudit/runtime.txt",report.ToString());EditorApplication.Exit(1);yield break;}if(!more)break;yield return steps.Current;}
        report.AppendLine("ALL PASS");File.WriteAllText("output/BalanceAudit/runtime.txt",report.ToString());EditorApplication.Exit(0);
    }
    static float Read(object component,string name)
    {for(Type t=component.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return Convert.ToSingle(f.GetValue(component));}throw new MissingFieldException(name);}
    static BalanceProfile.Parameter Parameter(BalanceProfile p,string type,string field)
    {foreach(var group in p.components)if(group.objectPath==""&&typeof(Health).Assembly.GetType(group.componentType) is Type t&&t.Name.Contains(type))foreach(var item in group.parameters)if(item.path==field)return item;throw new Exception("Parameter absent "+type+"/"+field);}
    IEnumerator Run()
    {
        DontDestroyOnLoad(gameObject);GameSession.EnsureSession().data.unlocked=15;
        Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/CameraRig.prefab"));
        var p=BalanceRuntime.Player(PlayerId.WuKong);var hp=Parameter(p,"Health","beginBlood");var speed=Parameter(p,"Movement","walkSpeed");var mana=Parameter(p,"Resources","maxMana");var boost=Parameter(p,"Boost","deltaBoost");
        double oldHp=hp.value,oldSpeed=speed.value,oldMana=mana.value,oldBoost=boost.value;
        BalanceProfile.Parameter hit=null;string hitPath=null;
        foreach(var group in p.components)if(group.componentType==typeof(PlayerAttackBox).FullName)foreach(var parameter in group.parameters)if(parameter.path=="Attack"&&hit==null){hit=parameter;hitPath=group.objectPath;}
        double oldHit=hit.value;hit.value=47;
        var skill=p.skills[0].levels[0];var changedSkill=skill;changedSkill.manaCost=37;changedSkill.cooldown=23;
        hp.value=321;speed.value=9.25;mana.value=222;boost.value=17;p.skills[0].levels[0]=changedSkill;
        var actor=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));actor.GetComponent<PlayerController>().enabled=false;
        try
        {
            Check(Mathf.Abs(actor.GetComponent<Health>().BeginBlood-321)<.001,"Player HP reads asset before Health initialization");
            Check(Mathf.Abs(Read(actor.GetComponent<PlayerMovement>(),"walkSpeed")-9.25f)<.001,"Player movement reads own asset");
            var common=BalanceRuntime.Load("Common/CombatRules");var shared=Array.Find(common.numbers,n=>n.key.StartsWith("PlayerMovement.",StringComparison.Ordinal));float originalShared=shared.value;
            shared.value=originalShared+2;common.Invalidate();
            Check(BalanceRuntime.Number(actor.GetComponent<PlayerMovement>(),shared.key,-1)==originalShared+2,"Derived character movement resolves shared base-class tuning");
            var movement=actor.GetComponent<PlayerMovement>();BalanceRuntime.Number(movement,shared.key,-1);
            long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();float sink=0;
            for(int i=0;i<10000;i++)sink+=BalanceRuntime.Number(movement,shared.key,-1);
            Check(GC.GetAllocatedBytesForCurrentThread()==allocatedBefore&&sink>0,"10000 warmed balance lookups allocate zero bytes");
            shared.value=originalShared;common.Invalidate();
            var attack=actor.transform.Find(hitPath).GetComponent<PlayerAttackBox>();attack.gameObject.SetActive(true);attack.ReturnBegin();
            Check(Mathf.Abs(attack.CreateDamageInfo(actor.transform).Amount-47)<.001,"Player attack box actual damage reads asset");attack.gameObject.SetActive(false);
            var resources=actor.GetComponent<PlayerResources>();Check(resources.MaxMana==222&&resources.CurrentMana==222,"Resource cap and initial mana read asset");
            Check(resources.TrySpendMana(37)&&resources.CurrentMana==185,"Actual mana spending uses configured resource state");
            actor.GetComponent<PlayerBoost>().AddEnergy();Check(actor.GetComponent<PlayerBoost>().Energy==17,"Hit energy amount reads per-player asset");
            var level=SkillProgression.Current.GetLevelData(PlayerId.WuKong,p.skills[0].skillNumber,1);Check(level.manaCost==37&&level.cooldown==23,"Skill progression reads separate player level table");
            Check(SkillProgression.Current.GetLevelData(PlayerId.Tang,1,1).manaCost!=37,"Changing Wukong skill does not alter Tang table");
            actor.GetComponent<Health>().SetBaseBlood(700);actor.SetActive(false);actor.SetActive(true);Check(actor.GetComponent<Health>().BeginBlood==700,"Pool reuse does not overwrite runtime/scaled HP");
        }
        finally{hp.value=oldHp;speed.value=oldSpeed;mana.value=oldMana;boost.value=oldBoost;hit.value=oldHit;p.skills[0].levels[0]=skill;Destroy(actor);}
        yield return null;
        var party=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/PlayerSystem.prefab")).GetComponent<PlayerManager>();
        var spawn=new GameObject("BalancePlayerSpawn");party.InitPlayer(spawn.transform);party.CurrentPlayer.enabled=false;
        var targetProfile=BalanceRuntime.Load("Enemies/GreenHammer");var reward=Parameter(targetProfile,"Rewards","experienceReward");double originalReward=reward.value;reward.value=42;
        GameObject target=null;
        try
        {
            target=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/GreenHammer.prefab"));target.GetComponent<EnemyController>().enabled=false;target.GetComponent<EnemyRewards>().SetPlayerManager(party);
            BattleRun.Begin(6,party);BattleRun.SetWave(0,1,1);var resources=party.CurrentPlayer.GetComponent<PlayerResources>();float before=resources.Experience;
            target.GetComponent<Health>().TakeDamage(new DamageInfo(party.CurrentPlayer.transform,10000,false));
            Check(Mathf.Abs(resources.Experience-before-42)<.001,"Actual enemy death awards asset-configured XP to current player");
        }
        finally{reward.value=originalReward;BattleRun.Cancel();if(target!=null)Destroy(target);}
        yield return null;
        foreach(var file in Directory.GetFiles("Assets/Projects/Prefabs/Enemy","*.prefab"))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(file);var binding=prefab.GetComponent<BalanceBinding>();Check(binding!=null,"Enemy binding "+prefab.name);
            var health=prefab.GetComponent<Health>();float expected=BalanceRuntime.BaseBlood(prefab,health.BeginBlood);
            var instance=Instantiate(prefab);instance.GetComponent<EnemyController>().enabled=false;
            Check(Mathf.Abs(instance.GetComponent<Health>().BeginBlood-expected)<.01,"Enemy actual initialized HP "+prefab.name);Destroy(instance);yield return null;
        }
        var bird=BalanceRuntime.Load("Bosses/JiuyaoXuanhuang");float old=bird.Get("JiuyaoCombat.OnFrame.8",12);BalanceProfile.Number number=Array.Find(bird.numbers,n=>n.key=="JiuyaoCombat.OnFrame.8");number.value=29;bird.Invalidate();
        Check(BalanceRuntime.Number(typeof(JiuyaoCombat),number.key,12)==29,"Hardcoded skill damage now reads boss asset");number.value=old;bird.Invalidate();
        Check(RebirthScaling.For(1).HP==1.35f&&RebirthScaling.For(10).Damage==2.5f&&RebirthScaling.For(11).HP==RebirthScaling.For(10).HP,"Default rebirth multipliers and cap preserved");
        Check(RebirthScaling.For(1).EnemyCount(24)==26&&RebirthScaling.For(10).EnemyCount(24)==44,"Quantity rounding preserved after data extraction");
    }
}
#endif

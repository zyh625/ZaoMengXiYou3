#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LateStagesRuntimeValidation : MonoBehaviour
{
    private readonly System.Text.StringBuilder report=new();
    private PlayerManager players;private PlayerController player;private Health playerHP;
    private void Require(bool condition,string label)
    {report.AppendLine((condition?"PASS ":"FAIL ")+label);File.WriteAllText("output/LateStagesAudit/runtime.txt",report.ToString());if(!condition){EditorApplication.Exit(1);throw new Exception(label);}}
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        var steps=Run();while(true){bool next;try{next=steps.MoveNext();}catch(Exception e){report.AppendLine(e.ToString());File.WriteAllText("output/LateStagesAudit/runtime.txt",report.ToString());EditorApplication.Exit(1);yield break;}if(!next)break;yield return steps.Current;}
        report.AppendLine("ALL PASS");File.WriteAllText("output/LateStagesAudit/runtime.txt",report.ToString());EditorApplication.Exit(0);
    }
    private T Spawn<T>(string name) where T:LateStageCombat
    {
        var creation=new GameObject("Creation");creation.SetActive(false);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/"+name+".prefab");
        var root=Instantiate(prefab,creation.transform);root.SetActive(false);root.GetComponent<EnemyController>().enabled=false;
        root.transform.SetParent(null);root.transform.position=new Vector2(0,.03f);root.SetActive(true);
        var c=root.GetComponent<T>();c.BindTarget(player);c.SetArenaBounds(-12,12);Destroy(creation);return c;
    }
    private void Place(float x,float y=.03f){player.transform.position=new Vector2(x,y);player.GetComponent<Rigidbody2D>().position=new Vector2(x,y);player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    private void ClearHit(LateStageCombat c)=>c.GetComponent<Health>().AttackedEnd();
    private IEnumerator Run()
    {
        TrainingSession.Begin();Time.timeScale=10;
        var ground=new GameObject("QA ground"){layer=6};ground.transform.position=new Vector2(0,-.5f);ground.AddComponent<BoxCollider2D>().size=new Vector2(100,1);
        Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/CameraRig.prefab"));
        var system=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/PlayerSystem.prefab"));
        players=system.GetComponent<PlayerManager>();players.TrainingMode=true;var spawn=new GameObject("Spawn").transform;spawn.position=new Vector2(1.1f,.03f);players.InitPlayer(spawn);
        player=players.CurrentPlayer;player.enabled=false;playerHP=player.GetComponent<Health>();playerHP.SetBaseBlood(100000);playerHP.Heal(100000);yield return null;
        var rift=Spawn<RiftBladeCombat>("RiftBlade");Place(1.1f);yield return new WaitForSeconds(.05f);
        Require(rift.GetComponent<Health>().BeginBlood==143,"Rift baseline HP143");
        Require(rift.BeginSkill(0),"Rift first cast starts");float before=playerHP.CurBlood;
        rift.SampleFrame(5);rift.SampleFrame(6);
        Require(Mathf.Abs(before-playerHP.CurBlood-10)<.05f,"Rift first window exactly10 once");
        rift.SampleFrame(8);yield return new WaitForSeconds(.27f);rift.SampleFrame(11);rift.SampleFrame(12);
        Require(Mathf.Abs(before-playerHP.CurBlood-22)<.05f,"Rift two windows exactly10+12 once each");rift.FinishSkill();
        yield return new WaitForSeconds(.4f);Place(2.5f);before=playerHP.CurBlood;Require(rift.BeginSkill(1),"Rift seam starts");rift.SampleFrame(5);
        yield return new WaitForSeconds(.8f);Require(rift.SeamResolutions==1&&Mathf.Abs(before-playerHP.CurBlood-18)<.05f,"Seam resolves once for18");rift.FinishSkill();
        yield return new WaitForSeconds(.4f);Require(rift.BeginSkill(2),"Rift anchors start");var old=rift.transform.position;
        rift.GetComponent<Health>().TakeDamage(new DamageInfo(player.transform,1,false));yield return new WaitForSeconds(.1f);rift.SampleFrame(16);
        Require(Vector2.Distance(old,rift.transform.position)<.05f&&rift.Skill<0,"Anchor windup interruptible");rift.gameObject.SetActive(false);
        var seal=Spawn<SealWardenCombat>("SealWarden");Place(8);yield return new WaitForSeconds(.05f);
        Require(seal.GetComponent<Health>().BeginBlood==176,"Seal HP176");Require(seal.BeginSkill(1),"Seal deploy starts");yield return new WaitForSeconds(.85f);seal.SampleFrame(14);
        Require(seal.HasWedges,"Two attackable wedges deployed");seal.FinishSkill();yield return new WaitForSeconds(.4f);
        Require(seal.BeginSkill(2),"Compression requires pair");
        var wedge=FindAnyObjectByType<LateStageDestructible>();Require(wedge!=null&&wedge.Health.BeginBlood==60,"Wedge has independent60HP");
        wedge.Health.TakeDamage(new DamageInfo(player.transform,100,false));yield return null;
        Require(!seal.HasWedges&&seal.Recovering&&seal.Skill<0,"Breaking wedge cancels compression and staggers");seal.gameObject.SetActive(false);
        seal=Spawn<SealWardenCombat>("SealWarden");Place(3);yield return new WaitForSeconds(.1f);
        Require(seal.BeginSkill(1),"Fresh seal deploys for normal compression");yield return new WaitForSeconds(.85f);seal.SampleFrame(14);seal.FinishSkill();yield return new WaitForSeconds(.4f);
        Require(seal.BeginSkill(2),"Normal compression starts");before=playerHP.CurBlood;yield return new WaitForSeconds(1.1f);seal.SampleFrame(16);seal.SampleFrame(16);
        Require(seal.CompressionCount==1&&Mathf.Abs(before-playerHP.CurBlood-14)<.05f,"Compression converges and resolves14 exactly once");seal.gameObject.SetActive(false);
        seal=Spawn<SealWardenCombat>("SealWarden");Place(8);yield return new WaitForSeconds(.1f);
        Require(seal.BeginSkill(1),"Fresh seal deploys for jump test");yield return new WaitForSeconds(.85f);seal.SampleFrame(14);seal.FinishSkill();yield return new WaitForSeconds(.4f);
        Require(seal.BeginSkill(2),"Jump-cancel compression starts");Place(3,2.5f);seal.SampleFrame(8);
        Require(seal.Skill<0&&seal.Recovering&&seal.CompressionCount==0,"Jump cancels and staggers without damage event");seal.gameObject.SetActive(false);
        var phase=Spawn<PhaseWeaverCombat>("PhaseWeaver");Place(4);yield return new WaitForSeconds(.1f);
        Require(phase.GetComponent<Health>().BeginBlood==104&&phase.GetComponent<Rigidbody2D>().gravityScale==0,"Phase HP104 zero gravity");
        before=playerHP.CurBlood;Require(phase.BeginSkill(0),"Low phase starts");yield return new WaitForSeconds(.8f);
        Require(phase.PhaseHits==1&&Mathf.Abs(before-playerHP.CurBlood-14)<.05f,"Low fixed rectangle resolves14 once");phase.FinishSkill();
        yield return new WaitForSeconds(.4f);before=playerHP.CurBlood;Require(phase.BeginSkill(2),"Invert starts");yield return new WaitForSeconds(1.7f);
        Require(phase.PhaseHits==3,"Invert exactly two independent layer events");phase.FinishSkill();
        yield return new WaitForSeconds(.4f);var ally=Spawn<RiftBladeCombat>("RiftBlade");ally.GetComponent<Rigidbody2D>().position=new Vector2(-4,.03f);Place(9);
        float allyBlood=ally.GetComponent<Health>().CurBlood;int life=ally.GetComponent<Health>().LifeVersion;
        Require(phase.BeginSkill(3),"Exchange starts with an existing ally");yield return new WaitForSeconds(1.1f);phase.SampleFrame(16);
        Require(phase.Exchanges==1&&ally.GetComponent<Health>().CurBlood==allyBlood&&ally.GetComponent<Health>().LifeVersion==life,"Exchange moves existing unit without heal/respawn");
        phase.gameObject.SetActive(false);ally.gameObject.SetActive(false);
        var bird=Spawn<JiuyaoCombat>("JiuyaoXuanhuang");Place(8);yield return new WaitForSeconds(.1f);var birdHP=bird.GetComponent<Health>();
        Require(birdHP.BeginBlood==1490,"Jiuyao HP1490");
        Place(3,.92f);before=playerHP.CurBlood;Require(bird.BeginSkill(1),"Feather fan starts");bird.SampleFrame(7);
        var shotList=(IList)typeof(LateStageCombat).GetField("shots",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bird);
        Require(shotList.Count==4,"Feather fan emits exactly four projectiles");yield return new WaitForSeconds(2.2f);
        float featherLoss=before-playerHP.CurBlood;Require(featherLoss>0&&featherLoss<=48&&Mathf.Abs(featherLoss/12-Mathf.Round(featherLoss/12))<.01f,"Fan hits use12 damage and each shot retires");bird.FinishSkill();
        yield return new WaitForSeconds(2.1f);Place(4,2.8f);before=playerHP.CurBlood;Require(bird.BeginSkill(2),"Low tail sweep starts");bird.SampleFrame(7);
        Require(playerHP.CurBlood==before,"Jumping avoids low tail sweep");Place(4,.92f);bird.SampleFrame(8);bird.SampleFrame(9);
        Require(Mathf.Abs(before-playerHP.CurBlood-22)<.05f,"Grounded tail sweep deals22 once");bird.FinishSkill();yield return new WaitForSeconds(2.1f);
        Place(8,.92f);Require(bird.BeginSkill(0),"Sky dash starts");float dashFrom=bird.transform.position.x;bird.SampleFrame(5);yield return new WaitForSeconds(.5f);bird.SampleFrame(12);
        Require(bird.transform.position.x>dashFrom+.5f&&bird.transform.position.x<12,"Sky dash moves and stays within arena");bird.FinishSkill();yield return new WaitForSeconds(2.1f);Place(8,.92f);before=playerHP.CurBlood;
        Require(bird.BeginSkill(3),"Star dive starts");bird.SampleFrame(3);bird.SampleFrame(10);bird.SampleFrame(11);
        Require(Mathf.Abs(before-playerHP.CurBlood-30)<.05f&&bird.transform.Find("Attack3").gameObject.activeSelf,"Dive box opens at landing and deals30 once");bird.SampleFrame(13);
        Require(!bird.transform.Find("Attack3").gameObject.activeSelf,"Dive box closes after frame12");bird.FinishSkill();
        birdHP.TakeDamage(new DamageInfo(player.transform,450,false));ClearHit(bird);yield return new WaitForSeconds(.1f);
        Require(bird.AscendStarts==1&&bird.SpecialActive,"70-percent ascend triggers once");before=birdHP.CurBlood;birdHP.TakeDamage(new DamageInfo(player.transform,100,false));
        Require(Mathf.Abs(before-birdHP.CurBlood-50)<.05f,"Ascend50-percent damage reduction");ClearHit(bird);
        float fraction=bird.SpecialFraction;Require(birdHP.Status.ApplyFreeze(this,10),"Boss accepts strong freeze in air");yield return new WaitForSeconds(.7f);
        Require(Mathf.Abs(fraction-bird.SpecialFraction)<.03f,"Strong freeze pauses ascend timer");birdHP.Status.CancelFreeze(this);
        yield return new WaitForSeconds(8.2f);Require(bird.RainVolleys==3&&bird.AscendLandings==1&&bird.SpecialActive,"8-second form: 3 rain volleys, one landing, punish state");
        before=birdHP.CurBlood;birdHP.TakeDamage(new DamageInfo(player.transform,10,false));Require(Mathf.Abs(before-birdHP.CurBlood-15)<.05f,"Landing50-percent vulnerability");
        yield return new WaitForSeconds(2.6f);Require(!bird.SpecialActive,"Landing recovery ends after2.5seconds");
        birdHP.TakeDamage(new DamageInfo(player.transform,birdHP.CurBlood-580,false));ClearHit(bird);yield return new WaitForSeconds(.1f);
        Require(bird.AscendStarts==2,"40-percent second ascend exactly once");yield return new WaitForSeconds(11);
        Require(bird.AscendStarts==2&&!bird.SpecialActive&&bird.AscendLandings==2,"Two thresholds finish without retrigger");bird.gameObject.SetActive(false);
        var emperor=Spawn<WuxiangCombat>("WuxiangDijun");Place(11);yield return new WaitForSeconds(.1f);var eh=emperor.GetComponent<Health>();
        Require(eh.BeginBlood==1670,"Wuxiang HP1670 unchanged");eh.TakeDamage(new DamageInfo(player.transform,510,false));ClearHit(emperor);yield return new WaitForSeconds(1.2f);
        Require(emperor.LockStarts==1&&Mathf.Abs(emperor.EffectiveRight-emperor.EffectiveLeft-18)<.1f,"70-percent lock narrows total width25percent");
        Require(player.transform.position.x<emperor.EffectiveRight,"Shrinking wall leaves player inside");
        eh.TakeDamage(new DamageInfo(player.transform,eh.CurBlood-800,false));ClearHit(emperor);yield return new WaitForSeconds(.1f);before=eh.CurBlood;eh.TakeDamage(new DamageInfo(player.transform,100,false));
        Require(emperor.MaskBreaks==1&&emperor.SecondStage&&eh.CurBlood==before,"50-percent mask break grants temporary invincibility");
        yield return new WaitForSeconds(2.2f);Require(!eh.IsInvincible,"Mask invincibility source removed after2seconds");
        Place(4,.92f);before=playerHP.CurBlood;Require(emperor.BeginSkill(4),"Phase2 mirror starts");yield return new WaitForSeconds(.9f);
        Require(Mathf.Abs(before-playerHP.CurBlood-14)<.05f,"Mirror copies previous slam at50percent damage");
        var image=FindAnyObjectByType<LateStageDestructible>();Require(image!=null&&image.Health.BeginBlood==1,"Mirror has one-hit health");image.Health.TakeDamage(new DamageInfo(player.transform,1,false));
        Require(!image.gameObject.activeSelf,"One hit breaks mirror");emperor.FinishSkill();

        eh.TakeDamage(new DamageInfo(player.transform,eh.CurBlood-300,false));ClearHit(emperor);yield return new WaitForSeconds(2.5f);
        Require(emperor.FinalBursts==1&&emperor.FinalRingHits==3,"20-percent final: three annuli exactly once");
        yield return new WaitForSeconds(21);Require(Mathf.Abs(emperor.EffectiveRight-emperor.EffectiveLeft-18)<.1f,"Second-stage25percent arena reduction remains permanent");
        eh.TakeDamage(new DamageInfo(player.transform,10000,false));Require(eh.Dead,"Final boss dies through normal Health pipeline");
        emperor.gameObject.SetActive(false);Time.timeScale=1;system.SetActive(false);TrainingSession.End();
        // All save IO is redirected to an isolated QA fixture directory.
        SaveSystem.path=Path.GetFullPath("output/LateStagesAudit/FixtureSaves/"+Guid.NewGuid().ToString("N"));var session=GameSession.EnsureSession();session.SelectFile(83,"LateStagesQA");
        session.data.gold=54321;session.data.gameLevel=9;session.data.unlocked=15;session.data.GetPlayer(PlayerId.Tang).playerLevel=12;
        var result=new BattleData{curLevel=6,victory=true};session.battle=result;session.RegisterVictory(result);Require(!session.CanRebirth,"Level6 no longer grants rebirth");
        result=new BattleData{curLevel=9,victory=false};session.battle=result;session.RegisterVictory(result);Require(!session.CanRebirth,"Level9 defeat does not grant rebirth");
        result=new BattleData{curLevel=9,victory=true};session.battle=result;session.RegisterVictory(result);Require(session.CanRebirth,"Level9 victory grants pending rebirth");Require(SaveSystem.TrySave(),"Deferred rebirth qualification saved");
        session.SelectFile(83,"LateStagesQA");Require(session.CanRebirth&&session.data.gold==54321,"Qualification survives reload with gold intact");
        var map=SceneManager.CreateScene("MapScene");SceneManager.SetActiveScene(map);
        Require(session.TryBeginRebirthFromMap(),"Map can consume deferred qualification");
        Require(session.Rebirth==1&&!session.CanRebirth&&session.battle.curLevel==1&&session.data.gameLevel==9&&session.data.gold==54321&&session.data.GetPlayer(PlayerId.Tang).playerLevel==12,"Rebirth preserves abilities/gold/unlocks and resets only battle and eligibility");
        Require(!session.TryBeginRebirthFromMap(),"Double click cannot consume one qualification twice");
        session.battle=new BattleData{curLevel=9,victory=true};session.RegisterVictory(session.battle);string valid=SaveSystem.path;Directory.CreateDirectory(valid);string blocked=Path.Combine(valid,"blocked-directory");File.WriteAllText(blocked,"fixture");SaveSystem.path=blocked;
        Require(!session.TryBeginRebirthFromMap()&&session.CanRebirth&&session.Rebirth==1,"Save failure rolls back count and preserves qualification");SaveSystem.path=valid;
        var oldSave=JsonUtility.FromJson<SaveData>("{\"gameLevel\":9,\"gold\":42}");oldSave.Normalize();Require(!oldSave.rebirthAvailable&&oldSave.rebirth==0&&oldSave.gold==42,"Old JSON is compatible and does not invent final completion");
        foreach(int level in new[]{8,9})
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Projects/Scenes/Levels/Level"+level+"Scene.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return new WaitForSeconds(.15f);
            var stage=FindAnyObjectByType<StageManager>();Require(stage!=null&&stage.IsInitialized,"Actual Level"+level+" initializes player, pools, UI and exit");
            var boss=stage.EnemyManager.CurBoss;Require(boss!=null,"Actual Level"+level+" creates configured boss");
            var health=boss.GetComponent<Health>();Require(Mathf.Abs(health.BeginBlood-(level==8?1490:1670)*RebirthScaling.Current.HP)<.1f,"Actual boss HP follows current rebirth multiplier");
            var boundUI=(BossUI)typeof(LevelManager).GetField("bossUI",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage);
            Require(boundUI!=null&&typeof(BossUI).GetField("bossHealth",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(boundUI)==(object)health,"Actual boss UI bound to actual boss Health");
            typeof(LevelManager).GetField("bossBorn",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(stage,true);
            boss.gameObject.SetActive(true);health.TakeDamage(new DamageInfo(FindAnyObjectByType<PlayerManager>().CurrentPlayer.transform,health.BeginBlood*10,false));
            yield return new WaitForSeconds(1.6f);
            var exit=(ExitManager)typeof(LevelManager).GetField("curExit",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage);
            Require(!boss.gameObject.activeSelf&&exit.gameObject.activeSelf,"Actual Level"+level+" boss death finishes clip and opens exit");
        }
        session.battle=new BattleData{curLevel=9,victory=true};
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Projects/Scenes/VictoryScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;yield return null;
        var victory=FindAnyObjectByType<VictoryControl>();Require(victory!=null&&victory.CanRebirth&&session.CanRebirth,"Actual Victory page offers rebirth after L9");
        victory.BackToMap();yield return null;yield return null;
        var mapControl=FindAnyObjectByType<Map>();Require(mapControl!=null&&mapControl.CanRebirth,"Actual map retains deferred eligibility");
        var entryObject=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/UI/RebirthEntry.prefab"),FindAnyObjectByType<Canvas>().transform);
        var button=entryObject.GetComponent<UnityEngine.UI.Button>();Require(button!=null&&button.interactable,"Unplaced button prefab works when dropped into map Canvas");
        int oldRebirth=session.Rebirth;button.onClick.Invoke();yield return null;yield return null;
        Require(SceneManager.GetActiveScene().name=="Level1Scene"&&session.Rebirth==oldRebirth+1&&!session.CanRebirth,"Actual map button saves and loads L1 once");

    }
}
#endif

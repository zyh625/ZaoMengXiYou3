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

//只在编辑器 Play Mode 存在的验收探针，不写玩家存档或场景资产。
public sealed class RedBoomRuntimeValidation : MonoBehaviour
{
    private readonly StringBuilder result=new();
    private IEnumerator Start()
    {
        IEnumerator run=Scenario();
        while(true)
        {
            object wait;
            try { if(!run.MoveNext())break;wait=run.Current; }
            catch(Exception exception)
            {
                result.AppendLine("FAIL: "+exception);File.WriteAllText(RedBoomValidationPaths.FilePath("runtime-validation.txt"),result.ToString());
                Debug.LogError("REDBOOM_RUNTIME_FAIL: "+exception);EditorApplication.isPlaying=false;yield break;
            }
            yield return wait;
        }
        result.AppendLine("ALL PASS");File.WriteAllText(RedBoomValidationPaths.FilePath("runtime-validation.txt"),result.ToString());
        Debug.Log("REDBOOM_RUNTIME_PASS");EditorApplication.isPlaying=false;
    }
    private void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);result.AppendLine("PASS: "+name);}
    private static void Edit(Object o,string n,Action<SerializedProperty> write)
    {SerializedObject so=new(o);write(so.FindProperty(n));so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Place(GameObject actor,float x)
    {
        BoxCollider2D shape=actor.GetComponent<BoxCollider2D>();float feet=(shape.offset.y-shape.size.y/2f)*actor.transform.lossyScale.y;
        actor.transform.position=new Vector3(x,0.025f-feet,0f);Rigidbody2D body=actor.GetComponent<Rigidbody2D>();body.position=actor.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    private static void ResetEnemy(GameObject enemy,PlayerController player)
    {enemy.SetActive(false);enemy.SetActive(true);enemy.GetComponent<RedBoomCombat>().BindTarget(player);enemy.GetComponent<EnemyMovement>().SetTarget(player.transform);Place(enemy,0f);}
    private static bool Near(float a,float b,float tolerance=0.01f)=>Mathf.Abs(a-b)<=tolerance;
    private IEnumerator Scenario()
    {
        yield return new WaitForSeconds(0.3f);
        LevelManager stage=FindAnyObjectByType<LevelManager>();
        Check(stage!=null && stage.IsInitialized,"Level5 real scene initializes");
        Check(FindObjectsByType<RedBoomController>(FindObjectsInactive.Include).Length==10,"Level5 prewarms 10 RedBoom enemies");
        Check(FindObjectsByType<RedChainController>(FindObjectsInactive.Include).Length==10,"existing RedChain pool is preserved");
        foreach(GameObject root in gameObject.scene.GetRootGameObjects())if(root!=gameObject)root.SetActive(false);

        GameObject floor=new("ValidationFloor"){layer=6};BoxCollider2D floorBox=floor.AddComponent<BoxCollider2D>();floorBox.size=new Vector2(100f,1f);floor.transform.position=new Vector3(0f,-0.5f);
        GameObject cameraRoot=new("ValidationCamera");cameraRoot.SetActive(false);cameraRoot.tag="MainCamera";
        Camera camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=15f;camera.transform.position=new Vector3(0f,2f,-10f);
        CameraManager cm=cameraRoot.AddComponent<CameraManager>();GameObject left=new("Left"),right=new("Right");left.transform.position=Vector3.left*40f;right.transform.position=Vector3.right*40f;
        Edit(cm,"cameraEdgeLeft",p=>{p.arraySize=1;p.GetArrayElementAtIndex(0).objectReferenceValue=left.transform;});
        Edit(cm,"cameraEdgeRight",p=>{p.arraySize=1;p.GetArrayElementAtIndex(0).objectReferenceValue=right.transform;});cameraRoot.SetActive(true);
        GameObject player=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));Place(player,5f);
        PlayerController hero=player.GetComponent<PlayerController>();Health hp=player.GetComponent<Health>();
        GameObject enemy=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/RedBoom.prefab"));
        RedBoomController controller=enemy.GetComponent<RedBoomController>();controller.enabled=false;
        RedBoomCombat combat=enemy.GetComponent<RedBoomCombat>();RedBoomMovement movement=enemy.GetComponent<RedBoomMovement>();Health enemyHealth=enemy.GetComponent<Health>();
        combat.BindTarget(hero);movement.SetTarget(hero.transform);Place(enemy,0f);
        RedBoomEffectPool pool=RedBoomEffectPool.GetOrCreate();
        RedBoomMark markPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/RedBoomMark.prefab").GetComponent<RedBoomMark>();
        RedBoomLantern lanternPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/RedBoomLantern.prefab").GetComponent<RedBoomLantern>();
        SpriteRenderer source=enemy.GetComponentInChildren<SpriteRenderer>();
        yield return new WaitForSeconds(0.15f);
        Check(enemyHealth.BeginBlood==80f,"HP exactly 80");
        Check(combat.ReadySkill==2,"unmarked target prioritizes mark");
        RedBoomMark priority=pool.Mark(markPrefab,hp,source);Check(priority!=null && combat.ReadySkill==0,"marked target prioritizes ray");
        Check(combat.BeginCast(0),"ray can begin");combat.CancelCast();Check(combat.ReadySkill==1,"lantern follows cooling down ray");
        Check(combat.BeginCast(1),"lantern can begin");combat.CancelCast();Check(combat.ReadySkill==-1,"cooling down skills are not restarted");priority.Cancel();

        GameObject reducer=new("ReductionSource");hp.SetDamageMultiplier(reducer,0.5f);
        float before=hp.CurBlood;hp.TakeDamage(new DamageInfo(enemy.transform,10f,false,false,null,false,true));
        Check(Near(before-hp.CurBlood,5f),"existing 0.5 reduction keeps its previous meaning");
        RedBoomMark first=pool.Mark(markPrefab,hp,source),second=pool.Mark(markPrefab,hp,source);
        before=hp.CurBlood;hp.TakeDamage(new DamageInfo(enemy.transform,10f,false,false,null,false,true));
        Check(Near(before-hp.CurBlood,7.2f),"two independent marks multiply with existing reduction");
        Func<DamageInfo,bool> shield=damage=>true;hp.DamageBlocked+=shield;before=hp.CurBlood;
        hp.TakeDamage(new DamageInfo(enemy.transform,10f));Check(hp.CurBlood==before,"shield still blocks damage before multipliers");hp.DamageBlocked-=shield;
        hp.RemoveDamageMultiplier(reducer);first.Cancel();before=hp.CurBlood;hp.TakeDamage(new DamageInfo(enemy.transform,10f,false,false,null,false,true));
        Check(Near(before-hp.CurBlood,12f),"removing one mark preserves the other source");second.Cancel();
        RedBoomMark timed=pool.Mark(markPrefab,hp,source);
        yield return new WaitForSeconds(4.1f);
        before=hp.CurBlood;hp.TakeDamage(new DamageInfo(enemy.transform,10f,false,false,null,false,true));
        Check(!timed.gameObject.activeSelf && !RedBoomMark.IsMarked(hp) && Near(before-hp.CurBlood,10f),"mark always removes its multiplier after 4 seconds");
        hp.Heal(1000f);RedBoomMark switched=pool.Mark(markPrefab,hp,source);player.SetActive(false);
        GameObject replacement=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/Bajie.prefab"));Place(replacement,5f);
        hero=replacement.GetComponent<PlayerController>();hp=replacement.GetComponent<Health>();combat.BindTarget(hero);movement.SetTarget(hero.transform);
        before=hp.CurBlood;hp.TakeDamage(new DamageInfo(enemy.transform,10f,false,false,null,false,true));
        Check(!switched.gameObject.activeSelf && !RedBoomMark.IsMarked(hp) && Near(before-hp.CurBlood,10f),"switching clears old mark without transferring it");hp.Heal(1000f);

        ResetEnemy(enemy,hero);Place(replacement,5f);yield return new WaitForSeconds(0.15f);
        Check(combat.BeginCast(0),"ray starts fresh warning");RedBoomRay ray=combat.Warning;before=hp.CurBlood;float castAt=Time.time;
        while(Time.time<castAt+0.9f)
        {
            combat.SampleFrame(15);
            if(combat.RayShots!=0 || hp.CurBlood!=before)throw new InvalidOperationException("Ray fired during 0.9 second warning");
            yield return null;
        }
        Check(ray!=null && ray.IsWarning,"red warning remains visible until release");combat.SampleFrame(15);
        Check(combat.RayShots==1 && Near(before-hp.CurBlood,12f),"ray releases once for exactly 12 damage");
        for(int i=0;i<100;i++)combat.SampleFrame(15);
        Check(combat.RayShots==1 && Near(before-hp.CurBlood,12f),"repeated release frames cannot repeat ray damage");
        enemy.SetActive(false);yield return new WaitForSeconds(2.1f);
        Check(Near(before-hp.CurBlood,18f),"ray burn adds exactly 6 damage over 2 seconds after shooter despawns");
        hp.Heal(1000f);ResetEnemy(enemy,hero);Check(combat.BeginCast(0),"second warning starts");ray=combat.Warning;
        enemyHealth.TakeDamage(new DamageInfo(hero.transform,1f,false));combat.SampleFrame(15);
        Check(combat.CurrentSkill==-1 && !ray.gameObject.activeSelf,"ordinary hurt cancels uncommitted warning");

        ResetEnemy(enemy,hero);Place(replacement,3.5f);controller.enabled=true;yield return new WaitForSeconds(0.2f);
        Check(enemy.GetComponent<Rigidbody2D>().linearVelocityX< -0.7f,"range AI retreats below 4 units");
        Place(replacement,9f);yield return new WaitForSeconds(0.1f);
        Check(enemy.GetComponent<Rigidbody2D>().linearVelocityX>0.7f,"range AI approaches above 7 units");
        Place(replacement,5f);yield return new WaitForSeconds(0.1f);
        Check(Mathf.Abs(enemy.GetComponent<Rigidbody2D>().linearVelocityX)<0.01f,"range AI stops within 4 to 7 units");controller.enabled=false;

        ResetEnemy(enemy,hero);Place(replacement,2f);movement.SetBlinkBounds(-30f,30f);yield return new WaitForSeconds(0.2f);
        Vector2 origin=enemy.GetComponent<Rigidbody2D>().position;before=hp.CurBlood;Check(movement.TryBlink(2f),"close target triggers safe blink");
        Check(Near(enemy.GetComponent<Rigidbody2D>().position.x,origin.x-4f) && Near(enemy.GetComponent<Rigidbody2D>().position.y,origin.y),
            $"blink travels exactly 4 units on the same horizontal line (from={origin}, body={enemy.GetComponent<Rigidbody2D>().position}, visual={enemy.transform.position})");
        Check(Near(movement.BlinkReadyAt-Time.time,6f,0.05f) && !movement.TryBlink(2f) && hp.CurBlood==before,"blink has 6 second cooldown and no damage");
        ResetEnemy(enemy,hero);Place(replacement,2f);yield return new WaitForSeconds(0.2f);
        GameObject wall=new("ValidationWall"){layer=6};wall.transform.position=new Vector3(-2f,2f);BoxCollider2D wallBox=wall.AddComponent<BoxCollider2D>();wallBox.size=new Vector2(0.3f,4f);Physics2D.SyncTransforms();
        Check(!movement.TryBlink(2f) && Near(enemy.transform.position.x,0f),"blink cannot cross or enter a wall");wall.SetActive(false);
        GameObject blocker=new("OtherEnemyBody"){layer=11};blocker.transform.position=new Vector3(-4f,1f);blocker.AddComponent<BoxCollider2D>().size=new Vector2(1f,2f);Physics2D.SyncTransforms();
        Check(!movement.TryBlink(2f),"blink cannot land in another enemy");blocker.SetActive(false);
        movement.SetBlinkBounds(-2f,2f);Check(!movement.TryBlink(2f),"blink cannot leave arena bounds");movement.SetBlinkBounds(-30f,30f);
        floorBox.size=new Vector2(3f,1f);Physics2D.SyncTransforms();Check(!movement.TryBlink(2f),"blink rejects unsupported landing over a ledge");floorBox.size=new Vector2(100f,1f);Physics2D.SyncTransforms();

        hp.Heal(1000f);ResetEnemy(enemy,hero);Place(replacement,5f);yield return new WaitForSeconds(0.2f);
        before=hp.CurBlood;RedBoomLantern lantern=pool.Throw(lanternPrefab,combat,new Vector2(0.65f,1.2f),hero);
        Check(lantern!=null,"lantern is obtained from independent pool");enemyHealth.TakeDamage(new DamageInfo(hero.transform,1000f));
        float peak=lantern.transform.position.y,deadline=Time.time+3f;
        while(!lantern.HasLanded && Time.time<deadline){peak=Mathf.Max(peak,lantern.transform.position.y);yield return null;}
        Check(lantern.HasLanded && peak>1.7f && hp.CurBlood==before,"lantern follows an arc and does not damage during flight");
        deadline=Time.time+2f;
        while(!lantern.HasExploded && Time.time<deadline)
        {
            if(hp.CurBlood!=before)throw new InvalidOperationException("Damage before lantern explosion");
            yield return null;
        }
        Check(enemyHealth.Dead && lantern.HasExploded && Near(lantern.ExplodedAt-lantern.LandedAt,1f,0.06f),"lantern waits 1 second after landing and survives shooter death");
        Check(Near(before-hp.CurBlood,18f),"lantern explosion deals exactly 18 damage once");
        Action step=(Action)Delegate.CreateDelegate(typeof(Action),lantern,typeof(RedBoomLantern).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic));
        for(int i=0;i<10;i++)step();
        long bytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)step();long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Check(allocated==0,"1000 fire-zone updates between ticks allocate 0 managed bytes");
        int ticks=0;float damageAtTick=hp.CurBlood;deadline=Time.time+3.3f;
        while(lantern.gameObject.activeSelf && Time.time<deadline)
        {
            if(lantern.FireTicks!=ticks)
            {
                if(lantern.FireTicks!=ticks+1 || !Near(damageAtTick-hp.CurBlood,5f))throw new InvalidOperationException("Fire tick cadence/damage");
                ticks=lantern.FireTicks;damageAtTick=hp.CurBlood;
            }
            yield return null;
        }
        Check(lantern.FireTicks==6 && Near(before-hp.CurBlood,48f) && !lantern.gameObject.activeSelf,"3 second fire zone ticks six times for 5 damage every 0.5 seconds then recycles");
        hp.Heal(1000f);ResetEnemy(enemy,hero);RedBoomLantern reused=pool.Throw(lanternPrefab,combat,new Vector2(0.65f,1.2f),hero);
        Check(reused==lantern && !reused.HasExploded && reused.FireTicks==0,"lantern reuses pooled instance with clean state");
        RedBoomMark cleanup=pool.Mark(markPrefab,hp,source);pool.ReleaseAll();before=hp.CurBlood;
        hp.TakeDamage(new DamageInfo(enemy.transform,10f,false,false,null,false,true));
        Check(!cleanup.gameObject.activeSelf && !RedBoomMark.IsMarked(hp) && Near(before-hp.CurBlood,10f),"pool release clears marks and restores damage multipliers");
    }
}

public static class RedBoomValidationPaths
{
    public static string AuditDirectory
    {
        get
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-redBoomAuditOutput")return Path.GetFullPath(args[i+1]);
            return Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/RedBoomAudit"));
        }
    }
    public static string FilePath(string name)=>Path.Combine(AuditDirectory,name);
}
#endif

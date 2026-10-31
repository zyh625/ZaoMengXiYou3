using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RedBoomSetup
{
    public const string EnemyPath="Assets/Projects/Prefabs/Enemy/RedBoom.prefab";
    public const string LanternPath="Assets/Projects/Prefabs/SeparateEffort/RedBoomLantern.prefab";
    public const string RayPath="Assets/Projects/Prefabs/SeparateEffort/RedBoomRay.prefab";
    public const string MarkPath="Assets/Projects/Prefabs/SeparateEffort/RedBoomMark.prefab";
    private const string MaterialPath="Assets/Projects/Prefabs/SeparateEffort/RedBoomWarning.mat";
    private const string ScenePath="Assets/Projects/Scenes/Levels/Level5Scene.unity";
    private const string ArtPath="Assets/Art/Enemies/Level5/RedBoom/";
    public static Sprite[] Frames(string action)=>AssetDatabase.LoadAllAssetsAtPath(ArtPath+action+".png").OfType<Sprite>()
        .OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
    private static void Edit(Object component,Action<SerializedObject> edit)
    { SerializedObject so=new(component);edit(so);so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Ref(Object c,string n,Object v)=>Edit(c,so=>so.FindProperty(n).objectReferenceValue=v);
    private static void Number(Object c,string n,float v)=>Edit(c,so=>so.FindProperty(n).floatValue=v);
    private static void Integer(Object c,string n,int v)=>Edit(c,so=>so.FindProperty(n).intValue=v);
    private static void Empty(Object c,string n)=>Edit(c,so=>so.FindProperty(n).arraySize=0);
    private static void Clip(Object c,string n,string action,float fps,bool loop)=>Edit(c,so=>
    {
        SerializedProperty p=so.FindProperty(n),sprites=p.FindPropertyRelative("sprites");Sprite[] frames=Frames(action);
        sprites.arraySize=frames.Length;
        for(int i=0;i<frames.Length;i++)sprites.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];
        p.FindPropertyRelative("fps").floatValue=fps;p.FindPropertyRelative("loop").boolValue=loop;
    });
    private static GameObject Child(GameObject root,string name,int layer=0)
    { GameObject child=new(name){layer=layer};child.transform.SetParent(root.transform,false);return child; }
    private static Material EnemyMaterial=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/PurpleRanged.prefab").GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
    private static SpriteManager Visual(GameObject root,string action,out SpriteRenderer renderer)
    {
        GameObject visual=Child(root,"Visual");renderer=visual.AddComponent<SpriteRenderer>();renderer.sharedMaterial=EnemyMaterial;
        renderer.sortingOrder=3;renderer.sprite=Frames(action)[0];SpriteManager sm=visual.AddComponent<SpriteManager>();Ref(sm,"sr",renderer);return sm;
    }
    private static Material WarningMaterial()
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material!=null)return material;
        Shader shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Require(shader!=null,"URP sprite unlit shader");
        material=new Material(shader){name="RedBoomWarning"};material.SetTexture("_MainTex",Texture2D.whiteTexture);
        AssetDatabase.CreateAsset(material,MaterialPath);return material;
    }
    private static LineRenderer Line(GameObject root,string name,int points,float width,Color color)
    {
        LineRenderer line=Child(root,name).AddComponent<LineRenderer>();line.sharedMaterial=WarningMaterial();line.positionCount=points;
        line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.numCapVertices=4;line.sortingOrder=4;line.enabled=false;
        return line;
    }

    [MenuItem("造梦西游/RedBoom/生成并验证引魂灯使")]
    public static void BuildAndValidate()
    {
        Scene previous=SceneManager.GetActiveScene();Scene scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateSprites();CreateEffects();CreateEnemy();WireScene();ValidateReferences();
            Directory.CreateDirectory(RedBoomValidationPaths.AuditDirectory);
            File.WriteAllText(RedBoomValidationPaths.FilePath("unity-assets.txt"),"PASS: 145 valid sprites, RedBoom plus three effect prefabs, no missing references, Level5 10 / 0.45.\n");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();Debug.Log("REDBOOM_ASSETS_PASS");
        }
        catch(Exception exception) { File.WriteAllText(RedBoomValidationPaths.FilePath("unity-assets.txt"),"FAIL: "+exception);Debug.LogException(exception);throw; }
        finally { if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scratch,true); }
    }
    public static void ValidateSprites()
    {
        foreach(string action in new[]{"Idle","Walk","Attacked","Die","AttackOne","AttackTwo","AttackThree","Lantern","Ray","Mark"})
        {
            Sprite[] frames=Frames(action);int expected=action=="Ray"?8:action=="Mark"?9:16;
            Require(frames.Length==expected,action+" frame count");
            Texture2D pixels=new(2,2,TextureFormat.RGBA32,false);pixels.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Art/Enemies/Level5/RedBoom/"+action+".png")));Color32[] rgba=pixels.GetPixels32();
            for(int i=0;i<expected;i++)
            {
                Sprite frame=frames[i];Rect rect=frame.rect;
                Require(frame.name==action+"_"+i && frame.pixelsPerUnit==100f,action+" name/PPU");
                Require(rect.xMin>=0 && rect.yMin>=0 && rect.xMax<=pixels.width && rect.yMax<=pixels.height,action+" bounds");
                bool visible=false;
                for(int y=(int)rect.yMin;y<(int)rect.yMax&&!visible;y++)
                    for(int x=(int)rect.xMin;x<(int)rect.xMax;x++) if(rgba[y*pixels.width+x].a>=2){visible=true;break;}
                Require(visible,frame.name+" empty sprite");
            }
            Object.DestroyImmediate(pixels);
        }
    }
    private static void CreateEffects()
    {
        GameObject root=new("RedBoomRay");SpriteManager sm=Visual(root,"Ray",out SpriteRenderer sr);
        LineRenderer line=Line(root,"Warning",2,0.035f,new Color(1f,0.08f,0.02f,0.95f));line.useWorldSpace=true;
        RedBoomRay ray=root.AddComponent<RedBoomRay>();Ref(ray,"spriteManager",sm);Ref(ray,"visual",sr);Ref(ray,"warningLine",line);
        Clip(ray,"flash","Ray",24f,false);Number(ray,"nativeWidth",Frames("Ray").Max(s=>s.rect.width)/100f);
        PrefabUtility.SaveAsPrefabAsset(root,RayPath);Object.DestroyImmediate(root);

        root=new("RedBoomMark");sm=Visual(root,"Mark",out sr);RedBoomMark mark=root.AddComponent<RedBoomMark>();
        Ref(mark,"spriteManager",sm);Clip(mark,"appearance","Mark",18f,false);
        PrefabUtility.SaveAsPrefabAsset(root,MarkPath);Object.DestroyImmediate(root);

        root=new("RedBoomLantern");sm=Visual(root,"Lantern",out sr);
        LineRenderer ring=Line(root,"FireArea",33,0.035f,new Color(1f,0.25f,0.02f,0.7f));ring.useWorldSpace=false;
        for(int i=0;i<33;i++){float angle=i*Mathf.PI*2f/32f;ring.SetPosition(i,new Vector3(Mathf.Cos(angle)*2.2f,Mathf.Sin(angle)*0.12f-0.08f,0f));}
        RedBoomLantern lantern=root.AddComponent<RedBoomLantern>();Ref(lantern,"spriteManager",sm);Ref(lantern,"visual",sr);Ref(lantern,"fireRing",ring);
        Clip(lantern,"flight","Lantern",14f,true);Clip(lantern,"explosion","Ray",20f,false);Clip(lantern,"fire","Ray",12f,true);
        Number(lantern,"flightTime",0.9f);Number(lantern,"gravity",18f);Number(lantern,"explosionDamage",18f);Number(lantern,"radius",2.2f);
        Number(lantern,"fuseDuration",1f);Number(lantern,"fireDuration",3f);Number(lantern,"fireInterval",0.5f);Number(lantern,"fireTickDamage",5f);
        Number(lantern,"blastNativeWidth",Frames("Ray").Max(s=>s.rect.width)/100f);
        PrefabUtility.SaveAsPrefabAsset(root,LanternPath);Object.DestroyImmediate(root);
    }
    private static void CreateEnemy()
    {
        GameObject root=new("RedBoom"){layer=11};Rigidbody2D body=root.AddComponent<Rigidbody2D>();
        body.gravityScale=0.9f;body.constraints=RigidbodyConstraints2D.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        BoxCollider2D physical=root.AddComponent<BoxCollider2D>();physical.size=new Vector2(1.05f,2.05f);physical.offset=new Vector2(0f,1.025f);
        Health health=root.AddComponent<Health>();Number(health,"beginBlood",80f);
        ActorStatus status=root.AddComponent<ActorStatus>();EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/PurpleRanged.prefab").GetComponent<ActorStatus>(),status);
        EnemyRewards rewards=root.AddComponent<EnemyRewards>();Number(rewards,"experienceReward",18f);
        RedBoomMovement movement=root.AddComponent<RedBoomMovement>();Number(movement,"moveSpeed",0.8f);Number(movement,"slowSpeed",0.4f);
        Number(movement,"backSpeed",3.5f);Number(movement,"upSpeed",6f);Number(movement,"boostRatio",1f);Number(movement,"minNormalY",0.6f);Integer(movement,"groundLayer",1 << 6);
        Number(movement,"blinkCooldown",6f);Number(movement,"blinkDistance",4f);
        RedBoomCombat combat=root.AddComponent<RedBoomCombat>();RedBoomController controller=root.AddComponent<RedBoomController>();
        foreach(string field in new[]{"AttackBoxes","attackDelay","CheckAttackLogic","AttackOpenIdx","AttackShutIdx"})Empty(combat,field);
        Ref(combat,"lanternPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(LanternPath).GetComponent<RedBoomLantern>());
        Ref(combat,"rayPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(RayPath).GetComponent<RedBoomRay>());
        Ref(combat,"markPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(MarkPath).GetComponent<RedBoomMark>());
        Transform muzzle=Child(root,"Muzzle").transform;muzzle.localPosition=new Vector3(0.65f,1.15f,0f);Ref(combat,"muzzle",muzzle);
        Number(combat,"rayRange",10f);Number(combat,"rayHeight",0.55f);
        Edit(combat,so=>{SerializedProperty cds=so.FindProperty("skillCooldowns");cds.arraySize=3;float[] values={5f,7f,12f};for(int i=0;i<3;i++)cds.GetArrayElementAtIndex(i).floatValue=values[i];});
        Visual(root,"Idle",out SpriteRenderer renderer);renderer.sortingOrder=2;renderer.transform.localScale=new Vector3(-1f,1f,1f);renderer.gameObject.AddComponent<SpriteOutlineController>();
        GameObject boxes=Child(root,"Combat"),hurt=Child(boxes,"Attacked",7);BoxCollider2D hurtBox=hurt.AddComponent<BoxCollider2D>();
        hurtBox.isTrigger=true;hurtBox.size=new Vector2(1.3f,2.25f);hurtBox.offset=new Vector2(0f,1.125f);Ref(controller,"attackedBox",hurtBox);
        GameObject check=Child(boxes,"SkillCheck",10);BoxCollider2D detector=check.AddComponent<BoxCollider2D>();detector.isTrigger=true;detector.size=new Vector2(14f,4f);detector.offset=new Vector2(0f,1.2f);check.AddComponent<EnemyCheckAttack>();
        Number(controller,"stopDistance",7f);
        Clip(controller,"Idle","Idle",10f,true);Clip(controller,"Walk","Walk",14f,true);Clip(controller,"Attacked","Attacked",18f,false);Clip(controller,"Die","Die",12f,false);
        foreach(string action in new[]{"AttackOne","AttackTwo","AttackThree"})Clip(controller,action,action,16f,false);
        PrefabUtility.SaveAsPrefabAsset(root,EnemyPath);Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        StageEnemyManager manager=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<StageEnemyManager>(true)).FirstOrDefault();
        Require(manager!=null,"Level5 StageEnemyManager");
        RedBoomController prefab=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<RedBoomController>();
        Edit(manager,so=>
        {
            SerializedProperty pools=so.FindProperty("enemyPools");int index=-1;
            for(int i=0;i<pools.arraySize;i++)if(pools.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is RedBoomController)index=i;
            if(index<0){index=pools.arraySize;pools.arraySize++;}
            SerializedProperty pool=pools.GetArrayElementAtIndex(index);pool.FindPropertyRelative("prefab").objectReferenceValue=prefab;
            pool.FindPropertyRelative("poolSize").intValue=10;pool.FindPropertyRelative("weight").floatValue=0.45f;
        });
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(opened)EditorSceneManager.CloseScene(scene,true);
    }
    public static void ValidateReferences()
    {
        foreach(string path in new[]{EnemyPath,LanternPath,RayPath,MarkPath})
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab!=null,path);
            foreach(Transform t in prefab.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,t.name+" missing script");
            foreach(Component c in prefab.GetComponentsInChildren<Component>(true))
            {
                SerializedProperty p=new SerializedObject(c).GetIterator();
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference)
                {
#pragma warning disable CS0618
                    Require(p.objectReferenceValue!=null||p.objectReferenceInstanceIDValue==0,path+" / "+p.propertyPath);
#pragma warning restore CS0618
                }
            }
        }
        GameObject enemy=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        Require(enemy.GetComponent<Health>().BeginBlood==80f,"HP 80");
        Require(new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue==18f,"XP 18");
        Require(enemy.GetComponentInChildren<BossUI>(true)==null,"No boss UI");
    }
    private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
}

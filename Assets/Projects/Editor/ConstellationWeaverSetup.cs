using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class ConstellationWeaverSetup
{
    public const string EnemyPath="Assets/Projects/Prefabs/Enemy/ConstellationWeaver.prefab";
    public const string AnchorPath="Assets/Projects/Prefabs/SeparateEffort/StarAnchor.prefab";
    public const string SilkPath="Assets/Projects/Prefabs/SeparateEffort/ConstellationWeaverSilkLine.prefab";
    public const string KnotPath="Assets/Projects/Prefabs/SeparateEffort/ConstellationWeaverStarKnot.prefab";
    private const string MaterialPath="Assets/Projects/Prefabs/SeparateEffort/ConstellationWeaverPlaceholder.mat";
    private const string ScenePath="Assets/Projects/Scenes/Levels/Level8Scene.unity";
    private const string ArtPath="Assets/Art/Enemies/Level8/ConstellationWeaver/";
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
        material=new Material(shader){name="ConstellationWeaverWarning"};material.SetTexture("_MainTex",Texture2D.whiteTexture);
        AssetDatabase.CreateAsset(material,MaterialPath);return material;
    }
    private static LineRenderer Line(GameObject root,string name,int points,float width,Color color)
    {
        LineRenderer line=Child(root,name).AddComponent<LineRenderer>();line.sharedMaterial=WarningMaterial();line.positionCount=points;
        line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.numCapVertices=4;line.sortingOrder=4;line.enabled=false;
        return line;
    }

    [MenuItem("造梦西游/ConstellationWeaver/生成并验证织宿星蛛")]
    public static void BuildAndValidate()
    {
        if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Scene previous=SceneManager.GetActiveScene();Scene scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateSprites();CreateEffects();CreateEnemy();WireScene();ValidateReferences();
            Directory.CreateDirectory("Temp/ConstellationWeaverAudit");
            File.WriteAllText("Temp/ConstellationWeaverAudit/unity-assets.txt","PASS: 112 frames, references, HP 138 XP 24, Level8 10 / 0.33.\n");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();Debug.Log("CONSTELLATION_WEAVER_ASSETS_PASS");
        }
        catch(Exception exception) { Debug.LogException(exception);throw; }
        finally { if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scratch,true); }
    }
    public static void ValidateSprites()
    {
        foreach(string action in new[]{"Idle","Walk","Attacked","Die","TwinAnchor","ThreeStarBind","SilkReposition"})
        {
            Sprite[] frames=Frames(action);int expected=16;
            Require(frames.Length==expected,action+" frame count");
            Texture2D pixels=new(2,2,TextureFormat.RGBA32,false);pixels.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Art/Enemies/Level8/ConstellationWeaver/"+action+".png")));Color32[] rgba=pixels.GetPixels32();
            Require(pixels.width==1024 && pixels.height==1536,action+" atlas dimensions");
            for(int i=0;i<expected;i++)
            {
                Sprite frame=frames[i];Rect rect=frame.rect; Require(Mathf.Abs(frame.pivot.x-rect.width*0.5f)<0.1f && Mathf.Abs(frame.pivot.y)<0.1f,frame.name+" foot pivot");
                Require(frame.name==action+"_"+i && frame.pixelsPerUnit==100f,action+" name/PPU");
                Require(rect.xMin>=0 && rect.yMin>=0 && rect.xMax<=pixels.width && rect.yMax<=pixels.height,action+" bounds");
                bool visible=false;
                for(int y=(int)rect.yMin;y<(int)rect.yMax&&!visible;y++)
                    for(int x=(int)rect.xMin;x<(int)rect.xMax;x++) if(rgba[y*pixels.width+x].a>=2){visible=true;break;}
                Require(visible,frame.name+" empty sprite");
                Require(frame.vertices.Length>=3 && frame.triangles.Length>=3,frame.name+" empty mesh");
            }
            Object.DestroyImmediate(pixels);
        }
    }
    private static void CreateEffects()
    {
        GameObject root=new("StarAnchor"){layer=7};
        Number(root.AddComponent<Health>(),"beginBlood",30f);
        Rigidbody2D body=root.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;
        BoxCollider2D box=root.AddComponent<BoxCollider2D>();box.isTrigger=true;box.size=new Vector2(0.4f,0.4f);
        root.AddComponent<StarAnchor>();
        const string squarePath="Assets/Projects/Prefabs/SeparateEffort/StarAnchorPlaceholder.png";
        if(!File.Exists(squarePath))
        {
            Texture2D square=new(8,8);square.SetPixels(Enumerable.Repeat(Color.white,64).ToArray());square.Apply();
            File.WriteAllBytes(squarePath,square.EncodeToPNG());Object.DestroyImmediate(square);
        }
        AssetDatabase.ImportAsset(squarePath,ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(squarePath);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=8;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        SpriteRenderer point=Child(root,"CyanSquarePlaceholder").AddComponent<SpriteRenderer>();
        point.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(squarePath);point.sharedMaterial=WarningMaterial();
        point.color=new Color(0.15f,0.85f,1f,0.35f);point.sortingOrder=3;point.transform.localScale=Vector3.one*0.3f;
        PrefabUtility.SaveAsPrefabAsset(root,AnchorPath);Object.DestroyImmediate(root);
        foreach(string path in new[]{SilkPath,KnotPath})
        {
            root=new(Path.GetFileNameWithoutExtension(path));
            LineRenderer line=Line(root,"CyanPlaceholder",2,0.035f,new Color(0.15f,0.85f,1f,0.3f));
            Ref(root.AddComponent<ConstellationWeaverArea>(),"line",line);
            PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
        }
    }
    private static void CreateEnemy()
    {
        GameObject root=new("ConstellationWeaver"){layer=11};Rigidbody2D body=root.AddComponent<Rigidbody2D>();
        body.gravityScale=0.9f;body.constraints=RigidbodyConstraints2D.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        BoxCollider2D physical=root.AddComponent<BoxCollider2D>();physical.size=new Vector2(1.35f,0.85f);physical.offset=new Vector2(0f,0.425f);
        Health health=root.AddComponent<Health>();Number(health,"beginBlood",138f);
        ActorStatus status=root.GetComponent<ActorStatus>();EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/PurpleRanged.prefab").GetComponent<ActorStatus>(),status);
        EnemyRewards rewards=root.AddComponent<EnemyRewards>();Number(rewards,"experienceReward",24f);
        ConstellationWeaverMovement movement=root.AddComponent<ConstellationWeaverMovement>();Number(movement,"moveSpeed",0.9f);Number(movement,"slowSpeed",0.4f);
        Number(movement,"backSpeed",3.5f);Number(movement,"upSpeed",6f);Number(movement,"boostRatio",1f);Number(movement,"minNormalY",0.6f);Integer(movement,"groundLayer",1 << 6);
        ConstellationWeaverCombat combat=root.AddComponent<ConstellationWeaverCombat>();ConstellationWeaverController controller=root.AddComponent<ConstellationWeaverController>();
        foreach(string field in new[]{"AttackBoxes","attackDelay","CheckAttackLogic","AttackOpenIdx","AttackShutIdx"})Empty(combat,field);
        Ref(combat,"starAnchorPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(AnchorPath).GetComponent<StarAnchor>());
        Ref(combat,"silkLinePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(SilkPath).GetComponent<ConstellationWeaverArea>());
        Ref(combat,"starKnotPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(KnotPath).GetComponent<ConstellationWeaverArea>());
        Visual(root,"Idle",out SpriteRenderer renderer);renderer.sortingOrder=2;renderer.transform.localScale=new Vector3(-1f,1f,1f);renderer.gameObject.AddComponent<SpriteOutlineController>();
        GameObject boxes=Child(root,"Combat"),hurt=Child(boxes,"Attacked",7);BoxCollider2D hurtBox=hurt.AddComponent<BoxCollider2D>();
        hurtBox.isTrigger=true;hurtBox.size=new Vector2(1.5f,1f);hurtBox.offset=new Vector2(0f,0.5f);Ref(controller,"attackedBox",hurtBox);
        Number(controller,"stopDistance",6f);
        Clip(controller,"Idle","Idle",10f,true);Clip(controller,"Walk","Walk",14f,true);Clip(controller,"Attacked","Attacked",18f,false);Clip(controller,"Die","Die",12f,false);
        foreach(string action in new[]{"TwinAnchor","ThreeStarBind","SilkReposition"})Clip(controller,action,action,16f,false);
        PrefabUtility.SaveAsPrefabAsset(root,EnemyPath);Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        StageEnemyManager manager=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<StageEnemyManager>(true)).FirstOrDefault();
        if(manager==null)
        {
            LevelManager layout=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<LevelManager>(true)).First();
            manager=layout.gameObject.AddComponent<StageEnemyManager>();
        }
        Require(manager!=null,"Level8 StageEnemyManager");
        ConstellationWeaverController prefab=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<ConstellationWeaverController>();
        Edit(manager,so=>
        {
            SerializedProperty pools=so.FindProperty("enemyPools");int index=-1;
            for(int i=0;i<pools.arraySize;i++)if(pools.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is ConstellationWeaverController)index=i;
            if(index<0){index=pools.arraySize;pools.arraySize++;}
            SerializedProperty pool=pools.GetArrayElementAtIndex(index);pool.FindPropertyRelative("prefab").objectReferenceValue=prefab;
            pool.FindPropertyRelative("poolSize").intValue=10;pool.FindPropertyRelative("weight").floatValue=0.33f;
        });
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(opened)EditorSceneManager.CloseScene(scene,true);
    }
    public static void ValidateReferences()
    {
        foreach(string path in new[]{EnemyPath,AnchorPath,SilkPath,KnotPath})
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
        Require(enemy.GetComponent<Health>().BeginBlood==138f,"HP 138");
        Require(new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue==24f,"XP 24");
        Require(enemy.GetComponentInChildren<BossUI>(true)==null,"No boss UI");
    }
    private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
}

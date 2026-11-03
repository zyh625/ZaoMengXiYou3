using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using zmxy.GamePlay;
using Object=UnityEngine.Object;

//显式菜单与批处理；只写GreenHammer资产、命名修正后的既有切片及Level6Scene。
public static class GreenHammerSetup
{
    public const string EnemyPath="Assets/Projects/Prefabs/Enemy/GreenHammer.prefab";
    public const string WavePath="Assets/Projects/Prefabs/SeparateEffort/GreenHammerShockwave.prefab";
    public const string ScenePath="Assets/Projects/Scenes/Levels/Level6Scene.unity";
    private const string Art="Assets/Art/Enemies/Level6/GreenHammer/";
    public const string EffectArtPath=Art+"Effects/GreenHammerShockwave.png";
    public static readonly string[] Actions={"Idle","Walk","HammerSweep","LeapSlam","GroundPound","Hit","Death"};
    public static string Audit=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/GreenHammerAudit"));
    public static Sprite[] Frames(string action)=>AssetDatabase.LoadAllAssetsAtPath(Art+action+".png").OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
    public static Sprite[] WaveFrames()=>AssetDatabase.LoadAllAssetsAtPath(EffectArtPath).OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
    private static void Edit(Object c,Action<SerializedObject> edit){SerializedObject so=new(c);edit(so);so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Ref(Object c,string field,Object value)=>Edit(c,so=>so.FindProperty(field).objectReferenceValue=value);
    private static void Num(Object c,string field,float value)=>Edit(c,so=>so.FindProperty(field).floatValue=value);
    private static void Int(Object c,string field,int value)=>Edit(c,so=>so.FindProperty(field).intValue=value);
    private static void Flag(Object c,string field,bool value)=>Edit(c,so=>so.FindProperty(field).boolValue=value);
    private static void Refs(Object c,string field,Object[] values)=>Edit(c,so=>
    {SerializedProperty p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];});
    private static void Ints(Object c,string field,params int[] values)=>Edit(c,so=>
    {SerializedProperty p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).intValue=values[i];});
    private static void Floats(Object c,string field,params float[] values)=>Edit(c,so=>
    {SerializedProperty p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).floatValue=values[i];});
    private static void Clip(Object c,string field,Sprite[] frames,float fps,bool loop)=>Edit(c,so=>
    {
        SerializedProperty p=so.FindProperty(field),sprites=p.FindPropertyRelative("sprites");sprites.arraySize=frames.Length;
        for(int i=0;i<frames.Length;i++)sprites.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];
        p.FindPropertyRelative("fps").floatValue=fps;p.FindPropertyRelative("loop").boolValue=loop;
    });
    private static GameObject Child(GameObject parent,string name,int layer=0){GameObject child=new(name){layer=layer};child.transform.SetParent(parent.transform,false);return child;}
    private static GameObject Reference=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/RedChain.prefab");
    private static SpriteManager Visual(GameObject root,Sprite sprite,out SpriteRenderer renderer)
    {
        GameObject go=Child(root,"Visual");renderer=go.AddComponent<SpriteRenderer>();SpriteRenderer reference=Reference.GetComponentInChildren<SpriteRenderer>(true);
        renderer.sharedMaterial=reference.sharedMaterial;renderer.sortingLayerID=reference.sortingLayerID;renderer.sortingOrder=reference.sortingOrder;renderer.sprite=sprite;
        SpriteManager sm=go.AddComponent<SpriteManager>();Ref(sm,"sr",renderer);return sm;
    }
    [MenuItem("造梦西游/GreenHammer/生成并验证青玉锤卒")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Audit);
        if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Scene previous=SceneManager.GetActiveScene(),scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);ValidateSprites();CreateWave();CreateEnemy();WireScene();ValidatePrefabs();
            File.WriteAllText(Path.Combine(Audit,"unity-assets.txt"),"PASS: 112 character sprites plus 16 dedicated shockwave sprites, preserved PNGs and pivots, GreenHammer plus shockwave prefabs, no missing references, HP 150, XP 20, frame windows 5-9 / 9-12 / 9-14, Level6 pool 10 / 0.55.\n");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();Debug.Log("GREENHAMMER_ASSETS_PASS");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scratch,true);}
    }
    public static void ValidateSprites()
    {
        foreach(string action in Actions)
        {
            Sprite[] sprites=Frames(action);Require(sprites.Length==16,action+" frame count");
            Texture2D texture=new(2,2);texture.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"../"+Art+action+".png")));Color32[] pixels=texture.GetPixels32();
            Require(texture.width==1024 && texture.height==1536,action+" image size");
            for(int i=0;i<16;i++)
            {
                Sprite sprite=sprites[i];Rect r=sprite.rect;Require(sprite.name==action+"_"+i && sprite.pixelsPerUnit==100f,action+" names / PPU");
                Require(r.x==i%4*256 && r.y==1536-(i/4+1)*384 && r.width==256 && r.height==384,action+" original rect");
                Require(Mathf.Abs(sprite.pivot.x-128f)<0.001f && Mathf.Abs(sprite.pivot.y-56f)<0.001f,action+" ground pivot");
                bool visible=false;for(int y=(int)r.yMin;y<(int)r.yMax&&!visible;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)if(pixels[y*1024+x].a>=2){visible=true;break;}
                Require(visible && sprite.vertices.Length>=3,sprite.name+" empty sprite");
            }
            Object.DestroyImmediate(texture);
        }
    }
    private static void CreateWave()
    {
        Sprite[] frames=WaveFrames();float nativeWidth=ValidateWaveSprites();GameObject root=new("GreenHammerShockwave"){layer=8};
        SpriteManager sm=Visual(root,frames[0],out SpriteRenderer renderer);GreenHammerShockwave wave=root.AddComponent<GreenHammerShockwave>();
        BoxCollider2D collider=root.AddComponent<BoxCollider2D>();collider.isTrigger=true;collider.size=new Vector2(2f,0.9f);collider.offset=Vector2.up*0.45f;collider.enabled=false;
        GreenHammerAttackBox box=root.AddComponent<GreenHammerAttackBox>();Num(box,"Attack",18f);Flag(box,"knockback",false);Flag(box,"instantOnly",true);Int(box,"shape",2);
        Ref(wave,"spriteManager",sm);Ref(wave,"visual",renderer);Ref(wave,"damageBox",box);Clip(wave,"frames",frames,16f,false);
        Num(wave,"nativeWidth",nativeWidth);Num(wave,"lifetime",frames.Length/16f);
        PrefabUtility.SaveAsPrefabAsset(root,WavePath);Object.DestroyImmediate(root);
    }
    public static float ValidateWaveSprites()
    {
        Sprite[] frames=WaveFrames();Require(frames.Length==16,"dedicated shockwave frame count");
        Texture2D texture=new(2,2);texture.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"../"+EffectArtPath)));Color32[] pixels=texture.GetPixels32();
        Require(texture.width==1024 && texture.height==1024,"shockwave canvas");int widest=0;
        for(int i=0;i<16;i++)
        {
            Sprite sprite=frames[i];Rect r=sprite.rect;
            Require(sprite.name=="GreenHammerShockwave_"+(i+1).ToString("00") && sprite.pixelsPerUnit==100f,"shockwave names / PPU");
            Require(r.x==i%4*256 && r.y==1024-(i/4+1)*256 && r.width==256 && r.height==256,"shockwave original rect");
            Require(Mathf.Abs(sprite.pivot.x-128f)<0.001f && Mathf.Abs(sprite.pivot.y-40f)<0.001f,"shockwave fixed ground pivot");
            int min=1024,max=-1;for(int y=(int)r.yMin;y<(int)r.yMax;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)if(pixels[y*1024+x].a>=2){min=Mathf.Min(min,x);max=Mathf.Max(max,x);}
            Require(max>=min && sprite.vertices.Length>=3,sprite.name+" empty shockwave");widest=Mathf.Max(widest,max-min+1);
        }
        Object.DestroyImmediate(texture);return widest/100f; //按实际可见峰值宽度缩放，透明留白不计入2u/6u范围。
    }
    private static void CreateEnemy()
    {
        GameObject root=new("GreenHammer"){layer=11};Rigidbody2D body=root.AddComponent<Rigidbody2D>();body.gravityScale=0.9f;body.constraints=RigidbodyConstraints2D.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        BoxCollider2D physical=root.AddComponent<BoxCollider2D>();physical.size=new Vector2(1.05f,2.05f);physical.offset=Vector2.up*1.025f;
        Health hp=root.AddComponent<Health>();Num(hp,"beginBlood",150f);
        EditorUtility.CopySerialized(Reference.GetComponent<ActorStatus>(),root.GetComponent<ActorStatus>()??root.AddComponent<ActorStatus>());
        EnemyRewards rewards=root.AddComponent<EnemyRewards>();Num(rewards,"experienceReward",20f);Flag(rewards,"isBoss",false);
        GreenHammerMovement movement=root.AddComponent<GreenHammerMovement>();Num(movement,"moveSpeed",0.8f);Num(movement,"slowSpeed",0.4f);Num(movement,"backSpeed",3.5f);Num(movement,"upSpeed",6f);Num(movement,"boostRatio",1f);Num(movement,"minNormalY",0.6f);Int(movement,"groundLayer",1<<6);
        Num(movement,"spawnGravity",0.9f);Num(movement,"leapUpSpeed",5f);Num(movement,"leapAcceleration",28f);Num(movement,"maxLeapAdvance",3f);
        GreenHammerCombat combat=root.AddComponent<GreenHammerCombat>();GreenHammerController controller=root.AddComponent<GreenHammerController>();Num(controller,"stopDistance",3.2f);Num(controller,"recoveryDuration",1.4f);
        Visual(root,Frames("Idle")[0],out SpriteRenderer sr);sr.transform.localScale=new Vector3(-1f,1f,1f);sr.gameObject.AddComponent<SpriteOutlineController>();
        GameObject boxes=Child(root,"Combat"),hurt=Child(boxes,"Attacked",7);BoxCollider2D hurtBox=hurt.AddComponent<BoxCollider2D>();hurtBox.isTrigger=true;hurtBox.size=new Vector2(1.25f,2.2f);hurtBox.offset=Vector2.up*1.1f;Ref(controller,"attackedBox",hurtBox);
        BoxCollider2D[] attacks=new BoxCollider2D[3];GreenHammerAttackBox[] hitBoxes=new GreenHammerAttackBox[3];EnemyCheckAttack[] checks=new EnemyCheckAttack[3];
        string[] names={"HammerSweep","LeapSlam","GroundPound"};float[] damage={19f,26f,18f};
        for(int i=0;i<3;i++)
        {
            GameObject hit=Child(boxes,names[i],8);BoxCollider2D box=hit.AddComponent<BoxCollider2D>();box.isTrigger=true;
            box.size=i==0?new Vector2(4.5f,2.2f):i==1?new Vector2(6f,6f):new Vector2(2f,0.9f);
            box.offset=i==0?new Vector2(2.775f,1.1f):i==1?Vector2.zero:new Vector2(2f,0.45f);
            GreenHammerAttackBox logic=hit.AddComponent<GreenHammerAttackBox>();Num(logic,"Attack",damage[i]);Flag(logic,"knockback",i==0);Flag(logic,"instantOnly",i!=0);Int(logic,"shape",i);
            attacks[i]=box;hitBoxes[i]=logic;hit.SetActive(false);
            GameObject detection=Child(boxes,names[i]+"Check",10);BoxCollider2D check=detection.AddComponent<BoxCollider2D>();check.isTrigger=true;check.size=new Vector2(i==2?16f:12f,4f);check.offset=Vector2.up*1.2f;checks[i]=detection.AddComponent<EnemyCheckAttack>();
        }
        Refs(combat,"AttackBoxes",attacks);Refs(combat,"hitBoxes",hitBoxes);Refs(combat,"CheckAttackLogic",checks);Floats(combat,"attackDelay",4f,10f,9f);Ints(combat,"AttackOpenIdx",5,9,9);Ints(combat,"AttackShutIdx",9,12,14);
        GreenHammerShockwaveEmitter emitter=Child(root,"Shockwave").AddComponent<GreenHammerShockwaveEmitter>();Ref(emitter,"prefab",AssetDatabase.LoadAssetAtPath<GameObject>(WavePath).GetComponent<GreenHammerShockwave>());Ref(combat,"shockwave",emitter);
        Clip(controller,"Idle",Frames("Idle"),10f,true);Clip(controller,"Walk",Frames("Walk"),14f,true);Clip(controller,"Attacked",Frames("Hit"),18f,false);Clip(controller,"Die",Frames("Death"),12f,false);
        foreach(string action in names)Clip(controller,action,Frames(action),16f,false);
        PrefabUtility.SaveAsPrefabAsset(root,EnemyPath);Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        GameObject Named(string name)=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).FirstOrDefault(g=>g.name==name);
        T[] Components<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        GameObject layout=Named("Level6_XuantianTreasurePavilion_Layout");Require(layout!=null,"Level6 layout");GameObjectUtility.RemoveMonoBehavioursWithMissingScript(layout);
        foreach(LevelManager previous in layout.GetComponents<LevelManager>())if(!(previous is GreenHammerStageManager))Object.DestroyImmediate(previous);
        GreenHammerStageManager stage=layout.GetComponent<GreenHammerStageManager>()??layout.AddComponent<GreenHammerStageManager>();
        GreenHammerStageEnemyManager enemies=layout.GetComponent<GreenHammerStageEnemyManager>()??layout.AddComponent<GreenHammerStageEnemyManager>();stage.enabled=enemies.enabled=true;
        Edit(enemies,so=>
        {
            SerializedProperty pools=so.FindProperty("enemyPools");int index=-1;
            for(int i=0;i<pools.arraySize;i++)if(pools.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is GreenHammerController)index=i;
            if(index<0){index=pools.arraySize;pools.arraySize++;}SerializedProperty pool=pools.GetArrayElementAtIndex(index);
            pool.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<GreenHammerController>();pool.FindPropertyRelative("poolSize").intValue=10;pool.FindPropertyRelative("weight").floatValue=0.55f;
        });
        Ref(enemies,"bossBornLocation",Named("DuowenTianwang_BossSpawn").transform);
        PlayerManager players=Components<PlayerManager>().FirstOrDefault();if(players==null)players=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/PlayerSystem.prefab"),scene)).GetComponent<PlayerManager>();
        UIManager ui=Components<UIManager>().FirstOrDefault();if(ui==null)ui=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/BattleUI.prefab"),scene)).GetComponentInChildren<UIManager>(true);
        foreach(PlayerController oldPlayer in Components<PlayerController>())oldPlayer.gameObject.SetActive(false);
        Camera camera=Components<Camera>().First();camera.tag="MainCamera";GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);
        CameraManager cm=camera.GetComponent<CameraManager>();if(cm==null){cm=camera.gameObject.AddComponent<CameraManager>();EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/CameraRig.prefab").GetComponentInChildren<CameraManager>(true),cm);}
        Refs(cm,"cameraEdgeLeft",Enumerable.Range(0,4).Select(i=>(Object)Named("tranL"+i).transform).ToArray());Refs(cm,"cameraEdgeRight",Enumerable.Range(0,4).Select(i=>(Object)Named("tranR"+i).transform).ToArray());cm.enabled=true;
        Transform spawn=Named("PlayerSpawn").transform;GameObject playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab");BoxCollider2D playerBody=playerPrefab.GetComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();RaycastHit2D floor=Physics2D.Raycast(new Vector2(spawn.position.x,20f),Vector2.down,100f,1<<6);
        if(floor.collider!=null){float feet=(playerBody.offset.y-playerBody.size.y/2f)*playerPrefab.transform.localScale.y;spawn.position=new Vector3(spawn.position.x,floor.point.y-feet+0.05f,0f);}
        Ref(stage,"playerManager",players);Ref(stage,"enemyManager",enemies);Ref(stage,"ui",ui);Ref(stage,"CM",cm);Ref(stage,"tran",spawn);Ref(stage,"exitLocation",Named("exit").transform);
        Ref(stage,"exit",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/UI/exit.prefab").GetComponent<ExitManager>());
        Int(stage,"maxWave",4);Ints(stage,"leftEnemyMax",3,4,7,7);Ints(stage,"rightEnemyMax",3,5,6,8);Num(stage,"enemyDelay",10f);Num(stage,"firstEnemyDelay",5f);Num(stage,"enemyMax",6f);
        stage.ResolveSceneReferences();Require(stage.ValidateConfiguration(out string error),"Level6: "+error);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);
    }
    public static void ValidatePrefabs()
    {
        foreach(string path in new[]{EnemyPath,WavePath})
        {
            GameObject root=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(root!=null,path);
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,path+" missing script");
            foreach(Component c in root.GetComponentsInChildren<Component>(true))
            {
                SerializedProperty p=new SerializedObject(c).GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference)
                {
#pragma warning disable CS0618
                    Require(p.objectReferenceValue!=null || p.objectReferenceInstanceIDValue==0,path+" "+p.propertyPath);
#pragma warning restore CS0618
                }
            }
        }
        GameObject enemy=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);Require(enemy.GetComponent<Health>().BeginBlood==150f,"HP 150");Require(new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue==20f,"XP 20");
        Require(enemy.GetComponentsInChildren<GreenHammerAttackBox>(true).Length==3,"three root attack boxes");Require(enemy.GetComponentInChildren<BossUI>(true)==null,"no boss UI");
    }
    private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}

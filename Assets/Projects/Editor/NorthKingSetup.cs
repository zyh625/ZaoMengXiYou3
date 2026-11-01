using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using zmxy.GamePlay;
using Object=UnityEngine.Object;

public static class NorthKingSetup
{
    public const string BossPath="Assets/Projects/Prefabs/Enemy/NorthKing.prefab";
    public const string RibPath="Assets/Projects/Prefabs/SeparateEffort/NorthKingRib.prefab";
    public const string RainPath="Assets/Projects/Prefabs/SeparateEffort/NorthKingRain.prefab";
    public const string ShieldPath="Assets/Projects/Prefabs/SeparateEffort/NorthKingShield.prefab";
    public const string ScenePath="Assets/Projects/Scenes/Levels/Level6Scene.unity";
    public static string Audit=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/NorthKingAudit"));
    public static readonly string[] Actions={"Idle","Walk","UmbrellaBash","UmbrellaVortex","RibVolley","RainCurtain","Guard","Hit","Death"};
    public static readonly string[] EffectActions={"NorthKingUmbrellaRib","NorthKingRainCurtain","NorthKingShield"};
    public static string ArtPath(string name)=>"Assets/Art/Bosses/NorthKing/SpriteSheets16/"+(EffectActions.Contains(name)?"Effects/":"")+name+".png";
    public static Sprite[] Frames(string name)=>AssetDatabase.LoadAllAssetsAtPath(ArtPath(name)).OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
    private static void Edit(Object o,Action<SerializedObject> edit){SerializedObject so=new(o);edit(so);so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Ref(Object o,string name,Object value)=>Edit(o,so=>so.FindProperty(name).objectReferenceValue=value);
    private static void Num(Object o,string name,float value)=>Edit(o,so=>so.FindProperty(name).floatValue=value);
    private static void Int(Object o,string name,int value)=>Edit(o,so=>so.FindProperty(name).intValue=value);
    private static GameObject Child(GameObject root,string name,int layer=0){GameObject go=new(name){layer=layer};go.transform.SetParent(root.transform,false);return go;}
    private static void Clip(Object o,string field,string action,float fps,bool loop,int start=0,int count=16)=>Edit(o,so=>
    {
        Sprite[] frames=Frames(action);SerializedProperty clip=so.FindProperty(field),array=clip.FindPropertyRelative("sprites");array.arraySize=count;
        for(int i=0;i<count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=frames[start+i];clip.FindPropertyRelative("fps").floatValue=fps;clip.FindPropertyRelative("loop").boolValue=loop;
    });
    private static GameObject Reference=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/WestKing.prefab");
    private static SpriteManager Visual(GameObject root,string action,out SpriteRenderer sr)
    {
        GameObject go=Child(root,"Visual");sr=go.AddComponent<SpriteRenderer>();SpriteRenderer reference=Reference.GetComponentInChildren<SpriteRenderer>(true);
        sr.sharedMaterial=reference.sharedMaterial;sr.sortingLayerID=reference.sortingLayerID;sr.sortingOrder=3;sr.sprite=Frames(action)[0];SpriteManager sm=go.AddComponent<SpriteManager>();Ref(sm,"sr",sr);return sm;
    }
    [MenuItem("造梦西游/NorthKing/生成多闻天王并接通第六关")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Audit);
        if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        if(!Application.isBatchMode && SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("请先打开Level6Scene。");
        Scene previous=SceneManager.GetActiveScene(),scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);ValidateSprites();Effects();Boss();WireScene();ValidateReferences();
            File.WriteAllText(Path.Combine(Audit,"unity-assets.txt"),"PASS: nine character actions and three effects, 16 valid frames each, original PNG/meta retained, four complete prefabs, HP1400/XP240/boss, no missing references. Level6 NorthKing, NorthBossUI, four waves, grounded player/Boss/exit, enabled managers.\n");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();Debug.Log("NORTHKING_ASSETS_PASS");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scratch,true);}
    }
    public static void ValidateSprites()
    {
        foreach(string name in Actions.Concat(EffectActions))
        {
            Sprite[] frames=Frames(name);Require(frames.Length==16,name+" frame count");TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(ArtPath(name));Require(importer.spriteImportMode==SpriteImportMode.Multiple && importer.spritePixelsPerUnit==100f,name+" Multiple / PPU");
            Texture2D t=new(2,2);t.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"../"+ArtPath(name))));Color32[] pixels=t.GetPixels32();Require(t.width==1024 && t.height==(Actions.Contains(name)?1536:1024),name+" dimensions");
            for(int i=0;i<16;i++)
            {
                Sprite frame=frames[i];Rect r=frame.rect;Require(frame.name==name+"_"+(i+1).ToString("00"),name+" names");Require(r.x>=0 && r.y>=0 && r.xMax<=t.width && r.yMax<=t.height,name+" bounds");
                if(Actions.Contains(name))Require(r.width==256f && r.height==384f && Vector2.Distance(frame.pivot,new Vector2(128f,56f))<0.001f,name+" ground pivot");
                bool visible=false;for(int y=(int)r.yMin;y<(int)r.yMax&&!visible;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)if(pixels[y*t.width+x].a>=2){visible=true;break;}
                Require(visible && frame.vertices.Length>=3,frame.name+" empty");
            }
            Object.DestroyImmediate(t);
        }
    }
    private static void Effects()
    {
        GameObject root=new("NorthKingRib");SpriteManager sm=Visual(root,"NorthKingUmbrellaRib",out SpriteRenderer sr);sr.transform.localScale=Vector3.one*0.65f;
        NorthKingRib rib=root.AddComponent<NorthKingRib>();Ref(rib,"spriteManager",sm);Ref(rib,"visual",sr);Clip(rib,"flight","NorthKingUmbrellaRib",16f,true);PrefabUtility.SaveAsPrefabAsset(root,RibPath);Object.DestroyImmediate(root);
        root=new("NorthKingRain");sm=Visual(root,"NorthKingRainCurtain",out sr);sr.transform.localScale=Vector3.one*0.75f;
        NorthKingRain rain=root.AddComponent<NorthKingRain>();Ref(rain,"spriteManager",sm);Clip(rain,"falling","NorthKingRainCurtain",16f,true);Clip(rain,"impact","NorthKingRainCurtain",20f,false);PrefabUtility.SaveAsPrefabAsset(root,RainPath);Object.DestroyImmediate(root);
        root=new("NorthKingShield");sm=Visual(root,"NorthKingShield",out sr);sr.transform.localScale=new Vector3(0.8f,1.3f,1f);
        NorthKingShield shield=root.AddComponent<NorthKingShield>();Ref(shield,"spriteManager",sm);Clip(shield,"shield","NorthKingShield",12f,true);PrefabUtility.SaveAsPrefabAsset(root,ShieldPath);Object.DestroyImmediate(root);
    }
    private static void Boss()
    {
        GameObject root=new("NorthKing"){layer=11};Rigidbody2D rb=root.AddComponent<Rigidbody2D>();rb.gravityScale=0.9f;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        BoxCollider2D physical=root.AddComponent<BoxCollider2D>();physical.size=new Vector2(1.4f,2.65f);physical.offset=Vector2.up*1.325f;
        Health health=root.AddComponent<Health>();Num(health,"beginBlood",1400f);EditorUtility.CopySerialized(Reference.GetComponent<ActorStatus>(),root.GetComponent<ActorStatus>());
        EnemyRewards rewards=root.AddComponent<EnemyRewards>();Num(rewards,"experienceReward",240f);Edit(rewards,so=>so.FindProperty("isBoss").boolValue=true);
        NorthMovement movement=root.AddComponent<NorthMovement>();Num(movement,"moveSpeed",1f);Num(movement,"slowSpeed",0.5f);Num(movement,"backSpeed",3.5f);Num(movement,"upSpeed",6f);Num(movement,"boostRatio",1f);Num(movement,"minNormalY",0.6f);Int(movement,"groundLayer",1<<6);
        NorthEffects effects=root.AddComponent<NorthEffects>();Ref(effects,"ribPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(RibPath).GetComponent<NorthKingRib>());Ref(effects,"rainPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(RainPath).GetComponent<NorthKingRain>());Ref(effects,"shieldPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPath).GetComponent<NorthKingShield>());
        NorthCombat combat=root.AddComponent<NorthCombat>();Num(combat,"attackInterval",2.2f);NorthController controller=root.AddComponent<NorthController>();Num(controller,"stopDistance",3.8f);
        Visual(root,"Idle",out SpriteRenderer sr);sr.sortingOrder=2;sr.transform.localScale=new Vector3(-1f,1f,1f);sr.gameObject.AddComponent<SpriteOutlineController>();
        GameObject boxes=Child(root,"Combat"),hurt=Child(boxes,"Attacked",7);BoxCollider2D hurtbox=hurt.AddComponent<BoxCollider2D>();hurtbox.size=new Vector2(1.55f,2.8f);hurtbox.offset=Vector2.up*1.4f;hurtbox.isTrigger=true;Ref(controller,"attackedBox",hurtbox);
        GameObject hit=Child(boxes,"UmbrellaBash",8);BoxCollider2D attack=hit.AddComponent<BoxCollider2D>();attack.isTrigger=true;attack.size=new Vector2(4f,2.2f);attack.offset=new Vector2(2.7f,1.2f);NorthBashBox bash=hit.AddComponent<NorthBashBox>();Num(bash,"Attack",25f);Edit(bash,so=>so.FindProperty("knockback").boolValue=true);Ref(combat,"bash",bash);hit.SetActive(false);
        GameObject check=Child(boxes,"BashCheck",10);BoxCollider2D detector=check.AddComponent<BoxCollider2D>();detector.isTrigger=true;detector.size=new Vector2(10f,4f);detector.offset=Vector2.up*1.5f;EnemyCheckAttack checker=check.AddComponent<EnemyCheckAttack>();
        Edit(combat,so=>
        {
            foreach(string field in new[]{"AttackBoxes","attackDelay","CheckAttackLogic","AttackOpenIdx","AttackShutIdx"})so.FindProperty(field).arraySize=1;
            so.FindProperty("AttackBoxes").GetArrayElementAtIndex(0).objectReferenceValue=attack;so.FindProperty("CheckAttackLogic").GetArrayElementAtIndex(0).objectReferenceValue=checker;
            so.FindProperty("attackDelay").GetArrayElementAtIndex(0).floatValue=4f;so.FindProperty("AttackOpenIdx").GetArrayElementAtIndex(0).intValue=5;so.FindProperty("AttackShutIdx").GetArrayElementAtIndex(0).intValue=9;
            SerializedProperty cds=so.FindProperty("cooldowns");cds.arraySize=4;float[] values={4f,9f,8f,12f};for(int i=0;i<4;i++)cds.GetArrayElementAtIndex(i).floatValue=values[i];
        });
        Transform muzzle=Child(root,"UmbrellaMuzzle").transform;muzzle.localPosition=new Vector3(0.85f,1.65f);Ref(combat,"muzzle",muzzle);
        Clip(controller,"Idle","Idle",10f,true);Clip(controller,"Walk","Walk",14f,true);Clip(controller,"Attacked","Hit",18f,false);Clip(controller,"Die","Death",12f,false);
        foreach(string action in new[]{"UmbrellaBash","UmbrellaVortex","RibVolley","RainCurtain","Guard"})Clip(controller,action,action,16f,false);
        Clip(controller,"VortexSpin","UmbrellaVortex",16f,true,4,8);Clip(controller,"VortexRecovery","UmbrellaVortex",16f,false,12,4);
        PrefabUtility.SaveAsPrefabAsset(root,BossPath);Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        T[] All<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        StageEnemyManager enemies=All<StageEnemyManager>().First();LevelManager stage=All<LevelManager>().First();enemies.enabled=stage.enabled=true;
        Ref(enemies,"boss",AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).transform);
        SerializedObject e=new(enemies),s=new(stage);Transform born=(Transform)e.FindProperty("bossBornLocation").objectReferenceValue;Require(born!=null,"existing Boss spawn");
        CameraManager camera=All<CameraManager>().First();Require(camera.ValidateConfiguration(out string cameraError),cameraError);SerializedObject c=new(camera);int last=c.FindProperty("cameraEdgeLeft").arraySize-1;Require(last==3,"four camera waves");
        Transform left=(Transform)c.FindProperty("cameraEdgeLeft").GetArrayElementAtIndex(last).objectReferenceValue,right=(Transform)c.FindProperty("cameraEdgeRight").GetArrayElementAtIndex(last).objectReferenceValue;
        GameObject bounds=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="NorthKingArena");if(bounds==null){bounds=new GameObject("NorthKingArena");SceneManager.MoveGameObjectToScene(bounds,scene);}
        Transform limitL=bounds.transform.Find("Left"),limitR=bounds.transform.Find("Right");if(limitL==null)limitL=Child(bounds,"Left").transform;if(limitR==null)limitR=Child(bounds,"Right").transform;
        float half=camera.GetComponent<Camera>().orthographicSize*16f/9f;limitL.position=Vector3.right*(left.position.x-half);limitR.position=Vector3.right*(right.position.x+half);Ref(enemies,"lungeLeft",limitL);Ref(enemies,"lungeRight",limitR);
        BossUI old=All<BossUI>().First();GameObject panel=old.gameObject;SerializedObject ui=new(old);Image blood=(Image)ui.FindProperty("bloodFull").objectReferenceValue,special=(Image)ui.FindProperty("specialState").objectReferenceValue;Require(blood!=null && special!=null,"blood and SpecialState images");
        NorthBossUI north=old as NorthBossUI;if(north==null){Object.DestroyImmediate(old);north=panel.AddComponent<NorthBossUI>();}Ref(north,"bloodFull",blood);Ref(north,"specialState",special);Ref(stage,"bossUI",north);
        Sprite portrait=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Bosses/DuoWenBlood.png");Require(portrait!=null,"existing NorthKing portrait");
        foreach(Image image in panel.GetComponentsInChildren<Image>(true))if(image.sprite!=null && image.sprite.name.EndsWith("Blood"))image.sprite=portrait;
        Physics2D.SyncTransforms();
        void Ground(Transform point,GameObject prefab)
        {
            RaycastHit2D floor=Physics2D.Raycast(new Vector2(point.position.x,100f),Vector2.down,200f,1<<6);Require(floor.collider!=null,"ground under "+point.name);
            BoxCollider2D box=prefab.GetComponent<BoxCollider2D>();float feet=(box.offset.y-box.size.y/2f)*prefab.transform.localScale.y;point.position=new Vector3(point.position.x,floor.point.y-feet+0.025f,point.position.z);
        }
        Transform playerSpawn=(Transform)s.FindProperty("tran").objectReferenceValue,exitPoint=(Transform)s.FindProperty("exitLocation").objectReferenceValue;
        Ground(born,AssetDatabase.LoadAssetAtPath<GameObject>(BossPath));Ground(playerSpawn,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"));
        ExitManager exit=(ExitManager)s.FindProperty("exit").objectReferenceValue;Ground(exitPoint,exit.gameObject);
        //只有本场景实例：布局/BattleUI重复带EventSystem时保留UI的一套，不改共享UI预制体。
        var eventSystems=All<UnityEngine.EventSystems.EventSystem>();var chosen=eventSystems.FirstOrDefault(x=>PrefabUtility.GetOutermostPrefabInstanceRoot(x.gameObject)?.GetComponentInChildren<UIManager>(true)!=null)??eventSystems.FirstOrDefault();foreach(var system in eventSystems)system.gameObject.SetActive(system==chosen);
        stage.ResolveSceneReferences();Require(stage.ValidateConfiguration(out string error),error);
        foreach(Transform node in All<Transform>())Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"Level6 missing script: "+node.name);
        File.WriteAllText(Path.Combine(Audit,"scene-wiring.txt"),$"Enabled {stage.GetType().Name} / {enemies.GetType().Name}; four waves; pool entries preserved; NorthKing boss; existing spawn {born.name} {born.position}; player {playerSpawn.position}; exit {exitPoint.position}; arena {limitL.position.x} .. {limitR.position.x}; NorthBossUI blood / durability images bound.\n");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);
    }
    public static void ValidateReferences()
    {
        foreach(string path in new[]{BossPath,RibPath,RainPath,ShieldPath})
        {
            GameObject root=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(root!=null,path);
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,path+" missing script");
            foreach(Component component in root.GetComponentsInChildren<Component>(true))
            {
                SerializedProperty p=new SerializedObject(component).GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference)
                {
#pragma warning disable CS0618
                    Require(p.objectReferenceValue!=null || p.objectReferenceInstanceIDValue==0,path+" "+p.propertyPath);
#pragma warning restore CS0618
                }
            }
        }
        GameObject boss=AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);Require(boss.GetComponent<Health>().BeginBlood==1400f,"HP1400");SerializedObject rewards=new(boss.GetComponent<EnemyRewards>());Require(rewards.FindProperty("experienceReward").floatValue==240f && rewards.FindProperty("isBoss").boolValue,"XP240 / isBoss");
    }
    private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}

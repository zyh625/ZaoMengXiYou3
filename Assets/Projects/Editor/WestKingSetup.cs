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

public static class WestKingSetup
{
    public const string BossPath="Assets/Projects/Prefabs/Enemy/WestKing.prefab";
    public const string PearlPath="Assets/Projects/Prefabs/SeparateEffort/WestKingPearl.prefab";
    public const string FlamePath="Assets/Projects/Prefabs/SeparateEffort/WestKingFlame.prefab";
    public const string OrbPath="Assets/Projects/Prefabs/SeparateEffort/WestKingOrb.prefab";
    public const string ScenePath="Assets/Projects/Scenes/Levels/Level5Scene.unity";
    public static readonly string[] Actions={"Idle","Walk","DragonClaw","PearlToss","DragonBreath","PearlOrbit","Ward","HitReaction","Death","DragonFlame","PearlProjectile","OrbSprites"};
    public static string Audit=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/WestKingAudit"));
    public static string ArtPath(string action)=>"Assets/Art/Bosses/WestKing/"+(action=="OrbSprites"?"":action=="DragonFlame"||action=="PearlProjectile"?"SpriteSheets16/Effects/":"SpriteSheets16/")+action+".png";
    public static Sprite[] Frames(string action)=>AssetDatabase.LoadAllAssetsAtPath(ArtPath(action)).OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
    private static void Edit(Object o,Action<SerializedObject> edit){SerializedObject so=new(o);edit(so);so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Ref(Object o,string name,Object value)=>Edit(o,so=>so.FindProperty(name).objectReferenceValue=value);
    private static void Num(Object o,string name,float value)=>Edit(o,so=>so.FindProperty(name).floatValue=value);
    private static void Int(Object o,string name,int value)=>Edit(o,so=>so.FindProperty(name).intValue=value);
    private static void Flag(Object o,string name,bool value)=>Edit(o,so=>so.FindProperty(name).boolValue=value);
    private static GameObject Child(GameObject root,string name,int layer=0){GameObject c=new(name){layer=layer};c.transform.SetParent(root.transform,false);return c;}
    private static void Clip(Object o,string field,string action,float fps,bool loop,int start=0,int count=16)=>Edit(o,so=>
    {
        SerializedProperty clip=so.FindProperty(field),array=clip.FindPropertyRelative("sprites");Sprite[] frames=Frames(action);array.arraySize=count;
        for(int i=0;i<count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=frames[start+i];
        clip.FindPropertyRelative("fps").floatValue=fps;clip.FindPropertyRelative("loop").boolValue=loop;
    });
    private static void EmptyCombat(EnemyCombat c)
    {foreach(string n in new[]{"AttackBoxes","attackDelay","CheckAttackLogic","AttackOpenIdx","AttackShutIdx"})Edit(c,so=>so.FindProperty(n).arraySize=0);}
    private static Material BodyMaterial=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/EastKing.prefab").GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
    private static SpriteManager Visual(GameObject root,string action,out SpriteRenderer renderer)
    {
        GameObject go=Child(root,"Visual");renderer=go.AddComponent<SpriteRenderer>();renderer.sharedMaterial=BodyMaterial;renderer.sortingOrder=3;renderer.sprite=Frames(action)[0];
        SpriteManager sm=go.AddComponent<SpriteManager>();Ref(sm,"sr",renderer);return sm;
    }
    private static LineRenderer Ray(GameObject root)
    {
        const string path="Assets/Projects/Prefabs/SeparateEffort/WestKingWarning.mat";
        Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")){name="WestKingWarning"};mat.SetTexture("_MainTex",Texture2D.whiteTexture);AssetDatabase.CreateAsset(mat,path);}
        LineRenderer ray=Child(root,"RayWarning").AddComponent<LineRenderer>();ray.sharedMaterial=mat;ray.positionCount=2;ray.useWorldSpace=true;ray.sortingOrder=10;ray.numCapVertices=4;ray.enabled=false;return ray;
    }
    private static void Status(GameObject root)=>EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/EastKing.prefab").GetComponent<ActorStatus>(),root.GetComponent<ActorStatus>()??root.AddComponent<ActorStatus>());
    private static void Motion(EnemyMovement movement,float speed)
    {
        Num(movement,"moveSpeed",speed);Num(movement,"slowSpeed",speed/2f);Num(movement,"backSpeed",3.5f);Num(movement,"upSpeed",6f);Num(movement,"boostRatio",1f);Num(movement,"minNormalY",0.6f);Int(movement,"groundLayer",1<<6);
    }
    [MenuItem("造梦西游/WestKing/生成并验证广目天王")]
    public static void BuildAndValidate()
    {
        if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Directory.CreateDirectory(Audit);Scene previous=SceneManager.GetActiveScene();Scene scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);ValidateSprites();Effects();Boss();WireScene();ValidateReferences();
            File.WriteAllText(Path.Combine(Audit,"unity-assets.txt"),"PASS: 192 sprites; four prefabs; HP 1100, XP 220, boss flag; no missing references; Level5 WestBossUI and Boss wiring.\n");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();Debug.Log("WESTKING_ASSETS_PASS");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scratch,true);}
    }
    public static void ValidateSprites()
    {
        foreach(string action in Actions)
        {
            Sprite[] frames=Frames(action);Require(frames.Length==16,action+" count");
            Texture2D texture=new(2,2);texture.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"../"+ArtPath(action))));Color32[] pixels=texture.GetPixels32();
            Require(texture.width==1024 && texture.height==1536,action+" dimensions");
            for(int i=0;i<16;i++)
            {
                Sprite sprite=frames[i];Rect r=sprite.rect;Require(sprite.name==action+"_"+i && sprite.pixelsPerUnit==100f,action+" names / PPU");
                Require(r.xMin>=0 && r.yMin>=0 && r.xMax<=1024 && r.yMax<=1536,action+" bounds");
                bool visible=false;for(int y=(int)r.yMin;y<(int)r.yMax&&!visible;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)if(pixels[y*1024+x].a>=2){visible=true;break;}
                Require(visible && sprite.vertices.Length>=3 && sprite.triangles.Length>=3,sprite.name+" empty sprite / mesh");
            }
            Object.DestroyImmediate(texture);
        }
    }
    private static void Effects()
    {
        GameObject root=new("WestKingPearl");SpriteManager sm=Visual(root,"PearlProjectile",out SpriteRenderer sr);WestKingPearl pearl=root.AddComponent<WestKingPearl>();
        Ref(pearl,"spriteManager",sm);Ref(pearl,"visual",sr);Clip(pearl,"flight","PearlProjectile",16f,true);PrefabUtility.SaveAsPrefabAsset(root,PearlPath);Object.DestroyImmediate(root);
        root=new("WestKingFlame");sm=Visual(root,"DragonFlame",out sr);WestKingFlame flame=root.AddComponent<WestKingFlame>();Ref(flame,"spriteManager",sm);Ref(flame,"visual",sr);
        Clip(flame,"stream","DragonFlame",16f,true,4,8);Clip(flame,"ground","DragonFlame",12f,true,4,8);Clip(flame,"burst","DragonFlame",40f,false);
        Num(flame,"length",7f);Num(flame,"nativeWidth",Frames("DragonFlame").Max(s=>s.rect.width)/100f);
        PrefabUtility.SaveAsPrefabAsset(root,FlamePath);Object.DestroyImmediate(root);
        root=new("WestKingOrb"){layer=11};Rigidbody2D body=root.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.gravityScale=0f;body.constraints=RigidbodyConstraints2D.FreezeAll;
        BoxCollider2D box=root.AddComponent<BoxCollider2D>();box.size=new Vector2(0.9f,0.9f);box.isTrigger=true;
        Health hp=root.AddComponent<Health>();Num(hp,"beginBlood",80f);Status(root);Motion(root.AddComponent<EnemyMovement>(),0f);EmptyCombat(root.AddComponent<BossCombat>());
        sm=Visual(root,"OrbSprites",out sr);sr.transform.localScale=Vector3.one*0.65f;
        WestKingOrb orb=root.AddComponent<WestKingOrb>();Transform muzzle=Child(root,"RayMuzzle").transform;Ref(orb,"muzzle",muzzle);Ref(orb,"ray",Ray(root));
        GameObject hurt=Child(root,"Attacked",7);BoxCollider2D hurtBox=hurt.AddComponent<BoxCollider2D>();hurtBox.size=new Vector2(0.95f,0.95f);hurtBox.isTrigger=true;Ref(orb,"attackedBox",hurtBox);
        foreach(string field in new[]{"Idle","Walk","Attacked","Die"})Clip(orb,field,"OrbSprites",12f,field=="Idle"||field=="Walk");
        PrefabUtility.SaveAsPrefabAsset(root,OrbPath);Object.DestroyImmediate(root);
    }
    private static void Boss()
    {
        GameObject root=new("WestKing"){layer=11};Rigidbody2D body=root.AddComponent<Rigidbody2D>();body.gravityScale=0.9f;body.constraints=RigidbodyConstraints2D.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        BoxCollider2D shape=root.AddComponent<BoxCollider2D>();shape.size=new Vector2(1.4f,2.65f);shape.offset=new Vector2(0f,1.325f);
        Health hp=root.AddComponent<Health>();Num(hp,"beginBlood",1100f);Status(root);
        EnemyRewards rewards=root.AddComponent<EnemyRewards>();Num(rewards,"experienceReward",220f);Flag(rewards,"isBoss",true);
        WestMovement movement=root.AddComponent<WestMovement>();Motion(movement,1f);
        WestCombat combat=root.AddComponent<WestCombat>();EmptyCombat(combat);Num(combat,"attackInterval",2.2f);
        WestEffectPool pool=root.AddComponent<WestEffectPool>();Ref(pool,"pearlPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(PearlPath).GetComponent<WestKingPearl>());
        Ref(pool,"flamePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(FlamePath).GetComponent<WestKingFlame>());Ref(pool,"orbPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(OrbPath).GetComponent<WestKingOrb>());
        Visual(root,"Idle",out SpriteRenderer sr);sr.sortingOrder=2;sr.transform.localScale=new Vector3(-1f,1f,1f);sr.gameObject.AddComponent<SpriteOutlineController>();
        WestController controller=root.AddComponent<WestController>();Num(controller,"stopDistance",5.5f);
        GameObject boxes=Child(root,"Combat"),hurt=Child(boxes,"Attacked",7);BoxCollider2D hurtBox=hurt.AddComponent<BoxCollider2D>();hurtBox.size=new Vector2(1.55f,2.8f);hurtBox.offset=Vector2.up*1.4f;hurtBox.isTrigger=true;Ref(controller,"attackedBox",hurtBox);
        GameObject hit=Child(boxes,"DragonClaw",8);BoxCollider2D hitBox=hit.AddComponent<BoxCollider2D>();hitBox.size=new Vector2(7f,2.2f);hitBox.offset=new Vector2(4.2f,1.2f);hitBox.isTrigger=true;
        WestClawBox claw=hit.AddComponent<WestClawBox>();Num(claw,"Attack",25f);Flag(claw,"knockback",true);Ref(combat,"claw",claw);hit.SetActive(false);
        GameObject check=Child(boxes,"ClawCheck",10);BoxCollider2D detector=check.AddComponent<BoxCollider2D>();detector.size=new Vector2(14f,4f);detector.offset=Vector2.up*1.5f;detector.isTrigger=true;EnemyCheckAttack checker=check.AddComponent<EnemyCheckAttack>();
        Edit(combat,so=>
        {
            foreach(string name in new[]{"AttackBoxes","attackDelay","CheckAttackLogic","AttackOpenIdx","AttackShutIdx"})so.FindProperty(name).arraySize=1;
            so.FindProperty("AttackBoxes").GetArrayElementAtIndex(0).objectReferenceValue=hitBox;so.FindProperty("CheckAttackLogic").GetArrayElementAtIndex(0).objectReferenceValue=checker;
            so.FindProperty("attackDelay").GetArrayElementAtIndex(0).floatValue=4f;so.FindProperty("AttackOpenIdx").GetArrayElementAtIndex(0).intValue=11;so.FindProperty("AttackShutIdx").GetArrayElementAtIndex(0).intValue=14;
        });
        Transform muzzle=Child(root,"Muzzle").transform;muzzle.localPosition=new Vector3(0.85f,1.65f);Ref(combat,"muzzle",muzzle);
        Clip(controller,"Idle","Idle",10f,true);Clip(controller,"Walk","Walk",14f,true);Clip(controller,"Attacked","HitReaction",18f,false);Clip(controller,"Die","Death",12f,false);
        foreach(string a in new[]{"DragonClaw","PearlToss","DragonBreath","PearlOrbit","Ward"})Clip(controller,a,a,16f,false);Clip(controller,"HitReaction","HitReaction",18f,false);
        PrefabUtility.SaveAsPrefabAsset(root,BossPath);Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        T Find<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        StageEnemyManager enemies=Find<StageEnemyManager>();LevelManager stage=Find<LevelManager>();Require(enemies!=null && stage!=null,"Level5 managers");
        Ref(enemies,"boss",AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).transform);enemies.enabled=true;stage.enabled=true;
        CameraManager camera=Find<CameraManager>();SerializedObject cm=new(camera);int last=cm.FindProperty("cameraEdgeLeft").arraySize-1;
        Transform left=(Transform)cm.FindProperty("cameraEdgeLeft").GetArrayElementAtIndex(last).objectReferenceValue,right=(Transform)cm.FindProperty("cameraEdgeRight").GetArrayElementAtIndex(last).objectReferenceValue;
        float half=camera.GetComponent<Camera>().orthographicSize*16f/9f;
        GameObject bounds=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="WestKingArena");if(bounds==null){bounds=new GameObject("WestKingArena");SceneManager.MoveGameObjectToScene(bounds,scene);}
        Transform arenaLeft=bounds.transform.Find("Left"),arenaRight=bounds.transform.Find("Right");if(arenaLeft==null)arenaLeft=Child(bounds,"Left").transform;if(arenaRight==null)arenaRight=Child(bounds,"Right").transform;
        arenaLeft.position=Vector3.right*(left.position.x-half);arenaRight.position=Vector3.right*(right.position.x+half);Ref(enemies,"lungeLeft",arenaLeft);Ref(enemies,"lungeRight",arenaRight);
        BossUI old=Find<BossUI>();Require(old!=null,"BossUI");GameObject panel=old.gameObject;SerializedObject ui=new(old);
        Image blood=(Image)ui.FindProperty("bloodFull").objectReferenceValue,special=(Image)ui.FindProperty("specialState").objectReferenceValue;
        WestBossUI west=old as WestBossUI;if(west==null){Object.DestroyImmediate(old);west=panel.AddComponent<WestBossUI>();}Ref(west,"bloodFull",blood);Ref(west,"specialState",special);Ref(stage,"bossUI",west);
        Sprite portrait=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Bosses/GuangMuBlood.png");
        foreach(Image image in panel.GetComponentsInChildren<Image>(true))if(image.sprite!=null && image.sprite.name=="TitanBlood")image.sprite=portrait;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);
    }
    public static void ValidateReferences()
    {
        foreach(string path in new[]{BossPath,PearlPath,FlamePath,OrbPath})
        {
            GameObject root=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(root!=null,path);
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,path+" missing script");
            foreach(Component component in root.GetComponentsInChildren<Component>(true))
            {
                SerializedProperty p=new SerializedObject(component).GetIterator();
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference)
                {
#pragma warning disable CS0618
                    Require(p.objectReferenceValue!=null || p.objectReferenceEntityIdValue==default,path+" "+p.propertyPath);
#pragma warning restore CS0618
                }
            }
        }
        GameObject boss=AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);Require(boss.GetComponent<Health>().BeginBlood==1100f,"HP 1100");
        SerializedObject rewards=new(boss.GetComponent<EnemyRewards>());Require(rewards.FindProperty("experienceReward").floatValue==220f && rewards.FindProperty("isBoss").boolValue,"XP / boss reward");
    }
    private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}

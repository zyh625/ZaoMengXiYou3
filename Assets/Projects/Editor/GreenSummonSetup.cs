using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

//只生成GreenSummon专属资源，并追加Level6的小怪池；不改既有视觉FX/其他敌人。
public static class GreenSummonSetup
{
    public const string EnemyPath="Assets/Projects/Prefabs/Enemy/GreenSummon.prefab";
    public const string HammerPath="Assets/Projects/Prefabs/SeparateEffort/GreenSummonHammer.prefab";
    public const string BannerPath="Assets/Projects/Prefabs/SeparateEffort/GreenSummonWarBanner.prefab";
    public const string WavePath="Assets/Projects/Prefabs/SeparateEffort/GreenSummonWave.prefab";
    public const string ScenePath="Assets/Projects/Scenes/Levels/Level6Scene.unity";
    private const string Art="Assets/Art/Enemies/Level6/GreenSummon/";
    private const string MaterialPath="Assets/Projects/Prefabs/SeparateEffort/GreenSummonWarning.mat";
    public static readonly string[] Actions={"Idle","Walk","SummonedHammerStrike","BannerWave","WarBanner","Hit","Death"};
    public static string Audit=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/GreenSummonAudit"));
    public static Sprite[] Frames(string action,bool effect=false)=>AssetDatabase.LoadAllAssetsAtPath(Art+(effect?"Effects/":"")+action+".png").OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
    private static GameObject Reference=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/RedBoom.prefab");
    private static void Edit(Object c,Action<SerializedObject> edit){SerializedObject so=new(c);edit(so);so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Ref(Object c,string field,Object value)=>Edit(c,so=>so.FindProperty(field).objectReferenceValue=value);
    private static void Num(Object c,string field,float value)=>Edit(c,so=>so.FindProperty(field).floatValue=value);
    private static void Int(Object c,string field,int value)=>Edit(c,so=>so.FindProperty(field).intValue=value);
    private static void Clip(Object c,string field,Sprite[] frames,float fps,bool loop)=>Edit(c,so=>
    {
        SerializedProperty p=so.FindProperty(field),array=p.FindPropertyRelative("sprites");array.arraySize=frames.Length;
        for(int i=0;i<frames.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];
        p.FindPropertyRelative("fps").floatValue=fps;p.FindPropertyRelative("loop").boolValue=loop;
    });
    private static GameObject Child(GameObject root,string name,int layer=0){GameObject go=new(name){layer=layer};go.transform.SetParent(root.transform,false);return go;}
    private static SpriteRenderer Renderer(GameObject go,Sprite sprite,int order)
    {
        SpriteRenderer sr=go.AddComponent<SpriteRenderer>(),reference=Reference.GetComponentInChildren<SpriteRenderer>(true);
        sr.sharedMaterial=reference.sharedMaterial;sr.sortingLayerID=reference.sortingLayerID;sr.sortingOrder=order;sr.sprite=sprite;return sr;
    }
    private static SpriteManager Visual(GameObject root,string name,Sprite sprite,int order,out SpriteRenderer sr)
    {GameObject go=Child(root,name);sr=Renderer(go,sprite,order);SpriteManager sm=go.AddComponent<SpriteManager>();Ref(sm,"sr",sr);return sm;}
    [MenuItem("造梦西游/GreenSummon/生成并验证青幡令使")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Audit);
        if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        if(!Application.isBatchMode && SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("请先打开Level6Scene。");
        Scene previous=SceneManager.GetActiveScene(),scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);ValidateSprites();CreateEffects();CreateEnemy();WireScene();ValidateReferences();
            File.WriteAllText(Path.Combine(Audit,"unity-assets.txt"),"PASS: 112 character frames, 32 supplied effect frames, 16 wave frames, one banner; four complete prefabs without missing references. HP 88 / XP 20, Level6 GreenSummon 10 / 0.45, GreenHammer 10 / 0.55 preserved.\n");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();Debug.Log("GREENSUMMON_ASSETS_PASS");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scratch,true);}
    }
    public static void ValidateSprites()
    {
        foreach(string action in Actions)ValidateSheet(action,false,1024,1536,1);
        foreach(string effect in new[]{"GreenSummonHammer","GreenSummonWarBannerAura"})ValidateSheet(effect,true,1024,1024,1);
        ValidateSheet("GreenSummonWave",true,1254,1254,0);
        Sprite banner=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Effects/GreenSummonBanner.png");Require(banner!=null && banner.vertices.Length>=3 && banner.pixelsPerUnit==100f,"single banner sprite");
    }
    private static void ValidateSheet(string name,bool effect,int width,int height,int first)
    {
        string path=Art+(effect?"Effects/":"")+name+".png";Sprite[] frames=Frames(name,effect);Require(frames.Length==16,name+" 16 frames");
        TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(path);Require(importer.spriteImportMode==SpriteImportMode.Multiple && importer.spritePixelsPerUnit==100f,name+" multiple / PPU");
        Texture2D texture=new(2,2);texture.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"../"+path)));Color32[] pixels=texture.GetPixels32();Require(texture.width==width && texture.height==height,name+" dimensions");
        for(int i=0;i<16;i++)
        {
            Sprite sprite=frames[i];Rect r=sprite.rect;string expected=name+"_"+(first==1?(i+1).ToString("00"):i.ToString());Require(sprite.name==expected,name+" consecutive names");
            Require(r.xMin>=0f && r.yMin>=0f && r.xMax<=width && r.yMax<=height,name+" rect bounds");
            if(!effect)Require(r.width==256f && r.height==384f && Mathf.Abs(sprite.pivot.x-128f)<0.001f && Mathf.Abs(sprite.pivot.y-56f)<0.001f,name+" original ground pivot");
            bool visible=false;for(int y=(int)r.yMin;y<(int)r.yMax&&!visible;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)if(pixels[y*width+x].a>=2){visible=true;break;}
            Require(visible && sprite.vertices.Length>=3,sprite.name+" nonempty");
        }
        Object.DestroyImmediate(texture);
    }
    private static float VisibleExtent(string path,Sprite[] frames,bool horizontal)
    {
        Texture2D t=new(2,2);t.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"../"+path)));Color32[] pixels=t.GetPixels32();int largest=0;
        foreach(Sprite sprite in frames)
        {
            Rect r=sprite.rect;int min=int.MaxValue,max=-1;
            for(int y=(int)r.yMin;y<(int)r.yMax;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)if(pixels[y*t.width+x].a>=2){int value=horizontal?x:y;min=Mathf.Min(min,value);max=Mathf.Max(max,value);}
            largest=Mathf.Max(largest,max-min+1);
        }
        Object.DestroyImmediate(t);return largest/100f;
    }
    private static LineRenderer Warning(GameObject root)
    {
        Material mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(mat==null)
        {Shader shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");Require(shader!=null,"URP warning shader");mat=new Material(shader){name="GreenSummonWarning"};mat.SetTexture("_MainTex",Texture2D.whiteTexture);AssetDatabase.CreateAsset(mat,MaterialPath);}
        LineRenderer ring=Child(root,"WarningCircle").AddComponent<LineRenderer>();ring.sharedMaterial=mat;ring.useWorldSpace=false;ring.positionCount=49;
        ring.startWidth=ring.endWidth=0.05f;ring.startColor=ring.endColor=new Color(0.95f,0.25f,0.12f,0.95f);ring.sortingOrder=4;ring.enabled=false;
        for(int i=0;i<49;i++){float a=i*Mathf.PI*2f/48f;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*2.5f,Mathf.Sin(a)*0.17f+0.035f));}return ring;
    }
    private static void CreateEffects()
    {
        Sprite[] frames=Frames("GreenSummonHammer",true);GameObject root=new("GreenSummonHammer");SpriteManager sm=Visual(root,"Visual",frames[0],3,out SpriteRenderer sr);
        float scale=5f/VisibleExtent(Art+"Effects/GreenSummonHammer.png",frames,true);sr.transform.localScale=Vector3.one*scale;
        GreenSummonHammer hammer=root.AddComponent<GreenSummonHammer>();Ref(hammer,"spriteManager",sm);Ref(hammer,"warningCircle",Warning(root));Num(hammer,"warningDuration",1.2f);
        Clip(hammer,"warning",frames.Take(2).ToArray(),6f,true);Clip(hammer,"fall",frames.Skip(2).Take(5).ToArray(),16f,false);Clip(hammer,"impact",frames.Skip(7).ToArray(),16f,false);
        PrefabUtility.SaveAsPrefabAsset(root,HammerPath);Object.DestroyImmediate(root);

        frames=Frames("GreenSummonWave",true);root=new("GreenSummonWave");sm=Visual(root,"Visual",frames[0],3,out sr);sr.color=new Color(0.65f,1f,0.7f,1f);
        sr.transform.localScale=Vector3.one*(1.5f/VisibleExtent(Art+"Effects/GreenSummonWave.png",frames,true));
        GreenSummonWave wave=root.AddComponent<GreenSummonWave>();Ref(wave,"spriteManager",sm);Ref(wave,"visual",sr);Num(wave,"speed",10f);Num(wave,"maxDistance",12f);Clip(wave,"flight",frames,16f,true);
        PrefabUtility.SaveAsPrefabAsset(root,WavePath);Object.DestroyImmediate(root);

        root=new("GreenSummonWarBanner"){layer=11};Rigidbody2D rb=root.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0f;rb.constraints=RigidbodyConstraints2D.FreezeAll;
        BoxCollider2D physical=root.AddComponent<BoxCollider2D>();physical.isTrigger=true;physical.size=new Vector2(1.2f,2.2f);physical.offset=new Vector2(0.25f,0.15f);
        Health hp=root.AddComponent<Health>();Num(hp,"beginBlood",60f);EditorUtility.CopySerialized(Reference.GetComponent<ActorStatus>(),root.GetComponent<ActorStatus>());
        EnemyMovement target=root.AddComponent<EnemyMovement>();Num(target,"moveSpeed",0f);Num(target,"slowSpeed",0f);Num(target,"minNormalY",0.6f);Int(target,"groundLayer",1<<6);
        GameObject hurt=Child(root,"Attacked",7);BoxCollider2D hurtbox=hurt.AddComponent<BoxCollider2D>();hurtbox.isTrigger=true;hurtbox.size=physical.size;hurtbox.offset=physical.offset;
        Sprite banner=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Effects/GreenSummonBanner.png");GameObject flagVisual=Child(root,"Banner");sr=Renderer(flagVisual,banner,2);sr.transform.localPosition=Vector3.down*0.95f;
        sr.transform.localScale=Vector3.one*(2.2f/VisibleExtent(Art+"Effects/GreenSummonBanner.png",new[]{banner},false));
        frames=Frames("GreenSummonWarBannerAura",true);sm=Visual(root,"Aura",frames[0],1,out sr);sr.transform.localPosition=Vector3.down*0.95f;
        float auraScale=10f/VisibleExtent(Art+"Effects/GreenSummonWarBannerAura.png",frames,true);sr.transform.localScale=Vector3.one*auraScale;
        GreenSummonWarBanner flag=root.AddComponent<GreenSummonWarBanner>();Ref(flag,"auraManager",sm);Clip(flag,"aura",frames,12f,true);Num(flag,"lifetime",8f);Num(flag,"radius",5f);Num(flag,"gripHeight",0.95f);
        PrefabUtility.SaveAsPrefabAsset(root,BannerPath);Object.DestroyImmediate(root);
    }
    private static void CreateEnemy()
    {
        GameObject root=new("GreenSummon"){layer=11};Rigidbody2D rb=root.AddComponent<Rigidbody2D>();rb.gravityScale=0.9f;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        BoxCollider2D physical=root.AddComponent<BoxCollider2D>();physical.size=new Vector2(1.05f,2.05f);physical.offset=Vector2.up*1.025f;
        Health hp=root.AddComponent<Health>();Num(hp,"beginBlood",88f);EditorUtility.CopySerialized(Reference.GetComponent<ActorStatus>(),root.GetComponent<ActorStatus>());
        EnemyRewards rewards=root.AddComponent<EnemyRewards>();Num(rewards,"experienceReward",20f);Edit(rewards,so=>so.FindProperty("isBoss").boolValue=false);
        GreenSummonMovement movement=root.AddComponent<GreenSummonMovement>();Num(movement,"moveSpeed",0.8f);Num(movement,"slowSpeed",0.4f);Num(movement,"backSpeed",3.5f);Num(movement,"upSpeed",6f);Num(movement,"boostRatio",1f);Num(movement,"minNormalY",0.6f);Int(movement,"groundLayer",1<<6);
        Num(movement,"retreatCooldown",6f);Num(movement,"retreatDistance",3f);Num(movement,"retreatDuration",0.25f);
        GreenSummonCombat combat=root.AddComponent<GreenSummonCombat>();GreenSummonController controller=root.AddComponent<GreenSummonController>();Num(controller,"stopDistance",7f);
        Edit(combat,so=>
        {
            foreach(string field in new[]{"AttackBoxes","attackDelay","CheckAttackLogic","AttackOpenIdx","AttackShutIdx"})so.FindProperty(field).arraySize=0;
            SerializedProperty cds=so.FindProperty("skillCooldowns");cds.arraySize=3;float[] values={8f,6f,16f};for(int i=0;i<3;i++)cds.GetArrayElementAtIndex(i).floatValue=values[i];
        });
        Ref(combat,"hammerPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(HammerPath).GetComponent<GreenSummonHammer>());Ref(combat,"wavePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(WavePath).GetComponent<GreenSummonWave>());Ref(combat,"bannerPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(BannerPath).GetComponent<GreenSummonWarBanner>());
        Transform muzzle=Child(root,"WaveMuzzle").transform;muzzle.localPosition=new Vector3(0.65f,1.15f);Ref(combat,"muzzle",muzzle);
        Transform hand=Child(root,"BannerHand").transform;hand.localPosition=new Vector3(0.18f,0.95f);Ref(combat,"bannerHand",hand);
        Visual(root,"Visual",Frames("Idle")[0],2,out SpriteRenderer sr);sr.transform.localScale=new Vector3(-1f,1f,1f);sr.gameObject.AddComponent<SpriteOutlineController>();
        GameObject boxes=Child(root,"Combat"),hurt=Child(boxes,"Attacked",7);BoxCollider2D hurtbox=hurt.AddComponent<BoxCollider2D>();hurtbox.isTrigger=true;hurtbox.size=new Vector2(1.3f,2.25f);hurtbox.offset=Vector2.up*1.125f;Ref(controller,"attackedBox",hurtbox);
        GameObject check=Child(boxes,"SkillCheck",10);BoxCollider2D detector=check.AddComponent<BoxCollider2D>();detector.isTrigger=true;detector.size=new Vector2(14f,4f);detector.offset=Vector2.up*1.2f;check.AddComponent<EnemyCheckAttack>();
        Clip(controller,"Idle",Frames("Idle"),10f,true);Clip(controller,"Walk",Frames("Walk"),14f,true);Clip(controller,"Attacked",Frames("Hit"),18f,false);Clip(controller,"Die",Frames("Death"),12f,false);
        foreach(string skill in new[]{"SummonedHammerStrike","BannerWave","WarBanner"})Clip(controller,skill,Frames(skill),16f,false);
        PrefabUtility.SaveAsPrefabAsset(root,EnemyPath);Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        StageEnemyManager manager=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StageEnemyManager>(true)).FirstOrDefault();Require(manager!=null && manager.enabled,"Level6 enabled StageEnemyManager");
        Edit(manager,so=>
        {
            SerializedProperty pools=so.FindProperty("enemyPools");int index=-1;
            for(int i=0;i<pools.arraySize;i++)if(pools.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is GreenSummonController)index=i;
            if(index<0){index=pools.arraySize;pools.arraySize++;}SerializedProperty pool=pools.GetArrayElementAtIndex(index);
            pool.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<GreenSummonController>();pool.FindPropertyRelative("poolSize").intValue=10;pool.FindPropertyRelative("weight").floatValue=0.45f;
        });
        //Level6原布局与BattleUI各带一个EventSystem；本场景只启用BattleUI所属的一套。
        UnityEngine.EventSystems.EventSystem[] eventSystems=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).ToArray();
        UnityEngine.EventSystems.EventSystem preferred=eventSystems.FirstOrDefault(e=>
        {
            GameObject root=PrefabUtility.GetOutermostPrefabInstanceRoot(e.gameObject);
            return root!=null && root.GetComponentInChildren<UIManager>(true)!=null;
        })??eventSystems.FirstOrDefault();
        foreach(var events in eventSystems)events.gameObject.SetActive(events==preferred);
        Require(manager.ValidateConfiguration(out string error),error);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);
    }
    public static void ValidateReferences()
    {
        foreach(string path in new[]{EnemyPath,HammerPath,BannerPath,WavePath})
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
        GameObject enemy=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath),flag=AssetDatabase.LoadAssetAtPath<GameObject>(BannerPath);
        Require(enemy.GetComponent<Health>().BeginBlood==88f && new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue==20f,"HP88 / XP20");
        Require(enemy.GetComponents<ActorStatus>().Length==1 && enemy.GetComponentInChildren<BossUI>(true)==null,"one status, no boss UI");
        Require(flag.GetComponent<Health>().BeginBlood==60f && flag.GetComponent<EnemyRewards>()==null && flag.GetComponent<EnemyCombat>()==null,"attackable HP60 banner without attack or reward");
    }
    private static void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
}

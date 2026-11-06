using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using zmxy.GamePlay;
using Object=UnityEngine.Object;

//编辑器仅负责生产资产。交付的TrainingManager/HUD/入口均可在Player构建中运行。
public static class TrainingSetup
{
    public const string ScenePath="Assets/Projects/Scenes/Training/TrainingScene.unity";
    public const string CatalogPath="Assets/Projects/Resources/Training/TrainingEnemyCatalog.asset";
    public const string MapPath="Assets/Projects/Scenes/MapScene.unity";
    public static string Audit=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/TrainingAudit"));
    private static void Edit(Object o,Action<SerializedObject> edit){SerializedObject so=new(o);edit(so);so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Ref(Object o,string field,Object value)=>Edit(o,so=>so.FindProperty(field).objectReferenceValue=value);
    private static void Num(Object o,string field,float value)=>Edit(o,so=>so.FindProperty(field).floatValue=value);
    private static GameObject Child(GameObject root,string name){GameObject go=new(name);go.transform.SetParent(root.transform,false);return go;}
    [MenuItem("造梦西游/运行时训练场/生成场景、目录和地图入口")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Audit);Directory.CreateDirectory("Assets/Projects/Scenes/Training");Directory.CreateDirectory("Assets/Projects/Resources/Training");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateCatalog();CreateScene();MapEntry();BuildList();Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        File.WriteAllText(Path.Combine(Audit,"unity-assets.txt"),"PASS: runtime TrainingScene, 15-entry resource catalog, MapScene training shortcut, appended build entry; no formal level/prefab edits.\n");Debug.Log("TRAINING_ASSETS_PASS");
    }
    private static void CreateCatalog()
    {
        TrainingEnemyCatalog catalog=AssetDatabase.LoadAssetAtPath<TrainingEnemyCatalog>(CatalogPath);
        if(catalog!=null){Require(catalog.Count>0,"existing catalog must not be overwritten");return;}
        string[] names={"WolfEnemy","TigerEnemy","PurpleHeavenly","PurpleRanged","BlueShield","BlueWheel","RedChain","RedBoom","TitanSpirit","EastKing","SouthKing","WestKing","NorthKing","GreenHammer","GreenSummon"};
        catalog=ScriptableObject.CreateInstance<TrainingEnemyCatalog>();catalog.enemies=names.Select(name=>new TrainingEnemyCatalog.Entry{displayName=name.Replace("Enemy",""),prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/"+name+".prefab").GetComponent<EnemyController>(),summonFallback=name=="PurpleHeavenly"}).ToArray();
        Require(catalog.enemies.All(e=>e.prefab!=null),"catalog missing enemy prefab");AssetDatabase.CreateAsset(catalog,CatalogPath);
    }
    private static void CreateScene()
    {
        if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(MapPath,OpenSceneMode.Single);
        Scene previous=SceneManager.GetActiveScene(),scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        try
        {
            GameObject floor=new("Training Ground"){layer=6};floor.transform.position=Vector3.down*0.5f;BoxCollider2D box=floor.AddComponent<BoxCollider2D>();box.size=new Vector2(96f,1f);
            Sprite sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");SpriteRenderer ground=floor.AddComponent<SpriteRenderer>();ground.sprite=sprite;ground.drawMode=SpriteDrawMode.Sliced;ground.size=box.size;ground.color=new Color(0.18f,0.28f,0.32f);ground.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
            //材质使用资源资产，避免场景保留未序列化的临时Material引用。
            const string materialPath="Assets/Projects/Resources/Training/TrainingUnlit.mat";Material material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Sprites/Default")){name="TrainingUnlit"};AssetDatabase.CreateAsset(material,materialPath);}Object.DestroyImmediate(ground.sharedMaterial);ground.sharedMaterial=material;
            for(int side=-1;side<=1;side+=2){GameObject wall=new("Training Boundary"){layer=6};wall.transform.position=new Vector3(side*47.5f,7f);wall.AddComponent<BoxCollider2D>().size=new Vector2(1f,16f);}
            GameObject light=new("Training Global Light");Light2D l=light.AddComponent<Light2D>();l.lightType=Light2D.LightType.Global;l.intensity=1f;
            GameObject controller=new("TrainingManager");TrainingManager manager=controller.AddComponent<TrainingManager>();Ref(manager,"catalog",AssetDatabase.LoadAssetAtPath<TrainingEnemyCatalog>(CatalogPath));
            Transform spawn=Child(controller,"PlayerSpawn").transform;GameObject playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab");BoxCollider2D playerBox=playerPrefab.GetComponent<BoxCollider2D>();float feet=(playerBox.offset.y-playerBox.size.y/2f)*playerPrefab.transform.localScale.y;spawn.position=new Vector3(-4f,0.025f-feet);Ref(manager,"playerSpawn",spawn);
            Transform enemySpawn=Child(controller,"EnemySpawn").transform;enemySpawn.position=new Vector3(4f,0.025f);Ref(manager,"enemySpawn",enemySpawn);
            GameObject cameraRoot=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/CameraRig.prefab"),scene);CameraManager cm=cameraRoot.GetComponentInChildren<CameraManager>();Camera camera=cm.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6.5f;camera.backgroundColor=new Color(0.08f,0.12f,0.18f);camera.clearFlags=CameraClearFlags.SolidColor;camera.tag="MainCamera";camera.transform.position=new Vector3(0f,2.2f,-10f);
            Transform left=Child(controller,"CameraLeft").transform,right=Child(controller,"CameraRight").transform;left.position=Vector3.left*36f;right.position=Vector3.right*36f;
            Edit(cm,so=>{foreach(string f in new[]{"cameraEdgeLeft","cameraEdgeRight"}){var a=so.FindProperty(f);a.arraySize=1;a.GetArrayElementAtIndex(0).objectReferenceValue=f=="cameraEdgeLeft"?left:right;}so.FindProperty("followSpeed").floatValue=8f;});
            Ref(manager,"cameraManager",cm);if(cm.GetComponent<TrainingCameraFocus>()==null)cm.gameObject.AddComponent<TrainingCameraFocus>();
            GameObject players=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/PlayerSystem.prefab"),scene);players.transform.position=Vector3.zero;Ref(manager,"players",players.GetComponent<PlayerManager>());
            GameObject canvasRoot=new("TrainingCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));Canvas canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;CanvasScaler scaler=canvasRoot.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600f,900f);scaler.matchWidthOrHeight=1f;
            TrainingHitboxDebug debug=controller.AddComponent<TrainingHitboxDebug>();TrainingHUD hud=canvasRoot.AddComponent<TrainingHUD>();Ref(hud,"manager",manager);Ref(hud,"hitboxes",debug);
            GameObject events=new("TrainingEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene,ScenePath),"save TrainingScene");
            foreach(Transform node in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,node.name+" missing script");
        }
        finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
    }
    private static void MapEntry()
    {
        Scene scene=SceneManager.GetSceneByPath(MapPath);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(MapPath,OpenSceneMode.Additive);
        Canvas canvas=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First();TrainingEntry entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TrainingEntry>(true)).FirstOrDefault();
        if(entry==null)
        {
            GameObject go=new("TrainingButton",typeof(RectTransform),typeof(Image),typeof(Button),typeof(TrainingEntry));go.transform.SetParent(canvas.transform,false);RectTransform rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.right;rect.pivot=Vector2.right;rect.anchoredPosition=new Vector2(-32f,32f);rect.sizeDelta=new Vector2(280f,64f);go.GetComponent<Image>().color=new Color(0.08f,0.4f,0.3f);
            GameObject label=new("TrainingLabel",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);RectTransform r=label.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;Text t=label.GetComponent<Text>();t.font=Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");t.fontSize=26;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.text="TRAINING";t.raycastTarget=false;
            entry=go.GetComponent<TrainingEntry>();UnityEventTools.AddPersistentListener(go.GetComponent<Button>().onClick,entry.OpenTraining);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);
    }
    private static void BuildList()
    {
        var scenes=EditorBuildSettings.scenes.ToList();int index=scenes.FindIndex(s=>s.path==ScenePath);if(index<0)scenes.Add(new EditorBuildSettingsScene(ScenePath,true));else scenes[index].enabled=true;EditorBuildSettings.scenes=scenes.ToArray();
    }
    private static void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
}

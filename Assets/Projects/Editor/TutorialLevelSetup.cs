using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using zmxy.GamePlay;
using Object = UnityEngine.Object;

//只迁移第一关：复用原关美术、共用关卡组件与现有狼，不修改其余关卡和任何敌人预制体。
public static class TutorialLevelSetup
{
    private const string PathName = "Assets/Projects/Scenes/Levels/Level1Scene.unity";
    private static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/" + name + ".prefab");
    private static void Edit(Object target, Action<SerializedObject> action) { SerializedObject so = new(target); action(so); so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Ref(Object target, string field, Object value) => Edit(target, so => so.FindProperty(field).objectReferenceValue = value);
    private static void Num(Object target, string field, float value) => Edit(target, so => so.FindProperty(field).floatValue = value);
    private static void Int(Object target, string field, int value) => Edit(target, so => so.FindProperty(field).intValue = value);
    private static Transform Point(GameObject parent, string name, Vector2 position)
    { GameObject go = new(name); go.transform.SetParent(parent.transform, false); go.transform.position = position; return go.transform; }
    [MenuItem("造梦西游/第一关/迁移到统一教程关流程")]
    public static void Build()
    {
        Scene old = EditorSceneManager.OpenScene(PathName, OpenSceneMode.Single);
        if (old.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TutorialStageManager>(true)).Any())
            return;//已迁移的场景保留手工美术与布局调整；生成器不再重置现有UI。
        Directory.CreateDirectory(RebirthSetup.Audit);
        string backup = System.IO.Path.Combine(RebirthSetup.Audit, "Level1Scene.before.unity");
        if (!File.Exists(backup)) File.Copy(PathName, backup);
        var legacyCamera = old.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<zmxy.Legacy.CameraManager>(true)).First();
        SpriteRenderer floorArt = legacyCamera.ground[0].GetComponentInChildren<SpriteRenderer>();
        SpriteRenderer background = old.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)).First(r => r.name == "bg");
        Sprite floorSprite = floorArt.sprite, skySprite = background.sprite;
        Vector3 floorScale = floorArt.transform.lossyScale;
        File.WriteAllText(System.IO.Path.Combine(RebirthSetup.Audit, "tutorial-art.txt"),
            "Original background: " + AssetDatabase.GetAssetPath(skySprite) + "\nOriginal ground: " + AssetDatabase.GetAssetPath(floorSprite) + "\n");
        if (floorSprite == null || skySprite == null) throw new InvalidOperationException("Missing original Level1 scenery");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject environment = new("Level1_Tutorial_Environment");
        //原关的竖向攀爬切换为与后续关卡一致的三段横向战斗，沿用原图像像素与材质。
        float skyHeight = 22f, skyScale = skyHeight / skySprite.bounds.size.y, skyWidth = skySprite.bounds.size.x * skyScale;
        for (float x = -18f; x < 98f; x += skyWidth)
        {
            Transform tile = Point(environment, "OriginalSky", new Vector2(x + skyWidth * 0.5f, 7f)); tile.localScale = Vector3.one * skyScale;
            SpriteRenderer visual = tile.gameObject.AddComponent<SpriteRenderer>(); visual.sprite = skySprite; visual.sortingOrder = -100;
        }
        float tileWidth = floorSprite.bounds.size.x * Mathf.Abs(floorScale.x), tileHeight = floorSprite.bounds.size.y * Mathf.Abs(floorScale.y);
        if (tileWidth < 0.1f) throw new InvalidOperationException("Invalid original ground width");
        for (float x = -18f; x < 98f; x += tileWidth)
        {
            Transform tile = Point(environment, "OriginalGround", new Vector2(x + tileWidth * 0.5f, -tileHeight * 0.5f)); tile.localScale = new Vector3(Mathf.Abs(floorScale.x), Mathf.Abs(floorScale.y), 1f);
            SpriteRenderer visual = tile.gameObject.AddComponent<SpriteRenderer>(); visual.sprite = floorSprite; visual.sortingOrder = -5;
        }
        Transform ground = Point(environment, "GroundCollider", new Vector2(40f, -1f)); ground.gameObject.layer = 6; ground.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(116f, 2f);
        foreach (float x in new[] { -18f, 98f })
        { Transform wall = Point(environment, "Boundary", new Vector2(x, 8f)); wall.gameObject.layer = 6; wall.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(1f, 20f); }
        Light2D light = new GameObject("GlobalLight").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1f;
        GameObject layout = new("Level1_Tutorial"); TutorialStageManager stage = layout.AddComponent<TutorialStageManager>(); TutorialEnemyManager enemies = layout.AddComponent<TutorialEnemyManager>();
        GameObject playerSystem = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("PlayerSystem"), scene); PlayerManager players = playerSystem.GetComponent<PlayerManager>();
        GameObject cameraRig = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("CameraRig"), scene); CameraManager cm = cameraRig.GetComponentInChildren<CameraManager>();
        Camera camera = cm.GetComponent<Camera>(); camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7f; camera.transform.position = new Vector3(-2f, 4f, -10f);
        Transform[] left = new Transform[3], right = new Transform[3];
        for (int i = 0; i < 3; i++)
        { left[i] = Point(layout, "WaveLeft" + i, new Vector2(-2f + i * 28f, 4f)); right[i] = Point(layout, "WaveRight" + i, new Vector2(8f + i * 28f, 4f)); }
        Edit(cm, so =>
        {
            foreach (string field in new[] { "cameraEdgeLeft", "cameraEdgeRight" })
            {
                SerializedProperty array = so.FindProperty(field); array.arraySize = 3;
                for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = field == "cameraEdgeLeft" ? left[i] : right[i];
            }
            so.FindProperty("followSpeed").floatValue = 8f; so.FindProperty("maxRise").floatValue = 3f;
        });
        GameObject uiRoot = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("BattleUI"), scene); UIManager ui = uiRoot.GetComponentInChildren<UIManager>(true);
        if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Any())
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab"); BoxCollider2D body = heroPrefab.GetComponent<BoxCollider2D>();
        float feet = (body.offset.y - body.size.y * 0.5f) * heroPrefab.transform.localScale.y;
        Transform spawn = Point(layout, "PlayerSpawn", new Vector2(-5f, 0.05f - feet)); Transform exit = Point(layout, "ExitLocation", new Vector2(74f, 0f));
        Ref(stage, "ui", ui); Ref(stage, "playerManager", players); Ref(stage, "enemyManager", enemies); Ref(stage, "CM", cm); Ref(stage, "tran", spawn); Ref(stage, "exitLocation", exit);
        Ref(stage, "exit", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/UI/exit.prefab").GetComponent<ExitManager>());
        Int(stage, "maxWave", 3); Num(stage, "enemyDelay", 3f); Num(stage, "enemyMax", 4f); Num(stage, "firstEnemyDelay", 1.5f);
        Edit(stage, so =>
        {
            foreach (string field in new[] { "leftEnemyMax", "rightEnemyMax" })
            { SerializedProperty array = so.FindProperty(field); array.arraySize = 3; for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).intValue = 4; }
        });
        Edit(enemies, so =>
        {
            SerializedProperty pools = so.FindProperty("enemyPools"); pools.arraySize = 1; SerializedProperty pool = pools.GetArrayElementAtIndex(0);
            pool.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/WolfEnemy.prefab").GetComponent<EnemyController>();
            pool.FindPropertyRelative("poolSize").intValue = 10; pool.FindPropertyRelative("weight").floatValue = 1f;
        });
        Canvas canvas = uiRoot.GetComponentInChildren<Canvas>(true);
        GameObject note = new("TutorialNotice", typeof(RectTransform), typeof(Text), typeof(RebirthText)); note.transform.SetParent(canvas.transform, false);
        RectTransform rect = note.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f); rect.anchoredPosition = new Vector2(0f, 24f); rect.sizeDelta = new Vector2(1050f, 42f);
        Text text = note.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf"); text.fontSize = 20; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
        text.text = "清完三波狼后前往出口，按交互键离开。通关解锁猪八戒，数字键 1–4 切换已加入的伙伴。";
        RepairPresentation(scene);
        stage.ResolveSceneReferences(); if (!stage.ValidateConfiguration(out string error)) throw new InvalidOperationException(error);
        EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene, PathName)) throw new InvalidOperationException("Save tutorial scene failed");
        File.WriteAllText(System.IO.Path.Combine(RebirthSetup.Audit, "tutorial-assets.txt"), "PASS: Level1 uses PlayerSystem/BattleUI/CameraRig, common waves and pooled WolfEnemy; 3x8 wolves, no Boss, original scenery reused, scene GUID preserved.\n");
        Debug.Log("TUTORIAL_ASSETS_PASS");
    }
    private static Material SceneryMaterial(string name, Texture texture)
    {
        const string folder = "Assets/Projects/Resources/Tutorial";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Projects/Resources", "Tutorial");
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Sprites/Default")) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.mainTexture = texture; material.color = Color.white; EditorUtility.SetDirty(material); return material;
    }
    private static void RepairPresentation(Scene scene)
    {
        Transform[] nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        SpriteRenderer[] sky = nodes.Where(t => t.name == "OriginalSky").Select(t => t.GetComponent<SpriteRenderer>()).ToArray();
        SpriteRenderer[] floor = nodes.Where(t => t.name == "OriginalGround").Select(t => t.GetComponent<SpriteRenderer>()).ToArray();
        Material skyMaterial = SceneryMaterial("TutorialSky", sky[0].sprite.texture), floorMaterial = SceneryMaterial("TutorialGround", floor[0].sprite.texture);
        for (int i = 0; i < sky.Length; i++)
        {
            SpriteRenderer renderer = sky[i]; renderer.sharedMaterial = skyMaterial;
            float width = renderer.sprite.bounds.size.x * renderer.transform.localScale.x;
            Vector3 center = renderer.sprite.bounds.center; Vector3 scale = renderer.transform.localScale;
            renderer.transform.position = new Vector3(-18f + (i + 0.5f) * width - center.x * scale.x, 7f - center.y * scale.y, 0f);
        }
        for (int i = 0; i < floor.Length; i++)
        {
            SpriteRenderer renderer = floor[i]; renderer.sharedMaterial = floorMaterial;
            Vector3 scale = renderer.transform.localScale; Bounds bounds = renderer.sprite.bounds;
            float width = bounds.size.x * scale.x;
            renderer.transform.position = new Vector3(-18f + i * width - bounds.min.x * scale.x, -bounds.max.y * scale.y, 0f);
        }
        Canvas canvas = nodes.Select(t => t.GetComponent<Canvas>()).First(c => c != null);
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.matchWidthOrHeight = 1f;
        void Anchor(string name, Vector2 anchor, Vector2 position)
        {
            RectTransform rect = nodes.First(t => t.name == name && t is RectTransform) as RectTransform;
            rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position;
        }
        Anchor("HealthBar", new Vector2(0f, 1f), new Vector2(153.3f, -77.1f));
        Anchor("Buttons", Vector2.zero, new Vector2(20f, 54f));
        Anchor("SkillBar", Vector2.right, new Vector2(-130f, 52f));
        Transform help = canvas.transform.Find("TutorialHelpPanel");
        if (help == null)
        {
            GameObject panel = new("TutorialHelpPanel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvas.transform, false);
            panel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.7f); panel.GetComponent<Image>().raycastTarget = false; help = panel.transform;
        }
        RectTransform panelRect = (RectTransform)help; panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f); panelRect.anchoredPosition = new Vector2(0f, 90f); panelRect.sizeDelta = new Vector2(620f, 38f);
        Text notice = nodes.First(t => t.name == "TutorialNotice").GetComponent<Text>(); notice.transform.SetParent(help, false);
        RectTransform textRect = notice.rectTransform; textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f); textRect.anchoredPosition = Vector2.zero; textRect.sizeDelta = new Vector2(600f, 34f);
        notice.fontSize = 16; notice.text = "清完三波狼后，前往出口交互。通关解锁猪八戒。";
        AssetDatabase.SaveAssets();
    }

}

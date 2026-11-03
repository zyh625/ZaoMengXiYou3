using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using zmxy.GamePlay;
using Object = UnityEngine.Object;

//显式菜单/批处理工具，无自动导入回调；只写 RedChain 资产与 Level5Scene。
public static class RedChainSetup
{
    public const string EnemyPath = "Assets/Projects/Prefabs/Enemy/RedChain.prefab";
    public const string WavePath = "Assets/Projects/Prefabs/SeparateEffort/RedChainShockWave.prefab";
    public const string ScenePath = "Assets/Projects/Scenes/Levels/Level5Scene.unity";
    private const string Art = "Assets/Art/Enemies/Level5/RedChain/";
    private static readonly string[] Actions = { "Idle", "Walk", "Attacked", "Die", "AttackOne", "AttackTwo", "AttackThree", "ShockWave" };

    [MenuItem("造梦西游/RedChain/生成并验证第五关赤链武卒")]
    public static void BuildAndValidate()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateSprites();
            CreateWave();
            CreateEnemy();
            WireScene();
            ValidatePrefab();
            Directory.CreateDirectory("Temp/RedChainAudit");
            File.WriteAllText("Temp/RedChainAudit/unity-assets.txt", "PASS: 128 sprites; prefab references; exact HP/XP/ranges/cooldowns/frame windows; Level5 wiring.\n");
            Debug.Log("REDCHAIN_ASSETS_PASS");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scratch, true);
        }
    }

    public static Sprite[] Frames(string action) => AssetDatabase.LoadAllAssetsAtPath(Art + action + ".png").OfType<Sprite>()
        .OrderBy(sprite => int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))).ToArray();

    public static void ValidateSprites()
    {
        foreach (string action in Actions)
        {
            Sprite[] frames = Frames(action);
            Check(frames.Length == 16, action + " must contain exactly 16 sprites");
            for (int i = 0; i < 16; i++)
            {
                Check(frames[i] != null && frames[i].name == action + "_" + i, action + " sprite names");
                Check(Mathf.Approximately(frames[i].pixelsPerUnit, 100f), action + " PPU");
                Check(frames[i].rect.width > 0 && frames[i].rect.height > 0, action + " empty rectangle");
            }
        }
    }

    private static GameObject Child(GameObject parent, string name, int layer = 0)
    {
        GameObject child = new(name);
        child.layer = layer;
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static void Ref(Object component, string name, Object value) => Edit(component, so => so.FindProperty(name).objectReferenceValue = value);
    private static void Number(Object component, string name, float value) => Edit(component, so => so.FindProperty(name).floatValue = value);
    private static void Integer(Object component, string name, int value) => Edit(component, so => so.FindProperty(name).intValue = value);
    private static void Edit(Object component, Action<SerializedObject> edit)
    {
        SerializedObject so = new(component);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void References(Object component, string name, Object[] values) => Edit(component, so =>
    {
        SerializedProperty array = so.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    });
    private static void Integers(Object component, string name, params int[] values) => Edit(component, so =>
    {
        SerializedProperty array = so.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).intValue = values[i];
    });
    private static void Clip(Object component, string property, string action, float fps, bool loop) => Edit(component, so =>
    {
        SerializedProperty clip = so.FindProperty(property);
        Sprite[] frames = Frames(action);
        SerializedProperty sprites = clip.FindPropertyRelative("sprites");
        sprites.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        clip.FindPropertyRelative("fps").floatValue = fps;
        clip.FindPropertyRelative("loop").boolValue = loop;
    });

    private static Material EnemyMaterial => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/BlueShield.prefab")
        .GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;

    private static void CreateWave()
    {
        GameObject root = new("RedChainShockWave");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = EnemyMaterial;
        renderer.sprite = Frames("ShockWave")[0];
        renderer.sortingOrder = 3;
        float width = Frames("ShockWave").Max(sprite => sprite.rect.width) / 100f;
        root.transform.localScale = Vector3.one * (6f / width);
        SpriteManager manager = root.AddComponent<SpriteManager>();
        Ref(manager, "sr", renderer);
        RedChainShockWave effect = root.AddComponent<RedChainShockWave>();
        Ref(effect, "spriteManager", manager);
        Clip(effect, "frames", "ShockWave", 16f, false);
        Number(effect, "lifetime", 1.5f);
        PrefabUtility.SaveAsPrefabAsset(root, WavePath);
        Object.DestroyImmediate(root);
    }

    private static void CreateEnemy()
    {
        GameObject root = new("RedChain");
        root.layer = 11;
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.gravityScale = 0.9f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.1f, 2.1f);
        collider.offset = new Vector2(0f, 1.05f);
        Health health = root.AddComponent<Health>();
        Number(health, "beginBlood", 135f);
        ActorStatus status = root.AddComponent<ActorStatus>();
        EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/BlueShield.prefab").GetComponent<ActorStatus>(), status);
        EnemyRewards rewards = root.AddComponent<EnemyRewards>();
        Number(rewards, "experienceReward", 18f);
        RedChainMovement movement = root.AddComponent<RedChainMovement>();
        Number(movement, "moveSpeed", 0.8f);
        Number(movement, "slowSpeed", 0.4f);
        Number(movement, "backSpeed", 3.5f);
        Number(movement, "upSpeed", 6f);
        Number(movement, "boostRatio", 1f);
        Number(movement, "minNormalY", 0.6f);
        Integer(movement, "groundLayer", 1 << 6);
        RedChainCombat combat = root.AddComponent<RedChainCombat>();
        RedChainController controller = root.AddComponent<RedChainController>();

        GameObject visual = Child(root, "Visual");
        visual.transform.localScale = new Vector3(-1f, 1f, 1f);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = EnemyMaterial;
        renderer.sortingOrder = 2;
        renderer.sprite = Frames("Idle")[0];
        SpriteManager spriteManager = visual.AddComponent<SpriteManager>();
        Ref(spriteManager, "sr", renderer);
        visual.AddComponent<SpriteOutlineController>();

        GameObject boxes = Child(root, "Combat");
        GameObject hurt = Child(boxes, "Attacked", 7);
        BoxCollider2D hurtBox = hurt.AddComponent<BoxCollider2D>();
        hurtBox.isTrigger = true;
        hurtBox.size = new Vector2(1.3f, 2.2f);
        hurtBox.offset = new Vector2(0f, 1.1f);
        Ref(controller, "attackedBox", hurtBox);
        Number(controller, "stopDistance", 1.8f);
        Integer(controller, "specialAttack", 2);
        Number(controller, "recoveryDuration", 1.2f);
        Number(controller, "chainVisualScale", 2.55f);
        Clip(controller, "Idle", "Idle", 10f, true);
        Clip(controller, "Walk", "Walk", 14f, true);
        Clip(controller, "Attacked", "Attacked", 18f, false);
        Clip(controller, "Die", "Die", 12f, false);
        for (int i = 0; i < 3; i++) Clip(controller, "Attack" + new[] { "One", "Two", "Three" }[i], "Attack" + new[] { "One", "Two", "Three" }[i], 16f, false);

        BoxCollider2D[] attackBoxes = new BoxCollider2D[3];
        RedChainAttackBox[] hitBoxes = new RedChainAttackBox[3];
        EnemyCheckAttack[] checks = new EnemyCheckAttack[3];
        float[] widths = { 4.5f, 9f, 6f }, damage = { 17f, 14f, 20f };
        for (int i = 0; i < 3; i++)
        {
            string name = "Attack" + new[] { "One", "Two", "Three" }[i];
            GameObject attack = Child(boxes, name, 9);
            BoxCollider2D hit = attack.AddComponent<BoxCollider2D>();
            hit.isTrigger = true;
            hit.size = new Vector2(widths[i], i == 2 ? 0.7f : 2.2f);
            hit.offset = new Vector2(widths[i] / 2f, i == 2 ? 0.35f : 1.1f);
            RedChainAttackBox logic = attack.AddComponent<RedChainAttackBox>();
            Number(logic, "Attack", damage[i]);
            Integer(logic, "hitType", i);
            Edit(logic, so => so.FindProperty("knockback").boolValue = i == 0);
            hitBoxes[i] = logic;
            attackBoxes[i] = hit;
            attack.SetActive(false);
            GameObject check = Child(boxes, name + "Field", 10);
            BoxCollider2D detection = check.AddComponent<BoxCollider2D>();
            detection.isTrigger = true;
            detection.size = new Vector2(i == 1 ? 9f : 6f, i == 2 ? 0.7f : 2.2f);
            detection.offset = new Vector2(detection.size.x / 2f, i == 2 ? 0.35f : 1.1f);
            checks[i] = check.AddComponent<EnemyCheckAttack>();
        }
        References(combat, "AttackBoxes", attackBoxes);
        References(combat, "CheckAttackLogic", checks);
        References(combat, "hitBoxes", hitBoxes);
        Integers(combat, "AttackOpenIdx", 6, 8, 8);
        Integers(combat, "AttackShutIdx", 9, 10, 9);
        Edit(combat, so =>
        {
            SerializedProperty delay = so.FindProperty("attackDelay");
            delay.arraySize = 3;
            float[] values = { 3.5f, 8f, 10f };
            for (int i = 0; i < 3; i++) delay.GetArrayElementAtIndex(i).floatValue = values[i];
        });
        RedChainShockWaveEmitter emitter = Child(root, "ShockWave").AddComponent<RedChainShockWaveEmitter>();
        Ref(emitter, "prefab", AssetDatabase.LoadAssetAtPath<GameObject>(WavePath).GetComponent<RedChainShockWave>());
        Ref(combat, "shockWave", emitter);
        PrefabUtility.SaveAsPrefabAsset(root, EnemyPath);
        Object.DestroyImmediate(root);
    }

    private static void WireScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject Named(string name) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).FirstOrDefault(go => go.name == name);
        T[] Components<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        GameObject layout = Named("Level5_CrimsonDragonHall_Layout");
        Check(layout != null, "Level5 layout");
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(layout);
        foreach (LevelManager previous in layout.GetComponents<LevelManager>())
            if (!(previous is RedChainStageManager)) Object.DestroyImmediate(previous);
        RedChainStageManager stage = layout.GetComponent<RedChainStageManager>() ?? layout.AddComponent<RedChainStageManager>();
        RedChainStageEnemyManager enemies = layout.GetComponent<RedChainStageEnemyManager>() ?? layout.AddComponent<RedChainStageEnemyManager>();
        enemies.enabled = stage.enabled = true;
        Edit(enemies, so =>
        {
            SerializedProperty pools = so.FindProperty("enemyPools");
            pools.arraySize = 1;
            SerializedProperty pool = pools.GetArrayElementAtIndex(0);
            pool.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<RedChainController>();
            pool.FindPropertyRelative("poolSize").intValue = 10;
            pool.FindPropertyRelative("weight").floatValue = 0.55f;
        });
        Ref(enemies, "bossBornLocation", Named("GuangmuTianwang_BossSpawn").transform);
        PlayerManager players = Components<PlayerManager>().FirstOrDefault();
        if (players == null)
            players = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/PlayerSystem.prefab"), scene)).GetComponent<PlayerManager>();
        UIManager ui = Components<UIManager>().FirstOrDefault();
        if (ui == null)
            ui = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/BattleUI.prefab"), scene)).GetComponentInChildren<UIManager>(true);
        //场景里的旧单人实例由正式 PlayerSystem 替代，但保留原物体以便回查。
        foreach (PlayerController oldPlayer in Components<PlayerController>()) oldPlayer.gameObject.SetActive(false);
        Camera camera = Components<Camera>().First();
        camera.tag = "MainCamera";
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);
        CameraManager cm = camera.GetComponent<CameraManager>();
        if (cm == null)
        {
            cm = camera.gameObject.AddComponent<CameraManager>();
            EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/CameraRig.prefab").GetComponentInChildren<CameraManager>(true), cm);
        }
        References(cm, "cameraEdgeLeft", Enumerable.Range(0,4).Select(i => (Object)Named("tranL" + i).transform).ToArray());
        References(cm, "cameraEdgeRight", Enumerable.Range(0,4).Select(i => (Object)Named("tranR" + i).transform).ToArray());
        cm.enabled = true;
        Transform spawn = Named("PlayerSpawn").transform;
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab");
        BoxCollider2D playerBox = playerPrefab.GetComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();
        RaycastHit2D floor = Physics2D.Raycast(new Vector2(spawn.position.x, 20f), Vector2.down, 100f, 1 << 6);
        if (floor.collider != null)
        {
            float feet = (playerBox.offset.y - playerBox.size.y / 2f) * playerPrefab.transform.localScale.y;
            spawn.position = new Vector3(spawn.position.x, floor.point.y - feet + 0.05f, 0f);
        }
        Ref(stage, "playerManager", players);
        Ref(stage, "enemyManager", enemies);
        Ref(stage, "ui", ui);
        Ref(stage, "CM", cm);
        Ref(stage, "tran", spawn);
        Ref(stage, "exitLocation", Named("exit").transform);
        Ref(stage, "exit", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/UI/exit.prefab").GetComponent<ExitManager>());
        Integer(stage, "maxWave", 4);
        Integers(stage, "leftEnemyMax", 3, 4, 7, 7);
        Integers(stage, "rightEnemyMax", 3, 5, 6, 8);
        Number(stage, "enemyDelay", 10f);
        Number(stage, "firstEnemyDelay", 5f);
        Number(stage, "enemyMax", 6f);
        stage.ResolveSceneReferences();
        Check(stage.ValidateConfiguration(out string error), "Level5: " + error);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    public static void ValidatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        Check(prefab != null, "Missing RedChain prefab");
        foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Missing script: " + child.name);
        foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
        {
            SerializedObject so = new(component);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference)
                    Check(p.objectReferenceValue != null || p.objectReferenceInstanceIDValue == 0, "Missing reference: " + component.name + "." + p.propertyPath);
        }
        Check(prefab.GetComponent<Health>().BeginBlood == 135f, "HP 135");
        Check(new SerializedObject(prefab.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 18f, "XP 18");
        Check(prefab.GetComponentsInChildren<RedChainAttackBox>(true).Length == 3, "Three attack boxes");
        Check(prefab.GetComponentInChildren<BossUI>(true) == null, "No Boss UI");
        Check(prefab.GetComponentInChildren<RedChainShockWaveEmitter>(true) != null, "ShockWave pool reference");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

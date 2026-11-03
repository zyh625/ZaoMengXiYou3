using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using zmxy.GamePlay;
using Object = UnityEngine.Object;

public static class RollingFurnaceSetup
{
    public const string EnemyPath = "Assets/Projects/Prefabs/Enemy/RollingFurnace.prefab";
    public const string WavePath = "Assets/Projects/Prefabs/SeparateEffort/RollingFurnaceShockwave.prefab";
    public const string ScenePath = "Assets/Projects/Scenes/Levels/Level7Scene.unity";
    public const string Art = "Assets/Art/Enemies/Level7/RollingFurnace/";
    public static readonly string[] Actions = { "Idle", "Walk", "RollCharge", "BounceSlam", "ShellVent", "Hit", "Death" };
    public static string Audit => Path.GetFullPath("output/RollingFurnaceAudit");
    public static Sprite[] Frames(string name) => AssetDatabase.LoadAllAssetsAtPath(Art + name + ".png").OfType<Sprite>()
        .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
    private static void Edit(Object target, Action<SerializedObject> edit)
    { var so = new SerializedObject(target); edit(so); so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Ref(Object c, string field, Object value) => Edit(c, s => s.FindProperty(field).objectReferenceValue = value);
    private static void Num(Object c, string field, float value) => Edit(c, s => s.FindProperty(field).floatValue = value);
    private static void Int(Object c, string field, int value) => Edit(c, s => s.FindProperty(field).intValue = value);
    private static void Flag(Object c, string field, bool value) => Edit(c, s => s.FindProperty(field).boolValue = value);
    private static void Refs(Object c, string field, Object[] values) => Edit(c, s =>
    { var p = s.FindProperty(field); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; });
    private static void Numbers(Object c, string field, params float[] values) => Edit(c, s =>
    { var p = s.FindProperty(field); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).floatValue = values[i]; });
    private static void Integers(Object c, string field, params int[] values) => Edit(c, s =>
    { var p = s.FindProperty(field); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).intValue = values[i]; });
    private static void Clip(Object c, string field, Sprite[] frames, float fps, bool loop) => Edit(c, s =>
    {
        var p = s.FindProperty(field); var a = p.FindPropertyRelative("sprites"); a.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        p.FindPropertyRelative("fps").floatValue = fps; p.FindPropertyRelative("loop").boolValue = loop;
    });
    private static GameObject Child(GameObject parent, string name, int layer = 0)
    { GameObject go = new(name) { layer = layer }; go.transform.SetParent(parent.transform, false); return go; }
    private static GameObject Reference => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/BlueShield.prefab");
    private static SpriteManager Visual(GameObject root, Sprite sprite, out SpriteRenderer renderer)
    {
        var go = Child(root, "Visual"); renderer = go.AddComponent<SpriteRenderer>();
        var original = Reference.GetComponentInChildren<SpriteRenderer>(true);
        renderer.sharedMaterial = original.sharedMaterial; renderer.sortingLayerID = original.sortingLayerID;
        renderer.sortingOrder = original.sortingOrder; renderer.sprite = sprite;
        var sm = go.AddComponent<SpriteManager>(); Ref(sm, "sr", renderer); return sm;
    }
    [MenuItem("造梦西游/RollingFurnace/生成预制体并接入第七关")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Audit);
        if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var previous = SceneManager.GetActiveScene();
        var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateSprites(); CreateWave(); CreateEnemy(); WireScene(); ValidateAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            File.WriteAllText(Path.Combine(Audit, "assets.txt"), "PASS: 7 x 16 frames; PPU 100; 256x384 cells; pivot (128,56); HP144 XP22 speed1; prefabs and Level7 pool10 weight0.33.\n");
            Debug.Log("ROLLING_FURNACE_ASSETS_PASS");
        }
        finally
        { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scratch, true); }
    }
    public static void ValidateSprites()
    {
        foreach (string action in Actions)
        {
            var sprites = Frames(action); Require(sprites.Length == 16, action + " 16 sprites");
            for (int i = 0; i < 16; i++)
            {
                var s = sprites[i]; Require(s.name == action + "_" + i && Mathf.Approximately(s.pixelsPerUnit, 100), action + " names / PPU");
                Require(s.rect == new Rect(i % 4 * 256, (3 - i / 4) * 384, 256, 384), action + " equal cells");
                Require(Vector2.Distance(s.pivot, new Vector2(128, 56)) < .01f, action + " foot pivot");
            }
        }
    }
    private static void CreateWave()
    {
        var frames = Frames("Effects/LandingShockwave"); Require(frames.Length == 16, "shockwave frames");
        GameObject root = new("RollingFurnaceShockwave"); var sm = Visual(root, frames[0], out var renderer);
        var wave = root.AddComponent<RollingFurnaceShockwave>(); Clip(wave, "frames", frames, 16, false); Ref(wave, "spriteManager", sm);
        // Find the peak opaque width without including transparent cell padding.
        Texture2D image = new(2, 2); image.LoadImage(File.ReadAllBytes(Art + "Effects/LandingShockwave.png"));
        var pixels = image.GetPixels32(); int widest = 1;
        foreach (var frame in frames)
        {
            Rect r = frame.rect; int min = (int)r.xMax, max = (int)r.xMin;
            for (int y = (int)r.yMin; y < r.yMax; y++) for (int x = (int)r.xMin; x < r.xMax; x++)
                if (pixels[y * image.width + x].a >= 2) { min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
            widest = Mathf.Max(widest, max - min + 1);
        }
        Object.DestroyImmediate(image); Num(wave, "nativeWidth", widest / 100f);
        PrefabUtility.SaveAsPrefabAsset(root, WavePath); Object.DestroyImmediate(root);
    }
    private static void CreateEnemy()
    {
        GameObject root = new("RollingFurnace") { layer = 11, tag = "Enemy" };
        var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = .9f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var shell = root.AddComponent<BoxCollider2D>(); shell.size = new Vector2(1.5f, 1.2f); shell.offset = new Vector2(0f, .6f); shell.edgeRadius = .08f;
        var hp = root.AddComponent<Health>(); Num(hp, "beginBlood", 144);
        EditorUtility.CopySerialized(Reference.GetComponent<ActorStatus>(), root.GetComponent<ActorStatus>());
        var rewards = root.AddComponent<EnemyRewards>(); Num(rewards, "experienceReward", 22);
        var move = root.AddComponent<RollingFurnaceMovement>();
        Num(move, "moveSpeed", 1); Num(move, "slowSpeed", .5f); Num(move, "backSpeed", 3.5f); Num(move, "upSpeed", 6);
        Num(move, "boostRatio", 1); Num(move, "minNormalY", .6f); Int(move, "groundLayer", 1 << 6);
        var combat = root.AddComponent<RollingFurnaceCombat>(); var controller = root.AddComponent<RollingFurnaceController>();
        Num(controller, "stopDistance", 2.5f); Int(controller, "specialAttack", -1);
        Visual(root, Frames("Idle")[0], out var sr); sr.transform.localScale = new Vector3(-1, 1, 1);
        var boxes = Child(root, "Combat"); var hurt = Child(boxes, "Attacked", 7);
        var hurtBox = hurt.AddComponent<BoxCollider2D>(); hurtBox.isTrigger = true; hurtBox.size = new Vector2(1.65f, 1.3f); hurtBox.offset = new Vector2(0f, .65f);
        Ref(controller, "attackedBox", hurtBox);
        var attacks = new BoxCollider2D[3]; var checks = new EnemyCheckAttack[3];
        string[] names = { "RollContact", "ReboundContact", "LandingRadius" };
        string[] fields = { "rollBox", "reboundBox", "slamBox" }; float[] damage = { 20, 10, 18 };
        for (int i = 0; i < 3; i++)
        {
            var go = Child(boxes, names[i], 9); var box = go.AddComponent<BoxCollider2D>(); box.isTrigger = true;
            box.size = i == 2 ? new Vector2(5, 5) : new Vector2(1.7f, 1.25f); box.offset = i == 2 ? Vector2.zero : new Vector2(0f, .625f);
            var hit = go.AddComponent<RollingFurnaceAttackBox>(); Num(hit, "Attack", damage[i]); Flag(hit, "knockback", true); Flag(hit, "landingOnly", i == 2);
            Ref(combat, fields[i], hit); attacks[i] = box; go.SetActive(false);
            var check = Child(boxes, names[i] + "Check", 10); var detector = check.AddComponent<BoxCollider2D>(); detector.isTrigger = true;
            detector.size = i == 2 ? new Vector2(8, 4) : new Vector2(16, 4); detector.offset = Vector2.up;
            checks[i] = check.AddComponent<EnemyCheckAttack>();
        }
        Refs(combat, "AttackBoxes", attacks); Refs(combat, "CheckAttackLogic", checks);
        Numbers(combat, "attackDelay", 6, 6, 8); Integers(combat, "AttackOpenIdx", 8, 8, 11); Integers(combat, "AttackShutIdx", 12, 12, 12);
        var pool = Child(root, "ShockwaveEmitter").AddComponent<RollingFurnaceShockwavePool>();
        Ref(pool, "prefab", AssetDatabase.LoadAssetAtPath<GameObject>(WavePath).GetComponent<RollingFurnaceShockwave>()); Ref(combat, "shockwaves", pool);
        Clip(controller, "Idle", Frames("Idle"), 10, true); Clip(controller, "Walk", Frames("Walk"), 14, true);
        Clip(controller, "Attacked", Frames("Hit"), 18, false); Clip(controller, "Die", Frames("Death"), 12, false);
        foreach (string action in new[] { "RollCharge", "BounceSlam", "ShellVent" }) Clip(controller, action, Frames(action), 16, false);
        PrefabUtility.SaveAsPrefabAsset(root, EnemyPath); Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        GameObject Named(string name) => All<Transform>().First(t => t.name == name).gameObject;
        var layout = Named("Level7_LihuoElixirPalace_Layout");
        // Keep the old disabled scene component in place; no existing resource is deleted.
        var old = layout.GetComponents<LevelManager>().FirstOrDefault(c => !(c is RollingFurnaceStageManager));
        var stage = layout.GetComponent<RollingFurnaceStageManager>() ?? layout.AddComponent<RollingFurnaceStageManager>();
        if (old != null)
        {
            var source = new SerializedObject(old); var destination = new SerializedObject(stage);
            foreach (string field in new[] { "CM", "leftEnemyMax", "rightEnemyMax", "enemyDelay", "enemyMax", "tran", "firstEnemyDelay", "maxWave", "bossBornDelay", "exitLocation", "exit" })
                destination.CopyFromSerializedProperty(source.FindProperty(field));
            destination.ApplyModifiedPropertiesWithoutUndo(); old.enabled = false;
        }
        var enemies = layout.GetComponent<RollingFurnaceStageEnemyManager>() ?? layout.AddComponent<RollingFurnaceStageEnemyManager>();
        Edit(enemies, s =>
        {
            var pools = s.FindProperty("enemyPools"); int index = -1;
            for (int i = 0; i < pools.arraySize; i++) if (pools.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is RollingFurnaceController) index = i;
            if (index < 0) { index = pools.arraySize; pools.arraySize++; }
            var entry = pools.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<RollingFurnaceController>();
            entry.FindPropertyRelative("poolSize").intValue = 10; entry.FindPropertyRelative("weight").floatValue = .33f;
        });
        Ref(enemies, "bossBornLocation", Named("ChiluLingzun_BossSpawn").transform);
        var players = All<PlayerManager>().FirstOrDefault();
        if (players == null) players = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/PlayerSystem.prefab"), scene)).GetComponent<PlayerManager>();
        var ui = All<UIManager>().FirstOrDefault();
        if (ui == null) ui = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Level/BattleUI.prefab"), scene)).GetComponentInChildren<UIManager>(true);
        foreach (var player in All<PlayerController>()) player.gameObject.SetActive(false);
        var camera = All<Camera>().First(c => c.gameObject.name == "Main Camera"); camera.tag = "MainCamera";
        var cm = camera.GetComponent<CameraManager>();
        Refs(cm, "cameraEdgeLeft", Enumerable.Range(0, 5).Select(i => (Object)Named("tranL" + i).transform).ToArray());
        Refs(cm, "cameraEdgeRight", Enumerable.Range(0, 5).Select(i => (Object)Named("tranR" + i).transform).ToArray());
        var spawn = Named("PlayerSpawn").transform;
        Physics2D.SyncTransforms(); var floor = Physics2D.Raycast(new Vector2(spawn.position.x, 20), Vector2.down, 100, 1 << 6);
        var playerBody = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Character/WuKong.prefab").GetComponent<BoxCollider2D>();
        if (floor.collider != null) spawn.position = new Vector3(spawn.position.x, floor.point.y - (playerBody.offset.y - playerBody.size.y * .5f) * playerBody.transform.localScale.y + .05f, 0);
        Ref(stage, "CM", cm); Ref(stage, "playerManager", players); Ref(stage, "enemyManager", enemies); Ref(stage, "ui", ui); Ref(stage, "tran", spawn);
        stage.enabled = true; enemies.enabled = true; cm.enabled = true;
        stage.ResolveSceneReferences();
        Debug.Log("RollingFurnace scene manager=" + enemies.GetType().Name + " HasBoss=" + enemies.HasBoss + " stage=" + new SerializedObject(stage).FindProperty("enemyManager").objectReferenceValue);
        Require(stage.ValidateConfiguration(out string error), "Level7: " + error);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
    public static void ValidateAssets()
    {
        foreach (string path in new[] { EnemyPath, WavePath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(root != null, path);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "missing script " + path);
        }
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        Require(enemy.GetComponent<Health>().BeginBlood == 144 && new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 22, "HP / XP");
        Require(enemy.GetComponentsInChildren<RollingFurnaceAttackBox>(true).Length == 3, "three attack boxes");
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

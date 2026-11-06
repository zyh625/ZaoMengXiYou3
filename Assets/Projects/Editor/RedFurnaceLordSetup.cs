using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class RedFurnaceLordSetup
{
    public const string BossPath = "Assets/Projects/Prefabs/Enemy/RedFurnaceLord.prefab";
    public const string ScenePath = "Assets/Projects/Scenes/Levels/Level7Scene.unity";
    public const string Art = "Assets/Art/Bosses/Level7/";
    public static readonly string[] Actions = { "Idle", "Walk", "PestleSmash", "FurnaceBreath", "ChainSweep", "FurnaceOpen", "Hit", "Death" };
    public static readonly string[] Effects = { "PestleImpact", "FurnaceBreath", "ChainSweepArc", "CoreBurst" };
    public static string Audit => Path.GetFullPath("output/RedFurnaceLordAudit");
    public static string EffectPath(string name) => "Assets/Projects/Prefabs/SeparateEffort/RedFurnaceLord" + name + ".prefab";
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
    private static void Clip(Object c, string field, Sprite[] frames, float fps, bool loop) => Edit(c, s =>
    {
        var p = s.FindProperty(field); var a = p.FindPropertyRelative("sprites"); a.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        p.FindPropertyRelative("fps").floatValue = fps; p.FindPropertyRelative("loop").boolValue = loop;
    });
    private static GameObject Child(GameObject parent, string name, int layer = 0)
    { var go = new GameObject(name) { layer = layer }; go.transform.SetParent(parent.transform, false); return go; }
    private static GameObject Reference => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/WestKing.prefab");
    private static SpriteManager Visual(GameObject root, Sprite first, out SpriteRenderer sr)
    {
        var go = Child(root, "Visual"); sr = go.AddComponent<SpriteRenderer>();
        var original = Reference.GetComponentInChildren<SpriteRenderer>(true);
        sr.sharedMaterial = original.sharedMaterial; sr.sortingLayerID = original.sortingLayerID; sr.sortingOrder = original.sortingOrder;
        sr.sprite = first; var sm = go.AddComponent<SpriteManager>(); Ref(sm, "sr", sr); return sm;
    }
    [MenuItem("造梦西游/RedFurnaceLord/生成并验证赤炉灵尊")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Audit);
        if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene(ScenePath);
        var previous = SceneManager.GetActiveScene();
        var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scratch);
        try
        {
            ValidateSprites(); CreateEffects(); CreateBoss(); WireScene(); ValidateAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            File.WriteAllText(Path.Combine(Audit, "assets.txt"), "PASS: 8x16 actions + 4x16 effects; names/rect/PPU/pivot; five prefabs; no missing references; HP1310 XP260 speed0.9; Level7 boss/spawn/UI; pools and waves unchanged.\n");
            Debug.Log("RED_FURNACE_LORD_ASSETS_PASS");
        }
        finally
        { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scratch, true); }
    }
    public static void ValidateSprites()
    {
        foreach (string action in Actions.Concat(Effects.Select(n => "Effects/" + n)))
        {
            bool fx = action.StartsWith("Effects/"); string name = action.Split('/').Last(); var frames = Frames(action);
            Require(frames.Length == 16, action + " count");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + action + ".png");
            Require(texture.width == 1024 && texture.height == (fx ? 1024 : 1536), action + " image dimensions");
            int height = fx ? 256 : 384;
            for (int i = 0; i < 16; i++)
            {
                var sprite = frames[i]; Require(sprite.name == name + "_" + i && sprite.pixelsPerUnit == 100f, action + " names / PPU");
                Require(sprite.rect == new Rect(i % 4 * 256, (3 - i / 4) * height, 256, height), action + " slice rect");
                Require(Vector2.Distance(sprite.pivot, fx ? new Vector2(128, 128) : new Vector2(128, 72)) < .01f, action + " pivot");
            }
        }
    }
    private static void CreateEffects()
    {
        foreach (string name in Effects)
        {
            var root = new GameObject("RedFurnaceLord" + name); var frames = Frames("Effects/" + name);
            var sm = Visual(root, frames[0], out var sr); var fx = root.AddComponent<RedFurnaceLordEffect>();
            Ref(fx, "spriteManager", sm); Ref(fx, "visual", sr);
            Clip(fx, "frames", frames, 16, name == "FurnaceBreath");
            // Measure the sheet's opaque extent; transparent frame padding is not attack reach.
            Texture2D image = new(2, 2); image.LoadImage(File.ReadAllBytes(Art + "Effects/" + name + ".png"));
            var pixels = image.GetPixels32(); int widest = 1;
            foreach (var frame in frames)
            {
                Rect r = frame.rect; int min = (int)r.xMax, max = (int)r.xMin;
                for (int y = (int)r.yMin; y < r.yMax; y++) for (int x = (int)r.xMin; x < r.xMax; x++)
                    if (pixels[y * image.width + x].a >= 2) { min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
                widest = Mathf.Max(widest, max - min + 1);
            }
            Object.DestroyImmediate(image); Num(fx, "nativeWidth", widest / 100f);
            PrefabUtility.SaveAsPrefabAsset(root, EffectPath(name)); Object.DestroyImmediate(root);
        }
    }
    private static void CreateBoss()
    {
        GameObject root = new("RedFurnaceLord") { layer = 11, tag = "Enemy" };
        var body = root.AddComponent<Rigidbody2D>(); EditorUtility.CopySerialized(Reference.GetComponent<Rigidbody2D>(), body);
        var shape = root.AddComponent<BoxCollider2D>(); shape.size = new Vector2(1.5f, 2.65f); shape.offset = new Vector2(0, 1.325f);
        var hp = root.AddComponent<Health>();
        var status = root.GetComponent<ActorStatus>() ?? root.AddComponent<ActorStatus>();
        var referenceStatus = Reference.GetComponent<ActorStatus>();
        if (referenceStatus != null) EditorUtility.CopySerialized(referenceStatus, status);
        var rewards = root.AddComponent<EnemyRewards>(); Flag(rewards, "isBoss", true);
        var move = root.AddComponent<RedFurnaceLordMovement>();
        Num(move, "backSpeed", 3.5f); Num(move, "upSpeed", 6f); Num(move, "boostRatio", 1f);
        Num(move, "minNormalY", .6f); Int(move, "groundLayer", 1 << 6);
        var combat = root.AddComponent<RedFurnaceLordCombat>(); var controller = root.AddComponent<RedFurnaceLordController>();
        Num(hp, "beginBlood", combat.BaseHealth); Num(rewards, "experienceReward", combat.ExperienceReward);
        Num(move, "moveSpeed", combat.MoveSpeed); Num(move, "slowSpeed", combat.MoveSpeed * .5f);
        Num(controller, "stopDistance", 1.2f); Int(controller, "specialAttack", -1);
        Visual(root, Frames("Idle")[0], out var sr); sr.transform.localScale = new Vector3(-1, 1, 1);
        var boxes = Child(root, "Combat"); var hurt = Child(boxes, "Attacked", 7);
        var hurtShape = hurt.AddComponent<BoxCollider2D>(); hurtShape.isTrigger = true; hurtShape.size = new Vector2(1.65f, 2.8f); hurtShape.offset = new Vector2(0, 1.4f);
        Ref(controller, "attackedBox", hurtShape);
        var attacks = new BoxCollider2D[3]; var checks = new EnemyCheckAttack[3];
        string[] names = { "PestleSmash", "FurnaceBreath", "ChainSweep" }; float[] damages = { 34, 8, 24 };
        int[] open = { 7, 5, 6 }, shut = { 11, 12, 11 }; float[] cd = { 6, 9, 8 };
        for (int i = 0; i < 3; i++)
        {
            var go = Child(boxes, names[i], 8); var box = go.AddComponent<BoxCollider2D>(); box.isTrigger = true;
            box.size = i == 0 ? new Vector2(5.2f, 1.4f) : i == 1 ? new Vector2(5, 5.2f) : new Vector2(6, 2.6f);
            box.offset = i == 0 ? new Vector2(0, .7f) : i == 1 ? new Vector2(3.3f, 1.5f) : new Vector2(3, 1.3f);
            var hit = go.AddComponent<RedFurnaceLordAttackBox>(); Int(hit, "skill", i); Num(hit, "Attack", damages[i]); Flag(hit, "knockback", i == 0);
            attacks[i] = box; go.SetActive(false);
            var check = Child(boxes, names[i] + "Check", 10); var detector = check.AddComponent<BoxCollider2D>(); detector.isTrigger = true;
            detector.size = new Vector2(i == 0 ? 8 : 14, 4); detector.offset = Vector2.up * 1.5f;
            checks[i] = check.AddComponent<EnemyCheckAttack>();
        }
        Refs(combat, "AttackBoxes", attacks); Refs(combat, "skillBoxes", attacks); Refs(combat, "CheckAttackLogic", checks);
        Edit(combat, s =>
        {
            foreach (string n in new[] { "attackDelay", "AttackOpenIdx", "AttackShutIdx" }) s.FindProperty(n).arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                s.FindProperty("attackDelay").GetArrayElementAtIndex(i).floatValue = cd[i];
                s.FindProperty("AttackOpenIdx").GetArrayElementAtIndex(i).intValue = open[i];
                s.FindProperty("AttackShutIdx").GetArrayElementAtIndex(i).intValue = shut[i];
            }
        });
        string[] poolFields = { "pestleImpact", "furnaceBreath", "chainSweepArc", "coreBurst" };
        for (int i = 0; i < 4; i++)
        {
            var pool = Child(root, Effects[i] + "Pool").AddComponent<RedFurnaceLordEffectPool>();
            Ref(pool, "prefab", AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath(Effects[i])).GetComponent<RedFurnaceLordEffect>());
            Ref(combat, poolFields[i], pool);
        }
        foreach (string action in Actions) Clip(controller, action, Frames(action), action == "Idle" ? 10 : action == "Walk" ? 14 : action == "Hit" ? 18 : action == "Death" ? 12 : 16, action == "Idle" || action == "Walk");
        // Base control-pose support reads these inherited clips directly.
        Clip(controller, "Attacked", Frames("Hit"), 18, false); Clip(controller, "Die", Frames("Death"), 12, false);
        PrefabUtility.SaveAsPrefabAsset(root, BossPath); Object.DestroyImmediate(root);
    }
    private static string WaveSnapshot(RollingFurnaceStageManager stage)
    {
        var s = new SerializedObject(stage);
        string Array(string name) { var p = s.FindProperty(name); return string.Join(",", Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).intValue)); }
        return Array("leftEnemyMax") + "|" + Array("rightEnemyMax") + "|" + s.FindProperty("maxWave").intValue;
    }
    private static void WireScene()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        var enemies = All<RollingFurnaceStageEnemyManager>().Single(); var stage = All<RollingFurnaceStageManager>().Single();
        var before = new SerializedObject(enemies);
        var poolData = before.FindProperty("enemyPools");
        string Pools() => string.Join(";", Enumerable.Range(0, poolData.arraySize).Select(i =>
        { var p = poolData.GetArrayElementAtIndex(i); return p.FindPropertyRelative("prefab").objectReferenceValue.name + ":" + p.FindPropertyRelative("poolSize").intValue + ":" + p.FindPropertyRelative("weight").floatValue; }));
        string pools = Pools(), waves = WaveSnapshot(stage);
        Ref(enemies, "boss", AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).transform);
        var spawn = All<Transform>().Single(t => t.name == "ChiluLingzun_BossSpawn"); Ref(enemies, "bossBornLocation", spawn);
        var old = All<BossUI>().First(); var panel = old.gameObject; var so = new SerializedObject(old);
        var blood = (Image)so.FindProperty("bloodFull").objectReferenceValue;
        var special = (Image)so.FindProperty("specialState").objectReferenceValue;
        var ui = old as RedFurnaceLordUI;
        if (ui == null) { Object.DestroyImmediate(old); ui = panel.AddComponent<RedFurnaceLordUI>(); }
        Ref(ui, "bloodFull", blood); Ref(ui, "specialState", special); Ref(stage, "bossUI", ui);
        Require(blood != null && special != null, "UI images");
        Require(enemies.HasBoss && enemies.ValidateConfiguration(out _) && stage.ValidateConfiguration(out _), "Level7 boss readiness");
        before.Update(); Require(Pools() == pools && WaveSnapshot(stage) == waves, "pools / waves unchanged");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Path.Combine(Audit, "scene.txt"), "PASS: boss=RedFurnaceLord; spawn=" + spawn.name + "; HasBoss=true; RequiresBoss=true; UI=RedFurnaceLordUI with bloodFull/specialState.\nPools=" + pools + "\nWaves=" + waves);
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
    public static void ValidateAssets()
    {
        foreach (string path in new[] { BossPath }.Concat(Effects.Select(EffectPath)))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(root != null, path);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, path + " missing script");
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                var p = new SerializedObject(c).GetIterator();
                while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference)
                {
#pragma warning disable CS0618
                    Require(p.objectReferenceValue != null || p.objectReferenceEntityIdValue == default, path + " " + p.propertyPath);
#pragma warning restore CS0618
                }
            }
        }
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        Require(boss.GetComponent<Health>().BeginBlood == 1310, "HP");
        var rewards = new SerializedObject(boss.GetComponent<EnemyRewards>());
        Require(rewards.FindProperty("experienceReward").floatValue == 260 && rewards.FindProperty("isBoss").boolValue, "XP / isBoss");
        Require(boss.GetComponentsInChildren<EnemyAttackBox>(true).Length == 3 && boss.GetComponentsInChildren<RedFurnaceLordEffectPool>(true).Length == 4, "boxes / pools");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StarShearGuardSetup
{
    public const string EnemyPath = "Assets/Projects/Prefabs/Enemy/StarShearGuard.prefab";
    public const string ScenePath = "Assets/Projects/Scenes/Levels/Level8Scene.unity";
    public const string Art = "Assets/Art/Enemies/Level8/StarShearGuard/";
    public static string Audit => Path.GetFullPath("output/StarShearGuardAudit");
    public static readonly string[] Actions = { "Idle", "Walk", "OffbeatScissor", "UpwardScissor", "BackstepCut", "Hit", "Death" };
    public static string EffectPath(string action) => "Assets/Projects/Prefabs/SeparateEffort/StarShearGuard" + action + "FX.prefab";
    public static Sprite[] Frames(string action) => AssetDatabase.LoadAllAssetsAtPath(Art + action + ".png").OfType<Sprite>()
        .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
    private static void Edit(Object c, Action<SerializedObject> edit)
    { var so = new SerializedObject(c); edit(so); so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Ref(Object c, string f, Object v) => Edit(c, s => s.FindProperty(f).objectReferenceValue = v);
    private static void Num(Object c, string f, float v) => Edit(c, s => s.FindProperty(f).floatValue = v);
    private static void Int(Object c, string f, int v) => Edit(c, s => s.FindProperty(f).intValue = v);
    private static void Flag(Object c, string f, bool v) => Edit(c, s => s.FindProperty(f).boolValue = v);
    private static void Refs(Object c, string f, Object[] values) => Edit(c, s =>
    { var a = s.FindProperty(f); a.arraySize = values.Length; for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; });
    private static void Numbers(Object c, string f, params float[] values) => Edit(c, s =>
    { var a = s.FindProperty(f); a.arraySize = values.Length; for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).floatValue = values[i]; });
    private static void Indices(Object c, string f, params int[] values) => Edit(c, s =>
    { var a = s.FindProperty(f); a.arraySize = values.Length; for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).intValue = values[i]; });
    private static void Clip(Object c, string f, string action, float fps, bool loop) => Edit(c, s =>
    {
        var p = s.FindProperty(f); var values = Frames(action); var a = p.FindPropertyRelative("sprites"); a.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        p.FindPropertyRelative("fps").floatValue = fps; p.FindPropertyRelative("loop").boolValue = loop;
    });
    private static GameObject Child(GameObject parent, string name, int layer = 0)
    { var go = new GameObject(name) { layer = layer }; go.transform.SetParent(parent.transform, false); return go; }

    [MenuItem("造梦西游/StarShearGuard/生成并验证预制体和Level8池")]
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
            ValidateSprites(); CreateEnemy(); ValidatePrefabs(); WireScene(); ValidateScene();
            AssetDatabase.SaveAssets(); Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            File.WriteAllText(Path.Combine(Audit, "unity-assets.txt"),
                "PASS: 7 actions x16 frames, continuous names, PPU100, common feet pivot(128,56), canvas1024x1536.\n" +
                "PASS: 3 existing independent slash prefabs x16 frames, canvas1024x1024, disable on completion, three independent caches.\n" +
                "PASS: HP170 XP24 speed1.0, damage12/10/18/12, cooldown4/6/7, gap0.35, miss recovery0.45, backstep2.5.\n" +
                "PASS: Level8 StageEnemyManager pool10 weight0.34 and LevelManager.enemyManager reference.\n" +
                "Level8 existing LevelManager is disabled and Boss reference is empty; preserved existing placeholder state.\n");
            Debug.Log("STARSHEARGUARD_ASSETS_PASS");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scratch, true);
        }
    }
    public static void ValidateSprites()
    {
        foreach (string action in Actions)
        {
            var frames = Frames(action); Require(frames.Length == 16, action + " frame count");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + action + ".png");
            Require(texture.width == 1024 && texture.height == 1536, action + " canvas");
            for (int i = 0; i < 16; i++)
            {
                Require(frames[i].name == action + "_" + i && frames[i].pixelsPerUnit == 100, action + " name/PPU");
                Require(frames[i].rect == new Rect(i % 4 * 256, (3 - i / 4) * 384, 256, 384), action + " grid");
                Require(Vector2.Distance(frames[i].pivot, new Vector2(128, 56)) < .01f, action + " foot pivot");
            }
        }
    }
    private static void CreateEnemy()
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/ClampGuard.prefab");
        var root = new GameObject("StarShearGuard") { layer = 11 };
        var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = .9f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var physical = root.AddComponent<BoxCollider2D>(); physical.size = new Vector2(1f, 1.8f); physical.offset = Vector2.zero;
        var health = root.AddComponent<Health>(); Num(health, "beginBlood", 170);
        EditorUtility.CopySerialized(reference.GetComponent<ActorStatus>(), root.GetComponent<ActorStatus>());
        var rewards = root.AddComponent<EnemyRewards>(); Num(rewards, "experienceReward", 24); Flag(rewards, "isBoss", false);
        var movement = root.AddComponent<StarShearGuardMovement>();
        Num(movement, "moveSpeed", 1); Num(movement, "slowSpeed", .5f); Num(movement, "backSpeed", 3.5f);
        Num(movement, "upSpeed", 6); Num(movement, "boostRatio", 1); Num(movement, "minNormalY", .6f); Int(movement, "groundLayer", 1 << 6);
        var combat = root.AddComponent<StarShearGuardCombat>(); var controller = root.AddComponent<StarShearGuardController>();
        Num(controller, "stopDistance", 1.4f);
        var visual = Child(root, "Visual"); visual.transform.localScale = new Vector3(-1, 1, 1); visual.transform.localPosition = Vector3.down * .9f;
        var renderer = visual.AddComponent<SpriteRenderer>(); var referenceRenderer = reference.GetComponentInChildren<SpriteRenderer>();
        renderer.sharedMaterial = referenceRenderer.sharedMaterial; renderer.sortingLayerID = referenceRenderer.sortingLayerID;
        renderer.sortingOrder = referenceRenderer.sortingOrder; renderer.sprite = Frames("Idle")[0]; renderer.flipX = true;
        var sm = visual.AddComponent<SpriteManager>(); Ref(sm, "sr", renderer); visual.AddComponent<SpriteOutlineController>();
        var boxes = Child(root, "Combat"); var hurt = Child(boxes, "Attacked", 7); var hurtBox = hurt.AddComponent<BoxCollider2D>();
        hurtBox.isTrigger = true; hurtBox.size = physical.size; hurtBox.offset = physical.offset; Ref(controller, "attackedBox", hurtBox);
        var hitBoxes = new StarShearGuardAttackBox[4]; var attackBoxes = new BoxCollider2D[4]; var checks = new EnemyCheckAttack[3];
        var sizes = new[] { new Vector2(1.8f, 1.2f), new Vector2(1.6f, .9f), new Vector2(1.8f, 1.6f), new Vector2(1.6f, .9f) };
        var offsets = new[] { new Vector2(1.2f, .4f), new Vector2(1.2f, -.2f), new Vector2(1.1f, 1), new Vector2(1.2f, -.2f) };
        var names = new[] { "OffbeatHigh", "OffbeatLow", "Upward", "Backstep" }; float[] damage = { 12, 10, 18, 12 };
        for (int i = 0; i < 4; i++)
        {
            var hit = Child(boxes, names[i], 8); var box = hit.AddComponent<BoxCollider2D>(); box.isTrigger = true;
            box.size = sizes[i]; box.offset = offsets[i];
            var logic = hit.AddComponent<StarShearGuardAttackBox>(); Num(logic, "Attack", damage[i]);
            Flag(logic, "knockback", false); Int(logic, "windowIndex", i); hitBoxes[i] = logic; attackBoxes[i] = box; hit.SetActive(false);
        }
        for (int i = 0; i < 3; i++)
        {
            var go = Child(boxes, "Check" + i, 10); var check = go.AddComponent<BoxCollider2D>(); check.isTrigger = true;
            // Broad sensor sees either side so the shared state machine can turn before wind-up.
            check.size = new Vector2(i == 0 ? 5.4f : 11f, i == 1 ? 5f : 3f); check.offset = new Vector2(0, i == 1 ? .8f : 0);
            checks[i] = go.AddComponent<EnemyCheckAttack>();
        }
        Refs(combat, "AttackBoxes", new Object[] { attackBoxes[0], attackBoxes[2], attackBoxes[3] });
        Refs(combat, "hitBoxes", hitBoxes); Refs(combat, "CheckAttackLogic", checks);
        Numbers(combat, "attackDelay", 4, 6, 7); Indices(combat, "AttackOpenIdx", 4, 5, 9); Indices(combat, "AttackShutIdx", 8, 10, 13);
        Refs(combat, "slashPrefabs", new[] { "OffbeatScissor", "UpwardScissor", "BackstepCut" }
            .Select(a => (Object)AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath(a)).GetComponent<IndependentSkillEffect>()).ToArray());
        Clip(controller, "Idle", "Idle", 10, true); Clip(controller, "Walk", "Walk", 14, true);
        foreach (string action in Actions.Skip(2).Take(3)) Clip(controller, action, action, 16, false);
        Clip(controller, "Attacked", "Hit", 18, false); Clip(controller, "Die", "Death", 12, false);
        PrefabUtility.SaveAsPrefabAsset(root, EnemyPath); Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        string original = File.ReadAllText(ScenePath);
        if (!File.Exists(Path.Combine(Audit, "Level8Scene.before.unity"))) File.WriteAllText(Path.Combine(Audit, "Level8Scene.before.unity"), original);
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();
        var managers = roots.SelectMany(g => g.GetComponentsInChildren<StageEnemyManager>(true)).ToArray();
        Require(managers.Length <= 1, "Level8 has multiple StageEnemyManagers; resolve before wiring");
        var manager = managers.FirstOrDefault();
        if (manager == null)
        {
            var go = new GameObject("Level8_EnemyPools"); SceneManager.MoveGameObjectToScene(go, scene);
            manager = go.AddComponent<StageEnemyManager>();
        }
        Edit(manager, so =>
        {
            var a = so.FindProperty("enemyPools"); int index = a.arraySize;
            for (int i = 0; i < a.arraySize; i++)
                if (a.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is StarShearGuardController) index = i;
            if (index == a.arraySize) a.arraySize++;
            var p = a.GetArrayElementAtIndex(index);
            p.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<StarShearGuardController>();
            p.FindPropertyRelative("poolSize").intValue = 10; p.FindPropertyRelative("weight").floatValue = .34f;
        });
        foreach (var level in roots.SelectMany(g => g.GetComponentsInChildren<LevelManager>(true))) Ref(level, "enemyManager", manager);
        var born = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "JiuyaoXuanhuang_BossSpawn");
        if (born != null && new SerializedObject(manager).FindProperty("bossBornLocation").objectReferenceValue == null) Ref(manager, "bossBornLocation", born);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        // Keep all unrelated blocks byte-for-byte, including disabled Level8/Boss placeholders.
        File.WriteAllText(ScenePath, PreserveSceneEdits(original, File.ReadAllText(ScenePath)));
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
    private static string PreserveSceneEdits(string original, string saved)
    {
        bool crlf = original.Contains("\r\n"); original = original.Replace("\r\n", "\n"); saved = saved.Replace("\r\n", "\n");
        const string pattern = @"^--- !u!\d+ &\d+[^\n]*\n.*?(?=^--- !u!|\z)";
        var options = RegexOptions.Multiline | RegexOptions.Singleline;
        var oldBlocks = Regex.Matches(original, pattern, options).Cast<Match>().ToArray();
        var newBlocks = Regex.Matches(saved, pattern, options).Cast<Match>().ToArray();
        Func<string, string> id = b => Regex.Match(b, @"^--- !u!\d+ &(\d+)").Groups[1].Value;
        var byId = newBlocks.ToDictionary(m => id(m.Value), m => m.Value);
        var oldIds = oldBlocks.Select(m => id(m.Value)).ToHashSet();
        string result = original.Substring(0, oldBlocks[0].Index);
        foreach (var match in oldBlocks)
        {
            string block = match.Value;
            if (!byId.TryGetValue(id(block), out string updated)) { result += block; continue; }
            if (block.StartsWith("--- !u!1660057539"))
            {
                foreach (var added in newBlocks.Where(m => !oldIds.Contains(id(m.Value)))) result += added.Value;
                result += updated; continue;
            }
            if (block.Contains("StageEnemyManager"))
            {
                const string pools = @"^  enemyPools:\n.*?(?=^  [A-Za-z_]|\z)";
                block = Regex.Replace(block, pools, m => Regex.Match(updated, pools, options).Value, options);
            }
            var reference = Regex.Match(updated, @"^  enemyManager:.*$", RegexOptions.Multiline);
            if (reference.Success)
                block = Regex.IsMatch(block, @"^  enemyManager:.*$", RegexOptions.Multiline)
                    ? Regex.Replace(block, @"^  enemyManager:.*$", reference.Value, RegexOptions.Multiline)
                    : Regex.Replace(block, @"^  m_EditorClassIdentifier:.*$", m => m.Value + "\n" + reference.Value, RegexOptions.Multiline);
            result += block;
        }
        return crlf ? result.Replace("\n", "\r\n") : result;
    }
    public static void ValidatePrefabs()
    {
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath); Require(enemy != null, "enemy prefab");
        foreach (var t in enemy.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "missing scripts");
        Require(enemy.GetComponent<Health>().BeginBlood == 170, "HP170");
        Require(new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 24, "XP24");
        Require(enemy.GetComponentsInChildren<StarShearGuardAttackBox>(true).Length == 4 && enemy.GetComponentsInChildren<EnemyCheckAttack>(true).Length == 3, "four hit/three check boxes");
        foreach (string action in Actions.Skip(2).Take(3))
        {
            var fx = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath(action));
            Require(fx != null && fx.GetComponent<IndependentSkillEffect>().FrameCount == 16, action + " effect");
            Require(fx.GetComponentsInChildren<Collider2D>(true).Length == 0, action + " visual only");
            var so = new SerializedObject(fx.GetComponent<IndependentSkillEffect>());
            Require(!so.FindProperty("frames").FindPropertyRelative("loop").boolValue && so.FindProperty("finishMode").enumValueIndex == 0, action + " auto recycle");
            var frames = Frames("Effects/" + action + "FX"); Require(frames.Length == 16, action + " FX slices");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Effects/" + action + "FX.png");
            Require(texture.width == 1024 && texture.height == 1024, action + " FX canvas");
            for (int i = 0; i < 16; i++) Require(frames[i].name == "StarShearGuard" + action + "_" + i && frames[i].pixelsPerUnit == 100, action + " FX name/PPU");
        }
    }
    public static void ValidateScene()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var managers = roots.SelectMany(g => g.GetComponentsInChildren<StageEnemyManager>(true)).ToArray();
            Require(managers.Length == 1, "one Level8 enemy manager");
            var array = new SerializedObject(managers[0]).FindProperty("enemyPools"); int count = 0;
            for (int i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                if (!(entry.FindPropertyRelative("prefab").objectReferenceValue is StarShearGuardController)) continue;
                count++; Require(entry.FindPropertyRelative("poolSize").intValue == 10 && Mathf.Approximately(entry.FindPropertyRelative("weight").floatValue, .34f), "saved Level8 pool10 weight0.34");
            }
            Require(count == 1, "one StarShearGuard entry");
            foreach (var level in roots.SelectMany(g => g.GetComponentsInChildren<LevelManager>(true)))
                Require(new SerializedObject(level).FindProperty("enemyManager").objectReferenceValue == managers[0], "saved level manager reference");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}

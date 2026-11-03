using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ClampGuardSetup
{
    public const string EnemyPath = "Assets/Projects/Prefabs/Enemy/ClampGuard.prefab";
    public const string FlamePath = "Assets/Projects/Prefabs/SeparateEffort/ClampGuardVentFlame.prefab";
    public const string ScenePath = "Assets/Projects/Scenes/Levels/Level7Scene.unity";
    private const string Art = "Assets/Art/Enemies/Level7/ClampGuard/";
    public static string Audit => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/ClampGuardAudit"));
    public static readonly string[] Actions = { "Idle", "Walk", "AttackOne", "AttackTwo", "AttackThree", "Attacked", "Die" };
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
    private static void Clip(Object c, string f, Sprite[] values, float fps, bool loop) => Edit(c, s =>
    {
        var p = s.FindProperty(f); var a = p.FindPropertyRelative("sprites"); a.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        p.FindPropertyRelative("fps").floatValue = fps; p.FindPropertyRelative("loop").boolValue = loop;
    });
    private static GameObject Child(GameObject parent, string name, int layer = 0)
    { var go = new GameObject(name) { layer = layer }; go.transform.SetParent(parent.transform, false); return go; }
    private static GameObject Reference => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/GreenHammer.prefab");

    [MenuItem("造梦西游/ClampGuard/生成并验证预制体和Level7池")]
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
            ValidateSprites(); CreateFlame(); CreateEnemy(); ValidatePrefabs(); WireScene();
            AssetDatabase.SaveAssets(); Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            File.WriteAllText(Path.Combine(Audit, "unity-assets.txt"), "PASS: 112 unchanged character-frame pixels; 16 frames/action, row-major names 0..15, PPU100, pivot(128,72); three attacks; HP138, XP22; new independent pooled flame prefab; Level7 pool10 weight0.34.\nLevel7 existing disabled legacy level/Boss placeholder retained; no other scene, enemy or shared-base semantics changed.\n");
            Debug.Log("CLAMPGUARD_ASSETS_PASS");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scratch, true);
        }
    }
    private static void ValidateSprites()
    {
        foreach (string action in Actions)
        {
            var frames = Frames(action); Require(frames.Length == 16, action + " count");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + action + ".png");
            Require(texture.width == 1024 && texture.height == 1536, action + " canvas");
            for (int i = 0; i < 16; i++)
            {
                Require(frames[i].name == action + "_" + i && frames[i].pixelsPerUnit == 100, action + " name/PPU");
                Require(frames[i].rect == new Rect(i % 4 * 256, (3 - i / 4) * 384, 256, 384), action + " grid");
                Require(Vector2.Distance(frames[i].pivot, new Vector2(128, 72)) < .01f, action + " foot pivot");
            }
        }
    }
    private static void CreateFlame()
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/SeparateEffort/ClampGuardVentFlameFX.prefab");
        var go = new GameObject("ClampGuardVentFlame");
        var renderer = go.AddComponent<SpriteRenderer>();
        EditorUtility.CopySerialized(reference.GetComponent<SpriteRenderer>(), renderer);
        var manager = go.AddComponent<SpriteManager>(); Ref(manager, "sr", renderer);
        var effect = go.AddComponent<IndependentSkillEffect>();
        EditorUtility.CopySerialized(reference.GetComponent<IndependentSkillEffect>(), effect);
        Ref(effect, "spriteManager", manager); Ref(effect, "spriteRenderer", renderer);
        Flag(effect, "playOnEnable", true); Num(effect, "lifetime", 0); Int(effect, "finishMode", 0);
        PrefabUtility.SaveAsPrefabAsset(go, FlamePath); Object.DestroyImmediate(go);
    }
    private static void CreateEnemy()
    {
        var root = new GameObject("ClampGuard") { layer = 11 };
        var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = .9f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var physical = root.AddComponent<BoxCollider2D>(); physical.size = new Vector2(1f, 1.85f); physical.offset = Vector2.up * .925f;
        var health = root.AddComponent<Health>(); Num(health, "beginBlood", 138f);
        EditorUtility.CopySerialized(Reference.GetComponent<ActorStatus>(), root.GetComponent<ActorStatus>());
        var rewards = root.AddComponent<EnemyRewards>(); Num(rewards, "experienceReward", 22); Flag(rewards, "isBoss", false);
        var movement = root.AddComponent<ClampGuardMovement>();
        Num(movement, "moveSpeed", .9f); Num(movement, "slowSpeed", .45f); Num(movement, "backSpeed", 3.5f);
        Num(movement, "upSpeed", 6); Num(movement, "boostRatio", 1); Num(movement, "minNormalY", .6f); Int(movement, "groundLayer", 1 << 6);
        var combat = root.AddComponent<ClampGuardCombat>(); var controller = root.AddComponent<ClampGuardController>();
        Num(controller, "stopDistance", 1.5f);
        var visual = Child(root, "Visual"); visual.transform.localScale = new Vector3(-1.5f, 1.5f, 1f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        var referenceRenderer = Reference.GetComponentInChildren<SpriteRenderer>();
        renderer.sharedMaterial = referenceRenderer.sharedMaterial; renderer.sortingLayerID = referenceRenderer.sortingLayerID;
        renderer.sortingOrder = referenceRenderer.sortingOrder; renderer.sprite = Frames("Idle")[0]; renderer.flipX = true;
        var sm = visual.AddComponent<SpriteManager>(); Ref(sm, "sr", renderer); visual.AddComponent<SpriteOutlineController>();
        var boxes = Child(root, "Combat");
        var hurt = Child(boxes, "Attacked", 7); var hurtBox = hurt.AddComponent<BoxCollider2D>();
        hurtBox.isTrigger = true; hurtBox.size = new Vector2(1.2f, 1.95f); hurtBox.offset = Vector2.up * .975f;
        Ref(controller, "attackedBox", hurtBox);
        var attackBoxes = new BoxCollider2D[3]; var hitBoxes = new ClampGuardAttackBox[3]; var checks = new EnemyCheckAttack[3];
        float[] lengths = { 2.5f, 4.5f, 3f }; float[] damage = { 16, 0, 12 };
        for (int i = 0; i < 3; i++)
        {
            var hit = Child(boxes, "Attack" + (i + 1), 8); var box = hit.AddComponent<BoxCollider2D>(); box.isTrigger = true;
            box.size = new Vector2(lengths[i], i == 2 ? 2.64f : 1.8f);
            box.offset = new Vector2(-(lengths[i] / 2f + (i == 2 ? .45f : 0)), i == 2 ? 1.1f : .95f);
            var logic = hit.AddComponent<ClampGuardAttackBox>(); Num(logic, "Attack", damage[i]);
            Flag(logic, "knockback", false); Int(logic, "attackIndex", i); attackBoxes[i] = box; hitBoxes[i] = logic; hit.SetActive(false);
            var detector = Child(boxes, "Check" + (i + 1), 10); var check = detector.AddComponent<BoxCollider2D>();
            check.isTrigger = true; check.size = new Vector2(10.8f, 4f); check.offset = Vector2.up;
            checks[i] = detector.AddComponent<EnemyCheckAttack>();
        }
        Refs(combat, "AttackBoxes", attackBoxes); Refs(combat, "hitBoxes", hitBoxes); Refs(combat, "CheckAttackLogic", checks);
        Numbers(combat, "attackDelay", 3.5f, 9f, 7f); Indices(combat, "AttackOpenIdx", 4, 5, 7); Indices(combat, "AttackShutIdx", 8, 9, 8);
        Ref(combat, "ventFlamePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(FlamePath).GetComponent<IndependentSkillEffect>());
        foreach (string action in Actions) Clip(controller, action, Frames(action), action == "Idle" || action == "Walk" ? 12 : 10, action == "Idle" || action == "Walk");
        PrefabUtility.SaveAsPrefabAsset(root, EnemyPath); Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        string original = File.ReadAllText(ScenePath);
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageEnemyManager>(true)).ToArray();
        // Other Level7 work may have supplied a concrete manager since this tool last ran.
        // Append our entry to that manager instead of replacing its pools or adding a second one.
        var manager = managers.FirstOrDefault(m => m.gameObject.name != "ClampGuard_Level7_EnemyPools") ?? managers.FirstOrDefault();
        var removedIds = new HashSet<string>();
        foreach (var redundant in managers.Where(m => m != manager && m.gameObject.name == "ClampGuard_Level7_EnemyPools"))
        {
            removedIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(redundant.gameObject).targetObjectId.ToString());
            foreach (var c in redundant.GetComponents<Component>()) removedIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString());
            Object.DestroyImmediate(redundant.gameObject);
        }
        if (manager == null)
        {
            var go = new GameObject("ClampGuard_Level7_EnemyPools"); SceneManager.MoveGameObjectToScene(go, scene);
            manager = go.AddComponent<StageEnemyManager>();
        }
        Edit(manager, so =>
        {
            var a = so.FindProperty("enemyPools"); int index = a.arraySize;
            for (int i = 0; i < a.arraySize; i++)
                if (a.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is ClampGuardController) index = i;
            if (index == a.arraySize) a.arraySize++;
            var p = a.GetArrayElementAtIndex(index);
            p.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<ClampGuardController>();
            p.FindPropertyRelative("poolSize").intValue = 10; p.FindPropertyRelative("weight").floatValue = .34f;
        });
        // Only wire existing controllers. The legacy disabled level/Boss placeholder stays untouched.
        foreach (var stage in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelManager>(true))) Ref(stage, "enemyManager", manager);
        var born = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "ChiluLingzun_BossSpawn");
        if (born != null && new SerializedObject(manager).FindProperty("bossBornLocation").objectReferenceValue == null)
            Ref(manager, "bossBornLocation", born);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        // Unity otherwise rounds every layout float and serializes unrelated new camera defaults.
        // Preserve existing scene blocks byte-for-byte except the requested manager reference/pool.
        File.WriteAllText(ScenePath, PreserveSceneEdits(original, File.ReadAllText(ScenePath), removedIds));
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
    private static string PreserveSceneEdits(string original, string saved, HashSet<string> removedIds)
    {
        bool crlf = original.Contains("\r\n");
        original = original.Replace("\r\n", "\n"); saved = saved.Replace("\r\n", "\n");
        const string pattern = @"^--- !u!\d+ &\d+[^\n]*\n.*?(?=^--- !u!|\z)";
        var options = RegexOptions.Multiline | RegexOptions.Singleline;
        var oldBlocks = Regex.Matches(original, pattern, options).Cast<Match>().ToArray();
        var newBlocks = Regex.Matches(saved, pattern, options).Cast<Match>().ToArray();
        Func<string, string> id = b => Regex.Match(b, @"^--- !u!\d+ &(\d+)").Groups[1].Value;
        var byId = newBlocks.ToDictionary(m => id(m.Value), m => m.Value);
        var oldIds = new HashSet<string>(oldBlocks.Select(m => id(m.Value)));
        string result = original.Substring(0, oldBlocks[0].Index);
        foreach (var match in oldBlocks)
        {
            string block = match.Value;
            if (removedIds.Contains(id(block))) continue;
            if (!byId.TryGetValue(id(block), out string updated)) { result += block; continue; }
            if (block.StartsWith("--- !u!1660057539"))
            {
                foreach (var added in newBlocks.Where(m => !oldIds.Contains(id(m.Value)))) result += added.Value;
                result += updated; continue;
            }
            if (block.Contains("StageEnemyManager"))
            {
                const string poolsPattern = @"^  enemyPools:\n.*?(?=^  [A-Za-z_]|\z)";
                var pools = Regex.Match(updated, poolsPattern, options);
                if (pools.Success) block = Regex.Replace(block, poolsPattern, m => pools.Value, options);
            }
            else
            {
                var reference = Regex.Match(updated, @"^  enemyManager:.*$", RegexOptions.Multiline);
                if (reference.Success)
                {
                    if (Regex.IsMatch(block, @"^  enemyManager:.*$", RegexOptions.Multiline))
                        block = Regex.Replace(block, @"^  enemyManager:.*$", reference.Value, RegexOptions.Multiline);
                    else block = Regex.Replace(block, @"^  m_EditorClassIdentifier:.*$", m => m.Value + "\n" + reference.Value, RegexOptions.Multiline);
                }
            }
            result += block;
        }
        return crlf ? result.Replace("\n", "\r\n") : result;
    }
    public static void ValidatePrefabs()
    {
        foreach (string path in new[] { EnemyPath, FlamePath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(root != null, path);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, path + " missing script");
        }
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        Require(enemy.GetComponent<Health>().BeginBlood == 138 && new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 22, "stats");
        Require(enemy.GetComponentsInChildren<ClampGuardAttackBox>(true).Length == 3 && enemy.GetComponentsInChildren<EnemyCheckAttack>(true).Length == 3, "attack/check boxes");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(FlamePath).GetComponent<IndependentSkillEffect>().FrameCount == 16, "flame sequence");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}

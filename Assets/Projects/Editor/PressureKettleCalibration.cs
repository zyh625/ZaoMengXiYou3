using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Updates existing prefab contents only. Never invokes the original setup/rebuild tool.
[InitializeOnLoad]
public static class PressureKettleCalibration
{
    public const string Audit = "Logs/PressureKettleCalibration";
    private const string Enemy = "Assets/Projects/Prefabs/Enemy/PressureKettle.prefab";
    private const string Fx = "Assets/Projects/Prefabs/SeparateEffort/PressureKettle";
    private const string Art = "Assets/Art/Enemies/Level7/PressureKettle/";
    private const string Scene = "Assets/Projects/Scenes/Levels/Level7Scene.unity";
    private const string Key = "PressureKettle.Calibration";
    private static readonly StringBuilder report = new();
    private static double deadline;

    static PressureKettleCalibration() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
        Directory.CreateDirectory(Audit);
        try
        {
            string sceneHash = Hash(Scene);
            Calibrate();
            ValidateArt();
            ValidateEnemy();
            ValidateScene();
            Require(sceneHash == Hash(Scene), "Level7Scene bytes unchanged");
            File.WriteAllText(Path.Combine(Audit, "asset-validation.txt"), report.ToString() + "ALL PASS\n");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            // No stage waves or other enemies participate in the combat checks.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            File.WriteAllText(Path.Combine(Audit, "runtime-validation.txt"), "RUNNING\n");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Batch", true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(Audit, "asset-validation.txt"), report + "FAIL: " + e);
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
    private static void OnPlay(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
        {
            SessionState.SetBool(Key, false);
            var probe = new GameObject("PressureKettleCalibrationValidation").AddComponent<PressureKettleRuntimeValidation>();
            probe.IsolatedCalibration = true; probe.AuditDirectory = Audit;
            deadline = EditorApplication.timeSinceStartup + 120d;
            EditorApplication.update += Watchdog;
        }
        if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".Batch", false))
        {
            SessionState.SetBool(Key + ".Batch", false);
            EditorApplication.update -= Watchdog;
            string path = Path.Combine(Audit, "runtime-validation.txt");
            EditorApplication.Exit(File.Exists(path) && File.ReadAllText(path).Contains("ALL PASS") ? 0 : 1);
        }
    }
    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup <= deadline) return;
        File.AppendAllText(Path.Combine(Audit, "runtime-validation.txt"), "FAIL: runtime validation timeout\n");
        EditorApplication.update -= Watchdog; EditorApplication.Exit(1);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.AppendLine("PASS: " + message);
    }
    private static string Hash(string path)
    { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
    private static int Index(string name) => int.Parse(name.Substring(name.LastIndexOf('_') + 1));
    private static Sprite[] Frames(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => Index(s.name)).ToArray();
    private static void Set(Object target, Action<SerializedObject> change)
    { var so = new SerializedObject(target); change(so); so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Number(SerializedObject so, string name, float value) => so.FindProperty(name).floatValue = value;
    private static void Ref(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
    private static void Floats(SerializedObject so, string name, params float[] values)
    {
        var p = so.FindProperty(name); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).floatValue = values[i];
    }
    private static void Clip(SerializedObject so, string field, string action, float fps, bool loop)
    {
        var p = so.FindProperty(field); var sprites = p.FindPropertyRelative("sprites");
        Sprite[] frames = Frames(Art + "SpriteSheets16/" + action + ".png");
        Require(frames.Length == 16, action + " has 16 sprites before binding");
        sprites.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        p.FindPropertyRelative("fps").floatValue = fps; p.FindPropertyRelative("loop").boolValue = loop;
    }
    private static void UpdateExisting(string path, Action<GameObject> update)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        Require(!string.IsNullOrEmpty(guid), "existing asset " + path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try { update(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Require(AssetDatabase.AssetPathToGUID(path) == guid, "preserved GUID " + guid);
    }
    private static void Calibrate()
    {
        foreach (string kind in new[] { "Steam", "Bomb", "Burst" })
            UpdateExisting(Fx + kind + ".prefab", root => Set(root.GetComponent<PressureKettleEffect>(), so =>
            {
                Number(so, "steamDamage", 3f); Number(so, "steamTickInterval", 0.25f);
                so.FindProperty("steamTicks").intValue = 4; Number(so, "steamRange", 3.5f);
                so.FindProperty("steamKnockback").boolValue = true;
                Number(so, "bombDamage", 12f); Number(so, "bombFuse", 0.6f); Number(so, "bombRadius", 2f);
                Number(so, "burstDamage", 11f); Number(so, "burstRadius", 4f);
                so.FindProperty("burstKnockback").boolValue = true;
            }));
        UpdateExisting(Enemy, root =>
        {
            Set(root.GetComponent<Health>(), so => Number(so, "beginBlood", 96f));
            Set(root.GetComponent<EnemyRewards>(), so => Number(so, "experienceReward", 22f));
            Set(root.GetComponent<PressureKettleMovement>(), so => Number(so, "moveSpeed", 0.8f));
            Transform muzzle = root.transform.Find("Muzzle");
            Require(muzzle != null, "reuse existing Muzzle child");
            muzzle.localPosition = new Vector3(0.7f, 0.75f, 0f);
            Set(root.GetComponent<PressureKettleCombat>(), so =>
            {
                Ref(so, "muzzle", muzzle);
                Ref(so, "steamPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Steam.prefab").GetComponent<PressureKettleEffect>());
                Ref(so, "bombPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Bomb.prefab").GetComponent<PressureKettleEffect>());
                Ref(so, "burstPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Burst.prefab").GetComponent<PressureKettleEffect>());
                Floats(so, "cooldowns", 6f, 8f, 12f); Floats(so, "windups", 5f / 16f, 8f / 16f, 7f / 16f);
                var frames = so.FindProperty("releaseFrames"); frames.arraySize = 3;
                foreach (var item in new[] { (0, 5), (1, 8), (2, 7) }) frames.GetArrayElementAtIndex(item.Item1).intValue = item.Item2;
                Number(so, "maxPressure", 100f); Number(so, "pressurePerEvent", 8f); Number(so, "releaseHealthRatio", 0.4f);
                Number(so, "overheatDuration", 3f); Number(so, "overheatDamageMultiplier", 1.4f);
            });
            Set(root.GetComponent<PressureKettleController>(), so =>
            {
                Clip(so, "Idle", "Idle", 10f, true); Clip(so, "Walk", "Walk", 14f, true);
                Clip(so, "Attacked", "Hit", 18f, false); Clip(so, "Die", "Death", 12f, false);
                foreach (string action in new[] { "SteamJet", "PressureBomb", "PressureRelease" }) Clip(so, action, action, 16f, false);
            });
        });
        AssetDatabase.SaveAssets();
    }
    private static void ValidateArt()
    {
        foreach (string action in new[] { "Idle", "Walk", "SteamJet", "PressureBomb", "PressureRelease", "Hit", "Death" })
        {
            string path = Art + "SpriteSheets16/" + action + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); Sprite[] frames = Frames(path);
            Require(texture.width == 1024 && texture.height == 1536 && frames.Length == 16, action + " 1024x1536 / 16 slices");
            for (int i = 0; i < 16; i++)
                Require(frames[i].name == action + "_" + i && frames[i].pixelsPerUnit == 100f
                    && Vector2.Distance(frames[i].pivot, new Vector2(128f, 56f)) < 0.001f
                    && frames[i].rect == new Rect(i % 4 * 256, (3 - i / 4) * 384, 256, 384), action + "_" + i + " name / order / pivot / PPU");
        }
        foreach (string effect in new[] { "SteamCone", "PressureBomb", "PressureBurst" })
        {
            string path = Art + "Effects/" + effect + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); Sprite[] frames = Frames(path);
            Require(texture.width == 1024 && texture.height == 1024 && frames.Length == 16, effect + " 1024x1024 / 16 slices");
            for (int i = 0; i < 16; i++) Require(Index(frames[i].name) == i + 1 && frames[i].pixelsPerUnit == 100f, effect + " frame " + i);
        }
    }
    private static void ValidateEnemy()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy);
        foreach (Type type in new[] { typeof(PressureKettleController), typeof(PressureKettleCombat), typeof(PressureKettleMovement),
            typeof(Health), typeof(ActorStatus), typeof(EnemyRewards), typeof(Rigidbody2D), typeof(BoxCollider2D) })
            Require(root.GetComponents(type).Length == 1, "root exactly one " + type.Name);
        Require(root.transform.Find("Visual").GetComponent<SpriteManager>() != null, "Visual/SpriteManager reference");
        Require(root.GetComponentInChildren<EnemyCheckAttack>(true) != null, "EnemyCheckAttack detector");
        Require(root.GetComponentsInChildren<EnemyAttackBox>(true).Length == 0, "no EnemyAttackBox components");
        Require(root.GetComponent<Health>().BeginBlood == 96f
            && new SerializedObject(root.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 22f
            && new SerializedObject(root.GetComponent<PressureKettleMovement>()).FindProperty("moveSpeed").floatValue == 0.8f, "HP 96 / XP 22 / speed 0.8");
        var controller = new SerializedObject(root.GetComponent<PressureKettleController>());
        foreach (var clip in new[] { ("Idle", 10f, true), ("Walk", 14f, true), ("Attacked", 18f, false), ("Die", 12f, false),
            ("SteamJet", 16f, false), ("PressureBomb", 16f, false), ("PressureRelease", 16f, false) })
        {
            var p = controller.FindProperty(clip.Item1);
            Require(p.FindPropertyRelative("sprites").arraySize == 16 && p.FindPropertyRelative("fps").floatValue == clip.Item2
                && p.FindPropertyRelative("loop").boolValue == clip.Item3, clip.Item1 + " 16 frames / " + clip.Item2 + " fps / loop=" + clip.Item3);
        }
        var combat = new SerializedObject(root.GetComponent<PressureKettleCombat>());
        Require(combat.FindProperty("baseAttackBoxes").arraySize == 0, "ranged enemy has no attack boxes");
        Require((Transform)combat.FindProperty("muzzle").objectReferenceValue == root.transform.Find("Muzzle"), "Muzzle bound to existing child at (0.7, 0.75, 0)");
        foreach (var pair in new[] { ("steamPrefab", "Steam"), ("bombPrefab", "Bomb"), ("burstPrefab", "Burst") })
            Require(AssetDatabase.GetAssetPath(combat.FindProperty(pair.Item1).objectReferenceValue) == Fx + pair.Item2 + ".prefab", pair.Item1 + " -> existing " + pair.Item2 + ".prefab");
        foreach (string path in new[] { Enemy, Fx + "Steam.prefab", Fx + "Bomb.prefab", Fx + "Burst.prefab" })
            foreach (Component c in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Component>(true))
            {
                Require(c != null, path + " no missing script");
                var p = new SerializedObject(c).GetIterator();
                while (p.NextVisible(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference)
#pragma warning disable CS0618
                        Require(p.objectReferenceValue != null || p.objectReferenceInstanceIDValue == 0, path + " resolved " + p.propertyPath);
#pragma warning restore CS0618
            }
    }
    private static void ValidateScene()
    {
        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageEnemyManager>(true));
        int matches = 0;
        foreach (var manager in managers)
        {
            var entries = new SerializedObject(manager).FindProperty("enemyPools");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (AssetDatabase.GetAssetPath(entry.FindPropertyRelative("prefab").objectReferenceValue) != Enemy) continue;
                matches++;
                Require(entry.FindPropertyRelative("poolSize").intValue == 10
                    && Mathf.Approximately(entry.FindPropertyRelative("weight").floatValue, 0.33f), "Level7Scene existing pool: size 10, weight 0.33");
            }
        }
        Require(matches == 1, "Level7Scene has exactly one PressureKettle pool; no scene edits");
    }
}

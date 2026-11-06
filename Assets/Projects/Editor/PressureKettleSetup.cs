using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PressureKettleSetup
{
    public const string EnemyPath = "Assets/Projects/Prefabs/Enemy/PressureKettle.prefab";
    public const string FxRoot = "Assets/Projects/Prefabs/SeparateEffort/PressureKettle";
    public const string ArtRoot = "Assets/Art/Enemies/Level7/PressureKettle/";
    public const string ScenePath = "Assets/Projects/Scenes/Levels/Level7Scene.unity";
    public static readonly string AuditRoot = Path.GetFullPath("Logs/PressureKettleAudit");
    private static readonly string[] Actions = { "Idle", "Walk", "SteamJet", "PressureBomb", "PressureRelease", "Hit", "Death" };
    private static readonly StringBuilder report = new();
    private static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); report.AppendLine("PASS: " + reason); }
    private static void Edit(Object c, Action<SerializedObject> edit) { SerializedObject so = new(c); edit(so); so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Ref(Object c, string n, Object v) => Edit(c, so => so.FindProperty(n).objectReferenceValue = v);
    private static void Number(Object c, string n, float v) => Edit(c, so => so.FindProperty(n).floatValue = v);
    private static void Integer(Object c, string n, int v) => Edit(c, so => so.FindProperty(n).intValue = v);
    public static Sprite[] Frames(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => Suffix(s.name)).ToArray();
    private static int Suffix(string name) => int.Parse(name.Substring(name.LastIndexOf('_') + 1));
    private static void Clip(Object c, string n, Sprite[] frames, float fps, bool loop) => Edit(c, so =>
    {
        var p = so.FindProperty(n); var sprites = p.FindPropertyRelative("sprites"); sprites.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        p.FindPropertyRelative("fps").floatValue = fps; p.FindPropertyRelative("loop").boolValue = loop;
    });
    private static GameObject Child(GameObject root, string name, int layer = 0)
    { GameObject child = new(name) { layer = layer }; child.transform.SetParent(root.transform, false); return child; }
    private static SpriteManager Visual(GameObject root, Sprite sprite, out SpriteRenderer sr)
    {
        GameObject visual = Child(root, "Visual"); sr = visual.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/PurpleRanged.prefab").GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
        sr.sortingOrder = 3; sr.sprite = sprite;
        SpriteManager sm = visual.AddComponent<SpriteManager>(); Ref(sm, "sr", sr); return sm;
    }
    [MenuItem("造梦西游/PressureKettle/整理美术并生成验收")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(AuditRoot); report.Clear();
        Scene previous = SceneManager.GetActiveScene();
        Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            MoveArt(); RenameSprites(); DeleteConfirmedDuplicates(); ValidateSprites(); CreateEffects(); CreateEnemy(); WireScene(); ValidateReferences();
            AssetDatabase.SaveAssets(); Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            report.AppendLine("ALL PASS"); Debug.Log("PRESSURE_KETTLE_ASSETS_PASS");
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(AuditRoot, "unity-assets.txt"), report.ToString());
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scratch, true);
        }
    }
    private static void MoveArt()
    {
        string from = "Assets/Art/Enemies/Level7/enemy3/SpriteSheets16", to = ArtRoot + "SpriteSheets16";
        if (!AssetDatabase.IsValidFolder(from)) { Require(AssetDatabase.IsValidFolder(to), "action folder already migrated"); return; }
        Require(!AssetDatabase.IsValidFolder(to), "destination does not overwrite any artwork");
        string[] paths = Directory.GetFiles(from, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).Prepend(from).ToArray();
        var guids = paths.ToDictionary(p => p, AssetDatabase.AssetPathToGUID);
        string error = AssetDatabase.MoveAsset(from, to); Require(string.IsNullOrEmpty(error), "AssetDatabase.MoveAsset: " + from + " -> " + to + " " + error);
        foreach (var entry in guids) Require(AssetDatabase.AssetPathToGUID(entry.Key.Replace(from, to)) == entry.Value, "preserved GUID " + Path.GetFileName(entry.Key));
    }
    private static void RenameSprites()
    {
        var factory = new SpriteDataProviderFactories(); factory.Init();
        foreach (string action in Actions)
        {
            string path = ArtRoot + "SpriteSheets16/" + action + ".png";
            var before = Frames(path).Select(s => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id); return id; }).ToArray();
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects().OrderBy(r => Suffix(r.name)).ToArray(); Require(rects.Length == 16, action + " existing 16 slices");
            for (int i = 0; i < rects.Length; i++) { rects[i].name = action + "_" + i; rects[i].pivot = new Vector2(0.5f, 56f / 384f); rects[i].alignment = SpriteAlignment.Custom; }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.spritePixelsPerUnit = 100f; importer.SaveAndReimport();
            var after = Frames(path).Select(s => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id); return id; }).ToArray();
            Require(before.SequenceEqual(after), action + " preserved all sprite local file IDs");
        }
    }
    private static string Hash(string path) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    private static void DeleteConfirmedDuplicates()
    {
        string root = "Assets/Art/Enemies/Level7/";
        var pairs = new[] { ("enemy1", "ClampGuard"), ("enemy2", "RollingFurnace"), ("enemy3", "PressureKettle") };
        var confirmed = new List<string>();
        foreach (var pair in pairs)
        {
            string folder = root + pair.Item1;
            if (!AssetDatabase.IsValidFolder(folder)) { report.AppendLine("INVENTORY: " + folder + " absent"); continue; }
            string stand = Directory.GetFiles(folder, "*.png").FirstOrDefault(p => Path.GetFileName(p).Equals("stand.png", StringComparison.OrdinalIgnoreCase));
            string canonical = root + pair.Item2 + "/Stand.png";
            if (stand == null || !File.Exists(canonical) || Hash(stand) != Hash(canonical)) { report.AppendLine("KEEP: no proven duplicate " + folder); continue; }
            stand = stand.Replace('\\', '/');
            string guid = AssetDatabase.AssetPathToGUID(stand);
            string[] references = Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories)
                .Where(p => new[] { ".prefab", ".unity", ".asset", ".mat", ".controller", ".anim" }.Contains(Path.GetExtension(p)))
                .Where(p => File.ReadAllText(p).Contains(guid)).ToArray();
            report.AppendLine("INVENTORY: " + stand + " == " + canonical + " SHA256=" + Hash(stand) + " refs=" + references.Length);
            if (references.Length == 0) confirmed.Add(stand); else report.AppendLine("KEEP referenced: " + string.Join(",", references));
        }
        // Persist the complete inventory before any deletion.
        File.WriteAllText(Path.Combine(AuditRoot, "duplicate-inventory-before-delete.txt"), report.ToString());
        foreach (string path in confirmed) { Require(AssetDatabase.DeleteAsset(path), "deleted confirmed unreferenced duplicate " + path); }
        foreach (var pair in pairs)
        {
            string folder = root + pair.Item1;
            if (AssetDatabase.IsValidFolder(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Require(AssetDatabase.DeleteAsset(folder), "deleted empty duplicate folder " + folder);
        }
    }
    public static void ValidateSprites()
    {
        foreach (string action in Actions)
        {
            var frames = Frames(ArtRoot + "SpriteSheets16/" + action + ".png"); Require(frames.Length == 16, action + " 16 frames");
            for (int i = 0; i < frames.Length; i++)
                Require(frames[i].name == action + "_" + i && Mathf.Approximately(frames[i].pixelsPerUnit, 100f)
                    && Vector2.Distance(frames[i].pivot, new Vector2(128f, 56f)) < 0.001f
                    && frames[i].rect.size == new Vector2(256f, 384f), action + " frame " + i + " name / PPU / feet pivot / cell");
        }
        foreach (string effect in new[] { "SteamCone", "PressureBomb", "PressureBurst" })
        {
            var frames = Frames(ArtRoot + "Effects/" + effect + ".png"); Require(frames.Length == 16, effect + " actual 16 frames");
            for (int i = 0; i < frames.Length; i++) Require(Suffix(frames[i].name) == i + 1 && frames[i].pixelsPerUnit == 100f, effect + " continuous index " + (i + 1));
        }
    }
    private static void CreateEffects()
    {
        foreach (string kind in new[] { "Steam", "Bomb", "Burst" })
        {
            string action = kind == "Steam" ? "SteamCone" : kind == "Bomb" ? "PressureBomb" : "PressureBurst";
            Sprite[] frames = Frames(ArtRoot + "Effects/" + action + ".png");
            GameObject root = new("PressureKettle" + kind);
            SpriteManager sm = Visual(root, frames[0], out SpriteRenderer sr);
            PressureKettleEffect effect = root.AddComponent<PressureKettleEffect>(); Ref(effect, "spriteManager", sm); Ref(effect, "visual", sr);
            Clip(effect, "appearance", kind == "Bomb" ? frames.Take(6).ToArray() : frames, kind == "Bomb" ? 12f : 16f, kind == "Bomb");
            Clip(effect, "explosion", kind == "Bomb" ? frames.Skip(6).ToArray() : frames, 16f, false);
            Number(effect, "nativeWidth", kind == "Steam" ? 1.86f : kind == "Bomb" ? 1.38f : 1.94f);
            if (kind == "Steam")
            {
                PolygonCollider2D cone = root.AddComponent<PolygonCollider2D>(); cone.isTrigger = true;
                Vector2[] points = new Vector2[15]; points[0] = Vector2.zero;
                for (int i = 0; i < 14; i++) { float a = (150f + i * 60f / 13f) * Mathf.Deg2Rad; points[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f; }
                cone.points = points; cone.enabled = false; Ref(effect, "cone", cone);
            }
            if (kind == "Bomb")
            {
                // The source art contains an arc inside each cell. Remove that baked translation;
                // the projectile's physics supplies the world-space arc, without redrawing the art.
                // Measured target_anchor values in Level7FX.AnimationNotes.json, not prompt coordinates.
                Vector2[] centers = { new(169, 144), new(155, 113), new(140, 94), new(124, 94), new(110, 113), new(98, 144) };
                Edit(effect, so =>
                {
                    var offsets = so.FindProperty("flightOffsets"); offsets.arraySize = 6;
                    for (int i = 0; i < 6; i++) offsets.GetArrayElementAtIndex(i).vector2Value = new Vector2((frames[i].pivot.x - centers[i].x) / 100f, (frames[i].pivot.y - (256f - centers[i].y)) / 100f);
                    so.FindProperty("explosionOffset").vector2Value = new Vector2((frames[6].pivot.x - 98f) / 100f, (frames[6].pivot.y - 112f) / 100f);
                });
            }
            PrefabUtility.SaveAsPrefabAsset(root, FxRoot + kind + ".prefab"); Object.DestroyImmediate(root);
        }
    }
    private static void CreateEnemy()
    {
        GameObject root = new("PressureKettle") { layer = 11 };
        Rigidbody2D body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 0.9f; body.constraints = RigidbodyConstraints2D.FreezeRotation; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D physical = root.AddComponent<BoxCollider2D>(); physical.size = new Vector2(1.1f, 1.3f); physical.offset = new Vector2(0f, 0.65f);
        Health health = root.AddComponent<Health>(); Number(health, "beginBlood", 96f);
        ActorStatus status = root.AddComponent<ActorStatus>(); EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/PurpleRanged.prefab").GetComponent<ActorStatus>(), status);
        EnemyRewards rewards = root.AddComponent<EnemyRewards>(); Number(rewards, "experienceReward", 22f);
        PressureKettleMovement movement = root.AddComponent<PressureKettleMovement>();
        Number(movement, "moveSpeed", 0.8f); Number(movement, "slowSpeed", 0.4f); Number(movement, "backSpeed", 3.5f); Number(movement, "upSpeed", 6f);
        Number(movement, "boostRatio", 1f); Number(movement, "minNormalY", 0.6f); Integer(movement, "groundLayer", 1 << 6);
        PressureKettleCombat combat = root.AddComponent<PressureKettleCombat>(); PressureKettleController controller = root.AddComponent<PressureKettleController>();
        foreach (string field in new[] { "AttackBoxes", "attackDelay", "CheckAttackLogic", "AttackOpenIdx", "AttackShutIdx" }) Edit(combat, so => so.FindProperty(field).arraySize = 0);
        Ref(combat, "steamPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(FxRoot + "Steam.prefab").GetComponent<PressureKettleEffect>());
        Ref(combat, "bombPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(FxRoot + "Bomb.prefab").GetComponent<PressureKettleEffect>());
        Ref(combat, "burstPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(FxRoot + "Burst.prefab").GetComponent<PressureKettleEffect>());
        Transform muzzle = Child(root, "Muzzle").transform; muzzle.localPosition = new Vector3(0.8f, 0.85f, 0f); Ref(combat, "muzzle", muzzle);
        Visual(root, Frames(ArtRoot + "SpriteSheets16/Idle.png")[0], out SpriteRenderer renderer);
        renderer.sortingOrder = 2; renderer.transform.localScale = new Vector3(-1f, 1f, 1f); renderer.gameObject.AddComponent<SpriteOutlineController>();
        GameObject boxes = Child(root, "Combat"), hurt = Child(boxes, "Attacked", 7);
        BoxCollider2D hurtBox = hurt.AddComponent<BoxCollider2D>(); hurtBox.isTrigger = true; hurtBox.size = new Vector2(1.5f, 1.4f); hurtBox.offset = new Vector2(0f, 0.7f); Ref(controller, "attackedBox", hurtBox);
        GameObject check = Child(boxes, "SkillCheck", 10); BoxCollider2D detector = check.AddComponent<BoxCollider2D>(); detector.isTrigger = true; detector.size = new Vector2(14f, 3f); detector.offset = new Vector2(0f, 0.85f); check.AddComponent<EnemyCheckAttack>();
        Number(controller, "stopDistance", 7f);
        Clip(controller, "Idle", Frames(ArtRoot + "SpriteSheets16/Idle.png"), 10f, true);
        Clip(controller, "Walk", Frames(ArtRoot + "SpriteSheets16/Walk.png"), 10f, true);
        Clip(controller, "Attacked", Frames(ArtRoot + "SpriteSheets16/Hit.png"), 16f, false);
        Clip(controller, "Die", Frames(ArtRoot + "SpriteSheets16/Death.png"), 12f, false);
        Clip(controller, "SteamJet", Frames(ArtRoot + "SpriteSheets16/SteamJet.png"), 8f, false);
        Clip(controller, "PressureBomb", Frames(ArtRoot + "SpriteSheets16/PressureBomb.png"), 14f, false);
        Clip(controller, "PressureRelease", Frames(ArtRoot + "SpriteSheets16/PressureRelease.png"), 14f, false);
        PrefabUtility.SaveAsPrefabAsset(root, EnemyPath); Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            StageEnemyManager manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StageEnemyManager>(true)).Single();
            PressureKettleController prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<PressureKettleController>();
            Edit(manager, so =>
            {
                var pools = so.FindProperty("enemyPools"); int index = -1;
                for (int i = 0; i < pools.arraySize; i++) if (pools.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is PressureKettleController) index = i;
                if (index < 0) { index = pools.arraySize; pools.arraySize++; }
                var pool = pools.GetArrayElementAtIndex(index); pool.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                pool.FindPropertyRelative("poolSize").intValue = 10; pool.FindPropertyRelative("weight").floatValue = 0.33f;
            });
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene), "Level7 saved PressureKettle pool 10 / weight 0.33");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    public static void ValidateReferences()
    {
        foreach (string path in new[] { EnemyPath, FxRoot + "Steam.prefab", FxRoot + "Bomb.prefab", FxRoot + "Burst.prefab" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(prefab != null, path);
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, t.name + " no missing script");
            foreach (Component c in prefab.GetComponentsInChildren<Component>(true))
            {
                var p = new SerializedObject(c).GetIterator();
                while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference)
                {
#pragma warning disable CS0618
                    if (p.objectReferenceValue == null && p.objectReferenceEntityIdValue != default) throw new InvalidOperationException(path + " missing " + p.propertyPath);
#pragma warning restore CS0618
                }
            }
        }
        GameObject enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        Require(enemy.GetComponent<Health>().BeginBlood == 96f, "HP 96");
        Require(new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 22f, "XP 22");
        Require(new SerializedObject(enemy.GetComponent<PressureKettleMovement>()).FindProperty("moveSpeed").floatValue == 0.8f, "speed 0.8");
    }
}

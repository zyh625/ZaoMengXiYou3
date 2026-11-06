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

public static class ResonantStarOwlSetup
{
    public const string EnemyPath = "Assets/Projects/Prefabs/Enemy/ResonantStarOwl.prefab";
    public const string FxRoot = "Assets/Projects/Prefabs/SeparateEffort/ResonantStarOwl";
    public const string ArtRoot = "Assets/Art/Enemies/Level8/ResonantStarOwl/";
    public const string ScenePath = "Assets/Projects/Scenes/Levels/Level8Scene.unity";
    public static readonly string AuditRoot = Path.GetFullPath("Logs/ResonantStarOwlAudit");
    private static readonly string[] Actions = { "Idle", "Walk", "SoundRings", "HighLowChime", "ResonanceBand", "Hit", "Death" };
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
    [MenuItem("造梦西游/ResonantStarOwl/生成并校验")]
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(AuditRoot); report.Clear();
        Scene previous = SceneManager.GetActiveScene();
        Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scratch);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateSprites(); CreateEffects(); CreateEnemy(); WireScene(); ValidateReferences();
            AssetDatabase.SaveAssets(); Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            report.AppendLine("ALL PASS"); Debug.Log("RESONANT_STAR_OWL_ASSETS_PASS");
        }
        finally
        {
            File.WriteAllText(Path.Combine(AuditRoot,"unity-assets.txt"),report.ToString());
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scratch,true);
        }
    }
    public static void ValidateSprites()
    {
        foreach (string action in Actions.Concat(new[]{"Effects/SoundRings","Effects/HighLowChime","Effects/ResonanceBand"}))
        {
            var frames = Frames(ArtRoot+action+".png"); string name = Path.GetFileName(action);
            Require(frames.Length == 16, action+" 16 frames");
            for(int i=0;i<16;i++)
            {
                var s = frames[i];
                Require(s.name == name+"_"+i && s.pixelsPerUnit == 100f,action+" continuous frame "+i);
                // Source cells are top-to-bottom, 256 x 384. Compensated pivots keep the same center.
                Vector2 center = s.rect.position+s.pivot;
                Vector2 expected = new Vector2((i%4)*256+128,(3-i/4)*384+192);
                Require(Vector2.Distance(center,expected)<0.01f,action+" hover pivot "+i);
            }
        }
    }
    private static void CreateEffects()
    {
        foreach(string action in new[]{"SoundRings","HighLowChime","ResonanceBand"})
        {
            var frames = Frames(ArtRoot+"Effects/"+action+".png");
            GameObject root = new("ResonantStarOwl"+action);
            SpriteManager sm = Visual(root,frames[0],out SpriteRenderer sr);
            var effect = root.AddComponent<ResonantStarOwlEffect>();
            Ref(effect,"spriteManager",sm); Ref(effect,"visual",sr); Clip(effect,"appearance",frames,16f,false);
            PrefabUtility.SaveAsPrefabAsset(root,FxRoot+action+".prefab"); Object.DestroyImmediate(root);
        }
    }
    private static void CreateEnemy()
    {
        GameObject root = new("ResonantStarOwl") { layer = 11 };
        Rigidbody2D body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 0f; body.bodyType = RigidbodyType2D.Kinematic; body.constraints = RigidbodyConstraints2D.FreezeRotation; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D physical = root.AddComponent<BoxCollider2D>(); physical.size = new Vector2(1.1f, 1.1f); physical.offset = Vector2.zero;
        Health health = root.AddComponent<Health>(); Number(health, "beginBlood", 100f);
        ActorStatus status = root.AddComponent<ActorStatus>(); EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/PurpleRanged.prefab").GetComponent<ActorStatus>(), status);
        EnemyRewards rewards = root.AddComponent<EnemyRewards>(); Number(rewards, "experienceReward", 24f);
        ResonantStarOwlMovement movement = root.AddComponent<ResonantStarOwlMovement>();
        Number(movement, "moveSpeed", 0.9f); Number(movement, "slowSpeed", 0.45f); Number(movement, "backSpeed", 3.5f); Number(movement, "upSpeed", 6f);
        Number(movement, "boostRatio", 1f); Number(movement, "minNormalY", 0.6f); Integer(movement, "groundLayer", 1 << 6);
        ResonantStarOwlCombat combat = root.AddComponent<ResonantStarOwlCombat>(); ResonantStarOwlController controller = root.AddComponent<ResonantStarOwlController>();
        foreach (string field in new[] { "AttackBoxes", "attackDelay", "CheckAttackLogic", "AttackOpenIdx", "AttackShutIdx" }) Edit(combat, so => so.FindProperty(field).arraySize = 0);
        Ref(combat,"ringsPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(FxRoot+"SoundRings.prefab").GetComponent<ResonantStarOwlEffect>());
        Ref(combat,"chimePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(FxRoot+"HighLowChime.prefab").GetComponent<ResonantStarOwlEffect>());
        Ref(combat,"bandPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(FxRoot+"ResonanceBand.prefab").GetComponent<ResonantStarOwlEffect>());
        Visual(root, Frames(ArtRoot + "Idle.png")[0], out SpriteRenderer renderer);
        renderer.sortingOrder = 2; renderer.transform.localScale = new Vector3(-1f, 1f, 1f); renderer.gameObject.AddComponent<SpriteOutlineController>();
        GameObject boxes = Child(root, "Combat"), hurt = Child(boxes, "Attacked", 7);
        BoxCollider2D hurtBox = hurt.AddComponent<BoxCollider2D>(); hurtBox.isTrigger = true; hurtBox.size = new Vector2(1.3f, 1.3f); hurtBox.offset = Vector2.zero; Ref(controller, "attackedBox", hurtBox);
        Number(controller, "stopDistance", 7f);
        Clip(controller, "Idle", Frames(ArtRoot + "Idle.png"), 10f, true);
        Clip(controller, "Walk", Frames(ArtRoot + "Walk.png"), 14f, true);
        Clip(controller, "Attacked", Frames(ArtRoot + "Hit.png"), 18f, false);
        Clip(controller, "Die", Frames(ArtRoot + "Death.png"), 12f, false);
        Clip(controller, "SoundRings", Frames(ArtRoot + "SoundRings.png"), 16f, false);
        Clip(controller, "HighLowChime", Frames(ArtRoot + "HighLowChime.png"), 16f, false);
        Clip(controller, "ResonanceBand", Frames(ArtRoot + "ResonanceBand.png"), 16f, false);
        PrefabUtility.SaveAsPrefabAsset(root, EnemyPath); Object.DestroyImmediate(root);
    }
    private static void WireScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            StageEnemyManager manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StageEnemyManager>(true)).Single();
            ResonantStarOwlController prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<ResonantStarOwlController>();
            Edit(manager, so =>
            {
                var pools = so.FindProperty("enemyPools"); int index = -1;
                for (int i = 0; i < pools.arraySize; i++) if (pools.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue is ResonantStarOwlController) index = i;
                if (index < 0) { index = pools.arraySize; pools.arraySize++; }
                var pool = pools.GetArrayElementAtIndex(index); pool.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                pool.FindPropertyRelative("poolSize").intValue = 10; pool.FindPropertyRelative("weight").floatValue = 0.33f;
            });
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene), "Level8 saved ResonantStarOwl pool 10 / weight 0.33");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    public static void ValidateReferences()
    {
        foreach (string path in new[] { EnemyPath, FxRoot + "SoundRings.prefab", FxRoot + "HighLowChime.prefab", FxRoot + "ResonanceBand.prefab" })
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
        Require(enemy.GetComponent<Health>().BeginBlood == 100f, "HP 100");
        Require(new SerializedObject(enemy.GetComponent<EnemyRewards>()).FindProperty("experienceReward").floatValue == 24f, "XP 24");
        Require(new SerializedObject(enemy.GetComponent<ResonantStarOwlMovement>()).FindProperty("moveSpeed").floatValue == 0.9f, "speed 0.9");
    }
}

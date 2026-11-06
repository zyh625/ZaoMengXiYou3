using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Level1BirdMigrationSetup
{
    private const string Legacy = "Assets/Level1/Level1Prefab/";
    private const string Prefabs = "Assets/Projects/Prefabs/";
    private const string BirdPath = Prefabs + "Enemy/BirdEnemy.prefab";
    private const string BossPath = Prefabs + "Enemy/WuYingWang.prefab";
    private const string OrbPath = Prefabs + "SeparateEffort/WuYingWangOrb.prefab";
    private const string LevelPath = "Assets/Projects/Scenes/Levels/Level1Scene.unity";

    public static void BuildAndWire()
    {
        BuildPrefabs();
        WireLevel();
    }

    [MenuItem("Tools/Level 1/Build Bird and Wu Ying Wang Prefabs")]
    public static void BuildPrefabs()
    {
        Scene scratch = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject template = Load(Prefabs + "Enemy/BlueShield.prefab");
            Bird oldBird = Load(Legacy + "BirdEnemy.prefab").GetComponent<Bird>();
            Boss1 oldBoss = Load(Legacy + "Boss1.prefab").GetComponent<Boss1>();
            Sprite[][] bird = Frames(oldBird, 16, 16, 16, 15);
            Sprite[][] boss = Frames(oldBoss, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);
            Ball oldBall = oldBoss.GetComponentInChildren<Ball>(true);
            Material material = template.GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
            var orb = BuildOrb(scratch, oldBall, material);
            BuildBird(scratch, oldBird, bird, template, material);
            BuildBoss(scratch, oldBoss, boss, template, material, orb);
            AssetDatabase.SaveAssets();
            Debug.Log("Level1 prefab setup complete: bird 16/16/16/15, boss 11×16, orb 16/16.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scratch); }
    }

    private static Sprite[][] Frames(Component legacy, params int[] counts)
    {
        SerializedProperty clips = new SerializedObject(legacy).FindProperty("birdFrames");
        if (clips == null || clips.arraySize != counts.Length) throw new InvalidOperationException($"Unexpected legacy clips on {legacy.name}.");
        Sprite[][] result = new Sprite[counts.Length][];
        for (int i = 0; i < counts.Length; i++)
            result[i] = ReadFrames(clips.GetArrayElementAtIndex(i).FindPropertyRelative("frames"), counts[i]);
        return result;
    }

    private static Sprite[] ReadFrames(SerializedProperty frames, int count)
    {
        // The restored atlases already have imported slices. Preserve their rects, pivots and local IDs.
        if (frames.arraySize != count) throw new InvalidOperationException($"{frames.propertyPath}: expected {count} legacy slices.");
        return Enumerable.Range(0, count).Select(i =>
            frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite
                ?? throw new InvalidOperationException($"{frames.propertyPath}[{i}] is missing.")).ToArray();
    }

    private static void BuildBird(Scene scratch, Bird legacy, Sprite[][] frames, GameObject template, Material material)
    {
        GameObject root = Actor(scratch, "BirdEnemy", template, 90f, 5f, 1.5f, new Vector2(1.35f, 0.67f), 0f);
        var combat = root.AddComponent<BirdEnemyCombat>();
        var controller = root.AddComponent<BirdEnemyController>();
        SpriteRenderer renderer = Visual(root, frames[0][0], material, 0.55f, false);
        Sprite oldCrown = ((Image)new SerializedObject(legacy).FindProperty("Trophy").objectReferenceValue).sprite;
        var crown = Child(renderer.gameObject, "EliteCrown").AddComponent<SpriteRenderer>();
        crown.sprite = oldCrown;
        crown.sortingOrder = renderer.sortingOrder + 1;
        crown.transform.localScale = Vector3.one * (0.8f / oldCrown.bounds.size.x);
        crown.transform.localPosition = Vector3.up * (frames[0][0].bounds.max.y + 0.38f);
        crown.gameObject.SetActive(false);
        Reference(controller, "crown", crown);
        Clip(controller, "Idle", frames[0], 20f, true);
        Clip(controller, "Walk", frames[0], 20f, true);
        Clip(controller, "dash", frames[1], 26f, false);//前半段俯冲、后半段返回，约 0.6 秒
        Clip(controller, "Attacked", frames[2], 24f, false);
        Clip(controller, "Die", frames[3], 20f, false);
        // F4 through F13 inclusive in the design, zero-based [3,13) in the player.
        // 检查箱放在鸟身下方：鸟在玩家头顶盘旋时正好覆盖玩家。
        Boxes(root, controller, combat, new Vector2(8f, 4.5f), new Vector2(0f, -2.6f),
            new Vector2(1.65f, 0.85f), new Vector2(0.4f, 0f), 6f, 6f, 3, 13);
        Save(root, BirdPath);
    }

    private static void BuildBoss(Scene scratch, Boss1 legacy, Sprite[][] frames, GameObject template, Material material, WuYingWangOrb orb)
    {
        GameObject root = Actor(scratch, "WuYingWang", template, 200f, 20f, 2f, new Vector2(1.45f, 2.4f), 1.2f);
        var combat = root.AddComponent<WuYingWangCombat>();
        var controller = root.AddComponent<WuYingWangController>();
        Visual(root, frames[0][0], material, 1f, true);
        string[] fields = { "Idle", "Walk", "lunge", "groundOrb", "Attacked", "Die", "takeoff", "fly", "flyingOrb", "flyingHit", "flyingDeath" };
        string[] legacyTimes = { "idelF", "moveF", "attack1F", "attack2F", "attackedF", "dieF", "changeF", "flyF", "flyAttackF", "flyAttackedF", "flyDieF" };
        SerializedObject source = new(legacy);
        for (int i = 0; i < fields.Length; i++)
            Clip(controller, fields[i], frames[i], 1f / source.FindProperty(legacyTimes[i]).floatValue, i == 0 || i == 1 || i == 7);
        Reference(controller, "orbPrefab", orb);
        Transform ground = Child(root, "GroundMuzzle").transform;
        ground.localPosition = new Vector3(0.95f, 1.6f);
        Transform flying = Child(root, "FlyingMuzzle").transform;
        flying.localPosition = new Vector3(0.85f, 0.95f);
        Reference(controller, "groundMuzzle", ground);
        Reference(controller, "flyingMuzzle", flying);
        Boxes(root, controller, combat, new Vector2(5.2f, 2.7f), new Vector2(2.3f, 1.15f),
            new Vector2(1.45f, 2.1f), new Vector2(0.8f, 1.15f), 18f, 0f, 4, 10);
        Save(root, BossPath);
    }

    private static WuYingWangOrb BuildOrb(Scene scratch, Ball legacy, Material material)
    {
        SerializedObject data = new(legacy);
        Sprite[] trail = ReadFrames(data.FindProperty("trace"), 16);
        Sprite[] burst = ReadFrames(data.FindProperty("boom"), 16);
        GameObject root = new("WuYingWangOrb");
        SceneManager.MoveGameObjectToScene(root, scratch);
        root.transform.localScale = legacy.transform.localScale;
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = trail[0];
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 20;
        root.AddComponent<SpriteManager>();
        WuYingWangOrb orb = root.AddComponent<WuYingWangOrb>();
        Clip(orb, "trail", trail, 1f / data.FindProperty("traceF").floatValue, true);
        Clip(orb, "burst", burst, 1f / data.FindProperty("boomF").floatValue, false);
        Save(root, OrbPath);
        return Load(OrbPath).GetComponent<WuYingWangOrb>();
    }

    private static GameObject Actor(Scene scratch, string name, GameObject template, float hp, float xp, float speed, Vector2 size, float centerY)
    {
        GameObject root = new(name) { layer = LayerMask.NameToLayer("body") };
        SceneManager.MoveGameObjectToScene(root, scratch);
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.gravityScale = 0.9f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.offset = Vector2.up * centerY;
        Number(root.AddComponent<Health>(), "beginBlood", hp);
        EditorUtility.CopySerialized(template.GetComponent<ActorStatus>(), root.AddComponent<ActorStatus>());
        Number(root.AddComponent<EnemyRewards>(), "experienceReward", xp);
        var movement = root.AddComponent<Level1EnemyMovement>();
        Edit(movement, so =>
        {
            so.FindProperty("moveSpeed").floatValue = speed;
            so.FindProperty("slowSpeed").floatValue = speed * 0.5f;
            so.FindProperty("backSpeed").floatValue = 4f;
            so.FindProperty("upSpeed").floatValue = 3f;
            so.FindProperty("boostRatio").floatValue = 1f;
            so.FindProperty("minNormalY").floatValue = 0.6f;
            so.FindProperty("groundLayer").intValue = LayerMask.GetMask("Ground");
        });
        return root;
    }

    private static SpriteRenderer Visual(GameObject root, Sprite first, Material material, float scale, bool floorPivot)
    {
        GameObject visual = Child(root, "Visual");
        visual.transform.localScale = Vector3.one * scale;
        if (floorPivot) visual.transform.localPosition = Vector3.up * (-first.bounds.min.y * scale);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = first;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 2;
        Reference(visual.AddComponent<SpriteManager>(), "sr", renderer);
        return renderer;
    }

    private static void Boxes(GameObject root, EnemyController controller, EnemyCombat combat,
        Vector2 checkSize, Vector2 checkOffset, Vector2 hitSize, Vector2 hitOffset,
        float damage, float cooldown, int open, int shut)
    {
        GameObject boxes = Child(root, "Combat");
        BoxCollider2D body = root.GetComponent<BoxCollider2D>();
        BoxCollider2D hurt = Trigger(boxes, "Attacked", "Enemy", body.size, body.offset);
        Reference(controller, "attackedBox", hurt);
        BoxCollider2D hit = Trigger(boxes, "Attack", "EnemyAttack", hitSize, hitOffset);
        Number(hit.gameObject.AddComponent<EnemyAttackBox>(), "Attack", damage);
        hit.gameObject.SetActive(false);
        EnemyCheckAttack check = Trigger(boxes, "EnemyCheckAttack", "EnemyCheck", checkSize, checkOffset).gameObject.AddComponent<EnemyCheckAttack>();
        Edit(combat, so =>
        {
            Single(so, "AttackBoxes").objectReferenceValue = hit;
            Single(so, "CheckAttackLogic").objectReferenceValue = check;
            Single(so, "attackDelay").floatValue = cooldown;
            Single(so, "AttackOpenIdx").intValue = open;
            Single(so, "AttackShutIdx").intValue = shut;
        });
    }

    [MenuItem("Tools/Level 1/Wire Bird and Wu Ying Wang Scene")]
    public static void WireLevel()
    {
        Scene scene = SceneManager.GetSceneByPath(LevelPath);
        if (!scene.isLoaded)
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && active.isDirty)
                throw new InvalidOperationException("Save the current untitled scene before wiring Level1.");
            scene = EditorSceneManager.OpenScene(LevelPath, string.IsNullOrEmpty(active.path)
                ? OpenSceneMode.Single : OpenSceneMode.Additive);
        }
        var stage = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<StageManager>(true)).Single();
        StageEnemyManager previous = stage.GetComponent<StageEnemyManager>();
        var pool = previous as Level1BirdStageEnemyManager;
        if (pool == null)
        {
            pool = previous.gameObject.AddComponent<Level1BirdStageEnemyManager>();
            SerializedObject old = new(previous);
            Edit(pool, so =>
            {
                SerializedProperty property = old.GetIterator();
                for (bool next = property.NextVisible(true); next; next = property.NextVisible(false))
                    if (!property.name.StartsWith("m_", StringComparison.Ordinal)) so.CopyFromSerializedProperty(property);
            });
            Reference(stage, "enemyManager", pool);
            Object.DestroyImmediate(previous);
        }

        Transform exit = (Transform)new SerializedObject(stage).FindProperty("exitLocation").objectReferenceValue;
        // The old boss platform was 3.5 units left of the exit. The current route has a flat floor.
        Transform spawn = Marker(pool.transform, "WuYingWangSpawn", exit.position + new Vector3(-3.5f, 0.02f));
        Transform left = Marker(pool.transform, "WuYingWangArenaLeft", exit.position + Vector3.left * 20f);
        Transform right = Marker(pool.transform, "WuYingWangArenaRight", exit.position + Vector3.right * 3f);
        Edit(pool, so =>
        {
            so.FindProperty("boss").objectReferenceValue = Load(BossPath).transform;
            so.FindProperty("bossBornLocation").objectReferenceValue = spawn;
            so.FindProperty("lungeLeft").objectReferenceValue = left;
            so.FindProperty("lungeRight").objectReferenceValue = right;
            SerializedProperty bird = Single(so, "enemyPools");
            bird.FindPropertyRelative("prefab").objectReferenceValue = Load(BirdPath).GetComponent<BirdEnemyController>();
            bird.FindPropertyRelative("poolSize").intValue = 10;
            bird.FindPropertyRelative("weight").floatValue = 1f;
        });
        Edit(stage, so =>
        {
            // Keep the old eight-second cadence on each side of the new waves.
            so.FindProperty("enemyDelay").floatValue = 8f;
            so.FindProperty("bossBornDelay").floatValue = 6f;
            SetWaveCounts(so.FindProperty("leftEnemyMax"), 1, 2, 2);
            SetWaveCounts(so.FindProperty("rightEnemyMax"), 1, 1, 2);
        });
        var camera = (zmxy.GamePlay.CameraManager)new SerializedObject(stage).FindProperty("CM").objectReferenceValue;
        Edit(camera, so =>
        {
            so.FindProperty("maxRise").floatValue = 3.857f;
            so.FindProperty("verticalFollowSpeed").floatValue = 4f;
            var edges = so.FindProperty("cameraEdgeRight");
            Transform finalEdge = (Transform)edges.GetArrayElementAtIndex(edges.arraySize - 1).objectReferenceValue;
            Vector3 edgePosition = finalEdge.position;
            edgePosition.x = exit.position.x - 4f;
            finalEdge.position = edgePosition;
        });
        WireBossUI(stage);
        if (!pool.ValidateConfiguration(out string error)) throw new InvalidOperationException(error);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {LevelPath}.");
    }

    private static void WireBossUI(StageManager stage)
    {
        var previous = (BossUI)new SerializedObject(stage).FindProperty("bossUI").objectReferenceValue;
        if (previous is WuYingWangBossUI) return;
        SerializedObject old = new(previous);
        Image blood = (Image)old.FindProperty("bloodFull").objectReferenceValue;
        Image rage = (Image)old.FindProperty("specialState").objectReferenceValue;
        GameObject root = previous.gameObject;
        Object.DestroyImmediate(previous);
        var ui = root.AddComponent<WuYingWangBossUI>();
        Reference(ui, "bloodFull", blood);
        Reference(ui, "specialState", rage);
        Reference(stage, "bossUI", ui);
    }

    private static void SetWaveCounts(SerializedProperty property, params int[] counts)
    {
        property.arraySize = counts.Length;
        for (int i = 0; i < counts.Length; i++) property.GetArrayElementAtIndex(i).intValue = counts[i];
    }

    private static Transform Marker(Transform parent, string name, Vector3 position)
    {
        Transform marker = parent.Find(name);
        if (marker == null) marker = Child(parent.gameObject, name).transform;
        marker.position = position;
        return marker;
    }

    private static BoxCollider2D Trigger(GameObject parent, string name, string layer, Vector2 size, Vector2 offset)
    {
        GameObject go = Child(parent, name);
        go.layer = LayerMask.NameToLayer(layer);
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = size;
        collider.offset = offset;
        return collider;
    }

    private static GameObject Child(GameObject parent, string name)
    {
        GameObject child = new(name);
        if (child.scene != parent.scene) SceneManager.MoveGameObjectToScene(child, parent.scene);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path)
        ?? throw new InvalidOperationException($"Missing {path}.");

    private static void Save(GameObject root, string path)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException($"Could not save {path}.");
        Object.DestroyImmediate(root);
    }

    private static SerializedProperty Single(SerializedObject so, string field)
    {
        SerializedProperty array = so.FindProperty(field);
        array.arraySize = 1;
        return array.GetArrayElementAtIndex(0);
    }

    private static void Edit(Object component, Action<SerializedObject> edit)
    {
        SerializedObject so = new(component);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Reference(Object component, string field, Object value) =>
        Edit(component, so => so.FindProperty(field).objectReferenceValue = value);

    private static void Number(Object component, string field, float value) =>
        Edit(component, so => so.FindProperty(field).floatValue = value);

    private static void Clip(Object component, string field, Sprite[] frames, float fps, bool loop) => Edit(component, so =>
    {
        SerializedProperty clip = so.FindProperty(field);
        SerializedProperty sprites = clip.FindPropertyRelative("sprites");
        sprites.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        clip.FindPropertyRelative("fps").floatValue = fps;
        clip.FindPropertyRelative("loop").boolValue = loop;
    });
}

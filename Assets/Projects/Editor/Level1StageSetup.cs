using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using LegacyCamera = zmxy.Legacy.CameraManager;
using StageCamera = zmxy.GamePlay.CameraManager;

//把第一关从旧版独立脚本接入通用关卡框架：保留原背景、平台与向上闯关的路线，
//玩家、HUD、相机、刷怪、Boss、出口和结算改用与第2～9关相同的预制体与流程。
public static class Level1StageSetup
{
    private const string ScenePath = "Assets/Projects/Scenes/Levels/Level1Scene.unity";
    private const string LevelPrefabs = "Assets/Projects/Prefabs/Level/";
    private const string BirdPath = "Assets/Projects/Prefabs/Enemy/BirdEnemy.prefab";
    private const string BossPath = "Assets/Projects/Prefabs/Enemy/WuYingWang.prefab";
    private const string BackupFolder = "output/Level1Backup";
    private const int GroundLayer = 6;

    [MenuItem("造梦西游/第一关/接入通用关卡流程")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("第一关接线", "请先退出 Play Mode。", "确定");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("第一关接线",
            "将改写 Level1Scene：删除旧版相机、HUD、鸟怪/Boss 管理器和场景里的旧悟空，换成通用的 PlayerSystem、BattleUI、CameraRig、LevelSetup。\n\n" +
            "背景与平台保持不动，修改前会把场景备份到 " + BackupFolder + "。", "开始", "取消")) return;
        try
        {
            string report = Wire();
            Debug.Log(report);
            EditorUtility.DisplayDialog("第一关接线", "完成，详情见 Console。", "确定");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("第一关接线失败", error.Message + "\n\n场景未保存，可直接重新打开 Level1Scene 放弃改动。", "确定");
        }
    }

    private static string Wire()
    {
        var log = new StringBuilder("[Level1StageSetup] 第一关已接入通用关卡流程\n");
        string backup = Backup();
        log.AppendLine("备份：" + backup);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BirdPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(BossPath) == null)
        {
            Level1BirdMigrationSetup.BuildPrefabs();
            log.AppendLine("已生成 BirdEnemy / WuYingWang 预制体");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        if (All<Level1StageManager>().Length > 0)
            throw new InvalidOperationException("Level1Scene 已经接入过通用流程。如需重做，请先用备份恢复旧场景。");

        //1. 从旧版相机读取路线：ground[0] 底层地面，ground[1] 顶层平台，ground[2] Boss 平台。
        LegacyCamera legacy = All<LegacyCamera>().SingleOrDefault()
            ?? throw new InvalidOperationException("找不到旧版第一关的 CameraManager（zmxy.Legacy），无法读取平台位置。");
        if (legacy.ground == null || legacy.ground.Length < 3 || legacy.ground.Take(3).Any(g => g == null))
            throw new InvalidOperationException("旧版 CameraManager 的 ground 需要依次填写底层地面、顶层平台和 Boss 平台。");
        Camera legacyCam = legacy.cam != null ? legacy.cam : All<Camera>().FirstOrDefault()
            ?? throw new InvalidOperationException("找不到旧版主相机。");
        GameObject bottom = legacy.ground[0], top = legacy.ground[1], bossFloor = legacy.ground[2];
        Vector3 camPos = legacyCam.transform.position;
        float bottomTop = Top(bottom), topTop = Top(top), bossTop = Top(bossFloor);
        float climb = topTop - bottomTop;
        if (climb <= 1f) throw new InvalidOperationException("顶层平台没有高于底层地面，路线数据异常。");
        Vector3 spawnPos = bottom.transform.position + Vector3.up * 2f;//与原启动器相同的出生点
        Vector3 exitPos = All<ExitManager>().Select(e => e.transform.position).DefaultIfEmpty(bossFloor.transform.position + Vector3.up * 1.2f).First();
        (float arenaLeft, float arenaRight) = TopFloorSpan(scene, topTop, camPos.x);
        float maxRise = top.transform.position.y - bottom.transform.position.y;//旧版相机最高可上升的距离
        CameraClearFlags clearFlags = legacyCam.clearFlags;
        Color background = legacyCam.backgroundColor;
        float orthoSize = legacyCam.orthographicSize;

        //2. 删除旧版运行逻辑，背景与平台（Ground 层碰撞体）一律保留。
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!IsLegacyRoot(root)) continue;
            if (root.GetComponentsInChildren<Collider2D>(true).Any(c => c.gameObject.layer == GroundLayer))
                throw new InvalidOperationException($"{root.name} 同时包含平台碰撞体，已停止以免误删关卡美术。");
            log.AppendLine("删除旧对象：" + root.name);
            Object.DestroyImmediate(root);
        }

        //3. 放入与第2～9关相同的共用预制体。
        GameObject playerSystem = Place(scene, "PlayerSystem");
        GameObject battleUI = Place(scene, "BattleUI");
        GameObject cameraRig = Place(scene, "CameraRig");
        GameObject levelSetup = Place(scene, "LevelSetup");
        levelSetup.name = "Level1Setup";
        GameObject spawn = new("PlayerSpawn");
        SceneManager.MoveGameObjectToScene(spawn, scene);
        spawn.transform.position = spawnPos;

        //4. 相机：水平固定在原位置，竖直跟随角色，最高与旧版一致。
        StageCamera stageCamera = cameraRig.GetComponentInChildren<StageCamera>(true);
        Camera camera = stageCamera.GetComponent<Camera>();
        camera.transform.position = new Vector3(camPos.x, camPos.y, camera.transform.position.z);
        camera.orthographicSize = orthoSize;
        camera.clearFlags = clearFlags;
        camera.backgroundColor = background;
        Edit(stageCamera, so =>
        {
            so.FindProperty("verticalFollow").boolValue = true;
            so.FindProperty("verticalFollowSpeed").floatValue = 8f;
            so.FindProperty("verticalReturnSpeed").floatValue = 8f;
            so.FindProperty("verticalTriggerOffset").floatValue = 0f;
            so.FindProperty("maxRise").floatValue = maxRise;
            foreach (string field in new[] { "cameraEdgeLeft", "cameraEdgeRight" })
            {
                SerializedProperty edges = so.FindProperty(field);
                for (int i = 0; i < edges.arraySize; i++)
                {
                    var edge = (Transform)edges.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (edge == null) continue;
                    Vector3 p = edge.position;
                    p.x = camPos.x;//每一波的镜头都不左右移动
                    edge.position = p;
                }
            }
        });

        //5. 关卡管理器：换成第一关专用的向上闯关流程与鸟怪出生规则，其余配置照搬预制体。
        StageManager oldStage = levelSetup.GetComponentsInChildren<StageManager>(true).Single();
        StageEnemyManager oldEnemies = oldStage.GetComponent<StageEnemyManager>()
            ?? throw new InvalidOperationException("LevelSetup 的关卡管理器缺少 StageEnemyManager。");
        GameObject manager = oldStage.gameObject;
        manager.name = "Level1";
        var enemies = manager.AddComponent<Level1BirdStageEnemyManager>();
        CopyFields(oldEnemies, enemies);
        var stage = manager.AddComponent<Level1StageManager>();
        CopyFields(oldStage, stage);
        Object.DestroyImmediate(oldStage);
        Object.DestroyImmediate(oldEnemies);

        Transform markers = levelSetup.transform.Find("transforms")
            ?? throw new InvalidOperationException("LevelSetup 缺少 transforms 位置点。");
        Transform exitMarker = Rename(markers, "exit", "exit", exitPos);
        Transform bossSpawn = Rename(markers, "TitanBorn", "WuYingWang_BossSpawn", new Vector3(bossFloor.transform.position.x, bossTop + 0.02f));
        Transform left = Rename(markers, "TitanLungeLeft", "WuYingWang_ArenaLeft", new Vector3(arenaLeft, topTop));
        Transform right = Rename(markers, "TitanLungeRight", "WuYingWang_ArenaRight", new Vector3(arenaRight, topTop));
        Transform bossTrigger = Marker(markers, "Level1_BossTrigger", new Vector3(camPos.x, topTop - 0.15f));

        Edit(enemies, so =>
        {
            so.FindProperty("boss").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).transform;
            so.FindProperty("bossBornLocation").objectReferenceValue = bossSpawn;
            so.FindProperty("lungeLeft").objectReferenceValue = left;
            so.FindProperty("lungeRight").objectReferenceValue = right;
            SerializedProperty pools = so.FindProperty("enemyPools");
            pools.arraySize = 1;
            SerializedProperty bird = pools.GetArrayElementAtIndex(0);
            bird.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BirdPath).GetComponent<BirdEnemyController>();
            bird.FindPropertyRelative("poolSize").intValue = 10;//旧版最多同时 10 只鸟怪
            bird.FindPropertyRelative("weight").floatValue = 1f;
        });

        BossUI bossUI = ReplaceBossUI(battleUI);
        Edit(stage, so =>
        {
            so.FindProperty("ui").objectReferenceValue = battleUI.GetComponentInChildren<UIManager>(true);
            so.FindProperty("bossUI").objectReferenceValue = bossUI;
            so.FindProperty("playerManager").objectReferenceValue = playerSystem.GetComponentInChildren<PlayerManager>(true);
            so.FindProperty("enemyManager").objectReferenceValue = enemies;
            so.FindProperty("CM").objectReferenceValue = stageCamera;
            so.FindProperty("tran").objectReferenceValue = spawn.transform;
            so.FindProperty("exitLocation").objectReferenceValue = exitMarker;
            so.FindProperty("maxWave").intValue = 1;//第一关不分波次，鸟怪按时间生成
            Ints(so.FindProperty("leftEnemyMax"), 0);
            Ints(so.FindProperty("rightEnemyMax"), 0);
            so.FindProperty("enemyDelay").floatValue = 8f;//旧版每 8 秒出一只鸟
            so.FindProperty("enemyMax").floatValue = 10f;//同时存活上限
            so.FindProperty("firstEnemyDelay").floatValue = 2f;
            so.FindProperty("bossBornDelay").floatValue = 6f;//旧版登顶 6 秒后 Boss 现身
            so.FindProperty("bossTrigger").objectReferenceValue = bossTrigger;
        });

        //6. 只保留一个 EventSystem（BattleUI 自带）。
        foreach (EventSystem system in All<EventSystem>().Skip(1)) Object.DestroyImmediate(system.gameObject);

        if (!stage.ValidateConfiguration(out string error)) throw new InvalidOperationException("关卡配置检查未通过：" + error);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("无法保存 " + ScenePath);

        log.AppendLine($"出生点 {spawnPos}，出口 {exitPos}，Boss 出生 {bossSpawn.position}，Boss 场地 {arenaLeft:F2}～{arenaRight:F2}");
        log.AppendLine($"登顶高度 {bossTrigger.position.y:F2}；相机最高上升 {maxRise:F2}");
        return log.ToString();
    }

    private static bool IsLegacyRoot(GameObject root)
    {
        if (root.GetComponentInChildren<LegacyCamera>(true) || root.GetComponentInChildren<GameManager>(true)
            || root.GetComponentInChildren<Level1Bootstrap>(true) || root.GetComponentInChildren<Level1Enemy>(true)
            || root.GetComponentInChildren<ChangeBlood>(true) || root.GetComponentInChildren<StatusBarUI>(true)
            || root.GetComponentInChildren<PlayerController>(true) || root.GetComponentInChildren<ExitManager>(true)
            || root.GetComponentInChildren<Exit>(true) || root.GetComponentInChildren<EventSystem>(true)
            || root.GetComponentInChildren<Camera>(true))
            return true;
        //旧 HUD 画布（新 BattleUI 稍后才放入，此时场景里的画布都是旧的）
        return root.GetComponentInChildren<Canvas>(true) != null;
    }

    private static float Top(GameObject floor)
    {
        Collider2D collider = floor.GetComponent<Collider2D>();
        if (collider != null) return collider.bounds.max.y;
        SpriteRenderer renderer = floor.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer.bounds.max.y : floor.transform.position.y;
    }

    private static (float, float) TopFloorSpan(Scene scene, float topY, float fallbackCenter)//顶层所有平台连起来的左右范围
    {
        var floors = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c => c.gameObject.layer == GroundLayer && !c.isTrigger && Mathf.Abs(c.bounds.max.y - topY) < 0.3f).ToList();
        if (floors.Count == 0) return (fallbackCenter - 7f, fallbackCenter + 7f);
        return (floors.Min(c => c.bounds.min.x) + 0.5f, floors.Max(c => c.bounds.max.x) - 0.5f);
    }

    private static string Backup()
    {
        Directory.CreateDirectory(BackupFolder);
        string target = Path.Combine(BackupFolder, "Level1Scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity");
        File.Copy(ScenePath, target, false);
        return Path.GetFullPath(target);
    }

    private static GameObject Place(Scene scene, string prefab)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(LevelPrefabs + prefab + ".prefab")
            ?? throw new InvalidOperationException("缺少共用预制体 " + prefab);
        return (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
    }

    private static BossUI ReplaceBossUI(GameObject battleUI)
    {
        BossUI old = battleUI.GetComponentsInChildren<BossUI>(true).FirstOrDefault()
            ?? throw new InvalidOperationException("BattleUI 中找不到 BossUI。");
        if (old is WuYingWangBossUI ready) return ready;
        SerializedObject so = new(old);
        Object blood = so.FindProperty("bloodFull")?.objectReferenceValue;
        Object special = so.FindProperty("specialState")?.objectReferenceValue;
        GameObject panel = old.gameObject;
        Object.DestroyImmediate(old);
        var ui = panel.AddComponent<WuYingWangBossUI>();
        Edit(ui, s =>
        {
            s.FindProperty("bloodFull").objectReferenceValue = blood;
            s.FindProperty("specialState").objectReferenceValue = special;
        });
        if (blood == null || special == null) Debug.LogWarning("[Level1StageSetup] BossUI 的血条或怒气图片引用为空，请在 Inspector 中补齐。", ui);
        return ui;
    }

    private static void CopyFields(Object from, Object to)//同名字段照搬，便于从通用组件换成第一关子类
    {
        SerializedObject source = new(from), target = new(to);
        SerializedProperty property = source.GetIterator();
        for (bool next = property.NextVisible(true); next; next = property.NextVisible(false))
            if (!property.name.StartsWith("m_", StringComparison.Ordinal) && target.FindProperty(property.propertyPath) != null)
                target.CopyFromSerializedProperty(property);
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Rename(Transform parent, string oldName, string newName, Vector3 position)
    {
        Transform marker = parent.Find(oldName) ?? parent.Find(newName);
        if (marker == null) return Marker(parent, newName, position);
        marker.name = newName;
        marker.position = new Vector3(position.x, position.y, marker.position.z);
        return marker;
    }

    private static Transform Marker(Transform parent, string name, Vector3 position)
    {
        Transform marker = parent.Find(name);
        if (marker == null)
        {
            marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
        }
        marker.position = position;
        return marker;
    }

    private static void Ints(SerializedProperty array, params int[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).intValue = values[i];
    }

    private static void Edit(Object component, Action<SerializedObject> edit)
    {
        SerializedObject so = new(component);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

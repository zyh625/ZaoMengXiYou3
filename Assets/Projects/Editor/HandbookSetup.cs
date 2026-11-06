using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

//在 MapScene 的 handbook 面板里搭建图鉴界面。生成的都是普通场景物体，之后可直接在场景里调整位置和大小。
//重复执行会删除上次生成的 HandbookContent 后重建，面板原有的 Block、bg 保留。
public static class HandbookSetup
{
    private const string ScenePath = "Assets/Projects/Scenes/MapScene.unity";
    private const string Images = "Assets/Art/UI/Images/";
    private const string Buttons = "Assets/Art/UI/Buttons/Handbook/";
    private const string ContentName = "HandbookContent";

    //背景板 700 宽；以下位置都按背景板图片的比例量出，单位与地图画布一致。
    private const float BoardWidth = 700f;
    private static readonly Vector2 InnerCenter = new(-0.7f, -23.9f);//背景板中间浅色区域
    private static readonly Vector2 InnerSize = new(650f, 423f);
    private static readonly Vector2 ExitPos = new(295.4f, 220f), ExitSize = new(50f, 46f);//压在背景板右上角的叉上
    private static readonly Vector2 BackPos = new(-95f, 221f);//背景板标题栏，“图鉴”字牌右侧
    private const float BackWidth = 100f;

    private static readonly (string name, string page, string prefab)[] Characters =
    {
        ("悟空", "wukong", "Assets/Projects/Prefabs/Character/WuKong.prefab"),
        ("八戒", "bajie", "Assets/Projects/Prefabs/Character/Bajie.prefab"),
        ("沙僧", "shaseng", "Assets/Projects/Prefabs/Character/Sha.prefab"),
        ("唐僧", "tangseng", "Assets/Projects/Prefabs/Character/Tang.prefab"),
    };
    private const string BirdPrefab = "Assets/Projects/Prefabs/Enemy/BirdEnemy.prefab";

    [MenuItem("造梦西游/地图/搭建图鉴界面")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("图鉴", "请先退出 Play Mode。", "确定");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            Build();
            EditorUtility.DisplayDialog("图鉴", "图鉴界面已生成并保存到 MapScene。", "确定");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("图鉴生成失败", error.Message + "\n\n场景未保存。", "确定");
        }
    }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Map map = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Map>(true)).SingleOrDefault()
            ?? throw new InvalidOperationException("MapScene 中找不到 Map 组件。");
        var panel = new SerializedObject(map).FindProperty("handBook").objectReferenceValue as GameObject
            ?? throw new InvalidOperationException("Map 的 handBook 面板未指定。");

        Transform old = panel.transform.Find(ContentName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform oldBg = panel.transform.Find("bg");
        if (oldBg != null) oldBg.gameObject.SetActive(false);//旧背景保留但不显示
        panel.transform.SetAsLastSibling();//画在地图和按钮上面

        RectTransform content = Rect(ContentName, panel.transform, Vector2.zero, Vector2.zero);
        Sprite boardSprite = Load(Images + "Handbook.png");
        Img("Board", content, boardSprite, Vector2.zero, new Vector2(BoardWidth, BoardWidth * boardSprite.rect.height / boardSprite.rect.width));

        //首页：左角色、右敌人两个方框
        RectTransform home = Rect("Home", content, InnerCenter, InnerSize);
        Button characterButton = Frame(home, "CharacterFrame", -165f, "character", out RectTransform characterStage);
        Button enemyButton = Frame(home, "EnemyFrame", 165f, "enemy", out RectTransform enemyStage);
        Loop(characterStage, Characters[0].prefab);
        Loop(enemyStage, BirdPrefab);

        //角色列表：2×2，从左到右、从上到下为悟空、八戒、沙僧、唐僧
        RectTransform list = Rect("Characters", content, InnerCenter, InnerSize);
        var entries = new HandbookUI.Entry[Characters.Length];
        for (int i = 0; i < Characters.Length; i++)
        {
            Vector2 cell = new(i % 2 == 0 ? -160f : 160f, i < 2 ? 98f : -112f);
            RectTransform stage = Rect(Characters[i].name, list, cell, new Vector2(240f, 185f));
            UISpriteLoop figure = Loop(stage, Characters[i].prefab, out Sprite[] idle, out float fps);
            entries[i] = new HandbookUI.Entry
            {
                name = Characters[i].name,
                page = Load(Images + Characters[i].page + ".png"),
                idle = idle,
                fps = fps,
                figure = Glow(figure.gameObject)
            };
        }

        //介绍页：整图（左文字右展示台），人物站在展示台上
        RectTransform detail = Rect("Detail", content, InnerCenter, InnerSize);
        Image page = Img("Page", detail, entries[0].page, Vector2.zero, InnerSize);
        page.preserveAspect = true;
        RectTransform pedestal = Rect("Stage", detail, new Vector2(172f, 12f), new Vector2(200f, 260f));//底边约在展示台台面
        UISpriteLoop detailFigure = Loop(pedestal, Characters[0].prefab);

        Button back = SpriteButton("BackButton", content, "back", "backLight", BackPos, BackWidth);
        Button exit = SpriteButton("ExitButton", content, Load(Images + "BagCloseNormal.png"), Load(Images + "BagCloseSelected.png"), ExitPos, ExitSize);

        HandbookUI ui = panel.GetComponent<HandbookUI>();
        if (ui == null) ui = panel.AddComponent<HandbookUI>();
        Edit(ui, so =>
        {
            so.FindProperty("homePage").objectReferenceValue = home.gameObject;
            so.FindProperty("characterPage").objectReferenceValue = list.gameObject;
            so.FindProperty("detailPage").objectReferenceValue = detail.gameObject;
            so.FindProperty("characterButton").objectReferenceValue = characterButton;
            so.FindProperty("enemyButton").objectReferenceValue = enemyButton;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("exitButton").objectReferenceValue = exit;
            so.FindProperty("detailImage").objectReferenceValue = page;
            so.FindProperty("detailFigure").objectReferenceValue = detailFigure;
            SerializedProperty array = so.FindProperty("characters");
            array.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = entries[i].name;
                e.FindPropertyRelative("page").objectReferenceValue = entries[i].page;
                e.FindPropertyRelative("fps").floatValue = entries[i].fps;
                e.FindPropertyRelative("figure").objectReferenceValue = entries[i].figure;
                SerializedProperty frames = e.FindPropertyRelative("idle");
                frames.arraySize = entries[i].idle.Length;
                for (int f = 0; f < entries[i].idle.Length; f++) frames.GetArrayElementAtIndex(f).objectReferenceValue = entries[i].idle[f];
            }
        });

        list.gameObject.SetActive(false);
        detail.gameObject.SetActive(false);
        back.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("无法保存 MapScene。");
        Debug.Log("[HandbookSetup] 图鉴界面已生成：" + ContentName + "（首页 / 角色列表 / 介绍页）");
    }

    //方框：上半部分展示待机动画，下半部分放按钮。方框图片里展示区约占 6.8%～72.3% 高度，按钮区约 73.6%～94.4%。
    private static Button Frame(RectTransform parent, string name, float x, string button, out RectTransform stage)
    {
        Sprite sprite = Load(Images + "Handbook_2.png");
        float height = 400f, width = height * sprite.rect.width / sprite.rect.height;
        Image frame = Img(name, parent, sprite, new Vector2(x, 0f), new Vector2(width, height));
        float Y(float fraction) => height / 2f - fraction * height;
        stage = Rect("Display", frame.transform, new Vector2(0f, (Y(0.0684f) + Y(0.7227f)) / 2f - 4f), new Vector2(width * 0.78f, height * 0.58f));
        return SpriteButton(name.Replace("Frame", "Button"), frame.transform, button, button + "Light",
            new Vector2(0f, (Y(0.7357f) + Y(0.944f)) / 2f), 140f);
    }

    private static UISpriteLoop Loop(RectTransform stage, string prefab) => Loop(stage, prefab, out _, out _);

    private static UISpriteLoop Loop(RectTransform stage, string prefab, out Sprite[] idle, out float fps)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab) ?? throw new InvalidOperationException("缺少预制体 " + prefab);
        Component controller = asset.GetComponent<PlayerController>();
        if (controller == null) controller = asset.GetComponent<EnemyController>();
        if (controller == null) throw new InvalidOperationException(prefab + " 上没有角色或敌人控制器。");
        SerializedProperty data = new SerializedObject(controller).FindProperty("Idle")
            ?? throw new InvalidOperationException(prefab + " 找不到 Idle 待机帧。");
        SerializedProperty sprites = data.FindPropertyRelative("sprites");
        idle = Enumerable.Range(0, sprites.arraySize).Select(i => sprites.GetArrayElementAtIndex(i).objectReferenceValue as Sprite).Where(s => s != null).ToArray();
        fps = Mathf.Max(1f, data.FindPropertyRelative("fps").floatValue);
        if (idle.Length == 0) throw new InvalidOperationException(prefab + " 的 Idle 待机帧为空。");

        GameObject go = new("Figure", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(stage, false);
        go.GetComponent<Image>().sprite = idle[0];
        go.GetComponent<Image>().raycastTarget = false;
        UISpriteLoop loop = go.AddComponent<UISpriteLoop>();
        Sprite[] frames = idle;
        float rate = fps;
        Edit(loop, so =>
        {
            SerializedProperty array = so.FindProperty("frames");
            array.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("fps").floatValue = rate;
        });
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        Vector2 largest = frames.Aggregate(Vector2.zero, (m, s) => Vector2.Max(m, s.rect.size));
        float scale = Mathf.Min(stage.sizeDelta.x / largest.x, stage.sizeDelta.y / largest.y);
        rect.sizeDelta = frames[0].rect.size * scale;//与运行时相同的尺寸，场景里预览即为实际效果
        return loop;
    }

    private static HandbookFigure Glow(GameObject figure)//黄色描边沿人物透明轮廓发光，悬停时开启
    {
        figure.GetComponent<Image>().raycastTarget = true;
        Outline inner = figure.AddComponent<Outline>();
        inner.effectColor = new Color(1f, 0.88f, 0.3f, 0.95f);
        inner.effectDistance = new Vector2(2.5f, -2.5f);
        Outline outer = figure.AddComponent<Outline>();
        outer.effectColor = new Color(1f, 0.75f, 0.1f, 0.45f);
        outer.effectDistance = new Vector2(5f, -5f);
        inner.enabled = outer.enabled = false;
        HandbookFigure hover = figure.AddComponent<HandbookFigure>();
        Edit(hover, so =>
        {
            SerializedProperty glow = so.FindProperty("glow");
            glow.arraySize = 2;
            glow.GetArrayElementAtIndex(0).objectReferenceValue = inner;
            glow.GetArrayElementAtIndex(1).objectReferenceValue = outer;
        });
        return hover;
    }

    private static Button SpriteButton(string name, Transform parent, string normal, string lighted, Vector2 position, float width)
    {
        Sprite n = Load(Buttons + normal + ".png");
        return SpriteButton(name, parent, n, Load(Buttons + lighted + ".png"), position, new Vector2(width, width * n.rect.height / n.rect.width));
    }

    private static Button SpriteButton(string name, Transform parent, Sprite normal, Sprite lighted, Vector2 position, Vector2 size)
    {
        Image image = Img(name, parent, normal, position, size);
        image.preserveAspect = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;//发亮由 MouseOn 换图负责
        MouseOn hover = image.gameObject.AddComponent<MouseOn>();
        Edit(hover, so =>
        {
            so.FindProperty("normal").objectReferenceValue = normal;
            so.FindProperty("lighted").objectReferenceValue = lighted;
        });
        return button;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Sprite Load(string path)//图片是多图模式且带零碎切片，取面积最大的那张（即整张图去掉透明边）
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
        return sprite != null ? sprite : throw new InvalidOperationException("找不到图片 " + path);
    }

    private static void Edit(Object target, Action<SerializedObject> edit)
    {
        SerializedObject so = new(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

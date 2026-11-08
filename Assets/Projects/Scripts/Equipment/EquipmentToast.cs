using System.Collections.Generic;
using TMPro;
using UnityEngine;

//拾取提示：在屏幕上方弹出一行。装备按品质颜色显示，法宝用青玉色；金币显示“金币 +数量”，用金色。
//连续拾取金币时合并到同一行累加，避免刷屏。背包已满等普通提示也用这里。
public sealed class EquipmentToast : MonoBehaviour
{
    private const float Life = 3f;
    private sealed class Line { public TextMeshProUGUI text; public float age; public int gold; }//gold > 0 表示金币行
    public static readonly Color GoldColor = new(1f, 0.82f, 0.22f);
    private const float GoldMergeWindow = 1.2f;//这段时间内再捡到金币，就加到同一行上
    private readonly List<Line> lines = new();
    private RectTransform root;
    private static EquipmentToast instance;
    private static Material outlined;

    private static Material OutlinedMaterial
    {
        get
        {
            if (outlined != null) return outlined;
            TMP_FontAsset font = UIFonts.Chinese;
            if (font == null || font.material == null) return null;
            outlined = new Material(font.material) { name = "ToastOutline" };
            outlined.EnableKeyword(ShaderUtilities.Keyword_Outline);
            outlined.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            outlined.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(0, 0, 0, 220));
            return outlined;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        instance = null;
        EquipmentService.Obtained -= Show;
        EquipmentService.Obtained += Show;
    }

    private static void Show(EquipmentItem item, EquipmentDatabase.Item def, int soldFor)
    {
        if (def == null) return;
        string rarity = TreasureRules.Is(def) ? "法宝" : EquipmentRules.RarityName(item.rarity);//法宝不分品质
        Message(soldFor > 0 ? $"获得【{rarity}】{def.displayName}（重复，换成 {soldFor} 金币）" : $"获得【{rarity}】{def.displayName}",
            EquipmentRules.ItemColor(def, item.rarity));
    }

    public static void Gold(int amount)
    {
        if (amount <= 0) return;
        Ensure();
        Line last = instance.lines.Count > 0 ? instance.lines[instance.lines.Count - 1] : null;
        if (last != null && last.gold > 0 && last.text != null && last.age < GoldMergeWindow)
        {
            last.gold += amount;
            last.age = 0f;//重新计时，最后一次拾取后才开始淡出
            last.text.SetText($"金币 +{last.gold}");
            return;
        }
        instance.Add($"金币 +{amount}", GoldColor).gold = amount;
    }

    public static void Message(string message, Color color)//背包已满、出售所得等普通提示也用这一行
    {
        Ensure();
        instance.Add(message, color);
    }

    private static void Ensure()
    {
        if (instance == null)
        {
            var go = new GameObject("EquipmentToast", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();//和战斗界面一样以 800x600 为基准，窗口放大时文字等比放大
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;
            instance = go.AddComponent<EquipmentToast>();
            instance.root = (RectTransform)go.transform;
        }
    }

    private Line Add(string message, Color color)
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(900f, 44f);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = UIFonts.Chinese;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        if (OutlinedMaterial != null) text.fontSharedMaterial = OutlinedMaterial;//所有提示共用一份带描边的材质，不再每行新建
        text.raycastTarget = false;
        text.SetText(message);
        var line = new Line { text = text };
        lines.Add(line);
        return line;
    }

    private void Update()
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            Line line = lines[i];
            line.age += Time.unscaledDeltaTime;
            if (line.age >= Life || line.text == null)
            {
                if (line.text != null) Destroy(line.text.gameObject);
                lines.RemoveAt(i);
            }
        }
        for (int i = 0; i < lines.Count; i++)//新提示在最上面，旧的往下排
        {
            Line line = lines[lines.Count - 1 - i];
            ((RectTransform)line.text.transform).anchoredPosition = new Vector2(0f, -90f - i * 46f);
            Color c = line.text.color;
            c.a = Mathf.Clamp01((Life - line.age) / 0.6f);
            line.text.color = c;
        }
    }
}

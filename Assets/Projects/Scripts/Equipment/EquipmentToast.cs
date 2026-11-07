using System.Collections.Generic;
using TMPro;
using UnityEngine;

//获得装备时在屏幕上方弹出一行提示，文字颜色按品质；换成金币的也会说明。
public sealed class EquipmentToast : MonoBehaviour
{
    private const float Life = 3f;
    private sealed class Line { public TextMeshProUGUI text; public float age; }
    private readonly List<Line> lines = new();
    private RectTransform root;
    private static EquipmentToast instance;

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
        if (instance == null)
        {
            var go = new GameObject("EquipmentToast", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            instance = go.AddComponent<EquipmentToast>();
            instance.root = (RectTransform)go.transform;
        }
        string rarity = EquipmentRules.RarityName(item.rarity);
        string message = soldFor > 0 ? $"获得【{rarity}】{def.displayName}（重复，换成 {soldFor} 金币）" : $"获得【{rarity}】{def.displayName}";
        instance.Add(message, EquipmentRules.RarityColor(item.rarity));
    }

    private void Add(string message, Color color)
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(900f, 44f);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;//默认字体已配置中文后备字体
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 220);
        text.raycastTarget = false;
        text.SetText(message);
        lines.Add(new Line { text = text });
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

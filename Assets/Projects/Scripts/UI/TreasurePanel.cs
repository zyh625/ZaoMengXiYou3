using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//法宝界面：显示当前角色法宝栏里的法宝。左边是法宝图，右边是名称、等级、各项属性和功能，下方升级。
//法宝在背包里装备到法宝栏；这里只看和升级。鼠标停在法宝图上显示与背包一致的说明。
public sealed class TreasurePanel : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI title, info, maxLabel;
    [SerializeField] private SpriteNumber level, cost, gold;
    [SerializeField] private Button upgrade;

    private PlayerManager party;
    private EquipmentItem shown;
    private GameObject tooltip;
    private TextMeshProUGUI tooltipText;
    private bool hovering;
    private float hoverStarted;
    private Vector2 hoverPoint;
    private Camera hoverCamera;
    private const float HoverDelay = 0.45f;

    private static readonly Color Ink = new(0.13f, 0.24f, 0.22f);//右侧浅色纸面上的墨色
    private static readonly Color TitleInk = new(0.08f, 0.30f, 0.26f);

    private void Awake()
    {
        if (upgrade != null)
        {
            upgrade.onClick.AddListener(OnUpgrade);
            var events = upgrade.gameObject.GetComponent<BagCellEvents>();
            if (events == null) events = upgrade.gameObject.AddComponent<BagCellEvents>();
            events.Enter = _ => { if (upgrade.interactable) upgrade.transform.localScale = Vector3.one * 1.06f; };
            events.Exit = _ => upgrade.transform.localScale = Vector3.one;
        }
        if (icon != null)
        {
            icon.raycastTarget = true;
            var events = icon.gameObject.GetComponent<BagCellEvents>();
            if (events == null) events = icon.gameObject.AddComponent<BagCellEvents>();
            events.Enter = e => { hovering = true; hoverStarted = Time.unscaledTime; hoverPoint = e.position; hoverCamera = e.enterEventCamera; };
            events.Move = e => { if (!tooltip.activeSelf) { hoverPoint = e.position; hoverStarted = Time.unscaledTime; } };
            events.Exit = _ => { hovering = false; HideTooltip(); };
        }
        foreach (TextMeshProUGUI text in new[] { title, info, maxLabel })
            if (text != null && UIFonts.Chinese != null) text.font = UIFonts.Chinese;
        if (title != null) title.color = TitleInk;
        if (info != null) info.color = Ink;
        BuildTooltip();
    }

    private void OnEnable()
    {
        if (GameSession.instance != null) GameSession.instance.DataChanged += Refresh;
        EquipmentService.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (GameSession.instance != null) GameSession.instance.DataChanged -= Refresh;
        EquipmentService.Changed -= Refresh;
        hovering = false;
        HideTooltip();
        if (upgrade != null) upgrade.transform.localScale = Vector3.one;
    }

    private void Update()//悬停计时用不受暂停影响的时间
    {
        if (!hovering || shown == null || tooltip.activeSelf) return;
        if (Time.unscaledTime - hoverStarted >= HoverDelay) ShowTooltip();
    }

    private void Refresh()
    {
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        gold?.SetValue(Mathf.Max(0, data.gold));
        if (party == null) party = FindAnyObjectByType<PlayerManager>();
        PlayerResources resources = party != null && party.CurrentPlayer != null ? party.CurrentPlayer.GetComponent<PlayerResources>() : null;
        PlayerId id = resources != null ? resources.Id : PlayerId.WuKong;
        EquipmentItem item = EquipmentService.Equipped(data, id, EquipSlot.Treasure);
        EquipmentDatabase.Item def = item != null ? EquipmentDatabase.Instance?.Find(item.id) : null;

        if (!TreasureRules.Is(def))
        {
            shown = null;
            HideTooltip();
            if (icon != null) icon.enabled = false;
            title?.SetText("未装备法宝");
            if (level != null) level.gameObject.SetActive(false);
            info?.SetText("在背包里把法宝放进法宝栏后，可以在这里查看和升级。\n法宝由各关 Boss 概率掉落，每件只有一个，没有品质之分，靠升级变强。");
            ShowCost(0, data.gold, false);
            return;
        }

        shown = item;
        int lv = TreasureRules.Level(item);
        TreasureKind kind = TreasureRules.Kind(def);
        if (icon != null) { icon.enabled = def.icon != null; icon.sprite = def.icon; }
        title?.SetText(def.displayName);
        if (level != null) { level.gameObject.SetActive(true); level.SetText("L" + lv); }
        var lines = new List<string>(TreasureRules.StatLines(kind, lv)) { TreasureRules.Function(kind) };
        info?.SetText(string.Join("\n", lines));
        ShowCost(TreasureRules.Cost(lv), data.gold, true);
        if (tooltip != null && tooltip.activeSelf) ShowTooltip();//升级后说明里的数值同步更新
    }

    private void ShowCost(int price, int owned, bool hasTreasure)
    {
        bool maxed = hasTreasure && price <= 0;
        if (cost != null) { cost.gameObject.SetActive(price > 0); if (price > 0) cost.SetValue(price); }
        if (maxLabel != null) { maxLabel.gameObject.SetActive(maxed); maxLabel.SetText("已满级"); }
        if (upgrade != null)
        {
            upgrade.interactable = price > 0;
            upgrade.transform.localScale = Vector3.one;
        }
    }

    private void OnUpgrade()
    {
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        if (shown == null) return;
        int price = TreasureRules.Cost(TreasureRules.Level(shown));
        if (price <= 0) return;
        if (data.gold < price) { EquipmentToast.Message("金币不足", Color.white); return; }
        TreasureRules.TryUpgrade(data, shown);//成功后 Changed 事件会刷新本界面和角色属性
    }

    //---------- 悬停说明：与背包相同的样式和内容 ----------
    private void BuildTooltip()
    {
        tooltip = new GameObject("TreasureTooltip", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)tooltip.transform;
        rect.SetParent(transform, false);
        rect.pivot = new Vector2(0f, 1f);
        var img = tooltip.GetComponent<Image>();
        img.color = new Color(0.08f, 0.06f, 0.04f, 0.94f);
        img.raycastTarget = false;
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var tr = (RectTransform)go.transform;
        tr.SetParent(rect, false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(10f, 8f); tr.offsetMax = new Vector2(-10f, -8f);
        tooltipText = go.GetComponent<TextMeshProUGUI>();
        tooltipText.font = UIFonts.Chinese;
        tooltipText.fontSize = 15f;
        tooltipText.alignment = TextAlignmentOptions.TopLeft;
        tooltipText.color = new Color(1f, 0.93f, 0.78f);
        tooltipText.raycastTarget = false;
        tooltipText.lineSpacing = 6f;
        tooltip.SetActive(false);
    }

    private void ShowTooltip()
    {
        if (shown == null) return;
        tooltip.SetActive(true);//先启用再测量
        tooltipText.SetText(BagInventory.Describe(shown));
        const float width = 240f;
        Vector2 size = tooltipText.GetPreferredValues(width - 20f, 0f);
        var rect = (RectTransform)tooltip.transform;
        rect.sizeDelta = new Vector2(width, size.y + 18f);
        tooltip.transform.SetAsLastSibling();
        var canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, hoverPoint, hoverCamera, out Vector2 local)) return;
        Vector2 half = canvasRect.rect.size / 2f, offset = new(14f, -14f);
        bool flipX = local.x + offset.x + rect.sizeDelta.x > half.x;
        bool flipY = local.y + offset.y - rect.sizeDelta.y < -half.y;
        rect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        rect.position = canvasRect.TransformPoint(local + new Vector2(flipX ? -offset.x : offset.x, flipY ? -offset.y : offset.y));
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.SetActive(false);
    }
}

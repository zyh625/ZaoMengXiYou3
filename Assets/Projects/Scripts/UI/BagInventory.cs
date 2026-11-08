using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//背包右侧 25 格和左侧 4 个装备栏。格子位置按 Bag.png 上画好的方格换算，运行时生成。
//· 单击背包里的装备：在鼠标处弹出“装备 / 出售”。装备会挤下原来那件放到背包最后。
//· 单击装备栏：直接卸下，放到背包最后。
//· 鼠标停留一会儿：在鼠标处显示名称、介绍、品质（法宝为等级）和属性。
public sealed class BagInventory : MonoBehaviour
{
    //Bag.png（1536x1023）铺满 750.4x500 的换算
    private const float ImgW = 1536f, ImgH = 1023f, VisW = 750.4f, VisH = 500f;
    private static Vector2 Ui(float px, float py) => new((px - ImgW / 2f) * VisW / ImgW, (ImgH / 2f - py) * VisH / ImgH);
    private static readonly float[] GridX = { 888f, 1015f, 1141.5f, 1268f, 1395f };
    private static readonly float[] GridY = { 231f, 362.5f, 494f, 625.5f, 757.5f };
    private static readonly (EquipSlot slot, float x, float y)[] EquipBoxes =
        { (EquipSlot.Weapon, 532f, 323.5f), (EquipSlot.Accessory, 696f, 323.5f), (EquipSlot.Armor, 532f, 495f), (EquipSlot.Treasure, 696f, 495f) };
    private const float HoverDelay = 0.45f;

    private sealed class Cell
    {
        public RectTransform rect;
        public Image cover, glow, icon;
        public EquipmentItem item;
        public bool equipSlot;
        public EquipSlot slot;
    }

    private RectTransform root, layer;
    private readonly List<Cell> grid = new(), equips = new();
    private PlayerId current;
    private GameObject menu, blocker, tooltip;
    private TextMeshProUGUI tooltipText;
    private Button equipButton;
    private TextMeshProUGUI equipLabel;
    private EquipmentItem menuItem;
    private Cell hovered;
    private float hoverStarted;
    private Vector2 hoverPoint;
    private Camera eventCamera;

    public void Build(RectTransform dataRoot, Sprite sell = null, Sprite sellLight = null, Sprite sort = null, Sprite sortLight = null)
    {
        if (root != null) return;
        root = (RectTransform)transform;
        layer = dataRoot != null ? dataRoot : root;
        Vector2 cellSize = new(110f * VisW / ImgW, 114f * VisH / ImgH);
        foreach (float y in GridY)
            foreach (float x in GridX)
                grid.Add(MakeCell("BagSlot" + (grid.Count + 1), Ui(x, y), cellSize, 44f, false, default));
        foreach (var (slot, x, y) in EquipBoxes)
            equips.Add(MakeCell("Equip" + slot, Ui(x, y), new Vector2(140f * VisW / ImgW, 145f * VisH / ImgH), 56f, true, slot));
        //右下角：金币框右边并排放两个按钮
        ActionButton("SellWhite", Ui(1232f, 883f), sell, sellLight, SellWhite);
        ActionButton("SortBag", Ui(1390f, 883f), sort, sortLight, SortBag);
        BuildMenu();
        BuildTooltip();
    }

    private void ActionButton(string name, Vector2 pos, Sprite normal, Sprite light, Action click)
    {
        if (normal == null) return;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(BagCellEvents));
        var rect = (RectTransform)go.transform;
        rect.SetParent(layer, false);
        rect.anchoredPosition = pos;
        float width = 150f * VisW / ImgW;
        rect.sizeDelta = new Vector2(width, width * normal.rect.height / normal.rect.width);
        var img = go.GetComponent<Image>();
        img.sprite = normal;
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;//悬停效果由换图完成
        button.onClick.AddListener(() => { CloseMenu(); HideTooltip(); click(); });
        var events = go.GetComponent<BagCellEvents>();
        events.Enter = _ => { if (light != null) img.sprite = light; };//外圈变黄
        events.Exit = _ => img.sprite = normal;
    }

    private void SellWhite()
    {
        EquipmentService.SellAllCommon(GameSession.EnsureSession().EnsureRuntimeData(), out _);//卖完不弹任何提示，格子和金币直接刷新
    }

    private void SortBag() => EquipmentService.SortBag(GameSession.EnsureSession().EnsureRuntimeData());

    public void Refresh(PlayerId id)
    {
        current = id;
        if (root == null) return;
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        List<EquipmentItem> loose = EquipmentService.LooseItems(data);
        for (int i = 0; i < grid.Count; i++) Show(grid[i], i < loose.Count ? loose[i] : null);
        foreach (Cell cell in equips) Show(cell, EquipmentService.Equipped(data, id, cell.slot));
    }

    private void OnDisable()
    {
        CloseMenu();
        HideTooltip();
        hovered = null;
    }

    private void Update()//悬停计时用不受暂停影响的时间
    {
        if (hovered == null || hovered.item == null || menu.activeSelf || tooltip.activeSelf) return;
        if (Time.unscaledTime - hoverStarted >= HoverDelay) ShowTooltip(hovered.item, hoverPoint);
    }

    //---------- 格子 ----------
    private Cell MakeCell(string name, Vector2 pos, Vector2 size, float iconSize, bool equipSlot, EquipSlot slot)
    {
        var cell = new Cell { equipSlot = equipSlot, slot = slot };
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(BagCellEvents));
        cell.rect = (RectTransform)go.transform;
        cell.rect.SetParent(layer, false);
        cell.rect.anchoredPosition = pos;
        cell.rect.sizeDelta = size;
        var hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);//透明点击区
        if (equipSlot)//装备栏底图上画着“武器”等字，穿上装备时用同色底块盖住
        {
            cell.cover = Child(cell.rect, "Cover", null, size * 0.84f, false);
            cell.cover.color = new Color(0.50f, 0.31f, 0.16f, 1f);
            cell.cover.enabled = false;
        }
        cell.glow = Child(cell.rect, "Glow", FxTextures.Glow, size * 1.1f, false);
        cell.icon = Child(cell.rect, "Icon", null, new Vector2(iconSize, iconSize), true);
        var events = go.GetComponent<BagCellEvents>();
        events.Click = e => OnCellClick(cell, e);
        events.Enter = e => { hovered = cell; hoverStarted = Time.unscaledTime; hoverPoint = e.position; eventCamera = e.enterEventCamera; };
        events.Move = e => { if (hovered == cell && !tooltip.activeSelf) { hoverPoint = e.position; hoverStarted = Time.unscaledTime; } };
        events.Exit = _ => { if (hovered == cell) hovered = null; HideTooltip(); };
        return cell;
    }

    private static Image Child(RectTransform parent, string name, Sprite sprite, Vector2 size, bool preserve)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = preserve;
        img.raycastTarget = false;
        return img;
    }

    private static void Show(Cell cell, EquipmentItem item)
    {
        cell.item = item;
        EquipmentDatabase.Item def = item != null ? EquipmentDatabase.Instance?.Find(item.id) : null;
        cell.icon.enabled = def != null && def.icon != null;
        cell.icon.sprite = def?.icon;
        cell.glow.enabled = def != null;
        if (cell.cover != null) cell.cover.enabled = def != null;
        if (def != null)
        {
            Color c = EquipmentRules.ItemColor(def, item.rarity);
            cell.glow.color = new Color(c.r, c.g, c.b, TreasureRules.Is(def) ? 0.6f : 0.35f + 0.1f * item.rarity);
        }
    }

    private void OnCellClick(Cell cell, PointerEventData e)
    {
        HideTooltip();
        if (cell.item == null) return;
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        if (cell.equipSlot)
        {
            if (cell.slot == EquipSlot.Treasure && TreasureUser.IsLocked(current))
                EquipmentToast.Message($"法宝冷却中，{Mathf.CeilToInt(TreasureUser.Remaining(current))} 秒后才能卸下", Color.white);
            else if (!EquipmentService.Unequip(data, current, cell.slot)) EquipmentToast.Message("背包已满，无法卸下", Color.white);
            return;
        }
        OpenMenu(cell.item, e.position, e.pressEventCamera);
    }

    //---------- 装备 / 出售 ----------
    private void BuildMenu()
    {
        blocker = new GameObject("BagMenuBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
        var br = (RectTransform)blocker.transform;
        br.SetParent(root, false);
        br.sizeDelta = new Vector2(4000f, 4000f);
        blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        blocker.GetComponent<Button>().onClick.AddListener(CloseMenu);
        blocker.SetActive(false);

        menu = new GameObject("BagMenu", typeof(RectTransform), typeof(Image));
        var mr = (RectTransform)menu.transform;
        mr.SetParent(root, false);
        mr.pivot = new Vector2(0f, 1f);
        mr.sizeDelta = new Vector2(92f, 70f);
        menu.GetComponent<Image>().color = new Color(0.10f, 0.07f, 0.04f, 0.95f);
        equipButton = MenuButton(mr, "装备", 0, () => { var item = menuItem; CloseMenu(); DoEquip(item); }, out equipLabel);
        MenuButton(mr, "出售", 1, () => { var item = menuItem; CloseMenu(); DoSell(item); }, out _);
        menu.SetActive(false);
    }

    private static Button MenuButton(RectTransform parent, string label, int index, Action click, out TextMeshProUGUI text)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -5f - index * 32f);
        rect.sizeDelta = new Vector2(82f, 28f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.45f, 0.28f, 0.12f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(() => click());
        text = Label(rect, label, 18f, TextAlignmentOptions.Center);
        return button;
    }

    private static TextMeshProUGUI Label(RectTransform parent, string value, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = UIFonts.Chinese;
        text.fontSize = size;
        text.alignment = align;
        text.color = new Color(1f, 0.93f, 0.78f);
        text.raycastTarget = false;
        text.SetText(value);
        return text;
    }

    private void OpenMenu(EquipmentItem item, Vector2 screen, Camera cam)
    {
        menuItem = item;
        EquipmentDatabase.Item def = EquipmentDatabase.Instance?.Find(item.id);
        bool mine = def != null && (def.owner < 0 || def.owner == (int)current);
        equipButton.interactable = mine;
        equipLabel.color = mine ? new Color(1f, 0.93f, 0.78f) : new Color(0.6f, 0.55f, 0.5f);
        blocker.SetActive(true);
        blocker.transform.SetAsLastSibling();
        menu.SetActive(true);
        menu.transform.SetAsLastSibling();
        Place((RectTransform)menu.transform, screen, cam, new Vector2(4f, -4f));
    }

    private void CloseMenu()
    {
        menuItem = null;
        if (menu != null) menu.SetActive(false);
        if (blocker != null) blocker.SetActive(false);
    }

    private void DoEquip(EquipmentItem item)
    {
        if (item == null) return;
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        if (TreasureRules.Is(EquipmentDatabase.Instance?.Find(item.id)) && TreasureUser.IsLocked(current))
        {
            EquipmentToast.Message($"法宝冷却中，{Mathf.CeilToInt(TreasureUser.Remaining(current))} 秒后才能更换", Color.white);
            return;
        }
        if (!EquipmentService.EquipFromBag(data, current, item))
        {
            EquipmentDatabase.Item def = EquipmentDatabase.Instance?.Find(item.id);
            string owner = def != null && def.owner >= 0 ? OwnerName(def.owner) : "当前角色";
            EquipmentToast.Message($"这是{owner}的专属装备", Color.white);
        }
    }

    private void DoSell(EquipmentItem item)
    {
        int gold = EquipmentService.SellItem(GameSession.EnsureSession().EnsureRuntimeData(), item);
        if (gold > 0) EquipmentToast.Message($"出售获得 {gold} 金币", new Color(1f, 0.85f, 0.3f));
    }

    //---------- 悬停说明 ----------
    private void BuildTooltip()
    {
        tooltip = new GameObject("BagTooltip", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)tooltip.transform;
        rect.SetParent(root, false);
        rect.pivot = new Vector2(0f, 1f);
        var img = tooltip.GetComponent<Image>();
        img.color = new Color(0.08f, 0.06f, 0.04f, 0.94f);
        img.raycastTarget = false;
        tooltipText = Label(rect, "", 15f, TextAlignmentOptions.TopLeft);
        var tr = (RectTransform)tooltipText.transform;
        tr.offsetMin = new Vector2(10f, 8f); tr.offsetMax = new Vector2(-10f, -8f);
        tooltipText.lineSpacing = 6f;
        tooltip.SetActive(false);
    }

    private void ShowTooltip(EquipmentItem item, Vector2 screen)
    {
        EquipmentDatabase.Item def = EquipmentDatabase.Instance?.Find(item.id);
        if (def == null) return;
        tooltip.SetActive(true);//先启用再测量，隐藏状态下测量可能取不到字形
        tooltipText.SetText(Describe(item));
        const float width = 240f;
        Vector2 size = tooltipText.GetPreferredValues(width - 20f, 0f);
        var rect = (RectTransform)tooltip.transform;
        rect.sizeDelta = new Vector2(width, size.y + 18f);
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();
        Place(rect, screen, eventCamera, new Vector2(14f, -14f));
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.SetActive(false);
    }

    //悬停说明，每项一行。背包和法宝界面共用，保证两边显示一致。
    public static string Describe(EquipmentItem item)
    {
        EquipmentDatabase.Item def = item != null ? EquipmentDatabase.Instance?.Find(item.id) : null;
        if (def == null) return string.Empty;
        int rarity = item.rarity;
        bool treasure = TreasureRules.Is(def);
        string hex = ColorUtility.ToHtmlStringRGB(EquipmentRules.ItemColor(def, rarity));
        float p = EquipmentRules.Power(def.grade, rarity);
        var lines = new List<string>
        {
            $"<size=19><b><color=#{hex}>{def.displayName}</color></b></size>",
            $"<color=#B9AE98><size=13>{def.description}</size></color>",
            treasure ? $"等级：<color=#{hex}>Lv{TreasureRules.Level(item)}</color>" : $"品质：<color=#{hex}>{EquipmentRules.RarityName(rarity)}</color>",
        };
        PlayerId owner = def.owner >= 0 ? (PlayerId)def.owner : PlayerId.WuKong;
        switch (def.slot)
        {
            case EquipSlot.Weapon:
                lines.Add($"攻击力：+{Pct(EquipmentRules.WeaponAttack * p)}");
                lines.Add($"暴击率：+{Pct(EquipmentRules.WeaponCrit * EquipmentRules.WeaponCritScale(owner) * p)}");
                break;
            case EquipSlot.Armor:
                lines.Add($"生命：+{Pct(EquipmentRules.ArmorHP * EquipmentRules.ArmorHPScale(owner) * p)}");
                lines.Add($"减伤：+{Pct(EquipmentRules.ArmorReduction * EquipmentRules.ArmorReductionScale(owner) * p)}");
                break;
            case EquipSlot.Accessory:
                lines.Add($"暴击率：+{Pct(EquipmentRules.AccessoryCrit * p)}");
                lines.Add($"回血：每5秒 +{EquipmentRules.AccessoryHPRegen * p:0.#}");
                lines.Add($"回蓝：每5秒 +{EquipmentRules.AccessoryMPRegen * p:0.#}");
                break;
            default:
                TreasureKind kind = TreasureRules.Kind(def);
                lines.AddRange(TreasureRules.StatLines(kind, TreasureRules.Level(item)));
                lines.Add(TreasureRules.Function(kind));
                break;
        }
        lines.Add(def.owner >= 0 ? $"专属：{OwnerName(def.owner)}" : "通用");
        lines.Add($"<color=#E8C25A>出售：{EquipmentService.SellPrice(item)} 金币</color>");
        return string.Join("\n", lines);
    }

    private static string Pct(float ratio) => $"{ratio * 100f:0.#}%";
    private static string OwnerName(int owner) => owner switch { 0 => "悟空", 1 => "八戒", 2 => "沙僧", 3 => "唐僧", _ => "" };

    private void Place(RectTransform rect, Vector2 screen, Camera cam, Vector2 offset)//放在鼠标处，超出画面时翻到另一侧
    {
        var canvasRect = (RectTransform)root.GetComponentInParent<Canvas>().rootCanvas.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out Vector2 local)) return;
        Vector2 half = canvasRect.rect.size / 2f;
        Vector2 size = rect.sizeDelta;
        bool flipX = local.x + offset.x + size.x > half.x;
        bool flipY = local.y + offset.y - size.y < -half.y;
        rect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        Vector2 at = local + new Vector2(flipX ? -offset.x : offset.x, flipY ? -offset.y : offset.y);
        Vector3 world = canvasRect.TransformPoint(at);
        rect.position = world;
    }
}

//格子的鼠标事件转发
public sealed class BagCellEvents : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public Action<PointerEventData> Click, Enter, Exit, Move;
    public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Click?.Invoke(e); }
    public void OnPointerEnter(PointerEventData e) => Enter?.Invoke(e);
    public void OnPointerExit(PointerEventData e) => Exit?.Invoke(e);
    public void OnPointerMove(PointerEventData e) => Move?.Invoke(e);
}

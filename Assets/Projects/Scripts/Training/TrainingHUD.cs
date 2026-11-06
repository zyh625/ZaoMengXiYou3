using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 训练场控制面板：
/// - 面板与按键半透明、尺寸紧凑，尽量少遮挡战斗画面；按键文字自动取能放下的最大字号；
/// - 左上角色开关、右上敌人控件、右侧伤害统计、底部角色状态栏（底部栏保持原样）；
/// - 按 H（可在 Inspector 改）隐藏/显示全部面板。
/// </summary>
public sealed class TrainingHUD : MonoBehaviour
{
    [SerializeField] private TrainingManager manager;
    [SerializeField] private TrainingHitboxDebug hitboxes;
    [SerializeField] private Key toggleKey = Key.H;//隐藏/显示训练面板

    // 左上方：角色控制控件
    private Button btnInvulnerable;
    private Button btnNoCooldown;
    private Button btnInfiniteResources;
    private Button btnBoost;
    private Text txtInvulnerable;
    private Text txtNoCooldown;
    private Text txtInfiniteResources;
    private Text txtBoost;
    private Outline outInvulnerable;
    private Outline outNoCooldown;
    private Outline outInfiniteResources;
    private Outline outBoost;

    // 右上方：敌人控制控件
    private Text txtEnemyName;
    private Button btnEnemyPassive;
    private Button btnEnemyInfiniteHp;
    private Text txtEnemyPassive;
    private Text txtEnemyInfiniteHp;
    private Outline outEnemyPassive;
    private Outline outEnemyInfiniteHp;

    // 辅助监控面板
    private Text txtStats;
    private Text txtPlayerStatus;
    private Text txtToggleHint;
    private readonly List<GameObject> panels = new();
    private bool panelsVisible = true;

    private readonly StringBuilder sb = new(512);
    private float refreshAt;

    private static UnityEngine.Font cachedFont;

    // 半透明配色：面板淡、按键稍实，开关开启时为绿色
    private static readonly Color PanelBgColor = new(0.06f, 0.09f, 0.14f, 0.38f);
    private static readonly Color PanelBorderColor = new(0.96f, 0.80f, 0.28f, 0.35f);
    private static readonly Color BtnNormalColor = new(0.14f, 0.18f, 0.25f, 0.55f);
    private static readonly Color BtnNormalBorder = new(0.40f, 0.55f, 0.72f, 0.45f);
    private static readonly Color BtnActiveColor = new(0.06f, 0.52f, 0.32f, 0.65f);
    private static readonly Color BtnActiveBorder = new(1.0f, 0.90f, 0.35f, 0.55f);
    private static readonly Color BtnDangerColor = new(0.72f, 0.12f, 0.12f, 0.6f);
    private static readonly Color BtnDangerBorder = new(1.0f, 0.45f, 0.45f, 0.5f);
    private static readonly Color BtnActionColor = new(0.78f, 0.48f, 0.08f, 0.6f);
    private static readonly Color BtnActionBorder = new(1.0f, 0.88f, 0.35f, 0.5f);
    // 底部状态栏沿用原来的不透明配色
    private static readonly Color BarBgColor = new(0.08f, 0.11f, 0.17f, 1f);
    private static readonly Color BarBorderColor = new(0.96f, 0.80f, 0.28f, 1f);

    private static readonly Color GoldTitle = new(1f, 0.88f, 0.40f, 0.95f);
    private static readonly Color TextWhite = new(1f, 1f, 1f, 1f);
    private const float Row = 38f, BtnH = 32f;//按键行距与高度
    private const float Header = 36f;//面板标题高度

    public static UnityEngine.Font GetSafeFont()
    {
        if (cachedFont != null) return cachedFont;
        try
        {
            cachedFont = UnityEngine.Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial Unicode MS", "Arial" }, 20);
        }
        catch { }
        if (cachedFont == null)
        {
            cachedFont = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
        }
        if (cachedFont == null)
        {
            cachedFont = Resources.GetBuiltinResource<UnityEngine.Font>("Arial.ttf");
        }
        if (cachedFont == null)
        {
            UnityEngine.Font[] all = Resources.FindObjectsOfTypeAll<UnityEngine.Font>();
            if (all != null && all.Length > 0) cachedFont = all[0];
        }
        return cachedFont;
    }

    private RectTransform CreatePanel(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool faded = true)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        go.GetComponent<Image>().color = faded ? PanelBgColor : BarBgColor;
        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = faded ? PanelBorderColor : BarBorderColor;
        outline.effectDistance = faded ? new Vector2(1.5f, -1.5f) : new Vector2(3f, -3f);

        panels.Add(go);
        return rt;
    }

    private Text CreateText(Transform parent, string content, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
        int fontSize = 18, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null, bool bestFit = false)
    {
        GameObject go = new("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text txt = go.GetComponent<Text>();
        txt.font = GetSafeFont() ?? Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = align;
        txt.color = color ?? TextWhite;
        txt.supportRichText = true;
        txt.raycastTarget = false;
        if (bestFit)//在框内取能放下的最大字号，上限为 fontSize
        {
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 10;
            txt.resizeTextMaxSize = fontSize;
        }

        go.AddComponent<RebirthText>();
        return txt;
    }

    private Button CreateButton(Transform parent, string title, Vector2 pos, Vector2 size, UnityAction onClick, Color bgColor, Color borderColor,
        out Text buttonText, out Outline buttonOutline, int fontSize = 22)
    {
        GameObject go = new("Btn_" + title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = bgColor;

        buttonOutline = go.GetComponent<Outline>();
        buttonOutline.effectColor = borderColor;
        buttonOutline.effectDistance = new Vector2(1f, -1f);

        Button btn = go.GetComponent<Button>();
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        //文字区比按键四周略缩进，字号取能放下的最大值
        buttonText = CreateText(go.transform, title, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -4f),
            fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, null, true);
        return btn;
    }

    private static Vector2 RowPos(int row, float x = 0f) => new(x, -Header - row * Row);

    private Text Title(RectTransform panel, string text, float width) =>
        CreateText(panel, text, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f),
            new Vector2(width, 28f), 22, FontStyle.Bold, TextAnchor.MiddleCenter, GoldTitle, true);

    private void Awake()
    {
        if (manager == null) manager = FindAnyObjectByType<TrainingManager>();
        if (hitboxes == null) hitboxes = FindAnyObjectByType<TrainingHitboxDebug>();
        Text t;
        Outline o;
        float panelHeight = Header + 5 * Row + 6f;

        // 1. 左上：角色开关
        const float leftWidth = 230f, leftInner = 214f;
        RectTransform leftPanel = CreatePanel("PlayerControlPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(leftWidth, panelHeight));
        Title(leftPanel, "角色", leftInner);
        btnInvulnerable = CreateButton(leftPanel, "无敌", RowPos(0), new Vector2(leftInner, BtnH),
            () => manager.SetInvulnerable(!manager.Invulnerable), BtnNormalColor, BtnNormalBorder, out txtInvulnerable, out outInvulnerable);
        btnNoCooldown = CreateButton(leftPanel, "技能/换人无冷却", RowPos(1), new Vector2(leftInner, BtnH),
            () => manager.SetNoCooldown(!manager.NoCooldown), BtnNormalColor, BtnNormalBorder, out txtNoCooldown, out outNoCooldown);
        btnInfiniteResources = CreateButton(leftPanel, "无消耗", RowPos(2), new Vector2(leftInner, BtnH),
            () => manager.SetInfiniteResources(!manager.InfiniteResources), BtnNormalColor, BtnNormalBorder, out txtInfiniteResources, out outInfiniteResources);
        btnBoost = CreateButton(leftPanel, "无双常驻", RowPos(3), new Vector2(leftInner, BtnH),
            () => manager.SetInfiniteBoost(!manager.InfiniteBoost), BtnNormalColor, BtnNormalBorder, out txtBoost, out outBoost);
        CreateButton(leftPanel, "满状态重置", RowPos(4), new Vector2(leftInner, BtnH), manager.ResetPlayer, BtnNormalColor, BtnNormalBorder, out t, out o);

        // 2. 右上：敌人控件
        const float rightWidth = 280f, inner = 264f, half = 130f;
        RectTransform rightPanel = CreatePanel("EnemyControlPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(rightWidth, panelHeight));
        Title(rightPanel, "敌人", inner);
        CreateButton(rightPanel, "◀", RowPos(0, -115f), new Vector2(34f, BtnH), manager.PreviousEnemy, BtnNormalColor, BtnNormalBorder, out t, out o);
        txtEnemyName = CreateText(rightPanel, "加载中...", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), RowPos(0),
            new Vector2(190f, BtnH), 22, FontStyle.Bold, TextAnchor.MiddleCenter, GoldTitle, true);
        CreateButton(rightPanel, "▶", RowPos(0, 115f), new Vector2(34f, BtnH), manager.NextEnemy, BtnNormalColor, BtnNormalBorder, out t, out o);
        CreateButton(rightPanel, "出生当前怪", RowPos(1, -67f), new Vector2(half, BtnH), manager.SpawnSelectedEnemy, BtnActionColor, BtnActionBorder, out t, out o);
        CreateButton(rightPanel, "敌人清空", RowPos(1, 67f), new Vector2(half, BtnH), manager.ClearAllEnemies, BtnDangerColor, BtnDangerBorder, out t, out o);
        btnEnemyPassive = CreateButton(rightPanel, "敌人待机", RowPos(2), new Vector2(inner, BtnH),
            manager.ToggleAI, BtnNormalColor, BtnNormalBorder, out txtEnemyPassive, out outEnemyPassive);
        btnEnemyInfiniteHp = CreateButton(rightPanel, "敌人血量无限", RowPos(3), new Vector2(inner, BtnH),
            () => manager.SetInfiniteHealth(!manager.InfiniteHealth), BtnActiveColor, BtnActiveBorder, out txtEnemyInfiniteHp, out outEnemyInfiniteHp);
        CreateButton(rightPanel, "碰撞盒显示", RowPos(4, -67f), new Vector2(half, BtnH),
            () => { if (hitboxes != null) hitboxes.Visible = !hitboxes.Visible; }, BtnNormalColor, BtnNormalBorder, out t, out o);
        CreateButton(rightPanel, "返回地图", RowPos(4, 67f), new Vector2(half, BtnH), manager.ExitTraining, BtnNormalColor, BtnNormalBorder, out t, out o);

        // 3. 右侧：伤害统计，紧贴敌人面板下方
        RectTransform statsPanel = CreatePanel("StatsPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f - panelHeight - 8f), new Vector2(rightWidth, 196f));
        Title(statsPanel, "伤害统计", inner);
        txtStats = CreateText(statsPanel, "等待伤害输入...", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(-16f, -40f),
            20, FontStyle.Bold, TextAnchor.UpperLeft, TextWhite, true);
        CreateButton(statsPanel, "重置统计", new Vector2(88f, -6f), new Vector2(86f, 24f), manager.Statistics.Reset, BtnNormalColor, BtnNormalBorder, out t, out o, 16);

        // 4. 底部：角色状态栏（保持原样）
        RectTransform playerBar = CreatePanel("PlayerBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1180f, 115f), false);
        txtPlayerStatus = CreateText(playerBar, "玩家状态加载中...", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(-30f, -12f), 20, FontStyle.Bold, TextAnchor.MiddleCenter, TextWhite);

        // 5. 左下角快捷键提示（面板隐藏后也保留，方便再次打开）
        txtToggleHint = CreateText(transform, "", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(14f, 8f), new Vector2(220f, 24f),
            16, FontStyle.Bold, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.55f));
        RefreshToggleHint();
    }

    private void SetPanelsVisible(bool visible)
    {
        panelsVisible = visible;
        foreach (GameObject panel in panels)
            if (panel != null) panel.SetActive(visible);
        RefreshToggleHint();
    }

    private void RefreshToggleHint()
    {
        if (txtToggleHint != null) txtToggleHint.text = $"[{toggleKey}] {(panelsVisible ? "隐藏面板" : "显示面板")}";
    }

    private void UpdateSwitchVisual(Button btn, Text label, Outline outline, string title, bool isOn)
    {
        if (label != null)
            label.text = $"{title}  {(isOn ? "<color=#FFEE44>开</color>" : "<color=#99AAB8>关</color>")}";
        if (btn != null)
        {
            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = isOn ? BtnActiveColor : BtnNormalColor;
        }
        if (outline != null) outline.effectColor = isOn ? BtnActiveBorder : BtnNormalBorder;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) SetPanelsVisible(!panelsVisible);

        if (manager == null) manager = FindAnyObjectByType<TrainingManager>();
        if (!panelsVisible || manager == null || !manager.Ready || Time.unscaledTime < refreshAt) return;//隐藏时不刷新文字
        refreshAt = Time.unscaledTime + 0.08f;

        // 1. 左上角色开关状态
        UpdateSwitchVisual(btnInvulnerable, txtInvulnerable, outInvulnerable, "无敌", manager.Invulnerable);
        UpdateSwitchVisual(btnNoCooldown, txtNoCooldown, outNoCooldown, "技能/换人无冷却", manager.NoCooldown);
        UpdateSwitchVisual(btnInfiniteResources, txtInfiniteResources, outInfiniteResources, "无消耗", manager.InfiniteResources);
        UpdateSwitchVisual(btnBoost, txtBoost, outBoost, "无双常驻", manager.InfiniteBoost);

        // 2. 右上敌人控件状态
        if (manager.Catalog != null && manager.Catalog.Count > 0)
        {
            int idx = Mathf.Clamp(manager.Selected, 0, manager.Catalog.Count - 1);
            txtEnemyName.text = $"[{idx + 1}/{manager.Catalog.Count}] {manager.Catalog.enemies[idx].displayName}";
        }

        UpdateSwitchVisual(btnEnemyPassive, txtEnemyPassive, outEnemyPassive, "敌人待机", manager.EnemyPassive);
        UpdateSwitchVisual(btnEnemyInfiniteHp, txtEnemyInfiniteHp, outEnemyInfiniteHp, "敌人血量无限", manager.InfiniteHealth);

        // 3. 统计面板刷新
        TrainingStatistics s = manager.Statistics;
        sb.Clear();
        sb.Append("秒伤 DPS: <color=#FFDD44>").Append(s.Dps.ToString("0.0")).Append("</color>\n")
          .Append("总伤害: <color=#FF9933>").Append(s.TotalDamage.ToString("0.0")).Append("</color>\n")
          .Append("命中: <color=#33FF88>").Append(s.Hits).Append("</color>  暴击: <color=#FF4444>").Append(s.CriticalHits).Append("</color>\n")
          .Append("暴击率: <color=#FFAA00>").Append((s.CriticalRate * 100f).ToString("0.0")).Append("%</color>  最高: <color=#FF6633>").Append(s.MaximumHit.ToString("0.0")).Append("</color>\n")
          .Append("击杀用时: ").Append(s.KillTime < 0f ? "--" : s.KillTime.ToString("0.00") + "s").Append('\n');

        if (manager.Target != null && manager.TargetHealth != null)
        {
            sb.Append("目标生命: <color=#33FF66>").Append(manager.TargetHealth.CurBlood.ToString("0.0"))
              .Append("</color> / ").Append(manager.TargetHealth.BeginBlood.ToString("0.0"));
        }
        else
        {
            sb.Append("目标生命: <color=#AAAAAA><场上无敌人></color>");
        }
        txtStats.text = sb.ToString();

        // 4. 底部角色状态栏（内容与原来一致）
        PlayerController p = manager.Players != null ? manager.Players.CurrentPlayer : null;
        if (p == null) return;

        PlayerResources r = p.GetComponent<PlayerResources>();
        PlayerCombat c = p.GetComponent<PlayerCombat>();
        Health h = p.GetComponent<Health>();
        PlayerBoost pb = p.GetComponent<PlayerBoost>();

        sb.Clear();
        sb.Append("<color=#FFD700>【出战角色】</color> ").Append(r != null ? r.Id.ToString() : "Player")
          .Append("   |   <color=#33FF88>生命:</color> ").Append(h != null ? h.CurBlood.ToString("0") : "0")
          .Append("   |   <color=#33CCFF>法力:</color> ").Append(r != null ? r.CurrentMana.ToString("0") : "0")
          .Append("   |   <color=#FFAA00>气力:</color> ").Append(r != null ? r.CurrentVigor.ToString("0") : "0");

        bool boostActive = manager.InfiniteBoost || (pb != null && pb.Active);
        if (boostActive && r != null)
        {
            string effectDesc = r.Id switch
            {
                PlayerId.WuKong => "暴击率+30% | 移速+30% | 气力锁定 | 残影拖尾",
                PlayerId.BaJie => "承受伤害-50% | 移速+30% | 气力锁定 | 残影拖尾",
                PlayerId.Sha => "普通霸体免硬直 | 移速+30% | 气力锁定 | 残影拖尾",
                PlayerId.Tang => "攻击力+20% | 移速+30% | 气力锁定 | 残影拖尾",
                _ => "移速+30% | 气力锁定 | 残影拖尾"
            };
            sb.Append("   |   <color=#FFEE44>【无双已激活】</color><color=#00FF66>").Append(effectDesc).Append("</color>");
        }
        else
        {
            sb.Append("   |   <color=#99AAB8>【无双未激活】(空格爆发/左上开启)</color>");
        }
        sb.Append("\n");

        if (c != null)
        {
            for (int k = 1; k <= c.SkillCount; k++)
            {
                float cd = c.GetSkillCooldownRemaining(k);
                if (cd > 0f) sb.Append("技").Append(k).Append(": <color=#FF5555>[").Append(cd.ToString("0.0")).Append("s]</color>   ");
                else sb.Append("技").Append(k).Append(": <color=#33FF66>[就绪]</color>   ");
            }
        }
        sb.Append(manager.NoCooldown ? " |   [1/2/3/4] 切人<color=#33FF66>[免CD]</color>   [U/I/O] 释放技能   [空格] 爆发无双" : " |   [1/2/3/4] 切换角色   [U/I/O] 释放技能   [空格] 爆发无双");
        txtPlayerStatus.text = sb.ToString();
    }
}

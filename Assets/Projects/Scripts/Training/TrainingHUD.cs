using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 训练场控制面板：
/// - 按钮大尺寸、高对比度、100% 完全不透明实色底色，拒绝半透明；
/// - 带有醒目金边/红边/绿边外框（Outline），无论任何场景背景均一目了然；
/// - 底部角色状态与技能监控字号大幅加大（20~22px Bold），色彩鲜明；
/// - 严格遵守左上角色开关、右上敌人控件、右中伤害统计、底部状态栏布局。
/// </summary>
public sealed class TrainingHUD : MonoBehaviour
{
    [SerializeField] private TrainingManager manager;
    [SerializeField] private TrainingHitboxDebug hitboxes;

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
    private Button btnSpawnEnemy;
    private Button btnClearEnemies;
    private Button btnEnemyPassive;
    private Button btnEnemyInfiniteHp;
    private Text txtEnemyPassive;
    private Text txtEnemyInfiniteHp;
    private Outline outEnemyPassive;
    private Outline outEnemyInfiniteHp;

    // 辅助监控面板
    private Text txtStats;
    private Text txtPlayerStatus;

    private readonly StringBuilder sb = new(512);
    private float refreshAt;

    private static UnityEngine.Font cachedFont;

    // 经典不透明配色方案（Alpha = 1.0f 纯实色）
    private static readonly Color PanelBgColor = new(0.08f, 0.11f, 0.17f, 1f);       // 深墨青石底板（100%实色）
    private static readonly Color PanelBorderColor = new(0.96f, 0.80f, 0.28f, 1f);   // 辉光灿金双线框
    private static readonly Color BtnNormalColor = new(0.14f, 0.18f, 0.25f, 1f);     // 常态石青实色按键
    private static readonly Color BtnNormalBorder = new(0.40f, 0.55f, 0.72f, 1f);    // 银蓝边框
    private static readonly Color BtnActiveColor = new(0.06f, 0.52f, 0.32f, 1f);     // 浓厚翡翠绿开启按键
    private static readonly Color BtnActiveBorder = new(1.0f, 0.90f, 0.35f, 1f);     // 金光亮线框
    private static readonly Color BtnDangerColor = new(0.72f, 0.12f, 0.12f, 1f);     // 浓厚朱砂红危险按键
    private static readonly Color BtnDangerBorder = new(1.0f, 0.45f, 0.45f, 1f);     // 鲜红亮线框
    private static readonly Color BtnActionColor = new(0.78f, 0.48f, 0.08f, 1f);     // 琥珀流金动作按键
    private static readonly Color BtnActionBorder = new(1.0f, 0.88f, 0.35f, 1f);     // 金光亮线框

    private static readonly Color GoldTitle = new(1f, 0.88f, 0.40f, 1f);
    private static readonly Color TextWhite = new(1f, 1f, 1f, 1f);

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

    private RectTransform CreatePanel(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = PanelBgColor;

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = PanelBorderColor;
        outline.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(4f, -4f);

        return rt;
    }

    private Text CreateText(Transform parent, string content, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize = 18, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
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

        go.AddComponent<RebirthText>();
        return txt;
    }

    private Button CreateButton(Transform parent, string title, Vector2 pos, Vector2 size, UnityAction onClick, Color bgColor, Color borderColor, out Text buttonText, out Outline buttonOutline, int fontSize = 18)
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
        buttonOutline.effectDistance = new Vector2(2f, -2f);

        Button btn = go.GetComponent<Button>();
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        buttonText = CreateText(go.transform, title, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
        return btn;
    }

    private void Awake()
    {
        if (manager == null) manager = FindAnyObjectByType<TrainingManager>();
        if (hitboxes == null) hitboxes = FindAnyObjectByType<TrainingHitboxDebug>();

        // ==========================================
        // 1. 左上方屏幕：角色相关开关（大按钮、清晰醒目、实色无透明）
        // ==========================================
        RectTransform leftPanel = CreatePanel("PlayerControlPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(310f, 345f));
        CreateText(leftPanel, "✦ 角色模式开关 ✦", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(290f, 30f), 20, FontStyle.Bold, TextAnchor.MiddleCenter, GoldTitle);

        btnInvulnerable = CreateButton(leftPanel, "是否无敌: 关", new Vector2(0f, -48f), new Vector2(275f, 48f),
            () => manager.SetInvulnerable(!manager.Invulnerable), BtnNormalColor, BtnNormalBorder, out txtInvulnerable, out outInvulnerable, 18);

        btnNoCooldown = CreateButton(leftPanel, "技能与换人无冷却: 关", new Vector2(0f, -104f), new Vector2(275f, 48f),
            () => manager.SetNoCooldown(!manager.NoCooldown), BtnNormalColor, BtnNormalBorder, out txtNoCooldown, out outNoCooldown, 17);

        btnInfiniteResources = CreateButton(leftPanel, "是否无消耗: 关", new Vector2(0f, -160f), new Vector2(275f, 48f),
            () => manager.SetInfiniteResources(!manager.InfiniteResources), BtnNormalColor, BtnNormalBorder, out txtInfiniteResources, out outInfiniteResources, 18);

        btnBoost = CreateButton(leftPanel, "角色无双状态: 关", new Vector2(0f, -216f), new Vector2(275f, 48f),
            () => manager.SetInfiniteBoost(!manager.InfiniteBoost), BtnNormalColor, BtnNormalBorder, out txtBoost, out outBoost, 18);

        Text txtReset;
        Outline outReset;
        CreateButton(leftPanel, "↺ 满状态重置角色", new Vector2(0f, -274f), new Vector2(275f, 44f),
            manager.ResetPlayer, BtnNormalColor, BtnNormalBorder, out txtReset, out outReset, 17);

        // ==========================================
        // 2. 右上方屏幕：敌人相关控件（大尺寸、大间距、红金对比）
        // ==========================================
        RectTransform rightPanel = CreatePanel("EnemyControlPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(390f, 350f));
        CreateText(rightPanel, "✦ 敌人生成与控制 ✦", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(370f, 30f), 20, FontStyle.Bold, TextAnchor.MiddleCenter, GoldTitle);

        // 敌人选择行（大箭头 ◀、居中大字、大箭头 ▶）
        Text prevTxt, nextTxt;
        Outline prevOut, nextOut;
        CreateButton(rightPanel, "◀", new Vector2(-155f, -48f), new Vector2(56f, 48f), manager.PreviousEnemy, BtnNormalColor, BtnNormalBorder, out prevTxt, out prevOut, 22);
        txtEnemyName = CreateText(rightPanel, "加载中...", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(240f, 48f), 18, FontStyle.Bold, TextAnchor.MiddleCenter, GoldTitle);
        CreateButton(rightPanel, "▶", new Vector2(155f, -48f), new Vector2(56f, 48f), manager.NextEnemy, BtnNormalColor, BtnNormalBorder, out nextTxt, out nextOut, 22);

        // 出生当前怪 & 敌人清空（大尺寸动作按钮）
        Text spawnTxt, clearTxt;
        Outline spawnOut, clearOut;
        btnSpawnEnemy = CreateButton(rightPanel, "⚡ 出生当前怪", new Vector2(-90f, -104f), new Vector2(174f, 48f),
            manager.SpawnSelectedEnemy, BtnActionColor, BtnActionBorder, out spawnTxt, out spawnOut, 17);

        btnClearEnemies = CreateButton(rightPanel, "💥 敌人清空", new Vector2(90f, -104f), new Vector2(174f, 48f),
            manager.ClearAllEnemies, BtnDangerColor, BtnDangerBorder, out clearTxt, out clearOut, 18);

        // 敌人状态大开关
        btnEnemyPassive = CreateButton(rightPanel, "敌人是否保持待机: 关", new Vector2(0f, -160f), new Vector2(354f, 48f),
            manager.ToggleAI, BtnNormalColor, BtnNormalBorder, out txtEnemyPassive, out outEnemyPassive, 18);

        btnEnemyInfiniteHp = CreateButton(rightPanel, "敌人是否血量无限: 开", new Vector2(0f, -216f), new Vector2(354f, 48f),
            () => manager.SetInfiniteHealth(!manager.InfiniteHealth), BtnActiveColor, BtnActiveBorder, out txtEnemyInfiniteHp, out outEnemyInfiniteHp, 18);

        // 辅助工具（判定框 & 返回地图）
        Text hitboxesTxt, returnTxt;
        Outline hitboxesOut, returnOut;
        CreateButton(rightPanel, "🎯 碰撞盒显示", new Vector2(-90f, -280f), new Vector2(174f, 44f),
            () => { if (hitboxes != null) hitboxes.Visible = !hitboxes.Visible; }, BtnNormalColor, BtnNormalBorder, out hitboxesTxt, out hitboxesOut, 16);

        CreateButton(rightPanel, "🚪 返回地图", new Vector2(90f, -280f), new Vector2(174f, 44f),
            manager.ExitTraining, BtnNormalColor, BtnNormalBorder, out returnTxt, out returnOut, 16);

        // ==========================================
        // 3. 右中下方：伤害监控面板（大号字体、直观清晰）
        // ==========================================
        RectTransform statsPanel = CreatePanel("StatsPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -385f), new Vector2(390f, 245f));
        CreateText(statsPanel, "✦ 伤害数据监控 ✦", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(370f, 28f), 18, FontStyle.Bold, TextAnchor.MiddleCenter, GoldTitle);
        txtStats = CreateText(statsPanel, "等待伤害输入...", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(-28f, -50f), 17, FontStyle.Bold, TextAnchor.UpperLeft, TextWhite);

        Text resetStatsTxt;
        Outline resetStatsOut;
        CreateButton(statsPanel, "重置统计", new Vector2(125f, -200f), new Vector2(110f, 34f), manager.Statistics.Reset, BtnNormalColor, BtnNormalBorder, out resetStatsTxt, out resetStatsOut, 15);

        // ==========================================
        // 4. 底部中央：大尺寸角色状态监控栏（大字体、色彩鲜明、一目了然）
        // ==========================================
        RectTransform playerBar = CreatePanel("PlayerBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1180f, 115f));
        txtPlayerStatus = CreateText(playerBar, "玩家状态加载中...", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-30f, -12f), 20, FontStyle.Bold, TextAnchor.MiddleCenter, TextWhite);
    }

    private void UpdateSwitchVisual(Button btn, Text label, Outline outline, string title, bool isOn)
    {
        if (label != null)
        {
            label.text = $"{title}: {(isOn ? "<color=#FFEE44>【✔ 已开启】</color>" : "<color=#99AAB8>【● 已关闭】</color>")}";
        }
        if (btn != null)
        {
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isOn ? BtnActiveColor : BtnNormalColor;
            }
        }
        if (outline != null)
        {
            outline.effectColor = isOn ? BtnActiveBorder : BtnNormalBorder;
        }
    }

    private void Update()
    {
        if (manager == null) manager = FindAnyObjectByType<TrainingManager>();
        if (manager == null || !manager.Ready || Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.08f;

        // 1. 左上角色开关状态
        UpdateSwitchVisual(btnInvulnerable, txtInvulnerable, outInvulnerable, "是否无敌", manager.Invulnerable);
        UpdateSwitchVisual(btnNoCooldown, txtNoCooldown, outNoCooldown, "技能与换人无冷却", manager.NoCooldown);
        UpdateSwitchVisual(btnInfiniteResources, txtInfiniteResources, outInfiniteResources, "是否无消耗", manager.InfiniteResources);
        UpdateSwitchVisual(btnBoost, txtBoost, outBoost, "角色无双状态", manager.InfiniteBoost);

        // 2. 右上敌人控件状态
        if (manager.Catalog != null && manager.Catalog.Count > 0)
        {
            int idx = Mathf.Clamp(manager.Selected, 0, manager.Catalog.Count - 1);
            txtEnemyName.text = $"[{idx + 1}/{manager.Catalog.Count}] {manager.Catalog.enemies[idx].displayName}";
        }

        UpdateSwitchVisual(btnEnemyPassive, txtEnemyPassive, outEnemyPassive, "敌人保持待机", manager.EnemyPassive);
        UpdateSwitchVisual(btnEnemyInfiniteHp, txtEnemyInfiniteHp, outEnemyInfiniteHp, "敌人血量无限", manager.InfiniteHealth);

        // 3. 统计面板刷新（大号字体加粗呈现）
        TrainingStatistics s = manager.Statistics;
        sb.Clear();
        sb.Append("实时秒伤 (DPS): <color=#FFDD44>").Append(s.Dps.ToString("0.0")).Append("</color>\n")
          .Append("累计总伤害: <color=#FF9933>").Append(s.TotalDamage.ToString("0.0")).Append("</color>\n")
          .Append("连击数: <color=#33FF88>").Append(s.Hits).Append("</color> 次  (暴击: <color=#FF4444>").Append(s.CriticalHits).Append("</color> 次)\n")
          .Append("暴击率: <color=#FFAA00>").Append((s.CriticalRate * 100f).ToString("0.0")).Append("%</color>  最高单次: <color=#FF6633>").Append(s.MaximumHit.ToString("0.0")).Append("</color>\n")
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

        // 4. 底部角色状态栏（超大号高亮字号）
        PlayerController p = manager.Players != null ? manager.Players.CurrentPlayer : null;
        if (p == null) return;

        PlayerResources r = p.GetComponent<PlayerResources>();
        PlayerCombat c = p.GetComponent<PlayerCombat>();
        Health h = p.GetComponent<Health>();
        PlayerBoost pb = p.GetComponent<PlayerBoost>();

        sb.Clear();
        // 第一行：角色核心属性大字
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

        // 第二行：技能状态与快捷键提示
        if (c != null)
        {
            for (int k = 1; k <= c.SkillCount; k++)
            {
                float cd = c.GetSkillCooldownRemaining(k);
                if (cd > 0f)
                {
                    sb.Append("技").Append(k).Append(": <color=#FF5555>[").Append(cd.ToString("0.0")).Append("s]</color>   ");
                }
                else
                {
                    sb.Append("技").Append(k).Append(": <color=#33FF66>[就绪]</color>   ");
                }
            }
        }
        sb.Append(manager.NoCooldown ? " |   [1/2/3/4] 切人<color=#33FF66>[免CD]</color>   [U/I/O] 释放技能   [空格] 爆发无双" : " |   [1/2/3/4] 切换角色   [U/I/O] 释放技能   [空格] 爆发无双");
        txtPlayerStatus.text = sb.ToString();
    }
}

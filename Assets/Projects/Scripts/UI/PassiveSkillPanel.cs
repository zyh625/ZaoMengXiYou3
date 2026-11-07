using System;
using UnityEngine;
using UnityEngine.UI;

//技能界面里的被动技能页：七行属性，每行显示当前加成、等级、升级花费和升级按钮。
//由 SkillManager 在切到被动页、或切换角色时调用 Show。
public sealed class PassiveSkillPanel : MonoBehaviour
{
    [Serializable]
    public sealed class Row
    {
        public SpriteNumber value;//当前加成
        public SpriteNumber level;
        public SpriteNumber cost;//升级到下一级所需金币
        public GameObject costGroup;//满级时连同金币图标一起隐藏
        public Button upgrade;
    }

    [SerializeField] private Row[] rows = new Row[PassiveSkills.Count];//顺序：血量上限、蓝量上限、气力上限、暴击率、每秒回血、每秒回蓝、气力恢复
    private PlayerId shown;
    private bool wired;

    public void Show(PlayerId id)
    {
        shown = id;
        Wire();
        Refresh();
    }

    private void Wire()
    {
        if (wired) return;
        wired = true;
        for (int i = 0; i < rows.Length; i++)
        {
            int stat = i;
            if (rows[i]?.upgrade != null) rows[i].upgrade.onClick.AddListener(() => Upgrade((PassiveStat)stat));
        }
    }

    private void OnEnable()
    {
        PassiveSkills.Changed += Refresh;
        GameSession.EnsureSession().DataChanged += Refresh;//拾取金币后按钮可用状态立即更新
        Wire();
        Refresh();
    }

    private void OnDisable()
    {
        PassiveSkills.Changed -= Refresh;
        if (GameSession.instance != null) GameSession.instance.DataChanged -= Refresh;
    }

    private void Upgrade(PassiveStat stat)
    {
        PassiveSkills.TryUpgrade(GameSession.EnsureSession().EnsureRuntimeData(), shown, stat);
        Refresh();
    }

    private void Refresh()
    {
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        PlayerData player = data.GetPlayer(shown);
        for (int i = 0; i < rows.Length && i < PassiveSkills.Count; i++)
        {
            Row row = rows[i];
            if (row == null) continue;
            var stat = (PassiveStat)i;
            int level = PassiveSkills.Level(player, stat);
            float bonus = PassiveSkills.Bonus(shown, stat, level);
            row.value?.SetText(stat == PassiveStat.Crit ? $"{Round(bonus * 100f)}%" : Round(bonus).ToString());
            row.level?.SetValue(level);
            int cost = PassiveSkills.Cost(shown, stat, level);
            bool maxed = cost <= 0;
            if (row.costGroup != null) row.costGroup.SetActive(!maxed);
            if (!maxed) row.cost?.SetValue(cost);
            if (row.upgrade != null)
            {
                bool affordable = !maxed && data.gold >= cost;
                row.upgrade.interactable = affordable;
                if (row.upgrade.targetGraphic != null)//买不起或满级时按钮变暗
                    row.upgrade.targetGraphic.color = affordable ? Color.white : new Color(0.45f, 0.45f, 0.45f, maxed ? 0.5f : 1f);
            }
        }
    }

    private static int Round(float value) => Mathf.FloorToInt(value + 0.5f);//0.5向上取整，避免银行家舍入
}

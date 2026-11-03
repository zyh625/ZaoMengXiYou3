using UnityEngine;
using UnityEngine.UI;

//增长天王的寒霜槽，血量、初始隐藏与死亡定格沿用BossUI。
public class SouthBossUI : BossUI
{
    [SerializeField] private Image specialState;//复用当前场景的填充图，不改变UI布局。
    private SouthKingCombat combat;
    public override void PrepareForBattle()
    {
        base.PrepareForBattle();
        if (specialState != null) specialState.fillAmount = 0f;
    }
    public override void BindBoss(Health health)
    {
        base.BindBoss(health);
        combat = health != null ? health.GetComponent<SouthKingCombat>() : null;
        if (combat != null) combat.FrostChanged += Refresh;
        Refresh();
    }
    private void Refresh()
    {
        if (!frozen && specialState != null) specialState.fillAmount = combat != null ? combat.Frost / combat.MaxFrost : 0f;
    }
    protected override void UnbindBoss()
    {
        if (combat != null) combat.FrostChanged -= Refresh;
        combat = null; base.UnbindBoss();
    }
}

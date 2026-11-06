using UnityEngine;
using UnityEngine.UI;

public sealed class WuYingWangBossUI : BossUI
{
    [SerializeField] private Image specialState;
    [SerializeField] private Color flyingColor = new(0.52f, 0.83f, 1f);
    private WuYingWangCombat combat;
    private Color rageColor;

    public override void PrepareForBattle()
    {
        base.PrepareForBattle();
        rageColor = specialState.color;
        RefreshRage();
    }

    public override void BindBoss(Health health)
    {
        base.BindBoss(health);
        combat = health != null ? health.GetComponent<WuYingWangCombat>() : null;
        if (combat != null) combat.RageChanged += RefreshRage;
        RefreshRage();
    }

    private void RefreshRage()
    {
        if (frozen || specialState == null) return;
        specialState.fillAmount = combat != null ? Mathf.Clamp01(combat.Rage / combat.MaxRage) : 0f;
        specialState.color = combat != null && combat.IsFlying ? flyingColor : rageColor;
    }

    protected override void UnbindBoss()
    {
        if (combat != null) combat.RageChanged -= RefreshRage;
        combat = null;
        base.UnbindBoss();
    }
}

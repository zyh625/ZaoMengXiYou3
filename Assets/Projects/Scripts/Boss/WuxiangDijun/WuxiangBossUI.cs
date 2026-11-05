using UnityEngine;
using UnityEngine.UI;

public sealed class WuxiangBossUI : BossUI
{
    [SerializeField] private Image sealFill, maskBreakFill;
    private WuxiangCombat combat;

    public override void BindBoss(Health health)
    {
        base.BindBoss(health);
        combat = health != null ? health.GetComponent<WuxiangCombat>() : null;
        Refresh();
    }

    private void LateUpdate()
    {
        if (!frozen) Refresh();
    }

    private void Refresh()
    {
        if (sealFill != null) sealFill.fillAmount = combat != null ? combat.SealFraction : 0f;
        if (maskBreakFill != null) maskBreakFill.fillAmount = combat != null ? combat.MaskBreakFraction : 0f;
    }

    protected override void UnbindBoss()
    {
        combat = null;
        base.UnbindBoss();
        Refresh();
    }
}

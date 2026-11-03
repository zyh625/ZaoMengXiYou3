using UnityEngine;
using UnityEngine.UI;

public sealed class RedFurnaceLordUI : BossUI
{
    [SerializeField] private Image specialState;
    [SerializeField] private Color exposedColor = new(1f, .24f, .06f);
    private RedFurnaceLordCombat combat;
    public override void PrepareForBattle() { base.PrepareForBattle(); Refresh(); }
    public override void BindBoss(Health health)
    {
        base.BindBoss(health);
        combat = health != null ? health.GetComponent<RedFurnaceLordCombat>() : null;
        if (combat != null) combat.CoreChanged += Refresh;
        Refresh();
    }
    private void Refresh()
    {
        if (specialState == null) return;
        specialState.fillAmount = combat != null && combat.IsCoreOpen
            ? Mathf.Clamp01(combat.CoreRemaining / Mathf.Max(.01f, combat.CoreDuration)) : 0f;
        specialState.color = exposedColor;
    }
    protected override void UnbindBoss()
    {
        if (combat != null) combat.CoreChanged -= Refresh;
        combat = null; base.UnbindBoss(); Refresh();
    }
}

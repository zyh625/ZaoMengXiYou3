using UnityEngine;
using UnityEngine.UI;

//第二关专属的特殊状态条：只读取巨灵神怒气，通用血量与死亡定格仍由BossUI管理。
public class TitanBossUI : BossUI
{
    [SerializeField] private Image specialState;//SpecialState下面已有的填充图片
    private TitanCombat combat;//只订阅当前Boss，不逐帧查找或计算怒气

    public override void PrepareForBattle()
    {
        base.PrepareForBattle();
        if (specialState != null) specialState.fillAmount = 0f;
    }

    public override void BindBoss(Health health)
    {
        base.BindBoss(health);
        combat = health != null ? health.GetComponent<TitanCombat>() : null;
        if (combat != null) combat.RageChanged += RefreshSpecialState;
        RefreshSpecialState();
    }

    private void RefreshSpecialState()//无双持续期间怒气由Combat锁满，UI只反映当前数值
    {
        if (frozen || specialState == null) return;
        specialState.fillAmount = combat != null && combat.MaxRage > 0f
            ? Mathf.Clamp01(combat.Rage / combat.MaxRage) : 0f;
    }

    protected override void UnbindBoss()
    {
        if (combat != null) combat.RageChanged -= RefreshSpecialState;
        combat = null;
        base.UnbindBoss();
    }
}

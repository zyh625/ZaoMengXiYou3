using UnityEngine;
using UnityEngine.UI;

//第三关专属急弦槽；血条、初始隐藏及死亡定格继续复用BossUI。
public class EastBossUI : BossUI
{
    [SerializeField] private Image specialState;//当前场景SpecialState下的填充图片，沿用已有布局和图案
    private EastCombat combat;//绑定关卡实际创建的持国天王，不保存场景外或模板引用

    public override void PrepareForBattle()
    {
        base.PrepareForBattle();
        if (specialState != null) specialState.fillAmount = 0f;
    }

    public override void BindBoss(Health health)
    {
        base.BindBoss(health);
        combat = health != null ? health.GetComponent<EastCombat>() : null;
        if (combat != null) combat.StringEnergyChanged += RefreshSpecialState;
        RefreshSpecialState();
    }

    private void RefreshSpecialState()//满能量期间Combat锁满，到期事件将槽归零，死亡后不再更新
    {
        if (frozen || specialState == null) return;
        specialState.fillAmount = combat != null && combat.MaxStringEnergy > 0f
            ? Mathf.Clamp01(combat.StringEnergy / combat.MaxStringEnergy) : 0f;
    }

    protected override void UnbindBoss()
    {
        if (combat != null) combat.StringEnergyChanged -= RefreshSpecialState;
        combat = null;
        base.UnbindBoss();
    }
}

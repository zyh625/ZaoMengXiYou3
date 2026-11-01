using UnityEngine;
using UnityEngine.UI;

public sealed class WestBossUI : BossUI
{
    [SerializeField] private Image specialState;
    private WestCombat combat;
    public override void PrepareForBattle(){base.PrepareForBattle();if(specialState!=null)specialState.fillAmount=0f;}
    public override void BindBoss(Health health)
    {
        base.BindBoss(health);combat=health!=null?health.GetComponent<WestCombat>():null;
        if(combat!=null)combat.WardChanged+=Refresh;Refresh();
    }
    private void Refresh()
    {
        if(frozen || specialState==null)return;
        specialState.fillAmount=combat==null?0f:combat.IsWarding?combat.WardRemaining/12f:combat.ExposureRemaining/3f;
        specialState.color=combat!=null && combat.Phase==WestCombat.WardPhase.Exposed?new Color(1f,0.75f,0.2f):new Color(1f,0.25f,0.12f);
    }
    protected override void UnbindBoss(){if(combat!=null)combat.WardChanged-=Refresh;combat=null;base.UnbindBoss();}
}

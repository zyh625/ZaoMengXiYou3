using UnityEngine;
using UnityEngine.UI;

public sealed class NorthBossUI : BossUI
{
    [SerializeField] private Image specialState;
    private NorthCombat combat;
    public override void PrepareForBattle(){base.PrepareForBattle();if(specialState!=null)specialState.fillAmount=0f;}
    public override void BindBoss(Health health)
    {base.BindBoss(health);combat=health!=null?health.GetComponent<NorthCombat>():null;if(combat!=null)combat.GuardChanged+=Refresh;Refresh();}
    private void Refresh()
    {
        if(frozen || specialState==null)return;
        specialState.fillAmount=combat==null?0f:combat.IsGuarding?combat.Durability/150f:combat.ExposureRemaining/3f;
        specialState.color=combat!=null && combat.Phase==NorthCombat.GuardPhase.Exposed?new Color(1f,0.75f,0.2f):new Color(0.25f,1f,0.65f);
    }
    protected override void UnbindBoss(){if(combat!=null)combat.GuardChanged-=Refresh;combat=null;base.UnbindBoss();}
}

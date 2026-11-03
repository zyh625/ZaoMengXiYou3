using UnityEngine;
using UnityEngine.UI;

public sealed class JiuyaoUI : BossUI
{
    [SerializeField] private Image specialState;
    private JiuyaoCombat combat;
    public override void BindBoss(Health health){base.BindBoss(health);combat=health!=null?health.GetComponent<JiuyaoCombat>():null;Refresh();}
    private void Update()=>Refresh();
    private void Refresh(){if(specialState!=null)specialState.fillAmount=combat!=null?combat.SpecialFraction:0;}
    protected override void UnbindBoss(){combat=null;base.UnbindBoss();Refresh();}
}

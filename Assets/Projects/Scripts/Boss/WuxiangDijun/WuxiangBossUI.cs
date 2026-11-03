using UnityEngine;
using UnityEngine.UI;

public sealed class WuxiangBossUI : BossUI
{
    [SerializeField] private Image specialState;
    private WuxiangCombat combat;
    public override void BindBoss(Health h){base.BindBoss(h);combat=h!=null?h.GetComponent<WuxiangCombat>():null;Refresh();}
    private void Update()=>Refresh();
    private void Refresh(){if(specialState!=null){specialState.fillAmount=combat!=null?combat.SpecialFraction:0;specialState.color=combat!=null&&combat.SecondStage?new Color(.9f,.15f,.25f):Color.yellow;}}
    protected override void UnbindBoss(){combat=null;base.UnbindBoss();Refresh();}
}

using UnityEngine;

public sealed class GreenHammerAttackBox : EnemyAttackBox
{
    public enum Shape { Sweep,Landing,GroundSegment }
    [SerializeField] private Shape shape;
    [SerializeField] private bool instantOnly;
    private readonly Collider2D[] hits=new Collider2D[24];
    private bool sampling;
    private EnemyCombat waveOwner;
    private float waveScale;

    public void BindWave(EnemyCombat source,float scale){waveOwner=source;waveScale=scale;}
    public void SampleNow()
    {
        ContactFilter2D filter=new();filter.SetLayerMask(1<<3);filter.useTriggers=true;
        sampling=true;
        int count=GetComponent<BoxCollider2D>().Overlap(filter,hits);
        for(int i=0;i<count;i++)TryAttack(hits[i]);
        sampling=false;
    }
    protected override void TryAttack(Collider2D hit)
    {
        if(instantOnly && !sampling)return;
        PlayerController player=hit!=null?hit.GetComponentInParent<PlayerController>():null;
        if(player==null || !player.isActiveAndEnabled || !player.CanReceiveHit)return;
        Health hp=player.GetComponent<Health>();if(hp==null || hp.Dead || hitEnemies.Contains(hp))return;
        if(shape==Shape.Landing)
        {
            //Box为半径3的外接矩形；按触发体最近点过滤四角，保留真正的圆形范围。
            Vector2 center=GetComponent<BoxCollider2D>().bounds.center;
            if((hit.ClosestPoint(center)-center).sqrMagnitude>9f)return;
        }
        if(waveOwner==null){base.TryAttack(hit);return;}
        if(!hitEnemies.Add(hp))return;
        float before=hp.CurBlood;
        hp.TakeDamage(new DamageInfo(waveOwner.transform,curAttack*waveScale,false,false,null,false,false,DamageKind.Skill));
        if(hp.CurBlood<before && waveOwner is GreenHammerCombat combat)combat.ReportSegmentHit();
    }
    private void OnDisable(){waveOwner=null;sampling=false;}
}

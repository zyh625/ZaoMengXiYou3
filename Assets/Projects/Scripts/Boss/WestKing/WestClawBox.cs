using UnityEngine;

//沿用EnemyAttackBox的一招去重，释放帧主动采样，后续触发回调也不会重复扣血。
public sealed class WestClawBox : EnemyAttackBox
{
    private readonly Collider2D[] hits=new Collider2D[32];
    public void Sample()
    {
        Bounds shape=GetComponent<BoxCollider2D>().bounds;
        int count=Physics2D.OverlapBox(shape.center,shape.size,0f,WestHitUtility.Players,hits);
        for(int i=0;i<count;i++)TryAttack(hits[i]);
    }
    protected override void TryAttack(Collider2D hit)
    {if(WestHitUtility.Player(hit)!=null)base.TryAttack(hit);}
}

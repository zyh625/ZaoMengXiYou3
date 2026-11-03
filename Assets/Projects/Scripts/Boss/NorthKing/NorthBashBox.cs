using UnityEngine;

public sealed class NorthBashBox : EnemyAttackBox
{
    private readonly Collider2D[] hits=new Collider2D[32];
    public void Sample()
    {
        BoxCollider2D box=GetComponent<BoxCollider2D>();int count=Physics2D.OverlapBox(box.bounds.center,box.bounds.size,0f,NorthHitUtility.Players,hits);
        for(int i=0;i<count;i++)TryAttack(hits[i]);
    }
    protected override void TryAttack(Collider2D hit){if(NorthHitUtility.Player(hit)!=null)base.TryAttack(hit);}
}

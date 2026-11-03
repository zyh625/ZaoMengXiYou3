using UnityEngine;

internal static class GreenSummonHitUtility
{
    public static ContactFilter2D Players
    {get{ContactFilter2D f=new();f.SetLayerMask(1<<3);f.useTriggers=true;return f;}}
    public static Health Player(Collider2D collider)
    {
        PlayerController player=collider!=null?collider.GetComponentInParent<PlayerController>():null;
        if(player==null || !player.isActiveAndEnabled || !player.CanReceiveHit)return null;
        Health hp=player.GetComponent<Health>();return hp!=null && !hp.Dead?hp:null;
    }
    public static Vector2 GroundAt(Vector2 point)
    {
        RaycastHit2D floor=Physics2D.Raycast(point+Vector2.up*0.5f,Vector2.down,30f,1<<6);
        return new Vector2(point.x,floor.collider!=null?floor.point.y:point.y);
    }
}

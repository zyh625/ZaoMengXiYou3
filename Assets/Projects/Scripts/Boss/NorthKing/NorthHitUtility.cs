using UnityEngine;

internal static class NorthHitUtility
{
    public static ContactFilter2D Players {get{ContactFilter2D f=new();f.SetLayerMask(1<<3);f.useTriggers=true;return f;}}
    public static Health Player(Collider2D box)
    {
        PlayerController player=box!=null?box.GetComponentInParent<PlayerController>():null;
        if(player==null || !player.isActiveAndEnabled || !player.CanReceiveHit)return null;
        Health hp=player.GetComponent<Health>();return hp!=null && !hp.Dead?hp:null;
    }
}

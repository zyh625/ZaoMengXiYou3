using System.Collections.Generic;
using UnityEngine;

internal static class WestHitUtility
{
    public static ContactFilter2D Players
    {
        get { ContactFilter2D f=new();f.SetLayerMask(1<<3);f.useTriggers=true;return f; }
    }
    public static Health Player(Collider2D hit)
    {
        PlayerController actor=hit!=null?hit.GetComponentInParent<PlayerController>():null;
        if(actor==null || !actor.isActiveAndEnabled || !actor.CanReceiveHit)return null;
        Health hp=actor.GetComponent<Health>();return hp!=null && !hp.Dead?hp:null;
    }
    public static void Area(EnemyCombat source,Vector2 center,Vector2 size,float angle,float damage,bool periodic,
        Collider2D[] buffer,HashSet<Health> unique,bool knockback=false)
    {
        int count=Physics2D.OverlapBox(center,size,angle,Players,buffer);unique.Clear();
        for(int i=0;i<count;i++)
        {
            Health hp=Player(buffer[i]);if(hp==null || !unique.Add(hp))continue;
            hp.TakeDamage(new DamageInfo(source.transform,damage,knockback,false,null,false,periodic,DamageKind.Skill));
        }
    }
}

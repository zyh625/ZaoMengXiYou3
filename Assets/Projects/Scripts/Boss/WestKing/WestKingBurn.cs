using System.Collections.Generic;
using UnityEngine;

//护珠射线的两秒灼烧：每0.5秒1.5点；角色换出/死亡/驱散时清理。
[DisallowMultipleComponent]
public sealed class WestKingBurn : MonoBehaviour
{
    private struct Burn { public Health Owner;public float Until,Next,Damage; }
    private readonly List<Burn> entries=new(4);
    private Health health;
    private int epoch;
    public static void Apply(Health victim,WestCombat source,float scale)
    {
        if(victim==null || victim.Dead || victim.IsInvincible)return;
        WestKingBurn effect=victim.GetComponent<WestKingBurn>();
        if(effect==null)effect=victim.gameObject.AddComponent<WestKingBurn>();
        Health owner=source.GetComponent<Health>();
        for(int i=0;i<effect.entries.Count;i++)if(effect.entries[i].Owner==owner)
        {Burn b=effect.entries[i];b.Until=Time.time+2f;b.Damage=1.5f*scale;effect.entries[i]=b;return;}
        effect.entries.Add(new Burn{Owner=owner,Until=Time.time+2f,Next=Time.time+0.5f,Damage=1.5f*scale});
    }
    private void Awake()=>health=GetComponent<Health>();
    private void OnEnable(){health.DebuffsCleared+=Clear;health.Died+=Died;}
    private void Update()
    {
        int version=epoch;
        for(int i=entries.Count-1;i>=0;i--)
        {
            Burn b=entries[i];
            if(b.Owner==null || b.Owner.Dead || !b.Owner.isActiveAndEnabled){entries.RemoveAt(i);continue;}
            while(b.Next<=b.Until+0.0001f && Time.time>=b.Next)
            {
                b.Next+=0.5f;health.TakeDamage(new DamageInfo(b.Owner.transform,b.Damage,false,false,null,false,true,DamageKind.Skill));
                if(version!=epoch || health.Dead || !isActiveAndEnabled)return;
            }
            if(Time.time>=b.Until)entries.RemoveAt(i);else entries[i]=b;
        }
    }
    private void Died(DamageInfo damage)=>Clear();
    private void Clear(){epoch++;entries.Clear();}
    private void OnDisable(){health.DebuffsCleared-=Clear;health.Died-=Died;Clear();}
}

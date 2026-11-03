using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RedBoomBurn : MonoBehaviour
{
    private struct Burn { public Transform Source; public float Until, TickAt, Damage; }
    private readonly List<Burn> burns = new(8);
    private Health health;
    private int epoch;
    public static void Apply(Health target, Transform source, float scale)
    {
        if (target == null || target.Dead || target.IsInvincible) return;
        RedBoomBurn component = target.GetComponent<RedBoomBurn>();
        if (component == null) component = target.gameObject.AddComponent<RedBoomBurn>();
        component.Add(source,scale);
    }
    private void Awake() => health = GetComponent<Health>();
    private void OnEnable() { health.DebuffsCleared += Clear; health.Died += OnDied; }
    private void Add(Transform source, float scale)
    {
        //同射手刷新灼烧；不同射手保留独立计时。不依赖射手继续存活。
        for (int i=0;i<burns.Count;i++)
            if (burns[i].Source==source)
            { Burn b=burns[i];b.Until=Time.time+2f;b.Damage=1.5f*scale;burns[i]=b;return; }
        burns.Add(new Burn { Source=source,Until=Time.time+2f,TickAt=Time.time+0.5f,Damage=1.5f*scale });
    }
    private void Update()
    {
        int version=epoch;
        for (int i=burns.Count-1;i>=0;i--)
        {
            Burn b=burns[i];
            while (b.TickAt<=b.Until+0.0001f && Time.time>=b.TickAt)
            {
                b.TickAt+=0.5f;
                health.TakeDamage(new DamageInfo(b.Source,b.Damage,false,false,null,false,true));
                if (version!=epoch || health.Dead || !isActiveAndEnabled) return;
            }
            if (Time.time>=b.Until) burns.RemoveAt(i); else burns[i]=b;
        }
    }
    private void Clear() { epoch++;burns.Clear(); }
    private void OnDied(DamageInfo damage) => Clear();
    private void OnDisable() { health.DebuffsCleared-=Clear;health.Died-=OnDied;Clear(); }
}

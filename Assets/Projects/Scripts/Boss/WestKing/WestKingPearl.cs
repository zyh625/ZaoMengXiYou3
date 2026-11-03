using System.Collections.Generic;
using UnityEngine;

public sealed class WestKingPearl : MonoBehaviour
{
    [SerializeField] private SpriteData flight;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    private readonly Collider2D[] hits=new Collider2D[32];
    private readonly RaycastHit2D[] casts=new RaycastHit2D[16];
    private readonly HashSet<Health> unique=new(8);
    private readonly Dictionary<Health,float> nextContact=new(4);
    private WestCombat owner;
    private Health sourceHealth;
    private int sourceLife;
    private Vector2 velocity;
    private bool orbit;
    private float began,expires,scale;
    public int Bounces { get; private set; }
    public int Explosions { get; private set; }
    public int OrbitHits { get; private set; }
    public bool IsOrbiting=>orbit && gameObject.activeSelf;
    public void Launch(WestCombat source,Vector2 from,bool orbiting)
    {
        owner=source;sourceHealth=source.GetComponent<Health>();sourceLife=sourceHealth.LifeVersion;scale=source.DamageMultiplier;
        orbit=orbiting;began=Time.time;expires=began+(orbit?5f:8f);Bounces=Explosions=OrbitHits=0;nextContact.Clear();
        PlayerController target=source.Target;float targetX=target!=null?target.transform.position.x:from.x+(source.AttackFaceRight?6f:-6f);
        RaycastHit2D floor=Physics2D.Raycast(new Vector2(targetX,from.y+2f),Vector2.down,30f,1<<6);
        float targetY=floor.collider!=null?floor.point.y+0.2f:from.y-1f;
        const float t=0.9f;velocity=new Vector2((targetX-from.x)/t,(targetY-from.y+18f*t*t/2f)/t);
        transform.position=from;visual.flipX=velocity.x>0f;gameObject.SetActive(true);spriteManager.ChangeState(flight);
    }
    private void FixedUpdate()
    {
        if(owner==null || sourceHealth==null || sourceHealth.Dead || !sourceHealth.isActiveAndEnabled || sourceHealth.LifeVersion!=sourceLife || !BattleRun.IsRunning || Time.time>=expires)
        {gameObject.SetActive(false);return;}
        if(orbit){Orbit();return;}
        const float gravity=18f;float dt=Time.fixedDeltaTime;Vector2 from=transform.position;
        Vector2 delta=velocity*dt+Vector2.down*(gravity*dt*dt/2f);velocity+=Vector2.down*gravity*dt;
        ContactFilter2D filter=new();filter.SetLayerMask(1<<6);filter.useTriggers=false;
        int count=Physics2D.CircleCast(from,0.2f,delta.normalized,filter,casts,delta.magnitude);int nearest=-1;
        for(int i=0;i<count;i++)if(nearest<0 || casts[i].distance<casts[nearest].distance)nearest=i;
        if(nearest<0){transform.position=from+delta;return;}
        RaycastHit2D hit=casts[nearest];transform.position=hit.centroid+hit.normal*0.025f;
        if(hit.normal.y<0.5f){velocity=Vector2.Reflect(velocity,hit.normal)*0.8f;return;}
        //两次实际弹跳分别16伤害，第三次最终落地只爆炸22，不重复算一次弹跳。
        bool final=Bounces>=2;float radius=final?2.2f:1.2f;
        DamageLanding(radius,final?22f:16f);owner.Effects.Flame(hit.point,velocity.x>0f,true,radius);
        if(final){Explosions++;gameObject.SetActive(false);return;}
        Bounces++;PlayerController target=owner.Target;
        velocity=new Vector2(target!=null?Mathf.Clamp((target.transform.position.x-transform.position.x)/0.7f,-6f,6f):velocity.x*0.5f,6.5f);
    }
    private void DamageLanding(float radius,float damage)
    {
        int count=Physics2D.OverlapCircle(transform.position,radius,WestHitUtility.Players,hits);unique.Clear();
        for(int i=0;i<count;i++)
        {
            Health hp=WestHitUtility.Player(hits[i]);if(hp==null || !unique.Add(hp))continue;
            hp.TakeDamage(new DamageInfo(owner.transform,damage*scale,false,false,null,false,false,DamageKind.Skill));
        }
    }
    private void Orbit()
    {
        float angle=(Time.time-began)*Mathf.PI;
        Vector2 center=owner.GetComponent<BoxCollider2D>().bounds.center;
        transform.position=center+new Vector2(Mathf.Cos(angle)*2.4f,Mathf.Sin(angle)*0.9f);
        int count=Physics2D.OverlapCircle(transform.position,0.4f,WestHitUtility.Players,hits);unique.Clear();
        for(int i=0;i<count;i++)
        {
            Health hp=WestHitUtility.Player(hits[i]);
            if(hp==null || !unique.Add(hp) || nextContact.TryGetValue(hp,out float next) && Time.time<next)continue;
            nextContact[hp]=Time.time+0.5f;OrbitHits++;
            hp.TakeDamage(new DamageInfo(owner.transform,14f*scale,false,false,null,false,false,DamageKind.Skill));
        }
    }
    private void OnDisable(){spriteManager.Pause();nextContact.Clear();unique.Clear();owner=null;sourceHealth=null;}
}

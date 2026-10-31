using System.Collections.Generic;
using UnityEngine;

public sealed class RedBoomLantern : MonoBehaviour
{
    private enum Phase { Flying, Fuse, Fire }
    [SerializeField] private SpriteData flight, explosion, fire;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private LineRenderer fireRing;
    [SerializeField] private float flightTime=0.9f, gravity=18f;
    [SerializeField] private float explosionDamage=18f, radius=2.2f, fuseDuration=1f;
    [SerializeField] private float fireDuration=3f, fireInterval=0.5f, fireTickDamage=5f;
    [SerializeField] private float blastNativeWidth=2f;
    private readonly RaycastHit2D[] casts=new RaycastHit2D[16];
    private readonly Collider2D[] overlaps=new Collider2D[32];
    private readonly HashSet<Health> damaged=new(16);
    private Phase phase;
    private Vector2 velocity;
    private Transform owner;
    private Health ownerHealth;
    private int ownerLife;
    private float scale, groundY, detonateAt, expiresAt, nextTick, fallbackAt, fireVisualAt;
    private bool fireAnimation;
    public bool HasLanded { get; private set; }
    public bool HasExploded { get; private set; }
    public int FireTicks { get; private set; }
    public float LandedAt { get; private set; }
    public float ExplodedAt { get; private set; }
    private Transform DamageSource => ownerHealth!=null && ownerHealth.LifeVersion==ownerLife ? owner : transform;

    public void Launch(RedBoomCombat source, Vector2 from, PlayerController target)
    {
        owner=source.transform;ownerHealth=source.GetComponent<Health>();ownerLife=ownerHealth.LifeVersion;
        scale=source.DamageMultiplier;
        Collider2D targetBody=target.GetComponent<Collider2D>();
        Vector2 aim=new(targetBody.bounds.center.x,targetBody.bounds.min.y+0.15f);
        RaycastHit2D floor=Physics2D.Raycast(aim,Vector2.down,30f,1 << 6);
        if (floor.collider!=null) aim.y=floor.point.y+0.12f;
        float t=Mathf.Max(0.1f,flightTime);
        velocity=new Vector2((aim.x-from.x)/t,(aim.y-from.y+gravity*t*t/2f)/t);
        transform.position=from;transform.localScale=Vector3.one;
        visual.transform.localPosition=Vector3.zero;visual.transform.localScale=Vector3.one;
        visual.flipX=aim.x>from.x;fireRing.enabled=false;
        SpriteRenderer renderer=source.GetComponentInChildren<SpriteRenderer>();
        if (renderer!=null) { visual.sortingLayerID=renderer.sortingLayerID;visual.sortingOrder=renderer.sortingOrder+1; }
        phase=Phase.Flying;HasLanded=HasExploded=fireAnimation=false;FireTicks=0;
        fallbackAt=Time.time+8f;
        gameObject.SetActive(true);spriteManager.ChangeState(flight);
    }
    private void FixedUpdate()
    {
        if (phase==Phase.Flying)
        {
            if (Time.time>=fallbackAt) { gameObject.SetActive(false);return; }
            float dt=Time.fixedDeltaTime;
            Vector2 from=transform.position;
            Vector2 delta=velocity*dt+Vector2.down*(gravity*dt*dt/2f);
            velocity+=Vector2.down*(gravity*dt);
            ContactFilter2D filter=new();filter.SetLayerMask(1 << 6);filter.useTriggers=false;
            int count=Physics2D.CircleCast(from,0.12f,delta.normalized,filter,casts,delta.magnitude);
            int closest=-1;
            for(int i=0;i<count;i++) if (closest<0 || casts[i].distance<casts[closest].distance) closest=i;
            if (closest>=0)
            {
                RaycastHit2D hit=casts[closest];
                transform.position=hit.centroid+hit.normal*0.01f;
                if (hit.normal.y>=0.5f) Land(hit.point.y);
                else { if (Mathf.Abs(hit.normal.x)>0.1f) velocity.x=0f; if(hit.normal.y<0f)velocity.y=Mathf.Min(velocity.y,0f); }
            }
            else transform.position=from+delta;
            return;
        }
        if (phase==Phase.Fuse)
        {
            if (Time.time>=detonateAt) Explode();
            return;
        }
        while(nextTick<=expiresAt+0.0001f && Time.time>=nextTick)
        {
            nextTick+=fireInterval;FireTicks++;
            DamageArea(false);
            if (!gameObject.activeSelf) return;
        }
        if (!fireAnimation && Time.time>=fireVisualAt) { fireAnimation=true;spriteManager.ChangeState(fire); }
        if (Time.time>=expiresAt) gameObject.SetActive(false);
    }
    private void Land(float y)
    {
        groundY=y;phase=Phase.Fuse;HasLanded=true;LandedAt=Time.time;
        transform.position=new Vector3(transform.position.x,y+0.3f,0f);
        detonateAt=Time.time+fuseDuration;
    }
    private void Explode()
    {
        phase=Phase.Fire;HasExploded=true;ExplodedAt=Time.time;
        expiresAt=Time.time+fireDuration;nextTick=Time.time+fireInterval;
        fireVisualAt=Time.time+explosion.sprites.Length/Mathf.Max(1f,explosion.fps);
        transform.position=new Vector3(transform.position.x,groundY+0.1f,0f);
        visual.transform.localScale=new Vector3(radius*2f/blastNativeWidth,1.4f,1f);
        spriteManager.ChangeState(explosion);
        fireRing.enabled=true;
        DamageArea(true);
    }
    private void DamageArea(bool blast)
    {
        ContactFilter2D filter=RedBoomHitUtility.PlayerFilter;
        int count=blast ? Physics2D.OverlapCircle(transform.position,radius,filter,overlaps)
            : Physics2D.OverlapBox(new Vector2(transform.position.x,groundY+0.3f),new Vector2(radius*2f,0.6f),0f,filter,overlaps);
        damaged.Clear();
        for(int i=0;i<count;i++)
        {
            Health victim=RedBoomHitUtility.PlayerHealth(overlaps[i]);
            if(victim==null || !damaged.Add(victim))continue;
            victim.TakeDamage(new DamageInfo(DamageSource,(blast ? explosionDamage : fireTickDamage)*scale,false,false,null,false,!blast));
            if(!gameObject.activeSelf)return;
        }
    }
    private void OnDisable() { spriteManager.Pause();fireRing.enabled=false;damaged.Clear();owner=null;ownerHealth=null; }
}

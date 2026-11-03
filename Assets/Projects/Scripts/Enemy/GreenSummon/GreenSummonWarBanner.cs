using System.Collections.Generic;
using UnityEngine;

//无控制器、无近战、无经验：EnemyMovement仅提供现有玩家技能的可攻击目标登记。
public sealed class GreenSummonWarBanner : MonoBehaviour
{
    [SerializeField] private SpriteManager auraManager;
    [SerializeField] private SpriteData aura;
    [SerializeField] private float lifetime=8f,radius=5f,gripHeight=0.95f;
    private readonly List<Recipient> recipients=new(32);
    private readonly HashSet<Health> seen=new(32);
    private readonly struct Recipient
    {
        public readonly Health Health;public readonly EnemyCombat Combat;public readonly int Life;
        public Recipient(Health hp,EnemyCombat combat){Health=hp;Combat=combat;Life=hp.LifeVersion;}
    }
    private Health health;
    private Rigidbody2D body;
    private GreenSummonEffectPool pool;
    private GreenSummonCombat owner;
    private Vector2 anchor;
    private float until,refreshAt;
    public Vector2 GroundPosition=>anchor-Vector2.up*gripHeight;
    public float ExpiresAt=>until;
    private void Awake(){health=GetComponent<Health>();body=GetComponent<Rigidbody2D>();}
    public void Plant(GreenSummonEffectPool effects,GreenSummonCombat source,Vector2 hand)
    {
        pool=effects;owner=source;anchor=hand;transform.position=hand;until=Time.time+lifetime;refreshAt=Time.time;
        gameObject.SetActive(true);body.position=anchor;health.SetSuperArmor(this,true);health.Died+=OnDied;auraManager.ChangeState(aura);RefreshBuffs();
    }
    public void RefreshLifetime()=>until=Time.time+lifetime;
    private void FixedUpdate()
    {
        if(!BattleRun.IsRunning || health.Dead || Time.time>=until){gameObject.SetActive(false);return;}
        //旗杆插地后固定在世界位置，携带/碰撞不让旗子跟随施法者或漂移。
        transform.position=anchor;body.position=anchor;body.linearVelocity=Vector2.zero;
        if(Time.time>=refreshAt){refreshAt=Time.time+0.1f;RefreshBuffs();}
    }
    private void RefreshBuffs()
    {
        seen.Clear();Vector2 center=GroundPosition;
        foreach(EnemyMovement enemy in EnemyMovement.ActiveEnemies)
        {
            if(enemy==null || !enemy.isActiveAndEnabled || enemy.GetComponent<GreenSummonWarBanner>()!=null)continue;
            Health hp=enemy.GetComponent<Health>();EnemyCombat combat=enemy.GetComponent<EnemyCombat>();
            if(hp==null || hp.Dead || combat==null || !combat.isActiveAndEnabled || ((Vector2)enemy.transform.position-center).sqrMagnitude>radius*radius)continue;
            seen.Add(hp);bool tracked=false;
            for(int i=0;i<recipients.Count;i++)if(recipients[i].Health==hp && recipients[i].Life==hp.LifeVersion){tracked=true;break;}
            if(!tracked)recipients.Add(new Recipient(hp,combat));
            combat.ApplyAttackBuff(this,1.15f,0.35f);hp.SetDamageMultiplier(this,0.9f);
        }
        for(int i=recipients.Count-1;i>=0;i--)
        {
            Recipient target=recipients[i];
            if(target.Health!=null && target.Health.LifeVersion==target.Life && seen.Contains(target.Health))continue;
            Remove(target);recipients.RemoveAt(i);
        }
    }
    private void Remove(Recipient target)
    {
        if(target.Health==null || target.Health.LifeVersion!=target.Life)return;
        target.Health.RemoveDamageMultiplier(this);if(target.Combat!=null)target.Combat.RemoveAttackBuff(this);
    }
    private void OnDied(DamageInfo damage)=>gameObject.SetActive(false);
    private void OnDisable()
    {
        foreach(Recipient target in recipients)Remove(target);recipients.Clear();seen.Clear();
        if(health!=null){health.Died-=OnDied;health.SetSuperArmor(this,false);}
        if(auraManager!=null)auraManager.Pause();
        if(pool!=null)pool.BannerEnded(owner,this);pool=null;owner=null;
    }
}

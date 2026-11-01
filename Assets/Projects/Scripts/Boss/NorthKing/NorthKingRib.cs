using UnityEngine;

public sealed class NorthKingRib : MonoBehaviour
{
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private SpriteData flight;
    private readonly RaycastHit2D[] hits=new RaycastHit2D[32];
    private Health owner;
    private int life;
    private Vector2 direction;
    private float distance,damage;
    public bool HitPlayer {get;private set;}
    public void Launch(NorthCombat source,Vector2 from,Vector2 velocity)
    {
        owner=source.GetComponent<Health>();life=owner.LifeVersion;direction=velocity.normalized;damage=10f*source.DamageMultiplier;distance=0f;HitPlayer=false;transform.position=from;
        visual.flipX=direction.x>0f;visual.transform.localRotation=Quaternion.Euler(0f,0f,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-(direction.x>0f?0f:180f));
        gameObject.SetActive(true);spriteManager.ChangeState(flight);
    }
    private void FixedUpdate()
    {
        if(!BattleRun.IsRunning){gameObject.SetActive(false);return;}
        float step=Mathf.Min(8f*Time.fixedDeltaTime,14f-distance);ContactFilter2D filter=new();filter.SetLayerMask((1<<3)|(1<<6));filter.useTriggers=true;
        int count=Physics2D.CircleCast(transform.position,0.12f,direction,filter,hits,step),nearest=-1;
        for(int i=0;i<count;i++)
        {
            bool wall=hits[i].collider.gameObject.layer==6 && !hits[i].collider.isTrigger;
            if(!wall && NorthHitUtility.Player(hits[i].collider)==null)continue;
            if(nearest<0 || hits[i].distance<hits[nearest].distance)nearest=i;
        }
        if(nearest>=0)
        {
            transform.position=hits[nearest].centroid;Health hp=NorthHitUtility.Player(hits[nearest].collider);
            if(hp!=null){HitPlayer=true;hp.TakeDamage(new DamageInfo(owner!=null && owner.LifeVersion==life?owner.transform:transform,damage,false,false,null,false,false,DamageKind.Skill));}
            gameObject.SetActive(false);return;
        }
        transform.position+=(Vector3)(direction*step);distance+=step;if(distance>=14f)gameObject.SetActive(false);
    }
    private void OnDisable(){spriteManager.Pause();owner=null;}
}

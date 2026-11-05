using UnityEngine;

//场景池持有；14点伤害在发射时快照，射手死亡后已发出的气浪仍继续。
public sealed class GreenSummonWave : MonoBehaviour
{
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private SpriteData flight;
    [SerializeField] private float speed=10f,maxDistance=12f;
    private readonly RaycastHit2D[] hits=new RaycastHit2D[32];
    private Transform owner;
    private Health ownerHealth;
    private int ownerLife;
    private Vector2 direction;
    private float travelled,damage;
    public bool HitPlayer { get; private set; }
    public void Launch(GreenSummonCombat source,Vector2 position,bool right)
    {
        owner=source.transform;ownerHealth=source.GetComponent<Health>();ownerLife=ownerHealth.LifeVersion;
        direction=right?Vector2.right:Vector2.left;damage=BalanceRuntime.Number(this, "GreenSummonWave.Launch.1", 14f)*source.DamageMultiplier;travelled=0f;HitPlayer=false;
        transform.position=position;visual.flipX=right;gameObject.SetActive(true);spriteManager.ChangeState(flight);
    }
    private void FixedUpdate()
    {
        if(!BattleRun.IsRunning){gameObject.SetActive(false);return;}
        float distance=Mathf.Min(speed*Time.fixedDeltaTime,maxDistance-travelled);
        ContactFilter2D filter=new();filter.SetLayerMask((1<<3)|(1<<6));filter.useTriggers=true;
        int count=Physics2D.CircleCast(transform.position,BalanceRuntime.Number(this, "GreenSummonWave.FixedUpdate.1", 0.18f),direction,filter,hits,distance),nearest=-1;
        for(int i=0;i<count;i++)
        {
            Collider2D hit=hits[i].collider;bool wall=hit.gameObject.layer==6 && !hit.isTrigger;
            if(!wall && GreenSummonHitUtility.Player(hit)==null)continue;
            if(nearest<0 || hits[i].distance<hits[nearest].distance)nearest=i;
        }
        if(nearest>=0)
        {
            transform.position=hits[nearest].centroid;Health player=GreenSummonHitUtility.Player(hits[nearest].collider);
            if(player!=null)
            {
                HitPlayer=true;Transform source=ownerHealth!=null && ownerHealth.LifeVersion==ownerLife?owner:transform;
                player.TakeDamage(new DamageInfo(source,damage,false,false,null,false,false,DamageKind.Skill));
            }
            gameObject.SetActive(false);return;
        }
        transform.position+=(Vector3)(direction*distance);travelled+=distance;
        if(travelled>=maxDistance)gameObject.SetActive(false);
    }
    private void OnDisable(){spriteManager.Pause();owner=null;ownerHealth=null;}
}

using System.Collections.Generic;
using UnityEngine;

public sealed class NorthKingRain : MonoBehaviour
{
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteData falling,impact;
    private readonly Collider2D[] hits=new Collider2D[32];
    private readonly HashSet<Health> unique=new(8);
    private Health owner;
    private int life;
    private float floor,damage,expires;
    public bool Impacted {get;private set;}
    public Vector2 Landing {get;private set;}
    public void Launch(NorthCombat source,Vector2 landing)
    {
        owner=source.GetComponent<Health>();life=owner.LifeVersion;damage=BalanceRuntime.Number(this, "NorthKingRain.Launch.1", 12f)*source.DamageMultiplier;Landing=landing;floor=landing.y;Impacted=false;
        transform.position=landing+Vector2.up*BalanceRuntime.Number(this, "NorthKingRain.Launch.2", 7f);expires=Time.time+BalanceRuntime.Number(this, "NorthKingRain.Launch.3", 4f);gameObject.SetActive(true);spriteManager.ChangeState(falling);
    }
    private void FixedUpdate()
    {
        if(!BattleRun.IsRunning || Time.time>=expires){gameObject.SetActive(false);return;}
        if(Impacted)return;float step=BalanceRuntime.Number(this, "NorthKingRain.FixedUpdate.1", 9f)*Time.fixedDeltaTime;Vector2 point=transform.position;
        RaycastHit2D ground=Physics2D.Raycast(point,Vector2.down,step+BalanceRuntime.Number(this, "NorthKingRain.FixedUpdate.2", 0.02f),1<<6);
        if(ground.collider!=null || point.y-step<=floor)
        {
            Impacted=true;transform.position=new Vector2(point.x,ground.collider!=null?ground.point.y:floor);expires=Time.time+impact.sprites.Length/impact.fps;spriteManager.ChangeState(impact);
            int count=Physics2D.OverlapCircle(transform.position,1f,NorthHitUtility.Players,hits);unique.Clear();Transform source=owner!=null && owner.LifeVersion==life?owner.transform:transform;
            for(int i=0;i<count;i++){Health hp=NorthHitUtility.Player(hits[i]);if(hp!=null && unique.Add(hp))hp.TakeDamage(new DamageInfo(source,damage,false,false,null,false,false,DamageKind.Skill));}
        }
        else transform.position+=Vector3.down*step;
    }
    private void OnDisable(){spriteManager.Pause();unique.Clear();owner=null;}
}

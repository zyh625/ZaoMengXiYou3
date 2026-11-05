using UnityEngine;

public sealed class PhaseWeaverCombat : LateStageCombat
{
    [SerializeField] private float[] cooldowns={6,6,10,12};
    private bool nextHigh;private Vector2 locked;private EnemyMovement ally;private int allyLife;private Vector2 sourceAt,allyAt;
    public int PhaseHits {get;private set;}
    public int Exchanges {get;private set;}
    protected override float[] Cooldowns=>cooldowns;
    protected override int SelectSkill(float d)
    {
        if(d>9)return -1;int phase=nextHigh?1:0;
        if(Ready(phase))return phase;if(Ready(2))return 2;if(Ready(3)&&FindAlly()!=null)return 3;return -1;
    }
    protected override void ResetActor(){nextHigh=false;PhaseHits=Exchanges=0;ally=null;}
    private EnemyMovement FindAlly()
    {
        EnemyMovement found=null;float best=8*8;
        foreach(var e in EnemyMovement.ActiveEnemies)
        {
            if(e==null||e.gameObject==gameObject||!e.CanReceiveHit||e.IsExternallyControlled||e.GetComponent<LateStageDestructible>()!=null)continue;
            if(e.GetComponent<LateStageCombat>() is LateStageCombat c&&c.IsBoss)continue;
            if(e.GetComponent<BossController>()!=null||e.GetComponent<RedFurnaceLordCombat>()!=null)continue;
            float d=((Vector2)(e.transform.position-transform.position)).sqrMagnitude;
            if(d<best){found=e;best=d;}
        }
        return found;
    }
    protected override void OnBegin(int i)
    {
        locked=Ground(Target.transform.position);
        if(i<3)
        {
            bool high=i==1 || i==2&&nextHigh;int fx=high?0:1;
            FX(fx,LayerCenter(high),3,BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.1", 0.7f),Color.yellow);
            Later(BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.2", 0.7f),LayerCenter(high),new Vector2(BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.3", 3f),BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.4", 1.1f)),i==2?10:14,high?2:1);
            if(i==2){FX(high?1:0,LayerCenter(!high),3,BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.5", 1.6f),new Color(1,1,1,BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.6", 0.35f)));Later(BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.7", 1.6f),LayerCenter(!high),new Vector2(BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.8", 3f),BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.9", 1.1f)),BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.Parameter1", 10f),high?1:2);FX(2,locked+Vector2.up,3,BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.10", 1.7f));}
            else{readyAt[1-i]=readyAt[i];nextHigh=!nextHigh;}
        }
        else
        {
            ally=FindAlly();if(ally==null)return;allyLife=ally.LifeVersion;sourceAt=Position;allyAt=ally.transform.position;
            FX(3,sourceAt+Vector2.up,2,1);FX(3,allyAt+Vector2.up,2,1);minSkillEnd=clock+BalanceRuntime.Number(this, "PhaseWeaverCombat.OnBegin.11", 1.3f);
        }
    }
    private Vector2 LayerCenter(bool high)=>locked+Vector2.up*(high?BalanceRuntime.Number(this, "PhaseWeaverCombat.LayerCenter.1", 2f):BalanceRuntime.Number(this, "PhaseWeaverCombat.LayerCenter.2", 0.55f));
    protected override void OnFrame(int f){if(Skill==3&&Once(0,Elapsed>=1f))Exchange();}
    private void Exchange()
    {
        if(ally==null||!ally.CanReceiveHit||ally.LifeVersion!=allyLife||ally.IsExternallyControlled)return;
        var other=ally.GetComponent<LateStageMovement>();
        if(!move.SafeSpot(allyAt,out var destination,true,ally.transform))return;
        Vector2 otherDestination=sourceAt;
        if(other!=null){if(!other.SafeSpot(sourceAt,out otherDestination,true,transform))return;}
        else
        {
            var floor=Physics2D.Raycast(sourceAt+Vector2.up*5,Vector2.down,30,1<<6);if(floor.collider==null)return;
            otherDestination.y=floor.point.y+BalanceRuntime.Number(this, "PhaseWeaverCombat.Exchange.1", 0.03f);
            var box=ally.GetComponent<BoxCollider2D>();
            if(Physics2D.OverlapBox(otherDestination+box.offset,box.size*BalanceRuntime.Number(this, "PhaseWeaverCombat.Exchange.2", 0.9f),0,1<<6)!=null)return;
        }
        // Only move the two existing bodies. No Instantiate, healing or reward reset.
        ally.GetComponent<EnemyController>().InterruptForSuction();move.Teleport(destination);
        var rb=ally.GetComponent<Rigidbody2D>();rb.position=otherDestination;rb.linearVelocity=Vector2.zero;
        Exchanges++;recoveryUntil=clock+BalanceRuntime.Number(this, "PhaseWeaverCombat.Exchange.3", 0.3f);
    }
    protected override void EventResolved(int tag){PhaseHits++;FX(tag==2?0:1,LayerCenter(tag==2),3,BalanceRuntime.Number(this, "PhaseWeaverCombat.EventResolved.1", 0.3f),Color.red);}
}

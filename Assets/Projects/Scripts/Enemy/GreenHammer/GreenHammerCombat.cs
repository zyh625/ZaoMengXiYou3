using UnityEngine;

public sealed class GreenHammerCombat : EnemyCombat
{
    [SerializeField] private GreenHammerAttackBox[] hitBoxes;
    [SerializeField] private GreenHammerShockwaveEmitter shockwave;
    private GreenHammerMovement movement;
    private bool sampled,leapLaunched,windowClosed;
    private int pending;
    private float nextSegment,segmentScale;
    private Vector2 groundOrigin;
    private bool groundRight;
    private readonly float[] segmentTimes=new float[3];
    public int SegmentsEmitted { get; private set; }
    public float SegmentTime(int index)=>segmentTimes[index];
    public bool LeapLaunched=>leapLaunched;
    public bool HasPendingSegments=>pending>0;
    public bool WindowClosed=>windowClosed;
    protected override bool UsesAnger=>false;
    protected override void Awake(){base.Awake();movement=GetComponent<GreenHammerMovement>();}
    public static int EligibleMask(float distance,int readyMask)
    {
        distance=Mathf.Abs(distance);int eligible=0;
        if(distance>=BalanceRuntime.Number(typeof(GreenHammerCombat), "GreenHammerCombat.EligibleMask.1", 2.5f) && distance<=BalanceRuntime.Number(typeof(GreenHammerCombat), "GreenHammerCombat.EligibleMask.2", 6f) && (readyMask&1)!=0)eligible|=1;
        if(distance<=BalanceRuntime.Number(typeof(GreenHammerCombat), "GreenHammerCombat.EligibleMask.3", 6f) && (readyMask&2)!=0)eligible|=2;
        if(distance>=BalanceRuntime.Number(typeof(GreenHammerCombat), "GreenHammerCombat.EligibleMask.4", 3f) && distance<=BalanceRuntime.Number(typeof(GreenHammerCombat), "GreenHammerCombat.EligibleMask.5", 8f) && (readyMask&4)!=0)eligible|=4;
        return eligible;
    }
    private int RequestMask=>AttackRequest!=null && AttackRequest.Length==3?(AttackRequest[0]?1:0)|(AttackRequest[1]?2:0)|(AttackRequest[2]?4:0):0;
    public bool HasEligible(float distance)=>EligibleMask(distance,RequestMask)!=0;
    public bool SelectRequest(float delta)
    {
        int mask=EligibleMask(delta,RequestMask),chosen=-1;
        //三招都就绪时随机；其余情况优先横扫，再在跃砸/震地之间选就绪招式。
        if(mask==7)chosen=Random.Range(0,3);
        else if((mask&1)!=0)chosen=0;
        else if(mask==6)chosen=Random.value<BalanceRuntime.Number(this, "GreenHammerCombat.SelectRequest.1", 0.5f)?1:2;
        else if((mask&2)!=0)chosen=1;else if((mask&4)!=0)chosen=2;
        ClearAttackRequests();if(chosen>=0)AttackRequest[chosen]=true;return chosen>=0;
    }
    public void BeginCast(){movement.PrepareCast();sampled=leapLaunched=windowClosed=false;pending=SegmentsEmitted=0;for(int i=0;i<3;i++)segmentTimes[i]=0f;}
    private Vector2 GroundPoint(float x)
    {
        float y=GetComponent<BoxCollider2D>().bounds.min.y;
        RaycastHit2D floor=Physics2D.Raycast(new Vector2(x,y+BalanceRuntime.Number(this, "GreenHammerCombat.GroundPoint.1", 0.8f)),Vector2.down,BalanceRuntime.Number(this, "GreenHammerCombat.GroundPoint.2", 2f),1<<6);
        return new Vector2(x,floor.collider!=null?floor.point.y:y);
    }
    public void SampleFrame(int frame)
    {
        if(Attack<0 || windowClosed)return;
        int attack=Attack;
        if(Attack==1 && !leapLaunched && frame>=BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.Parameter1", 4f)){leapLaunched=true;movement.BeginLeap(movement.DeltaX);}
        if(Attack==1 && (!leapLaunched || !movement.HasLanded))return;
        if(!sampled && AttackOpenOrPast(frame))
        {
            sampled=true;AttackBegin();Physics2D.SyncTransforms();hitBoxes[Attack].SampleNow();
            if(Attack!=attack)return; //反伤可在扣血回调中击杀施法者，不能继续访问已结束的攻击索引。
            if(Attack==1)shockwave.Emit(this,GroundPoint(transform.position.x),BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.1", 6f),false,DamageMultiplier);
            if(Attack==2)
            {
                groundOrigin=GroundPoint(transform.position.x);groundRight=AttackFaceRight;segmentScale=DamageMultiplier;
                SegmentsEmitted=1;segmentTimes[0]=Time.time;pending=2;nextSegment=Time.time+BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.2", 0.15f);
                shockwave.Emit(this,GroundPoint(groundOrigin.x+(groundRight?BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.3", 2f):-BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.4", 2f))),BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.5", 2f),false,segmentScale);
            }
        }
        if(pending>0 && Time.time>=nextSegment)
        {
            int segment=3-pending;pending--;nextSegment=Time.time+BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.6", 0.15f);
            segmentTimes[segment]=Time.time;SegmentsEmitted++;
            Vector2 position=GroundPoint(groundOrigin.x+(groundRight?1f:-1f)*BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.7", 2f)*(segment+1));
            shockwave.Emit(this,position,BalanceRuntime.Number(this, "GreenHammerCombat.SampleFrame.8", 2f),true,segmentScale);
            if(Attack!=attack)return;
        }
        if(sampled && AttackShut(frame))
        {AttackEnd();if(pending==0)windowClosed=true;}
        else if(sampled && AttackOpen(frame))AttackBegin();
    }
    private bool AttackOpenOrPast(int frame)=>frame>=(Attack==0?5:9);
    public void ReportSegmentHit(){if(Attack==2)ReportAttackHit(hitBoxes[2]);}
    public void CancelCast(){pending=0;movement?.CancelLeap();ClearAttackRequests();}
}

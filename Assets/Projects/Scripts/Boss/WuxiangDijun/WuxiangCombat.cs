using UnityEngine;

public sealed class WuxiangCombat : LateStageCombat
{
    private const float SealDuration = 12f;
    private const float MaskBreakDuration = 2f;
    [SerializeField] private float[] cooldowns={6,10,7,12,16};
    [SerializeField] private LateStageDestructible mirrorPrefab;
    private int thresholds,pending,phase,lastOrdinary;private float insetStart=-100,wallFxUntil;private LateStageEffect wallFxLeft,wallFxRight;private float phaseStart,lockUntil,nextWheel;
    private bool secondStage,permanent,wallActive;private Vector2 sealAt;private float sealUntil;
    private GameObject leftWall,rightWall;private Transform creation;private LateStageDestructible mirror;
    public override bool IsBoss=>true;
    public override bool SpecialActive=>phase!=0;
    public override int SpecialClip=>phase==1?5:phase==2?6:phase==3?7:-1;
    public override float SpecialFraction=>phase==2?MaskBreakFraction:wallActive?Mathf.Clamp01((lockUntil-clock)/20):secondStage?1:0;
    public float SealFraction => Mathf.Clamp01((sealUntil - clock) / SealDuration);
    public float MaskBreakFraction => phase == 2 ? Mathf.Clamp01(1f - (clock - phaseStart) / MaskBreakDuration) : 0f;
    public bool SecondStage=>secondStage;
    public float EffectiveLeft {get;private set;}
    public float EffectiveRight {get;private set;}
    public int LockStarts {get;private set;}
    public int MaskBreaks {get;private set;}
    public int FinalBursts {get;private set;}
    public int FinalRingHits {get;private set;}
    protected override float[] Cooldowns=>cooldowns;
    protected override void ResetActor()
    {thresholds=pending=phase=lastOrdinary=0;secondStage=permanent=wallActive=false;lockUntil=sealUntil=0;LockStarts=MaskBreaks=FinalBursts=FinalRingHits=0;}
    protected override int SelectSkill(float d)
    {
        if(d>18)return -1;
        if(secondStage&&Ready(3))return 3;
        if(secondStage&&Ready(4))return 4;
        if(d<4&&Ready(0))return 0;
        if(Ready(1))return 1;
        return Ready(2)?2:Ready(0)?0:-1;
    }
    protected override float HitFactor(PlayerController p)=>ResolvingTag==1&&clock<sealUntil&&Vector2.Distance(p.transform.position,sealAt)<BalanceRuntime.Number(this, "WuxiangCombat.HitFactor.1", 2.5f)?BalanceRuntime.Number(this, "WuxiangCombat.HitFactor.2", 1.5f):1f;
    protected override void DamageResolved(DamageInfo info,float actual)
    {
        if(hp.Dead||actual<=0)return;
        if(hp.ratio<=BalanceRuntime.Number(this, "WuxiangCombat.DamageResolved.1", 0.7f)&&(thresholds&1)==0){thresholds|=1;pending|=1;}
        if(hp.ratio<=BalanceRuntime.Number(this, "WuxiangCombat.DamageResolved.2", 0.5f)&&(thresholds&2)==0){thresholds|=2;pending|=2;}
        if(hp.ratio<=BalanceRuntime.Number(this, "WuxiangCombat.DamageResolved.3", 0.2f)&&(thresholds&4)==0){thresholds|=4;pending|=4;}
    }
    protected override void BeforeTick(){if(phase==2)hp.GrantInvincibility(this,float.PositiveInfinity);}
    protected override void OnBegin(int i)
    {
        if(i==0)sealAt=Ground(Target.transform.position);
        if(i==1)
        {
            var at=Ground(Target.transform.position);
            for(int j=-1;j<=1;j++){var p=Ground(at+Vector2.right*j*BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.1", 2.4f));FX(1,p,2,BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.2", 0.7f)+(BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.3", 0.3f)*(j+1)),Color.yellow);Later(BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.4", 0.7f)+BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.5", 0.3f)*(j+1),p+Vector2.up*BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.6", 0.7f),new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.7", 2f),BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.8", 1.4f)),BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.Parameter1", 22f),1);}
        }
        if(i==3){nextWheel=clock+BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.9", 0.5f);minSkillEnd=clock+6;FX(3,Position+Vector2.up,5,6);}
        if(i==4){minSkillEnd=clock+BalanceRuntime.Number(this, "WuxiangCombat.OnBegin.10", 1.2f);SpawnMirror();}
    }
    protected override void OnFrame(int f)
    {
        if(Skill==0&&Once(0,f>=BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.Parameter1", 9f)))
        {sealUntil=clock+SealDuration;Area(sealAt,new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.1", 5f),BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.2", 5f)),BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.3", 28f),BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.4", 2.5f),true);FX(0,sealAt,5,SealDuration);}
        else if(Skill==2)
        {Window(0,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.Parameter2", 7f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.Parameter3", 11f)),BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.5", 25f),new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.6", 8f),BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.7", 1.1f)),new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.8", 4f),BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.9", 0.55f)),true);if(Once(0,f>=BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.Parameter4", 7f)))FX(6,Position+new Vector2(Face*4,BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.10", 0.4f)),8,BalanceRuntime.Number(this, "WuxiangCombat.OnFrame.11", 0.7f));}
    }
    protected override void OnFinished(int i){if(i<4)lastOrdinary=i;}
    private void SpawnMirror()
    {
        if(creation==null){creation=new GameObject("MirrorCreation").transform;creation.SetParent(transform,false);creation.gameObject.SetActive(false);}
        if(mirror==null){mirror=Instantiate(mirrorPrefab,creation);mirror.gameObject.SetActive(false);mirror.transform.SetParent(null,true);}
        mirror.Dismiss();var at=Ground(Position+Vector2.right*Face*3);mirror.Activate(at,7,_=>{});
        var guard=mirror.Health;int guardLife=guard.LifeVersion;int copy=lastOrdinary;FX(4,at+Vector2.up,BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.1", 2.5f),1);
        LaterAction(BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.2", 0.8f),()=>
        {
            if(guard==null||guard.Dead||!guard.isActiveAndEnabled||guard.LifeVersion!=guardLife)return;
            var targetAt=Ground(Target!=null?(Vector2)Target.transform.position:at);
            if(copy==0){FX(0,targetAt,5,BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.3", 0.8f));Area(targetAt,new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.4", 5f),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.5", 5f)),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.6", 14f),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.7", 2.5f),true);}
            else if(copy==1)for(int j=-1;j<=1;j++)Later(BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.8", 0.25f)*(j+1),Ground(targetAt+Vector2.right*j*BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.9", 2.4f))+Vector2.up*BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.10", 0.7f),new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.11", 2f),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.12", 1.4f)),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.Parameter1", 11f),1,0,false,guard);
            else if(copy==2){FX(6,at+new Vector2(Face*4,BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.13", 0.4f)),8,BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.14", 0.7f));Area(at+new Vector2(Face*4,BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.15", 0.55f)),new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.16", 8f),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.17", 1.1f)),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.18", 12.5f));}
            else for(int j=1;j<=12;j++)Later(j*BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.19", 0.5f),at+Vector2.up,new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.20", 5f),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.21", 5f)),BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.Parameter2", 6f),0,BalanceRuntime.Number(this, "WuxiangCombat.SpawnMirror.22", 2.5f),false,guard);
        });
    }
    protected override void TickActor(float dt)
    {
        UpdateWalls();
        if(phase==0&&pending!=0)
        {
            if((pending&2)!=0){pending&=~2;StartPhase(2);}
            else if((pending&1)!=0){pending&=~1;StartPhase(1);}
            else{pending&=~4;StartPhase(3);}return;
        }
        if(phase==1&&clock-phaseStart>=1){phase=0;SpecialVersion++;}
        else if(phase==2&&clock-phaseStart>=MaskBreakDuration)
        {hp.RemoveInvincibility(this);Area(new Vector2((arenaLeft+arenaRight)/2,move.FloorY+4),new Vector2(arenaRight-arenaLeft,BalanceRuntime.Number(this, "WuxiangCombat.TickActor.1", 12f)),BalanceRuntime.Number(this, "WuxiangCombat.TickActor.2", 20f),0,true);phase=0;SpecialVersion++;}
        else if(phase==3)
        {
            // Three annuli, each once; the gaps between annuli remain safe.
            int ring=Mathf.FloorToInt((clock-phaseStart-BalanceRuntime.Number(this, "WuxiangCombat.TickActor.3", 0.7f))/BalanceRuntime.Number(this, "WuxiangCombat.TickActor.4", 0.45f));
            for(int j=0;j<3;j++)if(ring>=j&&Once(8+j,true))
            {float outer=BalanceRuntime.Number(this, "WuxiangCombat.TickActor.5", 2.2f)+j*BalanceRuntime.Number(this, "WuxiangCombat.TickActor.6", 2.5f);Area(Position,new Vector2(outer*2,outer*2),BalanceRuntime.Number(this, "WuxiangCombat.TickActor.7", 40f),outer,false,false,outer-BalanceRuntime.Number(this, "WuxiangCombat.TickActor.8", 1.1f));FinalRingHits++;FX(5,Position,outer*2,BalanceRuntime.Number(this, "WuxiangCombat.TickActor.9", 0.5f),Color.red);}
            if(clock-phaseStart>=BalanceRuntime.Number(this, "WuxiangCombat.TickActor.10", 2.1f)){phase=0;SpecialVersion++;}
        }
        if(Skill==3)while(clock>=nextWheel&&clock-skillStart<=BalanceRuntime.Number(this, "WuxiangCombat.TickActor.11", 6.01f))
        {nextWheel+=BalanceRuntime.Number(this, "WuxiangCombat.TickActor.12", 0.5f);Area(Position+Vector2.up,new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.TickActor.13", 6f),BalanceRuntime.Number(this, "WuxiangCombat.TickActor.14", 6f)),BalanceRuntime.Number(this, "WuxiangCombat.TickActor.15", 12f),3,false,true);}
    }
    private void StartPhase(int next)
    {
        CancelSkill();events.Clear();hp.AttackedEnd();phase=next;phaseStart=clock;SpecialVersion++;
        System.Array.Clear(released,0,released.Length);hp.SetSuperArmor(this,true);
        if(next==1){LockStarts++;if(!wallActive&&!permanent)insetStart=clock;wallActive=true;lockUntil=clock+20;}
        else if(next==2){if(!wallActive&&!permanent)insetStart=clock;MaskBreaks++;secondStage=permanent=true;hp.GrantInvincibility(this,float.PositiveInfinity);}
        else{FinalBursts++;FX(5,Position,14,BalanceRuntime.Number(this, "WuxiangCombat.StartPhase.1", 2.1f),Color.yellow);}
        nextAttack=clock+2;
    }
    private void EnsureWalls()
    {
        if(leftWall!=null)return;
        leftWall=Wall("Wuxiang_LeftSealWall");rightWall=Wall("Wuxiang_RightSealWall");
    }
    private GameObject Wall(string name)
    {var go=new GameObject(name){layer=6};var box=go.AddComponent<BoxCollider2D>();box.size=new Vector2(BalanceRuntime.Number(this, "WuxiangCombat.Wall.1", 0.16f),BalanceRuntime.Number(this, "WuxiangCombat.Wall.2", 10f));return go;}
    private void UpdateWalls()
    {
        if(wallActive&&clock>=lockUntil)wallActive=false;
        bool active=wallActive||permanent;EnsureWalls();leftWall.SetActive(active);rightWall.SetActive(active);
        float inset=active?(arenaRight-arenaLeft)*BalanceRuntime.Number(this, "WuxiangCombat.UpdateWalls.1", 0.125f):0;
        // Slide boundaries in over 1 second, pushing actors with them instead of spawning a wall through a player.
        if(active)inset*=Mathf.Clamp01(clock-insetStart);
        EffectiveLeft=arenaLeft+inset;EffectiveRight=arenaRight-inset;
        leftWall.transform.position=new Vector2(EffectiveLeft,move.FloorY+BalanceRuntime.Number(this, "WuxiangCombat.UpdateWalls.2", 4.5f));rightWall.transform.position=new Vector2(EffectiveRight,move.FloorY+BalanceRuntime.Number(this, "WuxiangCombat.UpdateWalls.3", 4.5f));
        if(active)
        {
            var p=Target;if(p!=null)
            {
                var rb=p.GetComponent<Rigidbody2D>();var shape=p.GetComponent<BoxCollider2D>();float half=shape.bounds.extents.x+BalanceRuntime.Number(this, "WuxiangCombat.UpdateWalls.4", 0.12f);
                float x=Mathf.Clamp(rb.position.x,EffectiveLeft+half,EffectiveRight-half);
                if(x!=rb.position.x){rb.position=new Vector2(x,rb.position.y);rb.linearVelocityX=0;}
            }
            float bx=Mathf.Clamp(transform.position.x,EffectiveLeft+1,EffectiveRight-1);
            if(bx!=transform.position.x)move.Teleport(new Vector2(bx,transform.position.y));
            if(clock>=wallFxUntil){wallFxUntil=clock+2;wallFxLeft=FX(2,leftWall.transform.position,1,BalanceRuntime.Number(this, "WuxiangCombat.UpdateWalls.5", 2.1f));wallFxRight=FX(2,rightWall.transform.position,1,BalanceRuntime.Number(this, "WuxiangCombat.UpdateWalls.6", 2.1f));}
            if(wallFxLeft!=null)wallFxLeft.transform.position=leftWall.transform.position;if(wallFxRight!=null)wallFxRight.transform.position=rightWall.transform.position;
        }
        if(phase==0)hp.SetSuperArmor(this,false);
    }
    protected override void StopActor()
    {phase=pending=0;wallActive=permanent=false;if(mirror!=null)mirror.Dismiss();if(leftWall!=null)leftWall.SetActive(false);if(rightWall!=null)rightWall.SetActive(false);}
    private void OnDestroy(){if(leftWall!=null)Destroy(leftWall);if(rightWall!=null)Destroy(rightWall);if(mirror!=null)Destroy(mirror.gameObject);}
}

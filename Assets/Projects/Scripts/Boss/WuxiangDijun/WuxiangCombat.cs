using UnityEngine;

public sealed class WuxiangCombat : LateStageCombat
{
    [SerializeField] private float[] cooldowns={6,10,7,12,16};
    [SerializeField] private LateStageDestructible mirrorPrefab;
    private int thresholds,pending,phase,lastOrdinary;private float insetStart=-100,wallFxUntil;private LateStageEffect wallFxLeft,wallFxRight;private float phaseStart,lockUntil,nextWheel;
    private bool secondStage,permanent,wallActive;private Vector2 sealAt;private float sealUntil;
    private GameObject leftWall,rightWall;private Transform creation;private LateStageDestructible mirror;
    public override bool IsBoss=>true;
    public override bool SpecialActive=>phase!=0;
    public override int SpecialClip=>phase==1?5:phase==2?6:phase==3?7:-1;
    public override float SpecialFraction=>phase==2?Mathf.Clamp01(1-(clock-phaseStart)/2):wallActive?Mathf.Clamp01((lockUntil-clock)/20):secondStage?1:0;
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
    protected override float HitFactor(PlayerController p)=>ResolvingTag==1&&clock<sealUntil&&Vector2.Distance(p.transform.position,sealAt)<2.5f?1.5f:1f;
    protected override void DamageResolved(DamageInfo info,float actual)
    {
        if(hp.Dead||actual<=0)return;
        if(hp.ratio<=.7f&&(thresholds&1)==0){thresholds|=1;pending|=1;}
        if(hp.ratio<=.5f&&(thresholds&2)==0){thresholds|=2;pending|=2;}
        if(hp.ratio<=.2f&&(thresholds&4)==0){thresholds|=4;pending|=4;}
    }
    protected override void BeforeTick(){if(phase==2)hp.GrantInvincibility(this,float.PositiveInfinity);}
    protected override void OnBegin(int i)
    {
        if(i==0)sealAt=Ground(Target.transform.position);
        if(i==1)
        {
            var at=Ground(Target.transform.position);
            for(int j=-1;j<=1;j++){var p=Ground(at+Vector2.right*j*2.4f);FX(1,p,2,.7f+(.3f*(j+1)),Color.yellow);Later(.7f+.3f*(j+1),p+Vector2.up*.7f,new Vector2(2,1.4f),22,1);}
        }
        if(i==3){nextWheel=clock+.5f;minSkillEnd=clock+6;FX(3,Position+Vector2.up,5,6);}
        if(i==4){minSkillEnd=clock+1.2f;SpawnMirror();}
    }
    protected override void OnFrame(int f)
    {
        if(Skill==0&&Once(0,f>=9))
        {sealUntil=clock+12;Area(sealAt,new Vector2(5,5),28,2.5f,true);FX(0,sealAt,5,12);}
        else if(Skill==2)
        {Window(0,f,7,11,25,new Vector2(8,1.1f),new Vector2(4,.55f),true);if(Once(0,f>=7))FX(6,Position+new Vector2(Face*4,.4f),8,.7f);}
    }
    protected override void OnFinished(int i){if(i<4)lastOrdinary=i;}
    private void SpawnMirror()
    {
        if(creation==null){creation=new GameObject("MirrorCreation").transform;creation.SetParent(transform,false);creation.gameObject.SetActive(false);}
        if(mirror==null){mirror=Instantiate(mirrorPrefab,creation);mirror.gameObject.SetActive(false);mirror.transform.SetParent(null,true);}
        mirror.Dismiss();var at=Ground(Position+Vector2.right*Face*3);mirror.Activate(at,7,_=>{});
        var guard=mirror.Health;int guardLife=guard.LifeVersion;int copy=lastOrdinary;FX(4,at+Vector2.up,2.5f,1);
        LaterAction(.8f,()=>
        {
            if(guard==null||guard.Dead||!guard.isActiveAndEnabled||guard.LifeVersion!=guardLife)return;
            var targetAt=Ground(Target!=null?(Vector2)Target.transform.position:at);
            if(copy==0){FX(0,targetAt,5,.8f);Area(targetAt,new Vector2(5,5),14,2.5f,true);}
            else if(copy==1)for(int j=-1;j<=1;j++)Later(.25f*(j+1),Ground(targetAt+Vector2.right*j*2.4f)+Vector2.up*.7f,new Vector2(2,1.4f),11,1,0,false,guard);
            else if(copy==2){FX(6,at+new Vector2(Face*4,.4f),8,.7f);Area(at+new Vector2(Face*4,.55f),new Vector2(8,1.1f),12.5f);}
            else for(int j=1;j<=12;j++)Later(j*.5f,at+Vector2.up,new Vector2(5,5),6,0,2.5f,false,guard);
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
        else if(phase==2&&clock-phaseStart>=2)
        {hp.RemoveInvincibility(this);Area(new Vector2((arenaLeft+arenaRight)/2,move.FloorY+4),new Vector2(arenaRight-arenaLeft,12),20,0,true);phase=0;SpecialVersion++;}
        else if(phase==3)
        {
            // Three annuli, each once; the gaps between annuli remain safe.
            int ring=Mathf.FloorToInt((clock-phaseStart-.7f)/.45f);
            for(int j=0;j<3;j++)if(ring>=j&&Once(8+j,true))
            {float outer=2.2f+j*2.5f;Area(Position,new Vector2(outer*2,outer*2),40,outer,false,false,outer-1.1f);FinalRingHits++;FX(5,Position,outer*2,.5f,Color.red);}
            if(clock-phaseStart>=2.1f){phase=0;SpecialVersion++;}
        }
        if(Skill==3)while(clock>=nextWheel&&clock-skillStart<=6.01f)
        {nextWheel+=.5f;Area(Position+Vector2.up,new Vector2(6,6),12,3,false,true);}
    }
    private void StartPhase(int next)
    {
        CancelSkill();events.Clear();hp.AttackedEnd();phase=next;phaseStart=clock;SpecialVersion++;
        System.Array.Clear(released,0,released.Length);hp.SetSuperArmor(this,true);
        if(next==1){LockStarts++;if(!wallActive&&!permanent)insetStart=clock;wallActive=true;lockUntil=clock+20;}
        else if(next==2){if(!wallActive&&!permanent)insetStart=clock;MaskBreaks++;secondStage=permanent=true;hp.GrantInvincibility(this,float.PositiveInfinity);}
        else{FinalBursts++;FX(5,Position,14,2.1f,Color.yellow);}
        nextAttack=clock+2;
    }
    private void EnsureWalls()
    {
        if(leftWall!=null)return;
        leftWall=Wall("Wuxiang_LeftSealWall");rightWall=Wall("Wuxiang_RightSealWall");
    }
    private GameObject Wall(string name)
    {var go=new GameObject(name){layer=6};var box=go.AddComponent<BoxCollider2D>();box.size=new Vector2(.16f,10);return go;}
    private void UpdateWalls()
    {
        if(wallActive&&clock>=lockUntil)wallActive=false;
        bool active=wallActive||permanent;EnsureWalls();leftWall.SetActive(active);rightWall.SetActive(active);
        float inset=active?(arenaRight-arenaLeft)*.125f:0;
        // Slide boundaries in over 1 second, pushing actors with them instead of spawning a wall through a player.
        if(active)inset*=Mathf.Clamp01(clock-insetStart);
        EffectiveLeft=arenaLeft+inset;EffectiveRight=arenaRight-inset;
        leftWall.transform.position=new Vector2(EffectiveLeft,move.FloorY+4.5f);rightWall.transform.position=new Vector2(EffectiveRight,move.FloorY+4.5f);
        if(active)
        {
            var p=Target;if(p!=null)
            {
                var rb=p.GetComponent<Rigidbody2D>();var shape=p.GetComponent<BoxCollider2D>();float half=shape.bounds.extents.x+.12f;
                float x=Mathf.Clamp(rb.position.x,EffectiveLeft+half,EffectiveRight-half);
                if(x!=rb.position.x){rb.position=new Vector2(x,rb.position.y);rb.linearVelocityX=0;}
            }
            float bx=Mathf.Clamp(transform.position.x,EffectiveLeft+1,EffectiveRight-1);
            if(bx!=transform.position.x)move.Teleport(new Vector2(bx,transform.position.y));
            if(clock>=wallFxUntil){wallFxUntil=clock+2;wallFxLeft=FX(2,leftWall.transform.position,1,2.1f);wallFxRight=FX(2,rightWall.transform.position,1,2.1f);}
            if(wallFxLeft!=null)wallFxLeft.transform.position=leftWall.transform.position;if(wallFxRight!=null)wallFxRight.transform.position=rightWall.transform.position;
        }
        if(phase==0)hp.SetSuperArmor(this,false);
    }
    protected override void StopActor()
    {phase=pending=0;wallActive=permanent=false;if(mirror!=null)mirror.Dismiss();if(leftWall!=null)leftWall.SetActive(false);if(rightWall!=null)rightWall.SetActive(false);}
    private void OnDestroy(){if(leftWall!=null)Destroy(leftWall);if(rightWall!=null)Destroy(rightWall);if(mirror!=null)Destroy(mirror.gameObject);}
}

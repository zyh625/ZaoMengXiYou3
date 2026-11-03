using UnityEngine;

public sealed class JiuyaoCombat : LateStageCombat
{
    [SerializeField] private float[] cooldowns={5,7,9,12};
    [SerializeField] private float ascendDuration=8f,ascendRainInterval=2.5f,landingRecovery=2.5f;
    private int thresholds,pending,phase;private float phaseStart,nextRain;
    private Vector2 diveAt;private bool diving;
    public int AscendStarts {get;private set;}
    public int RainVolleys {get;private set;}
    public int AscendLandings {get;private set;}
    public override bool IsBoss=>true;
    public override bool SpecialActive=>phase!=0;
    public override int SpecialClip=>phase==1?4:-1;
    public override float SpecialFraction=>phase==1?Mathf.Clamp01(1-(clock-phaseStart)/ascendDuration):phase==2?Mathf.Clamp01(1-(clock-phaseStart)/landingRecovery):0;
    protected override float[] Cooldowns=>cooldowns;
    protected override void ResetActor(){thresholds=pending=phase=0;AscendStarts=RainVolleys=AscendLandings=0;diving=false;}
    protected override int SelectSkill(float d)
    {
        if(d>16)return -1;
        if(d>6&&Ready(0))return 0;
        if(d<6&&Ready(2))return 2;
        if(Ready(1))return 1;
        if(Ready(3))return 3;
        return Ready(0)?0:-1;
    }
    protected override void DamageResolved(DamageInfo info,float actual)
    {
        if(hp.Dead||actual<=0)return;
        if(hp.ratio<=.7f&&(thresholds&1)==0){thresholds|=1;pending++;}
        if(hp.ratio<=.4f&&(thresholds&2)==0){thresholds|=2;pending++;}
    }
    protected override void OnBegin(int i){diving=false;diveAt=Ground(Target.transform.position);}
    protected override void OnFrame(int f)
    {
        if(Skill==0)
        {
            if(Once(0,f>=5)){move.DashVelocity=Face*14;FX(3,Position+Vector2.up,3,.55f);}
            Window(0,f,5,11,28,new Vector2(3,1.3f),new Vector2(.5f,.65f),true);
            if(f>=12)move.DashVelocity=0;
        }
        else if(Skill==1&&Once(0,f>=7))
        {
            Vector2 muzzle=Position+new Vector2(Face,1.3f);
            for(int i=0;i<4;i++){float a=(-24+i*16)*Mathf.Deg2Rad;Fire(muzzle,new Vector2(Face*Mathf.Cos(a),Mathf.Sin(a))*11,12,1);}
        }
        else if(Skill==2)
        {
            float range=(arenaRight-arenaLeft)*.5f;
            Window(1,f,7,11,22,new Vector2(range,1.1f),new Vector2(range*.5f,.55f),true);
            if(Once(0,f>=7))FX(5,Position+new Vector2(Face*range*.5f,.4f),range,.65f);
        }
        else if(Skill==3)
        {
            if(Once(0,f>=3)){move.Altitude=3;diving=true;}
            if(Once(1,f>=10))
            {diving=false;move.Altitude=0;var land=Ground(diveAt);move.Teleport(land);FX(4,land,5,.8f);}
            if(released[1])Window(2,f,10,12,30,new Vector2(5,5),Vector2.zero,true,2.5f);
        }
    }
    protected override void TickActor(float dt)
    {
        if(phase==0&&pending>0&&!OrdinaryInterrupted){StartAscend();return;}
        if(phase==1)
        {
            // Freeze/carry pause clock in LateStageCombat. Ordinary control is denied by our armor source.
            move.Altitude=5;move.Stop();
            while(clock-phaseStart>=nextRain && nextRain<ascendDuration)
            {
                nextRain+=ascendRainInterval;RainVolleys++;
                var center=Target!=null?Ground(Target.transform.position):Ground(Position);
                for(int i=-1;i<=1;i++)
                {var at=Ground(center+Vector2.right*(i*2+Random.Range(-.3f,.3f)));FX(2,at,1.2f,.7f,Color.yellow);Later(.7f,at+Vector2.up*.5f,new Vector2(1.1f,1.3f),10,1);}
            }
            if(clock-phaseStart>=ascendDuration)LandAscend();
        }
        else if(phase==2&&clock-phaseStart>=landingRecovery)
        {phase=0;hp.RemoveDamageMultiplier(this);hp.SetSuperArmor(this,false);nextAttack=clock+attackInterval;SpecialVersion++;}
    }
    private void StartAscend()
    {
        pending--;CancelSkill();events.Clear();hp.AttackedEnd();phase=1;phaseStart=clock;nextRain=ascendRainInterval;
        AscendStarts++;SpecialVersion++;hp.SetDamageMultiplier(this,.5f);hp.SetSuperArmor(this,true);move.Altitude=5;
        FX(0,Position+Vector2.up*3,3,ascendDuration);
    }
    private void LandAscend()
    {
        phase=2;phaseStart=clock;SpecialVersion++;AscendLandings++;move.Altitude=0;
        var at=Ground(Target!=null?(Vector2)Target.transform.position:Position);move.Teleport(at);
        Area(at,new Vector2(6,6),32,3,true);FX(4,at,6,1);
        hp.RemoveDamageMultiplier(this);hp.SetDamageMultiplier(this,1.5f);
        hp.SetSuperArmor(this,true); // occupies the entire punish window, even after ordinary hits
    }
    protected override void OnCancelled(int i){if(diving){move.Altitude=0;diving=false;}}
    protected override void StopActor(){phase=pending=0;diving=false;if(move!=null)move.Altitude=0;}
}

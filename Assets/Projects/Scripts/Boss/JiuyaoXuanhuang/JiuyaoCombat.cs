using UnityEngine;

public sealed class JiuyaoCombat : LateStageCombat
{
    [SerializeField] private float[] cooldowns={5,7,9,12};
    [SerializeField] private float ascendDuration=8f,ascendRainInterval=2.5f,landingRecovery=2.5f;
    [SerializeField] private float dashSpeed=14f,featherSpeed=11f,fanStartDegrees=-24f,fanStepDegrees=16f;
    [SerializeField,Min(1)] private int fanProjectileCount=4;
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
        if(hp.ratio<=BalanceRuntime.Number(this, "JiuyaoCombat.DamageResolved.1", 0.7f)&&(thresholds&1)==0){thresholds|=1;pending++;}
        if(hp.ratio<=BalanceRuntime.Number(this, "JiuyaoCombat.DamageResolved.2", 0.4f)&&(thresholds&2)==0){thresholds|=2;pending++;}
    }
    protected override void OnBegin(int i){diving=false;diveAt=Ground(Target.transform.position);}
    protected override void OnFrame(int f)
    {
        if(Skill==0)
        {
            if(Once(0,f>=BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter1", 5f))){move.DashVelocity=Face*dashSpeed;FX(3,Position+Vector2.up,3,BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.1", 0.55f));}
            Window(0,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter2", 5f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter3", 11f)),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.2", 28f),new Vector2(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.3", 3f),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.4", 1.3f)),new Vector2(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.5", 0.5f),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.6", 0.65f)),true);
            if(f>=BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter4", 12f))move.DashVelocity=0;
        }
        else if(Skill==1&&Once(0,f>=BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter5", 7f)))
        {
            Vector2 muzzle=Position+new Vector2(Face,BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.7", 1.3f));
            for(int i=0;i<Mathf.Max(1,fanProjectileCount);i++){float a=(fanStartDegrees+i*fanStepDegrees)*Mathf.Deg2Rad;Fire(muzzle,new Vector2(Face*Mathf.Cos(a),Mathf.Sin(a))*featherSpeed,BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.8", 12f),1);}
        }
        else if(Skill==2)
        {
            float range=(arenaRight-arenaLeft)*BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.9", 0.5f);
            Window(1,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter6", 7f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter7", 11f)),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.10", 22f),new Vector2(range,BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.11", 1.1f)),new Vector2(range*BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.12", 0.5f),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.13", 0.55f)),true);
            if(Once(0,f>=BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter8", 7f)))FX(5,Position+new Vector2(Face*range*BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.14", 0.5f),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.15", 0.4f)),range,BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.16", 0.65f));
        }
        else if(Skill==3)
        {
            if(Once(0,f>=BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter9", 3f))){move.Altitude=3;diving=true;}
            if(Once(1,f>=BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter10", 10f)))
            {diving=false;move.Altitude=0;var land=Ground(diveAt);move.Teleport(land);FX(4,land,5,BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.17", 0.8f));}
            if(released[1])Window(2,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter11", 10f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.Parameter12", 12f)),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.18", 30f),new Vector2(BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.19", 5f),BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.20", 5f)),Vector2.zero,true,BalanceRuntime.Number(this, "JiuyaoCombat.OnFrame.21", 2.5f));
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
                {var at=Ground(center+Vector2.right*(i*2+Random.Range(-BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.1", 0.3f),BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.2", 0.3f))));FX(2,at,BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.3", 1.2f),BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.4", 0.7f),Color.yellow);Later(BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.5", 0.7f),at+Vector2.up*BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.6", 0.5f),new Vector2(BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.7", 1.1f),BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.8", 1.3f)),BalanceRuntime.Number(this, "JiuyaoCombat.TickActor.Parameter1", 10f),1);}
            }
            if(clock-phaseStart>=ascendDuration)LandAscend();
        }
        else if(phase==2&&clock-phaseStart>=landingRecovery)
        {phase=0;hp.RemoveDamageMultiplier(this);hp.SetSuperArmor(this,false);nextAttack=clock+attackInterval;SpecialVersion++;}
    }
    private void StartAscend()
    {
        pending--;CancelSkill();events.Clear();hp.AttackedEnd();phase=1;phaseStart=clock;nextRain=ascendRainInterval;
        AscendStarts++;SpecialVersion++;hp.SetDamageMultiplier(this,BalanceRuntime.Number(this, "JiuyaoCombat.StartAscend.1", 0.5f));hp.SetSuperArmor(this,true);move.Altitude=5;
        FX(0,Position+Vector2.up*3,3,ascendDuration);
    }
    private void LandAscend()
    {
        phase=2;phaseStart=clock;SpecialVersion++;AscendLandings++;move.Altitude=0;
        var at=Ground(Target!=null?(Vector2)Target.transform.position:Position);move.Teleport(at);
        Area(at,new Vector2(BalanceRuntime.Number(this, "JiuyaoCombat.LandAscend.1", 6f),BalanceRuntime.Number(this, "JiuyaoCombat.LandAscend.2", 6f)),BalanceRuntime.Number(this, "JiuyaoCombat.LandAscend.3", 32f),3,true);FX(4,at,6,1);
        hp.RemoveDamageMultiplier(this);hp.SetDamageMultiplier(this,BalanceRuntime.Number(this, "JiuyaoCombat.LandAscend.4", 1.5f));
        hp.SetSuperArmor(this,true); // occupies the entire punish window, even after ordinary hits
    }
    protected override void OnCancelled(int i){if(diving){move.Altitude=0;diving=false;}}
    protected override void StopActor(){phase=pending=0;diving=false;if(move!=null)move.Altitude=0;}
}

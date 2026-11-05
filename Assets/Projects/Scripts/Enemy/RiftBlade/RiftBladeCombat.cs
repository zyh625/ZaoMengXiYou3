using UnityEngine;

public sealed class RiftBladeCombat : LateStageCombat
{
    [SerializeField] private float[] cooldowns={4f,8f,9f};
    private Vector2 mark,anchor;private float firstHitAt;
    public int SeamResolutions {get;private set;}
    protected override float[] Cooldowns=>cooldowns;
    protected override int SelectSkill(float d)=>d<=BalanceRuntime.Number(this, "RiftBladeCombat.SelectSkill.1", 2.8f)&&Ready(0)?0:d<=7&&Ready(1)?1:d<=9&&Ready(2)?2:-1;
    protected override void ResetActor(){SeamResolutions=0;firstHitAt=-1;}
    protected override void OnBegin(int i)
    {
        firstHitAt=-1;
        if(i==1){mark=Ground(Position+Vector2.right*Face*BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.1", 2.5f));FX(0,mark+Vector2.up, BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.2", 0.7f),BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.3", 0.7f),Color.yellow);}
        if(i==2)
        {
            anchor=Ground(Position+Vector2.right*Face*BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.4", 3f));
            FX(1,Ground(Position)+Vector2.up*BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.5", 0.2f),BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.6", 0.8f),BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.7", 0.9f),Color.yellow);FX(1,anchor+Vector2.up*BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.8", 0.2f),BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.9", 0.8f),BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.10", 0.9f),Color.yellow);
            minSkillEnd=clock+BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.11", 1.3f);
        }
    }
    public override bool HoldFrame(int frame)=>Skill==0&&frame>=BalanceRuntime.Number(this, "RiftBladeCombat.HoldFrame.Parameter1", 10f)&&firstHitAt>=0&&clock<firstHitAt+BalanceRuntime.Number(this, "RiftBladeCombat.HoldFrame.1", 0.25f) || base.HoldFrame(frame);
    protected override void OnFrame(int f)
    {
        if(Skill==0)
        {
            Window(0,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter1", 5f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter2", 8f)),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.1", 10f),new Vector2(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.2", 1.8f),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.3", 1.1f)),new Vector2(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.4", 1.1f),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.5", 0.3f)));
            if(Once(0,f>=BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter3", 8f)))firstHitAt=clock;
            if(firstHitAt>=0&&clock>=firstHitAt+BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.6", 0.25f))
            {
                if(Once(1,f>=BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter4", 11f))){move.DashVelocity=Face*3;LaterAction(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.7", 0.15f),()=>move.DashVelocity=0);}
                Window(1,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter5", 11f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter6", 14f)),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.8", 12f),new Vector2(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.9", 1.6f),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.10", 0.9f)),new Vector2(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.11", 1.1f),-BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.12", 0.1f)));
            }
        }
        else if(Skill==1&&Once(0,f>=BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter7", 5f)))Later(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.13", 0.7f),mark+Vector2.up*BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.14", 1.1f),new Vector2(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.15", 0.7f),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.16", 2.2f)),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter8", 18f),1);
        else if(Skill==2&&Once(0,Elapsed>=BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.17", 0.8f)))
        {
            if(move.SafeSpot(anchor,out var destination))
            {FX(2,Position+Vector2.up,BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.18", 1.2f),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.19", 0.4f));move.Teleport(destination);recoveryUntil=clock+BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.20", 0.5f);}
        }
    }
    protected override void EventResolved(int tag){if(tag==1){SeamResolutions++;FX(0,mark+Vector2.up, BalanceRuntime.Number(this, "RiftBladeCombat.EventResolved.1", 0.8f),BalanceRuntime.Number(this, "RiftBladeCombat.EventResolved.2", 0.3f),Color.red);}}
    protected override void OnFinished(int i){if(i==0&&!BoxHit(1))recoveryUntil=Mathf.Max(recoveryUntil,clock+BalanceRuntime.Number(this, "RiftBladeCombat.OnFinished.1", 0.45f));}
}

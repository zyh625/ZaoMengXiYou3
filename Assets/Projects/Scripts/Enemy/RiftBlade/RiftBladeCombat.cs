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
        if(i==1)mark=Ground(Position+Vector2.right*Face*BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.1", 2.5f));
        if(i==2)
        {
            anchor=Ground(Position+Vector2.right*Face*BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.4", 3f));
            Vector2 origin=Ground(Position);
            FX(1,(origin+anchor)*0.5f,Mathf.Max(0.5f,Mathf.Abs(anchor.x-origin.x)),BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.7", 0.9f));
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
        else if(Skill==1&&Once(0,f>=BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter7", 5f)))
        {
            FX(0,mark+Vector2.up*BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.14", 1.1f),BalanceRuntime.Number(this, "RiftBladeCombat.OnBegin.2", 0.7f),1f);//16帧/16fps，约0.7秒合拢时正处于红色刃口
            Later(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.13", 0.7f),mark+Vector2.up*BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.14", 1.1f),new Vector2(BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.15", 0.7f),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.16", 2.2f)),BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.Parameter8", 18f),1);
        }
        else if(Skill==2&&Once(0,Elapsed>=BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.17", 0.8f)))
        {
            if(move.SafeSpot(anchor,out var destination))
            {FX(2,Position,BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.18", 2.5f),1f);move.Teleport(destination);recoveryUntil=clock+BalanceRuntime.Number(this, "RiftBladeCombat.OnFrame.20", 0.5f);}
        }
    }
    protected override void EventResolved(int tag){if(tag==1)SeamResolutions++;}//合拢的红色刃口已包含在折痕特效里
    protected override void OnFinished(int i){if(i==0&&!BoxHit(1))recoveryUntil=Mathf.Max(recoveryUntil,clock+BalanceRuntime.Number(this, "RiftBladeCombat.OnFinished.1", 0.45f));}
}

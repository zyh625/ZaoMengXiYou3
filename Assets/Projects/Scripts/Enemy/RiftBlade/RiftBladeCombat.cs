using UnityEngine;

public sealed class RiftBladeCombat : LateStageCombat
{
    [SerializeField] private float[] cooldowns={4f,8f,9f};
    private Vector2 mark,anchor;private float firstHitAt;
    public int SeamResolutions {get;private set;}
    protected override float[] Cooldowns=>cooldowns;
    protected override int SelectSkill(float d)=>d<=2.8f&&Ready(0)?0:d<=7&&Ready(1)?1:d<=9&&Ready(2)?2:-1;
    protected override void ResetActor(){SeamResolutions=0;firstHitAt=-1;}
    protected override void OnBegin(int i)
    {
        firstHitAt=-1;
        if(i==1){mark=Ground(Position+Vector2.right*Face*2.5f);FX(0,mark+Vector2.up, .7f,.7f,Color.yellow);}
        if(i==2)
        {
            anchor=Ground(Position+Vector2.right*Face*3f);
            FX(1,Ground(Position)+Vector2.up*.2f,.8f,.9f,Color.yellow);FX(1,anchor+Vector2.up*.2f,.8f,.9f,Color.yellow);
            minSkillEnd=clock+1.3f;
        }
    }
    public override bool HoldFrame(int frame)=>Skill==0&&frame>=10&&firstHitAt>=0&&clock<firstHitAt+.25f || base.HoldFrame(frame);
    protected override void OnFrame(int f)
    {
        if(Skill==0)
        {
            Window(0,f,5,8,10,new Vector2(1.8f,1.1f),new Vector2(1.1f,.3f));
            if(Once(0,f>=8))firstHitAt=clock;
            if(firstHitAt>=0&&clock>=firstHitAt+.25f)
            {
                if(Once(1,f>=11)){move.DashVelocity=Face*3;LaterAction(.15f,()=>move.DashVelocity=0);}
                Window(1,f,11,14,12,new Vector2(1.6f,.9f),new Vector2(1.1f,-.1f));
            }
        }
        else if(Skill==1&&Once(0,f>=5))Later(.7f,mark+Vector2.up*1.1f,new Vector2(.7f,2.2f),18,1);
        else if(Skill==2&&Once(0,Elapsed>=.8f))
        {
            if(move.SafeSpot(anchor,out var destination))
            {FX(2,Position+Vector2.up,1.2f,.4f);move.Teleport(destination);recoveryUntil=clock+.5f;}
        }
    }
    protected override void EventResolved(int tag){if(tag==1){SeamResolutions++;FX(0,mark+Vector2.up, .8f,.3f,Color.red);}}
    protected override void OnFinished(int i){if(i==0&&!BoxHit(1))recoveryUntil=Mathf.Max(recoveryUntil,clock+.45f);}
}

using UnityEngine;

public sealed class SealWardenCombat : LateStageCombat
{
    [SerializeField] private float[] cooldowns={4.5f,12f,15f};
    [SerializeField] private LateStageDestructible wedgePrefab;
    private LateStageDestructible left,right;private Transform creation;
    private float punchAt;private Vector2 center;private bool compressing;
    public bool Compressing=>compressing;
    public bool HasWedges=>left!=null&&right!=null&&left.gameObject.activeSelf&&right.gameObject.activeSelf;
    public int CompressionCount {get;private set;}
    protected override float[] Cooldowns=>cooldowns;
    protected override int SelectSkill(float d)=>HasWedges&&Ready(2)?2:d<=BalanceRuntime.Number(this, "SealWardenCombat.SelectSkill.1", 2.7f)&&Ready(0)?0:!HasWedges&&d<=7&&Ready(1)?1:-1;
    protected override void ResetActor(){punchAt=-1;CompressionCount=0;compressing=false;}
    protected override void OnBegin(int i)
    {
        punchAt=-1;
        if(i==1){center=Ground(Position+Vector2.right*Face*BalanceRuntime.Number(this, "SealWardenCombat.OnBegin.1", 3f));FX(0,center,4,BalanceRuntime.Number(this, "SealWardenCombat.OnBegin.2", 0.8f),Color.yellow);minSkillEnd=clock+1f;}
        if(i==2){compressing=true;minSkillEnd=clock+BalanceRuntime.Number(this, "SealWardenCombat.OnBegin.3", 1.1f);FX(1,center+Vector2.up*BalanceRuntime.Number(this, "SealWardenCombat.OnBegin.4", 0.5f),4,BalanceRuntime.Number(this, "SealWardenCombat.OnBegin.5", 0.8f),Color.yellow);}
    }
    public override bool HoldFrame(int f)=>Skill==0&&f>=BalanceRuntime.Number(this, "SealWardenCombat.HoldFrame.Parameter1", 10f)&&punchAt>=0&&clock<punchAt+BalanceRuntime.Number(this, "SealWardenCombat.HoldFrame.1", 0.5f) || base.HoldFrame(f);
    protected override void OnFrame(int f)
    {
        if(Skill==0)
        {
            Window(0,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.Parameter1", 6f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.Parameter2", 8f)),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.1", 16f),new Vector2(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.2", 2f),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.3", 1.7f)),new Vector2(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.4", 1.2f),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.5", 0.85f)),true);
            if(Once(0,f>=BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.Parameter3", 6f))){punchAt=clock;FX(2,Position+new Vector2(Face*BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.6", 1.4f),1),2,BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.7", 0.4f));}
            if(punchAt>=0&&clock>=punchAt+BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.8", 0.5f))Window(1,f,Mathf.RoundToInt(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.Parameter4", 11f)),Mathf.RoundToInt(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.Parameter5", 14f)),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.9", 20f),new Vector2(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.10", 2f),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.11", 1.7f)),new Vector2(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.12", 1.2f),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.13", 0.85f)));
        }
        else if(Skill==1&&Once(0,Elapsed>=BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.14", 0.8f)))Deploy();
        else if(Skill==2)
        {
            // Jumping above the low wedges cancels compression, not an airborne hit.
            if(!HasWedges || Target!=null&&Target.GetComponent<BoxCollider2D>().bounds.min.y>center.y+BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.15", 1.05f)){BreakCompression(true);return;}
            if(Elapsed>=BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.16", 0.8f)&&HasWedges)
            {
                float t=Mathf.Clamp01((Elapsed-BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.17", 0.8f))/BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.18", 0.25f));
                left.transform.position=Vector2.Lerp(center+Vector2.left*BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.19", 1.8f),center+Vector2.left*BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.20", 0.6f),t);
                right.transform.position=Vector2.Lerp(center+Vector2.right*BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.21", 1.8f),center+Vector2.right*BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.22", 0.6f),t);
            }
            if(Once(0,Elapsed>=BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.23", 1.05f)))
            {
                compressing=false;CompressionCount++;Area(center+Vector2.up*BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.24", 0.55f),new Vector2(BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.25", 3.6f),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.26", 1.1f)),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.27", 14f),0,true);
                FX(1,center+Vector2.up*BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.28", 0.55f),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.29", 3.6f),BalanceRuntime.Number(this, "SealWardenCombat.OnFrame.30", 0.5f),Color.red);ClearWedges();
            }
        }
    }
    private LateStageDestructible NewWedge()
    {
        if(creation==null){creation=new GameObject("WedgeCreation").transform;creation.SetParent(transform,false);creation.gameObject.SetActive(false);}
        var w=Instantiate(wedgePrefab,creation);w.gameObject.SetActive(false);w.transform.SetParent(null,true);return w;
    }
    private void Deploy()
    {
        if(!move.SafeSpot(center+Vector2.left*BalanceRuntime.Number(this, "SealWardenCombat.Deploy.1", 1.8f),out var a)||!move.SafeSpot(center+Vector2.right*BalanceRuntime.Number(this, "SealWardenCombat.Deploy.2", 1.8f),out var b))return;
        ClearWedges();if(left==null)left=NewWedge();if(right==null)right=NewWedge();
        left.Activate(a,BalanceRuntime.Number(this, "SealWardenCombat.Deploy.3", 3f),LostWedge);right.Activate(b,BalanceRuntime.Number(this, "SealWardenCombat.Deploy.4", 3f),LostWedge);
    }
    private void LostWedge(bool broken){BreakCompression(broken);ClearWedges();}
    private void BreakCompression(bool stagger)
    {if(stagger)recoveryUntil=clock+BalanceRuntime.Number(this, "SealWardenCombat.BreakCompression.1", 1.5f);compressing=false;if(Skill==2)CancelSkill();}
    private void ClearWedges(){if(left!=null)left.Dismiss();if(right!=null)right.Dismiss();}
    protected override void OnCancelled(int i){if(i==2)compressing=false;}
    protected override void StopActor(){compressing=false;ClearWedges();}
    private void OnDestroy(){if(left!=null)Destroy(left.gameObject);if(right!=null)Destroy(right.gameObject);}
}

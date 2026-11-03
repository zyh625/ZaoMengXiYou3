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
    protected override int SelectSkill(float d)=>HasWedges&&Ready(2)?2:d<=2.7f&&Ready(0)?0:!HasWedges&&d<=7&&Ready(1)?1:-1;
    protected override void ResetActor(){punchAt=-1;CompressionCount=0;compressing=false;}
    protected override void OnBegin(int i)
    {
        punchAt=-1;
        if(i==1){center=Ground(Position+Vector2.right*Face*3f);FX(0,center,4,.8f,Color.yellow);minSkillEnd=clock+1f;}
        if(i==2){compressing=true;minSkillEnd=clock+1.1f;FX(1,center+Vector2.up*.5f,4,.8f,Color.yellow);}
    }
    public override bool HoldFrame(int f)=>Skill==0&&f>=10&&punchAt>=0&&clock<punchAt+.5f || base.HoldFrame(f);
    protected override void OnFrame(int f)
    {
        if(Skill==0)
        {
            Window(0,f,6,8,16,new Vector2(2,1.7f),new Vector2(1.2f,.85f),true);
            if(Once(0,f>=6)){punchAt=clock;FX(2,Position+new Vector2(Face*1.4f,1),2,.4f);}
            if(punchAt>=0&&clock>=punchAt+.5f)Window(1,f,11,14,20,new Vector2(2,1.7f),new Vector2(1.2f,.85f));
        }
        else if(Skill==1&&Once(0,Elapsed>=.8f))Deploy();
        else if(Skill==2)
        {
            // Jumping above the low wedges cancels compression, not an airborne hit.
            if(!HasWedges || Target!=null&&Target.GetComponent<BoxCollider2D>().bounds.min.y>center.y+1.05f){BreakCompression(true);return;}
            if(Elapsed>=.8f&&HasWedges)
            {
                float t=Mathf.Clamp01((Elapsed-.8f)/.25f);
                left.transform.position=Vector2.Lerp(center+Vector2.left*1.8f,center+Vector2.left*.6f,t);
                right.transform.position=Vector2.Lerp(center+Vector2.right*1.8f,center+Vector2.right*.6f,t);
            }
            if(Once(0,Elapsed>=1.05f))
            {
                compressing=false;CompressionCount++;Area(center+Vector2.up*.55f,new Vector2(3.6f,1.1f),14,0,true);
                FX(1,center+Vector2.up*.55f,3.6f,.5f,Color.red);ClearWedges();
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
        if(!move.SafeSpot(center+Vector2.left*1.8f,out var a)||!move.SafeSpot(center+Vector2.right*1.8f,out var b))return;
        ClearWedges();if(left==null)left=NewWedge();if(right==null)right=NewWedge();
        left.Activate(a,3f,LostWedge);right.Activate(b,3f,LostWedge);
    }
    private void LostWedge(bool broken){BreakCompression(broken);ClearWedges();}
    private void BreakCompression(bool stagger)
    {if(stagger)recoveryUntil=clock+1.5f;compressing=false;if(Skill==2)CancelSkill();}
    private void ClearWedges(){if(left!=null)left.Dismiss();if(right!=null)right.Dismiss();}
    protected override void OnCancelled(int i){if(i==2)compressing=false;}
    protected override void StopActor(){compressing=false;ClearWedges();}
    private void OnDestroy(){if(left!=null)Destroy(left.gameObject);if(right!=null)Destroy(right.gameObject);}
}

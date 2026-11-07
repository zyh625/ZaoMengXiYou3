using UnityEngine;

[DefaultExecutionOrder(110)]
public class LateStageMovement : EnemyMovement
{
    [SerializeField] private bool hovering;
    [SerializeField] private float hoverHeight=1.6f;
    private Health hp; private int life=-1;
    public float FloorY {get;private set;}
    public bool Hovering=>hovering;
    public float Altitude {get;set;}
    public float DashVelocity {get;set;}
    public void Prepare()
    {
        if(hp==null)hp=GetComponent<Health>();
        if(life==hp.LifeVersion)return;life=hp.LifeVersion;Altitude=DashVelocity=0f;
        FloorY=FindFloor(transform.position);if(hovering)Altitude=hoverHeight;
    }
    public static float FindFloor(Vector2 at)
    {var hit=Physics2D.Raycast(at+Vector2.up*BalanceRuntime.Number(typeof(LateStageMovement), "LateStageMovement.FindFloor.1", 8f),Vector2.down,BalanceRuntime.Number(typeof(LateStageMovement), "LateStageMovement.FindFloor.2", 60f),1<<6);return hit.collider!=null?hit.point.y:at.y;}
    public bool SafeSpot(Vector2 desired,out Vector2 result,bool checkActors=true,Transform other=null)
    {
        Prepare();
        var shape=GetComponent<BoxCollider2D>();
        //按碰撞箱底边贴地计算落点：本体轴心在身体中间的敌人（如裂隙刃侍）不能把轴心放在地面上，否则会半截埋进地里卡住
        float feet=(shape.offset.y-shape.size.y*0.5f)*transform.lossyScale.y;
        result=new Vector2(desired.x,FindFloor(desired)+BalanceRuntime.Number(this, "LateStageMovement.SafeSpot.1", 0.03f)+(hovering?hoverHeight:0f)-feet);
        var floor=Physics2D.Raycast(new Vector2(result.x,result.y+5),Vector2.down,30,1<<6);
        if(floor.collider==null)return false;
        Vector2 center=result+Vector2.Scale(shape.offset,transform.lossyScale);
        Vector2 size=Vector2.Scale(shape.size,new Vector2(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y)));
        var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(1<<6);
        var hits=new Collider2D[16];int n=Physics2D.OverlapBox(center,size*BalanceRuntime.Number(this, "LateStageMovement.SafeSpot.2", 0.92f),0,filter,hits);
        float ground=result.y+feet;
        for(int i=0;i<n;i++)if(hits[i]!=null && hits[i].bounds.max.y>ground+BalanceRuntime.Number(this, "LateStageMovement.SafeSpot.3", 0.15f))return false;
        if(checkActors)
        {
            //按所属角色判断是不是自己，不用 transform.root（训练场里敌人共用同一个父物体）
            var self=GetComponent<EnemyController>();
            n=Physics2D.OverlapBox(center,size,0,new ContactFilter2D{useTriggers=false},hits);
            for(int i=0;i<n;i++)
            {
                if(hits[i]==null)continue;
                var enemy=hits[i].GetComponentInParent<EnemyController>();
                if(enemy!=null&&(enemy==self||other!=null&&enemy.transform==other))continue;
                if(enemy!=null||hits[i].GetComponentInParent<PlayerController>()!=null)return false;
            }
        }
        return true;
    }
    //SafeSpot 给出的是本体轴心位置；需要地面点（如放置楔子）时用它换算回脚底
    public Vector2 FeetPoint(Vector2 actorPosition){var shape=GetComponent<BoxCollider2D>();return actorPosition+Vector2.up*(shape.offset.y-shape.size.y*0.5f)*transform.lossyScale.y;}
    public void Teleport(Vector2 at){transform.position=at;body.position=at;body.linearVelocity=Vector2.zero;FloorY=FindFloor(at);}
    public void Chase(float direction,bool slow)=>MoveAlong(direction,slow);
    private readonly RaycastHit2D[] castHits=new RaycastHit2D[12];
    private void FixedUpdate()
    {
        Prepare();if(hp.Dead){DashVelocity=0;body.bodyType=RigidbodyType2D.Dynamic;body.gravityScale=1;return;}
        if(IsExternallyControlled||hp.Status!=null&&hp.Status.BlocksMovement)return;
        var c=GetComponent<LateStageCombat>();
        if(c!=null)
        {
            float x=c.ClampArenaX(body.position.x,GetComponent<BoxCollider2D>().bounds.extents.x+BalanceRuntime.Number(this, "LateStageMovement.FixedUpdate.1", 0.05f));
            if(x!=body.position.x){body.position=new Vector2(x,body.position.y);body.linearVelocityX=0;}
        }
        if(c!=null&&(c.Skill>=0||c.SpecialActive||c.Recovering))Stop();
        if(Altitude>0f){body.gravityScale=0;body.linearVelocityY=Mathf.Clamp((FloorY+Altitude-body.position.y)*12,-15,15);}
        else if(!hovering)body.gravityScale=1;
        if(DashVelocity!=0)
        {
            float dx=DashVelocity*Time.fixedDeltaTime;
            var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(1<<6);
            int n=GetComponent<BoxCollider2D>().Cast(Vector2.right*Mathf.Sign(dx),filter,castHits,Mathf.Abs(dx)+BalanceRuntime.Number(this, "LateStageMovement.FixedUpdate.2", 0.02f));
            for(int i=0;i<n;i++)if(castHits[i].normal.x*Mathf.Sign(dx)<-BalanceRuntime.Number(this, "LateStageMovement.FixedUpdate.3", 0.1f))dx=Mathf.Sign(dx)*Mathf.Min(Mathf.Abs(dx),Mathf.Max(0,castHits[i].distance-BalanceRuntime.Number(this, "LateStageMovement.FixedUpdate.4", 0.02f)));
            Vector2 destination=body.position+Vector2.right*dx;
            if(c!=null)destination.x=c.ClampArenaX(destination.x,GetComponent<BoxCollider2D>().bounds.extents.x+BalanceRuntime.Number(this, "LateStageMovement.FixedUpdate.5", 0.05f));
            body.position=destination;body.linearVelocityX=0;
        }
    }
}

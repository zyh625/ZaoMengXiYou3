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
    {var hit=Physics2D.Raycast(at+Vector2.up*8f,Vector2.down,60f,1<<6);return hit.collider!=null?hit.point.y:at.y;}
    public bool SafeSpot(Vector2 desired,out Vector2 result,bool checkActors=true,Transform other=null)
    {
        Prepare();result=new Vector2(desired.x,FindFloor(desired)+.03f+(hovering?hoverHeight:0f));
        var floor=Physics2D.Raycast(new Vector2(result.x,result.y+5),Vector2.down,30,1<<6);
        if(floor.collider==null)return false;
        var shape=GetComponent<BoxCollider2D>();Vector2 center=result+shape.offset;
        var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(1<<6);
        var hits=new Collider2D[16];int n=Physics2D.OverlapBox(center,shape.size*.92f,0,filter,hits);
        for(int i=0;i<n;i++)if(hits[i]!=null && hits[i].bounds.max.y>result.y+.15f)return false;
        if(checkActors)
        {
            n=Physics2D.OverlapBox(center,shape.size,0,new ContactFilter2D{useTriggers=false},hits);
            for(int i=0;i<n;i++)if(hits[i].transform.root!=transform.root && (other==null||hits[i].transform.root!=other.root)
                && (hits[i].GetComponentInParent<PlayerController>()!=null||hits[i].GetComponentInParent<EnemyController>()!=null))return false;
        }
        return true;
    }
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
            float x=c.ClampArenaX(body.position.x,GetComponent<BoxCollider2D>().bounds.extents.x+.05f);
            if(x!=body.position.x){body.position=new Vector2(x,body.position.y);body.linearVelocityX=0;}
        }
        if(c!=null&&(c.Skill>=0||c.SpecialActive||c.Recovering))Stop();
        if(Altitude>0f){body.gravityScale=0;body.linearVelocityY=Mathf.Clamp((FloorY+Altitude-body.position.y)*12,-15,15);}
        else if(!hovering)body.gravityScale=1;
        if(DashVelocity!=0)
        {
            float dx=DashVelocity*Time.fixedDeltaTime;
            var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(1<<6);
            int n=GetComponent<BoxCollider2D>().Cast(Vector2.right*Mathf.Sign(dx),filter,castHits,Mathf.Abs(dx)+.02f);
            for(int i=0;i<n;i++)if(castHits[i].normal.x*Mathf.Sign(dx)<-.1f)dx=Mathf.Sign(dx)*Mathf.Min(Mathf.Abs(dx),Mathf.Max(0,castHits[i].distance-.02f));
            Vector2 destination=body.position+Vector2.right*dx;
            if(c!=null)destination.x=c.ClampArenaX(destination.x,GetComponent<BoxCollider2D>().bounds.extents.x+.05f);
            body.position=destination;body.linearVelocityX=0;
        }
    }
}

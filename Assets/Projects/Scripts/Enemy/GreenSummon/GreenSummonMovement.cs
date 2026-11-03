using UnityEngine;
using zmxy.GamePlay;

//继承EnemyMovement的私有Unity初始化；短后撤仍接受公共受击与强控制。
public sealed class GreenSummonMovement : EnemyMovement
{
    [SerializeField] private float retreatDistance=3f,retreatCooldown=6f,retreatDuration=0.25f;
    private readonly RaycastHit2D[] casts=new RaycastHit2D[24];
    private readonly Collider2D[] overlaps=new Collider2D[24];
    private CameraManager cameraManager;
    private bool customBounds;
    private Vector2 limits;
    private float destination,direction,readyAt;
    private int life=-1;
    public bool IsRetreating { get; private set; }
    public float RetreatReadyAt=>readyAt;
    public void SetRetreatBounds(float left,float right){limits=new Vector2(left,right);customBounds=true;}
    public void MoveInRange(float delta,bool retreat,bool slow)=>MoveAlong((delta>=0f?1f:-1f)*(retreat?-1f:1f),slow);
    public void CancelRetreat(){IsRetreating=false;body??=GetComponent<Rigidbody2D>();body.linearVelocityX=0f;}
    public bool TryRetreat(float delta)
    {
        Health hp=GetComponent<Health>();
        if(hp!=null && hp.LifeVersion!=life){life=hp.LifeVersion;readyAt=0f;IsRetreating=false;}
        if(IsRetreating || Time.time<readyAt || Mathf.Abs(delta)>=3f || hp==null || hp.Dead || hp.Attacked
            || IsExternallyControlled || hp.Status!=null && hp.Status.BlocksMovement || !IsOnGroundNow())return false;
        Bounds shape=BodyCollider.bounds;direction=delta>=0f?-1f:1f;destination=body.position.x+direction*retreatDistance;
        Vector2 center=(Vector2)shape.center+Vector2.right*(direction*retreatDistance);
        Vector2 bounds=new(float.NegativeInfinity,float.PositiveInfinity);
        if(customBounds)bounds=limits;
        else
        {
            if(cameraManager==null && Camera.main!=null)cameraManager=Camera.main.GetComponent<CameraManager>();
            if(cameraManager!=null && cameraManager.CameraHalf>0f)bounds=new Vector2(cameraManager.EdgeL-cameraManager.CameraHalf,cameraManager.EdgeR+cameraManager.CameraHalf);
        }
        if(center.x-shape.extents.x<bounds.x || center.x+shape.extents.x>bounds.y)return false;
        ContactFilter2D filter=new();filter.SetLayerMask((1<<6)|(1<<11));filter.useTriggers=false;
        int count=BodyCollider.Cast(Vector2.right*direction,filter,casts,retreatDistance+0.025f);
        for(int i=0;i<count;i++)if(Vector2.Dot(casts[i].normal,Vector2.right*direction)<-0.01f)return false;
        count=Physics2D.OverlapBox(center,(Vector2)shape.size-Vector2.one*0.04f,0f,filter,overlaps);
        for(int i=0;i<count;i++)if(overlaps[i]!=BodyCollider && overlaps[i].attachedRigidbody!=body)return false;
        //整条3u路径均有同层地面，避免跨过缺口后虽然终点有地却在中途坠落。
        for(float advance=0.5f;advance<=retreatDistance+0.01f;advance+=0.5f)
        for(int side=-1;side<=1;side+=2)
        {
            float probeX=shape.center.x+direction*advance+side*(shape.extents.x-0.04f);
            RaycastHit2D floor=Physics2D.Raycast(new Vector2(probeX,shape.min.y+0.15f),Vector2.down,0.35f,1<<6);
            if(floor.collider==null || floor.normal.y<0.6f || Mathf.Abs(floor.point.y-shape.min.y)>0.15f)return false;
        }
        IsRetreating=true;readyAt=Time.time+retreatCooldown;body.linearVelocityX=direction*retreatDistance/retreatDuration;return true;
    }
    public void StepRetreat()
    {
        if(!IsRetreating)return;
        float speed=retreatDistance/retreatDuration,remaining=(destination-body.position.x)*direction;
        ContactFilter2D filter=new();filter.SetLayerMask((1<<6)|(1<<11));filter.useTriggers=false;
        int count=BodyCollider.Cast(Vector2.right*direction,filter,casts,Mathf.Max(0f,Mathf.Min(remaining,speed*Time.fixedDeltaTime))+0.025f);
        for(int i=0;i<count;i++)if(Vector2.Dot(casts[i].normal,Vector2.right*direction)<-0.01f){CancelRetreat();return;}
        if(remaining<=speed*Time.fixedDeltaTime+0.001f)
        {
            Vector2 position=body.position;position.x=destination;transform.position=new Vector3(position.x,position.y,transform.position.z);body.position=position;
            CancelRetreat();Physics2D.SyncTransforms();return;
        }
        body.linearVelocityX=direction*speed;
    }
}

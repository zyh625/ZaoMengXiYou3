using UnityEngine;

//不声明Awake/OnEnable/OnDisable，保留EnemyMovement的私有Unity初始化与控制清理。
public sealed class GreenHammerMovement : EnemyMovement
{
    [SerializeField] private float leapUpSpeed=5f,leapAcceleration=28f,maxLeapAdvance=3f;
    [SerializeField] private float spawnGravity=0.9f;
    private readonly RaycastHit2D[] casts=new RaycastHit2D[24];
    private float savedGravity,leapAt;
    private bool gravitySaved,restoreAfterControl;
    public bool IsLeaping { get; private set; }
    public bool HasLanded { get; private set; }
    public bool LeapTimedOut { get; private set; }
    public void PrepareCast(){HasLanded=LeapTimedOut=false;}
    public void ResetLeap()
    {
        body??=GetComponent<Rigidbody2D>();body.gravityScale=spawnGravity;
        IsLeaping=HasLanded=LeapTimedOut=gravitySaved=restoreAfterControl=false;
    }
    public void BeginLeap(float delta)
    {
        savedGravity=body.gravityScale;gravitySaved=true;restoreAfterControl=false;
        float duration=2f*leapUpSpeed/leapAcceleration;
        float direction=delta>=0f?1f:-1f;
        float advance=Mathf.Clamp(Mathf.Abs(delta)-1.6f,0f,maxLeapAdvance);
        Bounds bounds=BodyCollider.bounds;
        ContactFilter2D filter=new();filter.SetLayerMask((1<<6)|(1<<11));filter.useTriggers=false;
        int count=BodyCollider.Cast(Vector2.right*direction,filter,casts,advance+0.025f);
        for(int i=0;i<count;i++)if(Vector2.Dot(casts[i].normal,Vector2.right*direction)<-0.1f)advance=Mathf.Min(advance,Mathf.Max(0f,casts[i].distance-0.025f));
        RaycastHit2D ground=Physics2D.Raycast(new Vector2(bounds.center.x+direction*advance,bounds.min.y+1f),Vector2.down,2f,1<<6);
        if(ground.collider==null || ground.normal.y<0.6f)advance=0f;
        body.gravityScale=0f;body.linearVelocity=new Vector2(direction*advance/duration,leapUpSpeed);
        IsLeaping=true;HasLanded=LeapTimedOut=false;leapAt=Time.time;
    }
    public void StepLeap()
    {
        if(!IsLeaping)return;
        if(Time.time-leapAt>1.2f){LeapTimedOut=true;CancelLeap();return;}
        Vector2 velocity=body.linearVelocity;velocity.y-=leapAcceleration*Time.fixedDeltaTime;
        if(Mathf.Abs(velocity.x)>0.01f)
        {
            ContactFilter2D filter=new();filter.SetLayerMask((1<<6)|(1<<11));filter.useTriggers=false;
            Vector2 direction=velocity.x>0f?Vector2.right:Vector2.left;
            int count=BodyCollider.Cast(direction,filter,casts,Mathf.Abs(velocity.x)*Time.fixedDeltaTime+0.025f);
            for(int i=0;i<count;i++)if(Vector2.Dot(casts[i].normal,direction)<-0.1f){velocity.x=0f;break;}
        }
        if(velocity.y<=0f)
        {
            Bounds bounds=BodyCollider.bounds;
            RaycastHit2D floor=Physics2D.Raycast(new Vector2(bounds.center.x,bounds.min.y+0.04f),Vector2.down,-velocity.y*Time.fixedDeltaTime+0.08f,1<<6);
            if(floor.collider!=null && floor.normal.y>=0.6f)
            {
                Vector2 position=body.position;position.y+=floor.point.y-bounds.min.y+0.015f;
                transform.position=new Vector3(position.x,position.y,transform.position.z);body.position=position;body.linearVelocity=Vector2.zero;
                IsLeaping=false;HasLanded=true;RestoreGravity();Physics2D.SyncTransforms();return;
            }
        }
        body.linearVelocity=velocity;
    }
    public void CancelLeap()
    {
        if(IsLeaping)body.linearVelocity=Vector2.zero;
        IsLeaping=false;
        if(!gravitySaved)return;
        //携带/吸附可能已保存了本次跃起的零重力；等公共控制释放后再恢复原重力。
        if(IsExternallyControlled){restoreAfterControl=true;return;}
        RestoreGravity();
    }
    private void RestoreGravity(){if(gravitySaved)body.gravityScale=savedGravity;gravitySaved=restoreAfterControl=false;}
    public void RestoreGravityAfterControl()
    {
        //Falling仍算外部控制，但此时必须恢复正常重力；主动吸附/携带仍保留各自的零重力。
        if(restoreAfterControl && !IsCarried && !IsStunned && !IsSuctionActive && !(GetComponent<Health>().Status?.BlocksMovement??false))RestoreGravity();
    }
}

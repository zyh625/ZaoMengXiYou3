using UnityEngine;

//先剥离上一物理步的本来源速度，玩家输入/跳跃/技能随后照常运行；Boss在1000顺序追加本步拉速。
[DefaultExecutionOrder(-500)]
public sealed class NorthVortexPull : MonoBehaviour
{
    private Rigidbody2D body;
    private Collider2D shape;
    private Health health;
    private PlayerController player;
    private ActorStun stun;
    private float added,result;
    private readonly RaycastHit2D[] hits=new RaycastHit2D[16];
    public bool IsPulling {get;private set;}
    private void Awake(){body=GetComponent<Rigidbody2D>();shape=GetComponent<BoxCollider2D>();health=GetComponent<Health>();player=GetComponent<PlayerController>();stun=GetComponent<ActorStun>();}
    private void FixedUpdate()=>RemovePrevious();
    private void RemovePrevious()
    {
        //地面摩擦造成的小幅速度变化仍属上一拉速；碰撞或其他技能明显重写速度时不反向补偿。
        if(body!=null && added!=0f && Mathf.Abs(body.linearVelocityX-result)<0.25f)body.linearVelocityX-=added;
        added=0f;IsPulling=false;
    }
    public void Apply(Vector2 center)
    {
        if(health==null || health.Dead || !player.isActiveAndEnabled || !player.CanReceiveHit || health.Attacked || player.State()==PlayerState.Attacked
            || health.Status!=null && health.Status.BlocksMovement || stun!=null && stun.IsStunned || health.IsInvincible)return;
        RemovePrevious();float delta=center.x-body.position.x;
        float pull=Mathf.Clamp(delta/Time.fixedDeltaTime,-2.5f,2.5f),before=body.linearVelocityX,total=before+pull;
        if(Mathf.Abs(total)>0.001f)
        {
            ContactFilter2D filter=new();filter.SetLayerMask(1<<6);filter.useTriggers=false;Vector2 dir=total>0f?Vector2.right:Vector2.left;
            int count=shape.Cast(dir,filter,hits,Mathf.Abs(total)*Time.fixedDeltaTime+0.025f);
            for(int i=0;i<count;i++)if(Vector2.Dot(hits[i].normal,dir)<-0.01f)
                total=dir.x*Mathf.Min(Mathf.Abs(total),Mathf.Max(0f,hits[i].distance-0.025f)/Time.fixedDeltaTime);
        }
        body.linearVelocityX=total;added=total-before;result=total;IsPulling=added!=0f;
    }
    private void OnDisable()=>RemovePrevious();
}

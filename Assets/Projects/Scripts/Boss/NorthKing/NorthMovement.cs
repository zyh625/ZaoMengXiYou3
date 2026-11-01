using UnityEngine;

//保留EnemyMovement的私有Unity初始化；小跳使用真实重力，不更改公共控制或重力接口。
public sealed class NorthMovement : EnemyMovement
{
    private float jumpedAt;
    public bool IsRainJumping {get;private set;}
    public bool RainLanded {get;private set;}
    public void PrepareRain(){RainLanded=false;}
    public void BeginRainJump()
    {if(IsRainJumping)return;body.linearVelocity=new Vector2(0f,3.2f);jumpedAt=Time.time;IsRainJumping=true;RainLanded=false;}
    public void StepRainJump()
    {
        if(!IsRainJumping)return;
        if(Time.time-jumpedAt>0.1f && body.linearVelocityY<=0.1f && IsOnGroundNow())
        {IsRainJumping=false;RainLanded=true;Stop();}
    }
    public void CancelRainJump()
    {if(IsRainJumping)body.linearVelocity=new Vector2(0f,Mathf.Min(0f,body.linearVelocityY));IsRainJumping=false;}
}

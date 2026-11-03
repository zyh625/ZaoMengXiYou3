using System.Collections.Generic;
using UnityEngine;

public sealed class WestKingFlame : MonoBehaviour
{
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private SpriteData stream,ground,burst;
    [SerializeField] private float length=7f,nativeWidth=2f;
    private readonly Collider2D[] hits=new Collider2D[32];
    private readonly HashSet<Health> unique=new(8);
    private WestCombat owner;
    private Health sourceHealth;
    private int sourceLife;
    private Vector2 anchor;
    private bool pulse,fire,right;
    private float began,streamEnd,end,nextTick,scale;
    public int StreamTicks { get; private set; }
    public int GroundTicks { get; private set; }
    public bool IsGroundFire=>fire && !pulse;
    public bool IsPulse=>pulse;
    public void Launch(WestCombat source,Vector2 position,bool faceRight,bool onlyVisual,float radius)
    {
        owner=source;sourceHealth=source.GetComponent<Health>();sourceLife=sourceHealth.LifeVersion;scale=source.DamageMultiplier;
        anchor=position;right=faceRight;pulse=onlyVisual;fire=false;StreamTicks=GroundTicks=0;
        began=Time.time;streamEnd=began+2f;end=pulse?began+0.4f:streamEnd+4f;nextTick=began+0.5f;
        transform.position=pulse?position:position+new Vector2((right?1f:-1f)*length/2f,0.65f);
        visual.transform.localScale=new Vector3((pulse?radius*2f:length)/nativeWidth,pulse?0.8f:1.4f,1f);
        visual.flipX=right;gameObject.SetActive(true);spriteManager.ChangeState(pulse?burst:stream);
    }
    public void StopStream()
    {
        if(pulse || fire || !gameObject.activeSelf || Time.time>=streamEnd)return;
        //被普通受击/护珠打断时结束喷吐，已经落地的火仍保留四秒。
        streamEnd=Mathf.Min(streamEnd,Time.time);end=streamEnd+4f;nextTick=Time.time+0.5f;StartGround();
    }
    private void StartGround()
    {
        fire=true;visual.transform.localScale=new Vector3(length/nativeWidth,0.65f,1f);
        transform.position=anchor+new Vector2((right?1f:-1f)*length/2f,0.3f);spriteManager.ChangeState(ground);
    }
    private void FixedUpdate()
    {
        if(owner==null || sourceHealth==null || sourceHealth.Dead || !sourceHealth.isActiveAndEnabled || sourceHealth.LifeVersion!=sourceLife || !BattleRun.IsRunning)
        {gameObject.SetActive(false);return;}
        if(pulse){if(Time.time>=end)gameObject.SetActive(false);return;}
        while(nextTick<=end+0.0001f && Time.time>=nextTick)
        {
            bool streamTick=nextTick<=streamEnd+0.0001f;nextTick+=0.5f;
            if(streamTick)StreamTicks++;else GroundTicks++;
            //地面箱允许跳跃离开；持续阶段每个Health每半秒只结算一次。
            Vector2 center=anchor+new Vector2((right?1f:-1f)*length/2f,streamTick?0.65f:0.3f);
            WestHitUtility.Area(owner,center,new Vector2(length,streamTick?1.3f:0.6f),0f,10f*scale,true,hits,unique);
            if(!gameObject.activeSelf)return;
        }
        if(!fire && Time.time>=streamEnd)StartGround();
        if(Time.time>=end)gameObject.SetActive(false);
    }
    private void OnDisable(){spriteManager.Pause();unique.Clear();owner=null;sourceHealth=null;}
}

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
        began=Time.time;streamEnd=began+BalanceRuntime.Number(this, "WestKingFlame.Launch.1", 2f);end=pulse?began+BalanceRuntime.Number(this, "WestKingFlame.Launch.2", 0.4f):streamEnd+BalanceRuntime.Number(this, "WestKingFlame.Launch.3", 4f);nextTick=began+BalanceRuntime.Number(this, "WestKingFlame.Launch.4", 0.5f);
        transform.position=pulse?position:position+new Vector2((right?1f:-1f)*length/BalanceRuntime.Number(this, "WestKingFlame.Launch.5", 2f),BalanceRuntime.Number(this, "WestKingFlame.Launch.6", 0.65f));
        visual.transform.localScale=new Vector3((pulse?radius*BalanceRuntime.Number(this, "WestKingFlame.Launch.7", 2f):length)/nativeWidth,pulse?BalanceRuntime.Number(this, "WestKingFlame.Launch.8", 0.8f):BalanceRuntime.Number(this, "WestKingFlame.Launch.9", 1.4f),1f);
        visual.flipX=right;gameObject.SetActive(true);spriteManager.ChangeState(pulse?burst:stream);
    }
    public void StopStream()
    {
        if(pulse || fire || !gameObject.activeSelf || Time.time>=streamEnd)return;
        //被普通受击/护珠打断时结束喷吐，已经落地的火仍保留四秒。
        streamEnd=Mathf.Min(streamEnd,Time.time);end=streamEnd+BalanceRuntime.Number(this, "WestKingFlame.StopStream.1", 4f);nextTick=Time.time+BalanceRuntime.Number(this, "WestKingFlame.StopStream.2", 0.5f);StartGround();
    }
    private void StartGround()
    {
        fire=true;visual.transform.localScale=new Vector3(length/nativeWidth,BalanceRuntime.Number(this, "WestKingFlame.StartGround.1", 0.65f),1f);
        transform.position=anchor+new Vector2((right?1f:-1f)*length/BalanceRuntime.Number(this, "WestKingFlame.StartGround.2", 2f),BalanceRuntime.Number(this, "WestKingFlame.StartGround.3", 0.3f));spriteManager.ChangeState(ground);
    }
    private void FixedUpdate()
    {
        if(owner==null || sourceHealth==null || sourceHealth.Dead || !sourceHealth.isActiveAndEnabled || sourceHealth.LifeVersion!=sourceLife || !BattleRun.IsRunning)
        {gameObject.SetActive(false);return;}
        if(pulse){if(Time.time>=end)gameObject.SetActive(false);return;}
        while(nextTick<=end+BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.1", 0.0001f) && Time.time>=nextTick)
        {
            bool streamTick=nextTick<=streamEnd+BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.2", 0.0001f);nextTick+=BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.3", 0.5f);
            if(streamTick)StreamTicks++;else GroundTicks++;
            //地面箱允许跳跃离开；持续阶段每个Health每半秒只结算一次。
            Vector2 center=anchor+new Vector2((right?1f:-1f)*length/BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.4", 2f),streamTick?BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.5", 0.65f):BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.6", 0.3f));
            //喷吐阶段是正常命中（有受击反应，不击退）；落地火焰按持续灼烧结算
            WestHitUtility.Area(owner,center,new Vector2(length,streamTick?BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.7", 1.3f):BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.8", 0.6f)),0f,BalanceRuntime.Number(this, "WestKingFlame.FixedUpdate.9", 10f)*scale,!streamTick,hits,unique);
            if(!gameObject.activeSelf)return;
        }
        if(!fire && Time.time>=streamEnd)StartGround();
        if(Time.time>=end)gameObject.SetActive(false);
    }
    private void OnDisable(){spriteManager.Pause();unique.Clear();owner=null;sourceHealth=null;}
}

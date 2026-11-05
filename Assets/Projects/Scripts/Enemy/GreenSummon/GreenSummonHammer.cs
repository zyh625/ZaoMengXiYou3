using System.Collections.Generic;
using UnityEngine;

public sealed class GreenSummonHammer : MonoBehaviour
{
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteData warning,fall,impact;
    [SerializeField] private LineRenderer warningCircle;
    [SerializeField] private float warningDuration=1.2f;
    private static readonly HashSet<GreenSummonHammer> active=new();
    private readonly Collider2D[] hits=new Collider2D[32];
    private readonly HashSet<Health> unique=new(8);
    private Health sourceHealth;
    private int sourceLife;
    private float strikeAt,expires,scale;
    private bool committed,falling;
    public bool HasStruck { get; private set; }
    public Vector2 Position { get; private set; }
    public float StrikeAt=>strikeAt;
    public static bool HasWarningAt(Vector2 point)
    {foreach(GreenSummonHammer h in active)if(h!=null && h.gameObject.activeInHierarchy && !h.HasStruck && (h.Position-point).sqrMagnitude<=BalanceRuntime.Number(typeof(GreenSummonHammer), "GreenSummonHammer.HasWarningAt.1", 6.25f))return true;return false;}
    public void Warn(GreenSummonCombat source,Vector2 position)
    {
        sourceHealth=source.GetComponent<Health>();sourceLife=sourceHealth.LifeVersion;
        Position=position;transform.position=position;strikeAt=Time.time+warningDuration;HasStruck=committed=falling=false;expires=float.PositiveInfinity;
        gameObject.SetActive(true);active.Add(this);warningCircle.enabled=true;spriteManager.ChangeState(warning);
    }
    public void Commit(float multiplier){committed=true;scale=multiplier;}
    private void FixedUpdate()
    {
        if(!BattleRun.IsRunning){Cancel();return;}
        //原16帧拆为2帧预警循环、5帧下落、9帧落地消散；下落提前播放，1.2秒落地才结算。
        if(!HasStruck && committed && !falling && Time.time>=strikeAt-fall.sprites.Length/fall.fps)
        {falling=true;spriteManager.ChangeState(fall);}
        if(!HasStruck && committed && Time.time>=strikeAt)
        {
            HasStruck=true;warningCircle.enabled=false;spriteManager.ChangeState(impact);expires=Time.time+impact.sprites.Length/impact.fps;
            Transform source=sourceHealth!=null && sourceHealth.LifeVersion==sourceLife?sourceHealth.transform:transform;
            GreenSummonCombat.ResolveHammer(source,Position,scale,hits,unique);
        }
        if(Time.time>=expires)Cancel();
    }
    public void Cancel(){if(gameObject.activeSelf)gameObject.SetActive(false);}
    private void OnDisable(){active.Remove(this);warningCircle.enabled=false;spriteManager.Pause();sourceHealth=null;unique.Clear();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetActive()=>active.Clear();
}

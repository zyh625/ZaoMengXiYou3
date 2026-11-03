using UnityEngine;

//8192条循环缓冲足够容纳训练3秒窗口；更新与记录不创建每帧临时集合。
public sealed class TrainingStatistics
{
    private struct Hit {public float time;public double damage;}
    private readonly Hit[] history=new Hit[8192];
    private int head,count;
    private double window;
    private float started=-1f;
    public double TotalDamage {get;private set;}
    public int Hits {get;private set;}
    public int CriticalHits {get;private set;}
    public float CriticalRate=>Hits>0?(float)CriticalHits/Hits:0f;
    public double MaximumHit {get;private set;}
    public float KillTime {get;private set;}=-1f;
    public double Dps {get{Prune(Time.time);return window/3d;}}
    public float Elapsed=>started<0f?0f:KillTime>=0f?KillTime:Time.time-started;
    public void Record(DamageInfo info,float damage)
    {
        if(damage<=0f || info.IsReflected || info.IsPeriodic || info.Source==null || info.Source.GetComponentInParent<PlayerController>(true)==null)return;
        float now=Time.time;Prune(now);if(started<0f)started=now;
        if(count==history.Length){window-=history[head].damage;head=(head+1)%history.Length;count--;}
        history[(head+count)%history.Length]=new Hit{time=now,damage=damage};count++;window+=damage;TotalDamage+=damage;Hits++;
        if(info.IsCritical)CriticalHits++;MaximumHit=System.Math.Max(MaximumHit,damage);
    }
    private void Prune(float now)
    {while(count>0 && now-history[head].time>=3f){window-=history[head].damage;head=(head+1)%history.Length;count--;}if(count==0)window=0d;}
    public void Died(){if(started>=0f && KillTime<0f)KillTime=Time.time-started;}
    public void Reset(){head=count=0;window=TotalDamage=MaximumHit=0d;Hits=CriticalHits=0;started=KillTime=-1f;}
}

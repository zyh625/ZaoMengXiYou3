using System.Collections.Generic;
using UnityEngine;

//EnemyController/EnemyMovement使现有玩家近战与远程索敌都能攻击幻影；自身永不追击或近战。
public sealed class WestKingOrb : EnemyController
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private LineRenderer ray;
    private readonly Collider2D[] hits=new Collider2D[32];
    private readonly HashSet<Health> unique=new(8);
    private WestCombat owner;
    private Vector2 start,end;
    private float remaining,flash;
    private int warningFrame;
    public bool IsWarning { get; private set; }
    public int Shots { get; private set; }
    public bool Alive=>gameObject.activeInHierarchy && EH!=null && !EH.Dead;
    protected override bool UsesAnger=>false;
    protected override bool CanStartNormalAttack=>false;
    protected override void Awake(){base.Awake();EH.Died+=Broken;}
    public void Spawn(WestCombat source,Vector2 position)
    {
        owner=source;transform.position=position;GetComponent<Rigidbody2D>().position=position;
        IsWarning=false;flash=0f;Shots=0;ray.enabled=false;gameObject.SetActive(true);
        EH.SetSuperArmor(this,true); //静止幻影不受普通击退改变位置，强控制仍遵循公共规则。
    }
    public void Warn()
    {
        PlayerController player=owner.Target;if(!Alive || player==null)return;
        start=muzzle.position;Vector2 target=player.GetComponent<Collider2D>().bounds.center;
        Vector2 direction=(target-start).normalized;end=start+direction*BalanceRuntime.Number(this, "WestKingOrb.Warn.1", 20f);
        RaycastHit2D wall=Physics2D.Raycast(start,direction,BalanceRuntime.Number(this, "WestKingOrb.Warn.2", 20f),1<<6);if(wall.collider!=null)end=wall.point-direction*BalanceRuntime.Number(this, "WestKingOrb.Warn.3", 0.02f);
        remaining=BalanceRuntime.Number(this, "WestKingOrb.Warn.4", 0.8f);warningFrame=Time.frameCount;IsWarning=true;flash=0f;
        ray.startWidth=ray.endWidth=BalanceRuntime.Number(this, "WestKingOrb.Warn.5", 0.035f);ray.startColor=ray.endColor=new Color(1f,BalanceRuntime.Number(this, "WestKingOrb.Warn.6", 0.1f),BalanceRuntime.Number(this, "WestKingOrb.Warn.7", 0.02f),BalanceRuntime.Number(this, "WestKingOrb.Warn.8", 0.95f));
        ray.SetPosition(0,start);ray.SetPosition(1,end);ray.enabled=true;
    }
    private void Update()
    {
        if(owner==null || !owner.IsWarding || !Alive){IsWarning=false;ray.enabled=false;return;}
        //与Boss护珠计时一致：固体冰冻暂停预警，融化阶段继续。
        if(owner.WardTimerPaused || EH.Status!=null && EH.Status.BlocksActions)return;
        if(IsWarning)
        {
            if(Time.frameCount==warningFrame)return; //不能把起手前本帧已经流逝的时间计入0.8秒预警。
            remaining-=Time.deltaTime;if(remaining>0f)return;
            IsWarning=false;Shots++;flash=BalanceRuntime.Number(this, "WestKingOrb.Update.1", 0.15f);ray.startWidth=ray.endWidth=BalanceRuntime.Number(this, "WestKingOrb.Update.2", 0.18f);
            Vector2 direction=end-start;float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            int count=Physics2D.OverlapBox((start+end)/BalanceRuntime.Number(this, "WestKingOrb.Update.3", 2f),new Vector2(direction.magnitude,BalanceRuntime.Number(this, "WestKingOrb.Update.4", 0.4f)),angle,WestHitUtility.Players,hits);unique.Clear();
            float scale=owner.DamageMultiplier;
            for(int i=0;i<count;i++)
            {
                Health victim=WestHitUtility.Player(hits[i]);if(victim==null || !unique.Add(victim))continue;
                float before=victim.CurBlood;victim.TakeDamage(new DamageInfo(owner.transform,BalanceRuntime.Number(this, "WestKingOrb.Update.5", 12f)*scale,false,false,null,false,false,DamageKind.Skill));
                if(victim.CurBlood<before && !victim.Dead)WestKingBurn.Apply(victim,owner,scale);
            }
        }
        else if(flash>0f){flash-=Time.deltaTime;if(flash<=0f)ray.enabled=false;}
    }
    protected override bool ProcessSpecialState()
    {
        if(EH.Status!=null && EH.Status.BlocksActions)return false;
        GetComponent<EnemyMovement>().Stop();return true;
    }
    private void Broken(DamageInfo damage){owner?.OrbBroken(this);gameObject.SetActive(false);}
    protected override void OnDisable(){IsWarning=false;if(ray!=null)ray.enabled=false;EH?.SetSuperArmor(this,false);base.OnDisable();}
    private void OnDestroy(){if(EH!=null)EH.Died-=Broken;}
}

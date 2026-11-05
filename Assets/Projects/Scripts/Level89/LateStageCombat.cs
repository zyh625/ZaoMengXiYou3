using System;
using System.Collections.Generic;
using UnityEngine;

// Infrastructure used only by the five new L8/L9 actors; existing base semantics stay intact.
public abstract class LateStageCombat : EnemyCombat,IEnemyArenaBounds
{
    [SerializeField] protected BoxCollider2D[] skillBoxes=Array.Empty<BoxCollider2D>();
    [SerializeField] protected LateStageEffectPool[] effectPools=Array.Empty<LateStageEffectPool>();
    [SerializeField] protected float attackInterval=1f;
    protected Health hp;protected LateStageMovement move;
    protected int ResolvingTag;
    protected float clock,skillStart,nextAttack,recoveryUntil,minSkillEnd;
    protected readonly float[] readyAt=new float[8];
    protected readonly bool[] released=new bool[16];
    private readonly HashSet<Health>[] boxHits={new(),new(),new()};
    private readonly float[] boxDamage=new float[3];private readonly bool[] boxBack=new bool[3];
    private readonly float[] boxRadius=new float[3];
    private readonly Collider2D[] overlap=new Collider2D[64];private readonly RaycastHit2D[] rayHits=new RaycastHit2D[64];
    private readonly HashSet<Health> eventHits=new();
    protected readonly List<Scheduled> events=new(32);
    private readonly List<Shot> shots=new(32);
    private PlayerManager players;private PlayerController fallback;
    protected struct Scheduled
    {public float Due,Damage,Radius;public Vector2 Position,Size;public bool Back,Periodic,HasGuard;public int Tag;public Health Guard;public int Life;public Action Action;}
    private struct Shot{public Vector2 At,Velocity;public float Remaining,Damage;public LateStageEffect Fx;}
    protected float arenaLeft=-12,arenaRight=12;
    private bool arenaBound;
    private zmxy.GamePlay.CameraManager arenaCamera;
    public int Skill {get;protected set;}=-1;
    public virtual bool IsBoss=>false;
    public virtual bool SpecialActive=>false;
    public virtual int SpecialClip=>-1;
    public int SpecialVersion {get;protected set;}
    public virtual float SpecialFraction=>0f;
    public bool Recovering=>clock<recoveryUntil;
    public bool Frozen=>hp.Status!=null&&hp.Status.BlocksActions || move.IsExternallyControlled;
    protected bool OrdinaryInterrupted=>hp.Attacked&&!hp.Boost;
    public float Elapsed=>clock-skillStart;
    protected float Face=>AttackFaceRight?1:-1;
    public Vector2 Position=>transform.position;
    public PlayerController Target
    {
        get{if(players==null)players=FindAnyObjectByType<PlayerManager>();
            if(players!=null)return players.CurrentPlayer;
            if(fallback==null||!fallback.isActiveAndEnabled)fallback=FindAnyObjectByType<PlayerController>();return fallback;}
    }
    public void BindTarget(PlayerController player){fallback=player;players=null;if(player!=null)move.SetTarget(player.transform);}
    protected abstract float[] Cooldowns{get;}
    protected abstract int SelectSkill(float distance);
    protected override bool UsesAnger=>false;
    public int ReadySkill=>hp!=null&&!hp.Dead&&!Frozen&&!OrdinaryInterrupted&&!Recovering&&!SpecialActive&&Skill<0&&clock>=nextAttack&&Target!=null
        ?SelectSkill(Mathf.Abs(Target.transform.position.x-transform.position.x)):-1;
    protected bool Ready(int i)=>clock>=readyAt[i];
    public void SetArenaBounds(float left,float right){arenaLeft=Mathf.Min(left,right);arenaRight=Mathf.Max(left,right);arenaBound=true;}
    public float ClampArenaX(float x,float margin=.7f)
    {
        if(arenaBound)return Mathf.Clamp(x,arenaLeft+margin,arenaRight-margin);
        if(arenaCamera==null&&Camera.main!=null)arenaCamera=Camera.main.GetComponent<zmxy.GamePlay.CameraManager>();
        if(arenaCamera!=null&&arenaCamera.CameraHalf>margin)
            return Mathf.Clamp(x,arenaCamera.CameraCenter-arenaCamera.CameraHalf+margin,arenaCamera.CameraCenter+arenaCamera.CameraHalf-margin);
        return x;
    }
    protected override void Awake(){base.Awake();hp=GetComponent<Health>();move=GetComponent<LateStageMovement>();}
    protected override void OnEnable()
    {
        base.OnEnable();clock=skillStart=nextAttack=recoveryUntil=minSkillEnd=0;Skill=-1;SpecialVersion=0;
        Array.Clear(readyAt,0,readyAt.Length);Array.Clear(released,0,released.Length);CloseBoxes();
        hp.RemoveDamageMultiplier(this);hp.RemoveInvincibility(this);hp.SetSuperArmor(this,false);
        hp.DamageResolved+=DamageResolved;hp.Died+=Died;ResetActor();
    }
    protected virtual void ResetActor(){}
    protected virtual void DamageResolved(DamageInfo info,float actual){}
    public bool BeginSkill(int i)
    {
        if(i<0||i>=Cooldowns.Length||!Ready(i)||Skill>=0||SpecialActive||Recovering||Frozen||hp.Dead||OrdinaryInterrupted||Target==null||clock<nextAttack)return false;
        Skill=i;skillStart=clock;minSkillEnd=clock;Array.Clear(released,0,released.Length);
        for(int j=0;j<boxHits.Length;j++)boxHits[j].Clear();
        readyAt[i]=clock+Cooldowns[i]*RebirthCooldownMultiplier;AttackFaceRight=Target.transform.position.x>=transform.position.x;
        hp.Status?.NotifyAttackStarted();move.Stop();OnBegin(i);return true;
    }
    protected virtual void OnBegin(int i){}
    public void SampleFrame(int frame){if(Skill>=0&&!hp.Dead&&!Frozen&&!OrdinaryInterrupted)OnFrame(frame);}
    protected abstract void OnFrame(int frame);
    public virtual bool HoldFrame(int frame)=>Skill>=0&&frame>=BalanceRuntime.Number(this, "LateStageCombat.HoldFrame.Parameter1", 15f)&&clock<minSkillEnd;
    public virtual void FinishSkill(){if(Skill<0)return;int old=Skill;CloseBoxes();Skill=-1;move.DashVelocity=0;nextAttack=clock+attackInterval*RebirthCooldownMultiplier;OnFinished(old);}
    protected virtual void OnFinished(int i){}
    public virtual void CancelSkill()
    {
        if(Skill>=0){OnCancelled(Skill);Skill=-1;nextAttack=clock+BalanceRuntime.Number(this, "LateStageCombat.CancelSkill.1", 0.2f);events.Clear();}
        CloseBoxes();if(move!=null)move.DashVelocity=0;
    }
    protected virtual void OnCancelled(int i){}
    protected bool Once(int flag,bool condition){if(!condition||released[flag])return false;released[flag]=true;return true;}
    protected void Window(int index,int frame,int first,int last,float damage,Vector2 size,Vector2 offset,bool back=false,float radius=0)
    {
        if(index>=skillBoxes.Length)return;
        var box=skillBoxes[index];box.size=size;box.offset=new Vector2(offset.x*Face,offset.y);
        boxDamage[index]=damage;boxBack[index]=back;boxRadius[index]=radius;box.gameObject.SetActive(frame>=first&&frame<=last);
        if(!box.gameObject.activeSelf)return;
        Physics2D.SyncTransforms();int n=Physics2D.OverlapBox(box.bounds.center,box.bounds.size,0,new ContactFilter2D{useTriggers=true},overlap);
        for(int j=0;j<n;j++)BoxContact(index,overlap[j]);
    }
    public void BoxContact(int index,Collider2D other)
    {
        if(Skill<0||index>=skillBoxes.Length||!skillBoxes[index].gameObject.activeSelf||Frozen||hp.Dead)return;
        var p=other.GetComponentInParent<PlayerController>();if(!Valid(p))return;var h=p.GetComponent<Health>();
        if(boxHits[index].Contains(h))return;
        if(boxRadius[index]>0&&Vector2.Distance(p.transform.position,transform.position)>boxRadius[index])return;
        float before=h.CurBlood;var attack=skillBoxes[index].GetComponent<EnemyAttackBox>();
        if(attack!=null)
        {
            var d=attack.CreateDamageInfo(transform);
            h.TakeDamage(new DamageInfo(transform,d.Amount*HitFactor(p),d.Knockback,d.Launch,d.Effects,false,false,DamageKind.Skill));
        }
        else Hit(p,boxDamage[index],boxBack[index]);
        if(h.CurBlood<before)boxHits[index].Add(h);
    }
    protected bool BoxHit(int i)=>boxHits[i].Count>0;
    protected void CloseBoxes(){if(skillBoxes!=null)foreach(var box in skillBoxes)if(box!=null)box.gameObject.SetActive(false);}
    protected bool Valid(PlayerController p)=>p!=null&&p.gameObject.activeInHierarchy&&p.CanReceiveHit&&!p.GetComponent<Health>().Dead;
    protected virtual float HitFactor(PlayerController p)=>1f;
    protected void Hit(PlayerController p,float amount,bool back=false,bool periodic=false)
    {if(Valid(p))p.GetComponent<Health>().TakeDamage(new DamageInfo(transform,amount*DamageMultiplier*HitFactor(p),back,false,null,false,periodic,DamageKind.Skill));}
    protected int Area(Vector2 at,Vector2 size,float damage,float radius=0,bool back=false,bool periodic=false,float inner=0)
    {
        Physics2D.SyncTransforms();eventHits.Clear();int count=0;
        int n=Physics2D.OverlapBox(at,size,0,new ContactFilter2D{useTriggers=true},overlap);
        for(int i=0;i<n;i++)
        {
            var p=overlap[i].GetComponentInParent<PlayerController>();if(!Valid(p))continue;var h=p.GetComponent<Health>();
            if(!eventHits.Add(h))continue;Vector2 d=(Vector2)p.transform.position-at;
            if(radius>0&&(d.sqrMagnitude>radius*radius||d.sqrMagnitude<inner*inner))continue;
            float before=h.CurBlood;Hit(p,damage,back,periodic);if(h.CurBlood<before)count++;
        }
        return count;
    }
    protected void Later(float delay,Vector2 at,Vector2 size,float damage,int tag=0,float radius=0,bool back=false,Health guard=null)
    {events.Add(new Scheduled{Due=clock+delay,Position=at,Size=size,Damage=damage,Radius=radius,Back=back,Tag=tag,Guard=guard,HasGuard=guard!=null,Life=guard!=null?guard.LifeVersion:0});minSkillEnd=Mathf.Max(minSkillEnd,clock+delay+BalanceRuntime.Number(this, "LateStageCombat.Later.1", 0.05f));}
    protected void LaterAction(float delay,Action action){events.Add(new Scheduled{Due=clock+delay,Action=action});minSkillEnd=Mathf.Max(minSkillEnd,clock+delay+BalanceRuntime.Number(this, "LateStageCombat.LaterAction.1", 0.05f));}
    protected virtual void EventResolved(int tag){}
    protected LateStageEffect FX(int index,Vector2 at,float width,float duration,Color? tint=null)
    {return index>=0&&index<effectPools.Length&&effectPools[index]!=null?effectPools[index].Play(at,width,duration,AttackFaceRight,tint??Color.white):null;}
    protected void Fire(Vector2 at,Vector2 velocity,float damage,int effect,float life=2f)
    {shots.Add(new Shot{At=at,Velocity=velocity,Damage=damage,Remaining=life,Fx=FX(effect,at,BalanceRuntime.Number(this, "LateStageCombat.Fire.1", 0.65f),life)});}
    protected Vector2 Ground(Vector2 at)=>new(ClampArenaX(at.x),LateStageMovement.FindFloor(at)+BalanceRuntime.Number(this, "LateStageCombat.Ground.1", 0.03f));
    private void Update()
    {
        if(hp.Dead)return;
        // All special timers and cast telegraphs pause under strong control (freeze/carry).
        BeforeTick();if(Frozen)return;clock+=Time.deltaTime;move.Prepare();
        if(OrdinaryInterrupted&&Skill>=0)CancelSkill();
        TickActor(Time.deltaTime);
        for(int i=events.Count-1;i>=0;i--)
        {
            if(i>=events.Count)continue;var e=events[i];if(e.Due>clock)continue;events.RemoveAt(i);
            if(e.HasGuard&&(e.Guard==null||e.Guard.Dead||e.Guard.LifeVersion!=e.Life||!e.Guard.isActiveAndEnabled))continue;
            if(e.Action!=null)e.Action();else{ResolvingTag=e.Tag;Area(e.Position,e.Size,e.Damage,e.Radius,e.Back,e.Periodic);ResolvingTag=0;EventResolved(e.Tag);}
            if(hp.Dead)break;
        }
        for(int i=shots.Count-1;i>=0;i--)
        {
            if(i>=shots.Count)break;var s=shots[i];Vector2 delta=s.Velocity*Time.deltaTime;bool end=false;
            int n=Physics2D.CircleCast(s.At,BalanceRuntime.Number(this, "LateStageCombat.Update.1", 0.12f),delta.normalized,new ContactFilter2D{useTriggers=true},rayHits,delta.magnitude);
            for(int j=0;j<n;j++)
            {
                var hit=rayHits[j];if(hit.collider.gameObject.layer==6&&!hit.collider.isTrigger){end=true;break;}
                var p=hit.collider.GetComponentInParent<PlayerController>();if(Valid(p)){Hit(p,s.Damage);end=true;break;}
            }
            if(hp.Dead||i>=shots.Count)break;
            s.At+=delta;s.Remaining-=Time.deltaTime;if(s.Fx!=null)s.Fx.transform.position=s.At;
            if(end||s.Remaining<=0){if(s.Fx!=null)s.Fx.gameObject.SetActive(false);shots.RemoveAt(i);}else shots[i]=s;
        }
    }
    protected virtual void BeforeTick(){}
    protected virtual void TickActor(float dt){}
    protected virtual void StopActor(){}
    private void Died(DamageInfo info)=>StopAll();
    private void StopAll()
    {
        CancelSkill();events.Clear();shots.Clear();StopActor();
        if(hp!=null){hp.RemoveDamageMultiplier(this);hp.RemoveInvincibility(this);hp.SetSuperArmor(this,false);}
        if(effectPools!=null)foreach(var pool in effectPools)if(pool!=null)pool.ReleaseAll();
    }
    protected virtual void OnDisable(){if(hp!=null){hp.DamageResolved-=DamageResolved;hp.Died-=Died;}StopAll();}
}

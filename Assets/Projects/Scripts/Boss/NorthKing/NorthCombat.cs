using System;
using System.Collections.Generic;
using UnityEngine;
using zmxy.GamePlay;

[DefaultExecutionOrder(1000)]
public sealed class NorthCombat : BossCombat,IEnemyArenaBounds
{
    public enum GuardPhase {Normal,Guarding,Exposed}
    [SerializeField] private NorthBashBox bash;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float attackInterval=2.2f;
    [SerializeField] private float[] cooldowns={4f,9f,8f,12f};
    private static readonly int[] releaseFrames={5,4,7,11};
    private readonly float[] readyAt=new float[4];
    private readonly Dictionary<Health,(int life,float ready)> vortexHits=new(4);
    private Health health;
    private NorthMovement movement;
    private NorthEffects effects;
    private PlayerManager players;
    private PlayerController fallback;
    private float resolveAt,nextAttack,guardElapsed,nextGuardVolley,exposedRemaining,channelUntil;
    private bool released,arenaSet,jumped;
    private int latched,pending;
    private Vector2 arena;
    public event Action GuardChanged;
    public GuardPhase Phase {get;private set;}
    public bool IsGuarding=>Phase==GuardPhase.Guarding;
    public bool IsSpecial=>Phase!=GuardPhase.Normal;
    public bool GuardFaceRight {get;private set;}
    public float Durability {get;private set;}
    public float GuardRemaining=>IsGuarding?Mathf.Max(0f,6f-guardElapsed):0f;
    public float ExposureRemaining=>Phase==GuardPhase.Exposed?Mathf.Max(0f,exposedRemaining):0f;
    public int GuardStarts {get;private set;}
    public int GuardVolleys {get;private set;}
    public int CurrentSkill {get;private set;}=-1;
    public bool Released=>released;
    public bool Channeling=>CurrentSkill==1 && released && Time.time<channelUntil-0.0001f;
    public float NextAttackAt=>nextAttack;
    public Vector2 Arena=>arena;
    public bool GuardTimerPaused=>health!=null && (health.Status!=null && health.Status.BlocksActions || movement.IsExternallyControlled);
    public PlayerController Target
    {
        get
        {
            if(players!=null && players.isActiveAndEnabled)return players.CurrentPlayer;
            if(fallback!=null && fallback.isActiveAndEnabled && !fallback.GetComponent<Health>().Dead)return fallback;
            if(Time.time<resolveAt)return null;resolveAt=Time.time+0.5f;players=FindAnyObjectByType<PlayerManager>();
            if(players!=null && players.isActiveAndEnabled)return players.CurrentPlayer;
            foreach(PlayerController p in FindObjectsByType<PlayerController>())if(p.isActiveAndEnabled && !p.GetComponent<Health>().Dead){fallback=p;return p;}return null;
        }
    }
    public void BindTarget(PlayerController target){fallback=target;players=null;}
    public void SetArenaBounds(float left,float right){arena=new Vector2(Mathf.Min(left,right),Mathf.Max(left,right));arenaSet=true;}
    private bool CanAct=>health!=null && !health.Dead && !health.Attacked && !IsSpecial && !movement.IsExternallyControlled
        && !(health.Status!=null && health.Status.BlocksActions);
    public int ReadyAttack
    {
        get
        {
            PlayerController target=Target;if(CurrentSkill>=0 || !CanAct || target==null || Time.time<nextAttack)return -1;
            float distance=Mathf.Abs(target.transform.position.x-transform.position.x);
            if(health.ratio<=0.4f && distance<=14f && Time.time>=readyAt[3])return 3;
            if(distance<3f && Time.time>=readyAt[1])return 1;
            if(distance is >=3f and <=5f && Time.time>=readyAt[0])return 0;
            if(distance is >=3f and <=12f && Time.time>=readyAt[2])return 2;
            if(distance<=6f && Time.time>=readyAt[1])return 1;
            if(distance<=5f && Time.time>=readyAt[0])return 0;
            return distance<=14f && Time.time>=readyAt[3]?3:-1;
        }
    }
    protected override void Awake(){base.Awake();health=GetComponent<Health>();movement=GetComponent<NorthMovement>();effects=GetComponent<NorthEffects>();}
    protected override void OnEnable()
    {
        base.OnEnable();Phase=GuardPhase.Normal;CurrentSkill=-1;latched=pending=GuardStarts=GuardVolleys=0;Durability=guardElapsed=exposedRemaining=0f;
        released=jumped=false;nextAttack=Time.time;vortexHits.Clear();effects.ResetCounters();
        for(int i=0;i<4;i++)readyAt[i]=Time.time;
        health.DamagePreparing+=PrepareHit;health.DamageReceived+=OnDamage;health.DamageResolved+=ResolveHit;health.Died+=Died;GuardChanged?.Invoke();
    }
    public bool BeginAttack(int skill)
    {
        PlayerController target=Target;
        if(skill<0 || skill>3 || CurrentSkill>=0 || !CanAct || target==null || Time.time<nextAttack || Time.time<readyAt[skill])return false;
        CurrentSkill=skill;readyAt[skill]=Time.time+cooldowns[skill]*RebirthCooldownMultiplier;AttackFaceRight=target.transform.position.x>=transform.position.x;released=jumped=false;
        movement.PrepareRain();vortexHits.Clear();health.Status?.NotifyAttackStarted();return true;
    }
    private Vector2 Muzzle(bool right)=>(Vector2)transform.position+new Vector2((right?1f:-1f)*Mathf.Abs(muzzle.localPosition.x),muzzle.localPosition.y);
    public void SampleFrame(int frame)
    {
        if(CurrentSkill<0)return;if(!CanAct){CancelAttack();return;}
        if(CurrentSkill==3)
        {
            if(frame>=3 && !jumped){jumped=true;movement.BeginRainJump();}
            movement.StepRainJump();if(!movement.RainLanded)return;
        }
        if(CurrentSkill==0 && released && frame>=9)bash.gameObject.SetActive(false);
        if(released || frame<releaseFrames[CurrentSkill])return;released=true;
        switch(CurrentSkill)
        {
            case 0:
                BoxCollider2D box=bash.GetComponent<BoxCollider2D>();box.offset=new Vector2(Mathf.Abs(box.offset.x)*(AttackFaceRight?1f:-1f),box.offset.y);
                bash.gameObject.SetActive(true);Physics2D.SyncTransforms();bash.Sample();break;
            case 1:channelUntil=Time.time+2f;break;
            case 2:effects.Fan(this,Muzzle(AttackFaceRight),AttackFaceRight,5);break;
            case 3:EnsureArena();effects.Rain(this,arena);break;
        }
    }
    private void FixedUpdate()
    {
        if(!Channeling || !CanAct || !BattleRun.IsRunning)return;
        PlayerController player=Target;if(player==null || !player.CanReceiveHit)return;
        Vector2 center=Muzzle(AttackFaceRight),position=player.transform.position;
        if((position-center).sqrMagnitude>36f)return;
        NorthVortexPull pull=player.GetComponent<NorthVortexPull>();if(pull==null)pull=player.gameObject.AddComponent<NorthVortexPull>();pull.Apply(center);
        Health hp=player.GetComponent<Health>();Collider2D shape=player.GetComponent<BoxCollider2D>();
        if(((Vector2)shape.ClosestPoint(center)-center).sqrMagnitude>2.25f)return;
        if(vortexHits.TryGetValue(hp,out var hit) && hit.life==hp.LifeVersion && Time.time<hit.ready)return;
        vortexHits[hp]=(hp.LifeVersion,Time.time+0.4f);hp.TakeDamage(new DamageInfo(transform,8f*DamageMultiplier,false,false,null,false,false,DamageKind.Skill));
    }
    public void CancelAttack()
    {
        if(CurrentSkill>=0)nextAttack=Time.time+attackInterval*RebirthCooldownMultiplier;
        if(bash!=null)bash.gameObject.SetActive(false);movement?.CancelRainJump();CurrentSkill=-1;released=jumped=false;vortexHits.Clear();
    }
    private void EnsureArena()
    {
        if(arenaSet)return;CameraManager cm=Camera.main!=null?Camera.main.GetComponent<CameraManager>():null;
        arena=cm!=null?new Vector2(cm.EdgeL-cm.CameraHalf,cm.EdgeR+cm.CameraHalf):new Vector2(transform.position.x-10f,transform.position.x+10f);
    }
    private bool Front(DamageInfo damage)
    {if(damage.Source==null)return false;float delta=damage.Source.position.x-transform.position.x;return Mathf.Abs(delta)<0.001f || delta>0f==GuardFaceRight;}
    private void PrepareHit(DamageInfo damage)
    {
        if(!IsGuarding)return;
        bool front=!damage.IsPeriodic && damage.Amount>0f && Front(damage);
        if(front)
        {
            health.SetDamageMultiplier(this,0.1f);health.SetReflection(this,0.2f);
            Durability=Mathf.Max(0f,Durability-damage.Amount);GuardChanged?.Invoke(); //150耐久扣减减伤前原始伤害；破伞这一击仍只让Boss吃10%。
        }
        else{health.RemoveDamageMultiplier(this);health.RemoveReflection(this);}
    }
    private void OnDamage(DamageInfo damage,float actual)
    {
        if(health.Dead)return;
        if(health.ratio<=0.7f && (latched&1)==0){latched|=1;pending|=1;}
        if(health.ratio<=0.4f && (latched&2)==0){latched|=2;pending|=2;}
    }
    private void ResolveHit(DamageInfo damage,float actual)
    {
        if(health.Dead){Cleanup();return;}
        if(IsGuarding)
        {
            //DamageResolved在Health完成20%反伤之后；不提前撤销破伞这一击的反伤，也不把触发阈值的旧命中反弹。
            health.RemoveDamageMultiplier(this);health.RemoveReflection(this);if(Durability<=0f)BreakGuard();
        }
        if(Phase==GuardPhase.Normal && pending!=0)BeginGuard();
    }
    private void BeginGuard()
    {
        if(pending==0 || health.Dead)return;
        pending&=pending==3?2:0;CancelAttack();movement.Stop();Phase=GuardPhase.Guarding;Durability=150f;guardElapsed=0f;nextGuardVolley=2f;GuardStarts++;
        PlayerController target=Target;GuardFaceRight=target==null?AttackFaceRight:target.transform.position.x>=transform.position.x;
        health.SetSuperArmor(this,true);effects.ShowShield(this);GuardChanged?.Invoke();
    }
    private void Update()
    {
        if(health==null || health.Dead)return;
        if(Phase==GuardPhase.Normal){if(pending!=0)BeginGuard();return;}
        if(Phase==GuardPhase.Exposed)
        {exposedRemaining-=Time.deltaTime;if(exposedRemaining<=0f)Restore();else GuardChanged?.Invoke();return;}
        //明确选择暂停：固体冰冻及携带/吸附等外控同时暂停护伞与2秒弹幕计时；3秒破伞易伤按游戏时间固定计时。
        if(GuardTimerPaused)return;guardElapsed=Mathf.Min(6f,guardElapsed+Time.deltaTime);
        while(guardElapsed+0.0001f>=nextGuardVolley && nextGuardVolley<=6f)
        {nextGuardVolley+=2f;GuardVolleys++;effects.Fan(this,Muzzle(GuardFaceRight),GuardFaceRight,3);}
        if(guardElapsed>=6f)Restore();else GuardChanged?.Invoke();
    }
    private void BreakGuard()
    {
        Phase=GuardPhase.Exposed;Durability=0f;guardElapsed=0f;exposedRemaining=3f;health.RemoveDamageMultiplier(this);health.RemoveReflection(this);health.SetSuperArmor(this,false);
        health.SetDamageMultiplier(this,1.5f);movement.Stop();effects.HideShield();GuardChanged?.Invoke();
    }
    private void Restore()
    {Cleanup();Phase=GuardPhase.Normal;nextAttack=Time.time+attackInterval*RebirthCooldownMultiplier;GuardChanged?.Invoke();}
    private void Cleanup()
    {health.RemoveDamageMultiplier(this);health.RemoveReflection(this);health.SetSuperArmor(this,false);Durability=guardElapsed=exposedRemaining=0f;effects.HideShield();}
    private void Died(DamageInfo damage){CancelAttack();Cleanup();Phase=GuardPhase.Normal;pending=0;GuardChanged?.Invoke();}
    private void OnDisable()
    {
        if(health!=null){health.DamagePreparing-=PrepareHit;health.DamageReceived-=OnDamage;health.DamageResolved-=ResolveHit;health.Died-=Died;Cleanup();}
        CancelAttack();Phase=GuardPhase.Normal;pending=0;
    }
}

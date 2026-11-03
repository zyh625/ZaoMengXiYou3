using System;
using UnityEngine;
using zmxy.GamePlay;

public sealed class WestCombat : BossCombat,IEnemyArenaBounds
{
    public enum WardPhase { Normal,Guarding,Exposed }
    [SerializeField] private WestClawBox claw;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float attackInterval=2.2f;
    [SerializeField] private float[] cooldowns={4f,6f,10f,9f};
    private static readonly float[] windups={0.7f,0.6f,1f,0.4f};
    private static readonly int[] releaseFrames={11,9,12,6};
    private readonly float[] readyAt=new float[4];
    private Health health;
    private WestMovement movement;
    private WestEffectPool pool;
    private PlayerManager players;
    private PlayerController fallback;
    private float resolveAt,castAt,nextAttack,channelUntil,wardRemaining,nextRay,exposedRemaining;
    private int nextSkill,pendingWards,latchedWards;
    private bool released,arenaSet;
    private Vector2 arena;
    private WestKingFlame breath;
    public event Action WardChanged;
    public WardPhase Phase { get; private set; }
    public bool IsWarding=>Phase==WardPhase.Guarding;
    public bool IsSpecial=>Phase!=WardPhase.Normal;
    public bool WardTimerPaused=>health!=null && health.Status!=null && health.Status.BlocksActions;
    public float WardRemaining=>IsWarding?wardRemaining:0f;
    public float ExposureRemaining=>Phase==WardPhase.Exposed?exposedRemaining:0f;
    public int WardStarts { get; private set; }
    public int TimeoutHits { get; private set; }
    public int ActiveOrbs
    {
        get{int count=0;foreach(WestKingOrb orb in pool.Orbs)if(orb!=null && orb.Alive)count++;return count;}
    }
    public int CurrentSkill { get; private set; }=-1;
    public bool Released=>released;
    public bool Channeling=>CurrentSkill==2 && released && Time.time<channelUntil;
    public int ReleaseFrame=>CurrentSkill>=0?releaseFrames[CurrentSkill]:0;
    public float Windup=>CurrentSkill>=0?windups[CurrentSkill]:0f;
    public float NextAttackAt=>nextAttack;
    public Vector2 Arena=>arena;
    public WestEffectPool Effects=>pool;
    public PlayerController Target
    {
        get
        {
            if(players!=null && players.isActiveAndEnabled)return players.CurrentPlayer;
            if(fallback!=null && fallback.isActiveAndEnabled && !fallback.GetComponent<Health>().Dead)return fallback;
            if(Time.time<resolveAt)return null;resolveAt=Time.time+0.5f;
            players=FindAnyObjectByType<PlayerManager>();if(players!=null && players.isActiveAndEnabled)return players.CurrentPlayer;
            foreach(PlayerController p in FindObjectsByType<PlayerController>())if(p.isActiveAndEnabled && !p.GetComponent<Health>().Dead){fallback=p;return p;}
            return null;
        }
    }
    public void BindTarget(PlayerController target){fallback=target;players=null;}
    public void SetArenaBounds(float left,float right){arena=new Vector2(Mathf.Min(left,right),Mathf.Max(left,right));arenaSet=true;}
    private bool CanAct=>health!=null && !health.Dead && (!health.Attacked || health.Boost) && !IsSpecial
        && !(health.Status!=null && health.Status.BlocksActions) && !movement.IsExternallyControlled;
    public int ReadyAttack
    {
        get
        {
            PlayerController target=Target;
            if(CurrentSkill>=0 || !CanAct || Time.time<nextAttack || target==null)return -1;
            float distance=Mathf.Abs(target.transform.position.x-transform.position.x);
            for(int step=0;step<4;step++)
            {
                int i=(nextSkill+step)%4;float range=i==1?14f:i==3?4.5f:7f;
                if(Time.time>=readyAt[i] && distance<=range)return i;
            }
            return -1;
        }
    }
    protected override void Awake()
    {
        base.Awake();health=GetComponent<Health>();movement=GetComponent<WestMovement>();pool=GetComponent<WestEffectPool>();
    }
    protected override void OnEnable()
    {
        base.OnEnable();Phase=WardPhase.Normal;CurrentSkill=-1;released=false;latchedWards=pendingWards=WardStarts=TimeoutHits=nextSkill=0;
        nextAttack=Time.time;wardRemaining=exposedRemaining=0f;
        for(int i=0;i<4;i++)readyAt[i]=Time.time;
        health.DamageReceived+=OnDamage;health.Died+=Died;
        WardChanged?.Invoke();
    }
    public bool BeginAttack(int skill)
    {
        PlayerController target=Target;
        if(skill<0 || skill>3 || CurrentSkill>=0 || !CanAct || target==null || Time.time<nextAttack || Time.time<readyAt[skill])return false;
        CurrentSkill=skill;nextSkill=(skill+1)%4;readyAt[skill]=Time.time+cooldowns[skill]*RebirthCooldownMultiplier;castAt=Time.time;
        AttackFaceRight=target.transform.position.x>=transform.position.x;released=false;health.Status?.NotifyAttackStarted();return true;
    }
    public void SampleFrame(int frame)
    {
        if(CurrentSkill<0)return;
        if(!CanAct){CancelAttack();return;}
        if(CurrentSkill==0 && released && frame>=14)claw.gameObject.SetActive(false);
        if(released || frame<ReleaseFrame || Time.time<castAt+Windup)return;
        released=true;Vector2 origin=(Vector2)transform.position+new Vector2((AttackFaceRight?1f:-1f)*Mathf.Abs(muzzle.localPosition.x),muzzle.localPosition.y);
        switch(CurrentSkill)
        {
            case 0:
                BoxCollider2D box=claw.GetComponent<BoxCollider2D>();Vector2 offset=box.offset;offset.x=Mathf.Abs(offset.x)*(AttackFaceRight?1f:-1f);box.offset=offset;
                claw.gameObject.SetActive(true);Physics2D.SyncTransforms();claw.Sample();break;
            case 1:pool.Pearl(origin,false);break;
            case 2:breath=pool.Flame(transform.position,AttackFaceRight);channelUntil=Time.time+2f;break;
            case 3:pool.Pearl(origin,true);break;
        }
    }
    public void CancelAttack()
    {
        if(CurrentSkill>=0)nextAttack=Time.time+attackInterval*RebirthCooldownMultiplier;
        claw?.gameObject.SetActive(false);
        if(breath!=null && breath.gameObject.activeSelf)breath.StopStream();
        breath=null;CurrentSkill=-1;released=false;
    }
    private void OnDamage(DamageInfo damage,float actual)
    {
        if(health.Dead)return;
        if(health.ratio<=0.7f && (latchedWards&1)==0){latchedWards|=1;pendingWards|=1;}
        if(health.ratio<=0.3f && (latchedWards&2)==0){latchedWards|=2;pendingWards|=2;}
        if(!IsSpecial)BeginWard();
    }
    private void BeginWard()
    {
        if(pendingWards==0 || health.Dead)return;
        pendingWards&=pendingWards==3?2:0;WardStarts++;
        CancelAttack();movement.Stop();Phase=WardPhase.Guarding;wardRemaining=12f;nextRay=8f;
        health.SetDamageMultiplier(this,0.3f);health.SetSuperArmor(this,true);
        if(!arenaSet)
        {
            CameraManager cm=Camera.main!=null?Camera.main.GetComponent<CameraManager>():null;
            arena=cm!=null?new Vector2(cm.EdgeL-cm.CameraHalf,cm.EdgeR+cm.CameraHalf):new Vector2(transform.position.x-10f,transform.position.x+10f);
        }
        RaycastHit2D ground=Physics2D.Raycast((Vector2)transform.position+Vector2.up,Vector2.down,30f,1<<6);
        pool.SpawnOrbs(arena,ground.collider!=null?ground.point.y:transform.position.y);
        WardChanged?.Invoke();
    }
    private void Update()
    {
        if(health==null || health.Dead)return;
        if(Phase==WardPhase.Normal){if(pendingWards!=0)BeginWard();return;}
        //明确选择暂停：固体冰冻时护珠与射线预警暂停，融化阶段恢复；易伤窗口仍固定三秒。
        if(IsWarding && WardTimerPaused)return;
        if(Phase==WardPhase.Exposed)
        {
            exposedRemaining-=Time.deltaTime;if(exposedRemaining<=0f)Restore();
            else WardChanged?.Invoke();return;
        }
        wardRemaining=Mathf.Max(0f,wardRemaining-Time.deltaTime);
        if(ActiveOrbs==0){Expose();return;}
        if(wardRemaining<=0f){Timeout();return;}
        if(wardRemaining<=nextRay)
        {
            nextRay-=4f;int choice=UnityEngine.Random.Range(0,ActiveOrbs);
            foreach(WestKingOrb orb in pool.Orbs)if(orb.Alive && choice--==0){orb.Warn();break;}
        }
        WardChanged?.Invoke();
    }
    public void OrbBroken(WestKingOrb orb)
    {
        if(!IsWarding)return;
        if(ActiveOrbs==0)Expose();else WardChanged?.Invoke();
    }
    private void Expose()
    {
        Phase=WardPhase.Exposed;pool.ClearOrbs();health.RemoveDamageMultiplier(this);health.SetSuperArmor(this,false);
        health.SetDamageMultiplier(this,1.5f);exposedRemaining=3f;movement.Stop();WardChanged?.Invoke();
    }
    private void Timeout()
    {
        //先退出护珠，防止伤害回调重入后重复惩罚；只命中结算时的当前角色。
        Restore();TimeoutHits++;PlayerController player=Target;
        if(player!=null && player.CanReceiveHit)player.GetComponent<Health>().TakeDamage(new DamageInfo(transform,35f*DamageMultiplier,true,false,null,false,false,DamageKind.Skill));
    }
    private void Restore()
    {
        pool.ClearOrbs();health.RemoveDamageMultiplier(this);health.SetSuperArmor(this,false);Phase=WardPhase.Normal;
        wardRemaining=exposedRemaining=0f;nextAttack=Time.time+attackInterval*RebirthCooldownMultiplier;WardChanged?.Invoke();
    }
    private void Died(DamageInfo damage){CancelAttack();Restore();pool.ReleaseAll();pendingWards=0;}
    private void OnDisable()
    {
        if(health!=null){health.DamageReceived-=OnDamage;health.Died-=Died;health.RemoveDamageMultiplier(this);health.SetSuperArmor(this,false);}
        CancelAttack();pool?.ReleaseAll();Phase=WardPhase.Normal;
    }
}

using System.Collections.Generic;
using UnityEngine;

public sealed class GreenSummonCombat : EnemyCombat
{
    [SerializeField] private GreenSummonHammer hammerPrefab;
    [SerializeField] private GreenSummonWave wavePrefab;
    [SerializeField] private GreenSummonWarBanner bannerPrefab;
    [SerializeField] private Transform muzzle,bannerHand;
    [SerializeField] private float[] skillCooldowns={8f,6f,16f};
    private static readonly int[] releaseFrames={9,7,15};
    private readonly float[] readyAt=new float[3];
    private Health health;
    private PlayerManager party;
    private PlayerController fallback;
    private float resolveAt;
    private bool released;
    private GreenSummonHammer warning;
    private GreenSummonEffectPool effects;
    private GreenSummonEffectPool Effects=>effects!=null && effects.isActiveAndEnabled?effects:effects=GreenSummonEffectPool.GetOrCreate();
    public int CurrentSkill {get;private set;}=-1;
    public bool FaceRight {get;private set;}
    public int ReleaseFrame=>CurrentSkill>=0?releaseFrames[CurrentSkill]:0;
    public GreenSummonHammer Warning=>warning;
    public bool Released=>released;
    protected override bool UsesAnger=>false;
    public PlayerController Target
    {
        get
        {
            if(party!=null && party.isActiveAndEnabled)return party.CurrentPlayer;
            if(fallback!=null && fallback.isActiveAndEnabled && !fallback.GetComponent<Health>().Dead)return fallback;
            if(Time.time<resolveAt)return null;
            resolveAt=Time.time+0.5f;party=FindAnyObjectByType<PlayerManager>();
            if(party!=null && party.isActiveAndEnabled)return party.CurrentPlayer;
            foreach(PlayerController player in FindObjectsByType<PlayerController>())
                if(player.isActiveAndEnabled && !player.GetComponent<Health>().Dead){fallback=player;return player;}
            return null;
        }
    }
    public void BindTarget(PlayerController target){fallback=target;party=null;}
    public bool InRange=>Target!=null && Mathf.Abs(Target.transform.position.x-transform.position.x) is >=4f and <=7f;
    public int ReadySkill
    {
        get
        {
            PlayerController target=Target;
            if(CurrentSkill>=0 || health==null || health.Dead || health.Attacked || target==null || target.GetComponent<Health>().Dead
                || health.Status!=null && health.Status.BlocksActions)return -1;
            if(Time.time>=readyAt[2] && !Effects.HasFriendlyBanner)return 2;
            if(Time.time>=readyAt[0] && !GreenSummonHammer.HasWarningAt(GreenSummonHitUtility.GroundAt(target.transform.position)))return 0;
            return Time.time>=readyAt[1]?1:-1;
        }
    }
    protected override void Awake(){base.Awake();health=GetComponent<Health>();party=FindAnyObjectByType<PlayerManager>();}
    protected override void OnEnable()
    {
        base.OnEnable();health??=GetComponent<Health>();CurrentSkill=-1;released=false;warning=null;
        for(int i=0;i<3;i++)readyAt[i]=Time.time;health.Died+=OnDied;
    }
    private Vector2 Anchor(Transform point)=>(Vector2)transform.position+new Vector2((FaceRight?1f:-1f)*Mathf.Abs(point.localPosition.x),point.localPosition.y);
    public bool BeginCast(int skill)
    {
        PlayerController target=Target;
        if(skill<0 || skill>=3 || target==null || target.GetComponent<Health>().Dead || health==null || health.Dead || health.Attacked
            || CurrentSkill>=0 || Time.time<readyAt[skill] || health.Status!=null && health.Status.BlocksActions)return false;
        GreenSummonEffectPool effects=Effects;
        Vector2 ground=GreenSummonHitUtility.GroundAt(target.transform.position);
        if(skill==0 && GreenSummonHammer.HasWarningAt(ground) || skill==2 && !effects.ReserveBanner(this))return false;
        CurrentSkill=skill;readyAt[skill]=Time.time+skillCooldowns[skill];released=false;FaceRight=target.transform.position.x>=transform.position.x;
        health.Status?.NotifyAttackStarted();if(skill==0)warning=effects.Warn(hammerPrefab,this,ground);return true;
    }
    public void SampleFrame(int frame)
    {
        if(CurrentSkill<0 || released)return;
        if(health.Dead || health.Attacked || health.Status!=null && health.Status.BlocksActions){CancelCast();return;}
        if(frame<ReleaseFrame)return;released=true;
        if(CurrentSkill==0)warning?.Commit(DamageMultiplier);
        else if(CurrentSkill==1)Effects.Fire(wavePrefab,this,Anchor(muzzle),FaceRight);
        else Effects.Plant(bannerPrefab,this,Anchor(bannerHand));
    }
    public static void ResolveHammer(Transform source,Vector2 point,float multiplier,Collider2D[] hits,HashSet<Health> unique)
    {
        //单次圆形查询；命中记录在伤害回调之前提交，多个受击箱/反伤不会重入重复结算。
        int count=Physics2D.OverlapCircle(point,2.5f,GreenSummonHitUtility.Players,hits);unique.Clear();
        for(int i=0;i<count;i++)
        {
            Health victim=GreenSummonHitUtility.Player(hits[i]);if(victim==null || !unique.Add(victim))continue;
            victim.TakeDamage(new DamageInfo(source,22f*multiplier,false,false,null,false,false,DamageKind.Skill));
        }
    }
    public void CancelCast()
    {
        if(!released && warning!=null)warning.Cancel();warning=null;
        if(effects!=null)effects.ReleaseReservation(this);CurrentSkill=-1;released=false;
    }
    private void OnDied(DamageInfo damage)=>CancelCast();
    private void OnDisable(){if(health!=null)health.Died-=OnDied;CancelCast();}
}

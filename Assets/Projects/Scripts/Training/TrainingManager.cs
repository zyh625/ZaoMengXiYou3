using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using zmxy.GamePlay;

[DefaultExecutionOrder(-20000)]
public sealed class TrainingManager : LevelEnemyManager
{
    public enum EnemyMode {Combat,Dummy,Respawn}
    [SerializeField] private PlayerManager players;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Transform playerSpawn,enemySpawn;
    [SerializeField] private TrainingEnemyCatalog catalog;
    private readonly HashSet<GameObject> protectedRoots=new();
    private readonly List<GameObject> roots=new();
    private readonly List<EnemyController> summons=new();
    private Transform enemiesRoot,creation;
    private EnemyController target;
    private Health targetHealth,playerHealth;
    private RigidbodyConstraints2D normalConstraints;
    private float normalGravity,respawnAt=-1f,playerRespawnAt=-1f;
    private Coroutine switching;
    private int selected;
    public readonly TrainingStatistics Statistics=new();
    public TrainingEnemyCatalog Catalog=>catalog;
    public EnemyController Target=>target;
    public Health TargetHealth=>targetHealth;
    public PlayerManager Players=>players;
    public int Selected=>selected;
    public EnemyMode Mode {get;private set;}
    public bool InfiniteResources {get;private set;}=true;
    public bool NoCooldown {get;private set;}
    public bool Invulnerable {get;private set;}=true;
    public bool InfiniteDummy {get;private set;}=true;
    public bool Ready {get;private set;}
    private void Awake(){TrainingSession.Begin();GamePause.Reset();}
    private void Start()
    {
        if(catalog==null)catalog=Resources.Load<TrainingEnemyCatalog>("Training/TrainingEnemyCatalog");
        if(catalog==null || catalog.Count==0){Debug.LogError("TrainingEnemyCatalog is empty.",this);enabled=false;return;}
        players.TrainingMode=true;players.InitPlayer(playerSpawn);players.CurrentPlayerChanged+=PlayerChanged;PlayerChanged(players.CurrentPlayer);
        GameObject parent=new("TrainingEnemies");enemiesRoot=parent.transform;
        GameObject inactive=new("InactiveCreation");inactive.transform.SetParent(transform,false);inactive.SetActive(false);creation=inactive.transform;
        roots.Clear();gameObject.scene.GetRootGameObjects(roots);foreach(GameObject root in roots)protectedRoots.Add(root);
        Ready=true;SelectEnemy(0);
    }
    public void ProtectRoot(GameObject root)=>protectedRoots.Add(root);//原生探针也可注册自己的独立根节点。
    public void SelectEnemy(int index)
    {
        if(!Ready)return;selected=(index%catalog.Count+catalog.Count)%catalog.Count;
        if(switching!=null)StopCoroutine(switching);switching=StartCoroutine(ReplaceTarget());
    }
    public void PreviousEnemy()=>SelectEnemy(selected-1);
    public void NextEnemy()=>SelectEnemy(selected+1);
    public void RestartTarget()=>SelectEnemy(selected);
    private IEnumerator ReplaceTarget()
    {
        UnbindTarget();if(target!=null){target.gameObject.SetActive(false);Destroy(target.gameObject);}target=null;curBoss=null;Statistics.Reset();respawnAt=-1f;
        ClearTransientEffects();yield return null;
        target=Create(catalog.enemies[selected].prefab,enemySpawn.position);targetHealth=target.GetComponent<Health>();targetHealth.DamageReceived+=Damage;targetHealth.Died+=Died;
        normalConstraints=target.GetComponent<Rigidbody2D>().constraints;normalGravity=target.GetComponent<Rigidbody2D>().gravityScale;
        if(target is BossController)curBoss=target.transform;ApplyMode();switching=null;
    }
    private EnemyController Create(EnemyController prefab,Vector2 position)
    {
        EnemyController enemy=Instantiate(prefab,creation);enemy.gameObject.SetActive(false);
        foreach(EnemyRewards rewards in enemy.GetComponentsInChildren<EnemyRewards>(true))rewards.enabled=false;
        enemy.transform.SetParent(enemiesRoot,false);enemy.transform.position=position;enemy.gameObject.SetActive(true);
        BoxCollider2D box=enemy.GetComponent<BoxCollider2D>();Vector3 p=enemy.transform.position;p.y-=(box.offset.y-box.size.y/2f)*enemy.transform.lossyScale.y;enemy.transform.position=p;
        enemy.GetComponent<EnemyMovement>().SetTarget(players.CurrentPlayer.transform);enemy.SetSpawnSide(true);
        foreach(MonoBehaviour component in enemy.GetComponentsInChildren<MonoBehaviour>(true))if(component is IEnemyArenaBounds bounds)bounds.SetArenaBounds(-38f,38f);
        return enemy;
    }
    public void SetMode(EnemyMode value)
    {
        Mode=value;respawnAt=-1f;if(target==null)return;
        if(targetHealth.Dead){StartCoroutine(ReviveTarget());return;}ApplyMode();
    }
    public void ToggleAI()=>SetMode(Mode==EnemyMode.Dummy?EnemyMode.Combat:EnemyMode.Dummy);
    private void ApplyMode()
    {
        if(target==null)return;
        target.TrainingPassive=true;EnemyCombat combat=target.GetComponent<EnemyCombat>();combat.enabled=false;
        targetHealth.ClearDebuffs();target.GetComponent<EnemyMovement>().ClearExternalControl();ClearTransientEffects();
        Rigidbody2D body=target.GetComponent<Rigidbody2D>();body.linearVelocity=Vector2.zero;
        targetHealth.TrainingMinimumBlood=Mode==EnemyMode.Dummy && InfiniteDummy?1f:0f;
        if(Mode==EnemyMode.Dummy){body.constraints=RigidbodyConstraints2D.FreezeAll;body.gravityScale=0f;}
        else{body.constraints=normalConstraints;body.gravityScale=normalGravity;StartCoroutine(ResumeAI(target,combat));}
    }
    private IEnumerator ResumeAI(EnemyController actor,EnemyCombat combat)
    {yield return null;if(actor!=null && actor==target && !actor.GetComponent<Health>().Dead && Mode!=EnemyMode.Dummy){actor.TrainingPassive=false;combat.enabled=true;}}
    public void SetInfiniteResources(bool value){InfiniteResources=value;ApplyPlayerOptions();}
    public void SetNoCooldown(bool value){NoCooldown=value;ApplyPlayerOptions();}
    public void SetInvulnerable(bool value){Invulnerable=value;ApplyPlayerOptions();}
    public void SetInfiniteDummy(bool value){InfiniteDummy=value;if(targetHealth!=null)targetHealth.TrainingMinimumBlood=Mode==EnemyMode.Dummy && value?1f:0f;}
    private void ApplyPlayerOptions()
    {
        if(players==null)return;
        foreach(PlayerController player in players.Players)
        {player.GetComponent<PlayerResources>().TrainingInfiniteResources=InfiniteResources;player.GetComponent<PlayerCombat>().TrainingNoCooldown=NoCooldown;player.GetComponent<Health>().TrainingPreventHealthLoss=Invulnerable;}
    }
    private void PlayerChanged(PlayerController player)
    {
        if(playerHealth!=null)playerHealth.Died-=PlayerDied;playerHealth=player!=null?player.GetComponent<Health>():null;if(playerHealth!=null)playerHealth.Died+=PlayerDied;
        ApplyPlayerOptions();if(target!=null)target.GetComponent<EnemyMovement>().SetTarget(player.transform);
        foreach(EnemyController summon in summons)if(summon!=null)summon.GetComponent<EnemyMovement>().SetTarget(player.transform);
    }
    private void PlayerDied(DamageInfo damage)=>playerRespawnAt=Time.time+2f;
    public void ResetPlayer()
    {
        players.ReviveTrainingPlayer(playerSpawn.position);playerRespawnAt=-1f;ApplyPlayerOptions();
    }
    private void Update()
    {
        if(!Ready)return;
        if(playerRespawnAt>=0f && Time.time>=playerRespawnAt)ResetPlayer();
        if(respawnAt>=0f && Time.time>=respawnAt && switching==null){respawnAt=-1f;StartCoroutine(ReviveTarget());}
    }
    private void Damage(DamageInfo info,float amount)=>Statistics.Record(info,targetHealth.TrainingMinimumBlood>0f?targetHealth.TrainingReportedDamage:amount);
    private void Died(DamageInfo info){Statistics.Died();if(Mode==EnemyMode.Respawn)respawnAt=Time.time+2f;}
    private IEnumerator ReviveTarget()
    {
        EnemyController reviving=target;
        if(reviving==null)yield break;reviving.gameObject.SetActive(false);ClearTransientEffects();yield return null;if(target==null || target!=reviving)yield break;
        target.transform.position=enemySpawn.position;target.gameObject.SetActive(true);BoxCollider2D box=target.GetComponent<BoxCollider2D>();Vector3 p=target.transform.position;p.y-=(box.offset.y-box.size.y/2f)*target.transform.lossyScale.y;target.transform.position=p;
        target.GetComponent<EnemyMovement>().SetTarget(players.CurrentPlayer.transform);target.SetSpawnSide(true);Statistics.Reset();ApplyMode();
    }
    private void UnbindTarget(){if(targetHealth!=null){targetHealth.DamageReceived-=Damage;targetHealth.Died-=Died;}targetHealth=null;}
    private void ClearTransientEffects()
    {
        players.StopEffects();foreach(PlayerController p in players.Players)p.GetComponent<Health>().ClearDebuffs();
        foreach(EnemyController summon in summons)if(summon!=null){summon.gameObject.SetActive(false);Destroy(summon.gameObject);}summons.Clear();
        roots.Clear();gameObject.scene.GetRootGameObjects(roots);
        foreach(GameObject root in roots)if(!protectedRoots.Contains(root)){root.SetActive(false);Destroy(root);}
    }
    public void ExitTraining(){players.StopEffects();SceneManager.LoadScene("MapScene");}
    protected override void InitEnemyPools(){}
    protected override void InitBoss(){}
    public override bool ValidateConfiguration(out string error){error=null;return true;}
    public override bool TryCreateEnemy(bool right,float createY)=>false;
    public override bool TryCreateSummonedEnemy(Vector2 ground)
    {
        if(!Ready || Mode==EnemyMode.Dummy)return false;int alive=0;foreach(EnemyController s in summons)if(s!=null && s.gameObject.activeSelf)alive++;if(alive>=2)return false;
        foreach(EnemyController s in summons)if(s!=null && !s.gameObject.activeSelf){s.transform.position=ground+Vector2.up*0.025f;s.gameObject.SetActive(true);s.GetComponent<EnemyMovement>().SetTarget(players.CurrentPlayer.transform);return true;}
        foreach(TrainingEnemyCatalog.Entry entry in catalog.enemies)if(entry.summonFallback && entry.prefab!=null){summons.Add(Create(entry.prefab,ground+Vector2.up*0.025f));return true;}return false;
    }
    public override void SetTarget(Transform player){if(target!=null)target.GetComponent<EnemyMovement>().SetTarget(player);}
    private void OnDestroy()
    {
        UnbindTarget();if(players!=null)players.CurrentPlayerChanged-=PlayerChanged;if(playerHealth!=null)playerHealth.Died-=PlayerDied;
    }
}

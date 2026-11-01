using System.Collections.Generic;
using UnityEngine;

//场景拥有已释放的技能。施法者死亡/回池不删除落锤、气浪或已插下的战旗。
public sealed class GreenSummonEffectPool : MonoBehaviour
{
    private static GreenSummonEffectPool current;
    private readonly Dictionary<GreenSummonHammer,PrefabPool<GreenSummonHammer>> hammers=new();
    private readonly Dictionary<GreenSummonWave,PrefabPool<GreenSummonWave>> waves=new();
    private readonly Dictionary<GreenSummonWarBanner,PrefabPool<GreenSummonWarBanner>> banners=new();
    private readonly Dictionary<GreenSummonCombat,GreenSummonWarBanner> owned=new();
    private GreenSummonCombat reservation;
    private Transform inactiveRoot;
    private bool used;
    public static GreenSummonEffectPool GetOrCreate()
    {
        if(current!=null && current.isActiveAndEnabled)return current;
        current=FindAnyObjectByType<GreenSummonEffectPool>();
        if(current==null)current=new GameObject("GreenSummonEffectPool").AddComponent<GreenSummonEffectPool>();
        return current;
    }
    public bool HasFriendlyBanner
    {
        get
        {
            if(reservation!=null && reservation.isActiveAndEnabled && !reservation.GetComponent<Health>().Dead)return true;
            foreach(GreenSummonWarBanner flag in owned.Values)if(flag!=null && flag.gameObject.activeInHierarchy)return true;
            return false;
        }
    }
    public bool ReserveBanner(GreenSummonCombat owner)
    {
        if(owner==null || HasFriendlyBanner)return false;
        reservation=owner;return true;
    }
    public void ReleaseReservation(GreenSummonCombat owner){if(reservation==owner)reservation=null;}
    private void Awake()
    {
        GameObject root=new("InactiveCreation");root.transform.SetParent(transform,false);root.SetActive(false);inactiveRoot=root.transform;
    }
    private T Get<T>(T prefab,Dictionary<T,PrefabPool<T>> pools) where T:MonoBehaviour
    {
        if(prefab==null)return null;
        if(!pools.TryGetValue(prefab,out PrefabPool<T> pool)){pool=new();pools.Add(prefab,pool);pool.Prewarm(prefab,2,transform,inactiveRoot);}
        used=true;return pool.Get(prefab,transform,inactiveRoot);
    }
    public GreenSummonHammer Warn(GreenSummonHammer prefab,GreenSummonCombat source,Vector2 point)
    {
        if(source==null || !BattleRun.IsRunning)return null;
        GreenSummonHammer effect=Get(prefab,hammers);effect?.Warn(source,point);return effect;
    }
    public GreenSummonWave Fire(GreenSummonWave prefab,GreenSummonCombat source,Vector2 point,bool right)
    {
        if(source==null || !BattleRun.IsRunning)return null;
        GreenSummonWave effect=Get(prefab,waves);effect?.Launch(source,point,right);return effect;
    }
    public GreenSummonWarBanner Plant(GreenSummonWarBanner prefab,GreenSummonCombat source,Vector2 hand)
    {
        if(source==null || !BattleRun.IsRunning)return null;
        //同一施法者的重复请求只刷新原旗寿命；不恢复血量、不搬走原旗、不重复叠加。
        if(owned.TryGetValue(source,out GreenSummonWarBanner own) && own!=null && own.gameObject.activeInHierarchy)
        {own.RefreshLifetime();ReleaseReservation(source);return own;}
        if(reservation!=source && HasFriendlyBanner)return null;
        GreenSummonWarBanner effect=Get(prefab,banners);
        if(effect!=null){owned[source]=effect;effect.Plant(this,source,hand);}
        ReleaseReservation(source);return effect;
    }
    public void BannerEnded(GreenSummonCombat owner,GreenSummonWarBanner flag)
    {if(owner!=null && owned.TryGetValue(owner,out GreenSummonWarBanner own) && own==flag)owned.Remove(owner);}
    public void ReleaseAll()
    {
        foreach(var pool in hammers.Values)pool.ReleaseAll();foreach(var pool in waves.Values)pool.ReleaseAll();foreach(var pool in banners.Values)pool.ReleaseAll();
        owned.Clear();reservation=null;used=false;
    }
    private void LateUpdate(){if(used && !BattleRun.IsRunning)ReleaseAll();}
    private void OnDisable()=>ReleaseAll();
    private void OnDestroy(){if(current==this)current=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetPool()=>current=null;
}

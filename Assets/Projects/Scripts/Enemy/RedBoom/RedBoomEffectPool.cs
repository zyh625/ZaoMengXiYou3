using System.Collections.Generic;
using UnityEngine;

//场景持有全部实例；射手回池只取消尚未释放的招式，不回收已投出的灯笼/已命中的标记。
public sealed class RedBoomEffectPool : MonoBehaviour
{
    private static RedBoomEffectPool current;
    private readonly Dictionary<RedBoomLantern,PrefabPool<RedBoomLantern>> lanterns=new();
    private readonly Dictionary<RedBoomRay,PrefabPool<RedBoomRay>> rays=new();
    private readonly Dictionary<RedBoomMark,PrefabPool<RedBoomMark>> marks=new();
    private Transform inactiveRoot;
    private bool used;
    public static RedBoomEffectPool GetOrCreate()
    {
        if(current!=null && current.isActiveAndEnabled)return current;
        current=FindAnyObjectByType<RedBoomEffectPool>();
        if(current==null)current=new GameObject("RedBoomEffectPool").AddComponent<RedBoomEffectPool>();
        return current;
    }
    private void Awake()
    {
        GameObject inactive=new("InactiveCreation");inactive.transform.SetParent(transform,false);inactive.SetActive(false);inactiveRoot=inactive.transform;
    }
    private T Get<T>(T prefab,Dictionary<T,PrefabPool<T>> pools) where T:MonoBehaviour
    {
        if(prefab==null)return null;
        if(!pools.TryGetValue(prefab,out PrefabPool<T> pool)) { pool=new PrefabPool<T>();pools.Add(prefab,pool);pool.Prewarm(prefab,2,transform,inactiveRoot); }
        used=true;return pool.Get(prefab,transform,inactiveRoot);
    }
    public RedBoomLantern Throw(RedBoomLantern prefab,RedBoomCombat source,Vector2 from,PlayerController target)
    {
        if(source==null || target==null || !BattleRun.IsRunning)return null;
        RedBoomLantern effect=Get(prefab,lanterns);effect?.Launch(source,from,target);return effect;
    }
    public RedBoomRay Warn(RedBoomRay prefab,Vector2 from,Vector2 to,SpriteRenderer source)
    { RedBoomRay effect=Get(prefab,rays);effect?.Warn(from,to,source);return effect; }
    public RedBoomMark Mark(RedBoomMark prefab,Health target,SpriteRenderer source)
    { RedBoomMark effect=Get(prefab,marks);if(effect!=null && !effect.Apply(target,source))return null;return effect; }
    public void ReleaseAll()
    {
        foreach(var pool in lanterns.Values)pool.ReleaseAll();foreach(var pool in rays.Values)pool.ReleaseAll();foreach(var pool in marks.Values)pool.ReleaseAll();used=false;
    }
    private void LateUpdate() { if(used && !BattleRun.IsRunning)ReleaseAll(); }
    private void OnDisable()=>ReleaseAll();
    private void OnDestroy(){if(current==this)current=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool()=>current=null;
}

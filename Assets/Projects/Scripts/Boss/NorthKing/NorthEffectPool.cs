using System.Collections.Generic;
using UnityEngine;

//场景级池；Boss死亡只关护伞表现，已经飞出的弹幕到命中/寿命结束或战斗结束才回收。
public sealed class NorthEffectPool : MonoBehaviour
{
    private static NorthEffectPool current;
    private readonly Dictionary<NorthKingRib,PrefabPool<NorthKingRib>> ribs=new();
    private readonly Dictionary<NorthKingRain,PrefabPool<NorthKingRain>> rains=new();
    private readonly Dictionary<NorthKingShield,PrefabPool<NorthKingShield>> shields=new();
    private Transform inactive;
    private bool used;
    public static NorthEffectPool GetOrCreate()
    {
        if(current!=null && current.isActiveAndEnabled)return current;
        current=FindAnyObjectByType<NorthEffectPool>();if(current==null)current=new GameObject("NorthKingEffects").AddComponent<NorthEffectPool>();return current;
    }
    private void Awake(){GameObject root=new("InactiveCreation");root.transform.SetParent(transform,false);root.SetActive(false);inactive=root.transform;}
    private T Get<T>(T prefab,Dictionary<T,PrefabPool<T>> pools,int size) where T:MonoBehaviour
    {
        if(!pools.TryGetValue(prefab,out PrefabPool<T> pool)){pool=new();pools.Add(prefab,pool);pool.Prewarm(prefab,size,transform,inactive);}used=true;return pool.Get(prefab,transform,inactive);
    }
    public NorthKingRib Rib(NorthKingRib prefab,NorthCombat source,Vector2 from,Vector2 direction)
    {NorthKingRib shot=Get(prefab,ribs,8);shot.Launch(source,from,direction);return shot;}
    public NorthKingRain Rain(NorthKingRain prefab,NorthCombat source,Vector2 floor)
    {NorthKingRain shot=Get(prefab,rains,6);shot.Launch(source,floor);return shot;}
    public NorthKingShield Shield(NorthKingShield prefab,NorthCombat source)
    {NorthKingShield effect=Get(prefab,shields,1);effect.Show(source);return effect;}
    public void ReleaseAll(){foreach(var p in ribs.Values)p.ReleaseAll();foreach(var p in rains.Values)p.ReleaseAll();foreach(var p in shields.Values)p.ReleaseAll();used=false;}
    private void LateUpdate(){if(used && !BattleRun.IsRunning)ReleaseAll();}
    private void OnDisable()=>ReleaseAll();
    private void OnDestroy(){if(current==this)current=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetPool()=>current=null;
}

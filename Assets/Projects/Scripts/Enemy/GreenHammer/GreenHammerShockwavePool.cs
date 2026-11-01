using System.Collections.Generic;
using UnityEngine;

//API接受EnemyCombat（含BossCombat），任何Boss/小怪均可借用同一场景简单池。
public sealed class GreenHammerShockwavePool : MonoBehaviour
{
    private static GreenHammerShockwavePool current;
    private readonly Dictionary<GreenHammerShockwave,PrefabPool<GreenHammerShockwave>> pools=new();
    private Transform inactive;
    private bool used;
    public static GreenHammerShockwavePool GetOrCreate()
    {
        if(current!=null && current.isActiveAndEnabled)return current;
        current=FindAnyObjectByType<GreenHammerShockwavePool>();
        if(current==null)current=new GameObject("GreenHammerShockwavePool").AddComponent<GreenHammerShockwavePool>();return current;
    }
    private void Awake(){GameObject root=new("InactiveCreation");root.transform.SetParent(transform,false);root.SetActive(false);inactive=root.transform;}
    public GreenHammerShockwave Spawn(GreenHammerShockwave prefab,EnemyCombat source,Vector2 position,float width,bool damage,float scale)
    {
        if(prefab==null || source==null || !BattleRun.IsRunning)return null;
        if(!pools.TryGetValue(prefab,out PrefabPool<GreenHammerShockwave> pool)){pool=new PrefabPool<GreenHammerShockwave>();pools.Add(prefab,pool);pool.Prewarm(prefab,3,transform,inactive);}
        GreenHammerShockwave effect=pool.Get(prefab,transform,inactive);effect.Play(source,position,width,damage,scale);used=true;return effect;
    }
    public void ReleaseAll(){foreach(var pool in pools.Values)pool.ReleaseAll();used=false;}
    private void LateUpdate(){if(used && !BattleRun.IsRunning)ReleaseAll();}
    private void OnDisable()=>ReleaseAll();
    private void OnDestroy(){if(current==this)current=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetPool()=>current=null;
}

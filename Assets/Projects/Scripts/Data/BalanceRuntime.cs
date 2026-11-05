using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class BalanceRuntime
{
    private sealed class Resolution {public BalanceProfile profile;}
    private static ConditionalWeakTable<Component,Resolution> resolved=new();
    private static readonly Dictionary<string,BalanceProfile> profiles=new();
    private static readonly Dictionary<string,BalanceProfile> keyProfiles=new();
    private static readonly Dictionary<string,string> routes=new();
    private static bool initialized;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){resolved=new();profiles.Clear();keyProfiles.Clear();routes.Clear();initialized=false;}
    public static BalanceProfile Load(string resource)
    {
        if(!profiles.TryGetValue(resource,out var p)){p=Resources.Load<BalanceProfile>("Balance/"+resource);profiles[resource]=p;}
        return p;
    }
    public static BalanceProfile Player(PlayerId id)=>Load("Players/"+(id==PlayerId.WuKong?"Wukong":id==PlayerId.BaJie?"Bajie":id==PlayerId.Tang?"Tang":"Sha"));
    public static float Number(Component context,string key,float fallback)
    {
        if(context==null)return fallback;
        var entry=resolved.GetValue(context,c=>new Resolution{profile=c.GetComponentInParent<BalanceBinding>(true)?.Profile});
        return entry.profile!=null&&entry.profile.TryGet(key,out float value)?value:Number(context.GetType(),key,fallback);
    }
    public static float Number(Type context,string key,float fallback)
    {
        if(keyProfiles.TryGetValue(key,out var cached))return cached!=null?cached.Get(key,fallback):fallback;
        if(!initialized)
        {
            initialized=true;var asset=Resources.Load<TextAsset>("Balance/TypeRoutes");
            if(asset!=null)foreach(var line in asset.text.Split('\n')){var pair=line.Trim().Split('|');if(pair.Length==2)routes[pair[0]]=pair[1];}
        }
        string declaringType=key.Contains(".")?key.Substring(0,key.IndexOf('.')):context.FullName;
        if(routes.TryGetValue(declaringType,out var path)||routes.TryGetValue(context.FullName,out path)){var p=Load(path);keyProfiles[key]=p;if(p!=null)return p.Get(key,fallback);}
        else keyProfiles[key]=null;
        return fallback;
    }
    public static float BaseBlood(GameObject prefab,float fallback)
    {
        var p=prefab!=null?prefab.GetComponent<BalanceBinding>()?.Profile:null;
        if(p!=null)foreach(var group in p.components)
            if(group.objectPath==""&&group.componentType==typeof(Health).FullName)
                foreach(var value in group.parameters)if(value.path=="beginBlood")return Mathf.Max(0,(float)value.value);
        return fallback;
    }
    public static void ApplyLevel(Component target)
    {
        if(TrainingSession.Active)return;
        var profile=Load("Levels/"+target.gameObject.scene.name);
        if(profile==null)return;
        foreach(var group in profile.components)
            if(group.componentType==target.GetType().FullName)
                foreach(var p in group.parameters)BalanceBinding.Assign(target,p.path.Replace(".Array.data[","["),p.value);
    }
}

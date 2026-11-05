using System;
using System.Collections.Generic;
using UnityEngine;

// Assets hold base tuning only. Saves, temporary buffs and pooled-instance state stay outside them.
[CreateAssetMenu(menuName = "造梦西游/数值配置", fileName = "Balance")]
public sealed class BalanceProfile : ScriptableObject
{
    [Serializable] public sealed class Parameter
    {
        public string path;
        public string label;
        public double value;
        public bool integer;
        public bool boolean;
    }
    [Serializable] public sealed class ComponentSettings
    {
        public string objectPath;
        public string componentType;
        public int componentIndex;
        public Parameter[] parameters = Array.Empty<Parameter>();
    }
    [Serializable] public sealed class Number
    {
        public string key;
        public string label;
        public string source;
        public float value;
    }
    public string displayName;
    [TextArea] public string description = "修改后重新进入场景/训练目标生效；不写入存档，不修改临时状态。";
    public ComponentSettings[] components = Array.Empty<ComponentSettings>();
    public Number[] numbers = Array.Empty<Number>();
    public SkillProgression.SkillDefinition[] skills = Array.Empty<SkillProgression.SkillDefinition>();
    public BalanceProfile[] relatedProfiles = Array.Empty<BalanceProfile>();
    private Dictionary<string,float> lookup;
    public float Get(string key,float fallback)
    {
        return TryGet(key,out float value)?value:fallback;
    }
    public bool TryGet(string key,out float value)
    {
        if(lookup==null){lookup=new Dictionary<string,float>();foreach(var n in numbers)if(n!=null&&!string.IsNullOrEmpty(n.key))lookup[n.key]=n.value;}
        return lookup.TryGetValue(key,out value);
    }
    public void Invalidate()=>lookup=null;
    private void OnValidate()=>Invalidate();
    private void OnEnable()=>Invalidate();
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(-32000), DisallowMultipleComponent]
public sealed class BalanceBinding : MonoBehaviour
{
    [SerializeField] private BalanceProfile profile;
    public BalanceProfile Profile=>profile;
    private bool applied;
    private static readonly Dictionary<string,Type> types=new();
    public static Type ResolveType(string name)
    {
        if(types.TryGetValue(name,out var found))return found;
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){found=assembly.GetType(name);if(found!=null)break;}
        types[name]=found;return found;
    }
    private void Awake()=>ApplyOnce();
    //数值资产只保存数值：本体缩放、碰撞箱尺寸与偏移一律以预制体为准，资产里即使残留这类条目也不应用。
    public static bool ControlsSize(Type type)=>type!=null&&(typeof(Collider2D).IsAssignableFrom(type)||typeof(Transform).IsAssignableFrom(type));
    // Never reapply on OnEnable: doing so would erase rebirth/scaled HP when an enemy leaves its pool.
    public void ApplyOnce()
    {
        if(applied||profile==null)return;applied=true;
        foreach(var group in profile.components)
        {
            var node=string.IsNullOrEmpty(group.objectPath)?transform:transform.Find(group.objectPath);
            if(node==null){Debug.LogError($"数值配置路径失效：{profile.name}/{group.objectPath}",this);continue;}
            Type type=ResolveType(group.componentType);
            if(type==null){Debug.LogError("数值配置组件失效："+group.componentType,this);continue;}
            if(ControlsSize(type))continue;
            var matches=node.GetComponents(type);
            if(group.componentIndex>=matches.Length){Debug.LogError("数值配置组件缺失："+group.componentType,this);continue;}
            foreach(var parameter in group.parameters)
                try{Assign(matches[group.componentIndex],parameter.path.Replace(".Array.data[","["),parameter.value);}
                catch(Exception e){Debug.LogError($"数值配置应用失败：{profile.name}/{group.componentType}/{parameter.path}: {e.Message}",this);}
        }
    }
    public static void Assign(object target,string path,double number)
    {
        // A path can cross arrays and value-type SpriteData/Combat records; write boxed parents back.
        var tokens=path.Replace("[",".[").Split('.');Write(target,tokens,0,number);
    }
    private static object Write(object target,string[] tokens,int index,double number)
    {
        if(index==tokens.Length)
        {
            Type type=target.GetType();
            if(type==typeof(bool))return number!=0;
            if(type==typeof(int))return (int)Math.Round(number);
            if(type==typeof(float))return (float)number;
            if(type==typeof(double))return number;
            throw new InvalidOperationException("Unsupported numeric field "+type);
        }
        string token=tokens[index];
        if(token.StartsWith("[",StringComparison.Ordinal))
        {
            var list=(IList)target;int item=int.Parse(token.Substring(1,token.Length-2));
            list[item]=Write(list[item],tokens,index+1,number);return target;
        }
        FieldInfo field=null;
        for(Type type=target.GetType();type!=null&&field==null;type=type.BaseType)
            field=type.GetField(token,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
        if(field==null)
        {
            string propertyName=token.StartsWith("m_",StringComparison.Ordinal)?char.ToLowerInvariant(token[2])+token.Substring(3):token;
            var property=target.GetType().GetProperty(propertyName,BindingFlags.Instance|BindingFlags.Public);
            if(property==null||!property.CanWrite)throw new MissingFieldException(target.GetType().Name,token);
            property.SetValue(target,Write(property.GetValue(target),tokens,index+1,number));return target;
        }
        field.SetValue(target,Write(field.GetValue(target),tokens,index+1,number));return target;
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BalanceProfile))]
public sealed class BalanceProfileEditor:Editor
{
    private string search="";private readonly HashSet<string> expanded=new();
    public override void OnInspectorGUI()
    {
        serializedObject.Update();EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.HelpBox("资产是基础数值的权威来源。修改后重新进入场景/重建训练目标；不覆盖存档养成、轮回倍率和临时增益。路径与键不可改，避免绑定失效。",MessageType.Info);
        search=EditorGUILayout.TextField("筛选（名称/字段/路径）",search);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("relatedProfiles"),new GUIContent("关联投射物/特效数值"),true);
        var skills=serializedObject.FindProperty("skills");if(skills.arraySize>0)EditorGUILayout.PropertyField(skills,new GUIContent("技能等级：费用 / 蓝耗 / 冷却（下标0=1级）"),true);
        var groups=serializedObject.FindProperty("components");
        for(int i=0;i<groups.arraySize;i++)
        {
            var group=groups.GetArrayElementAtIndex(i);string key=i.ToString();string title=group.FindPropertyRelative("componentType").stringValue+" / "+group.FindPropertyRelative("objectPath").stringValue;
            var values=group.FindPropertyRelative("parameters");bool filter=!string.IsNullOrWhiteSpace(search);bool open=filter||expanded.Contains(key);
            if(!filter){bool next=EditorGUILayout.Foldout(open,title,true);if(next)expanded.Add(key);else expanded.Remove(key);open=next;}
            if(!open)continue;
            if(filter)EditorGUILayout.LabelField(title,EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for(int j=0;j<values.arraySize;j++)
            {
                var p=values.GetArrayElementAtIndex(j);string label=p.FindPropertyRelative("label").stringValue;
                if(filter&&!title.ToLowerInvariant().Contains(search.ToLowerInvariant())&&!label.ToLowerInvariant().Contains(search.ToLowerInvariant()))continue;
                var value=p.FindPropertyRelative("value");var content=new GUIContent(label,p.FindPropertyRelative("path").stringValue);
                if(p.FindPropertyRelative("boolean").boolValue)value.doubleValue=EditorGUILayout.Toggle(content,value.doubleValue!=0)?1:0;
                else if(p.FindPropertyRelative("integer").boolValue)value.doubleValue=EditorGUILayout.IntField(content,(int)value.doubleValue);
                else value.doubleValue=EditorGUILayout.DoubleField(content,value.doubleValue);
            }
            EditorGUI.indentLevel--;
        }
        var numbers=serializedObject.FindProperty("numbers");
        bool show=EditorGUILayout.Foldout(expanded.Contains("constants"),"招式、范围与特殊机制（"+numbers.arraySize+"）",true);if(show)expanded.Add("constants");else expanded.Remove("constants");
        if(show||!string.IsNullOrWhiteSpace(search))for(int i=0;i<numbers.arraySize;i++)
        {
            var p=numbers.GetArrayElementAtIndex(i);string key=p.FindPropertyRelative("key").stringValue;
            if(!string.IsNullOrWhiteSpace(search)&&!key.ToLowerInvariant().Contains(search.ToLowerInvariant()))continue;
            EditorGUILayout.PropertyField(p.FindPropertyRelative("value"),new GUIContent(p.FindPropertyRelative("label").stringValue,key+"\n"+p.FindPropertyRelative("source").stringValue));
        }
        if(serializedObject.ApplyModifiedProperties())((BalanceProfile)target).Invalidate();
    }
}

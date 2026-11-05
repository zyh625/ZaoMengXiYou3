using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BalanceValidation
{
    static BalanceValidation(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("BalanceValidation",false)){SessionState.SetBool("BalanceValidation",false);new GameObject("BalanceValidation").AddComponent<BalanceRuntimeValidation>();}};}
    [MenuItem("Tools/数值配置/校验资产与预制体绑定")]
    public static void CheckAssets()
    {
        var profiles=Resources.LoadAll<BalanceProfile>("Balance");int groups=0,parameters=0,prefabs=0;
        foreach(var path in Directory.GetFiles("Assets/Projects/Prefabs","*.prefab",SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/Projects/Resources/Gold","*.prefab")))
        {
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(original.GetComponent<BalanceBinding>()==null)continue;
            var instance=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var binding=instance.GetComponent<BalanceBinding>();var profile=binding.Profile;if(profile==null)throw new Exception("Missing profile "+path);
                binding.ApplyOnce();prefabs++;
                foreach(var group in profile.components)
                {
                    var t=group.objectPath==""?instance.transform:instance.transform.Find(group.objectPath);if(t==null)throw new Exception("Missing path "+group.objectPath);
                    var component=t.GetComponents(BalanceBinding.ResolveType(group.componentType))[group.componentIndex];var so=new SerializedObject(component);groups++;
                    foreach(var p in group.parameters)
                    {
                        var value=so.FindProperty(p.path);if(value==null)throw new Exception("Missing parameter "+p.path);
                        double actual=p.boolean?(value.boolValue?1:0):p.integer?value.intValue:value.doubleValue;
                        if(Math.Abs(actual-p.value)>.0001*Math.Max(1,Math.Abs(p.value)))throw new Exception("Parameter not applied "+profile.name+"/"+p.path);
                        parameters++;
                    }
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(instance);}
        }
        if(profiles.Count(p=>AssetDatabase.GetAssetPath(p).Contains("/Players/"))!=4)throw new Exception("Expected four separate player profiles");
        if(profiles.Count(p=>AssetDatabase.GetAssetPath(p).Contains("/Enemies/")||AssetDatabase.GetAssetPath(p).Contains("/Bosses/"))!=27)throw new Exception("Expected 27 enemy/boss profiles");
        Directory.CreateDirectory("output/BalanceAudit");File.WriteAllText("output/BalanceAudit/asset-validation.txt",$"PASS {profiles.Length} profiles / {prefabs} prefab bindings / {groups} component groups / {parameters} parameter assignments\nPASS four independent player skill tables / 27 enemy and boss profiles\n");
    }
    public static void Run()
    {
        CheckAssets();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("BalanceValidation",true);EditorApplication.EnterPlaymode();
    }
    public static void PrepareAndRun(){BalanceAssetBuilder.FinalizeMetadata();Run();}
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LateStagesValidationBridge
{
    static LateStagesValidationBridge(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        Directory.CreateDirectory("output/LateStagesAudit");
        foreach(string path in Directory.EnumerateFiles("Assets/Projects/Prefabs/SeparateEffort","*.prefab"))
            if(new[]{"JiuyaoXuanhuang","RiftBlade","SealWarden","PhaseWeaver","WuxiangDijun","WuxiangMirror"}.Any(n=>Path.GetFileName(path).StartsWith(n,StringComparison.Ordinal)))
                CheckReferences(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        CheckReferences(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/UI/RebirthEntry.prefab"));
        foreach(string name in new[]{"JiuyaoXuanhuang","RiftBlade","SealWarden","PhaseWeaver","WuxiangDijun"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projects/Prefabs/Enemy/"+name+".prefab");
            if(prefab==null)throw new Exception("Missing prefab "+name);
            CheckReferences(prefab);
            var clips=(SpriteData[])typeof(LateStageController).GetField("skillAnimations",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(prefab.GetComponent<LateStageController>());
            foreach(var clip in clips)if(clip.sprites.Length!=16||clip.sprites.Any(s=>s==null))throw new Exception("Unfilled animation "+name);
        }
        foreach(int level in new[]{8,9})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Projects/Scenes/Levels/Level"+level+"Scene.unity");
            var stage=UnityEngine.Object.FindAnyObjectByType<StageManager>();
            if(stage==null||!stage.enabled||!stage.ValidateConfiguration(out var error))throw new Exception("Stage configuration "+level);
            foreach(var root in scene.GetRootGameObjects())CheckReferences(root);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        File.WriteAllText("output/LateStagesAudit/native-assets.txt","PASS five actors / clips / two authored scenes / complete stage configuration / no missing references\n");
        SessionState.SetBool("LateStagesRun",true);EditorApplication.EnterPlaymode();
    }
    private static void CheckReferences(GameObject root)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
        foreach(var c in root.GetComponentsInChildren<Component>(true))
        {
            if(c==null)throw new Exception("Missing component");
            var p=new SerializedObject(c).GetIterator();
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference)
            {
#pragma warning disable CS0618
                if(p.objectReferenceValue==null&&p.objectReferenceEntityIdValue!=default)throw new Exception(root.name+" missing "+p.propertyPath);
#pragma warning restore CS0618
            }
        }
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("LateStagesRun",false))return;
        SessionState.SetBool("LateStagesRun",false);new GameObject("LateStagesValidation").AddComponent<LateStagesRuntimeValidation>();
    }
}

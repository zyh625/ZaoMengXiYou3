using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Level7EffectsValidation
{
    [Serializable] private sealed class Entry { public string key = "", texture = "", prefab = ""; public float[] pivot = Array.Empty<float>(); }
    [Serializable] private sealed class Manifest { public Entry[] effects = Array.Empty<Entry>(); }
    [Serializable] private sealed class Report
    {
        public string status, unityVersion, error;
        public int sheets, frames, prefabs;
    }
    private const string Root = "Assets/Art/Enemies/Level7/";

    [MenuItem("Tools/Validation/Level7 Effect Assets")]
    public static void Run()
    {
        var report = new Report { status = "running", unityVersion = Application.unityVersion };
        try
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "Level7FX.AnimationNotes.json"));
            Require(manifest.effects.Length == 5, "Expected five effects");
            foreach (var e in manifest.effects)
            {
                AssetDatabase.ImportAsset(e.texture, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(e.texture);
                var importer = AssetImporter.GetAtPath(e.texture) as TextureImporter;
                Require(texture != null && texture.width == 1024 && texture.height == 1024, e.key + " texture size");
                Require(importer != null && importer.spriteImportMode == SpriteImportMode.Multiple
                    && importer.alphaIsTransparency && !importer.mipmapEnabled
                    && Mathf.Approximately(importer.spritePixelsPerUnit, 100), e.key + " importer settings");
                var sprites = AssetDatabase.LoadAllAssetsAtPath(e.texture).OfType<Sprite>()
                    .OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                Require(sprites.Length == 16, e.key + " slice count");
                for (int i = 0; i < 16; i++)
                {
                    Require(sprites[i].name == e.key + "_" + (i + 1).ToString("00"), e.key + " frame order");
                    Require(sprites[i].rect == new Rect(i % 4 * 256, (3 - i / 4) * 256, 256, 256), e.key + " slice rectangle");
                    Require(Vector2.Distance(sprites[i].pivot, new Vector2(e.pivot[0] * 256, e.pivot[1] * 256)) < .02f, e.key + " pivot");
                }
                report.sheets++; report.frames += 16;
                AssetDatabase.ImportAsset(e.prefab, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var go = PrefabUtility.LoadPrefabContents(e.prefab);
                try
                {
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0, e.key + " missing script");
                    var fx = go.GetComponent<IndependentSkillEffect>();
                    var renderer = go.GetComponent<SpriteRenderer>();
                    Require(fx != null && go.GetComponent<SpriteManager>() != null && renderer != null
                        && renderer.sharedMaterial != null && fx.FrameCount == 16, e.key + " prefab components");
                    var so = new SerializedObject(fx);
                    var sequence = so.FindProperty("frames");
                    var frames = sequence.FindPropertyRelative("sprites");
                    Require(frames.arraySize == 16 && !sequence.FindPropertyRelative("loop").boolValue
                        && Mathf.Approximately(sequence.FindPropertyRelative("fps").floatValue, 16), e.key + " animation settings");
                    for (int i = 0; i < 16; i++)
                        Require(frames.GetArrayElementAtIndex(i).objectReferenceValue == sprites[i], e.key + " prefab sprite reference " + i);
                    Require(renderer.sprite == sprites[0], e.key + " initial sprite");
                    report.prefabs++;
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            report.status = "passed";
        }
        catch (Exception ex) { report.status = "failed"; report.error = ex.ToString(); Debug.LogException(ex); }
        File.WriteAllText(Root + "Level7FX.UnityValidation.json", JsonUtility.ToJson(report, true));
        Debug.Log("LEVEL7_FX_VALIDATION: " + report.status + ", " + report.frames + " frames, " + report.prefabs + " prefabs.");
        if (Application.isBatchMode) EditorApplication.Exit(report.status == "passed" ? 0 : 1);
        else Require(report.status == "passed", report.error);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

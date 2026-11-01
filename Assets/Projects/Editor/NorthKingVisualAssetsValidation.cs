using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NorthKingVisualAssetsValidation
{
    private const string CharacterRoot = "Assets/Art/Bosses/NorthKing/SpriteSheets16/";
    private const string PrefabRoot = "Assets/Projects/Prefabs/SeparateEffort/";
    private static readonly string[] Actions = { "Idle", "Walk", "UmbrellaBash", "UmbrellaVortex", "RibVolley", "RainCurtain", "Guard", "Hit", "Death" };
    private static readonly string[] Effects = { "GreenSummonHammer", "GreenSummonWarBannerAura", "NorthKingUmbrellaRib", "NorthKingRainCurtain", "NorthKingShield" };

    [Serializable]
    private sealed class Report
    {
        public string status;
        public string unityVersion;
        public int characterSheets, characterFrames, effectSheets, effectFrames, prefabs;
        public List<string> checks = new List<string>();
        public string error;
    }

    [MenuItem("Tools/Validation/NorthKing and GreenSummon Visual Assets")]
    public static void Run()
    {
        var report = new Report { status = "running", unityVersion = Application.unityVersion };
        try
        {
            foreach (string action in Actions)
            {
                ValidateSheet(CharacterRoot + action + ".png", action, 1024, 1536, 256, 384, new Vector2(.5f, 56f / 384f));
                report.characterSheets++;
                report.characterFrames += 16;
            }
            foreach (string key in Effects)
            {
                string root = key.StartsWith("GreenSummon", StringComparison.Ordinal)
                    ? "Assets/Art/Enemies/Level6/GreenSummon/Effects/" : CharacterRoot + "Effects/";
                ValidateSheet(root + key + ".png", key, 1024, 1024, 256, 256, null);
                ValidatePrefab(PrefabRoot + key + "FX.prefab");
                report.effectSheets++;
                report.effectFrames += 16;
                report.prefabs++;
            }
            report.checks.Add("Native TextureImporter dimensions, Multiple mode, PPU and 224 named sprite rectangles validated.");
            report.checks.Add("Five prefabs load with no missing scripts, with 16 valid texture subasset references and assigned materials.");
            report.checks.Add("Play/Pause/Resume/Stop, following anchor placement, pool reactivation and reentrant completion replay validated in isolated prefab contents.");
            report.checks.Add("Visual-only assets; no scene, damage, collider, AI or runtime combat integration was changed or exercised.");
            report.status = "passed";
            WriteReport(report);
            Debug.Log("NORTHKING_VISUAL_ASSETS_VALIDATED: 9 character sheets / 144 frames, 5 FX sheets / 80 frames, 5 independent prefabs.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            report.status = "failed";
            report.error = ex.ToString();
            WriteReport(report);
            Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ValidateSheet(string path, string prefix, int width, int height, int cellWidth, int cellHeight, Vector2? expectedPivot)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Require(texture != null && texture.width == width && texture.height == height, path + ": wrong imported texture size");
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Require(importer != null && importer.spriteImportMode == SpriteImportMode.Multiple, path + ": not Multiple sprites");
        Require(Mathf.Approximately(importer.spritePixelsPerUnit, 100f), path + ": incorrect PPU");
        Require(importer.alphaIsTransparency && !importer.mipmapEnabled, path + ": incorrect alpha/mipmap settings");
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        Require(sprites.Length == 16, path + ": expected sixteen imported sprites");
        for (int i = 0; i < 16; i++)
        {
            Sprite sprite = sprites[i];
            Require(sprite.name == prefix + "_" + (i + 1).ToString("00"), path + ": frame names out of order");
            var rect = new Rect(i % 4 * cellWidth, (3 - i / 4) * cellHeight, cellWidth, cellHeight);
            Require(sprite.rect == rect, path + ": wrong slice rectangle for " + sprite.name);
            if (expectedPivot.HasValue)
            {
                Vector2 pivot = Vector2.Scale(expectedPivot.Value, new Vector2(cellWidth, cellHeight));
                Require(Vector2.Distance(sprite.pivot, pivot) < .02f, path + ": wrong common ground pivot");
            }
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id) && id != 0 && !string.IsNullOrEmpty(guid), path + ": missing sprite subasset ID");
        }
    }

    private static void ValidatePrefab(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0, path + ": missing script");
            var fx = root.GetComponent<IndependentSkillEffect>();
            var renderer = root.GetComponent<SpriteRenderer>();
            Require(fx != null && root.GetComponent<SpriteManager>() != null && renderer != null, path + ": incomplete visual components");
            Require(renderer.sharedMaterial != null, path + ": missing transparent material");
            var serialized = new SerializedObject(fx);
            SerializedProperty frames = serialized.FindProperty("frames").FindPropertyRelative("sprites");
            Require(frames.arraySize == 16 && fx.FrameCount == 16, path + ": wrong frame count");
            for (int i = 0; i < frames.arraySize; i++)
                Require(frames.GetArrayElementAtIndex(i).objectReferenceValue is Sprite, path + ": missing sprite at " + i);
            Require(renderer.sprite == frames.GetArrayElementAtIndex(0).objectReferenceValue, path + ": first-frame binding mismatch");
            fx.Play();
            Require(fx.IsPlaying && root.activeSelf, path + ": Play failed");
            fx.Pause();
            Require(fx.IsPaused, path + ": Pause failed");
            fx.Resume();
            Require(fx.IsPlaying && !fx.IsPaused, path + ": Resume failed");
            fx.StopEffect();
            Require(!fx.IsPlaying && !root.activeSelf, path + ": Stop did not return instance to inactive state");
            var anchor = new GameObject("TemporaryIndependentFXValidationAnchor");
            try
            {
                anchor.transform.position = new Vector3(3, 4, 0);
                fx.Play(anchor.transform, Vector3.up, true, 0f);
                Require(root.activeSelf && fx.IsPlaying && renderer.flipX, path + ": pooled replay/mirror failed");
                Require(Vector3.Distance(root.transform.position, new Vector3(3, 5, 0)) < .001f, path + ": follow anchor placement failed");
                fx.Completed.AddListener(() => fx.Play());
                fx.StopEffect();
                Require(root.activeSelf && fx.IsPlaying, path + ": completion replay was incorrectly disabled");
                fx.Completed.RemoveAllListeners();
            }
            finally { UnityEngine.Object.DestroyImmediate(anchor); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void WriteReport(Report report)
    {
        string path = PrefabRoot + "GreenSummon_NorthKing_FX.UnityValidation.json";
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
    }
}

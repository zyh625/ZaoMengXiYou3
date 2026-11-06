using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

//项目共用的中文 TMP 字体：由 Noto Sans SC（SIL 开源字体许可，可随游戏发布）生成动态字体资产，
//只在用到某个字时才写入图集；并加入 TMP 全局备用字体，任何 TMP 文本缺中文时都会自动使用它。
public static class ChineseFontSetup
{
    public const string FontPath = "Assets/Projects/Fonts/NotoSansSC-Regular.ttf";
    public const string AssetPath = "Assets/Projects/Fonts/NotoSansSC-Regular SDF.asset";

    [MenuItem("造梦西游/字体/生成中文字体")]
    public static void Run()
    {
        TMP_FontAsset font = Ensure();
        EditorGUIUtility.PingObject(font);
        EditorUtility.DisplayDialog("中文字体", "已生成并加入 TMP 全局备用字体：\n" + AssetPath, "确定");
    }

    public static TMP_FontAsset Ensure()
    {
        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        if (asset == null) asset = Create();
        AddGlobalFallback(asset);
        return asset;
    }

    private static TMP_FontAsset Create()
    {
        AssetDatabase.ImportAsset(FontPath);
        UnityEngine.Font font = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(FontPath)
            ?? throw new InvalidOperationException("缺少字体文件 " + FontPath);
        //调用 TMP 自带的“Create > TextMeshPro > Font Asset > SDF”，生成结果与手动右键创建完全一致
        Type menu = typeof(TMP_FontAsset).Assembly.GetType("TMPro.TMP_FontAsset_CreationMenu")
            ?? Type.GetType("TMPro.TMP_FontAsset_CreationMenu, Unity.TextMeshPro.Editor");
        if (menu == null)
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if ((menu = assembly.GetType("TMPro.TMP_FontAsset_CreationMenu")) != null) break;
        MethodInfo create = menu?.GetMethod("CreateFontAssetFromSelectedObject", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("找不到 TMP 的字体创建方法，请手动右键字体文件 Create > TextMeshPro > Font Asset > SDF。");
        create.Invoke(null, new object[] { font, GlyphRenderMode.SDFAA });
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath)
            ?? throw new InvalidOperationException("中文字体资产生成失败：" + AssetPath);
    }

    private static void AddGlobalFallback(TMP_FontAsset asset)
    {
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null) return;
        TMP_Settings.fallbackFontAssets ??= new System.Collections.Generic.List<TMP_FontAsset>();
        if (TMP_Settings.fallbackFontAssets.Contains(asset)) return;
        TMP_Settings.fallbackFontAssets.Add(asset);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}

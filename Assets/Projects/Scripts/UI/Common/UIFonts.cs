using TMPro;
using UnityEngine;

//代码生成的 TMP 文字直接用中文字体，不依赖默认英文字体的后备链（后备链在测量隐藏文字时可能找不到汉字）。
public static class UIFonts
{
    private static TMP_FontAsset chinese;

    public static TMP_FontAsset Chinese
    {
        get
        {
            if (chinese != null) return chinese;
            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.name.StartsWith("NotoSansSC")) { chinese = font; break; }
            if (chinese == null && TMP_Settings.fallbackFontAssets != null)
                foreach (TMP_FontAsset font in TMP_Settings.fallbackFontAssets)
                    if (font != null) { chinese = font; break; }
            return chinese != null ? chinese : TMP_Settings.defaultFontAsset;
        }
    }
}

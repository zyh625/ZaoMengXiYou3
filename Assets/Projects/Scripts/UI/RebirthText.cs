using UnityEngine;
using UnityEngine.UI;

//项目原有字体只含拉丁字形；轮回文字使用系统中文动态字体，兼容Windows运行时。
[RequireComponent(typeof(Text))]
public sealed class RebirthText : MonoBehaviour
{
    private static UnityEngine.Font font;
    private void Awake()
    {
        if (font == null) font = UnityEngine.Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 24);
        if (font != null) GetComponent<Text>().font = font;
    }
}

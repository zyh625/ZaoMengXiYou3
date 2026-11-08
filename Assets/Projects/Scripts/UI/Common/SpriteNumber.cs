using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//用图片字体显示数字串（支持0-9、"/"、"%"，"L"显示“Lv”字样），在自身矩形里水平、垂直居中。
//字高等于矩形高度，总宽度超出矩形时整体等比缩小；"/"和"%"未指定图片时显示白色占位块。
[RequireComponent(typeof(RectTransform))]
public sealed class SpriteNumber : MonoBehaviour
{
    [SerializeField] private Sprite[] digits = new Sprite[10];//下标即数字
    [SerializeField] private Sprite slash, percent;//以后替换成正式图片即可
    [SerializeField] private Sprite levelMark;//“Lv”字样，文本里用字母 L 表示
    [SerializeField] private float spacing = 0.06f;//字间距，按字高比例；负数可让留白较多的字图靠拢
    [SerializeField] private float placeholderWidth = 0.35f;//白色占位块宽度，按字高比例
    [SerializeField] private Color color = Color.white;
    [SerializeField] private string preview = "0";//进入运行前显示的内容，便于在编辑器中检查大小

    private readonly List<Image> glyphs = new();
    private string shown;

    private void Awake()
    {
        if (shown == null) SetText(preview);
    }

    public void SetValue(int value) => SetText(value.ToString());

    public void SetText(string text)
    {
        text ??= string.Empty;
        if (text == shown) return;
        shown = text;
        var rect = (RectTransform)transform;
        float height = rect.rect.height;
        var widths = new float[text.Length];
        float total = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            Sprite sprite = SpriteFor(text[i]);
            widths[i] = sprite != null ? height * sprite.rect.width / Mathf.Max(1f, sprite.rect.height) : height * placeholderWidth;
            total += widths[i] + (i > 0 ? height * spacing : 0f);
        }
        float scale = total > rect.rect.width && total > 0f ? rect.rect.width / total : 1f;
        float x = -total * scale * 0.5f;
        for (int i = 0; i < text.Length; i++)
        {
            Image image = Glyph(i);
            Sprite sprite = SpriteFor(text[i]);
            image.sprite = sprite;
            image.color = color;
            image.gameObject.SetActive(true);
            var glyph = (RectTransform)image.transform;
            float width = widths[i] * scale;
            glyph.sizeDelta = new Vector2(width, height * scale);
            glyph.anchoredPosition = new Vector2(x + width * 0.5f, 0f);
            x += width + height * spacing * scale;
        }
        for (int i = text.Length; i < glyphs.Count; i++) glyphs[i].gameObject.SetActive(false);
    }

    private Sprite SpriteFor(char c)
    {
        if (c >= '0' && c <= '9') return digits != null && c - '0' < digits.Length ? digits[c - '0'] : null;
        return c == '/' ? slash : c == '%' ? percent : c == 'L' ? levelMark : null;
    }

    private Image Glyph(int index)//运行时按需创建子物体，之后重复使用
    {
        while (glyphs.Count <= index)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;
            glyphs.Add(image);
        }
        return glyphs[index];
    }
}

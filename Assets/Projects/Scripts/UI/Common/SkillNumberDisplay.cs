using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//复用技能面板已有的图片数字，保留每一组数字原本的字体、位置和大小。
public sealed class SkillNumberDisplay
{
    private readonly List<Image> digits = new();//缓存原数字与后来扩充的位数
    private readonly Sprite[] numberSprites;//Numbers图片中0到9的精灵
    private readonly bool goldStyle;//通过原图片纹理判断字体，不把所有数字换成同一种样式
    private readonly int originalCount;//原有数字槽数量，冷却的秒单位不计入
    private readonly Vector2 spacing;//以后费用超过三位数时沿原间距扩充
    private readonly RectTransform suffix;//冷却后面的秒单位图片
    private readonly Vector2 suffixPosition;//数字位数恢复后把单位恢复到原位置
    private int previousValue = -1;//相同数值不重复替换图片或扩充对象

    public SkillNumberDisplay(Transform container, int digitCount, Sprite[] numberSprites, Transform suffix = null, int firstDigit = 0)
    {
        this.numberSprites = numberSprites;
        this.suffix = suffix as RectTransform;
        if (this.suffix != null) suffixPosition = this.suffix.anchoredPosition;
        if (container == null) return;
        for (int i = 0; i < digitCount; i++)
        {
            Transform child = container.Find(digitCount == 1 && firstDigit == 0 ? "num" : $"num{i + firstDigit}");//等级使用num，余额数字从num1开始
            Image image = child != null ? child.GetComponent<Image>() : null;
            if (image != null) digits.Add(image);
        }
        originalCount = digits.Count;
        if (originalCount == 0) return;
        Sprite first = digits[0].sprite;//费用和等级沿用Numbers，MP与冷却沿用Resources/Digits
        goldStyle = first != null && numberSprites != null && numberSprites.Length >= 10 &&
            numberSprites[0] != null && first.texture == numberSprites[0].texture;
        spacing = originalCount > 1
            ? digits[1].rectTransform.anchoredPosition - digits[0].rectTransform.anchoredPosition
            : new Vector2(digits[0].rectTransform.rect.width * 0.7f, 0f);
        spacing = new Vector2(spacing.x, 0f);//原数字行的微小浮点误差不累积到新增位数
    }

    public void SetValue(int value)
    {
        value = Mathf.Max(0, value);
        if (value == previousValue || originalCount == 0) return;
        previousValue = value;
        string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);//不补无意义的前导零
        while (digits.Count < text.Length)
        {
            Image last = digits[digits.Count - 1];//只有超过已有位数才复制，避免大额费用被截成后三位
            Image image = Object.Instantiate(last, last.transform.parent);
            image.name = $"extraDigit{digits.Count}";
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = last.rectTransform.anchoredPosition + spacing;
            digits.Add(image);
        }
        for (int i = 0; i < digits.Count; i++)
        {
            Image image = digits[i];
            bool visible = i < text.Length;//物体本身关闭时只开启Image仍然不可见，数字位必须一起切换
            image.enabled = visible;
            image.gameObject.SetActive(visible);
            if (!visible) continue;
            int number = text[i] - '0';
            image.sprite = goldStyle ? numberSprites[number] : Font.GetSprite(number);
        }
        if (suffix != null)
            suffix.anchoredPosition = suffixPosition + spacing * Mathf.Max(0, text.Length - originalCount);
    }
}

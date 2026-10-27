using UnityEngine;
using UnityEngine.UI;

//胜负界面共用原有数字图片，只更新内容和必要位数，不调整现有控件布局。
internal static class BattleResultDisplay
{
    internal static void SetTime(Image[] times, int seconds)
    {
        int value = Mathf.Max(0, seconds);
        int hours = Mathf.Min(99, value / 3600);
        int minutes = value % 3600 / 60;
        int remainder = value % 60;
        int[] digits = { hours / 10, hours % 10, minutes / 10, minutes % 10, remainder / 10, remainder % 10 };
        for (int i = 0; times != null && i < times.Length && i < digits.Length; i++)
            if (times[i] != null) times[i].sprite = Font.GetSprite(digits[i]);
    }

    internal static void SetPercent(Image[] digits, Image hundreds, int percentage)
    {
        int value = Mathf.Clamp(percentage, 0, 100);
        if (hundreds != null)
        {
            hundreds.gameObject.SetActive(value == 100);
            if (value == 100) hundreds.sprite = Font.GetSprite(1);
        }
        if (digits != null && digits.Length > 0 && digits[0] != null)
        {
            digits[0].gameObject.SetActive(value >= 10);
            digits[0].sprite = Font.GetSprite(value / 10 % 10);
        }
        if (digits != null && digits.Length > 1 && digits[1] != null)
        {
            digits[1].gameObject.SetActive(true);
            digits[1].sprite = Font.GetSprite(value % 10);
        }
    }

    internal static void SetMVP(Image image, Sprite[] pictures, int id)
    {
        if (image != null && pictures != null && id >= 0 && id < pictures.Length && pictures[id] != null)
            image.sprite = pictures[id];
    }
}

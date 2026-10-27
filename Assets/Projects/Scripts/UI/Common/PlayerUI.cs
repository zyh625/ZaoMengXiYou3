using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] private Sprite portrait;//角色头像资源，换人时由共享UI显示
    private Image hpUI, mpUI, xpUI, vigorUI;
    private TextMeshProUGUI hp, mp, xp;
    private Image angryEnegy, angryFull;
    public void SetUI(Image[] images,TextMeshProUGUI[] texts, Image[] angrys)
    {
        hpUI = GetElement(images, 0); hp = GetElement(texts, 0);
        mpUI = GetElement(images, 1); mp = GetElement(texts, 1);
        xpUI= GetElement(images, 2);xp= GetElement(texts, 2);
        vigorUI= GetElement(images, 3);
        angryEnegy = GetElement(angrys, 0);
        angryFull= GetElement(angrys, 1);
    }
    public void SetIdentityUI(Image portraitImage)//角色只提供自己的新头像，场景Head保持原位置和尺寸
    {
        if (portraitImage != null && portrait != null) portraitImage.sprite = portrait;
    }
    public void ClearUI()//换出后断开共享控件，后续属性变化不会写入当前角色的UI
    {
        hpUI = mpUI = xpUI = vigorUI = angryEnegy = angryFull = null;
        hp = mp = xp = null;
    }
    public void UpdateHP(float a, float b) => UpdateUI(hpUI, hp, a, b);
    public void UpdateMP(float a, float b) => UpdateUI(mpUI, mp, a, b);
    public void UpdateXP(float a, float b) => UpdateUI(xpUI, xp, a, b);
    public void UpdateVigor(float a, float b) => UpdateUI(vigorUI, null, a, b);//气力只更新原有能量槽，不显示具体数字
    public void UpdateBoost(float energy, float maximum, bool active)//只更新原有能量Image，不调整任何UI位置或素材
    {
        if (angryEnegy != null)
            angryEnegy.fillAmount = maximum > 0f ? Mathf.Clamp01(energy / maximum) : 0f;
        if (angryFull != null)
            angryFull.gameObject.SetActive(!active && maximum > 0f && energy >= maximum);
    }
    private void UpdateUI(Image img,TextMeshProUGUI text, float cur,float pri)
    {
        text?.SetText($"{(int)cur}/{(int)pri}");
        if (img != null) img.fillAmount = pri > 0f ? Mathf.Clamp01(cur / pri) : 0f;//首次接线前的生命事件也可以安全接收
    }
    private static T GetElement<T>(T[] values, int index) where T : class =>
        values != null && index < values.Length ? values[index] : null;//允许尚未接入的UI项目留空
}

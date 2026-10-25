using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    private Image hpUI, mpUI, xpUI, vigorUI;
    private TextMeshProUGUI hp, mp, xp;
    private Image angryEnegy, angryFull;
    public void SetUI(Image[] images,TextMeshProUGUI[] texts, Image[] angrys)
    {
        hpUI = images[0]; hp = texts[0];
        mpUI = images[1]; mp = texts[1];
        xpUI= images[2];xp= texts[2];
        vigorUI= images[3];
        angryEnegy = angrys[0];
        angryFull= angrys[1];
    }
    public void UpdateHP(float a, float b) => UpdateUI(hpUI, hp, a, b);
    public void UpdateMP(float a, float b) => UpdateUI(mpUI, mp, a, b);
    public void UpdateXP(float a, float b) => UpdateUI(xpUI, xp, a, b);
    public void UpdateVigor(float a, float b) => UpdateUI(vigorUI, null, a, b);
    private void UpdateUI(Image img,TextMeshProUGUI text, float cur,float pri)
    {
        text?.SetText($"{(int)cur}/{(int)pri}");
        img.fillAmount = cur / pri;
    }
}

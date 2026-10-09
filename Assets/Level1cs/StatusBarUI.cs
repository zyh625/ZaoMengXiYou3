using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatusBarUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI ui;
    [SerializeField] private Image img;
    public void UpdateValue(float cur,float pri)
    {
        ui.SetText($"{(int)cur}/{(int)pri}");
        img.fillAmount = cur / pri;
    } 
}

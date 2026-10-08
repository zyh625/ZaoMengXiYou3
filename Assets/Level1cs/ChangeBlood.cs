using UnityEngine;
using UnityEngine.UI;
public class ChangeBlood : MonoBehaviour
{
    [SerializeField] private GameObject bar;
    [SerializeField] private Image bloodUI;
    public void ShowUI()
    {
        bloodUI.fillAmount = 1;
        bar.SetActive(true);
    }
    public void UpdateBlood(float cur,float pri)
    {
        bloodUI.fillAmount = cur / pri;
    }
}

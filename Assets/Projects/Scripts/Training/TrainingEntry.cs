using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TrainingEntry : MonoBehaviour
{
    private UnityEngine.Font font;
    private void Awake()
    {
        UnityEngine.UI.Text label=GetComponentInChildren<UnityEngine.UI.Text>();if(label==null)return;
        foreach(string name in UnityEngine.Font.GetOSInstalledFontNames())if(name=="Microsoft YaHei" || name=="PingFang SC" || name=="Noto Sans CJK SC")
        {font=UnityEngine.Font.CreateDynamicFontFromOSFont(name,24);label.font=font;label.text="训练场 / TRAINING";return;}
        label.text="TRAINING";
    }
    private void OnDestroy(){if(font!=null)Destroy(font);}
    public void OpenTraining(){GamePause.Reset();TrainingSession.Begin();SceneManager.LoadScene(TrainingSession.SceneName);}
}

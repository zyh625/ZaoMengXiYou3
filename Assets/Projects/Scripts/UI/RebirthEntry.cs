using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Drop the prefab into any existing Canvas. No runtime creation or layout/Sprite writes.
public sealed class RebirthEntry : MonoBehaviour
{
    [SerializeField] private Button entryButton;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Text message;
    private GameSession session;private bool entering;
    private void OnEnable(){session=GameSession.instance;if(session!=null)session.DataChanged+=Refresh;if(entryButton!=null)entryButton.onClick.AddListener(RequestRebirth);Refresh();}
    private void OnDisable(){if(session!=null)session.DataChanged-=Refresh;if(entryButton!=null)entryButton.onClick.RemoveListener(RequestRebirth);}
    private void Refresh(){if(entryButton!=null)entryButton.interactable=!entering&&session!=null&&session.CanRebirth;}
    public void RequestRebirth()
    {
        if(entering||session==null||!session.CanRebirth)return;
        if(confirmationPanel!=null){confirmationPanel.SetActive(true);if(message!=null)message.text="再入轮回？保留能力、金币与解锁，从第1关重新开始。";}
        else ConfirmRebirth(); // The explicitly clicked entry is the confirmation if no optional panel is assigned.
    }
    public void CancelRebirth(){if(confirmationPanel!=null)confirmationPanel.SetActive(false);}
    public void ConfirmRebirth()
    {
        if(entering||session==null||!session.CanRebirth)return;
        if(!Application.CanStreamedLevelBeLoaded("RebirthScene")){if(message!=null)message.text="轮回漫画场景尚未加入构建列表";return;}
        entering=true;bool saved=SceneManager.GetActiveScene().name=="MapScene"?session.TryBeginRebirthFromMap():session.TryBeginRebirth(session.battle);
        if(!saved){entering=false;if(message!=null)message.text="保存失败，轮回资格已保留，请重试";Refresh();return;}
        Refresh();SceneManager.LoadScene("RebirthScene");
    }
}

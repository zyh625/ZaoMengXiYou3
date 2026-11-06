using UnityEngine;
using UnityEngine.SceneManagement;

//旧第一关的出口只负责动画；这里补上通关结算：解锁下一关并存档，再回地图。
public sealed class Level1ExitTrigger : MonoBehaviour
{
    private bool entered;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (entered) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;
        entered = true;

        GameSession session = GameSession.EnsureSession();
        if (session.data != null)
        {
            session.data.gameLevel = Mathf.Max(session.data.gameLevel, 2);
            session.maxLevel = session.data.gameLevel;
        }
        SaveSystem.Save();
        SceneManager.LoadScene("MapScene");
    }
}

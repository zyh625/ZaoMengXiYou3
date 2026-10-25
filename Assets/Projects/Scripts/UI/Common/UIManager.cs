using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Image[] playerStates;//血量、蓝量、经验、气力
    [SerializeField] private Image[] playerAngry;//无双能量槽
    [SerializeField]private TextMeshProUGUI[] texts;

    private PlayerUI player;
    public void SetPlayer(PlayerController curPlayer) => 
        player = curPlayer.GetComponent<PlayerUI>();
    public void InitPlayer() => player.SetUI(playerStates, texts, playerAngry);
}

using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonPress : MonoBehaviour
{
    public void NewGame()//新游戏
    {
        SceneManager.LoadScene("MapScene");
    }
    public void ContinueGame()//继续游戏
    {

    }
    public void AboutGame()//关于游戏
    {

    }
    public  void ExitGame()//退出游戏
    {
        SaveSystem.Save();
        Application.Quit();
    }
}

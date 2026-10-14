using UnityEngine;
using UnityEngine.SceneManagement;

public class SkillManager : MonoBehaviour
{
    [SerializeField] private GameObject[] Skills;//猴，猪，沙，唐
    private int curCharactor = 0;//默认打开猴子的技能面板
    public void PressCharactor(int num)//点击哪个角色就打开哪个面板
    {
        if (num >= Skills.Length) return;
        Skills[curCharactor].SetActive(false);
        Skills[num].SetActive(true);
        curCharactor = num;
    }
    public void PressBack()
    {
        SceneManager.LoadScene(GameSession.instance.priScene);
    }
}

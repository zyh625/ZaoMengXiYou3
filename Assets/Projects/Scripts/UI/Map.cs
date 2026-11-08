using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Map : MonoBehaviour
{
    private SaveData data;
    private float beginAlpha;//“保存成功”的字体初始透明度
    [SerializeField] private Button[] buttons;
    [SerializeField] private Image saveSucess;
    [SerializeField] private float saveDuration = 2f;//"保存游戏"逐渐消失
    [SerializeField] private float saveKeep = 2f;//"保存游戏"先一直显示
    [SerializeField] private GameObject handBook, skills;
    [SerializeField] private Text rebirthLabel;
    [SerializeField] private GameObject rebirthTime;
    [SerializeField] private Image time1, time2_1, time2_2;
    [SerializeField] private Sprite[] rebirthDigits;
    private bool isSaving = false;
    private float beginSave = 0f;
    private GameSession instance;
    public bool CanRebirth => instance != null && instance.CanRebirth;
    public void EnterRebirth()
    {
        if(instance==null)instance=GameSession.instance;
        if(instance==null || !Application.CanStreamedLevelBeLoaded("RebirthScene"))return;
        if(instance.TryBeginRebirthFromMap())SceneManager.LoadScene("RebirthScene");
    }
    private void Start()
    {
        beginAlpha = saveSucess.color.a;
        instance = GameSession.EnsureSession();
        instance.DataChanged += RefreshProgress;
        RefreshProgress();
    }
    private void RefreshProgress()
    {
        for (int i = 0; buttons != null && i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].gameObject.SetActive(i < instance.maxLevel);

        int count = instance.Rebirth;
        if (rebirthLabel != null)
        {
            rebirthLabel.text = $"第 {count} 轮回";
            rebirthLabel.gameObject.SetActive(rebirthTime == null && count > 0);
        }
        if (rebirthTime == null) return;

        SetRebirthDigit(time1, count > 0 && count < 10, count % 10);
        SetRebirthDigit(time2_1, count >= 10, count / 10);
        SetRebirthDigit(time2_2, count >= 10, count % 10);
        rebirthTime.SetActive(count > 0);
    }
    private void SetRebirthDigit(Image image, bool visible, int digit)
    {
        if (image == null) return;
        if (visible) image.sprite = rebirthDigits[digit];
        image.gameObject.SetActive(visible);
    }
    private void OnDestroy()
    {
        if (instance != null) instance.DataChanged -= RefreshProgress;
    }
    private void Update()
    {
        if (isSaving&&Time.time-beginSave>=saveKeep)
        {
            float alpha = 1f - (Time.time - beginSave-saveKeep) / saveDuration;
            Color c = saveSucess.color;
            if (alpha <=0f)
            {
                isSaving = false;
                c.a = beginAlpha;
                saveSucess.gameObject.SetActive(false);
                saveSucess.color = c;
                return;
            }
            c.a = alpha;
            saveSucess.color = c;
        }
    }
    public void Skills() => skills.SetActive(true);
    public void HandBook()=>handBook.SetActive(true);
    public void BackToMap()=>handBook.SetActive(false);
    public void Back()//返回主菜单按键，不保存
    {
        instance.DiscardUnsavedProgress();//与设置中的返回主菜单一致，放弃未手动保存的本次修改
        SceneManager.LoadScene("MenuScene");
    }
    public void Save()//保存游戏按键
    {
        if (!SaveSystem.TrySave()) return;//只有真正写盘成功才显示保存成功，未选档直跑不误报
        saveSucess.gameObject.SetActive(true);
        isSaving = true;
        beginSave = Time.time;
    }
    public void EnterLevel(int level)//进入游戏的按键
    {
        if (instance == null || level < 1 || level > instance.maxLevel) return;
        string sceneName = "Level" + level.ToString() + "Scene";
        if (!Application.CanStreamedLevelBeLoaded(sceneName)) return;
        instance.battle.curLevel = level;
        instance.priScene = "MapScene";
        LevelLoader.Load(sceneName);//先进加载界面
    }
}

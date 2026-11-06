using System;
using UnityEngine;
using UnityEngine.UI;

//地图上的图鉴界面。三级页面：首页（角色/敌人两个方框）→ 角色列表（四人待机）→ 单个角色介绍。
//背景板和右上角退出键始终保留；退出在任何页面都直接回到地图，返回键回到上一页。
public sealed class HandbookUI : MonoBehaviour
{
    [Serializable]
    public sealed class Entry
    {
        public string name;
        public Sprite page;//介绍页整图（左侧文字已画在图上）
        public Sprite[] idle = Array.Empty<Sprite>();//右侧展示台上循环的待机帧
        [Min(0.1f)] public float fps = 10f;
        public HandbookFigure figure;//角色列表里对应的人物
    }

    [Header("页面")]
    [SerializeField] private GameObject homePage;
    [SerializeField] private GameObject characterPage;
    [SerializeField] private GameObject detailPage;
    [Header("按钮")]
    [SerializeField] private Button characterButton;
    [SerializeField] private Button enemyButton;//敌人图鉴尚未制作，暂不响应
    [SerializeField] private Button backButton;
    [SerializeField] private Button exitButton;
    [Header("介绍页")]
    [SerializeField] private Image detailImage;
    [SerializeField] private UISpriteLoop detailFigure;
    [SerializeField] private Entry[] characters = Array.Empty<Entry>();//悟空、八戒、沙僧、唐僧

    //characters 数组固定按悟空、八戒、沙僧、唐僧排列，与列表里从左到右、从上到下的位置一致
    private static readonly PlayerId[] CharacterIds = { PlayerId.WuKong, PlayerId.BaJie, PlayerId.Sha, PlayerId.Tang };

    private enum Page { Home, Characters, Detail }
    private Page page;
    private bool wired;

    private void Awake() => Wire();

    private void OnEnable() => Show(Page.Home);//每次从地图打开都从首页开始

    private void Wire()
    {
        if (wired) return;
        wired = true;
        characterButton.onClick.AddListener(() => Show(Page.Characters));
        backButton.onClick.AddListener(Back);
        exitButton.onClick.AddListener(Exit);
        for (int i = 0; i < characters.Length; i++)
        {
            int index = i;
            if (characters[i].figure != null) characters[i].figure.Clicked += () => ShowCharacter(index);
        }
    }

    public void Back()
    {
        if (page == Page.Detail) Show(Page.Characters);
        else Show(Page.Home);
    }

    public void Exit() => gameObject.SetActive(false);//与地图 Map.BackToMap 相同：关闭图鉴回到地图

    public void ShowCharacter(int index)
    {
        if (index < 0 || index >= characters.Length || !IsUnlocked(index)) return;
        Entry entry = characters[index];
        detailImage.sprite = entry.page;
        Show(Page.Detail);
        detailFigure.SetFrames(entry.idle, entry.fps);
    }

    private void Show(Page next)
    {
        page = next;
        homePage.SetActive(next == Page.Home);
        characterPage.SetActive(next == Page.Characters);
        detailPage.SetActive(next == Page.Detail);
        backButton.gameObject.SetActive(next != Page.Home);//首页没有上一页
        if (next == Page.Characters) RefreshUnlocked();
    }

    private void RefreshUnlocked()//未解锁的角色在列表里不显示，也就无法点击进入介绍页
    {
        for (int i = 0; i < characters.Length; i++)
            if (characters[i].figure != null) characters[i].figure.gameObject.SetActive(IsUnlocked(i));
    }

    private static bool IsUnlocked(int index)//读取当前存档；悟空始终可见
    {
        if (index < 0 || index >= CharacterIds.Length) return false;
        if (CharacterIds[index] == PlayerId.WuKong) return true;
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        return data != null && data.IsPlayerUnlocked(CharacterIds[index]);
    }
}

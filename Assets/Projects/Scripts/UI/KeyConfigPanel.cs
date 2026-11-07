using System;
using UnityEngine;
using UnityEngine.UI;

//技能界面里的按键配置页：左边 U、I、O 三个技能键，右边 1～4 四个换人键。
//点技能键弹出三选一的技能小面板，点换人键弹出已解锁角色的小面板，选中后立即改绑并关闭小面板。
public sealed class KeyConfigPanel : MonoBehaviour
{
    [Serializable]
    public sealed class Slot { public Button button; public Image icon; }

    [SerializeField] private Slot[] skillKeys = new Slot[KeyBindings.SkillKeys];//U、I、O
    [SerializeField] private Slot[] switchKeys = new Slot[KeyBindings.SwitchKeys];//1、2、3、4
    [Header("选择技能的小面板")]
    [SerializeField] private GameObject skillPopup;
    [SerializeField] private Slot[] skillChoices = new Slot[3];
    [SerializeField] private Button skillPopupClose;
    [Header("选择角色的小面板")]
    [SerializeField] private GameObject characterPopup;
    [SerializeField] private Slot[] characterChoices = new Slot[4];
    [SerializeField] private Button characterPopupClose;

    private SkillManager manager;
    private PlayerId character;
    private int editingKey = -1;
    private readonly PlayerId[] choiceIds = new PlayerId[4];
    private bool wired;

    public void Show(SkillManager owner, PlayerId id)
    {
        manager = owner;
        character = id;
        Wire();
        ClosePopups();
        Refresh();
    }

    private void Wire()
    {
        if (wired) return;
        wired = true;
        for (int i = 0; i < skillKeys.Length; i++) { int key = i; skillKeys[i]?.button?.onClick.AddListener(() => OpenSkillPopup(key)); }
        for (int i = 0; i < switchKeys.Length; i++) { int key = i; switchKeys[i]?.button?.onClick.AddListener(() => OpenCharacterPopup(key)); }
        for (int i = 0; i < skillChoices.Length; i++) { int skill = i + 1; skillChoices[i]?.button?.onClick.AddListener(() => ChooseSkill(skill)); }
        for (int i = 0; i < characterChoices.Length; i++) { int index = i; characterChoices[i]?.button?.onClick.AddListener(() => ChooseCharacter(index)); }
        skillPopupClose?.onClick.AddListener(ClosePopups);
        characterPopupClose?.onClick.AddListener(ClosePopups);
    }

    private void OnEnable() => KeyBindings.Changed += Refresh;
    private void OnDisable()
    {
        KeyBindings.Changed -= Refresh;
        ClosePopups();
    }

    private void Refresh()
    {
        if (manager == null) return;
        for (int i = 0; i < skillKeys.Length; i++)
            SetIcon(skillKeys[i], manager.SkillIcon(character, KeyBindings.SkillFor(character, i + 1)));
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        for (int i = 0; i < switchKeys.Length; i++)
        {
            PlayerId target = KeyBindings.SwitchTarget(i);
            SetIcon(switchKeys[i], data.IsPlayerUnlocked(target) ? manager.Portrait(target) : null);//未解锁的角色不显示头像
        }
    }

    private void OpenSkillPopup(int key)
    {
        editingKey = key;
        if (characterPopup != null) characterPopup.SetActive(false);
        for (int i = 0; i < skillChoices.Length; i++) SetIcon(skillChoices[i], manager != null ? manager.SkillIcon(character, i + 1) : null);
        if (skillPopup != null) skillPopup.SetActive(true);
    }

    private void OpenCharacterPopup(int key)//只列出已解锁的角色，从左往右依次放
    {
        editingKey = key;
        if (skillPopup != null) skillPopup.SetActive(false);
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        int count = 0;
        foreach (PlayerId id in Enum.GetValues(typeof(PlayerId)))
            if (data.IsPlayerUnlocked(id) && count < characterChoices.Length) choiceIds[count++] = id;
        for (int i = 0; i < characterChoices.Length; i++)
        {
            Slot slot = characterChoices[i];
            if (slot == null) continue;
            bool used = i < count;
            SetIcon(slot, used && manager != null ? manager.Portrait(choiceIds[i]) : null);
            if (slot.button != null) slot.button.interactable = used;
        }
        if (characterPopup != null) characterPopup.SetActive(true);
    }

    private void ChooseSkill(int skill)
    {
        if (editingKey >= 0) KeyBindings.SetSkill(character, editingKey + 1, skill);
        ClosePopups();
    }

    private void ChooseCharacter(int index)
    {
        if (editingKey >= 0 && characterChoices[index]?.button != null && characterChoices[index].button.interactable)
            KeyBindings.SetSwitch(editingKey, choiceIds[index]);
        ClosePopups();
    }

    private void ClosePopups()
    {
        editingKey = -1;
        if (skillPopup != null) skillPopup.SetActive(false);
        if (characterPopup != null) characterPopup.SetActive(false);
    }

    private static void SetIcon(Slot slot, Sprite sprite)
    {
        if (slot?.icon == null) return;
        slot.icon.sprite = sprite;
        slot.icon.enabled = sprite != null;
    }
}

using UnityEngine;

public class PlayerButtonManager : MonoBehaviour
{
    [SerializeField] private GameObject bag, settings, skills;
    public void OnBag()=>bag.SetActive(true);
    public void OnSettings() => settings.SetActive(true);
    public void OnSkills()=>skills.SetActive(true);
}

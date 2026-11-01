using UnityEngine;

//只有表现；耐久、朝向减伤和反弹只在Boss的Health命中管线处理，避免双重受击。
public sealed class NorthKingShield : MonoBehaviour
{
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteData shield;
    private NorthCombat owner;
    public void Show(NorthCombat boss){owner=boss;gameObject.SetActive(true);Follow();spriteManager.ChangeState(shield);}
    private void LateUpdate()
    {
        if(owner==null || !owner.isActiveAndEnabled || !owner.IsGuarding || !BattleRun.IsRunning){gameObject.SetActive(false);return;}
        Follow();if(owner.GuardTimerPaused)spriteManager.Pause();else spriteManager.Resume();
    }
    private void Follow(){transform.position=owner.transform.position+new Vector3(owner.GuardFaceRight?1f:-1f,0.15f);}
    private void OnDisable(){spriteManager.Pause();owner=null;}
}

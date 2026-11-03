using UnityEngine;
using UnityEngine.UI;

//所有关卡共用的Boss血条；特殊状态由对应关卡的子类订阅，不依赖任何具体Boss。
[DisallowMultipleComponent]
public class BossUI : MonoBehaviour
{
    [SerializeField] private Image bloodFull;//现有血条的填充图片，只更新fillAmount
    protected Health bossHealth;//关卡创建的实际Boss实例，不把场景引用保存在预制体中
    protected bool frozen;//死亡后保留最后一次显示，停用Boss也不重置槽

    public virtual void PrepareForBattle()//新一轮关卡先隐藏整个面板，血条恢复满值
    {
        UnbindBoss();
        frozen = false;
        if (bloodFull != null) bloodFull.fillAmount = 1f;
        SetVisible(false);
    }

    public virtual void BindBoss(Health health)//允许在面板尚未启用时订阅，出生时再显示
    {
        UnbindBoss();
        bossHealth = health;
        frozen = false;
        if (bossHealth == null) return;
        bossHealth.HealthChanged += OnHealthChanged;
        bossHealth.Died += OnBossDied;
        OnHealthChanged(bossHealth.CurBlood, bossHealth.BeginBlood);
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);//只开关BossUI，不影响Canvas中的其他界面

    private void OnHealthChanged(float current, float maximum)//致死伤害先显示零血，再把所有槽定格
    {
        if (frozen) return;
        if (bloodFull != null) bloodFull.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (bossHealth != null && bossHealth.Dead) frozen = true;
    }

    private void OnBossDied(DamageInfo damage)//兼容死亡事件前没有发送血量通知的Boss
    {
        if (frozen) return;
        if (bossHealth != null) OnHealthChanged(bossHealth.CurBlood, bossHealth.BeginBlood);
        frozen = true;
    }

    protected virtual void UnbindBoss()//隐藏界面不退订，只有换Boss或销毁时解除引用
    {
        if (bossHealth != null)
        {
            bossHealth.HealthChanged -= OnHealthChanged;
            bossHealth.Died -= OnBossDied;
        }
        bossHealth = null;
    }

    protected virtual void OnDestroy() => UnbindBoss();//不在OnDisable中清空，死亡和菜单开关都保留最终显示
}

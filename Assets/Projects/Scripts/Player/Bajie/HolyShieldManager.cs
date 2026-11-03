using UnityEngine;

public class HolyShieldManager : MonoBehaviour
{
    [SerializeField] private SpriteData shieldLight, shieldMiss;
    [SerializeField] private float lightDuration;
    [SerializeField] private SpriteManager SM;
    [SerializeField, Min(0f)] private float healPerSecond = 1f;//圣盾持续期间每秒恢复的生命值
    private Health health;//圣盾只影响所属人物，不直接改写其他来源的霸体
    private float lastHeal = -1f;//已经结算回血的时间，保证刷新圣盾不会重复结算
    private SpriteRenderer shieldRenderer;//独立的头顶护盾渲染器
    private float lastShield = -1f;
    private bool ShieldLight = false, ShieldMiss = false;
    private void OnEnable()
    {
        InitializeVisual();
        StopShieldBuff();
        SetVisualActive(false);
        ShieldLight = false;
        ShieldMiss = false;
        lastShield = -1f;
        lastHeal = -1f;
    }
    public void OpenShield()//按下技能一
    {
        InitializeVisual();
        if (health == null || health.Dead) return;
        if (SM == null || shieldLight == null || shieldLight.sprites == null || shieldLight.sprites.Length == 0)
            return;

        if (ShieldLight) UpdateHealing();//先结清刷新前尚未恢复的生命，再开始新的持续时间
        UpdateSortingOrder();
        SetVisualActive(true);
        SM.ChangeState(shieldLight);
        lastShield = Time.time;
        lastHeal = lastShield;
        health.SetSuperArmor(this, true);
        ShieldLight = true;
        ShieldMiss = false;//重复释放时重新计时，取消上一次消失阶段
    }
    private void FixedUpdate()
    {
        if (health != null && health.Dead)
        {
            CloseShield();
            return;
        }
        if (ShieldLight) UpdateHealing();
        if (ShieldLight && Time.time - lastShield >= Mathf.Max(0f, lightDuration))
        {
            StopShieldBuff();
            ShieldLight = false;
            lastShield = -1f;
            if (SM == null || shieldMiss == null || shieldMiss.sprites == null || shieldMiss.sprites.Length == 0)
            {
                SetVisualActive(false);
                return;
            }
            ShieldMiss = true;
            SM.ChangeState(new SpriteData
            {
                sprites = shieldMiss.sprites,
                fps = shieldMiss.fps,
                loop = false
            });
        }
        else if (ShieldMiss && (SM == null || SM.IsFinished))
        {
            ShieldMiss = false;
            SetVisualActive(false);
        }
    }

    private void InitializeVisual()//包括尚未激活的子Visual，避免首次施放时找不到动画组件
    {
        if (health == null) health = GetComponentInParent<Health>(true);
        if (SM == null) SM = GetComponentInChildren<SpriteManager>(true);
        if (SM != null && shieldRenderer == null)
            shieldRenderer = SM.GetComponent<SpriteRenderer>();
    }

    private void UpdateHealing()//连续按秒恢复，结算时间不超出圣盾持续阶段
    {
        if (health == null || health.Dead || lastHeal < 0f) return;
        float healUntil = Mathf.Min(Time.time, lastShield + Mathf.Max(0f, lightDuration));
        float elapsed = Mathf.Max(0f, healUntil - lastHeal);
        if (elapsed > 0f) health.Heal(healPerSecond * elapsed);
        lastHeal = Mathf.Max(lastHeal, healUntil);
    }

    private void StopShieldBuff()//只撤销圣盾自己的霸体来源，保留反甲等其他效果
    {
        if (health != null) health.SetSuperArmor(this, false);
        lastHeal = -1f;
    }

    private void CloseShield()//死亡、换人或外部停用时立即停止回血、霸体和头顶特效
    {
        StopShieldBuff();
        ShieldLight = ShieldMiss = false;
        lastShield = -1f;
        SetVisualActive(false);
    }

    private void OnDisable() => CloseShield();//人物换出时不把圣盾效果留给下次启用

    private void SetVisualActive(bool active)//只隐藏显示组件，不能关闭承载管理器的对象
    {
        if (SM == null) return;
        if (SM.gameObject != gameObject) SM.gameObject.SetActive(active);
        if (shieldRenderer != null) shieldRenderer.enabled = active;
    }

    private void UpdateSortingOrder()//关卡可能提高人物显示顺序，护盾始终位于人物上方
    {
        PlayerController player = GetComponentInParent<PlayerController>(true);
        if (player == null || shieldRenderer == null) return;
        Transform bodyVisual = player.transform.Find("Visual");
        SpriteRenderer bodyRenderer = bodyVisual == null ? null : bodyVisual.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) return;
        shieldRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        shieldRenderer.sortingOrder = bodyRenderer.sortingOrder + 2;
    }
}

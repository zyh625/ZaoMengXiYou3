using UnityEngine;
using UnityEngine.UI;

//小怪共用的世界空间血条，只管理本体Canvas/blood，不改变人物、Canvas布局或其他状态UI。
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Image blood;//现有Canvas/blood的填充图片，允许预制体显式绑定或按固定层级自动查找。
    [SerializeField, Min(0f)] private float seeDuration = 3.5f;//沿用第一关鸟怪：最后一次实际受伤后完整显示的时长。
    [SerializeField, Min(0f)] private float fadeDuration = 1.5f;//完整显示结束后，从不透明淡出至隐藏所需的时长。
    private Health health;//只监听自己本体的真实生命值，霸体、中毒和反伤都走同一入口。
    private float showUntil;//再次受到伤害时刷新截止时间，淡出中的血条也立即恢复不透明。
    private bool visible;//隐藏时不再计算淡出，也不会因为回血而自行出现。

    public static void EnsureFor(EnemyController enemy)//以后新增小怪只需保留Canvas/blood层级，Boss继续使用自己的顶部血条。
    {
        if (enemy == null || enemy is BossController || enemy.GetComponent<EnemyHealthBar>() != null) return;
        Transform bar = enemy.transform.Find("Canvas/blood");
        if (bar != null && bar.GetComponent<Image>() != null) enemy.gameObject.AddComponent<EnemyHealthBar>();
    }

    private void Awake() => ResolveReferences();

    private void ResolveReferences()//兼容运行时补挂，以及对象池首次启用时各组件Awake/OnEnable的先后顺序。
    {
        if (health == null) health = GetComponent<Health>();
        if (blood == null)
        {
            Transform bar = transform.Find("Canvas/blood");
            if (bar != null) blood = bar.GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        Hide();
        if (health == null) return;
        health.HealthChanged -= OnHealthChanged;
        health.DamageReceived -= OnDamageReceived;
        health.HealthChanged += OnHealthChanged;
        health.DamageReceived += OnDamageReceived;
        OnHealthChanged(health.CurBlood, health.BeginBlood);//若Health稍后才初始化，会由它的HealthChanged再次同步满血。
    }

    private void OnHealthChanged(float current, float maximum)//回血、上限变化与致死伤害都更新长度，只有真实受伤才唤起显示。
    {
        if (blood != null) blood.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    }

    private void OnDamageReceived(DamageInfo damage, float actualDamage)//无敌、格挡或零伤害不会发出有效扣血通知。
    {
        if (actualDamage <= 0f || blood == null) return;
        OnHealthChanged(health.CurBlood, health.BeginBlood);
        showUntil = Time.time + Mathf.Max(0f, seeDuration);
        visible = true;
        SetAlpha(1f);
        blood.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!visible || blood == null || Time.time < showUntil) return;
        float alpha = fadeDuration > 0f ? Mathf.Clamp01(1f - (Time.time - showUntil) / fadeDuration) : 0f;
        SetAlpha(alpha);
        if (alpha <= 0f) Hide();
    }

    private void SetAlpha(float alpha)//只改变这一张血条图片的透明度，保留原来的颜色和其他Canvas内容。
    {
        if (blood == null) return;
        Color color = blood.color;
        color.a = alpha;
        blood.color = color;
    }

    private void Hide()//对象池回收时清空计时，下一次出生保持隐藏，不继承上一条生命的淡出状态。
    {
        visible = false;
        showUntil = 0f;
        if (blood == null) return;
        SetAlpha(1f);
        blood.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
            health.DamageReceived -= OnDamageReceived;
        }
        Hide();
    }
}

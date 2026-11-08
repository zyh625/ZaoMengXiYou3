using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//屏幕右侧的输出数字：玩家每造成一次伤害，就在同一位置弹出实际扣掉的血量，向上飘并逐渐变淡消失。
//普通伤害用橙色数字，暴击用深红数字并且更大；新数字总是画在旧数字上面。挂在 BattleUI 的 Canvas/DamageNumbers 上。
public sealed class DamageNumbers : MonoBehaviour
{
    [SerializeField] private Sprite[] normalDigits = new Sprite[10];//下标即数字
    [SerializeField] private Sprite[] criticalDigits = new Sprite[10];
    [SerializeField] private float normalHeight = 26f, criticalHeight = 36f;//字高（画布单位）
    [SerializeField] private float rise = 70f;//整段时间上飘的距离
    [SerializeField] private float lifetime = 0.9f;
    [SerializeField, Range(0f, 1f)] private float holdRatio = 0.35f;//前这部分时间保持不透明，之后渐隐
    [SerializeField] private float spacing = -0.04f;//字间距，按字高比例；负数让描边略微相叠更紧凑

    private sealed class Popup
    {
        public RectTransform rect;
        public CanvasGroup group;
        public readonly List<Image> glyphs = new();
        public float age;
        public float pop;//刚出现时的放大倍数，暴击更明显
    }

    private readonly List<Popup> active = new(), idle = new();

    private void OnEnable() => Health.AnyDamageReceived += OnDamage;
    private void OnDisable()
    {
        Health.AnyDamageReceived -= OnDamage;
        foreach (Popup p in active) { p.rect.gameObject.SetActive(false); idle.Add(p); }
        active.Clear();
    }

    private void OnDamage(Health target, DamageInfo damage, float amount)
    {
        if (target == null || damage.Source == null || target.GetComponent<PlayerResources>() != null) return;//只显示玩家打出的伤害
        if (damage.Source.GetComponentInParent<PlayerResources>(true) == null) return;//换下场的角色留下的毒素也算
        float value = Mathf.Max(amount, target.TrainingReportedDamage);//训练场木桩锁血时仍显示完整伤害
        if (value <= 0f) return;
        Show(Mathf.Max(1, Mathf.RoundToInt(value)), damage.IsCritical);
    }

    private void Show(int value, bool critical)
    {
        Popup p = idle.Count > 0 ? idle[idle.Count - 1] : Create();
        if (idle.Count > 0) idle.RemoveAt(idle.Count - 1);
        Sprite[] digits = critical ? criticalDigits : normalDigits;
        float height = critical ? criticalHeight : normalHeight;
        string text = value.ToString();
        var widths = new float[text.Length];
        float total = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            Sprite s = digits[text[i] - '0'];
            widths[i] = s != null ? height * s.rect.width / Mathf.Max(1f, s.rect.height) : height * 0.6f;
            total += widths[i] + (i > 0 ? height * spacing : 0f);
        }
        float x = -total;//右对齐：位数再多也不会超出屏幕右边
        for (int i = 0; i < text.Length; i++)
        {
            Image g = Glyph(p, i);
            g.sprite = digits[text[i] - '0'];
            g.gameObject.SetActive(true);
            var r = (RectTransform)g.transform;
            r.sizeDelta = new Vector2(widths[i], height);
            r.anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0f);
            x += widths[i] + height * spacing;
        }
        for (int i = text.Length; i < p.glyphs.Count; i++) p.glyphs[i].gameObject.SetActive(false);
        p.age = 0f;
        p.pop = critical ? 1.45f : 1.15f;
        p.rect.anchoredPosition = Vector2.zero;
        p.rect.localScale = Vector3.one * p.pop;
        p.group.alpha = 1f;
        p.rect.gameObject.SetActive(true);
        p.rect.SetAsLastSibling();//新数字画在之前的数字上面
        active.Add(p);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;//打开菜单暂停时也照常淡出，不会停在屏幕上
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup p = active[i];
            p.age += dt;
            float t = p.age / lifetime;
            if (t >= 1f)
            {
                p.rect.gameObject.SetActive(false);
                active.RemoveAt(i);
                idle.Add(p);
                continue;
            }
            float ease = 1f - (1f - t) * (1f - t);//先快后慢地上飘
            p.rect.anchoredPosition = new Vector2(0f, rise * ease);
            p.rect.localScale = Vector3.one * Mathf.Lerp(p.pop, 1f, Mathf.Clamp01(t / 0.15f));//弹出后迅速回到正常大小
            p.group.alpha = t <= holdRatio ? 1f : 1f - (t - holdRatio) / (1f - holdRatio);
        }
    }

    private Popup Create()
    {
        var go = new GameObject("Damage", typeof(RectTransform), typeof(CanvasGroup));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.sizeDelta = Vector2.zero;
        var group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        return new Popup { rect = rect, group = group };
    }

    private static Image Glyph(Popup p, int index)
    {
        while (p.glyphs.Count <= index)
        {
            var go = new GameObject("Digit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var r = (RectTransform)go.transform;
            r.SetParent(p.rect, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            p.glyphs.Add(img);
        }
        return p.glyphs[index];
    }
}

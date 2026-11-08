using UnityEngine;
using UnityEngine.UI;

//一波小怪打完后，在屏幕右侧显示向右的指引，外圈黄光一闪一闪，直到玩家走进下一波的范围。
//最后一波打完会直接进入Boss战或开出口，不显示指引。挂在 BattleUI 的 Canvas/WaveGuide 上。
public sealed class WaveGuide : MonoBehaviour
{
    [SerializeField] private GameObject content;//显示时打开，里面是黄光和箭头
    [SerializeField] private Image arrow, glow;
    [SerializeField, Min(0.1f)] private float blinkPerSecond = 1.6f;//黄光每秒闪几次
    [SerializeField] private float nudge = 8f;//箭头左右轻微晃动的幅度，提示向右走

    //guide_glow.png 是在 guide.png 四周各加 70 像素做的发光，按原图比例放大才能对齐
    private static readonly Vector2 GlowScale = new((2172f + 140f) / 2172f, (724f + 140f) / 724f);

    private LevelManager level;
    private Vector2 arrowPos;
    private float shownAt, nextSearch;

    private void Awake()
    {
        if (arrow != null)
        {
            var rect = (RectTransform)arrow.transform;
            arrowPos = rect.anchoredPosition;
            if (glow != null) ((RectTransform)glow.transform).sizeDelta = Vector2.Scale(rect.sizeDelta, GlowScale);
        }
        if (content != null) content.SetActive(false);
    }

    private void Update()
    {
        if (level == null && Time.unscaledTime >= nextSearch)//没有波次的场景（训练场等）每秒找一次就够了
        {
            level = FindAnyObjectByType<LevelManager>();
            nextSearch = Time.unscaledTime + 1f;
        }
        bool show = level != null && level.AwaitingNextWave;
        if (content == null) return;
        if (content.activeSelf != show)
        {
            content.SetActive(show);
            shownAt = Time.unscaledTime;
        }
        if (!show) return;
        float t = Time.unscaledTime - shownAt;
        float pulse = 0.5f - 0.5f * Mathf.Cos(t * blinkPerSecond * 2f * Mathf.PI);//从暗开始，0～1
        if (glow != null) glow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, pulse));
        if (arrow != null)
            ((RectTransform)arrow.transform).anchoredPosition = arrowPos + new Vector2(nudge * Mathf.Sin(t * blinkPerSecond * 2f * Mathf.PI), 0f);
        if (glow != null) glow.rectTransform.anchoredPosition = arrow != null ? ((RectTransform)arrow.transform).anchoredPosition : arrowPos;
    }
}

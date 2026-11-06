using UnityEngine;
using UnityEngine.UI;

//在UI里循环播放帧动画（图鉴里的角色/敌人待机）。
//按每帧真实像素大小显示，脚底对齐父物体底边、水平居中，紧切片的帧不会被拉伸或上下抖动；
//整体缩放按所有帧中最大的宽高放进父物体的矩形里。
[RequireComponent(typeof(Image))]
public sealed class UISpriteLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
    [SerializeField, Min(0.1f)] private float fps = 10f;
    [SerializeField] private bool flipX;
    private Image image;
    private RectTransform rect;
    private float scale = 1f, timer;
    private int frame;

    public void SetFrames(Sprite[] sprites, float framesPerSecond)
    {
        frames = sprites ?? System.Array.Empty<Sprite>();
        fps = Mathf.Max(0.1f, framesPerSecond);
        frame = 0;
        timer = 0f;
        Refit();
    }

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        image.preserveAspect = false;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
    }

    private void OnEnable()
    {
        frame = 0;
        timer = 0f;
        Refit();
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2) return;
        timer += Time.unscaledDeltaTime;//地图可能处于暂停状态，界面动画不受时间缩放影响
        float step = 1f / fps;
        if (timer < step) return;
        int advance = Mathf.FloorToInt(timer / step);
        timer -= advance * step;
        frame = (frame + advance) % frames.Length;
        Show(frames[frame]);
    }

    private void Refit()//以最大帧适配父矩形，之后每帧只换图和尺寸
    {
        if (image == null || frames == null || frames.Length == 0) return;
        Vector2 largest = Vector2.zero;
        foreach (Sprite sprite in frames)
            if (sprite != null) largest = Vector2.Max(largest, sprite.rect.size);
        RectTransform box = transform.parent as RectTransform;
        if (box != null && largest.x > 0f && largest.y > 0f)
            scale = Mathf.Min(box.rect.width / largest.x, box.rect.height / largest.y);
        Show(frames[Mathf.Clamp(frame, 0, frames.Length - 1)]);
    }

    private void Show(Sprite sprite)
    {
        if (sprite == null) return;
        image.sprite = sprite;
        rect.sizeDelta = sprite.rect.size * scale;
        rect.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);
    }
}

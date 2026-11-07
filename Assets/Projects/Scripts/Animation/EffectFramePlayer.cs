using UnityEngine;

//特效逐帧播放：先从 from 播到 to；指定循环段时到末尾后在 loopFrom..loopTo 之间循环（用于“出现→保持脉冲”这类特效），否则停在最后一帧。
//帧图、帧率和显示大小都在预制体上设置。
public sealed class EffectFramePlayer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer target;
    [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
    [SerializeField, Min(1f)] private float fps = 16f;
    private int from, to, loopFrom = -1, loopTo = -1, current = -1;
    private float started, rate;
    private bool playing;

    public bool HasFrames => target != null && frames != null && frames.Length > 0;
    public SpriteRenderer Target => target;
    public Sprite FirstFrame => HasFrames ? frames[0] : null;

    private void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
    }

    //speed<=0 时使用预制体上的帧率
    public void Play(int first, int last, int loopFirst = -1, int loopLast = -1, float speed = 0f)
    {
        if (!HasFrames) return;
        int max = frames.Length - 1;
        from = Mathf.Clamp(first, 0, max);
        to = Mathf.Clamp(last, from, max);
        bool loops = loopFirst >= 0 && loopLast >= loopFirst;
        loopFrom = loops ? Mathf.Clamp(loopFirst, 0, max) : -1;
        loopTo = loops ? Mathf.Clamp(loopLast, loopFrom, max) : -1;
        rate = speed > 0f ? speed : fps;
        started = Time.time;
        playing = true;
        current = -1;
        target.enabled = true;
        Show(from);
    }

    public void Stop() => playing = false;

    private void Update()
    {
        if (!playing) return;
        int step = Mathf.FloorToInt((Time.time - started) * rate);
        int length = to - from + 1;
        int frame;
        if (step < length) frame = from + step;
        else if (loopFrom >= 0) frame = loopFrom + (step - length) % (loopTo - loopFrom + 1);
        else { frame = to; playing = false; }
        Show(frame);
    }

    private void Show(int frame)
    {
        if (frame == current) return;
        current = frame;
        target.sprite = frames[frame];
    }
}

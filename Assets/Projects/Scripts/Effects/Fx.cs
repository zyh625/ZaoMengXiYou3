using System.Collections.Generic;
using UnityEngine;

//一次特效播放的参数。用 Fx.Make 建一个默认值，再按需修改字段。
public struct FxSpec
{
    public string clip;          //FxLibrary 里的名字；为空时用 sprite
    public Sprite sprite;        //单张图（例如代码生成的光晕）
    public Vector3 position;     //世界坐标；有 follow 时作为相对 follow 的偏移
    public Transform follow;     //跟随的物体（角色、Boss）；它被销毁时特效也结束
    public float size;           //第一帧较长一边的世界尺寸
    public Color color;
    public bool loop;            //循环播放帧，直到 life 用完或被 Stop
    public float life;           //持续时间；<=0 时：不循环=播完一遍，循环=一直播放
    public float fps;            //<=0 时用帧库里的帧率
    public float growFrom, growTo; //整段时间内从 growFrom 倍缩放到 growTo 倍
    public float fadeIn, fadeOut;  //开头淡入、结尾淡出的秒数
    public float squashY;        //竖直压扁，地面法阵用 0.35 左右
    public float rotation, spin; //初始角度和每秒旋转角度
    public Vector2 drift;        //每秒移动（例如上升的光点）
    public int sortingLayerID, order;
    public bool flipX;
}

//通用特效播放：对象池复用，帧动画 + 缩放 + 淡入淡出 + 跟随。使用游戏时间，暂停时一起停住。
public static class Fx
{
    private static readonly List<FxInstance> pool = new();
    private static Transform root;

    public static FxSpec Make(string clip, Vector3 position, float size, Color color) => new()
    {
        clip = clip, position = position, size = size, color = color,
        growFrom = 1f, growTo = 1f, squashY = 1f, fadeOut = 0.12f,
    };

    //让特效与参考图层同层，order 为相对参考的偏移（负数在后面）
    public static void SortLike(ref FxSpec spec, SpriteRenderer reference, int offset)
    {
        spec.sortingLayerID = reference != null ? reference.sortingLayerID : 0;
        spec.order = (reference != null ? reference.sortingOrder : 0) + offset;
    }

    public static FxInstance Spawn(FxSpec spec)
    {
        Sprite[] frames;
        float fps;
        FxLibrary.Clip clip = FxLibrary.Get(spec.clip);
        if (clip != null) { frames = clip.frames; fps = spec.fps > 0f ? spec.fps : clip.fps; }
        else if (spec.sprite != null) { frames = new[] { spec.sprite }; fps = 1f; }
        else return null;
        FxInstance fx = Take();
        fx.Begin(spec, frames, fps);
        return fx;
    }

    private static FxInstance Take()
    {
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null) { pool.RemoveAt(i); continue; }
            if (!pool[i].gameObject.activeSelf) return pool[i];
        }
        if (root == null) root = new GameObject("FxPool").transform;//随场景销毁，下一场景重新建
        var go = new GameObject("Fx", typeof(SpriteRenderer), typeof(FxInstance));
        go.transform.SetParent(root, false);
        go.SetActive(false);
        var fx = go.GetComponent<FxInstance>();
        pool.Add(fx);
        return fx;
    }

    internal static void Return(FxInstance fx)
    {
        if (fx == null) return;
        fx.gameObject.SetActive(false);
        if (root != null) fx.transform.SetParent(root, false);
        else Object.Destroy(fx.gameObject);//原池子随场景销毁了，这个实例也不再复用
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool() { pool.Clear(); root = null; }
}

public sealed class FxInstance : MonoBehaviour
{
    private SpriteRenderer render;
    private FxSpec spec;
    private Sprite[] frames;
    private float fps, started, baseScale, life, stopAt = -1f, stopFade;
    private bool following;

    public bool Playing => gameObject.activeSelf;

    internal void Begin(FxSpec s, Sprite[] f, float rate)
    {
        if (render == null) render = GetComponent<SpriteRenderer>();
        spec = s; frames = f; fps = Mathf.Max(1f, rate);
        started = Time.time; stopAt = -1f;
        life = s.life > 0f ? s.life : s.loop ? float.PositiveInfinity : frames.Length / fps;
        Vector2 dim = frames[0].bounds.size;
        baseScale = s.size > 0f ? s.size / Mathf.Max(0.0001f, Mathf.Max(dim.x, dim.y)) : 1f;
        following = s.follow != null;
        transform.SetParent(following ? s.follow : transform.parent, false);
        render.sortingLayerID = s.sortingLayerID;
        render.sortingOrder = s.order;
        render.flipX = s.flipX;
        render.sprite = frames[0];
        gameObject.SetActive(true);
        Apply(0f);
    }

    public void Stop(float fade = 0.2f)//提前结束（例如护罩被打破、飞行结束），淡出后回收
    {
        if (!gameObject.activeSelf || stopAt >= 0f) return;
        stopAt = Time.time; stopFade = Mathf.Max(0.01f, fade);
    }

    public void SetColor(Color c) => spec.color = c;

    private void LateUpdate()
    {
        if (following && spec.follow == null) { Fx.Return(this); return; }
        float t = Time.time - started;
        if (t >= life || stopAt >= 0f && Time.time - stopAt >= stopFade) { Fx.Return(this); return; }
        Apply(t);
    }

    private static float SafeDivide(float value, float scale) => Mathf.Abs(scale) < 0.0001f ? value : value / scale;//保留正负号：父物体用负缩放翻转时偏移方向不反

    private void Apply(float t)
    {
        int frame = Mathf.FloorToInt(t * fps);
        frame = spec.loop ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1);
        render.sprite = frames[frame];
        float k = float.IsInfinity(life) ? 0f : Mathf.Clamp01(t / life);
        float grow = Mathf.Lerp(spec.growFrom, spec.growTo, 1f - (1f - k) * (1f - k));//先快后慢地放大
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float sx = baseScale * grow / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float sy = baseScale * grow * spec.squashY / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
        transform.localScale = new Vector3(sx, sy, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, spec.rotation + spec.spin * t);
        Vector3 offset = spec.position + (Vector3)(spec.drift * t);
        if (following)
        {
            //偏移按世界单位：抵消父物体缩放与翻转
            transform.localPosition = new Vector3(SafeDivide(offset.x, parentScale.x), SafeDivide(offset.y, parentScale.y), 0f);
        }
        else transform.position = offset;
        float a = 1f;
        if (spec.fadeIn > 0f) a = Mathf.Min(a, t / spec.fadeIn);
        if (!float.IsInfinity(life) && spec.fadeOut > 0f) a = Mathf.Min(a, (life - t) / spec.fadeOut);
        if (stopAt >= 0f) a = Mathf.Min(a, 1f - (Time.time - stopAt) / stopFade);
        Color c = spec.color; c.a *= Mathf.Clamp01(a);
        render.color = c;
    }
}

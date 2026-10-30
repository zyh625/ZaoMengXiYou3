using UnityEngine;
[System.Serializable]
public class SpriteData
{
    public Sprite[] sprites;
    public bool loop;//帧动画是否循环
    [Min(0f)] public float fps;//每秒播放几帧

}
[RequireComponent(typeof(SpriteRenderer))]

public class SpriteManager : MonoBehaviour
{
    private SpriteData current;
    [SerializeField] private SpriteRenderer sr;
    private float curTime = 0f;
    private bool isEnd = false;//当前是否到达最后一帧且不循环播放
    public bool IsFinished => isEnd;
    private int idx;
    public int CurFrame => idx;
    private bool paused = false;
    public void Pause()=>paused= true;
    public void Resume() => paused = false;
    public float spriteHalf => sr.bounds.size.x / 2;
    private SpriteOutlineController outline;

    private void Awake()
    {
        sr = sr == null ? GetComponent<SpriteRenderer>() : sr;
        outline=GetComponent<SpriteOutlineController>();
        if (outline == null && sr.sharedMaterial != null && sr.sharedMaterial.HasProperty("_OutlineTexelSize"))
            outline = sr.gameObject.AddComponent<SpriteOutlineController>();//同材质的角色、敌人和特效统一取得实际纹理尺寸。
    }
    private void OnEnable()
    {
        if (outline != null)
            outline.SetOutline(false);//开始时关闭红色描边
    }

    public void ChangeState(SpriteData newSd)
    {
        if (newSd == null || newSd.sprites == null || newSd.sprites.Length == 0)
            return;
        paused = false;
        current = newSd;
        isEnd = false;
        sr.sprite = current.sprites[0];
        curTime = 0;
        idx = 0;
    }
    public void ChangeSpriteFace(bool right) => sr.flipX = !right;

    private void Update()
    {
        if (current == null || isEnd || paused) return;
        curTime += Time.deltaTime;
        idx = Mathf.FloorToInt(curTime * Mathf.Max(1f, current.fps));
        if (idx >= current.sprites.Length)
        {
            if (current.loop)
                idx %= current.sprites.Length;
            else
            {
                sr.sprite = current.sprites[current.sprites.Length - 1];//统一播放最后一帧
                isEnd = true;
                return;
            }
        }
        sr.sprite= current.sprites[idx];
    }
    public void SetAngry(bool flag)
    {
        if (outline != null)
            outline.SetOutline(flag);
    }
    public void SetRed() => outline.SetOutlineColor(Color.red);
    public void SetYellow() => outline.SetOutlineColor(Color.yellow);
}

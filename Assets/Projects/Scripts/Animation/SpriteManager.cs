using UnityEngine;
[System.Serializable]
public class SpriteData
{
    public Sprite[] sprites;
    public bool loop;//帧动画是否循环
    [Min(1f)] public float fps;//每秒播放几帧

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
    private void Awake()
    {
        sr = sr == null ? GetComponent<SpriteRenderer>() : sr;
    }

    public void ChangeState(SpriteData newSd)
    {
        if (newSd == null || newSd.sprites == null || newSd.sprites.Length == 0)
            return;
        current = newSd;
        isEnd = false;
        sr.sprite = current.sprites[0];
        curTime = 0;
        idx = 0;
    }
    public void ChangeSpriteFace(bool right) => sr.flipX = !right;

    private void Update()
    {
        if (current == null || isEnd) return;
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
}

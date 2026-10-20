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
    [SerializeField] private int[] SkillThreeState;//到第几帧换阶段，整个运动过程类似尖三角
    private float curTime = 0f;
    private bool isEnd = false;//当前是否到达最后一帧且不循环播放
    public bool IsFinished => isEnd;
    private int idx;
    public int CurFrame => idx;
    public int SkillThreeCur = 0;
    private bool skillThree = false;
    [SerializeField]private int RiseOpenIdx, RiseShutIdx;
    public bool RiseOpen => CurFrame >= RiseOpenIdx && CurFrame < RiseShutIdx;
    public bool RiseShut=>CurFrame>=RiseShutIdx;
    [SerializeField] private int[] ComboOpenIdx, ComboShutIdx;
    public bool ComboOpen(int k) => CurFrame >= ComboOpenIdx[k] && CurFrame < ComboShutIdx[k];
    public bool ComboShut(int k) => CurFrame >= ComboShutIdx[k];
    [SerializeField] private int[] AttackOpenIdx;
    public bool AttackOpen(int k) => CurFrame >= AttackOpenIdx[k] && CurFrame < AttackShutIdx[k];
    [SerializeField] private int[] AttackShutIdx;
    public bool AttackShut(int k)=>CurFrame>=AttackShutIdx[k];
    [SerializeField] private int[] SkillOpenIdx;
    public bool SkillOpen(int k) => idx >= SkillOpenIdx[k] && idx < SkillShutIdx[k];
    [SerializeField] private int[] SkillShutIdx;
    public bool SkillShut(int k) => idx >= SkillShutIdx[k];
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
        if (current == null || isEnd || SkillThreeCur == 2) return;
        curTime += Time.deltaTime;
        idx = Mathf.FloorToInt(curTime * Mathf.Max(1f, current.fps));
        if (skillThree && SkillThreeCur == 0 &&
            idx >= SkillThreeState[0] + 1) SkillThreeCur = 1;
        else if (skillThree && SkillThreeCur == 1 &&
            idx >= SkillThreeState[1] + 1) SkillThreeCur = 2;//直到被落地打破
        else if (skillThree && SkillThreeCur == 2 &&
            idx >= SkillThreeState[2] + 1) SkillThreeCur = 3;
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
    public void ChangeSkillThree(bool flag)
    {
        skillThree= flag;
        SkillThreeCur = 0;
    }
}

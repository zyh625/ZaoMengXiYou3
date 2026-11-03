using UnityEngine;

public class ShaCombat : PlayerCombat
{
    [SerializeField, Min(0)] private int skullFrame = 10;//第11帧投出麻痹药剂，动作帧下标从0开始
    [SerializeField, Min(0)] private int boomerangFrame = 11;//第12帧投出回风摄魂
    [SerializeField, Min(0)] private int quicksandFrame = 10;//第11帧生成流沙领域
    private PlayerManager effectManager;//独立效果统一由场景的角色管理器生成和保留
    private bool effectReleased;//每次施法只生成一次，重复检查动作帧不会重复生成
    private ShaMovement movement;//流沙领域输入时及真正施法前都必须校验脚下地面

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<ShaMovement>();
    }

    public override void OnSkill(int k)//只有技能三禁止空中释放，前两招保留空中施法
    {
        if (k == 3 && (movement == null || !movement.IsOnGroundNow())) return;
        base.OnSkill(k);
    }

    public bool CanStartSkill() => Skilled != 3 || movement != null && movement.IsOnGroundNow();//不允许空中请求落地后自动补放领域

    public void SetEffectManager(PlayerManager manager) => effectManager = manager;//由PlayerManager在创建角色时注入

    public void BeginSkill() => effectReleased = false;//实际开始施法后重置，不在按键请求时重置

    public void CheckSkillEffect(int frame, bool faceRight)
    {
        if (effectReleased || effectManager == null) return;
        int effectFrame = Skilled == 1 ? skullFrame : Skilled == 2 ? boomerangFrame : quicksandFrame;
        if (Skilled < 1 || Skilled > 3 || frame < effectFrame) return;
        effectReleased = true;//先消费本次生成机会，缺少目标或地面也不会在后续帧重复尝试
        switch (Skilled)
        {
            case 1: effectManager.SpawnSkull(transform, faceRight); break;
            case 2: effectManager.SpawnBoomerang(transform, faceRight); break;
            case 3: effectManager.SpawnQuicksand(transform); break;
        }
    }

    public override void SkillEnd()
    {
        base.SkillEnd();
        effectReleased = false;//受击、死亡和换人只取消尚未生效的本次施法，已生成的效果继续存在
    }
}

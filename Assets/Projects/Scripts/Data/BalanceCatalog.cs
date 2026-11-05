using UnityEngine;

[CreateAssetMenu(menuName="造梦西游/数值配置总目录",fileName="BalanceCatalog")]
public sealed class BalanceCatalog:ScriptableObject
{
    [Header("四角色（技能等级数据也在各自资产内）")] public BalanceProfile[] players;
    [Header("各类小怪")] public BalanceProfile[] enemies;
    [Header("各关 Boss")] public BalanceProfile[] bosses;
    [Header("投射物与独立特效")] public BalanceProfile[] effects;
    [Header("公共战斗、金币、技能等级与轮回规则")] public BalanceProfile[] common;
    [Header("关卡波次、数量及刷怪间隔")] public BalanceProfile[] levels;
}

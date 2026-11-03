//需要由关卡提供左右活动边界的Boss或敌人组件实现此接口。
public interface IEnemyArenaBounds
{
    void SetArenaBounds(float left, float right);//注入当前关卡场地的世界坐标边界
}

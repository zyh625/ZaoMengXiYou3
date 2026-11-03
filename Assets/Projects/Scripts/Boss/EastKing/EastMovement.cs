using UnityEngine;

public class EastMovement : EnemyMovement
{
    public bool FaceTowardsPlayer => DeltaX > 0f;//只在一轮弹琴起手时读取，发射后的方向由各音符自己决定
}

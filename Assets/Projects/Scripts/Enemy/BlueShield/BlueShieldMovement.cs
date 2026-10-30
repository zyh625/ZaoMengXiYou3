using UnityEngine;

public class BlueShieldMovement : EnemyMovement
{
    private SpriteRenderer visual;//延迟读取，不遮蔽EnemyMovement现有的Awake初始化。
    public bool FaceRight
    {
        get
        {
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>(true);
            return visual == null || !visual.flipX;
        }
    }
}

using UnityEngine;
using zmxy.GamePlay;

public class SouthKingMovement : EnemyMovement
{
    [SerializeField, Min(0f)] private float thrustSpeed = 6f;//前戳有效阶段的水平速度，不改变追击和击退基础值。
    private CameraManager cameraManager;//运行时获取关卡边界，预制体不引用场景相机。
    public void Thrust(bool right)
    {
        Health health = GetComponent<Health>();
        if (body == null || IsExternallyControlled || health != null && health.Status != null && health.Status.BlocksMovement)
        { Stop(); return; }
        float speed = Mathf.Max(0f, thrustSpeed) * (right ? 1f : -1f);
        if (cameraManager == null && Camera.main != null) cameraManager = Camera.main.GetComponent<CameraManager>();
        if (cameraManager != null && cameraManager.CameraHalf > 0f && BodyCollider != null)
        {
            float half = BodyCollider.bounds.extents.x;
            float left = cameraManager.EdgeL - cameraManager.CameraHalf + half;
            float rightEdge = cameraManager.EdgeR + cameraManager.CameraHalf - half;
            if (left <= rightEdge)
            {
                float destination = body.position.x + speed * Time.fixedDeltaTime;
                if (right && destination > rightEdge) speed = Mathf.Max(0f, rightEdge - body.position.x) / Time.fixedDeltaTime;
                else if (!right && destination < left) speed = Mathf.Min(0f, left - body.position.x) / Time.fixedDeltaTime;
            }
        }
        body.linearVelocity = new Vector2(speed, body.linearVelocityY);
    }
}

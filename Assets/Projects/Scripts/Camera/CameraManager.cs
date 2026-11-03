using UnityEngine;
namespace zmxy.GamePlay
{
    [RequireComponent(typeof(Camera))]
    public class CameraManager : MonoBehaviour
    {
        private Camera _camera;
        [SerializeField] private float followSpeed;//镜头跟随角色的速度
        [Header("垂直跟随")]
        [SerializeField] private bool verticalFollow = true;//玩家跳到镜头中心以上时是否向上跟随
        [SerializeField, Min(0f)] private float verticalFollowSpeed = 5f;//向上跟随的速度（单位/秒），越大跟得越紧
        [SerializeField, Min(0f)] private float verticalReturnSpeed = 2.5f;//落地后回到基准高度的速度
        [Tooltip("相对镜头基准高度，玩家超过这个高度才开始竖直跟随；负数会提前触发，例如 -2.5 表示普通起跳就跟")]
        [SerializeField] private float verticalTriggerOffset = 0.35f;
        [SerializeField, Min(0f)] private float maxRise = 4f;//相对基准高度最多上升多少，防止镜头升到天空外
        [SerializeField] private Transform[] cameraEdgeLeft;
        [SerializeField] private Transform[] cameraEdgeRight;
        public float CameraCenter => _camera != null ? _camera.transform.position.x : transform.position.x;//摄像机中心
        public float CameraCenterY => _camera != null ? _camera.transform.position.y : transform.position.y;//摄像机中心的竖直高度
        public float CameraHalf => _camera != null ? _camera.orthographicSize * _camera.aspect : 0f;
        public int WaveCount => cameraEdgeLeft == null ? 0 : cameraEdgeLeft.Length;//供关卡与创建工具核对波次边界数量
        private float edgeL, edgeR;
        public float EdgeL => edgeL;
        public float EdgeR => edgeR;
        private float baseY;//镜头在关卡中的基准高度，垂直跟随结束后回到这里
        private bool configurationValid;//边界配置不完整时停止跟随，避免持续抛出空引用

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            configurationValid = ValidateConfiguration(out string error);
            if (!configurationValid)
            {
                StopForInvalidConfiguration(error);
                return;
            }
            //角色和敌人可能先执行Start，因此在Awake准备好首波边界。
            edgeL = cameraEdgeLeft[0].position.x;
            edgeR = cameraEdgeRight[0].position.x;
            baseY = _camera.transform.position.y;//以场景里摆放的镜头高度作为基准，不在运行时修改
        }

        public bool ValidateConfiguration(out string error)//运行前和创建关卡时使用同一套边界检查
        {
            if (GetComponent<Camera>() == null)
                error = "CameraManager所在物体缺少Camera组件";
            else if (cameraEdgeLeft == null || cameraEdgeRight == null || cameraEdgeLeft.Length == 0 || cameraEdgeRight.Length == 0)
                error = "CameraManager至少需要一组左右相机边界";
            else if (cameraEdgeLeft.Length != cameraEdgeRight.Length)
                error = "CameraManager左右相机边界数量不一致";
            else
            {
                for (int i = 0; i < cameraEdgeLeft.Length; i++)
                {
                    if (cameraEdgeLeft[i] == null || cameraEdgeRight[i] == null)
                    {
                        error = $"CameraManager第{i + 1}波的相机边界未填写";
                        return false;
                    }
                    if (cameraEdgeLeft[i].position.x > cameraEdgeRight[i].position.x)
                    {
                        error = $"CameraManager第{i + 1}波的左边界位于右边界右侧";
                        return false;
                    }
                }
                error = null;
                return true;
            }
            return false;
        }

        public bool CheckNextWave(int wave)
        {
            if (!TryGetWave(wave)) return false;
            bool b = CameraCenter >= cameraEdgeLeft[wave].position.x;
            if (b) SetEdge(wave);
            return b;
        }

        public void UpdateCamera(float deltaX)//只做水平跟随的旧入口，垂直方向保持基准高度
        {
            UpdateCamera(deltaX, baseY);
        }

        public void UpdateCamera(float deltaX, float playerY)//中心水平跟随玩家，并在他跳到镜头中心以上时竖直跟随
        {
            if (!configurationValid || _camera == null) return;
            Vector3 pos = _camera.transform.position;
            float targetX = Mathf.Clamp(deltaX + pos.x, edgeL, edgeR);
            pos.x = Mathf.MoveTowards(pos.x, targetX, followSpeed * Time.deltaTime);
            if (verticalFollow) pos.y = VerticalStep(pos.y, playerY);
            _camera.transform.position = pos;
        }

        private float VerticalStep(float currentY, float playerY)//只在上方跟随，落地后缓慢回到底部基准高度
        {
            float targetY = baseY;
            if (playerY > baseY + verticalTriggerOffset)
                targetY = Mathf.Clamp(playerY, baseY, baseY + Mathf.Max(0f, maxRise));
            //向上用跟随速度、向下用回落速度，分别可调；暂停或帧间隔异常时仍保持平滑。
            float speed = targetY > currentY ? verticalFollowSpeed : verticalReturnSpeed;
            return Mathf.MoveTowards(currentY, targetY, Mathf.Max(0f, speed) * Time.deltaTime);
        }
        public void SetEdge(int wave)
        {
            //负数表示先扩展下一波右边界；避免极小整数取反后溢出。
            if (wave == int.MinValue)
            {
                StopForInvalidConfiguration("CameraManager收到无效的相机边界波次");
                return;
            }
            if (!TryGetWave(wave < 0 ? -wave : wave)) return;
            if (wave < 0)
                edgeR = cameraEdgeRight[-wave].position.x;
            else
                edgeL = cameraEdgeLeft[wave].position.x;
        }

#if UNITY_EDITOR
        public void FocusBattlePractice(int wave, float targetX)//测试直接定位当前波或Boss场地，不经过逐波镜头推进。
        {
            if (!TryGetWave(wave)) return;
            edgeL = cameraEdgeLeft[wave].position.x;
            edgeR = cameraEdgeRight[wave].position.x;
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(targetX, edgeL, edgeR);
            transform.position = position;
        }
#endif

        private bool TryGetWave(int wave)//外部传错波次或运行时丢失边界时安全停止相机
        {
            if (!configurationValid || _camera == null) return false;
            if (wave < 0 || cameraEdgeLeft == null || cameraEdgeRight == null ||
                wave >= cameraEdgeLeft.Length || wave >= cameraEdgeRight.Length ||
                cameraEdgeLeft[wave] == null || cameraEdgeRight[wave] == null)
            {
                StopForInvalidConfiguration($"CameraManager找不到第{wave + 1}波的完整左右边界");
                return false;
            }
            return true;
        }

        private void StopForInvalidConfiguration(string error)//只报当前配置错误，不让后续每帧继续访问无效引用
        {
            Debug.LogError(error, this);
            configurationValid = false;
            enabled = false;
        }
    }
}

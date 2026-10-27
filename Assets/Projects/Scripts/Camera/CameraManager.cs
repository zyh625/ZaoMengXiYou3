using UnityEngine;
namespace zmxy.GamePlay
{
    [RequireComponent(typeof(Camera))]
    public class CameraManager : MonoBehaviour
    {
        private Camera _camera;
        [SerializeField] private float followSpeed;//镜头跟随角色的速度
        [SerializeField] private Transform[] cameraEdgeLeft;
        [SerializeField] private Transform[] cameraEdgeRight;
        public float CameraCenter => _camera != null ? _camera.transform.position.x : transform.position.x;//摄像机中心
        public float CameraHalf => _camera != null ? _camera.orthographicSize * _camera.aspect : 0f;
        public int WaveCount => cameraEdgeLeft == null ? 0 : cameraEdgeLeft.Length;//供关卡与创建工具核对波次边界数量
        private float edgeL, edgeR;
        public float EdgeL => edgeL;
        public float EdgeR => edgeR;
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

        public void UpdateCamera(float deltaX)//中心跟随玩家
        {
            if (!configurationValid || _camera == null) return;
            Vector3 pos = _camera.transform.position;
            float targetX = Mathf.Clamp(deltaX + pos.x, edgeL, edgeR);
            pos.x = Mathf.MoveTowards(pos.x, targetX, followSpeed * Time.deltaTime);
            _camera.transform.position = pos;
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

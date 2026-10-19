using UnityEngine;
namespace zmxy.GamePlay
{
    public class CameraManager : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform[] cameraEdgeLeft;
        [SerializeField] private Transform[] cameraEdgeRight;
        public float CameraCenter => _camera.transform.position.x;//摄像机中心
        public float CameraHalf=>_camera.orthographicSize * _camera.aspect;
        private float edgeL, edgeR;
        private void Start()
        {
            edgeL = cameraEdgeLeft[0].position.x;
            edgeR = cameraEdgeRight[0].position.x;
        }

        public void UpdateCamera(float deltaX)//中心跟随玩家
        {
            Vector3 pos = _camera.transform.position;
            pos.x = Mathf.Clamp(deltaX + pos.x, edgeL, edgeR);//镜头移动根据当前波次有关
            _camera.transform.position = pos;
        }
        public void SetEdge(int wave)
        {
            edgeL = cameraEdgeLeft[wave - 1].position.x;
            edgeR = cameraEdgeRight[wave - 1].position.x;
        }
    }
}

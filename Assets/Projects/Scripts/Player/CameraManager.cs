using System;
using UnityEngine;
namespace zmxy.GamePlay
{
    public class CameraManager : MonoBehaviour
    {
        private Camera _camera;
        [SerializeField] private float followSpeed;//镜头跟随角色的速度
        [SerializeField] private Transform[] cameraEdgeLeft;
        [SerializeField] private Transform[] cameraEdgeRight;
        public float CameraCenter => _camera.transform.position.x;//摄像机中心
        public float CameraHalf=>_camera.orthographicSize * _camera.aspect;
        private float edgeL, edgeR;
        public float EdgeL => edgeL;
        public float EdgeR => edgeR;
        private void Awake() => _camera = GetComponent<Camera>();
        public bool CheckNextWave(int wave)
        {
            bool b = CameraCenter >= cameraEdgeLeft[wave].position.x;
            if (b) SetEdge(wave);
            return b;
        }
        private void Start()
        {
            edgeL = cameraEdgeLeft[0].position.x;
            edgeR = cameraEdgeRight[0].position.x;
        }

        public void UpdateCamera(float deltaX)//中心跟随玩家
        {
            Vector3 pos = _camera.transform.position;
            float targetX = Mathf.Clamp(deltaX + pos.x, edgeL, edgeR);
            pos.x = Mathf.MoveTowards(pos.x, targetX, followSpeed * Time.deltaTime);
            _camera.transform.position = pos;
        }
        public void SetEdge(int wave)
        {
            if (wave < 0)
                edgeR = cameraEdgeRight[-wave].position.x;
            else
                edgeL = cameraEdgeLeft[wave].position.x;
        }
    }
}

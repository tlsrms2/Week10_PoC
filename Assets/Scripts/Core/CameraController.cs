using UnityEngine;
using UnityEngine.InputSystem;

namespace Overlap.Core
{
    /// <summary>
    /// 마우스 우클릭 드래그로 2D 카메라(맵 시점)를 이동시키는 컨트롤러입니다.
    /// 추가로 마우스 휠을 이용한 줌 인/아웃 기능도 포함되어 있습니다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [Header("Pan Settings")]
        [Tooltip("드래그 이동을 활성화할지 여부")]
        public bool enablePanning = true;

        [Header("Zoom Settings")]
        [Tooltip("마우스 휠 줌을 활성화할지 여부")]
        public bool enableZoom = true;
        public float zoomSpeed = 1f;
        public float minZoom = 2f;
        public float maxZoom = 15f;

        private Camera targetCamera;
        private Vector3 dragOriginWorld;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            HandleZoom(mouse);
            HandlePanning(mouse);
        }

        private void HandlePanning(Mouse mouse)
        {
            if (!enablePanning) return;

            // 마우스 오른쪽 버튼을 누른 순간, 현재 마우스 위치의 '월드 좌표'를 저장합니다.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                dragOriginWorld = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                dragOriginWorld.z = 0f;
            }

            // 누르고 있는 동안, 저장한 원본 좌표와 현재 마우스 좌표의 차이만큼 카메라를 이동시킵니다.
            if (mouse.rightButton.isPressed)
            {
                Vector3 currentMouseWorld = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                currentMouseWorld.z = 0f;

                Vector3 difference = dragOriginWorld - currentMouseWorld;
                
                // 카메라 이동
                transform.position += difference;
            }
        }

        private void HandleZoom(Mouse mouse)
        {
            if (!enableZoom) return;

            float scrollDelta = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // 스크롤 방향에 따라 Orthographic Size 조절 (위로 굴리면 확대, 아래로 굴리면 축소)
                targetCamera.orthographicSize -= scrollDelta * zoomSpeed * 0.001f;
                targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, minZoom, maxZoom);
            }
        }
    }
}

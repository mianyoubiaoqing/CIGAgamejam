using UnityEngine;
using UnityEngine.EventSystems;

namespace CIGAgamejam
{
    public sealed class CameraPanZoomController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _preserveSceneCameraOnStart = true;

        [Header("Movement")]
        [Tooltip("相机中心点允许移动到的最小世界坐标。")]
        [SerializeField] private Vector2 _movementMin = new(-20f, -20f);
        [Tooltip("相机中心点允许移动到的最大世界坐标。")]
        [SerializeField] private Vector2 _movementMax = new(20f, 20f);
        [SerializeField] private int _dragMouseButton = 2;

        [Header("Zoom")]
        [Tooltip("每格鼠标滚轮改变的正交缩放尺寸。")]
        [Min(0f)]
        [SerializeField] private float _zoomSpeed = 2f;
        [Tooltip("最大放大倍率对应的正交尺寸。数值越小，画面越近。")]
        [Min(0.01f)]
        [SerializeField] private float _minSize = 3f;
        [Tooltip("最大缩小倍率对应的正交尺寸。数值越大，画面越远。")]
        [Min(0.01f)]
        [SerializeField] private float _maxSize = 10f;

        private Vector3 _lastMouseWorld;
        private bool _isDragging;

        private void Awake()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();

            if (!_preserveSceneCameraOnStart && _camera != null && _camera.orthographic)
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, _minSize, _maxSize);

            if (!_preserveSceneCameraOnStart)
                ClampPosition();
        }

        private void Update()
        {
            if (_camera == null)
                return;

            if (IsPointerOverUi())
                return;

            HandleZoom();
            HandleDrag();
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
                return;

            float size = _camera.orthographicSize - scroll * _zoomSpeed;
            _camera.orthographicSize = Mathf.Clamp(size, _minSize, _maxSize);
            ClampPosition();
        }

        private void HandleDrag()
        {
            if (Input.GetMouseButtonDown(_dragMouseButton))
            {
                _isDragging = true;
                _lastMouseWorld = GetMouseWorld();
                return;
            }

            if (Input.GetMouseButtonUp(_dragMouseButton))
            {
                _isDragging = false;
                return;
            }

            if (!_isDragging || !Input.GetMouseButton(_dragMouseButton))
                return;

            Vector3 currentMouseWorld = GetMouseWorld();
            Vector3 delta = _lastMouseWorld - currentMouseWorld;
            transform.position += new Vector3(delta.x, delta.y, 0f);
            ClampPosition();
            _lastMouseWorld = GetMouseWorld();
        }

        private void ClampPosition()
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, _movementMin.x, _movementMax.x);
            position.y = Mathf.Clamp(position.y, _movementMin.y, _movementMax.y);
            transform.position = position;
        }

        private Vector3 GetMouseWorld()
        {
            Vector3 mouse = Input.mousePosition;
            mouse.z = -_camera.transform.position.z;
            return _camera.ScreenToWorldPoint(mouse);
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
        }

        private void OnValidate()
        {
            _zoomSpeed = Mathf.Max(0f, _zoomSpeed);
            _minSize = Mathf.Max(0.01f, _minSize);
            _maxSize = Mathf.Max(_minSize, _maxSize);

            _movementMax.x = Mathf.Max(_movementMin.x, _movementMax.x);
            _movementMax.y = Mathf.Max(_movementMin.y, _movementMax.y);

            if (_camera == null)
                _camera = GetComponent<Camera>();

            if (!_preserveSceneCameraOnStart && _camera != null && _camera.orthographic)
            {
                _camera.orthographicSize = Mathf.Clamp(
                    _camera.orthographicSize,
                    _minSize,
                    _maxSize);

                ClampPosition();
            }
        }
    }
}

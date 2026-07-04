using UnityEngine;
using UnityEngine.EventSystems;

namespace CIGAgamejam
{
    public sealed class CameraPanZoomController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _minSize = 3f;
        [SerializeField] private float _maxSize = 10f;
        [SerializeField] private int _dragMouseButton = 2;

        private Vector3 _lastMouseWorld;
        private bool _isDragging;

        private void Awake()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
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
            _lastMouseWorld = GetMouseWorld();
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
    }
}
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CIGAgamejam
{
    public sealed class PrototypeInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private PlacementSystem _placementSystem;
        [SerializeField] private ToolInventorySystem _inventorySystem;
        [SerializeField] private NightTurnSystem _nightTurnSystem;
        [SerializeField] private PrototypeWorldView _worldView;
        [SerializeField] private RawImage _roomView;
        [Header("Cursor")]
        [SerializeField] private Texture2D _mouseIdle;
        [SerializeField] private Texture2D _mouseClick;
        [SerializeField] private Vector2 _cursorHotspot = Vector2.zero;
        [SerializeField, Min(0.01f)] private float _clickCursorDuration = 0.2f;

        private ToolConfig _selectedTool;
        private float _clickCursorTimer;

        public ToolConfig SelectedTool => _selectedTool;

        private void Awake()
        {
            if (_roomView == null)
            {
                GameObject roomView = GameObject.Find("Room View");
                if (roomView != null)
                    _roomView = roomView.GetComponent<RawImage>();
            }
        }

        private void OnEnable()
        {
            ApplyIdleCursor();
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _worldView?.HidePlacementPreview();
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void Update()
        {
            UpdateCursorTimer();

            if (_selectedTool == null)
            {
                _worldView?.HidePlacementPreview();
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                SelectTool(null);
                return;
            }

            if (_camera == null || _worldView == null || _placementSystem == null || _inventorySystem == null) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _worldView.HidePlacementPreview();
                if (Input.GetMouseButtonDown(0))
                    FlashClickCursor();
                return;
            }

            if (!TryScreenToRoomWorld(Input.mousePosition, out Vector3 worldPosition))
            {
                _worldView.HidePlacementPreview();
                return;
            }

            if (!_worldView.TryWorldToGrid(worldPosition, out GridPosition gridPosition))
            {
                _worldView.HidePlacementPreview();
                return;
            }

            bool canPlace = _placementSystem.CanPlaceTool(_selectedTool, gridPosition) == PlacementResult.Success;
            _worldView.ShowPlacementPreview(_selectedTool, gridPosition, canPlace);

            if (!Input.GetMouseButtonDown(0)) return;

            FlashClickCursor();
            TryPlaceSelectedTool(gridPosition);
        }

        private bool TryScreenToRoomWorld(Vector2 screenPosition, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (_camera == null || _roomView == null)
                return false;

            RectTransform roomRect = _roomView.rectTransform;
            Canvas canvas = _roomView.canvas;
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    roomRect,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            Rect rect = roomRect.rect;
            if (!rect.Contains(localPoint))
                return false;

            float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
            float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
            Rect uvRect = _roomView.uvRect;
            float viewportX = uvRect.x + normalizedX * uvRect.width;
            float viewportY = uvRect.y + normalizedY * uvRect.height;
            float distanceToGridPlane = Mathf.Abs(_camera.transform.position.z);

            worldPosition = _camera.ViewportToWorldPoint(
                new Vector3(viewportX, viewportY, distanceToGridPlane));
            return true;
        }

        public void SelectTool(ToolConfig tool)
        {
            _selectedTool = tool;
            EventBus<OnToolSelected>.Publish(new OnToolSelected(tool));
            EventBus<OnPrototypeLogMessage>.Publish(
                new OnPrototypeLogMessage(tool == null
                    ? "\u53d6\u6d88\u9009\u62e9\u9053\u5177\u3002"
                    : $"\u9009\u62e9 {tool.DisplayName}: \u70b9\u51fb\u5e97\u94fa\u683c\u5b50\u653e\u7f6e\u3002"));
        }

        public bool TryPlaceSelectedTool(GridPosition gridPosition)
        {
            if (_selectedTool == null) return false;

            ToolConfig tool = _selectedTool;
            if (_inventorySystem.GetCount(tool) <= 0)
            {
                EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage($"{tool.DisplayName} \u5e93\u5b58\u4e0d\u8db3\u3002"));
                return false;
            }

            if (!_placementSystem.TryPlaceTool(tool, gridPosition, out _))
                return false;

            _inventorySystem.TryConsume(tool);
            _nightTurnSystem?.RecordPlayerAction($"\u653e\u7f6e {tool.DisplayName}");
            return true;
        }

        private void FlashClickCursor()
        {
            if (_mouseClick == null)
                return;

            Cursor.SetCursor(_mouseClick, _cursorHotspot, CursorMode.Auto);
            _clickCursorTimer = _clickCursorDuration;
        }

        private void UpdateCursorTimer()
        {
            if (_clickCursorTimer <= 0f)
                return;

            _clickCursorTimer -= Time.unscaledDeltaTime;
            if (_clickCursorTimer <= 0f)
                ApplyIdleCursor();
        }

        private void ApplyIdleCursor()
        {
            if (_mouseIdle != null)
                Cursor.SetCursor(_mouseIdle, _cursorHotspot, CursorMode.Auto);
        }
    }
}

using UnityEngine;

namespace CIGAgamejam
{
    public sealed class PrototypeInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private PlacementSystem _placementSystem;
        [SerializeField] private ToolInventorySystem _inventorySystem;
        [SerializeField] private NightTurnSystem _nightTurnSystem;
        [SerializeField] private PrototypeWorldView _worldView;

        private ToolConfig _selectedTool;

        public ToolConfig SelectedTool => _selectedTool;

        private void Update()
        {
            if (_selectedTool == null) return;
            if (Input.GetMouseButtonDown(1))
            {
                SelectTool(null);
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (_camera == null || _worldView == null || _placementSystem == null || _inventorySystem == null) return;

            Vector3 worldPosition = _camera.ScreenToWorldPoint(Input.mousePosition);
            if (!_worldView.TryWorldToGrid(worldPosition, out GridPosition gridPosition))
                return;

            TryPlaceSelectedTool(gridPosition);
        }

        public void SelectTool(ToolConfig tool)
        {
            _selectedTool = tool;
            EventBus<OnToolSelected>.Publish(new OnToolSelected(tool));
            EventBus<OnPrototypeLogMessage>.Publish(
                new OnPrototypeLogMessage(tool == null ? "取消选择道具。" : $"选择 {tool.DisplayName}: 点击店铺格子放置。"));
        }

        public bool TryPlaceSelectedTool(GridPosition gridPosition)
        {
            if (_selectedTool == null) return false;

            ToolConfig tool = _selectedTool;
            if (_inventorySystem.GetCount(tool) <= 0)
            {
                EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage($"{tool.DisplayName} 库存不足。"));
                return false;
            }

            if (!_placementSystem.TryPlaceTool(tool, gridPosition, out _))
            {
                EventBus<OnPrototypeLogMessage>.Publish(new OnPrototypeLogMessage($"{tool.DisplayName} 不能放在 {gridPosition}。"));
                return false;
            }

            _inventorySystem.TryConsume(tool);
            _nightTurnSystem?.RecordPlayerAction($"放置 {tool.DisplayName}");
            return true;
        }
    }
}

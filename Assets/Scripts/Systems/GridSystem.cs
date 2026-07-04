using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class GridSystem : MonoBehaviour
    {
        [SerializeField] private GridConfig _config;

        private readonly Dictionary<GridPosition, GridCellType> _cellTypes = new();
        private readonly Dictionary<GridPosition, PlacedTool> _occupants = new();
        private readonly List<PlacedTool> _placedTools = new();
        private int _nextToolInstanceId = 1;
        private bool _hasConfigError;

        public IReadOnlyList<PlacedTool> PlacedTools => _placedTools;
        public int Width => _hasConfigError ? 0 : _config.Width;
        public int Height => _hasConfigError ? 0 : _config.Height;

        private void Awake()
        {
            InitializeGrid();
        }

        public void InitializeGrid()
        {
            _cellTypes.Clear();
            _occupants.Clear();
            _placedTools.Clear();
            _nextToolInstanceId = 1;
            _hasConfigError = false;

            if (_config == null)
            {
                Debug.LogError("[GridSystem] GridConfig is not assigned.");
                _hasConfigError = true;
                return;
            }

            _config.Validate();

            for (int y = 0; y < _config.Height; y++)
            for (int x = 0; x < _config.Width; x++)
                _cellTypes[new GridPosition(x, y)] = GridCellType.Floor;

            GridCellDefinition[] overrides = _config.CellOverrides;
            if (overrides == null) return;

            for (int i = 0; i < overrides.Length; i++)
            {
                GridPosition position = new GridPosition(overrides[i].Position);
                if (IsInBounds(position))
                    _cellTypes[position] = overrides[i].CellType;
            }
        }

        public bool IsInBounds(GridPosition position)
        {
            return !_hasConfigError
                && position.X >= 0
                && position.Y >= 0
                && position.X < _config.Width
                && position.Y < _config.Height;
        }

        public bool TryGetCellType(GridPosition position, out GridCellType cellType)
        {
            return _cellTypes.TryGetValue(position, out cellType);
        }

        public bool TryGetToolAt(GridPosition position, out PlacedTool tool)
        {
            return _occupants.TryGetValue(position, out tool);
        }

        public bool IsOccupied(GridPosition position)
        {
            return _occupants.ContainsKey(position);
        }

        public bool IsRouteWalkable(GridPosition position)
        {
            if (!IsInBounds(position)) return false;
            if (!TryGetCellType(position, out GridCellType cellType)) return false;

            return cellType != GridCellType.Wall
                && cellType != GridCellType.Warehouse
                && cellType != GridCellType.Restroom
                && cellType != GridCellType.Blocked;
        }

        public PlacementResult CanPlaceTool(ToolConfig tool, GridPosition origin)
        {
            if (_hasConfigError) return PlacementResult.OutOfBounds;
            if (tool == null) return PlacementResult.MissingTool;

            tool.Validate();

            if (tool.UniquePerBoard && ContainsToolId(tool.Id))
                return PlacementResult.DuplicateUniqueTool;

            Vector2Int[] footprint = tool.Footprint;
            for (int i = 0; i < footprint.Length; i++)
            {
                GridPosition position = Offset(origin, footprint[i]);
                if (!IsInBounds(position))
                    return PlacementResult.OutOfBounds;

                if (_occupants.ContainsKey(position))
                    return PlacementResult.CellOccupied;

                if (!TryGetCellType(position, out GridCellType cellType) || !CanPlaceToolOnCellType(tool, cellType))
                    return PlacementResult.CellTypeNotAllowed;
            }

            return PlacementResult.Success;
        }

        public bool TryPlaceTool(ToolConfig tool, GridPosition origin, out PlacedTool placedTool)
        {
            placedTool = null;
            PlacementResult result = CanPlaceTool(tool, origin);
            if (result != PlacementResult.Success)
                return false;

            Vector2Int[] footprint = tool.Footprint;
            GridPosition[] occupiedCells = new GridPosition[footprint.Length];
            for (int i = 0; i < footprint.Length; i++)
                occupiedCells[i] = Offset(origin, footprint[i]);

            placedTool = new PlacedTool(_nextToolInstanceId++, tool, origin, occupiedCells);
            _placedTools.Add(placedTool);

            for (int i = 0; i < occupiedCells.Length; i++)
                _occupants[occupiedCells[i]] = placedTool;

            EventBus<OnToolPlaced>.Publish(new OnToolPlaced(placedTool));
            return true;
        }

        public IReadOnlyList<PlacedTool> GetTriggerableTools(ToolTriggerTiming timing, GridPosition position)
        {
            var result = new List<PlacedTool>();
            for (int i = 0; i < _placedTools.Count; i++)
            {
                PlacedTool tool = _placedTools[i];
                if (!tool.CanTrigger(timing)) continue;
                if (timing == ToolTriggerTiming.OnDayStart || TriggerAreaContains(tool, position))
                    result.Add(tool);
            }

            return result;
        }

        public PlacedTool DisableRandomBossTarget()
        {
            var candidates = new List<PlacedTool>();
            for (int i = 0; i < _placedTools.Count; i++)
            {
                PlacedTool tool = _placedTools[i];
                if (tool.Config != null && tool.Config.CanBeDisabledByBoss && !tool.IsDisabled && !tool.IsExhausted)
                    candidates.Add(tool);
            }

            if (candidates.Count == 0)
                return null;

            PlacedTool selected = candidates[Random.Range(0, candidates.Count)];
            if (selected.Disable(ToolDisableReason.BossInterference))
                EventBus<OnToolDisabled>.Publish(new OnToolDisabled(selected, ToolDisableReason.BossInterference));

            return selected;
        }

        private bool ContainsToolId(string toolId)
        {
            for (int i = 0; i < _placedTools.Count; i++)
                if (_placedTools[i].Config != null && _placedTools[i].Config.Id == toolId)
                    return true;

            return false;
        }

        private static GridPosition Offset(GridPosition origin, Vector2Int offset)
        {
            return new GridPosition(origin.X + offset.x, origin.Y + offset.y);
        }

        private static bool CanPlaceToolOnCellType(ToolConfig tool, GridCellType cellType)
        {
            if (tool.AllowsCellType(cellType))
                return true;

            // The prototype art layer uses semantic cell types for shelves, cashier tiles,
            // doors, and patrol marks. Keep those visual tiles usable as trap placement
            // surfaces unless they are hard blockers.
            return cellType == GridCellType.Floor
                || cellType == GridCellType.Warehouse
                || cellType == GridCellType.Security
                || cellType == GridCellType.Entrance
                || cellType == GridCellType.Checkout
                || cellType == GridCellType.Exit;
        }

        private static bool TriggerAreaContains(PlacedTool tool, GridPosition position)
        {
            Vector2Int[] offsets = tool.Config.TriggerOffsets;
            for (int i = 0; i < offsets.Length; i++)
            {
                GridPosition triggerPosition = Offset(tool.Origin, offsets[i]);
                if (triggerPosition.Equals(position))
                    return true;
            }

            return false;
        }
    }
}

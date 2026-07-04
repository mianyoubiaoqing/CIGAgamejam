using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class GridSystem : MonoBehaviour
    {
        [SerializeField] private TilemapGridBridge _tilemapBridge;

        private readonly Dictionary<GridPosition, GridCellType> _cellTypes = new();
        private readonly Dictionary<GridPosition, PuzzleTileState> _tileStates = new();
        private readonly List<PlacedTool> _placedTools = new();
        private int _nextToolInstanceId = 1;
        private bool _hasConfigError;
        private int _minX;
        private int _minY;
        private int _maxXExclusive;
        private int _maxYExclusive;

        public IReadOnlyList<PlacedTool> PlacedTools => _placedTools;
        public int Width => _hasConfigError ? 0 : _maxXExclusive - _minX;
        public int Height => _hasConfigError ? 0 : _maxYExclusive - _minY;
        public int MinX => _hasConfigError ? 0 : _minX;
        public int MinY => _hasConfigError ? 0 : _minY;
        public int MaxXExclusive => _hasConfigError ? 0 : _maxXExclusive;
        public int MaxYExclusive => _hasConfigError ? 0 : _maxYExclusive;

        private void Awake()
        {
            InitializeGrid();
        }

        private void Start()
        {
            if (_hasConfigError)
            {
                Debug.LogError("[GridSystem] TilemapGridBridge is missing or has no readable visual Tilemap layers.");
                InitializeGrid();
            }
        }

        public void InitializeGrid()
        {
            _cellTypes.Clear();
            _tileStates.Clear();
            _placedTools.Clear();
            _nextToolInstanceId = 1;
            _hasConfigError = false;

            if (_tilemapBridge != null
                && _tilemapBridge.TryReadCells(out Dictionary<GridPosition, GridCellType> tileCells, out BoundsInt bounds))
            {
                _minX = bounds.xMin;
                _minY = bounds.yMin;
                _maxXExclusive = bounds.xMax;
                _maxYExclusive = bounds.yMax;
                foreach (KeyValuePair<GridPosition, GridCellType> pair in tileCells)
                {
                    _cellTypes[pair.Key] = pair.Value;
                    _tileStates[pair.Key] = new PuzzleTileState(pair.Value);
                }
                return;
            }

            Debug.LogError("[GridSystem] TilemapGridBridge is required. GridConfig fallback has been removed.");
            _hasConfigError = true;
        }

        public bool IsInBounds(GridPosition position)
        {
            return !_hasConfigError
                && position.X >= _minX
                && position.Y >= _minY
                && position.X < _maxXExclusive
                && position.Y < _maxYExclusive
                && _cellTypes.ContainsKey(position);
        }

        public bool TryGetCellType(GridPosition position, out GridCellType cellType)
        {
            return _cellTypes.TryGetValue(position, out cellType);
        }

        public bool TryGetToolAt(GridPosition position, out PlacedTool tool)
        {
            tool = null;
            if (!_tileStates.TryGetValue(position, out PuzzleTileState state)) return false;
            tool = state.ReplacementTool ?? state.AttachedTool;
            return tool != null;
        }

        public bool TryGetTileState(GridPosition position, out PuzzleTileState state)
        {
            return _tileStates.TryGetValue(position, out state);
        }

        public bool IsOccupied(GridPosition position)
        {
            return TryGetToolAt(position, out _);
        }

        public bool IsRouteWalkable(GridPosition position)
        {
            if (!IsInBounds(position)) return false;
            return _tilemapBridge != null && _tilemapBridge.HasGroundTile(position);
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

                if (!_tileStates.TryGetValue(position, out PuzzleTileState state))
                    return PlacementResult.OutOfBounds;

                bool occupied = tool.PlacementKind == ToolPlacementKind.ModifyPuzzle
                    ? state.AttachedTool != null || state.IsDestroyed
                    : state.ReplacementTool != null || state.IsDestroyed;
                if (occupied)
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
            {
                PuzzleTileState state = _tileStates[occupiedCells[i]];
                bool placed = tool.PlacementKind == ToolPlacementKind.ModifyPuzzle
                    ? state.TryAttach(placedTool)
                    : state.TryReplace(placedTool);
                if (!placed)
                {
                    RemoveToolFromBoard(placedTool);
                    placedTool = null;
                    return false;
                }
            }

            EventBus<OnToolPlaced>.Publish(new OnToolPlaced(placedTool));
            return true;
        }

        public bool RemoveToolFromBoard(PlacedTool tool)
        {
            if (tool == null || !_placedTools.Remove(tool)) return false;

            for (int i = 0; i < tool.OccupiedCells.Length; i++)
                if (_tileStates.TryGetValue(tool.OccupiedCells[i], out PuzzleTileState state))
                    state.Remove(tool);

            return true;
        }

        public bool MarkTileDestroyed(GridPosition position)
        {
            if (!_tileStates.TryGetValue(position, out PuzzleTileState state)) return false;

            PlacedTool attached = state.AttachedTool;
            PlacedTool replacement = state.ReplacementTool;
            state.MarkDestroyed();
            if (attached != null) _placedTools.Remove(attached);
            if (replacement != null) _placedTools.Remove(replacement);
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
            {
                EventBus<OnToolDisabled>.Publish(new OnToolDisabled(selected, ToolDisableReason.BossInterference));
                RemoveToolFromBoard(selected);
            }

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

            return false;
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class SecurityPatrolSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField, Min(3)] private int _minPathLength = 6;
        [SerializeField, Min(3)] private int _maxPathLength = 10;
        [SerializeField, Min(0)] private int _visionRange = 2;
        [SerializeField, Min(1)] private int _stepsPerTurn = 2;

        [Header("Day Patrol")]
        [SerializeField] private bool _enableDayPatrol = true;
        [SerializeField, Min(0.5f)] private float _dayPatrolInterval = 3f;

        private readonly List<GridPosition> _patrolPath = new();
        private readonly HashSet<GridPosition> _bribedPositions = new();
        private int _patrolIndex;
        private GridPosition _currentPosition;
        private bool _hasConfigError;
        private GamePhase _currentPhase = GamePhase.None;
        private Coroutine _dayPatrolCoroutine;

        public GridPosition CurrentPosition => _currentPosition;
        public int VisionRange => _visionRange;
        public int StepsPerTurn => _stepsPerTurn;
        public IReadOnlyList<GridPosition> PatrolPath => _patrolPath;

        private void Awake()
        {
            if (_gridSystem == null)
            {
                Debug.LogError("[SecurityPatrolSystem] GridSystem is not assigned.");
                _hasConfigError = true;
                return;
            }

            ResetPositionRandomly();
        }

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
            EventBus<OnSecurityPositionBribed>.Subscribe(HandleSecurityPositionBribed);
        }

        private void OnDestroy()
        {
            StopDayPatrol();
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
            EventBus<OnSecurityPositionBribed>.Unsubscribe(HandleSecurityPositionBribed);
        }

        public void BeginNightPatrol()
        {
            if (_hasConfigError) return;
            GeneratePatrolPathFrom(_currentPosition);
            _patrolIndex = 0;
            PublishPatrolPathChanged();
            PublishMoved();
            CheckVisibleTools();
        }

        public void AdvancePatrolStep()
        {
            if (_hasConfigError || _patrolPath.Count == 0) return;

            _patrolIndex = (_patrolIndex + 1) % _patrolPath.Count;
            _currentPosition = _patrolPath[_patrolIndex];
            PublishMoved();
            CheckVisibleTools();
        }

        public void GenerateRandomPatrolPath()
        {
            _patrolPath.Clear();
            if (_hasConfigError || _gridSystem == null)
                return;

            List<GridPosition> candidates = CollectWeightedCandidates();
            if (candidates.Count == 0)
                return;

            int minLength = Mathf.Max(1, _minPathLength);
            int maxLength = Mathf.Max(minLength, _maxPathLength);
            int targetLength = Random.Range(minLength, maxLength + 1);

            GridPosition current = candidates[Random.Range(0, candidates.Count)];
            _patrolPath.Add(current);
            var visited = new HashSet<GridPosition> { current };

            for (int i = 1; i < targetLength; i++)
            {
                List<GridPosition> neighbors = CollectWeightedWalkableUnvisitedNeighbors(current, visited);
                if (neighbors.Count == 0)
                    break;

                current = neighbors[Random.Range(0, neighbors.Count)];
                _patrolPath.Add(current);
                visited.Add(current);
            }
        }

        public void GeneratePatrolPathFrom(GridPosition start)
        {
            _patrolPath.Clear();
            if (_hasConfigError || _gridSystem == null)
                return;

            if (!TryResolvePatrolStart(start, out GridPosition current))
                return;

            int minLength = Mathf.Max(1, _minPathLength);
            int maxLength = Mathf.Max(minLength, _maxPathLength);
            int targetLength = Random.Range(minLength, maxLength + 1);

            _patrolPath.Add(current);
            var visited = new HashSet<GridPosition> { current };

            for (int i = 1; i < targetLength; i++)
            {
                List<GridPosition> neighbors = CollectWeightedWalkableUnvisitedNeighbors(current, visited);
                if (neighbors.Count == 0)
                    break;

                current = neighbors[Random.Range(0, neighbors.Count)];
                _patrolPath.Add(current);
                visited.Add(current);
            }

            _patrolIndex = 0;
            _currentPosition = _patrolPath[0];
        }

        public void AdvancePatrolTurn()
        {
            for (int i = 0; i < _stepsPerTurn; i++)
                AdvancePatrolStep();
        }

        public bool CanSee(GridPosition position)
        {
            int distance = Mathf.Abs(position.X - _currentPosition.X) + Mathf.Abs(position.Y - _currentPosition.Y);
            return distance <= _visionRange;
        }

        private void ResetPosition()
        {
            _patrolIndex = 0;
            _currentPosition = _patrolPath.Count > 0
                ? _patrolPath[0]
                : new GridPosition(0, 0);
        }

        private void ResetPositionRandomly()
        {
            if (_patrolPath.Count == 0)
            {
                ResetPosition();
                return;
            }

            _patrolIndex = Random.Range(0, _patrolPath.Count);
            _currentPosition = _patrolPath[_patrolIndex];
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            _currentPhase = e.NewPhase;

            if (e.NewPhase == GamePhase.NightPlanning)
            {
                StopDayPatrol();
                _bribedPositions.Clear();
                GeneratePatrolPathFrom(_currentPosition);
                PublishPatrolPathChanged();
                PublishMoved();
                CheckVisibleTools();
                return;
            }

            if (e.NewPhase == GamePhase.DaySimulation)
            {
                if (_enableDayPatrol)
                {
                    GeneratePatrolPathFrom(_currentPosition);
                    PublishPatrolPathChanged();
                    PublishMoved();
                    CheckVisibleTools();
                    StartDayPatrol();
                }
                else
                {
                    EventBus<OnSecurityPatrolPathCleared>.Publish(new OnSecurityPatrolPathCleared());
                    EventBus<OnSecurityPatrolCleared>.Publish(new OnSecurityPatrolCleared());
                }
                return;
            }

            StopDayPatrol();
        }

        private bool TryResolvePatrolStart(GridPosition requestedStart, out GridPosition start)
        {
            if (IsPatrolWalkable(requestedStart))
            {
                start = requestedStart;
                return true;
            }

            if (TryFindNearestPatrolWalkable(requestedStart, out start))
            {
                Debug.LogWarning(
                    $"[SecurityPatrolSystem] Patrol start {requestedStart} is not walkable. Using nearest walkable cell {start}.");
                return true;
            }

            start = default;
            Debug.LogError("[SecurityPatrolSystem] Could not find a walkable patrol start.");
            return false;
        }

        private bool TryFindNearestPatrolWalkable(GridPosition origin, out GridPosition nearest)
        {
            nearest = default;
            bool found = false;
            int bestDistance = int.MaxValue;

            for (int y = _gridSystem.MinY; y < _gridSystem.MaxYExclusive; y++)
            for (int x = _gridSystem.MinX; x < _gridSystem.MaxXExclusive; x++)
            {
                var position = new GridPosition(x, y);
                if (!IsPatrolWalkable(position))
                    continue;

                int distance = Mathf.Abs(position.X - origin.X) + Mathf.Abs(position.Y - origin.Y);
                if (found && distance >= bestDistance)
                    continue;

                found = true;
                bestDistance = distance;
                nearest = position;
            }

            return found;
        }

        private List<GridPosition> CollectWeightedCandidates()
        {
            var candidates = new List<GridPosition>();
            for (int y = _gridSystem.MinY; y < _gridSystem.MaxYExclusive; y++)
            for (int x = _gridSystem.MinX; x < _gridSystem.MaxXExclusive; x++)
            {
                var position = new GridPosition(x, y);
                if (!IsPatrolWalkable(position)) continue;

                int weight = GetPatrolWeight(position);
                for (int i = 0; i < weight; i++)
                    candidates.Add(position);
            }

            return candidates;
        }

        private List<GridPosition> CollectWeightedWalkableUnvisitedNeighbors(
            GridPosition position,
            HashSet<GridPosition> visited)
        {
            var neighbors = new List<GridPosition>();
            foreach (GridPosition neighbor in GetNeighbors(position))
            {
                if (visited.Contains(neighbor) || !IsPatrolWalkable(neighbor))
                    continue;

                int weight = GetPatrolWeight(neighbor);
                for (int i = 0; i < weight; i++)
                    neighbors.Add(neighbor);
            }

            return neighbors;
        }

        private bool IsPatrolWalkable(GridPosition position)
        {
            if (!_gridSystem.IsRouteWalkable(position))
                return false;

            if (!_gridSystem.TryGetCellType(position, out GridCellType cellType))
                return false;

            return cellType != GridCellType.Wall
                && cellType != GridCellType.Warehouse
                && cellType != GridCellType.Restroom
                && cellType != GridCellType.FortuneTree
                && cellType != GridCellType.Blocked;
        }

        private int GetPatrolWeight(GridPosition position)
        {
            int weight = HasAdjacentCellType(position, GridCellType.Wall)
                || HasAdjacentCellType(position, GridCellType.Warehouse)
                ? 3
                : 1;

            if (HasAdjacentCellType(position, GridCellType.Security))
                weight = Mathf.Max(1, weight - 1);

            return weight;
        }

        private bool HasAdjacentCellType(GridPosition position, GridCellType targetType)
        {
            foreach (GridPosition neighbor in GetNeighbors(position))
                if (_gridSystem.TryGetCellType(neighbor, out GridCellType cellType)
                    && cellType == targetType)
                    return true;

            return false;
        }

        private static IEnumerable<GridPosition> GetNeighbors(GridPosition position)
        {
            yield return new GridPosition(position.X + 1, position.Y);
            yield return new GridPosition(position.X - 1, position.Y);
            yield return new GridPosition(position.X, position.Y + 1);
            yield return new GridPosition(position.X, position.Y - 1);
        }

        private void StartDayPatrol()
        {
            StopDayPatrol();
            _dayPatrolCoroutine = StartCoroutine(DayPatrolRoutine());
        }

        private IEnumerator DayPatrolRoutine()
        {
            while (_currentPhase == GamePhase.DaySimulation)
            {
                yield return new WaitForSeconds(Mathf.Max(0.5f, _dayPatrolInterval));
                if (_currentPhase == GamePhase.DaySimulation)
                    AdvancePatrolStep();
            }

            _dayPatrolCoroutine = null;
        }

        private void StopDayPatrol()
        {
            if (_dayPatrolCoroutine == null)
                return;

            StopCoroutine(_dayPatrolCoroutine);
            _dayPatrolCoroutine = null;
        }

        private void HandleSecurityPositionBribed(OnSecurityPositionBribed e)
        {
            _bribedPositions.Add(e.Position);
        }

        private void CheckVisibleTools()
        {
            for (int i = _gridSystem.PlacedTools.Count - 1; i >= 0; i--)
            {
                PlacedTool tool = _gridSystem.PlacedTools[i];
                if (tool == null || tool.IsDisabled || tool.IsExhausted) continue;
                if (ToolIsProtectedByBribe(tool)) continue;
                if (!ToolIsVisible(tool)) continue;

                if (tool.Disable(ToolDisableReason.SecurityPatrol))
                {
                    EventBus<OnToolDisabled>.Publish(new OnToolDisabled(tool, ToolDisableReason.SecurityPatrol));
                    EventBus<OnSecurityRemovedTool>.Publish(new OnSecurityRemovedTool(tool, _currentPosition));
                    string message = _currentPhase == GamePhase.DaySimulation
                        ? $"保安在日常巡逻中发现了 {tool.Config.DisplayName} 并拆除！"
                        : $"保安在夜间巡逻时发现了 {tool.Config.DisplayName} 并拆除！";
                    EventBus<OnPrototypeLogMessage>.Publish(
                        new OnPrototypeLogMessage(message));
                    _gridSystem.RemoveToolFromBoard(tool);
                }
            }
        }

        private bool ToolIsProtectedByBribe(PlacedTool tool)
        {
            GridPosition[] cells = tool.OccupiedCells;
            for (int i = 0; i < cells.Length; i++)
                if (_bribedPositions.Contains(cells[i]))
                    return true;

            return false;
        }

        private bool ToolIsVisible(PlacedTool tool)
        {
            GridPosition[] cells = tool.OccupiedCells;
            for (int i = 0; i < cells.Length; i++)
                if (CanSee(cells[i]))
                    return true;

            return false;
        }

        private void PublishMoved()
        {
            EventBus<OnSecurityPatrolMoved>.Publish(new OnSecurityPatrolMoved(_currentPosition, _patrolIndex));
        }

        private void PublishPatrolPathChanged()
        {
            EventBus<OnSecurityPatrolPathChanged>.Publish(new OnSecurityPatrolPathChanged(_patrolPath));
        }
    }
}

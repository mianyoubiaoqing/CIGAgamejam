using System.Collections.Generic;
using UnityEngine;

namespace CIGAgamejam
{
    public sealed class SecurityPatrolSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField, Min(3)] private int _minPathLength = 4;
        [SerializeField, Min(3)] private int _maxPathLength = 8;
        [SerializeField, Min(0)] private int _visionRange = 1;
        [SerializeField, Min(1)] private int _stepsPerTurn = 1;

        private readonly List<GridPosition> _patrolPath = new();
        private int _patrolIndex;
        private GridPosition _currentPosition;
        private bool _hasConfigError;

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

            ResetPosition();
        }

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
        }

        private void OnDestroy()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
        }

        public void BeginNightPatrol()
        {
            if (_hasConfigError) return;
            if (_patrolPath.Count == 0)
                GenerateRandomPatrolPath();

            ResetPosition();
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

            List<GridPosition> candidates = CollectWalkableCandidates();
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
                List<GridPosition> neighbors = CollectWalkableUnvisitedNeighbors(current, visited);
                if (neighbors.Count == 0)
                    break;

                current = neighbors[Random.Range(0, neighbors.Count)];
                _patrolPath.Add(current);
                visited.Add(current);
            }
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

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            if (e.NewPhase == GamePhase.NightPlanning)
            {
                GenerateRandomPatrolPath();
                ResetPosition();
                PublishPatrolPathChanged();
                return;
            }

            if (e.NewPhase == GamePhase.DaySimulation)
            {
                EventBus<OnSecurityPatrolPathCleared>.Publish(new OnSecurityPatrolPathCleared());
                EventBus<OnSecurityPatrolCleared>.Publish(new OnSecurityPatrolCleared());
            }
        }

        private List<GridPosition> CollectWalkableCandidates()
        {
            var candidates = new List<GridPosition>();
            for (int y = _gridSystem.MinY; y < _gridSystem.MaxYExclusive; y++)
            for (int x = _gridSystem.MinX; x < _gridSystem.MaxXExclusive; x++)
            {
                var position = new GridPosition(x, y);
                if (_gridSystem.IsRouteWalkable(position))
                    candidates.Add(position);
            }

            return candidates;
        }

        private List<GridPosition> CollectWalkableUnvisitedNeighbors(GridPosition position, HashSet<GridPosition> visited)
        {
            var neighbors = new List<GridPosition>();
            foreach (GridPosition neighbor in GetNeighbors(position))
            {
                if (!visited.Contains(neighbor) && _gridSystem.IsRouteWalkable(neighbor))
                    neighbors.Add(neighbor);
            }

            return neighbors;
        }

        private static IEnumerable<GridPosition> GetNeighbors(GridPosition position)
        {
            yield return new GridPosition(position.X + 1, position.Y);
            yield return new GridPosition(position.X - 1, position.Y);
            yield return new GridPosition(position.X, position.Y + 1);
            yield return new GridPosition(position.X, position.Y - 1);
        }

        private void CheckVisibleTools()
        {
            for (int i = _gridSystem.PlacedTools.Count - 1; i >= 0; i--)
            {
                PlacedTool tool = _gridSystem.PlacedTools[i];
                if (tool == null || tool.IsDisabled || tool.IsExhausted) continue;
                if (!ToolIsVisible(tool)) continue;

                if (tool.Disable(ToolDisableReason.SecurityPatrol))
                {
                    EventBus<OnToolDisabled>.Publish(new OnToolDisabled(tool, ToolDisableReason.SecurityPatrol));
                    EventBus<OnSecurityRemovedTool>.Publish(new OnSecurityRemovedTool(tool, _currentPosition));
                    EventBus<OnPrototypeLogMessage>.Publish(
                        new OnPrototypeLogMessage($"保安发现并拆除了 {tool.Config.DisplayName}。"));
                    _gridSystem.RemoveToolFromBoard(tool);
                }
            }
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

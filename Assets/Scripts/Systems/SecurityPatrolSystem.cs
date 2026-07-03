using UnityEngine;

namespace CIGAgamejam
{
    public sealed class SecurityPatrolSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private Vector2Int[] _patrolPath = { new(1, 1), new(3, 1), new(5, 1), new(5, 3), new(3, 3), new(1, 3) };
        [SerializeField, Min(0)] private int _visionRange = 1;
        [SerializeField, Min(1)] private int _stepsPerTurn = 1;

        private int _patrolIndex;
        private GridPosition _currentPosition;
        private bool _hasConfigError;

        public GridPosition CurrentPosition => _currentPosition;
        public int VisionRange => _visionRange;
        public int StepsPerTurn => _stepsPerTurn;

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

        public void BeginNightPatrol()
        {
            if (_hasConfigError) return;

            ResetPosition();
            PublishMoved();
            CheckVisibleTools();
        }

        public void AdvancePatrolStep()
        {
            if (_hasConfigError || _patrolPath == null || _patrolPath.Length == 0) return;

            _patrolIndex = (_patrolIndex + 1) % _patrolPath.Length;
            _currentPosition = new GridPosition(_patrolPath[_patrolIndex]);
            PublishMoved();
            CheckVisibleTools();
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
            _currentPosition = _patrolPath != null && _patrolPath.Length > 0
                ? new GridPosition(_patrolPath[0])
                : new GridPosition(0, 0);
        }

        private void CheckVisibleTools()
        {
            for (int i = 0; i < _gridSystem.PlacedTools.Count; i++)
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
    }
}

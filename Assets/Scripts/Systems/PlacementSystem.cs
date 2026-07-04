using UnityEngine;

namespace CIGAgamejam
{
    public sealed class PlacementSystem : MonoBehaviour
    {
        [SerializeField] private GridSystem _gridSystem;

        private GamePhase _currentPhase = GamePhase.None;
        private bool _hasConfigError;

        private void Awake()
        {
            if (_gridSystem == null)
            {
                Debug.LogError("[PlacementSystem] GridSystem is not assigned.");
                _hasConfigError = true;
            }
        }

        private void OnEnable()
        {
            EventBus<OnGamePhaseChanged>.Subscribe(HandleGamePhaseChanged);
        }

        private void OnDestroy()
        {
            EventBus<OnGamePhaseChanged>.Unsubscribe(HandleGamePhaseChanged);
        }

        public bool TryPlaceTool(ToolConfig tool, GridPosition origin, out PlacedTool placedTool)
        {
            placedTool = null;

            if (_hasConfigError)
                return false;

            if (_currentPhase != GamePhase.NightPlanning)
            {
                EventBus<OnToolPlacementRejected>.Publish(
                    new OnToolPlacementRejected(tool, origin, PlacementResult.NotNightPlanning));
                return false;
            }

            PlacementResult result = _gridSystem.CanPlaceTool(tool, origin);
            if (result != PlacementResult.Success)
            {
                EventBus<OnToolPlacementRejected>.Publish(new OnToolPlacementRejected(tool, origin, result));
                return false;
            }

            return _gridSystem.TryPlaceTool(tool, origin, out placedTool);
        }

        public PlacementResult CanPlaceTool(ToolConfig tool, GridPosition origin)
        {
            if (_hasConfigError)
                return PlacementResult.OutOfBounds;

            if (_currentPhase != GamePhase.NightPlanning)
                return PlacementResult.NotNightPlanning;

            return _gridSystem.CanPlaceTool(tool, origin);
        }

        private void HandleGamePhaseChanged(OnGamePhaseChanged e)
        {
            _currentPhase = e.NewPhase;
        }
    }
}

using UnityEngine;
using UnityEngine.Tilemaps;

namespace CIGAgamejam
{
    public sealed class TilemapOverlayController : MonoBehaviour
    {
        [SerializeField] private Tilemap _toolOverlay;
        [SerializeField] private Tilemap _stateOverlay;
        [SerializeField] private TileBase _toolTile;
        [SerializeField] private TileBase _destroyedTile;

        private void OnEnable()
        {
            EventBus<OnToolPlaced>.Subscribe(HandleToolPlaced);
            EventBus<OnToolDisabled>.Subscribe(HandleToolDisabled);
            EventBus<OnToolRemoved>.Subscribe(HandleToolRemoved);
            EventBus<OnWorldObjectDestroyed>.Subscribe(HandleDestroyed);
        }

        private void OnDestroy()
        {
            EventBus<OnToolPlaced>.Unsubscribe(HandleToolPlaced);
            EventBus<OnToolDisabled>.Unsubscribe(HandleToolDisabled);
            EventBus<OnToolRemoved>.Unsubscribe(HandleToolRemoved);
            EventBus<OnWorldObjectDestroyed>.Unsubscribe(HandleDestroyed);
        }

        private void HandleToolPlaced(OnToolPlaced e)
        {
            if (_toolOverlay == null || _toolTile == null || e.Tool == null) return;
            _toolOverlay.SetTile(ToCell(e.Tool.Origin), _toolTile);
        }

        private void HandleToolDisabled(OnToolDisabled e)
        {
            if (_toolOverlay == null || e.Tool == null) return;
            foreach (GridPosition position in e.Tool.OccupiedCells)
                _toolOverlay.SetTile(ToCell(position), null);
        }

        private void HandleToolRemoved(OnToolRemoved e)
        {
            if (_toolOverlay == null || e.Tool == null) return;
            foreach (GridPosition position in e.Tool.OccupiedCells)
                _toolOverlay.SetTile(ToCell(position), null);
        }

        private void HandleDestroyed(OnWorldObjectDestroyed e)
        {
            if (_stateOverlay == null || _destroyedTile == null) return;
            _stateOverlay.SetTile(ToCell(e.Position), _destroyedTile);
        }

        private static Vector3Int ToCell(GridPosition position)
        {
            return new Vector3Int(position.X, position.Y, 0);
        }
    }
}

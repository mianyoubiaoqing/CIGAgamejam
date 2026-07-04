using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CIGAgamejam
{
    public sealed class TilemapGridBridge : MonoBehaviour
    {
        [SerializeField] private Tilemap _gameplayTilemap;

        public Tilemap GameplayTilemap => _gameplayTilemap;
        public bool IsReady => _gameplayTilemap != null && _gameplayTilemap.GetUsedTilesCount() > 0;

        public bool TryReadCells(
            out Dictionary<GridPosition, GameplayTile> cells,
            out BoundsInt bounds)
        {
            cells = new Dictionary<GridPosition, GameplayTile>();
            bounds = default;
            if (!IsReady) return false;

            bounds = _gameplayTilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (_gameplayTilemap.GetTile(cell) is GameplayTile tile)
                    cells[new GridPosition(cell.x, cell.y)] = tile;
            }

            return cells.Count > 0;
        }

        public Vector3 CellToWorld(GridPosition position)
        {
            return _gameplayTilemap != null
                ? _gameplayTilemap.GetCellCenterWorld(new Vector3Int(position.X, position.Y, 0))
                : new Vector3(position.X + 0.5f, position.Y + 0.5f, 0f);
        }

        public GridPosition WorldToCell(Vector3 worldPosition)
        {
            Vector3Int cell = _gameplayTilemap != null
                ? _gameplayTilemap.WorldToCell(worldPosition)
                : Vector3Int.FloorToInt(worldPosition);
            return new GridPosition(cell.x, cell.y);
        }
    }
}

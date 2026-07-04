using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CIGAgamejam
{
    public sealed class TilemapGridBridge : MonoBehaviour
    {
        [SerializeField] private LogicTilemapLayer[] _visualLayers = Array.Empty<LogicTilemapLayer>();

        public Tilemap CoordinateTilemap => ResolveCoordinateTilemap();
        public float CellSize => ResolveCellSize();
        public bool IsReady => CoordinateTilemap != null;

        public bool TryReadCells(
            out Dictionary<GridPosition, GridCellType> cells,
            out BoundsInt bounds)
        {
            cells = new Dictionary<GridPosition, GridCellType>();
            bounds = default;
            if (!IsReady) return false;

            bool hasBounds = false;
            for (int i = 0; i < _visualLayers.Length; i++)
            {
                Tilemap tilemap = _visualLayers[i].Tilemap;
                if (tilemap == null || tilemap.GetUsedTilesCount() == 0) continue;

                bounds = hasBounds ? Union(bounds, tilemap.cellBounds) : tilemap.cellBounds;
                hasBounds = true;

                foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                {
                    TileBase tile = tilemap.GetTile(cell);
                    if (tile == null) continue;

                    GridCellType cellType = ResolveCellType(tile, _visualLayers[i].CellType);
                    cells[new GridPosition(cell.x, cell.y)] = cellType;
                }
            }

            return cells.Count > 0;
        }

        public Vector3 CellToWorld(GridPosition position)
        {
            if (!IsReady)
            {
                Debug.LogError("[TilemapGridBridge] Cannot convert cell to world without a visual Tilemap source.");
                return Vector3.zero;
            }

            return CoordinateTilemap.GetCellCenterWorld(new Vector3Int(position.X, position.Y, 0));
        }

        public GridPosition WorldToCell(Vector3 worldPosition)
        {
            if (!IsReady)
            {
                Debug.LogError("[TilemapGridBridge] Cannot convert world to cell without a visual Tilemap source.");
                return new GridPosition(0, 0);
            }

            Vector3Int cell = CoordinateTilemap.WorldToCell(worldPosition);
            return new GridPosition(cell.x, cell.y);
        }

        private Tilemap ResolveCoordinateTilemap()
        {
            for (int i = 0; i < _visualLayers.Length; i++)
                if (_visualLayers[i].Tilemap != null)
                    return _visualLayers[i].Tilemap;

            return null;
        }

        private float ResolveCellSize()
        {
            Tilemap tilemap = CoordinateTilemap;
            if (tilemap != null && tilemap.layoutGrid != null)
            {
                Vector3 size = tilemap.layoutGrid.cellSize;
                float maxAxis = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y));
                if (maxAxis > 0f)
                    return maxAxis;
            }

            return 1f;
        }

        private static BoundsInt Union(BoundsInt a, BoundsInt b)
        {
            int xMin = Mathf.Min(a.xMin, b.xMin);
            int yMin = Mathf.Min(a.yMin, b.yMin);
            int zMin = Mathf.Min(a.zMin, b.zMin);
            int xMax = Mathf.Max(a.xMax, b.xMax);
            int yMax = Mathf.Max(a.yMax, b.yMax);
            int zMax = Mathf.Max(a.zMax, b.zMax);
            return new BoundsInt(xMin, yMin, zMin, xMax - xMin, yMax - yMin, zMax - zMin);
        }

        private static GridCellType ResolveCellType(TileBase tile, GridCellType fallback)
        {
            if (tile is GameplayTile gameplayTile)
                return gameplayTile.CellType;

            string tileName = tile != null ? tile.name.ToLowerInvariant() : string.Empty;
            if (tileName.Contains("wc"))
                return GridCellType.Restroom;
            if (tileName.Contains("cashier") || tileName.Contains("checkstand"))
                return GridCellType.Checkout;
            if (tileName.Contains("money_tree"))
                return GridCellType.FortuneTree;
            if (tileName.Contains("shelf"))
                return GridCellType.Warehouse;
            if (tileName.Contains("wall"))
                return GridCellType.Wall;

            return fallback;
        }

        [Serializable]
        public struct LogicTilemapLayer
        {
            public Tilemap Tilemap;
            public GridCellType CellType;
        }
    }
}

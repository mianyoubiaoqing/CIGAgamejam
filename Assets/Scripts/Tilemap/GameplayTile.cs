using UnityEngine;
using UnityEngine.Tilemaps;

namespace CIGAgamejam
{
    [CreateAssetMenu(fileName = "GameplayTile", menuName = "CIGAgamejam/Tilemap/Gameplay Tile")]
    public sealed class GameplayTile : TileBase
    {
        [SerializeField] private GridCellType _cellType = GridCellType.Floor;
        [SerializeField] private bool _walkable = true;
        [SerializeField] private bool _canBeDestroyed;

        public GridCellType CellType => _cellType;
        public bool Walkable => _walkable;
        public bool CanBeDestroyed => _canBeDestroyed;
    }
}

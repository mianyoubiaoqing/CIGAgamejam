using System;
using UnityEngine;

namespace CIGAgamejam
{
    [CreateAssetMenu(fileName = "GridConfig", menuName = "CIGAgamejam/Configs/GridConfig")]
    public sealed class GridConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _width = 8;
        [SerializeField, Min(1)] private int _height = 6;
        [SerializeField] private GridCellDefinition[] _cellOverrides = Array.Empty<GridCellDefinition>();

        public int Width => _width;
        public int Height => _height;
        public GridCellDefinition[] CellOverrides => _cellOverrides;

        private void OnValidate() => Validate();

        public void Validate()
        {
            if (_width < 1) _width = 1;
            if (_height < 1) _height = 1;
        }
    }

    [Serializable]
    public struct GridCellDefinition
    {
        public Vector2Int Position;
        public GridCellType CellType;
    }
}

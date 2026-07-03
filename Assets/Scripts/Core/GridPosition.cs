using System;
using UnityEngine;

namespace CIGAgamejam
{
    [Serializable]
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public readonly int X;
        public readonly int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPosition(Vector2Int value)
        {
            X = value.x;
            Y = value.y;
        }

        public Vector2Int ToVector2Int() => new Vector2Int(X, Y);

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X}, {Y})";
    }
}

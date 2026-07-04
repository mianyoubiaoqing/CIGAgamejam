namespace CIGAgamejam
{
    public sealed class PuzzleTileState
    {
        public GridCellType BaseCellType { get; }
        public bool IsDestroyed { get; private set; }
        public PlacedTool AttachedTool { get; private set; }
        public PlacedTool ReplacementTool { get; private set; }

        public PuzzleTileState(GridCellType baseCellType)
        {
            BaseCellType = baseCellType;
        }

        public bool TryAttach(PlacedTool tool)
        {
            if (tool == null || IsDestroyed || AttachedTool != null) return false;
            AttachedTool = tool;
            return true;
        }

        public bool TryReplace(PlacedTool tool)
        {
            if (tool == null || IsDestroyed || ReplacementTool != null) return false;
            ReplacementTool = tool;
            return true;
        }

        public void MarkDestroyed()
        {
            IsDestroyed = true;
            AttachedTool = null;
            ReplacementTool = null;
        }

        public void Remove(PlacedTool tool)
        {
            if (AttachedTool == tool) AttachedTool = null;
            if (ReplacementTool == tool) ReplacementTool = null;
        }
    }

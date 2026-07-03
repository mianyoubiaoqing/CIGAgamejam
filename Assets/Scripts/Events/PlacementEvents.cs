namespace CIGAgamejam
{
    public readonly struct OnToolPlacementRejected
    {
        public readonly ToolConfig Tool;
        public readonly GridPosition Origin;
        public readonly PlacementResult Result;

        public OnToolPlacementRejected(ToolConfig tool, GridPosition origin, PlacementResult result)
        {
            Tool = tool;
            Origin = origin;
            Result = result;
        }
    }

    public readonly struct OnToolPlaced
    {
        public readonly PlacedTool Tool;

        public OnToolPlaced(PlacedTool tool)
        {
            Tool = tool;
        }
    }

    public readonly struct OnToolDisabled
    {
        public readonly PlacedTool Tool;

        public OnToolDisabled(PlacedTool tool)
        {
            Tool = tool;
        }
    }
}

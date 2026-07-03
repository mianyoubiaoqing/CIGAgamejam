namespace CIGAgamejam
{
    public readonly struct OnToolTriggered
    {
        public readonly PlacedTool Tool;
        public readonly ToolTriggerTiming Timing;

        public OnToolTriggered(PlacedTool tool, ToolTriggerTiming timing)
        {
            Tool = tool;
            Timing = timing;
        }
    }

    public readonly struct OnToolEffectResolved
    {
        public readonly PlacedTool Tool;
        public readonly ToolEffectDefinition Effect;
        public readonly int CustomerId;

        public OnToolEffectResolved(PlacedTool tool, ToolEffectDefinition effect, int customerId)
        {
            Tool = tool;
            Effect = effect;
            CustomerId = customerId;
        }
    }
}

using System;

namespace CIGAgamejam
{
    public sealed class PlacedTool
    {
        public int InstanceId { get; }
        public ToolConfig Config { get; }
        public GridPosition Origin { get; }
        public GridPosition[] OccupiedCells { get; }
        public int RemainingUses { get; private set; }
        public bool IsDisabled { get; private set; }
        public ToolDisableReason DisableReason { get; private set; }

        public bool IsExhausted => RemainingUses == 0;

        public PlacedTool(int instanceId, ToolConfig config, GridPosition origin, GridPosition[] occupiedCells)
        {
            InstanceId = instanceId;
            Config = config;
            Origin = origin;
            OccupiedCells = occupiedCells ?? Array.Empty<GridPosition>();
            RemainingUses = config != null ? config.UseLimit : 0;
            DisableReason = ToolDisableReason.None;
        }

        public bool Disable(ToolDisableReason reason)
        {
            if (IsDisabled)
                return false;

            IsDisabled = true;
            DisableReason = reason;
            return true;
        }

        public void ConsumeUse()
        {
            if (RemainingUses > 0)
                RemainingUses--;
        }

        public bool CanTrigger(ToolTriggerTiming timing)
        {
            return Config != null
                && !IsDisabled
                && !IsExhausted
                && Config.TriggerTiming == timing;
        }
    }
}

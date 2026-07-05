namespace CIGAgamejam
{
    public readonly struct OnCustomerLeftStore
    {
        public readonly int CustomerId;
        public readonly ToolEffectType Reason;
        public readonly CustomerState State;

        public OnCustomerLeftStore(int customerId, ToolEffectType reason)
            : this(customerId, reason, CustomerState.Scared)
        {
        }

        public OnCustomerLeftStore(int customerId, ToolEffectType reason, CustomerState state)
        {
            CustomerId = customerId;
            Reason = reason;
            State = state;
        }
    }

    public readonly struct OnFavorabilityDeltaRequested
    {
        public readonly float Delta;
        public readonly int CustomerId;
        public readonly string Reason;

        public OnFavorabilityDeltaRequested(float delta, int customerId, string reason)
        {
            Delta = delta;
            CustomerId = customerId;
            Reason = reason;
        }
    }

    public readonly struct OnCustomerAngered
    {
        public readonly int CustomerId;
        public readonly ToolEffectType Reason;
        public readonly PlacedTool SourceTool;

        public OnCustomerAngered(int customerId, ToolEffectType reason, PlacedTool sourceTool)
        {
            CustomerId = customerId;
            Reason = reason;
            SourceTool = sourceTool;
        }
    }

    public readonly struct OnGroupScareRequested
    {
        public readonly GridPosition Origin;
        public readonly int Count;
        public readonly int PrimaryCustomerId;

        public OnGroupScareRequested(GridPosition origin, int count, int primaryCustomerId)
        {
            Origin = origin;
            Count = count;
            PrimaryCustomerId = primaryCustomerId;
        }
    }

    public readonly struct OnDayStartScareQuotaRequested
    {
        public readonly int Count;

        public OnDayStartScareQuotaRequested(int count)
        {
            Count = count;
        }
    }

    public readonly struct OnCustomerFinalized
    {
        public readonly int CustomerId;
        public readonly CustomerState State;
        public readonly bool Purchased;

        public OnCustomerFinalized(int customerId, CustomerState state, bool purchased)
        {
            CustomerId = customerId;
            State = state;
            Purchased = purchased;
        }
    }

    public readonly struct OnWorldObjectDestroyed
    {
        public readonly GridPosition Position;
        public readonly GridCellType CellType;

        public OnWorldObjectDestroyed(GridPosition position, GridCellType cellType)
        {
            Position = position;
            CellType = cellType;
        }
    }
}

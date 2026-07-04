namespace CIGAgamejam
{
    using System.Collections.Generic;

    public readonly struct OnNightTurnStarted
    {
        public readonly int Turn;

        public OnNightTurnStarted(int turn)
        {
            Turn = turn;
        }
    }

    public readonly struct OnNightTurnAdvanced
    {
        public readonly int Turn;
        public readonly string PlayerAction;

        public OnNightTurnAdvanced(int turn, string playerAction)
        {
            Turn = turn;
            PlayerAction = playerAction;
        }
    }

    public readonly struct OnSecurityPatrolMoved
    {
        public readonly GridPosition Position;
        public readonly int PatrolIndex;

        public OnSecurityPatrolMoved(GridPosition position, int patrolIndex)
        {
            Position = position;
            PatrolIndex = patrolIndex;
        }
    }

    public readonly struct OnSecurityRemovedTool
    {
        public readonly PlacedTool Tool;
        public readonly GridPosition SecurityPosition;

        public OnSecurityRemovedTool(PlacedTool tool, GridPosition securityPosition)
        {
            Tool = tool;
            SecurityPosition = securityPosition;
        }
    }

    public readonly struct OnSecurityPatrolPathChanged
    {
        public readonly IReadOnlyList<GridPosition> Path;

        public OnSecurityPatrolPathChanged(IReadOnlyList<GridPosition> path)
        {
            Path = path;
        }
    }

    public readonly struct OnSecurityPatrolPathCleared
    {
    }

    public readonly struct OnSecurityPatrolCleared
    {
    }

    public readonly struct OnToolInventoryChanged
    {
        public readonly ToolConfig Tool;
        public readonly int Count;
        public readonly ToolStockSource Source;

        public OnToolInventoryChanged(ToolConfig tool, int count, ToolStockSource source)
        {
            Tool = tool;
            Count = count;
            Source = source;
        }
    }

    public readonly struct OnCustomerFlowChanged
    {
        public readonly int InStoreCount;
        public readonly int TodayTotal;
        public readonly float Trend;

        public OnCustomerFlowChanged(int inStoreCount, int todayTotal, float trend)
        {
            InStoreCount = inStoreCount;
            TodayTotal = todayTotal;
            Trend = trend;
        }
    }

    public readonly struct OnPrototypeCustomerMoved
    {
        public readonly int CustomerId;
        public readonly GridPosition Position;
        public readonly float GridX;
        public readonly float GridY;
        public readonly CustomerState State;

        public OnPrototypeCustomerMoved(int customerId, GridPosition position)
            : this(customerId, position, position.X, position.Y, CustomerState.Normal)
        {
        }

        public OnPrototypeCustomerMoved(int customerId, GridPosition position, float gridX, float gridY)
            : this(customerId, position, gridX, gridY, CustomerState.Normal)
        {
        }

        public OnPrototypeCustomerMoved(int customerId, GridPosition position, float gridX, float gridY, CustomerState state)
        {
            CustomerId = customerId;
            Position = position;
            GridX = gridX;
            GridY = gridY;
            State = state;
        }
    }

    public readonly struct OnPrototypeCustomerRemoved
    {
        public readonly int CustomerId;

        public OnPrototypeCustomerRemoved(int customerId)
        {
            CustomerId = customerId;
        }
    }

    public readonly struct OnRouteChanged
    {
        public readonly bool HasRoute;
        public readonly int StepCount;

        public OnRouteChanged(bool hasRoute, int stepCount)
        {
            HasRoute = hasRoute;
            StepCount = stepCount;
        }
    }

    public readonly struct OnPrototypeLogMessage
    {
        public readonly string Message;

        public OnPrototypeLogMessage(string message)
        {
            Message = message;
        }
    }
}

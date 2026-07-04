namespace CIGAgamejam
{
    public enum GamePhase
    {
        None,
        NightPlanning,
        DaySimulation,
        DayResult,
        GameOver
    }

    public enum GameOutcome
    {
        None,
        ShopBankrupted,
        TimeLimitFailed
    }

    public enum GridCellType
    {
        Floor,
        Wall,
        Warehouse,
        Security,
        Entrance,
        Checkout,
        Restroom,
        Exit,
        Blocked,
        FortuneTree
    }

    public enum ToolCategory
    {
        Tax,
        Destroy,
        Scare,
        FakeGoods,
        Bribe,
        Utility
    }

    public enum ToolPlacementKind
    {
        ModifyPuzzle,
        ReplacePuzzle
    }

    public enum ToolTriggerTiming
    {
        OnDayStart,
        OnCustomerEnterCell,
        OnCustomerPassFrontCell,
        OnCustomerPurchase,
        OnManualResolve
    }

    public enum ToolEffectType
    {
        ModifyPurchaseCost = 0,
        ScareCustomerAway = 1,
        ReplaceGoodsWithFake = 2,
        BribeSecurity = 3,
        DestroyObject = 4,
        DisableTool = 5,
        ScareCustomerGroup = 6,
        ReduceFavorability = 7
    }

    public enum CustomerState
    {
        Normal,
        Angry,
        Scared
    }

    public enum ToolDisableReason
    {
        None,
        BossInterference,
        AfterRemovingCustomer,
        Effect,
        SecurityPatrol,
        Exhausted
    }

    public enum ToolRemovalReason
    {
        DayExpired
    }

    public enum ToolStockSource
    {
        BlackBossSupport,
        CarriedOver
    }

    public enum PlacementResult
    {
        Success,
        NotNightPlanning,
        MissingTool,
        OutOfBounds,
        CellOccupied,
        CellTypeNotAllowed,
        DuplicateUniqueTool
    }
}

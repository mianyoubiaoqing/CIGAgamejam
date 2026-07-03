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
        Blocked
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
        ModifyPurchaseCost,
        ScareCustomerAway,
        ReplaceGoodsWithFake,
        BribeSecurity,
        DestroyObject,
        DisableTool
    }

    public enum ToolDisableReason
    {
        None,
        BossInterference,
        AfterRemovingCustomer,
        Effect,
        SecurityPatrol
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

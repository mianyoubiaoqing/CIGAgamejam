namespace CIGAgamejam
{
    public sealed class CustomerContext
    {
        public int CustomerId { get; }
        public GridPosition Position { get; set; }
        public bool HasLeftStore { get; set; }
        public bool WasScaredAway { get; set; }
        public bool BoughtFakeGoods { get; set; }
        public bool WasRemovedBySecurity { get; set; }
        public float PurchaseCostModifier { get; set; }

        public CustomerContext(int customerId, GridPosition position)
        {
            CustomerId = customerId;
            Position = position;
            PurchaseCostModifier = 0f;
        }
    }
}

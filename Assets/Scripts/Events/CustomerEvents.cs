namespace CIGAgamejam
{
    public readonly struct OnCustomerLeftStore
    {
        public readonly int CustomerId;
        public readonly ToolEffectType Reason;

        public OnCustomerLeftStore(int customerId, ToolEffectType reason)
        {
            CustomerId = customerId;
            Reason = reason;
        }
    }
}

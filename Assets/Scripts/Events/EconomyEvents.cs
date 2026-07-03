namespace CIGAgamejam
{
    public readonly struct OnRevenueChanged
    {
        public readonly float CurrentRevenueIndex;
        public readonly float Delta;

        public OnRevenueChanged(float currentRevenueIndex, float delta)
        {
            CurrentRevenueIndex = currentRevenueIndex;
            Delta = delta;
        }
    }

    public readonly struct OnShopBankrupted
    {
        public readonly float CurrentRevenueIndex;
        public readonly float BankruptcyThreshold;

        public OnShopBankrupted(float currentRevenueIndex, float bankruptcyThreshold)
        {
            CurrentRevenueIndex = currentRevenueIndex;
            BankruptcyThreshold = bankruptcyThreshold;
        }
    }
}

namespace CIGAgamejam
{
    public readonly struct OnDayStarted
    {
        public readonly int CurrentDay;
        public readonly int MaxDays;

        public OnDayStarted(int currentDay, int maxDays)
        {
            CurrentDay = currentDay;
            MaxDays = maxDays;
        }
    }

    public readonly struct OnDayEnded
    {
        public readonly int CurrentDay;

        public OnDayEnded(int currentDay)
        {
            CurrentDay = currentDay;
        }
    }

    public readonly struct OnDayLimitReached
    {
        public readonly int CurrentDay;
        public readonly int MaxDays;

        public OnDayLimitReached(int currentDay, int maxDays)
        {
            CurrentDay = currentDay;
            MaxDays = maxDays;
        }
    }
}

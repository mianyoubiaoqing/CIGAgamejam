namespace CIGAgamejam
{
    public readonly struct OnGamePhaseChanged
    {
        public readonly GamePhase NewPhase;
        public readonly GamePhase PreviousPhase;

        public OnGamePhaseChanged(GamePhase newPhase, GamePhase previousPhase)
        {
            NewPhase = newPhase;
            PreviousPhase = previousPhase;
        }
    }

    public readonly struct OnGameEnded
    {
        public readonly GameOutcome Outcome;

        public OnGameEnded(GameOutcome outcome)
        {
            Outcome = outcome;
        }
    }
}

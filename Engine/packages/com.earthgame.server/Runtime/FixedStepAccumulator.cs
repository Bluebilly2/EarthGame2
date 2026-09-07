namespace EarthGame.Server
{
    /// <summary>
    /// Turns irregular real time into fixed steps. The host calls <see cref="Accumulate"/> with however much
    /// real time passed, then drains whole steps with <see cref="TryStep"/>. A stall longer than
    /// <see cref="MaxStepsPerUpdate"/> steps is not "caught up" — the excess is dropped and counted in
    /// <see cref="DroppedSeconds"/> — because a server that tries to simulate a ten-second hitch in one call
    /// hitches for ten more seconds, and the world's time is measured by ticks, not by the wall clock.
    /// </summary>
    public sealed class FixedStepAccumulator
    {
        private double _accumulated;

        public FixedStepAccumulator(double stepSeconds, int maxStepsPerUpdate = 5)
        {
            StepSeconds = stepSeconds > 0.0 ? stepSeconds : 1.0 / 20.0;
            MaxStepsPerUpdate = maxStepsPerUpdate < 1 ? 1 : maxStepsPerUpdate;
        }

        /// <summary>Length of one step in real seconds.</summary>
        public double StepSeconds { get; }

        /// <summary>Most steps one Accumulate may release; the rest of that update's time is dropped.</summary>
        public int MaxStepsPerUpdate { get; }

        /// <summary>Real seconds discarded because the host fell too far behind. Diagnostics, never gameplay.</summary>
        public double DroppedSeconds { get; private set; }

        /// <summary>Steps released so far.</summary>
        public long StepsReleased { get; private set; }

        private int _budgetThisUpdate;

        /// <summary>Adds elapsed real time and opens a fresh per-update step budget.</summary>
        public void Accumulate(double realSeconds)
        {
            if (realSeconds > 0.0) _accumulated += realSeconds;
            _budgetThisUpdate = MaxStepsPerUpdate;
            double cap = StepSeconds * MaxStepsPerUpdate;
            if (_accumulated > cap)
            {
                DroppedSeconds += _accumulated - cap;
                _accumulated = cap;
            }
        }

        /// <summary>
        /// A whole step is owed when the accumulated time is within a nanosecond of it. Subtracting 0.05 from 0.15
        /// twice leaves 0.049999999999999996, not 0.05; without the slack the third step of a 150 ms update is
        /// never released and time leaks a nanosecond at a time. The slack is far below any real frame's duration
        /// and cannot release a step that was not owed.
        /// </summary>
        private const double Slack = 1e-9;

        /// <summary>Releases one step if a whole one is owed and the per-update budget allows.</summary>
        public bool TryStep()
        {
            if (_budgetThisUpdate <= 0 || _accumulated + Slack < StepSeconds) return false;
            _accumulated -= StepSeconds;
            _budgetThisUpdate--;
            StepsReleased++;
            return true;
        }
    }
}

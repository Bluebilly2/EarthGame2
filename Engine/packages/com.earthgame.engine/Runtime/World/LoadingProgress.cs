using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>Where a phase of a loading stands.</summary>
    public enum LoadingPhaseState : byte
    {
        Waiting = 0,
        Working = 1,
        Done = 2,
    }

    /// <summary>A phase of a loading as its row on the loading screen shows it.</summary>
    public readonly struct LoadingPhase
    {
        public readonly string Name;
        public readonly LoadingPhaseState State;

        /// <summary>How much of the phase is done, 0 to 1, by the same reckoning as the whole bar.</summary>
        public readonly double Fraction;

        /// <summary>Seconds: what it took when done, how long it has run while working, what it is expected to take while waiting (NaN until the pace is known).</summary>
        public readonly double Seconds;

        /// <summary>The step in hand while the phase is working, else null.</summary>
        public readonly string Step;

        public LoadingPhase(string name, LoadingPhaseState state, double fraction, double seconds, string step)
        {
            Name = name;
            State = state;
            Fraction = fraction;
            Seconds = seconds;
            Step = step;
        }
    }

    /// <summary>
    /// How far a world's loading has got, from the steps it has reported and when (the loading screen's bar, 2026-09-25). It is
    /// fed the caller's own clock, the engine reading none.
    ///
    /// <para>The table is chosen by the reports: both loadings begin "Reading world", and the first report only one of
    /// <see cref="LoadingSteps.Making"/> and <see cref="LoadingSteps.Opening"/> holds settles which it is. The fraction is the
    /// shares of the steps behind the one in hand, and of its own share the part its expected time has run: its share at the
    /// pace the loading has kept so far (seconds for each share done), held to nine tenths until the next report comes, since a
    /// step can take longer than its share says and the bar must not reach its end before the step does. The saving step counts
    /// its layers instead. The fraction never goes back. A report the tables do not hold leaves it where it was.</para>
    /// </summary>
    public sealed class LoadingProgress
    {
        /// <summary>The most of a step's own share its time may fill before its end is reported.</summary>
        public const double MostOfAStep = 0.9;

        /// <summary>The fraction done before the time left is judged: under it the pace is one or two steps' guess.</summary>
        public const double LeastForAnEstimate = 0.05;

        private List<IReadOnlyList<LoadingStep>> _candidates = new List<IReadOnlyList<LoadingStep>> { LoadingSteps.Making, LoadingSteps.Opening };
        private IReadOnlyList<LoadingStep> _steps;
        private double _total;
        private int _index = -1;
        private double _startedAt = double.NaN, _stepStartedAt, _doneBefore, _shown;
        private int _saved;
        private readonly Dictionary<int, double> _stepAt = new Dictionary<int, double>();

        /// <summary>The table the reports settled on, or null until they have.</summary>
        public IReadOnlyList<LoadingStep> Steps => _steps;

        /// <summary>The step in hand, one-based, the first while the table is still one of two; zero before any step.</summary>
        public int StepNumber => _index + 1;

        /// <summary>How many steps the loading has; zero before the table is known.</summary>
        public int StepCount => _steps?.Count ?? 0;

        /// <summary>Takes a report of the loading and the time it came, s on the caller's clock.</summary>
        public void Report(string report, double atSeconds)
        {
            string step = LoadingSteps.StepOf(report);
            if (step == null) return;
            // A loading is followed from its first step: a join to another's world reports only the ground round the founder,
            // which is a table's last step and says nothing of how far it has got.
            if (_index < 0 && !_candidates.Exists(table => table.Count > 0 && table[0].Name == step)) return;
            if (double.IsNaN(_startedAt)) _startedAt = atSeconds;
            if (_steps == null)
            {
                _candidates.RemoveAll(table => IndexOf(table, step, 0) < 0);
                if (_candidates.Count == 0) return;
                if (_candidates.Count > 1)
                {
                    // Still either loading: the steps both begin with, taken on the first table, whose place they share.
                    Advance(_candidates[0], step, atSeconds);
                    return;
                }
                _steps = _candidates[0];
                _total = 0.0;
                foreach (LoadingStep s in _steps) _total += s.Share;
            }
            Advance(_steps, step, atSeconds);
            if (step == LoadingSteps.Saving && report != LoadingSteps.Saving) _saved++;
        }

        /// <summary>The fraction of the loading done at a time, 0 to 1, never less than it was shown before.</summary>
        public double FractionAt(double nowSeconds)
        {
            IReadOnlyList<LoadingStep> steps = _steps ?? (_candidates.Count > 0 ? _candidates[0] : null);
            if (steps == null || _index < 0) return _shown;
            double total = _steps != null ? _total : Sum(steps);
            if (total <= 0.0) return _shown;
            double share = steps[_index].Share / total;
            double within;
            if (steps[_index].Name == LoadingSteps.Saving)
            {
                within = Math.Min(1.0, (double)_saved / Math.Max(1, LoadingSteps.SavedLayers));
            }
            else
            {
                double spent = _stepStartedAt - _startedAt;
                double pace = _doneBefore > 0.0 ? spent / _doneBefore : double.NaN;
                double expected = share * pace;
                within = expected > 0.0 ? Math.Min(MostOfAStep, Math.Max(0.0, nowSeconds - _stepStartedAt) / expected) : 0.0;
            }
            double fraction = Math.Min(1.0, _doneBefore + share * within);
            if (fraction > _shown) _shown = fraction;
            return _shown;
        }

        /// <summary>
        /// About how many seconds are left at a time, judged from the pace so far; NaN while too little is done to judge
        /// (under <see cref="LeastForAnEstimate"/>).
        /// </summary>
        public double SecondsLeftAt(double nowSeconds)
        {
            double fraction = FractionAt(nowSeconds);
            if (double.IsNaN(_startedAt) || fraction < LeastForAnEstimate) return double.NaN;
            double elapsed = nowSeconds - _startedAt;
            if (elapsed <= 0.0) return double.NaN;
            return elapsed * (1.0 - fraction) / fraction;
        }

        /// <summary>
        /// Each phase of the loading at a time, in the order they run: done with what it took, working with how long it has run
        /// and its step, waiting with what it is expected to take at the pace so far. Empty before the loading's first step.
        /// </summary>
        public IReadOnlyList<LoadingPhase> PhasesAt(double nowSeconds)
        {
            var phases = new List<LoadingPhase>();
            IReadOnlyList<LoadingStep> steps = _steps ?? (_candidates.Count > 0 ? _candidates[0] : null);
            if (steps == null || _index < 0) return phases;
            double total = Sum(steps);
            if (total <= 0.0) return phases;
            double overall = FractionAt(nowSeconds);
            double pace = _doneBefore > 0.0 ? (_stepStartedAt - _startedAt) / _doneBefore : double.NaN;
            int first = 0;
            double before = 0.0;
            while (first < steps.Count)
            {
                string name = steps[first].Phase;
                int last = first;
                double share = steps[first].Share;
                while (last + 1 < steps.Count && steps[last + 1].Phase == name) share += steps[++last].Share;
                double from = before / total, part = share / total;
                if (_index > last)
                {
                    double began = _stepAt.TryGetValue(first, out double b) ? b : _startedAt;
                    double ended = _stepAt.TryGetValue(last + 1, out double e) ? e : nowSeconds;
                    phases.Add(new LoadingPhase(name, LoadingPhaseState.Done, 1.0, ended - began, null));
                }
                else if (_index >= first)
                {
                    double began = _stepAt.TryGetValue(first, out double b) ? b : _startedAt;
                    double within = part > 0.0 ? Math.Min(1.0, Math.Max(0.0, (overall - from) / part)) : 0.0;
                    phases.Add(new LoadingPhase(name, LoadingPhaseState.Working, within, Math.Max(0.0, nowSeconds - began), steps[_index].Name));
                }
                else
                {
                    phases.Add(new LoadingPhase(name, LoadingPhaseState.Waiting, 0.0, part * pace, null));
                }
                before += share;
                first = last + 1;
            }
            return phases;
        }

        private void Advance(IReadOnlyList<LoadingStep> steps, string step, double atSeconds)
        {
            int index = IndexOf(steps, step, Math.Max(0, _index));
            if (index < 0 || index <= _index) return;
            for (int i = Math.Max(0, _index + 1); i <= index; i++) _stepAt[i] = atSeconds;
            double total = Sum(steps);
            double done = 0.0;
            for (int i = 0; i < index; i++) done += steps[i].Share;
            _doneBefore = total > 0.0 ? done / total : 0.0;
            _index = index;
            _stepStartedAt = atSeconds;
            if (_doneBefore > _shown) _shown = _doneBefore;
        }

        private static int IndexOf(IReadOnlyList<LoadingStep> steps, string step, int from)
        {
            for (int i = from; i < steps.Count; i++)
                if (steps[i].Name == step) return i;
            return -1;
        }

        private static double Sum(IReadOnlyList<LoadingStep> steps)
        {
            double sum = 0.0;
            foreach (LoadingStep s in steps) sum += s.Share;
            return sum;
        }
    }
}

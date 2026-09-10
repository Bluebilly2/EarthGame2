using System;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// A frame's allowance for streaming work, in milliseconds (ARCHITECTURE §8: a stopwatch budget, never an
    /// item count). The main thread starts a piece of work while the budget has time left and stops until the
    /// next frame when it does not; a piece already started always finishes, so what one item costs is measured
    /// and reported rather than pretended away.
    ///
    /// <para>Engine-free and told the time rather than reading it, so the arithmetic can be asserted without a
    /// renderer or a clock (M1.4e).</para>
    /// </summary>
    public sealed class FrameBudget
    {
        private readonly Func<double> _nowMs;
        private double _frameStartedMs;
        private bool _open;

        public FrameBudget(double budgetMs, Func<double> nowMs)
        {
            if (!(budgetMs > 0.0)) throw new ArgumentOutOfRangeException(nameof(budgetMs), "a budget is some milliseconds, not " + budgetMs);
            BudgetMs = budgetMs;
            _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
        }

        public double BudgetMs { get; }

        /// <summary>What the frame most recently ended spent, milliseconds; the worst is the caller's to keep.</summary>
        public double LastFrameMs { get; private set; }

        /// <summary>The worst frame since this budget was made, milliseconds.</summary>
        public double WorstFrameMs { get; private set; }

        /// <summary>How many frames have ended having spent anything at all.</summary>
        public int FramesSpent { get; private set; }

        public void BeginFrame()
        {
            _frameStartedMs = _nowMs();
            _open = true;
        }

        /// <summary>
        /// Whether another piece of work may be started this frame, and what the frame had spent when the answer
        /// was yes. The caller records that number rather than reading the clock again at the top of the work:
        /// the first call into a piece of work is compiled on the way in, and 3.26 ms of that was read as the
        /// budget having allowed work on a spent frame (2026-09-10).
        /// </summary>
        public bool TryStart(out double spentMs)
        {
            spentMs = _open ? _nowMs() - _frameStartedMs : LastFrameMs;
            return _open && spentMs < BudgetMs;
        }

        /// <summary>Whether another piece of work may be started this frame.</summary>
        public bool HasTimeLeft => TryStart(out _);

        /// <summary>What has been spent since <see cref="BeginFrame"/>, milliseconds.</summary>
        public double SpentMs => _open ? _nowMs() - _frameStartedMs : LastFrameMs;

        /// <summary>Closes the frame and records what it spent.</summary>
        public double EndFrame()
        {
            if (!_open) return 0.0;
            LastFrameMs = _nowMs() - _frameStartedMs;
            _open = false;
            if (LastFrameMs > 0.0) FramesSpent++;
            if (LastFrameMs > WorstFrameMs) WorstFrameMs = LastFrameMs;
            return LastFrameMs;
        }
    }
}

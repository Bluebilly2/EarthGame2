using System;
using System.Collections.Generic;

namespace EarthGame.Server
{
    /// <summary>What the last window of ticks cost, as the N4 rows want it.</summary>
    public struct TickWindow
    {
        public long Count;
        /// <summary>Ticks whose step took longer than the tick interval.</summary>
        public long OverInterval;
        public double MaxSeconds;
        public double MeanSeconds;
        public double P95Seconds;
    }

    /// <summary>
    /// The server's step durations, measured by the host's clock (the server reads none) and summarised per
    /// window: how many ticks, how many overran the tick interval, the mean, the maximum and the 95th percentile.
    /// N4 asks for ≥ 99.9 % of ticks inside the interval and a p95 of at most 5 ms; the host writes a window to
    /// the run log every minute and this class starts the next one.
    /// </summary>
    public sealed class TickStats
    {
        private readonly double _intervalSeconds;
        private readonly List<double> _durations = new List<double>(1500);
        private double _sum;
        private double _max;
        private long _over;

        public TickStats(double intervalSeconds)
        {
            _intervalSeconds = intervalSeconds;
        }

        public void Record(double seconds)
        {
            _durations.Add(seconds);
            _sum += seconds;
            if (seconds > _max) _max = seconds;
            if (seconds > _intervalSeconds) _over++;
        }

        /// <summary>Summarises the window so far and begins a new one.</summary>
        public TickWindow Snapshot()
        {
            TickWindow w;
            w.Count = _durations.Count;
            w.OverInterval = _over;
            w.MaxSeconds = _max;
            w.MeanSeconds = _durations.Count > 0 ? _sum / _durations.Count : 0.0;
            if (_durations.Count > 0)
            {
                double[] sorted = _durations.ToArray();
                Array.Sort(sorted);
                int index = (int)Math.Ceiling(0.95 * sorted.Length) - 1;
                if (index < 0) index = 0;
                w.P95Seconds = sorted[index];
            }
            else w.P95Seconds = 0.0;
            _durations.Clear();
            _sum = 0.0;
            _max = 0.0;
            _over = 0;
            return w;
        }
    }
}

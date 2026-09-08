using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>One point of a scripted route, and the name of the segment that leads to it.</summary>
    public struct Waypoint
    {
        public double East;
        public double North;
        /// <summary>The segment this point ends; a correction on the way is charged to it (N2's named segments).</summary>
        public string Segment;
        /// <summary>Whether the founder runs on the way here.</summary>
        public bool Sprint;

        public Waypoint(double east, double north, string segment, bool sprint)
        {
            East = east;
            North = north;
            Segment = segment;
            Sprint = sprint;
        }
    }

    /// <summary>
    /// A scenario's legs: turns the founder's position into the facing and gait that reach the next waypoint,
    /// so the client's scripted input seam can be driven without a person. Engine-free and stepped by the
    /// caller, so a test can walk it. A waypoint the founder cannot get nearer to for a stated while is skipped
    /// and counted, so a route blocked by a ledge does not stall a thirty-minute soak.
    /// </summary>
    public sealed class RouteFollower
    {
        private readonly IReadOnlyList<Waypoint> _route;
        private readonly bool _loop;
        private readonly double _reachM;
        private readonly double _stuckSeconds;
        private double _bestDistance = double.MaxValue;
        private double _sinceProgress;

        public RouteFollower(IReadOnlyList<Waypoint> route, bool loop, double reachM = 2.0, double stuckSeconds = 30.0)
        {
            if (route == null || route.Count == 0) throw new ArgumentException("a route needs at least one waypoint", nameof(route));
            _route = route;
            _loop = loop;
            _reachM = reachM;
            _stuckSeconds = stuckSeconds;
        }

        /// <summary>The waypoint being walked to.</summary>
        public int Index { get; private set; }
        public Waypoint Target => _route[Index];
        public string Segment => _route[Index].Segment;
        public int Laps { get; private set; }
        public int Skipped { get; private set; }
        /// <summary>True when a non-looping route has been walked to its last point.</summary>
        public bool Finished { get; private set; }
        public double DistanceToTarget { get; private set; } = double.NaN;

        /// <summary>
        /// One step: where to face (yaw 0 north, 90 east) and whether to run, given where the founder is now and
        /// how long since the last step. Returns false when the route is finished and the founder should stand.
        /// </summary>
        public bool Advance(double east, double north, double dt, out double yawDeg, out bool sprint)
        {
            if (Finished)
            {
                yawDeg = 0.0;
                sprint = false;
                return false;
            }
            Waypoint target = _route[Index];
            double dEast = target.East - east;
            double dNorth = target.North - north;
            double distance = Math.Sqrt(dEast * dEast + dNorth * dNorth);
            DistanceToTarget = distance;
            if (distance <= _reachM)
            {
                Next();
                _bestDistance = double.MaxValue;
                _sinceProgress = 0.0;
                return Advance(east, north, 0.0, out yawDeg, out sprint);
            }
            if (distance < _bestDistance - 0.05)
            {
                _bestDistance = distance;
                _sinceProgress = 0.0;
            }
            else
            {
                _sinceProgress += dt;
                if (_sinceProgress >= _stuckSeconds)
                {
                    Skipped++;
                    Next();
                    _bestDistance = double.MaxValue;
                    _sinceProgress = 0.0;
                    return Advance(east, north, 0.0, out yawDeg, out sprint);
                }
            }
            yawDeg = Math.Atan2(dEast, dNorth) * GeoMath.RadToDeg;
            if (yawDeg < 0.0) yawDeg += 360.0;
            sprint = target.Sprint;
            return true;
        }

        private void Next()
        {
            if (Index + 1 < _route.Count)
            {
                Index++;
                return;
            }
            if (_loop)
            {
                Index = 0;
                Laps++;
                return;
            }
            Finished = true;
        }
    }
}

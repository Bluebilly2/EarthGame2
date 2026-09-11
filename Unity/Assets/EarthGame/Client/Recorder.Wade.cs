using System;
using System.Collections;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>How deep the water the founder is walked towards is at least, m: past the wading depth, so the walk crosses it.</summary>
        private const double WadeTowardsM = 0.6;

        /// <summary>How far from where they stand the founder looks for that water, m.</summary>
        private const double WadeSearchM = 40.0;

        /// <summary>How deep the water the founder is walked towards to swim is at least, m (M1.5e): past the swimming depth, so the walk reaches it.</summary>
        private const double SwimTowardsM = 2.0;

        /// <summary>How far from where they stand the founder looks for water that deep, m.</summary>
        private const double SwimSearchM = 60.0;

        private int _strokesHeard;

        /// <summary>
        /// The wading frames (M1.5d promise 3): the founder, stood on a lake's shore by <c>Tools/world/wade.py</c>, finds the
        /// nearest water deep enough to wade in the depth the server streamed, looks towards it ("shore"), walks into it
        /// until wading and on for two seconds more, and stops ("wading"). The end record says whether they waded, how fast
        /// they went on land and in the water, and the streamed depth where they stopped; the exit is 0 when they waded,
        /// went slower in the water than on land, every frame was written and nothing was logged as an error.
        /// </summary>
        private IEnumerator RunWade()
        {
            if (_client == null || _script == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the wade scenario has no client or no script to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            // The spawn's drop, and the depth tiles, have a moment to land.
            yield return Wait(1.5);
            Double3 feet = _player.State.Feet;
            if (!NearestWater(feet.X, feet.Z, WadeTowardsM, WadeSearchM, out double toEast, out double toNorth))
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no water " + WadeTowardsM + " m deep within " + WadeSearchM + " m of where the founder stands"));
                _running = false;
                Finish(1);
                yield break;
            }
            _script.YawTargetDeg = (float)(Math.Atan2(toEast - feet.X, toNorth - feet.Z) * 180.0 / Math.PI);
            _script.PitchTargetDeg = 12f;
            yield return Wait(1.2);
            yield return Capture("shore");
            _script.Move = new Vector2(0f, 1f);
            _script.Sprint = false;
            double landSum = 0.0, wadeSum = 0.0;
            int landN = 0, wadeN = 0;
            double started = T, until = T + 30.0, wadingFrom = -1.0;
            while (T < until)
            {
                MoverState s = _player.State;
                if (s.Wading)
                {
                    if (wadingFrom < 0.0) wadingFrom = T;
                    // Half a second in, the stride is the water's.
                    if (T > wadingFrom + 0.5)
                    {
                        wadeSum += s.HorizontalSpeed;
                        wadeN++;
                    }
                    if (T > wadingFrom + 2.5) break;
                }
                else if (s.Grounded && T > started + 0.5)
                {
                    landSum += s.HorizontalSpeed;
                    landN++;
                }
                yield return null;
            }
            _script.Move = Vector2.zero;
            yield return Wait(0.8);
            yield return Capture("wading");
            bool waded = wadingFrom >= 0.0;
            double land = landN > 0 ? landSum / landN : 0.0, wade = wadeN > 0 ? wadeSum / wadeN : 0.0;
            bool slower = waded && landN > 0 && wadeN > 0 && wade < 0.75 * land;
            MoverState end = _player.State;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("waded", waded).With("wading", end.Wading).With("land_speed", land).With("wading_speed", wade)
                .With("depth_m", StreamedDepth(end.East, end.North))
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", end.East).With("up", end.Up).With("north", end.North)));
            _running = false;
            Finish(_errors == 0 && waded && slower && _frames == 2 * Sizes.Length ? 0 : 1);
        }

        /// <summary>
        /// The swimming frames (M1.5e promise 4): the founder, stood on a lake's shore by <c>Tools/world/wade.py --swim</c>,
        /// finds the nearest water deep enough to swim in the depth the server streamed, looks towards it ("swim-shore"),
        /// walks into it until swimming, swims on for five seconds and stops ("swimming"). The camera's height over the water
        /// is watched all the way in. The end record says whether they swam, how fast, the lowest the camera came to the
        /// water, the strokes heard and the streamed depth where they stopped; the exit is 0 when they swam, the camera never
        /// went under, every frame was written and nothing was logged as an error.
        /// </summary>
        private IEnumerator RunSwim()
        {
            if (_client == null || _script == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the swim scenario has no client or no script to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            yield return Wait(1.5);
            Double3 feet = _player.State.Feet;
            if (!NearestWater(feet.X, feet.Z, SwimTowardsM, SwimSearchM, out double toEast, out double toNorth))
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no water " + SwimTowardsM + " m deep within " + SwimSearchM + " m of where the founder stands"));
                _running = false;
                Finish(1);
                yield break;
            }
            _script.YawTargetDeg = (float)(Math.Atan2(toEast - feet.X, toNorth - feet.Z) * 180.0 / Math.PI);
            _script.PitchTargetDeg = 8f;
            yield return Wait(1.2);
            yield return Capture("swim-shore");
            _player.Stepped += OnStroke;
            _script.Move = new Vector2(0f, 1f);
            _script.Sprint = false;
            double swimSum = 0.0, lowest = double.PositiveInfinity;
            int swimN = 0;
            double until = T + 60.0, swimmingFrom = -1.0;
            while (T < until)
            {
                MoverState s = _player.State;
                double water = _player.WaterSurface;
                if (!double.IsNaN(water)) lowest = Math.Min(lowest, _camera.transform.position.y - water);
                if (s.Swimming)
                {
                    if (swimmingFrom < 0.0) swimmingFrom = T;
                    // A second in, the pace is the stroke's.
                    if (T > swimmingFrom + 1.0)
                    {
                        swimSum += s.HorizontalSpeed;
                        swimN++;
                    }
                    if (T > swimmingFrom + 5.0) break;
                }
                yield return null;
            }
            _script.Move = Vector2.zero;
            yield return Wait(1.0);
            yield return Capture("swimming");
            _player.Stepped -= OnStroke;
            bool swam = swimmingFrom >= 0.0;
            MoverState end = _player.State;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("swam", swam).With("swimming", end.Swimming).With("swimming_speed", swimN > 0 ? swimSum / swimN : 0.0)
                .With("eye_over_water_min_m", double.IsInfinity(lowest) ? double.NaN : lowest).With("strokes", _strokesHeard)
                .With("depth_m", StreamedDepth(end.East, end.North))
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", end.East).With("up", end.Up).With("north", end.North)));
            _running = false;
            Finish(_errors == 0 && swam && lowest > 0.0 && _frames == 2 * Sizes.Length ? 0 : 1);
        }

        private void OnStroke(Footfall footfall)
        {
            if (footfall.Stroke) _strokesHeard++;
        }

        /// <summary>The depth the server streamed at a point, m; NaN where its tile is not held.</summary>
        private double StreamedDepth(double east, double north)
        {
            ReceivedTile tile = _client.Grid != null ? _client.Tiles.Holding(TileLayer.WaterDepth, _client.Grid.ForPosition(east, north)) : null;
            return tile?.Heights != null ? TileGround.HeightAt(tile, east, north) : double.NaN;
        }

        /// <summary>The nearest point, out to a distance, where the streamed water is at least a depth deep.</summary>
        private bool NearestWater(double east, double north, double depthM, double searchM, out double atEast, out double atNorth)
        {
            for (double r = 1.0; r <= searchM; r += 1.0)
                for (int k = 0; k < 72; k++)
                {
                    double a = k * Math.PI / 36.0;
                    double e = east + r * Math.Sin(a), n = north + r * Math.Cos(a);
                    if (StreamedDepth(e, n) >= depthM)
                    {
                        atEast = e;
                        atNorth = n;
                        return true;
                    }
                }
            atEast = 0.0;
            atNorth = 0.0;
            return false;
        }
    }
}

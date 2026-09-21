using System;
using System.Collections;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>The dune (M1.5h, CANON ruling 34): see <see cref="RunDune"/>.</summary>
        public const string DuneScenario = "dune";

        /// <summary>How far from where the founder stands a face is looked for, m, and the cell the ground is sampled at.</summary>
        private const double DuneSearchM = 150.0, DuneCellM = 4.0;

        /// <summary>The slopes a face must have to be the dune: steep enough to have crawled a founder under Tobler, and walked at all.</summary>
        private const double DuneLeastDeg = 18.0, DuneMostDeg = 34.0;

        /// <summary>How long the founder walks down the face and up it, s, and how long the walk-to-the-top may take.</summary>
        private const double DuneWalkS = 4.0, DuneApproachS = 90.0;

        /// <summary>
        /// The dune (M1.5h, CANON ruling 34: "when i walk down a steep hill ... the player just slows right down"). The steepest
        /// face of <see cref="DuneLeastDeg"/> to <see cref="DuneMostDeg"/> within <see cref="DuneSearchM"/> of where the founder
        /// stands is found on the client's own ground; the founder is walked by script to a point above it, then straight down
        /// the fall line for <see cref="DuneWalkS"/> seconds and straight back up, and the slope walked and the speed held (past
        /// the first second, which the acceleration owns) are written beside each other. The exit is 0 when such a face was
        /// found and walked, the descent went faster than half a metre a second and slower than the flat walk, and the ascent
        /// slower than the descent: the number he felt, written where he can read it.
        /// </summary>
        private IEnumerator RunDune()
        {
            if (_player == null || _script == null || _player.Ground == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the dune scenario has no scripted founder or no ground to read"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            yield return Wait(1.5);

            // The face: the ground sampled on a grid round the feet, the slope at each cell from its neighbours, the nearest
            // cell steep enough taken, and among those within ten metres of it the steepest.
            Double3 feet = _player.State.Feet;
            int cells = (int)(DuneSearchM / DuneCellM);
            double bestDeg = 0.0, bestE = 0.0, bestN = 0.0, bestDist = double.MaxValue, downE = 0.0, downN = 0.0;
            for (int i = -cells; i <= cells; i++)
            {
                for (int j = -cells; j <= cells; j++)
                {
                    double e = feet.X + i * DuneCellM, n = feet.Z + j * DuneCellM;
                    double dist = Math.Sqrt((e - feet.X) * (e - feet.X) + (n - feet.Z) * (n - feet.Z));
                    if (dist > DuneSearchM) continue;
                    double ge = (_player.Ground.HeightAt(e + DuneCellM, n) - _player.Ground.HeightAt(e - DuneCellM, n)) / (2.0 * DuneCellM);
                    double gn = (_player.Ground.HeightAt(e, n + DuneCellM) - _player.Ground.HeightAt(e, n - DuneCellM)) / (2.0 * DuneCellM);
                    double deg = Math.Atan(Math.Sqrt(ge * ge + gn * gn)) * 180.0 / Math.PI;
                    if (deg < DuneLeastDeg || deg > DuneMostDeg) continue;
                    bool nearer = dist < bestDist - 10.0, steeperNearby = dist <= bestDist + 10.0 && deg > bestDeg;
                    if (bestDist == double.MaxValue || nearer || steeperNearby)
                    {
                        bestDeg = deg;
                        bestE = e;
                        bestN = n;
                        bestDist = dist;
                        double g = Math.Sqrt(ge * ge + gn * gn);
                        downE = -ge / g;
                        downN = -gn / g;
                    }
                }
            }
            bool found = bestDist < double.MaxValue;
            _log.Record(T, Tick, "dune", new JsonObject().With("found", found).With("face_deg", bestDeg).With("east", bestE).With("north", bestN).With("from_m", found ? bestDist : 0.0));
            if (!found)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no face of " + DuneLeastDeg + " to " + DuneMostDeg + " degrees within " + DuneSearchM + " m of where the founder stands"));
                _running = false;
                Finish(1);
                yield break;
            }

            // To the top: five metres up the fall line from the face's cell, walked by script.
            double topE = bestE - downE * 5.0, topN = bestN - downN * 5.0;
            yield return WalkTo(topE, topN, DuneApproachS);
            yield return Wait(1.0);
            yield return Capture("dune-top");

            // Down the fall line, the slope walked and the speed held past the first second.
            double downDeg, downMs;
            yield return WalkAlong(downE, downN, DuneWalkS);
            downDeg = _walkedDeg;
            downMs = _walkedMs;
            _log.Record(T, Tick, "dune_down", new JsonObject().With("slope_deg", downDeg).With("speed_ms", downMs).With("moved_m", _walkedM));
            yield return Wait(1.0);
            yield return Capture("dune-bottom");

            // And back up it.
            double upDeg, upMs;
            yield return WalkAlong(-downE, -downN, DuneWalkS);
            upDeg = _walkedDeg;
            upMs = _walkedMs;
            _log.Record(T, Tick, "dune_up", new JsonObject().With("slope_deg", upDeg).With("speed_ms", upMs).With("moved_m", _walkedM));

            double flat = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0);
            bool walkedDown = downDeg >= 15.0 && downMs > 0.5 && downMs < flat;
            bool slowerUp = upMs < downMs;
            _log.Record(T, Tick, "end", new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("found", found).With("face_deg", bestDeg).With("slope_down_deg", downDeg).With("speed_down_ms", downMs)
                .With("slope_up_deg", upDeg).With("speed_up_ms", upMs).With("flat_ms", flat)
                .With("walked_down", walkedDown).With("slower_up", slowerUp));
            _running = false;
            Finish(_errors == 0 && walkedDown && slowerUp && _frames == 2 * Sizes.Length ? 0 : 1);
        }

        private double _walkedDeg, _walkedMs, _walkedM;

        /// <summary>The founder walked by script toward a point until within a stride of it or the time is up.</summary>
        private IEnumerator WalkTo(double toE, double toN, double mostS)
        {
            double until = T + mostS;
            while (T < until)
            {
                Double3 f = _player.State.Feet;
                double dE = toE - f.X, dN = toN - f.Z;
                if (Math.Sqrt(dE * dE + dN * dN) < 1.5) break;
                _script.YawTargetDeg = (float)(Math.Atan2(dE, dN) * 180.0 / Math.PI);
                _script.PitchTargetDeg = 8f;
                _script.Move = new Vector2(0f, 1f);
                _script.Sprint = false;
                yield return null;
            }
            _script.Move = Vector2.zero;
        }

        /// <summary>
        /// The founder walked by script along a direction for some seconds: the slope walked (from the ground under the feet at
        /// the start and the end) and the speed held after the first 1.2 s, which the acceleration owns, are left in
        /// <see cref="_walkedDeg"/>, <see cref="_walkedMs"/> and <see cref="_walkedM"/>.
        /// </summary>
        private IEnumerator WalkAlong(double dirE, double dirN, double seconds)
        {
            Double3 start = _player.State.Feet;
            _script.YawTargetDeg = (float)(Math.Atan2(dirE, dirN) * 180.0 / Math.PI);
            _script.PitchTargetDeg = 10f;
            yield return Wait(0.8);
            _script.Move = new Vector2(0f, 1f);
            _script.Sprint = false;
            double began = T, sum = 0.0;
            int n = 0;
            while (T < began + seconds)
            {
                MoverState s = _player.State;
                if (s.Grounded && T > began + 1.2)
                {
                    sum += s.HorizontalSpeed;
                    n++;
                }
                yield return null;
            }
            _script.Move = Vector2.zero;
            Double3 end = _player.State.Feet;
            double dE = end.X - start.X, dN = end.Z - start.Z;
            double across = Math.Sqrt(dE * dE + dN * dN);
            double rise = _player.Ground.HeightAt(end.X, end.Z) - _player.Ground.HeightAt(start.X, start.Z);
            _walkedM = across;
            _walkedDeg = across > 0.5 ? Math.Abs(Math.Atan2(rise, across)) * 180.0 / Math.PI : 0.0;
            _walkedMs = n > 0 ? sum / n : 0.0;
        }
    }
}

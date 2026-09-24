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

        /// <summary>How far a face too steep to stand on is looked for, m, and the steepest taken: past it is a cliff, not a slide.</summary>
        private const double SlideSearchM = 300.0, SlideMostDeg = 60.0;

        /// <summary>How long the founder walks down the face and up it, s, and how long the walk-to-the-top may take.</summary>
        private const double DuneWalkS = 4.0, DuneApproachS = 90.0;

        /// <summary>How long the founder walks into the face too steep to stand on, s: long enough to reach it from three metres below and be on it.</summary>
        private const double SlideWalkS = 6.0;

        /// <summary>How far under the ground a sliding body's feet may be read before it counts as through the world, m.</summary>
        private const double SlideThroughM = 0.05;

        /// <summary>
        /// The dune (M1.5h, CANON ruling 34: "when i walk down a steep hill ... the player just slows right down"). The steepest
        /// face of <see cref="DuneLeastDeg"/> to <see cref="DuneMostDeg"/> within <see cref="DuneSearchM"/> of where the founder
        /// stands is found on the client's own ground; the founder is walked by script to a point above it, then straight down
        /// the fall line for <see cref="DuneWalkS"/> seconds and straight back up, and the slope walked and the speed held (past
        /// the first second, which the acceleration owns) are written beside each other. Then (M1.5i, the evening William fell
        /// through the world) the steepest face too steep to stand on within <see cref="SlideSearchM"/> is walked into from below
        /// for the same seconds, and the lowest the feet were read below the ground is written: a sliding body keeps its feet on
        /// the surface. The exit is 0 when the dune was found and walked, the descent went faster than half a metre a second
        /// and slower than the flat walk, the ascent slower than the descent, and no face walked into took the founder under.
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

            Double3 feet = _player.State.Feet;
            // -eg-dune slide: the slide part alone, for a founder the tool has stood at the foot of a face over the limit
            // (M1.5i's owed proof); the dune's own parts are then not looked for and not gated on.
            bool slideOnly = LaunchArgs.Get("dune", "all") == "slide";
            SteepFace dune = slideOnly ? default : FindSteepFace(feet, DuneSearchM, DuneLeastDeg, DuneMostDeg);
            _log.Record(T, Tick, "dune", new JsonObject().With("found", dune.Found).With("face_deg", dune.Deg).With("east", dune.East).With("north", dune.North).With("from_m", dune.FromM));
            if (!slideOnly && !dune.Found)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no face of " + DuneLeastDeg + " to " + DuneMostDeg + " degrees within " + DuneSearchM + " m of where the founder stands"));
                _running = false;
                Finish(1);
                yield break;
            }

            double downDeg = 0.0, downMs = 0.0, upDeg = 0.0, upMs = 0.0;
            if (!slideOnly)
            {
                // To the top: five metres up the fall line from the face's cell, walked by script.
                yield return WalkTo(dune.East - dune.DownE * 5.0, dune.North - dune.DownN * 5.0, DuneApproachS);
                yield return Wait(1.0);
                yield return Capture("dune-top");

                // Down the fall line, the slope walked and the speed held past the first second; then back up it.
                yield return WalkAlong(dune.DownE, dune.DownN, DuneWalkS);
                downDeg = _walkedDeg; downMs = _walkedMs;
                _log.Record(T, Tick, "dune_down", new JsonObject().With("slope_deg", downDeg).With("speed_ms", downMs).With("moved_m", _walkedM));
                yield return Wait(1.0);
                yield return Capture("dune-bottom");
                yield return WalkAlong(-dune.DownE, -dune.DownN, DuneWalkS);
                upDeg = _walkedDeg; upMs = _walkedMs;
                _log.Record(T, Tick, "dune_up", new JsonObject().With("slope_deg", upDeg).With("speed_ms", upMs).With("moved_m", _walkedM));
            }

            // The slide (M1.5i): the steepest face too steep to stand on, walked into from six metres below its cell; the feet
            // read against the ground every frame.
            double limit = MoverConfig.Default.WalkableSlopeDeg;
            // Any face past the limit is a slide to the mover, so the margin is a hair: the gate world's steepest faces near the wake are 35.7°.
            SteepFace slide = FindSteepFace(_player.State.Feet, SlideSearchM, limit + 0.05, SlideMostDeg);
            double lowestUnder = 0.0, steepestUnder = 0.0, reachedM = 0.0;
            if (slide.Found)
            {
                double footE = slide.East + slide.DownE * 3.0, footN = slide.North + slide.DownN * 3.0;
                yield return WalkTo(footE, footN, DuneApproachS);
                // How far short of the face's foot the walk stopped: a founder walked by script goes straight, and a trunk can hold them.
                reachedM = Math.Sqrt((footE - _player.State.East) * (footE - _player.State.East) + (footN - _player.State.North) * (footN - _player.State.North));
                yield return Wait(0.5);
                yield return WalkAlong(-slide.DownE, -slide.DownN, SlideWalkS, under =>
                {
                    lowestUnder = Math.Min(lowestUnder, under);
                    steepestUnder = Math.Max(steepestUnder, SlopeUnderFeetDeg());
                });
            }
            bool slideOk = !slide.Found || lowestUnder > -SlideThroughM;
            _log.Record(T, Tick, "slide", new JsonObject().With("found", slide.Found).With("face_deg", slide.Deg).With("east", slide.East).With("north", slide.North)
                .With("from_m", slide.FromM).With("lowest_under_m", lowestUnder).With("kept_feet", slideOk).With("walked_into_deg", _walkedDeg)
                .With("steepest_under_deg", steepestUnder).With("short_of_foot_m", reachedM));

            // The face's own ground sets the pace (BF.4 stage two): a dune's loose sand is walked at three-quarters of the table's
            // pace, so the descent's bounds, faster than half a metre a second and slower than the flat, are its ground's.
            GroundType ground = dune.Found && _player.UnderfootAt != null ? _player.UnderfootAt(dune.East, dune.North) : Locomotion.TableGround;
            double pace = Locomotion.GroundPace(ground);
            double flat = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0, ground);
            bool walkedDown = slideOnly || (downDeg >= 15.0 && downMs > 0.5 * pace && downMs < flat);
            bool slowerUp = slideOnly || upMs < downMs;
            _log.Record(T, Tick, "end", new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("found", dune.Found).With("face_deg", dune.Deg).With("slope_down_deg", downDeg).With("speed_down_ms", downMs)
                .With("slope_up_deg", upDeg).With("speed_up_ms", upMs).With("flat_ms", flat).With("ground", ground.ToString()).With("pace", pace)
                .With("walked_down", walkedDown).With("slower_up", slowerUp)
                .With("slide_found", slide.Found).With("slide_face_deg", slide.Deg).With("slide_lowest_under_m", lowestUnder).With("slide_kept_feet", slideOk)
                .With("slide_steepest_under_deg", steepestUnder));
            _running = false;
            Finish(_errors == 0 && walkedDown && slowerUp && slideOk && (slideOnly || _frames == 2 * Sizes.Length) ? 0 : 1);
        }

        /// <summary>A steep face found on the client's ground: its steepness, its cell, how far off it is, and the way down it.</summary>
        private struct SteepFace
        {
            public bool Found;
            public double Deg, East, North, FromM, DownE, DownN;
        }

        /// <summary>
        /// The ground sampled on a grid round the feet, the slope at each cell from its neighbours, the nearest cell within the
        /// slopes asked for taken, and among those within ten metres of it the steepest.
        /// </summary>
        private SteepFace FindSteepFace(Double3 feet, double withinM, double leastDeg, double mostDeg)
        {
            SteepFace best = new SteepFace { FromM = double.MaxValue };
            int cells = (int)(withinM / DuneCellM);
            for (int i = -cells; i <= cells; i++)
            {
                for (int j = -cells; j <= cells; j++)
                {
                    double e = feet.X + i * DuneCellM, n = feet.Z + j * DuneCellM;
                    double dist = Math.Sqrt((e - feet.X) * (e - feet.X) + (n - feet.Z) * (n - feet.Z));
                    if (dist > withinM) continue;
                    double ge = (_player.Ground.HeightAt(e + DuneCellM, n) - _player.Ground.HeightAt(e - DuneCellM, n)) / (2.0 * DuneCellM);
                    double gn = (_player.Ground.HeightAt(e, n + DuneCellM) - _player.Ground.HeightAt(e, n - DuneCellM)) / (2.0 * DuneCellM);
                    double g = Math.Sqrt(ge * ge + gn * gn);
                    double deg = Math.Atan(g) * 180.0 / Math.PI;
                    if (deg < leastDeg || deg > mostDeg) continue;
                    bool nearer = dist < best.FromM - 10.0, steeperNearby = dist <= best.FromM + 10.0 && deg > best.Deg;
                    if (!best.Found || nearer || steeperNearby)
                        best = new SteepFace { Found = true, Deg = deg, East = e, North = n, FromM = dist, DownE = -ge / g, DownN = -gn / g };
                }
            }
            if (!best.Found) best.FromM = 0.0;
            return best;
        }

        private double _walkedDeg, _walkedMs, _walkedM;

        /// <summary>The ground's slope under the feet, degrees, from the client's own ground a metre either way.</summary>
        private double SlopeUnderFeetDeg()
        {
            MoverState s = _player.State;
            double ge = (_player.Ground.HeightAt(s.East + 1.0, s.North) - _player.Ground.HeightAt(s.East - 1.0, s.North)) / 2.0;
            double gn = (_player.Ground.HeightAt(s.East, s.North + 1.0) - _player.Ground.HeightAt(s.East, s.North - 1.0)) / 2.0;
            return Math.Atan(Math.Sqrt(ge * ge + gn * gn)) * 180.0 / Math.PI;
        }

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
        /// <see cref="_walkedDeg"/>, <see cref="_walkedMs"/> and <see cref="_walkedM"/>; each frame the feet's height over the
        /// ground under them is handed to <paramref name="underground"/> when one is given.
        /// </summary>
        private IEnumerator WalkAlong(double dirE, double dirN, double seconds, Action<double> underground = null)
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
                underground?.Invoke(s.Up - _player.Ground.HeightAt(s.East, s.North));
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

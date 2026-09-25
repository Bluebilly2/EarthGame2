using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>
        /// The rocks (BF.4 stage three), run by <c>-eg-scenario rocks</c> with the rock (<c>-eg-rock east,north</c>) and what is done
        /// with it (<c>-eg-rock-walk stop</c> or <c>on</c>): the founder walks at a boulder and is stopped by it, or stands on one.
        /// </summary>
        public const string RocksScenario = "rocks";

        /// <summary>How long the founder must come no nearer for a walk to be over, s.</summary>
        private const double RockHeldS = 1.5;

        /// <summary>
        /// The rocks (BF.4 stage three), each on a rock the tool chose from the server's own rule and the client found among the
        /// rocks it placed. <c>stop</c>: stood by the host six metres off, the founder looks at the rock ("rock-ahead"), walks
        /// straight at it and is stopped or glances off its side ("rock"); the end record says how near the body's middle came to
        /// its outline, against the body's radius: never further in than the physics' fit to the rock allows. <c>on</c>: stood by
        /// the host on the rock itself, where the server's surface is its top,
        /// the founder stands there ("rock-ahead", "rock-on"), their feet on the rock's surface as the engine gives it and the
        /// server judges it, and is not corrected. A rock the client did not place where the tool said fails the run.
        ///
        /// <para>A founder does not step onto a knee-high boulder: the step is taken only where the round foot lands flat, and a
        /// rock's rim is steep; a jump from a standstill carried the founder half a metre up and not far enough forward
        /// (2026-09-25). How that feels is William's (DEBTS).</para>
        /// </summary>
        private IEnumerator RunRocks()
        {
            StandViews stand = GetComponent<ClientRuntime>()?.Stand;
            if (_client == null || _script == null || stand == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the rocks scenario has no client, no script or no stand views"));
                _running = false;
                Finish(1);
                yield break;
            }
            string[] at = (LaunchArgs.Get("rock", "") ?? "").Split(',');
            string walk = LaunchArgs.Get("rock-walk", "stop");
            if (at.Length != 2 || !double.TryParse(at[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double rockEast)
                || !double.TryParse(at[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double rockNorth) || (walk != "stop" && walk != "on"))
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the rocks scenario wants -eg-rock east,north and -eg-rock-walk stop or on"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            // The spawn's drop, and the stand tiles and their rocks, have a moment to land.
            yield return Wait(1.5);
            List<StandingRock> near = new List<StandingRock>();
            StandingRock rock = default;
            bool found = false;
            for (double searchUntil = T + 10.0; T < searchUntil && !found; )
            {
                near.Clear();
                stand.RocksNear(rockEast, rockNorth, 2.0, near);
                foreach (StandingRock r in near)
                    if (Math.Abs(r.East - rockEast) < 0.05 && Math.Abs(r.North - rockNorth) < 0.05)
                    {
                        rock = r;
                        found = true;
                    }
                if (!found) yield return Wait(0.5);
            }
            if (!found)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no rock placed at east " + rockEast + " north " + rockNorth + "; " + near.Count + " within 2 m"));
                _running = false;
                Finish(1);
                yield break;
            }
            Double3 feet = _player.State.Feet;
            bool standing = walk == "on";
            // Walking at the rock, the founder faces it; stood on it, they look out across it.
            _script.YawTargetDeg = standing ? 0f : (float)(Math.Atan2(rock.East - feet.X, rock.North - feet.Z) * 180.0 / Math.PI);
            _script.PitchTargetDeg = standing ? 35f : 10f;
            yield return Wait(1.4);
            yield return Capture("rock-ahead");
            _script.Move = standing ? Vector2.zero : new Vector2(0f, 1f);
            _script.Sprint = false;
            double gap = double.MaxValue, held = 0.0, last = T, until = T + (standing ? 3.0 : 25.0), highest = _player.State.Up, nearestOutline = double.MaxValue;
            // The body's physics is the rock's smooth shape cut into coarse faces, which lie inside it by the faces' sag, up to a
            // few centimetres (StandMeshes.RockBody), and the mover keeps a skin off what it touches; a foot within this of the rock
            // is touching it, not in it.
            const double SagM = 0.08;
            double deepest = double.NegativeInfinity, nextTrace = T;
            while (T < until)
            {
                double step = T - last;
                last = T;
                MoverState s = _player.State;
                double dx = s.East - rock.East, dz = s.North - rock.North;
                double away = Math.Sqrt(dx * dx + dz * dz);
                highest = Math.Max(highest, s.Up);
                // How near the body's middle came to the rock's outline, and, wherever it stood over the rock, how far its feet
                // were below the rock's surface there: a body stopped by the rock, glancing off its rounded side or stepping onto a
                // shoulder low on the uphill side never has its feet inside the rock (2026-09-25: the first walk slid round a
                // boulder on a slope, and the second stepped onto its low shoulder, both of which a rock may let a founder do).
                rock.ToLocal(s.East, s.North, out double a, out double c);
                double d = Math.Sqrt(a * a + c * c), m = rock.OutlineAt(a, c);
                if (m > 0.0) nearestOutline = Math.Min(nearestOutline, d - d / m);
                double overlap = Overlap(rock, s.East, s.North, s.Up, MoverConfig.Default.CapsuleRadius);
                deepest = Math.Max(deepest, overlap);
                // The walk's trace, a tenth of a second at a time: where the body is against the rock.
                if (T >= nextTrace)
                {
                    nextTrace = T + 0.1;
                    JsonObject trace = new JsonObject().With("east", s.East).With("north", s.North).With("up", s.Up).With("grounded", s.Grounded)
                        .With("from_middle_m", d).With("bodies", GetComponent<ClientRuntime>()?.RockBodiesStanding ?? 0);
                    if (!double.IsInfinity(overlap)) trace.With("overlap_m", overlap);
                    _log.Record(T, Tick, "rock-walk", trace);
                }
                if (!standing)
                {
                    if (away < gap - 0.01)
                    {
                        gap = away;
                        held = 0.0;
                    }
                    else held += step;
                    if (gap < double.MaxValue && held > RockHeldS) break;
                }
                yield return null;
            }
            _script.Move = Vector2.zero;
            yield return Wait(standing ? 0.3 : 0.6);
            yield return Capture(standing ? "rock-on" : "rock");

            MoverState end = _player.State;
            double ground = Fine != null ? Fine.HeightAt(end.East, end.North) : double.NaN;
            bool onTop = rock.TryTopAt(end.East, end.North, out double top);
            // How far the founder's middle stopped from the rock's outline, along the line they walked in on.
            rock.ToLocal(end.East, end.North, out double along, out double across);
            double fromMiddle = Math.Sqrt(along * along + across * across), measure = rock.OutlineAt(along, across);
            double fromOutline = measure > 0.0 ? fromMiddle - fromMiddle / measure : double.NaN;
            double body = MoverConfig.Default.CapsuleRadius;
            // At a tall rock the body met it (came within a stride of touching) and never came further inside its outline than the
            // physics' fit to the rock allows; stood on one, it stands on the top, above the ground there, and stays with its round
            // foot out of the rock. (Against a blocky rock's side the round foot's measure is no guide: its top climbs so steeply just
            // inside the rim that a body a few centimetres in reads as deep in, 0.17 m on the walk of 2026-09-25 whose body came
            // 0.05 m inside the outline.)
            bool met = nearestOutline <= body + 0.3;
            bool neverInside = standing ? deepest <= SagM : nearestOutline >= body - SagM;
            bool ok = !standing
                ? met && neverInside
                : onTop && end.Grounded && Math.Abs(end.Up - top) <= 0.12 && end.Up > ground + 0.08 && neverInside;
            JsonObject record = new JsonObject().With("frames", _frames).With("errors", _errors).With("walk", walk).With("ok", ok)
                .With("rock_east", rock.East).With("rock_north", rock.North).With("form", rock.Form.ToString()).With("stone", rock.StoneType?.Name ?? "")
                .With("half_length_m", rock.HalfLength).With("half_width_m", rock.HalfWidth).With("half_height_m", rock.HalfHeight)
                .With("rock_top_m", rock.TopUp).With("body_radius_m", body).With("on_top", onTop).With("highest_m", highest)
                .With("rocks_near", near.Count).With("bodies", GetComponent<ClientRuntime>()?.RockBodiesStanding ?? 0)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", end.East).With("up", end.Up).With("north", end.North).With("grounded", end.Grounded);
            // A record never carries a NaN (FP.2's trap): what could not be measured is left out.
            if (!double.IsNaN(fromOutline)) record.With("from_outline_m", fromOutline);
            if (nearestOutline < double.MaxValue) record.With("nearest_outline_m", nearestOutline);
            record.With("met", met).With("never_inside", neverInside);
            if (!double.IsInfinity(deepest)) record.With("deepest_overlap_m", deepest);
            if (onTop) record.With("top_under_feet_m", top);
            if (!double.IsNaN(ground)) record.With("ground_m", ground);
            _log.Record(T, Tick, "end", WithFeet(record));
            _running = false;
            Finish(_errors == 0 && ok && _player.Corrections == 0 && _frames == 2 * Sizes.Length ? 0 : 1);
        }

        /// <summary>
        /// How far a rock's top reaches into a body standing at a point, m, negative where it stays clear: the most, over points
        /// of the body's footprint, that the rock's surface stands above the underside of the body's rounded foot (a hemisphere of
        /// the body's radius, its lowest point at the feet) and, at the footprint's edge, above its middle. A body pressed against
        /// a rock's sloping face has its feet below the rock's surface under its middle without being in it (2026-09-25, the
        /// second walk's measure), so what is measured is the round foot against the rock, not the feet against the surface.
        /// </summary>
        private static double Overlap(in StandingRock rock, double east, double north, double feet, double radius)
        {
            double most = double.NegativeInfinity;
            for (int i = -4; i <= 4; i++)
                for (int j = -4; j <= 4; j++)
                {
                    double ox = radius * i / 4.0, oz = radius * j / 4.0, rho2 = ox * ox + oz * oz;
                    if (rho2 > radius * radius || !rock.TryTopAt(east + ox, north + oz, out double top)) continue;
                    double underside = feet + radius - Math.Sqrt(radius * radius - rho2);
                    most = Math.Max(most, top - underside);
                }
            return most;
        }
    }
}

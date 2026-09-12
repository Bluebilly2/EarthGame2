using System;
using System.Collections;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>How far off the founder looks for a tree to walk into, m.</summary>
        private const double TrunkSearchM = 30.0;

        /// <summary>How far off the tree walked at stands, m: far enough to walk up to, near enough that the ground between is plain.</summary>
        private const double TrunkNearestM = 4.0, TrunkFurthestM = 25.0;

        /// <summary>How far from the walk every other trunk stands, m, so that what stops the founder is the tree they aimed at.</summary>
        private const double TrunkClearM = 2.0;

        /// <summary>How long the founder must come no nearer for the walk to be over, s.</summary>
        private const double TrunkHeldS = 1.5;

        /// <summary>
        /// The trunk frames (M1.6b promise 5): the founder finds the nearest tree the client holds with a clear walk to it,
        /// looks at it ("trunk-ahead"), walks straight at it until they come no nearer, and stops ("trunk"). The end record
        /// says how near the trunk's middle they came, against the bark — the radius the tree is drawn with plus the body's
        /// own — so a founder who walked through the tree, or stopped a stride short of it, fails the run rather than
        /// leaving a picture nobody reads.
        /// </summary>
        private IEnumerator RunTrunk()
        {
            if (_client == null || _script == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the trunk scenario has no client or no script to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            // The spawn's drop, and the stand tiles, have a moment to land.
            yield return Wait(1.5);
            Double3 feet = _player.State.Feet;
            List<TrunkNearby> near = new List<TrunkNearby>();
            TrunksNear.Find(feet.X, feet.Z, TrunkSearchM, TrunkBodies.MeetsAtM, _client.Tiles, _client.Grid, near);
            TrunkNearby target = default;
            double best = double.MaxValue;
            foreach (TrunkNearby trunk in near)
            {
                double dx = trunk.East - feet.X, dz = trunk.North - feet.Z;
                double away = Math.Sqrt(dx * dx + dz * dz);
                if (away < TrunkNearestM || away > TrunkFurthestM || away >= best) continue;
                if (!ClearWalk(feet, trunk, near)) continue;
                best = away;
                target = trunk;
            }
            if (best > TrunkFurthestM)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message",
                    "no tree with a clear walk to it stands " + TrunkNearestM + " to " + TrunkFurthestM + " m from where the founder woke; "
                    + near.Count + " stand within " + TrunkSearchM + " m"));
                _running = false;
                Finish(1);
                yield break;
            }

            _script.YawTargetDeg = (float)(Math.Atan2(target.East - feet.X, target.North - feet.Z) * 180.0 / Math.PI);
            _script.PitchTargetDeg = 6f;
            yield return Wait(1.4);
            yield return Capture("trunk-ahead");
            _script.Move = new Vector2(0f, 1f);
            _script.Sprint = false;
            double bark = target.RadiusM + MoverConfig.Default.CapsuleRadius;
            double gap = double.MaxValue, held = 0.0, last = T, until = T + 25.0;
            while (T < until)
            {
                double step = T - last;
                last = T;
                MoverState s = _player.State;
                double dx = s.East - target.East, dz = s.North - target.North;
                double away = Math.Sqrt(dx * dx + dz * dz);
                if (away < gap - 0.01)
                {
                    gap = away;
                    held = 0.0;
                }
                else held += step;
                if (gap < double.MaxValue && held > TrunkHeldS) break;
                yield return null;
            }
            _script.Move = Vector2.zero;
            yield return Wait(0.6);
            yield return Capture("trunk");

            bool stopped = held > TrunkHeldS && gap < TrunkFurthestM;
            bool atTheBark = stopped && Math.Abs(gap - bark) <= 0.25;
            MoverState end = _player.State;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("stopped", stopped).With("at_the_bark", atTheBark).With("gap_m", gap).With("bark_m", bark)
                .With("trunk_radius_m", target.RadiusM).With("tree_height_m", target.HeightM)
                .With("trunk_east", target.East).With("trunk_north", target.North).With("trunks_near", near.Count)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", end.East).With("up", end.Up).With("north", end.North)));
            _running = false;
            Finish(_errors == 0 && atTheBark && _frames == 2 * Sizes.Length ? 0 : 1);
        }

        /// <summary>Whether the straight walk to a trunk passes wide of every other, so that the tree aimed at is the one that stops the founder.</summary>
        private static bool ClearWalk(Double3 from, TrunkNearby target, List<TrunkNearby> near)
        {
            double dx = target.East - from.X, dz = target.North - from.Z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (!(length > 0.0)) return false;
            dx /= length;
            dz /= length;
            foreach (TrunkNearby other in near)
            {
                if (Math.Abs(other.East - target.East) < 1e-6 && Math.Abs(other.North - target.North) < 1e-6) continue;
                double ox = other.East - from.X, oz = other.North - from.Z;
                double along = ox * dx + oz * dz;
                if (along <= 0.0 || along >= length) continue;
                if (Math.Abs(ox * dz - oz * dx) < other.RadiusM + TrunkClearM) return false;
            }
            return true;
        }
    }
}

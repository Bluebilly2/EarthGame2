using System;
using System.Collections;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>The animals' looks (M1.7b): see <see cref="RunLooks"/>.</summary>
        public const string LooksScenario = "looks";

        /// <summary>The local hour the frames are taken at: mid-morning, so the sun is well up and the shadows are short enough to read a shape by.</summary>
        private const double LooksHour = 10.0;

        /// <summary>How far off each animal is looked at, m: near enough to read its pieces, and far enough to read its silhouette.</summary>
        private const double LooksNearM = 3.0, LooksFarM = 8.0;

        /// <summary>Where the eye is aimed on each animal, m above its feet: the flank of a kangaroo, the back of a bird, and a bird that has gone up.</summary>
        private const double LooksRooUpM = 0.7, LooksBirdUpM = 0.25, LooksFlyingUpM = 1.4;

        /// <summary>The longest a set-down animal is waited for, and the longest the founder backs away from one, s.</summary>
        private const double LooksArriveSeconds = 6.0, LooksBackSeconds = 30.0;

        /// <summary>How many frames each capture makes, and how many captures the scenario makes.</summary>
        private const int LooksCaptures = 6;

        private EntityView _looksAnimal;

        /// <summary>
        /// The animals' looks (M1.7b, CANON ruling 29). In a development game the clock is set to ten in the morning; one
        /// kangaroo and then one oystercatcher are set down two metres ahead by the developer's panel's own deeds; the
        /// founder backs off to about three metres and then about eight from each, looking at it, and a frame is taken at
        /// each distance ("roo-near", "roo-far", "bird-near", "bird-far"); then the animals set down by hand are put into
        /// the fleeing pose, and the bird in the air and the kangaroo mid-hop are each looked at ("bird-flying",
        /// "roo-fleeing"). Every capture writes a <c>looks</c> record with what was looked at, its pose and how far off it
        /// stood. The exit is 0 when both animals arrived, every frame was written and nothing was logged as an error.
        ///
        /// <para>The frames themselves are nobody's verdict here: whether a kangaroo looks like a kangaroo is William's
        /// (STANDARDS 15), and this only puts them in front of him.</para>
        /// </summary>
        private IEnumerator RunLooks()
        {
            if (_client == null || _script == null || _player == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the looks scenario has no client, no script or no founder to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            // A development game's leave is the server's answer (M1.E), which comes after the join: it is waited for, as the
            // knap and changes scenarios wait. It looked once, before the answer could come; the sweep's first run (M1.Bd,
            // 2026-09-24) found it so.
            double leaveFrom = T;
            while (!_player.FlightAllowed && T < leaveFrom + 6.0) yield return null;
            if (!_player.FlightAllowed)
            {
                // The panel's deeds are a development server's alone: without one the server refuses and closes the door.
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the looks scenario needs a development game (-eg-dev), and the server gave no leave"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            yield return Wait(1.5);

            // Mid-morning, so two runs of this scenario light the animals the same way (DEBTS: frames of different slices
            // are not a pixel comparison until the clock is pinned).
            _client.SendDevSetting(DevSettings.ClockLocalHour, LooksHour);
            yield return Wait(1.0);

            bool roo = false, bird = false;
            yield return SetDown(DevSettings.SpawnKangaroo, DefinitionCatalogue.AnimalOf(AnimalSpecies.EasternGreyKangaroo), "a kangaroo");
            EntityView kangaroo = _looksAnimal;
            if (kangaroo != null)
            {
                roo = true;
                yield return BackAwayTo(kangaroo, LooksNearM, LooksRooUpM, "the kangaroo");
                yield return CaptureLook("roo-near", kangaroo);
                yield return BackAwayTo(kangaroo, LooksFarM, LooksRooUpM, "the kangaroo");
                yield return CaptureLook("roo-far", kangaroo);
            }

            // A quarter turn before the bird is set down, so the two do not stand in each other's frames.
            _script.YawTargetDeg = _player.YawDeg + 90f;
            yield return Wait(2.0);
            yield return SetDown(DevSettings.SpawnOystercatcher, DefinitionCatalogue.AnimalOf(AnimalSpecies.PiedOystercatcher), "an oystercatcher");
            EntityView oystercatcher = _looksAnimal;
            if (oystercatcher != null)
            {
                bird = true;
                yield return BackAwayTo(oystercatcher, LooksNearM, LooksBirdUpM, "the oystercatcher");
                yield return CaptureLook("bird-near", oystercatcher);
                yield return BackAwayTo(oystercatcher, LooksFarM, LooksBirdUpM, "the oystercatcher");
                yield return CaptureLook("bird-far", oystercatcher);
            }

            // The moving pose: the bird up and beating, the kangaroo bounding. Nothing a founder can do makes a hand-set
            // animal flee, so the panel's row is what puts them into it.
            _client.SendDevSetting(DevSettings.AnimalsSetDownPose, AnimalPose.Fleeing);
            yield return Wait(1.5);
            if (oystercatcher != null)
            {
                yield return LookAt(oystercatcher, LooksFlyingUpM);
                yield return CaptureLook("bird-flying", oystercatcher);
            }
            if (kangaroo != null)
            {
                yield return LookAt(kangaroo, LooksRooUpM + 0.4);
                yield return CaptureLook("roo-fleeing", kangaroo);
            }

            bool fleeing = (kangaroo == null || kangaroo.Animal.Pose == AnimalPose.Fleeing)
                           && (oystercatcher == null || oystercatcher.Animal.Pose == AnimalPose.Fleeing);
            if (!fleeing)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the animals set down did not take the fleeing pose"));
            }
            MoverState end = _player.State;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("local_hour", LooksHour).With("kangaroo", roo).With("oystercatcher", bird).With("fleeing", fleeing)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", end.East).With("up", end.Up).With("north", end.North)));
            _running = false;
            Finish(_errors == 0 && roo && bird && fleeing && _frames == LooksCaptures * Sizes.Length ? 0 : 1);
        }

        /// <summary>
        /// Asks the panel's deed for one animal and waits for the server to show it: the entity of that kind the client did
        /// not hold before, which is the one this deed set down.
        /// </summary>
        private IEnumerator SetDown(string deed, Definition kind, string what)
        {
            _looksAnimal = null;
            HashSet<ulong> before = new HashSet<ulong>();
            foreach (EntityView view in _client.Entities.Views.Values)
                if (ReferenceEquals(view.Definition, kind)) before.Add(view.Id.Value);
            _client.SendDevSetting(deed, 0.0);
            double until = T + LooksArriveSeconds;
            while (T < until && _looksAnimal == null)
            {
                foreach (EntityView view in _client.Entities.Views.Values)
                {
                    if (!ReferenceEquals(view.Definition, kind) || before.Contains(view.Id.Value)) continue;
                    _looksAnimal = view;
                    break;
                }
                if (_looksAnimal != null) break;
                yield return null;
            }
            if (_looksAnimal == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", what + " was asked for and none arrived within " + LooksArriveSeconds + " s"));
                yield break;
            }
            _log.Record(T, Tick, "set_down", new JsonObject().With("what", kind.Key).With("id", _looksAnimal.Id.Value.ToString())
                .With("pose", (int)_looksAnimal.Animal.Pose).With("east", _looksAnimal.Position.X).With("up", _looksAnimal.Position.Y)
                .With("north", _looksAnimal.Position.Z).With("yaw_deg", (double)_looksAnimal.YawDeg));
            yield return Wait(0.8);
        }

        /// <summary>Walks the founder backwards, facing the animal, until it stands the wanted distance off.</summary>
        private IEnumerator BackAwayTo(EntityView animal, double metres, double liftM, string what)
        {
            double until = T + LooksBackSeconds;
            _script.Sprint = false;
            while (T < until)
            {
                Aim(animal, liftM);
                if (AwayM(animal) >= metres) break;
                _script.Move = new Vector2(0f, -1f);
                yield return null;
            }
            _script.Move = Vector2.zero;
            if (T >= until)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "could not back away to " + metres.ToString("0.0")
                    + " m from " + what + " in " + LooksBackSeconds + " s; it stands " + AwayM(animal).ToString("0.0") + " m off"));
            }
            // The body settles and the view catches up: what is drawn is the mirrors' delay behind what the server said.
            yield return Wait(1.0);
            Aim(animal, liftM);
            yield return Wait(0.6);
        }

        /// <summary>Turns to look at an animal where it stands and lets the turn finish.</summary>
        private IEnumerator LookAt(EntityView animal, double liftM)
        {
            Aim(animal, liftM);
            yield return Wait(1.2);
        }

        private void Aim(EntityView animal, double liftM) =>
            Face(new Double3(animal.Position.X, animal.Position.Y + liftM, animal.Position.Z));

        private double AwayM(EntityView animal)
        {
            Double3 feet = _player.State.Feet;
            double de = animal.Position.X - feet.X, dn = animal.Position.Z - feet.Z;
            return Math.Sqrt(de * de + dn * dn);
        }

        /// <summary>A frame of one animal, with a record of what was looked at, the pose it was in and how far off it stood.</summary>
        private IEnumerator CaptureLook(string name, EntityView animal)
        {
            _log.Record(T, Tick, "looks", new JsonObject().With("frame", name).With("what", animal.Definition.Key)
                .With("id", animal.Id.Value.ToString()).With("pose", (int)animal.Animal.Pose).With("away_m", AwayM(animal))
                .With("east", animal.Position.X).With("up", animal.Position.Y).With("north", animal.Position.Z)
                .With("yaw_deg", (double)animal.YawDeg));
            yield return Capture(name);
        }
    }
}

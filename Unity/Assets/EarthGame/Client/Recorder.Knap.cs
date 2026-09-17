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
        /// <summary>The first stone (FP.3): see <see cref="RunKnap"/>.</summary>
        public const string KnapScenario = "knap";

        /// <summary>
        /// How long the work button is held for each blow, s: a tap, a measured blow and a full swing. The arm winds up over
        /// <see cref="VerbController.WindUpSeconds"/>, so the tap is a light blow that should bounce off a fresh cobble, the
        /// measured blow should take a small sharp flake, and the full swing a bigger, coarser one.
        /// </summary>
        private const double KnapTapSeconds = 0.04, KnapHalfSeconds = 0.55, KnapFullSeconds = 1.4;
        /// <summary>How far the founder turns between the two stones set down, degrees, so the two lie a pace apart.</summary>
        private const float KnapTurnDeg = 35f;
        private const double KnapArriveSeconds = 6.0, KnapAnswerSeconds = 4.0, KnapSettleSeconds = 1.2, KnapAimSeconds = 4.0;
        /// <summary>How many captures the scenario makes: a flake lying, and one in hand.</summary>
        private const int KnapCaptures = 2;

        private readonly List<EntityView> _knapFlakes = new List<EntityView>();
        private readonly List<IntentResultMessage> _knapAnswers = new List<IntentResultMessage>();
        private EntityView _knapStone;
        private int _knapBlows, _knapFlaked, _knapBounced;
        private bool _knapUsable;

        /// <summary>
        /// The first stone (FP.3). In a development game the founder is stood at the wake with a full body; two silcrete
        /// cobbles are set down two metres ahead by the panel's deed, the founder turning a little between them so they lie a
        /// pace apart; the first is faced and picked up, the hammer; the second is faced, and the verb line should offer the
        /// blow. Then three blows with the work button: a tap, a measured hold and a full swing, each a <c>blow</c> record with
        /// the hammer, the core as it was, the wind-up sent, the founder's water, the server's answer in its own words, and what
        /// came of it: the core as it is now, and the flake that came away with its own mass and edge. A frame of the flakes
        /// lying beside the core ("flake-lying"); then a flake is faced, picked up and made the hand ("flake-held"). The end
        /// record counts the blows, the flakes and the bounces; the exit is 0 when the tap bounced, a flake came away and was a
        /// usable tool, a flake was held, every frame was written and nothing was logged as an error. The frames are William's
        /// to judge (STANDARDS 15); the numbers are <c>knap_check.py</c>'s, which restates the physics from its sources.
        /// </summary>
        private IEnumerator RunKnap()
        {
            if (_client == null || _script == null || _verbs == null || _player == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the knap scenario has no client, no script, no verbs or no founder to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            if (!_player.FlightAllowed)
            {
                // The panel's deeds are a development server's alone: without one the server refuses and closes the door.
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the knap scenario needs a development game (-eg-dev)"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            yield return Wait(1.5);
            _client.Entities.Spawned += OnKnapSpawned;
            _client.IntentAnswered += OnKnapAnswered;

            // A founder at the wake with a full body, so the work behind the swing is a whole body's, at the game's own rate.
            _client.SendDevSetting(DevSettings.StandAtWake, 0.0);
            _client.SendDevSetting(DevSettings.FounderWater, 1.0);
            _client.SendDevSetting(DevSettings.ClockScale, 1.0);
            double toldFrom = T;
            while (_client.LastWater01 < 0.999 && T < toldFrom + 3.0) yield return null;
            _script.PitchTargetDeg = 0f;
            yield return Wait(1.0);

            // Two silcrete cobbles, the coast's knapping stone, a pace apart: the hammer, then the core.
            yield return SetDownStone("the hammer");
            EntityView hammer = _knapStone;
            _script.YawTargetDeg = _player.YawDeg + KnapTurnDeg;
            yield return Wait(1.2);
            yield return SetDownStone("the core");
            EntityView core = _knapStone;
            if (hammer == null || core == null || hammer.Id.Value == core.Id.Value)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "two stones were asked for and two did not lie there"));
            }

            // The hammer into the hand, as the night scenario takes its stick: faced, picked up.
            float hammerMass = 0f;
            bool held = false;
            if (hammer != null)
            {
                hammerMass = hammer.Item.MassKg > 0f ? hammer.Item.MassKg : (float)hammer.Definition.MassKg;
                yield return TakeUp(hammer, "the hammer");
                held = Carries(hammer.Id.Value);
            }

            // The core under the crosshair, the line offering the blow.
            bool offered = false;
            if (held && core != null)
            {
                Face(core.Position);
                double aimFrom = T;
                while (T < aimFrom + KnapAimSeconds && (_verbs.Target == null || _verbs.Target.Id.Value != core.Id.Value)) yield return null;
                yield return Wait(0.3);
                offered = _verbs.Target != null && _verbs.Target.Id.Value == core.Id.Value;
                _log.Record(T, Tick, "aim", new JsonObject().With("core", core.Id.Value.ToString()).With("aimed", offered).With("line", _verbs.Line));
                if (!offered)
                {
                    _errors++;
                    _log.Record(T, Tick, "error", new JsonObject().With("message", "the core was not under the crosshair; the line was '" + _verbs.Line + "'"));
                }
            }

            // Three blows: a tap, a measured blow, a full swing.
            if (offered)
            {
                yield return Blow("tap", KnapTapSeconds, hammer, hammerMass, core);
                yield return Blow("half", KnapHalfSeconds, hammer, hammerMass, core);
                yield return Blow("full", KnapFullSeconds, hammer, hammerMass, core);
            }

            // The flakes lying beside the core.
            if (core != null && _client.Entities.Views.ContainsKey(core.Id.Value)) Face(core.Position);
            yield return Wait(0.8);
            yield return Capture("flake-lying");

            // A flake into the hand and made the hand, so the frame shows it held.
            bool flakeHeld = false;
            EntityView flake = NearestFlake();
            if (flake != null)
            {
                yield return TakeUp(flake, "a flake");
                flakeHeld = Carries(flake.Id.Value);
                if (flakeHeld)
                {
                    _script.Hold(PlaceOf(flake.Id.Value));
                    yield return Wait(0.8);
                }
            }
            else
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no flake lay within reach to pick up"));
            }
            yield return Capture("flake-held");

            _client.Entities.Spawned -= OnKnapSpawned;
            _client.IntentAnswered -= OnKnapAnswered;
            MoverState end = _player.State;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("hammer_held", held).With("offered", offered).With("blows", _knapBlows).With("flaked", _knapFlaked).With("bounced", _knapBounced)
                .With("flakes_seen", _knapFlakes.Count).With("usable_flake", _knapUsable).With("flake_held", flakeHeld)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", end.East).With("up", end.Up).With("north", end.North)));
            _running = false;
            Finish(_errors == 0 && _knapBounced >= 1 && _knapFlaked >= 1 && _knapUsable && flakeHeld && _frames == KnapCaptures * Sizes.Length ? 0 : 1);
        }

        /// <summary>Asks the panel's deed for a silcrete cobble and waits for the server to show it lying: the one of that kind the client did not hold before.</summary>
        private IEnumerator SetDownStone(string what)
        {
            _knapStone = null;
            Definition kind = DefinitionCatalogue.CobbleOf(StoneType.Silcrete);
            HashSet<ulong> before = new HashSet<ulong>();
            foreach (EntityView view in _client.Entities.Views.Values)
                if (ReferenceEquals(view.Definition, kind)) before.Add(view.Id.Value);
            _client.SendDevSetting(DevSettings.SpawnSilcreteCobble, 0.0);
            double until = T + KnapArriveSeconds;
            while (T < until && _knapStone == null)
            {
                foreach (EntityView view in _client.Entities.Views.Values)
                {
                    if (!ReferenceEquals(view.Definition, kind) || before.Contains(view.Id.Value) || !view.Item.Resting) continue;
                    _knapStone = view;
                    break;
                }
                if (_knapStone != null) break;
                yield return null;
            }
            if (_knapStone == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", what + " was asked for and no silcrete cobble lay there within " + KnapArriveSeconds + " s"));
                yield break;
            }
            _log.Record(T, Tick, "set_down", new JsonObject().With("what", kind.Key).With("id", _knapStone.Id.Value.ToString())
                .With("east", _knapStone.Position.X).With("up", _knapStone.Position.Y).With("north", _knapStone.Position.Z));
            yield return Wait(0.5);
        }

        /// <summary>Faces a thing lying, waits for the crosshair to find it, and presses use until it is carried or the time is up.</summary>
        private IEnumerator TakeUp(EntityView thing, string what)
        {
            ulong id = thing.Id.Value;
            Face(thing.Position);
            double aimFrom = T;
            while (T < aimFrom + KnapAimSeconds && (_verbs.Target == null || _verbs.Target.Id.Value != id)) yield return null;
            if (_verbs.Target != null && _verbs.Target.Id.Value == id)
            {
                _script.Use();
                double pickFrom = T;
                while (T < pickFrom + KnapAimSeconds && !Carries(id)) yield return null;
            }
            if (!Carries(id))
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", what + " was not picked up; the crosshair's line was '" + _verbs.Line + "'"));
            }
            yield return Wait(0.4);
        }

        /// <summary>
        /// One blow: the work button held for a time and let go, the answer waited for, the flake let fall, and everything the
        /// check needs written as a <c>blow</c> record: the hammer and the core as they were, the wind-up the client sent, the
        /// body's water, the outcome in the server's words, and the core and the flake as they are after.
        /// </summary>
        private IEnumerator Blow(string name, double holdSeconds, EntityView hammer, float hammerMass, EntityView core)
        {
            float massBefore = core.Item.MassKg > 0f ? core.Item.MassKg : (float)core.Definition.MassKg;
            float platformBefore = core.Item.PlatformDeg > 0f ? core.Item.PlatformDeg : (float)StoneCore.FreshPlatformDeg;
            int flakesBefore = core.Item.FlakesTaken;
            int flakesSeen = _knapFlakes.Count;
            double water = _client.LastWater01;
            KnapAsked? askedBefore = _verbs.LastKnap;

            _script.Work = true;
            yield return Wait(holdSeconds);
            _script.Work = false;

            KnapAsked? asked = null;
            IntentResultMessage? answer = null;
            double from = T;
            while (T < from + KnapAnswerSeconds)
            {
                asked = _verbs.LastKnap;
                bool fresh = asked.HasValue && (!askedBefore.HasValue || asked.Value.Sequence != askedBefore.Value.Sequence);
                if (fresh)
                    foreach (IntentResultMessage a in _knapAnswers)
                        if (a.Sequence == asked.Value.Sequence) answer = a;
                if (answer.HasValue) break;
                yield return null;
            }
            bool sent = asked.HasValue && (!askedBefore.HasValue || asked.Value.Sequence != askedBefore.Value.Sequence);
            if (!sent)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the " + name + " blow was not sent; the line was '" + _verbs.Line + "'"));
                yield break;
            }
            if (!answer.HasValue)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no answer to the " + name + " blow within " + KnapAnswerSeconds + " s"));
                yield break;
            }
            // The flake falls a put-down's height and the core's new state arrives: a moment for both.
            yield return Wait(KnapSettleSeconds);

            _knapBlows++;
            VerbOutcome outcome = answer.Value.Outcome;
            if (outcome == VerbOutcome.Flaked) _knapFlaked++;
            if (outcome == VerbOutcome.Bounced) _knapBounced++;
            bool coreGone = !_client.Entities.Views.ContainsKey(core.Id.Value);
            EntityView flake = null;
            for (int i = flakesSeen; i < _knapFlakes.Count; i++) flake = _knapFlakes[i];
            JsonObject o = new JsonObject().With("name", name).With("hold_s", holdSeconds).With("wind_up", asked.Value.WindUp01).With("sequence", (int)asked.Value.Sequence)
                .With("hammer_id", hammer.Id.Value.ToString()).With("hammer_key", hammer.Definition.Key).With("hammer_stone", StoneName(hammer.Definition)).With("hammer_mass_kg", (double)hammerMass)
                .With("core_id", core.Id.Value.ToString()).With("core_key", core.Definition.Key).With("core_stone", StoneName(core.Definition))
                .With("core_mass_before_kg", (double)massBefore).With("core_platform_before_deg", (double)platformBefore).With("core_flakes_before", flakesBefore)
                .With("water", water).With("outcome", outcome.ToString()).With("note", answer.Value.Note ?? string.Empty)
                .With("core_gone", coreGone);
            if (!coreGone)
                o.With("core_mass_after_kg", (double)core.Item.MassKg).With("core_platform_after_deg", (double)core.Item.PlatformDeg).With("core_flakes_after", (int)core.Item.FlakesTaken);
            if (flake != null)
            {
                bool usable = flake.Item.Edge01 >= Knapping.UsableEdge && flake.Item.MassKg >= Knapping.UsableFlakeKg;
                _knapUsable |= usable;
                o.With("flake_id", flake.Id.Value.ToString()).With("flake_key", flake.Definition.Key).With("flake_mass_kg", (double)flake.Item.MassKg)
                 .With("flake_edge", (double)flake.Item.Edge01).With("flake_usable", usable).With("flake_resting", flake.Item.Resting)
                 .With("flake_east", flake.Position.X).With("flake_up", flake.Position.Y).With("flake_north", flake.Position.Z);
            }
            _log.Record(T, Tick, "blow", o);
        }

        private static string StoneName(Definition definition)
        {
            StoneType stone = DefinitionCatalogue.StoneOf(definition);
            return stone != null ? stone.Name : string.Empty;
        }

        /// <summary>The nearest flake of this run lying at rest within reach of the eye, or null.</summary>
        private EntityView NearestFlake()
        {
            Double3 eye = _player.Eye;
            EntityView best = null;
            double bestM = Hands.ReachM - 0.25;
            foreach (EntityView v in _knapFlakes)
            {
                if (!_client.Entities.Views.ContainsKey(v.Id.Value) || !v.Item.Resting) continue;
                double d = Double3.Distance(eye, v.Position);
                if (d > bestM) continue;
                bestM = d;
                best = v;
            }
            return best;
        }

        /// <summary>The place a carried thing is in, 0 when it is not carried.</summary>
        private int PlaceOf(ulong id)
        {
            CarriedThing[] things = _client.Carrying.Things;
            if (things != null)
                foreach (CarriedThing t in things)
                    if (t.Id == id) return t.Place;
            return 0;
        }

        private void OnKnapSpawned(EntityView view)
        {
            if (DefinitionCatalogue.IsFlake(view.Definition)) _knapFlakes.Add(view);
        }

        private void OnKnapAnswered(IntentResultMessage result) => _knapAnswers.Add(result);
    }
}

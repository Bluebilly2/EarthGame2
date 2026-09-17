using System;
using System.Collections;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>The thirst and the drink (FP.1): see <see cref="RunDrink"/>.</summary>
        public const string DrinkScenario = "drink";

        /// <summary>The clock's rate while the founder dries: sixty times the game's, a day in half a minute.</summary>
        private const double DrinkClockScale = 60.0;
        /// <summary>How long very thirsty is waited for at that rate, s: it comes at 4% lost, seven tenths of a day, twenty-one seconds.</summary>
        private const double DrinkThirstTimeoutSeconds = 60.0;
        private const double DrinkFreshSearchM = 60.0, DrinkSeaSearchM = 150.0, DrinkStandOffM = 2.5;
        /// <summary>Water counts where the engine says it stands: the one threshold, the server's and the aim's.</summary>
        private const double DrinkWalkTimeoutSeconds = 150.0;

        private double _drinkWater = double.NaN;
        private ThirstLevel _drinkLevel = ThirstLevel.Fine;
        private int _drinkAnswers;
        private VerbOutcome _drinkLast = VerbOutcome.NotNow;

        /// <summary>
        /// The thirst and the drink (FP.1). The founder, stood by fresh water and near the sea by <c>Tools/world/drink.py</c> in a
        /// development game, has the clock sped sixty times and waits, every word of the server's about their body written as a
        /// <c>thirst</c> record, until very thirsty ("thirsty" frame: the word under the clock); the clock is put back; they walk to
        /// the nearest fresh water within reach, look at it and press use, and the answer and the water before and after are a
        /// <c>drink</c> record ("drank" frame); then to the sea, the same ("sea" frame), where the answer should be salt. The end
        /// record says how long the thirst took, whether they drank and whether the sea refused; the exit is 0 when both, every
        /// frame was written and nothing was logged as an error.
        /// </summary>
        private IEnumerator RunDrink()
        {
            if (_client == null || _script == null || _verbs == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the drink scenario has no client, no script or no verbs to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            yield return Wait(1.5);
            _client.FounderStateChanged += OnDrinkFounderState;
            _client.IntentAnswered += OnDrinkAnswered;
            // A full body to begin with, by the panel's row: the world's save keeps the thirst of the last run, and the run
            // on 2026-09-15 that woke very thirsty from it measured no drying at all.
            double toldFrom = T;
            _client.SendDevSetting(DevSettings.FounderWater, 1.0);
            while (_client.LastWater01 < 1.0 && T < toldFrom + 3.0) yield return null;
            _drinkWater = _client.LastWater01;
            _drinkLevel = Hydration.LevelOf(_drinkWater);
            RecordThirst("start");

            // The day sped: a founder dries in half a minute what takes a day, and every word of it is recorded.
            _client.SendDevSetting(DevSettings.ClockScale, DrinkClockScale);
            double dryingFrom = T;
            while (_drinkLevel < ThirstLevel.VeryThirsty && T < dryingFrom + DrinkThirstTimeoutSeconds) yield return null;
            double thirstSeconds = T - dryingFrom;
            bool veryThirsty = _drinkLevel >= ThirstLevel.VeryThirsty;
            _client.SendDevSetting(DevSettings.ClockScale, 1.0);
            if (!veryThirsty)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "not very thirsty after " + thirstSeconds.ToString("0.0") + " s at " + DrinkClockScale + " times the game's rate; water " + _drinkWater.ToString("0.0000")));
            }
            yield return Wait(0.5);
            yield return Capture("thirsty");

            // The fresh water within reach, and the drink.
            bool drank = false;
            double before = _drinkWater, after = double.NaN;
            VerbOutcome freshAnswer = VerbOutcome.NotNow;
            if (FindWater(GroundCover.FreshWater, DrinkFreshSearchM, out double freshEast, out double freshNorth))
            {
                yield return WalkTo(freshEast, freshNorth, DrinkStandOffM, DrinkWalkTimeoutSeconds, "the fresh water");
                yield return LookAtWater(freshEast, freshNorth);
                before = _drinkWater;
                yield return PressUse();
                freshAnswer = _drinkLast;
                yield return WaitForWater(before, 2.0);
                after = _drinkWater;
                drank = freshAnswer == VerbOutcome.Done;
                _log.Record(T, Tick, "drink", WithWhere(new JsonObject().With("water_kind", "fresh").With("outcome", freshAnswer.ToString())
                    .With("water_before", before).With("water_after", after).With("looked_at", _verbs.WaterAt.HasValue)));
            }
            else
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no fresh water within " + DrinkFreshSearchM + " m of the founder"));
            }
            yield return Capture("drank");

            // The sea, which will not drink.
            bool saltRefused = false;
            if (FindWater(GroundCover.Sea, DrinkSeaSearchM, out double seaEast, out double seaNorth))
            {
                yield return WalkTo(seaEast, seaNorth, DrinkStandOffM, DrinkWalkTimeoutSeconds, "the sea");
                yield return LookAtWater(seaEast, seaNorth);
                double seaBefore = _drinkWater;
                yield return PressUse();
                VerbOutcome seaAnswer = _drinkLast;
                yield return Wait(1.2);
                saltRefused = seaAnswer == VerbOutcome.Salt;
                _log.Record(T, Tick, "drink", WithWhere(new JsonObject().With("water_kind", "sea").With("outcome", seaAnswer.ToString())
                    .With("water_before", seaBefore).With("water_after", _drinkWater).With("looked_at", _verbs.WaterAt.HasValue)));
            }
            else
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no sea within " + DrinkSeaSearchM + " m of the founder"));
            }
            yield return Capture("sea");

            _client.FounderStateChanged -= OnDrinkFounderState;
            _client.IntentAnswered -= OnDrinkAnswered;
            MoverState end = _player.State;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("very_thirsty", veryThirsty).With("thirst_seconds", thirstSeconds).With("clock_scale", DrinkClockScale)
                .With("drank", drank).With("water_before", before).With("water_after", after).With("salt_refused", saltRefused)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", end.East).With("up", end.Up).With("north", end.North)));
            _running = false;
            Finish(_errors == 0 && veryThirsty && drank && saltRefused && _frames == 3 * Sizes.Length ? 0 : 1);
        }

        private void OnDrinkFounderState(FounderStateMessage founder)
        {
            _drinkWater = founder.Water01;
            ThirstLevel level = Hydration.LevelOf(founder.Water01);
            bool changed = level != _drinkLevel;
            _drinkLevel = level;
            RecordThirst(changed ? "word" : "told");
        }

        private void OnDrinkAnswered(IntentResultMessage result)
        {
            _drinkAnswers++;
            _drinkLast = result.Outcome;
        }

        /// <summary>Every word the server has about the founder's water: what it is, the loss, the level and the word on the screen.</summary>
        private void RecordThirst(string why)
        {
            _log.Record(T, Tick, "thirst", new JsonObject().With("why", why).With("water", _drinkWater).With("loss", 1.0 - _drinkWater)
                .With("level", (int)_drinkLevel).With("word", Hydration.WordFor(_drinkLevel)));
        }

        private JsonObject WithWhere(JsonObject o)
        {
            MoverState s = _player.State;
            return o.With("east", s.East).With("up", s.Up).With("north", s.North);
        }


        /// <summary>The nearest point out to a distance where the streamed water stands and its cover is the one asked for: the engine-free search the corpus's walker uses too (<see cref="Drinking.WaterNear"/>, one owner since 2026-09-16).</summary>
        private bool FindWater(GroundCover cover, double searchM, out double atEast, out double atNorth)
        {
            Double3 feet = _player.State.Feet;
            if (_client.Grid == null || _client.Tiles == null)
            {
                atEast = 0.0;
                atNorth = 0.0;
                return false;
            }
            return Drinking.WaterNear((layer, id) => _client.Tiles.Holding(layer, id), _client.Grid, feet.X, feet.Z, searchM, cover, out atEast, out atNorth);
        }

        /// <summary>Walks the founder towards a point until within a distance of it, turning to face it as they go.</summary>
        private IEnumerator WalkTo(double east, double north, double withinM, double timeoutSeconds, string what)
        {
            double from = T;
            _script.Sprint = false;
            while (T < from + timeoutSeconds)
            {
                Double3 feet = _player.State.Feet;
                double de = east - feet.X, dn = north - feet.Z;
                if (Math.Sqrt(de * de + dn * dn) <= withinM) break;
                _script.YawTargetDeg = (float)(Math.Atan2(de, dn) * 180.0 / Math.PI);
                _script.PitchTargetDeg = 0f;
                _script.Move = new Vector2(0f, 1f);
                yield return null;
            }
            _script.Move = Vector2.zero;
            if (T >= from + timeoutSeconds)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "did not reach " + what + " at east " + east.ToString("0.0") + " north " + north.ToString("0.0") + " in " + timeoutSeconds + " s"));
            }
            yield return Wait(0.6);
        }

        /// <summary>Turns to face the water and pitches down until the crosshair meets its surface within reach, as the verbs see it.</summary>
        private IEnumerator LookAtWater(double east, double north)
        {
            Double3 feet = _player.State.Feet;
            _script.YawTargetDeg = (float)(Math.Atan2(east - feet.X, north - feet.Z) * 180.0 / Math.PI);
            foreach (float pitch in new[] { 25f, 35f, 45f, 55f, 15f, 65f })
            {
                _script.PitchTargetDeg = pitch;
                yield return Wait(0.6);
                if (_verbs.WaterAt.HasValue) yield break;
            }
        }

        /// <summary>The use key, and the server's answer waited for.</summary>
        private IEnumerator PressUse()
        {
            int answers = _drinkAnswers;
            _script.Use();
            double from = T;
            while (_drinkAnswers == answers && T < from + 3.0) yield return null;
            if (_drinkAnswers == answers)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no answer to the use key within 3 s"));
            }
        }

        /// <summary>Waits for the server's next word about the water, up to a time, so a drink's rise is read after it lands.</summary>
        private IEnumerator WaitForWater(double before, double seconds)
        {
            double from = T;
            while (_drinkWater == before && T < from + seconds) yield return null;
        }
    }
}

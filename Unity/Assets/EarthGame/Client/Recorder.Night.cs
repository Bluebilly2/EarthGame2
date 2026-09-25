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
        /// <summary>The night's cold and death (FP.2): see <see cref="RunNight"/>.</summary>
        public const string NightScenario = "night";

        /// <summary>
        /// The clock's rate through the night: a world hour every second and a quarter, 2,880 times the game's since ruling 52
        /// made the game's day a real one (2026-09-25); it was sixty times the thirty-minute day, the same pace in real seconds.
        /// </summary>
        private const double NightClockScale = 2880.0;
        /// <summary>How long the cold is waited for at that rate, s: twelve hours of the world's night.</summary>
        private const double NightColdTimeoutSeconds = 15.0;
        /// <summary>How long the dawn is waited for at that rate from the night's start, s: twenty hours of the world's.</summary>
        private const double NightDawnTimeoutSeconds = 25.0;
        /// <summary>How far the founder walks from the wake before the night, m, so where they fall is not where they wake.</summary>
        private const double NightWalkM = 12.0;
        private const double NightWakeWithinM = 3.0, NightStickWithinM = 2.5;

        private double _nightWater = double.NaN, _nightCore = double.NaN, _nightLowestCore = double.NaN;
        private ColdLevel _nightWorst = ColdLevel.Well;
        private int _nightDeaths;
        private Death _nightDeath;

        /// <summary>
        /// The night's cold and death (FP.2). In a development game the founder is stood at the wake with a full body, a stick
        /// spawned, faced and picked up, and walked a dozen metres off; the clock is put to ten in the evening and sped to a day
        /// in half a minute, and every word the server has of the body is a <c>warmth</c> record with the sky the client works out and
        /// the world's hours beside it. The founder stands there through the night: the first "cold" is a <c>cold_reached</c>
        /// record and a frame (the word under the clock), the sun's rise a <c>dawn</c> record and a frame (the lowest core the
        /// night reached, the coldest word, whether the night itself killed). Then, unless it did, the panel's row moves the
        /// core below the lethal, and the death that follows is a <c>died</c> record with the sentence ("died" frame); the
        /// founder should stand at the wake again with the stick lying where they fell. The end record says what came of
        /// each; the exit is 0 when the founder went cold, saw the dawn or died in the night, died, woke at the wake, the
        /// stick lies where they fell, every frame was written and nothing was logged as an error. The run passes
        /// <c>-eg-no-bridge</c>: under the beta arc's bridge (ServerConfig.BetaArcBridge) no cold kills.
        /// </summary>
        private IEnumerator RunNight()
        {
            if (_client == null || _script == null || _verbs == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the night scenario has no client, no script or no verbs to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            yield return Wait(1.5);
            _client.FounderStateChanged += OnNightFounderState;
            _client.Died += OnNightDied;

            // A new founder at the wake with a full, warm body, whatever the world's save remembers.
            _client.SendDevSetting(DevSettings.StandAtWake, 0.0);
            _client.SendDevSetting(DevSettings.FounderWater, 1.0);
            _client.SendDevSetting(DevSettings.FounderCoreC, Warmth.NormalCoreC);
            _client.SendDevSetting(DevSettings.ClockScale, 1.0);
            double toldFrom = T;
            while ((_client.LastCoreC < Warmth.NormalCoreC - 0.05 || _client.LastWater01 < 0.999) && T < toldFrom + 3.0) yield return null;
            yield return Wait(1.0);
            // Where the deed stood them is the wake: the Welcome's spawn is where the saved founder joined, which need not be it.
            Double3 wake = _player.State.Feet;
            _nightLowestCore = _client.LastCoreC;
            _nightWorst = Warmth.LevelOf(_client.LastCoreC);
            RecordWarmth("start");

            // A stick in hand: spawned two metres ahead, faced and picked up as the carry scenario picks up. A fixed pitch is
            // not enough (the second run's crosshair passed half a metre over it); the founder looks at the thing itself.
            ulong stickId = 0;
            _client.SendDevSetting(DevSettings.SpawnStick, 0.0);
            EntityView spawned = null;
            double spawnFrom = T;
            while ((spawned = Nearest(DefinitionCatalogue.Stick)) == null && T < spawnFrom + 4.0) yield return null;
            if (spawned != null)
            {
                ulong id = spawned.Id.Value;
                Face(spawned.Position);
                double aimFrom = T;
                while (T < aimFrom + 4.0 && (_verbs.Target == null || _verbs.Target.Id.Value != id)) yield return null;
                if (_verbs.Target != null && _verbs.Target.Id.Value == id)
                {
                    _script.Use();
                    double pickFrom = T;
                    while (T < pickFrom + 4.0 && !Carries(id)) yield return null;
                    if (Carries(id)) stickId = id;
                }
            }
            if (stickId == 0)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", spawned == null
                    ? "no stick lay within reach four seconds after the spawn"
                    : "the stick was not picked up; the crosshair's line was '" + _verbs.Line + "'"));
            }
            _script.PitchTargetDeg = 0f;

            // A dozen metres off, so where they fall is not where they wake; then still, so the body's walk is over before the night.
            Double3 feet = _player.State.Feet;
            double yaw = _player.YawDeg * Math.PI / 180.0;
            yield return WalkTo(feet.X + NightWalkM * Math.Sin(yaw), feet.Z + NightWalkM * Math.Cos(yaw), 1.0, 40.0, "the night's place");
            double stillFrom = T;
            while (_player.State.HorizontalSpeed > 0.05 && T < stillFrom + 8.0) yield return null;
            Double3 stood = _player.State.Feet;

            // The night: the clock put to ten in the evening and this client's own clock given a moment to follow it, so the sky it
            // records is the night's; then sped, every word of the body recorded with the sky, until the founder is cold.
            _client.SendDevSetting(DevSettings.ClockLocalHour, 22.0);
            yield return Wait(2.5);
            _log.Record(T, Tick, "night", Hours(new JsonObject()).With("east", stood.X).With("up", stood.Y).With("north", stood.Z).With("stick", stickId)
                .With("wake_east", wake.X).With("wake_north", wake.Z).With("speed_ms", _player.State.HorizontalSpeed));
            _client.SendDevSetting(DevSettings.ClockScale, NightClockScale);
            double nightFrom = T;
            int deathsBefore = _nightDeaths;
            while (Warmth.LevelOf(_nightCore) < ColdLevel.Cold && _nightDeaths == deathsBefore && T < nightFrom + NightColdTimeoutSeconds) yield return null;
            double coldAfter = T - nightFrom;
            bool cold = Warmth.LevelOf(_nightCore) >= ColdLevel.Cold;
            _log.Record(T, Tick, "cold_reached", WithFinite(WithFinite(Hours(new JsonObject()).With("cold", cold).With("after_s", coldAfter), "core_c", _nightCore), "lowest_core_c", _nightLowestCore));
            if (!cold)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "not cold after " + coldAfter.ToString("0.0") + " s at " + NightClockScale + " times the game's rate; core " + _nightCore.ToString("0.00")));
            }
            // The clock runs on through the frame: the word under the night's own sky.
            yield return Capture("cold");

            // On to the dawn at the same rate, the founder standing where they are: the sun's rise ends the night, or a death in it does.
            while ((_sun == null || _sun() < 0.0) && _nightDeaths == deathsBefore && T < nightFrom + NightDawnTimeoutSeconds) yield return null;
            _client.SendDevSetting(DevSettings.ClockScale, 1.0);
            bool diedInTheNight = _nightDeaths > deathsBefore;
            bool sunUp = _sun != null && _sun() >= 0.0;
            double naturalLowest = _nightLowestCore;
            ColdLevel worst = _nightWorst;
            _log.Record(T, Tick, "dawn", WithFinite(WithFinite(WithFinite(Hours(new JsonObject()).With("sun_up", sunUp).With("after_s", T - nightFrom)
                .With("coldest_word", Warmth.WordFor(worst)).With("coldest", (int)worst).With("died_in_the_night", diedInTheNight),
                "core_c", _nightCore), "lowest_core_c", naturalLowest), "sun_elevation_deg", _sun != null ? _sun() : double.NaN));
            if (!sunUp && !diedInTheNight)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no dawn and no death " + (T - nightFrom).ToString("0.0") + " s after the night began at " + NightClockScale + " times the game's rate"));
            }
            yield return Wait(0.5);
            yield return Capture("dawn");

            // The death, by the panel's row, unless the night was death enough: the next step is the death, and the server tells it.
            bool died = diedInTheNight;
            if (!died)
            {
                int deaths = _nightDeaths;
                _client.SendDevSetting(DevSettings.FounderCoreC, 26.0);
                double deathFrom = T;
                while (_nightDeaths == deaths && T < deathFrom + 5.0) yield return null;
                died = _nightDeaths > deaths;
                if (!died)
                {
                    _errors++;
                    _log.Record(T, Tick, "error", new JsonObject().With("message", "no death told within 5 s of the core put to 26"));
                }
            }
            yield return Wait(1.0);
            yield return Capture("died");
            yield return Wait(1.5);

            Double3 now = _player.State.Feet;
            double wakeDistance = Math.Sqrt((now.X - wake.X) * (now.X - wake.X) + (now.Z - wake.Z) * (now.Z - wake.Z));
            bool atWake = wakeDistance <= NightWakeWithinM;
            // The stick is held to where the server says they fell, which is where it let the stick go.
            double fellEast = died ? _nightDeath.East : stood.X, fellNorth = died ? _nightDeath.North : stood.Z;
            double stickDistance = double.NaN;
            bool stickLies = false;
            if (stickId != 0 && _client.Entities.Views.TryGetValue(stickId, out EntityView stick))
            {
                stickDistance = Math.Sqrt((stick.Position.X - fellEast) * (stick.Position.X - fellEast) + (stick.Position.Z - fellNorth) * (stick.Position.Z - fellNorth));
                stickLies = stickDistance <= NightStickWithinM;
                _log.Record(T, Tick, "stick", new JsonObject().With("id", stickId).With("east", stick.Position.X).With("up", stick.Position.Y).With("north", stick.Position.Z)
                    .With("fell_east", fellEast).With("fell_north", fellNorth).With("distance_m", stickDistance).With("resting", stick.Item.Resting));
            }

            _client.FounderStateChanged -= OnNightFounderState;
            _client.Died -= OnNightDied;
            _log.Record(T, Tick, "end", WithFinite(WithFinite(WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("cold", cold).With("cold_after_s", coldAfter).With("clock_scale", NightClockScale)
                .With("sun_up", sunUp).With("died_in_the_night", diedInTheNight).With("coldest_word", Warmth.WordFor(worst))
                .With("died", died).With("cause", died ? _nightDeath.Cause.ToString() : "").With("sentence", died ? _nightDeath.Explain() : "")
                .With("respawned_at_wake", atWake).With("wake_distance_m", wakeDistance)
                .With("stick_lies_where_fell", stickLies)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", now.X).With("up", now.Y).With("north", now.Z)), "lowest_core_c", naturalLowest), "stick_distance_m", stickDistance));
            _running = false;
            Finish(_errors == 0 && cold && (sunUp || diedInTheNight) && died && atWake && stickLies && _frames == 3 * Sizes.Length ? 0 : 1);
        }

        private void OnNightFounderState(FounderStateMessage founder)
        {
            _nightWater = founder.Water01;
            _nightCore = founder.CoreC;
            if (double.IsNaN(_nightLowestCore) || founder.CoreC < _nightLowestCore) _nightLowestCore = founder.CoreC;
            ColdLevel level = Warmth.LevelOf(founder.CoreC);
            if (level > _nightWorst) _nightWorst = level;
            RecordWarmth("told");
        }

        private void OnNightDied(Death death)
        {
            _nightDeaths++;
            _nightDeath = death;
            _log.Record(T, Tick, "died", Hours(new JsonObject()).With("cause", death.Cause.ToString()).With("local_hour", death.LocalHour).With("clock", death.Clock)
                .With("air_c", death.AirC).With("wind_ms", death.WindMs).With("loss_w", death.LossW).With("production_w", death.ProductionW)
                .With("core_c", death.CoreC).With("water_loss", death.WaterLoss).With("east", death.East).With("north", death.North)
                .With("sentence", death.Explain()));
        }

        /// <summary>
        /// Every word the server has of the body, with the sky the client works out for where the founder stands (the world's
        /// weather is a function of the seed and the clock, so both ends have it) and the world's hours: the check restates
        /// the balance from these, by the world's own time.
        /// </summary>
        private void RecordWarmth(string why)
        {
            JsonObject o = WithFinite(WithFinite(Hours(new JsonObject()).With("why", why), "water", _nightWater), "core_c", _nightCore)
                .With("cold", (int)Warmth.LevelOf(_nightCore)).With("cold_word", Warmth.WordFor(Warmth.LevelOf(_nightCore)))
                .With("thirst_word", Hydration.WordFor(Hydration.LevelOf(_nightWater)));
            if (_sky != null)
            {
                Weather sky = _sky();
                o.With("air_c", sky.AirC).With("wind_open_ms", sky.WindMs).With("cloud", sky.CloudCover01).With("humidity", sky.RelativeHumidity01);
            }
            if (_sun != null) WithFinite(o, "sun_elevation_deg", _sun());
            MoverState s = _player.State;
            o.With("speed_ms", s.HorizontalSpeed).With("east", s.East).With("up", s.Up).With("north", s.North);
            _log.Record(T, Tick, "warmth", o);
        }

        /// <summary>The world's hours on this client's clock, when it has one: the time the body's balance runs on.</summary>
        private JsonObject Hours(JsonObject o) => _hours != null ? WithFinite(o, "world_hours", _hours()) : o;

        /// <summary>A number a record takes only when it is one: JSON has no NaN (one ended the second run), and a key missing is what the check reads as unmeasured.</summary>
        private static JsonObject WithFinite(JsonObject o, string key, double value) => double.IsNaN(value) || double.IsInfinity(value) ? o : o.With(key, value);
    }
}

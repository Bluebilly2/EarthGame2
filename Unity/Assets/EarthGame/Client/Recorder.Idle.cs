using System;
using System.Collections;
using System.Globalization;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>The idle pause (M1.E, CANON ruling 38): see <see cref="RunIdle"/>.</summary>
        public const string IdleScenario = "idle";

        /// <summary>Whether the game is asleep: set by the client runtime, which owns the idle watch (M1.E).</summary>
        public Func<bool> Asleep;

        /// <summary>
        /// What the idle watch had counted when the world came into hand, s, NaN before it (ClientRuntime.IdleCountedAtInHand):
        /// set by the client runtime.
        /// </summary>
        public Func<double> CountedAtInHand;

        /// <summary>How long the game is left alone before it must have slept, s: the watch's minute and a margin.</summary>
        private static readonly double IdleFallSeconds = IdleWatch.AfterSeconds + 5.0;

        /// <summary>How long the game is held asleep, s: long enough that a world still stepping would show it on its own clock.</summary>
        private const double IdleHeldSeconds = 20.0;

        /// <summary>How long a key is given to wake it, and how long the world is then given to tell its clock again, s.</summary>
        private const double IdleWakeSeconds = 5.0, IdleToldSeconds = 3.0;

        /// <summary>
        /// Asleep, the world's clock may move by no more than this share of what it would have moved over the same time
        /// awake: the pongs either side of the sleep are each up to a second old, and nothing else moves it.
        /// </summary>
        private const double IdleMostShareMoved = 0.25;

        /// <summary>
        /// The idle pause (M1.E, CANON ruling 38): "if the game is ever open, and idle, i am not playing … the game is paused,
        /// time is paused." The one recorded run allowed to sleep, since sleeping is what it proves. Played for a while, the
        /// world's clock is read from the server's own pongs, which gives the rate it runs at; then nothing is touched until
        /// the game sleeps ("paused" frame: the line on the screen); held asleep for <see cref="IdleHeldSeconds"/>; woken by
        /// a key on a keyboard of the scenario's own; and the server's clock read again. A world that slept moved by the
        /// seconds either side of the sleep alone; one still stepping would have moved by all of them. The frames drawn while
        /// it was held asleep, over those seconds, are the sleeping game's frame rate. The exit is 0 when the watch counted
        /// nothing while the world was being prepared, the game slept within its minute, woke to the key, the clock moved by
        /// less than <see cref="IdleMostShareMoved"/> of what it would have awake, the sleeping frame rate was within half
        /// again of the runtime's cap, and the founder did not rise in the half second after waking (the waking key does
        /// nothing else).
        /// </summary>
        private IEnumerator RunIdle()
        {
            if (_client == null || Asleep == null || CountedAtInHand == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the idle scenario has no client or no idle watch to read"));
                _running = false;
                Finish(1);
                yield break;
            }
            // The world being prepared: the watch counts nothing until the world is in hand (2026-09-25: it counted the loading,
            // and William's whole valley, a new world that takes minutes to prepare, fell asleep on the loading screen and
            // prepared next to nothing until he clicked the game). The runtime notes the watch's count at the moment the world
            // comes into hand, which is nothing at all where the watch waits, and a frame's seconds or more where it counts
            // from the connection.
            double from = T;
            while (double.IsNaN(CountedAtInHand()) && T < from + ReadyTimeoutSeconds) yield return null;
            double countedAtInHand = CountedAtInHand();
            bool watchWaited = countedAtInHand == 0.0;
            if (!watchWaited)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", double.IsNaN(countedAtInHand)
                    ? "the world did not come into hand"
                    : "the idle watch had counted " + countedAtInHand.ToString("0.0000", CultureInfo.InvariantCulture) + " s when the world came into hand"));
            }
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            yield return Wait(3.0);

            // Played: the server's clock at the start of the stretch the game is left alone.
            double h0 = _client.LastServerTotalHours, t0 = T;
            _log.Record(T, Tick, "idle_start", new JsonObject().With("server_hours", h0).With("counted_at_in_hand_s", countedAtInHand));

            // Left alone: no key, no button, no mouse. The last pong before the sleep is the clock's last word awake.
            double hLast = h0, tLast = t0;
            while (!Asleep() && T < t0 + IdleFallSeconds)
            {
                if (_client.LastServerTotalHours != hLast)
                {
                    hLast = _client.LastServerTotalHours;
                    tLast = T;
                }
                yield return null;
            }
            bool slept = Asleep();
            double sleptAt = T;
            _log.Record(T, Tick, "asleep", new JsonObject().With("asleep", slept).With("after_s", sleptAt - t0).With("server_hours", hLast));
            if (!slept)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "left alone for " + (T - t0).ToString("0.0", CultureInfo.InvariantCulture) + " s and not asleep"));
            }
            yield return Capture("paused");
            // Held asleep: the frames drawn meanwhile, over the seconds, are the rate the sleeping game runs at, which the
            // runtime holds to ClientRuntime.SleepingFrameRate so an idle game leaves the GPU to whatever else is running.
            int framesAtSleep = Time.frameCount;
            double heldFrom = T;
            yield return Wait(IdleHeldSeconds);
            double framesAsleepPerSecond = (Time.frameCount - framesAtSleep) / Math.Max(1e-6, T - heldFrom);
            bool framesHeld = framesAsleepPerSecond <= 1.5 * ClientRuntime.SleepingFrameRate;

            // A key on a keyboard of the scenario's own wakes it. A windowless player never has the focus, and without it the
            // Input System ignores a device's presses, as the controls scenario found (M1.5d); this one is heard regardless.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Idle scenario keyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            double pressedAt = T;
            // What the player saw of the key, for the record: the asset's Touched action (the watch's feed since 2026-09-21),
            // whether it resolved to this keyboard's anyKey at all, and the keyboard's own word.
            InputAction touched = InputSystem.actions != null ? InputSystem.actions.FindAction(Controls.Touched, false) : null;
            bool boundHere = false;
            for (int i = 0; touched != null && i < touched.controls.Count; i++) boundHere |= ReferenceEquals(touched.controls[i], keyboard.anyKey);
            bool touchedSeen = false, actionSeen = false, keySeen = false;
            while (Asleep() && T < pressedAt + IdleWakeSeconds)
            {
                touchedSeen |= _player != null && _player.Touched;
                actionSeen |= touched != null && touched.IsPressed();
                keySeen |= keyboard.anyKey.isPressed;
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            bool woke = !Asleep();
            double wokeAt = T;
            _log.Record(T, Tick, "woke", new JsonObject().With("woke", woke).With("after_s", wokeAt - pressedAt)
                .With("touched_action_found", touched != null).With("touched_action_enabled", touched != null && touched.enabled)
                .With("touched_controls", touched != null ? touched.controls.Count : 0).With("bound_to_this_keyboard", boundHere)
                .With("player_saw_touch", touchedSeen).With("action_pressed", actionSeen).With("key_pressed", keySeen));
            if (!woke)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "a key pressed and still asleep"));
            }
            // The waking key does nothing else (M1.E): the key is Space, the jump, and a founder who leaps on waking was moved by it.
            double upAtWake = _player != null ? _player.State.Up : 0.0;
            yield return Wait(0.5);
            double afterWakeRise = _player != null ? _player.State.Up - upAtWake : 0.0;
            bool stayedPut = Math.Abs(afterWakeRise) < 0.05;

            // The world's clock told again, awake.
            double hBefore = _client.LastServerTotalHours;
            double toldFrom = T;
            while (_client.LastServerTotalHours == hBefore && T < toldFrom + IdleToldSeconds) yield return null;
            yield return Wait(0.2);
            double hAfter = _client.LastServerTotalHours, tAfter = T;
            InputSystem.RemoveDevice(keyboard);

            double ratePerSecond = tLast > t0 && hLast > h0 ? (hLast - h0) / (tLast - t0) : double.NaN;
            double moved = hAfter - hLast;
            double wouldHave = ratePerSecond * (tAfter - tLast);
            bool stood = !double.IsNaN(ratePerSecond) && wouldHave > 0.0 && moved < IdleMostShareMoved * wouldHave;
            _log.Record(T, Tick, "end", new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("slept", slept).With("slept_after_s", sleptAt - t0).With("woke", woke)
                .With("clock_rate_hours_per_s", double.IsNaN(ratePerSecond) ? 0.0 : ratePerSecond)
                .With("clock_moved_hours", moved).With("clock_would_have_moved_hours", double.IsNaN(wouldHave) ? 0.0 : wouldHave)
                .With("clock_stood", stood).With("real_seconds_across", tAfter - tLast)
                .With("frames_asleep_per_s", framesAsleepPerSecond).With("frames_held", framesHeld)
                .With("after_wake_rise_m", afterWakeRise).With("stayed_put", stayedPut)
                .With("counted_at_in_hand_s", countedAtInHand).With("watch_waited", watchWaited));
            _running = false;
            Finish(_errors == 0 && watchWaited && slept && woke && stood && framesHeld && stayedPut && _frames == Sizes.Length ? 0 : 1);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EarthGame.Client
{
    /// <summary>
    /// The controls scenario (M1.5d promise 4; v1's K1c). A keyboard, a mouse and a gamepad are added to the running player,
    /// and for every action the control the controls asset binds to it is pressed on them as a person's finger would press
    /// it, so what is proved is the whole road — the asset's bindings, its actions, the controls frame, and the verb or the
    /// body at the end of it — rather than the scripted seam the other scenarios stand on. The scenario names actions and
    /// the asset's schemes, never a key: the asset is the one owner of which key does what (M1.5a), and a check records the
    /// binding it pressed as the asset states it.
    /// </summary>
    public sealed partial class Recorder
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private Gamepad _pad;
        /// <summary>The developer's panel (M1.D), when the run is a development game's; null otherwise.</summary>
        private DevPanelController _devPanel;
        private int _checks;
        private readonly List<string> _failed = new List<string>();

        /// <summary>
        /// Every action, at the desk and on the gamepad, each checked by what it did: the carrying window, the look, use
        /// picking up and putting down, the places' keys, the wheel and the shoulders choosing the hand, work, the body's
        /// gaits and its jump, and the menu and the screenshot reaching the controls frame, their effects being ones a
        /// windowless run withholds. One frame, the carrying window open with two things carried ("controls-carrying").
        /// The exit is 0 when every check passed, the frame was written and nothing was logged as an error.
        /// </summary>
        private IEnumerator RunControls()
        {
            if (_client == null || _verbs == null || _hud == null || _player == null || _player.Input is ScriptedInputSource)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the controls scenario needs the controls asset, a client, the verbs and the HUD"));
                _running = false;
                Finish(1);
                yield break;
            }
            // A windowless player never has the focus, and without it the Input System ignores a person's devices; these
            // are heard regardless.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _keyboard = InputSystem.AddDevice<Keyboard>("Scenario keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Scenario mouse");
            _pad = InputSystem.AddDevice<Gamepad>("Scenario gamepad");
            _client.IntentAnswered += OnAnswered;
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            EntityView stick = null, cobble = null;
            double until = T + 20.0;
            while (T < until && ((stick = Nearest(DefinitionCatalogue.Stick)) == null || (cobble = Nearest(DefinitionCatalogue.Cobble)) == null)) yield return null;
            if (stick == null || cobble == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no stick and cobble of -eg-items lay within reach"));
                _running = false;
                Finish(1);
                yield break;
            }

            // The carrying window.
            yield return Expect("the carrying window opens", Controls.Carrying, Controls.Desk, () => _hud.CarryingOpen, 1.0, Window);
            yield return Expect("the carrying window closes", Controls.Carrying, Controls.Desk, () => !_hud.CarryingOpen, 1.0, Window);
            yield return Expect("the carrying window opens", Controls.Carrying, Controls.Pad, () => _hud.CarryingOpen, 1.0, Window);
            yield return Expect("the carrying window closes", Controls.Carrying, Controls.Pad, () => !_hud.CarryingOpen, 1.0, Window);

            // The look: the mouse's pixels turn the view by the controls' own degrees a pixel, the stick by its rate.
            float yaw0 = _player.YawDeg;
            string pointer = Turn(20f, 0f);
            yield return Frames(4);
            float turned = Mathf.DeltaAngle(yaw0, _player.YawDeg);
            Check("the view turns", Controls.Desk, pointer, false, pointer != null && Mathf.Abs(turned - 20f) < 1f, "turned " + F2(turned) + "° for 20°");
            yaw0 = _player.YawDeg;
            string stickLook = null;
            yield return Hold(Controls.Pad, 0.5, null, s => stickLook = s, (Controls.Look, null, new Vector2(1f, 0f)));
            turned = Mathf.DeltaAngle(yaw0, _player.YawDeg);
            Check("the view turns", Controls.Pad, stickLook, false, stickLook != null && turned > 30f, "turned " + F2(turned) + "° in half a second");

            // The free look (M1.E, CANON ruling 36): Alt held, the mouse turns the view and not the body; let go, the view
            // glides home to the body's facing.
            if (Bound(Controls.FreeLook, Controls.Desk, null, out InputControl alt, out string altKey))
            {
                float body0 = _player.YawDeg;
                Send(alt.device, To(alt, 1f));
                yield return Frames(2);
                Turn(30f, 0f);
                yield return Frames(4);
                float bodyTurned = Mathf.DeltaAngle(body0, _player.YawDeg);
                double offset = _player.FreeLookOffsetDeg;
                Check("Alt held turns the view and not the body", Controls.Desk, altKey, false,
                      Mathf.Abs(bodyTurned) < 0.5f && Math.Abs(offset - 30.0) < 1.5,
                      "the body turned " + F2(bodyTurned) + "°, the view stands " + F2((float)offset) + "° off it");
                Send(alt.device, To(alt, 0f));
                yield return Wait(FreeLook.ReturnSeconds + 0.3);
                Check("Alt let go brings the view home", Controls.Desk, altKey, false, Math.Abs(_player.FreeLookOffsetDeg) < 0.01,
                      "the view stands " + F2((float)_player.FreeLookOffsetDeg) + "° off the body");
            }
            else Check("Alt held turns the view and not the body", Controls.Desk, null, false, false, "nothing bound to the free look");

            // Use on a thing lying, empty-handed and then with the stick in hand.
            ulong stickId = stick.Id.Value, cobbleId = cobble.Id.Value;
            yield return Aim(stick.Position, stickId);
            yield return Expect("use picks up the stick looked at", Controls.Use, Controls.Desk, () => Carries(stickId), 4.0, Carried);
            yield return Aim(cobble.Position, cobbleId);
            yield return Expect("use picks up the cobble looked at", Controls.Use, Controls.Desk, () => Carries(cobbleId), 4.0, Carried);

            // The hand: the places' keys, the wheel both ways and round its end, and the gamepad's shoulders.
            yield return Expect("the second place's key puts it in hand", Controls.Hand(2), Controls.Desk, () => _client.Carrying.Hand == 2, 3.0, Carried);
            yield return Expect("the wheel towards you moves the hand on, to the empty hand", Controls.HandScroll, Controls.Desk, () => _client.Carrying.Hand == 0, 3.0, Carried, -120f);
            yield return Expect("the wheel towards you wraps round to the first place", Controls.HandScroll, Controls.Desk, () => _client.Carrying.Hand == 1, 3.0, Carried, -120f);
            yield return Expect("the wheel away from you moves the hand back", Controls.HandScroll, Controls.Desk, () => _client.Carrying.Hand == 0, 3.0, Carried, 120f);
            yield return Expect("the hand moves on", Controls.HandNext, Controls.Pad, () => _client.Carrying.Hand == 1, 3.0, Carried);
            yield return Expect("the hand moves back", Controls.HandPrevious, Controls.Pad, () => _client.Carrying.Hand == 0, 3.0, Carried);
            yield return Expect("the first place's key puts it in hand", Controls.Hand(1), Controls.Desk, () => _client.Carrying.Hand == 1, 3.0, Carried);
            yield return Tap(Controls.Carrying, Controls.Desk);
            yield return Wait(0.4);
            yield return Capture("controls-carrying");
            yield return Tap(Controls.Carrying, Controls.Desk);

            // Work is held, not pressed.
            yield return Held("work while held", Controls.Work, Controls.Desk, () => _player.Working);
            yield return Held("work while held", Controls.Work, Controls.Pad, () => _player.Working);

            // Use on the gamepad: the stick in hand put down on the ground a couple of metres ahead.
            Turn(0f, 40f - _player.PitchDeg);
            until = T + 4.0;
            while (T < until && !_verbs.Ground.HasValue) yield return null;
            int carried = CarriedCount();
            yield return Expect("use puts down what is in hand", Controls.Use, Controls.Pad, () => CarriedCount() == carried - 1, 4.0, Carried);

            // The menu, the screenshot and fullscreen do nothing a windowless run can see, so the first two presses are looked
            // for in the controls frame, and fullscreen's in the window's count of what it was asked.
            bool seen = false;
            _player.TakeSeen();
            yield return Expect("the menu reaches the controls", Controls.Menu, Controls.Desk, () => seen |= _player.TakeSeen().Menu, 1.0, () => "a windowless run lets go of no mouse");
            seen = false;
            _player.TakeSeen();
            yield return Expect("the menu reaches the controls", Controls.Menu, Controls.Pad, () => seen |= _player.TakeSeen().Menu, 1.0, () => "a windowless run lets go of no mouse");
            seen = false;
            _player.TakeSeen();
            yield return Expect("the screenshot reaches the controls", Controls.Screenshot, Controls.Desk, () => seen |= _player.TakeSeen().Screenshot, 1.0, () => "a windowless run saves no screenshot");
            int asked = WindowMode.Requested;
            yield return Expect("the fullscreen key reaches the window", Controls.Fullscreen, Controls.Desk, () => WindowMode.Requested > asked, 1.0, () => "a windowless run fills no screen");
            // The developer's switch (M1.E, CANON ruling 39): F2 turns developer mode on and off while the game runs, and the
            // panel's key answers only while it is on. A run started with -eg-dev asked for it at the join and begins on; one
            // without begins off. Either way the switch is pressed both ways, and the run is left as it began, so the flight
            // below is flown in a development run alone.
            bool startedOn = _client.DeveloperMode;
            if (startedOn)
                yield return Expect("the developer's switch turns developer mode off", Controls.DeveloperMode, Controls.Desk,
                                    () => !_client.DeveloperMode && !_player.FlightAllowed, 2.0, Developer);
            Bound(Controls.DevPanel, Controls.Desk, null, out _, out string panelKey);
            yield return Tap(Controls.DevPanel, Controls.Desk);
            yield return Frames(3);
            Check("the dev panel key opens nothing while developer mode is off", Controls.Desk, panelKey, false, !_devPanel.Open, Panel());
            yield return Expect("the developer's switch turns developer mode on", Controls.DeveloperMode, Controls.Desk,
                                () => _client.DeveloperMode && _player.FlightAllowed, 2.0, Developer);
            // The developer's panel (M1.D): opened and closed by its key while developer mode is on.
            yield return Expect("the dev panel key opens the panel", Controls.DevPanel, Controls.Desk, () => _devPanel.Open, 1.0, Panel);
            yield return Expect("the dev panel key closes the panel", Controls.DevPanel, Controls.Desk, () => !_devPanel.Open, 1.0, Panel);
            if (!startedOn)
                yield return Expect("the developer's switch turns developer mode off again", Controls.DeveloperMode, Controls.Desk,
                                    () => !_client.DeveloperMode && !_player.FlightAllowed, 2.0, Developer);

            // The body: walking, running, crouching and the jump, at the desk and on the gamepad.
            Turn(0f, -_player.PitchDeg);
            yield return Frames(3);
            yield return Gaits(Controls.Desk, "up", Vector2.one);
            yield return Gaits(Controls.Pad, null, new Vector2(0f, 1f));
            // A development game's flight (M1.5e), when the run is one (-eg-dev).
            if (_player.FlightAllowed) yield return Flies();

            _client.IntentAnswered -= OnAnswered;
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_mouse);
            InputSystem.RemoveDevice(_pad);
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("checks", _checks).With("failed", string.Join(",", _failed)).With("answers", string.Join(",", _answers))
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)));
            _running = false;
            Finish(_errors == 0 && _checks > 0 && _failed.Count == 0 && _frames == Sizes.Length ? 0 : 1);
        }

        /// <summary>
        /// Walking, running, crouching and the jump in one scheme: the move held forward (a composite's part at the desk, the
        /// stick on the gamepad), then with the sprint, then with the crouch, then the jump pressed.
        /// </summary>
        /// <summary>
        /// How long a gait is held before its speed is read at three fifths of the hold: since M1.5h a body takes about a second
        /// and a third to reach its walk from rest, so the reading at 1.5 s is of the pace and not of the getting there (1.2 s
        /// until then, which read the crouch as three quarters of a walk still on its way up).
        /// </summary>
        private const double GaitHoldS = 2.5;

        private IEnumerator Gaits(string scheme, string forwardPart, Vector2 forward)
        {
            double walk = 0.0, run = 0.0, crouched = 0.0, moved = 0.0;
            Stance stance = Stance.Standing;
            string walked = null, ran = null, crouchedBy = null;
            (string, string, Vector2) move = (Controls.Move, forwardPart, forward);
            yield return Hold(scheme, GaitHoldS, null, s => walked = s, move);
            walk = _heldSpeed;
            moved = _heldAcross;
            Check("the move walks", scheme, walked, false, walked != null && moved >= 0.8 && walk > 0.5, F2(walk) + " m/s, " + F2(moved) + " m");
            yield return Hold(scheme, GaitHoldS, null, s => ran = s, move, (Controls.Sprint, null, Vector2.one));
            run = _heldSpeed;
            Check("the sprint runs", scheme, ran, false, ran != null && run > 1.3 * walk, F2(run) + " m/s against a walk's " + F2(walk));
            yield return Hold(scheme, GaitHoldS, null, s => crouchedBy = s, move, (Controls.Crouch, null, Vector2.one));
            crouched = _heldSpeed;
            stance = _heldStance;
            Check("the crouch crouches", scheme, crouchedBy, false, crouchedBy != null && stance == Stance.Crouching && crouched < 0.75 * walk, stance + ", " + F2(crouched) + " m/s");
            yield return Expect("the jump leaves the ground", Controls.Jump, scheme, () => !_player.State.Grounded, 0.6, Height);
            yield return Wait(1.2);
        }

        /// <summary>
        /// The fly key at the desk (M1.5e): pressed, the founder flies; the jump held, they rise; the crouch held, they sink
        /// through the ground (CANON ruling 25); pressed again, they fly no more and are back on the ground.
        /// </summary>
        private IEnumerator Flies()
        {
            yield return Expect("the fly key takes off", Controls.Fly, Controls.Desk, () => _player.Flying, 1.0, Flying);
            double up = _player.State.Up;
            string rose = null;
            yield return Hold(Controls.Desk, 1.0, null, s => rose = s, (Controls.Jump, null, Vector2.one));
            double risen = _player.State.Up - up;
            Check("the jump held rises in flight", Controls.Desk, rose, false, rose != null && _player.Flying && risen > 5.0, "rose " + F2(risen) + " m");
            bool under = Under();
            string sank = null;
            yield return Hold(Controls.Desk, 3.0, Under, s => sank = s, (Controls.Crouch, null, Vector2.one));
            Check("the crouch held sinks through the ground in flight", Controls.Desk, sank, under, sank != null && _heldEffect, Flying());
            yield return Expect("the fly key sets the founder back on the ground", Controls.Fly, Controls.Desk,
                                () => !_player.Flying && _player.State.Grounded, 8.0, Flying);
        }

        /// <summary>Whether the founder is under the ground the client holds, by half a metre (M1.5e's noclip).</summary>
        private bool Under()
        {
            double ground = GroundHere();
            return !double.IsNaN(ground) && _player.State.Up < ground - 0.5;
        }

        private double GroundHere() => _player.Ground != null ? _player.Ground.HeightAt(_player.State.East, _player.State.North) : double.NaN;

        private string Flying() => (_player.Flying ? "flying, " : "not flying, ") + Height() + ", the ground at " + F2(GroundHere());

        private string Panel() => _devPanel != null && _devPanel.Open ? "the panel is open" : "the panel is closed";

        private string Developer() => "developer mode " + (_client.DeveloperMode ? "on" : "off") + (_client.DeveloperModeRefused ? ", refused" : "")
                                       + ", flight " + (_player.FlightAllowed ? "allowed" : "not allowed");

        /// <summary>
        /// An action pressed by the control the asset binds to it in a scheme, and its effect waited for. The check passes
        /// when the effect holds after the press and did not before it, so a check that could not have failed fails.
        /// </summary>
        private IEnumerator Expect(string name, string action, string scheme, Func<bool> holds, double withinS, Func<string> saw, float value = 1f)
        {
            if (!Bound(action, scheme, null, out InputControl control, out string binding))
            {
                Check(name, scheme, null, false, false, action + ": the asset binds nothing the scenario's devices have");
                yield break;
            }
            bool before = holds();
            Send(control.device, To(control, value));
            yield return Frames(2);
            Send(control.device, To(control, 0f));
            yield return Frames(2);
            double until = T + withinS;
            bool after = holds();
            while (!after && T < until)
            {
                yield return null;
                after = holds();
            }
            Check(name, scheme, binding, before, after, saw());
        }

        /// <summary>
        /// An action held for a moment and let go: its effect must hold while it is held and not before, and must stop when
        /// it is let go.
        /// </summary>
        private IEnumerator Held(string name, string action, string scheme, Func<bool> holds)
        {
            string binding = null;
            bool before = holds();
            yield return Hold(scheme, 0.4, holds, s => binding = s, (action, null, Vector2.one));
            bool during = _heldEffect, after = holds();
            Check(name, scheme, binding, before, during, "held: " + during + ", let go: " + after);
            Check(name.Replace("while held", "stops when let go"), scheme, binding, !during, !after, "held: " + during + ", let go: " + after);
        }

        private bool _heldEffect;
        private double _heldSpeed;
        private double _heldAcross;
        private Stance _heldStance;

        /// <summary>
        /// Controls held together for a while — each an action, the part of its composite when it is one, and the value to
        /// hold it at (a stick's direction, or a button's press as its x) — then let go. Part-way through, the body's speed
        /// and stance and whether a probe holds are kept for the caller, with how far the body went; the bindings pressed
        /// are handed to it, or null when the asset binds one of the actions to nothing the scenario has.
        /// </summary>
        private IEnumerator Hold(string scheme, double seconds, Func<bool> probe, Action<string> pressed, params (string Action, string Part, Vector2 Value)[] holds)
        {
            Dictionary<InputDevice, List<Action<InputEventPtr>>> press = new Dictionary<InputDevice, List<Action<InputEventPtr>>>();
            Dictionary<InputDevice, List<Action<InputEventPtr>>> release = new Dictionary<InputDevice, List<Action<InputEventPtr>>>();
            List<string> bindings = new List<string>();
            foreach ((string action, string part, Vector2 value) in holds)
            {
                if (!Bound(action, scheme, part, out InputControl control, out string binding))
                {
                    pressed(null);
                    yield break;
                }
                bindings.Add(binding);
                bool stick = control.valueType == typeof(Vector2);
                Add(press, control.device, stick ? To(control, value) : To(control, value.x));
                Add(release, control.device, stick ? To(control, Vector2.zero) : To(control, 0f));
            }
            Double3 start = _player.State.Feet;
            foreach (KeyValuePair<InputDevice, List<Action<InputEventPtr>>> p in press) Send(p.Key, p.Value.ToArray());
            yield return Wait(seconds * 0.6);
            _heldSpeed = _player.State.HorizontalSpeed;
            _heldStance = _player.State.Stance;
            _heldEffect = probe != null && probe();
            yield return Wait(seconds * 0.4);
            foreach (KeyValuePair<InputDevice, List<Action<InputEventPtr>>> r in release) Send(r.Key, r.Value.ToArray());
            yield return Wait(0.3);
            _heldAcross = Across(start, _player.State.Feet);
            pressed(string.Join(" + ", bindings));
        }

        private static void Add(Dictionary<InputDevice, List<Action<InputEventPtr>>> writes, InputDevice device, Action<InputEventPtr> write)
        {
            if (!writes.TryGetValue(device, out List<Action<InputEventPtr>> list)) writes[device] = list = new List<Action<InputEventPtr>>();
            list.Add(write);
        }

        /// <summary>One check, recorded with what was seen before and after; passed only when it was false before and true after.</summary>
        private void Check(string name, string scheme, string binding, bool before, bool after, string saw)
        {
            _checks++;
            bool ok = binding != null && !before && after;
            if (!ok) _failed.Add(name + " (" + scheme + ")");
            _log.Record(T, Tick, "control", new JsonObject().With("name", name).With("scheme", scheme).With("control", binding ?? "nothing bound")
                .With("before", before).With("after", after).With("ok", ok).With("saw", saw ?? string.Empty));
            Debug.Log("[controls] " + (ok ? "ok " : "FAILED ") + name + " (" + scheme + ", " + (binding ?? "nothing bound") + "): " + saw);
        }

        /// <summary>Turns the view to a thing with the mouse and waits until the crosshair is on it.</summary>
        private IEnumerator Aim(Double3 at, ulong id)
        {
            Double3 eye = _player.Eye;
            double dx = at.X - eye.X, dz = at.Z - eye.Z;
            float yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            float pitch = (float)(Math.Atan2(eye.Y - at.Y, Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
            Turn(Mathf.DeltaAngle(_player.YawDeg, yaw), pitch - _player.PitchDeg);
            double until = T + 4.0;
            while (T < until && (_verbs.Target == null || _verbs.Target.Id.Value != id)) yield return null;
        }

        /// <summary>
        /// A turn of the view by the control the desk's look is bound to, right and down in degrees, sent as the pixels the
        /// controls make those degrees of; the binding it moved, or null when the asset binds none the scenario has.
        /// </summary>
        private string Turn(float rightDeg, float downDeg)
        {
            if (!Bound(Controls.Look, Controls.Desk, null, out InputControl pointer, out string binding)) return null;
            float perPixel = InputSystemSource.MouseDegreesPerPixel;
            Send(pointer.device, To(pointer, new Vector2(rightDeg / perPixel, -downDeg / perPixel)));
            return binding;
        }

        /// <summary>An action pressed and let go by its binding in a scheme, its effect not checked.</summary>
        private IEnumerator Tap(string action, string scheme)
        {
            if (!Bound(action, scheme, null, out InputControl control, out _)) yield break;
            Send(control.device, To(control, 1f));
            yield return Frames(2);
            Send(control.device, To(control, 0f));
            yield return Frames(2);
        }

        /// <summary>
        /// The control the controls asset binds to an action in a scheme, found on the scenario's own devices: for a
        /// composite, the binding of the part named ("up" of the move); and the binding's path, as the asset states it.
        /// </summary>
        private bool Bound(string action, string scheme, string part, out InputControl control, out string binding)
        {
            control = null;
            binding = null;
            InputActionMap map = InputSystem.actions != null ? InputSystem.actions.FindActionMap(Controls.Map, false) : null;
            InputAction found = map != null ? map.FindAction(action, false) : null;
            if (found == null) return false;
            InputDevice[] devices = scheme == Controls.Pad ? new InputDevice[] { _pad } : new InputDevice[] { _keyboard, _mouse };
            foreach (InputBinding b in found.bindings)
            {
                if (b.isComposite || b.isPartOfComposite != (part != null) || (part != null && b.name != part)) continue;
                if (string.IsNullOrEmpty(b.groups) || Array.IndexOf(b.groups.Split(';'), scheme) < 0) continue;
                foreach (InputDevice device in devices)
                {
                    InputControl c = InputControlPath.TryFindControl(device, b.effectivePath);
                    if (c == null) continue;
                    control = c;
                    binding = b.effectivePath;
                    return true;
                }
            }
            return false;
        }

        /// <summary>One state event for a device: its present state with the writes made to it, so what else it holds stays held.</summary>
        private static void Send(InputDevice device, params Action<InputEventPtr>[] writes)
        {
            using (NativeArray<byte> buffer = StateEvent.From(device, out InputEventPtr eventPtr))
            {
                foreach (Action<InputEventPtr> write in writes) write(eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }

        private static Action<InputEventPtr> To(InputControl control, float value) => e => control.WriteValueIntoEvent(value, e);

        private static Action<InputEventPtr> To(InputControl control, Vector2 value) => e => control.WriteValueIntoEvent(value, e);

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static double Across(Double3 a, Double3 b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));

        private int CarriedCount() => _client.Carrying.Things != null ? _client.Carrying.Things.Length : 0;

        private string Carried() => "carrying " + CarriedCount() + ", hand " + _client.Carrying.Hand;

        private string Window() => _hud.CarryingOpen ? "open" : "closed";

        private string Height() => "up " + F2(_player.State.Up) + (_player.State.Grounded ? ", on the ground" : ", in the air");

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

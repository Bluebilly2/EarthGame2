using System.Collections.Generic;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EarthGame.Client
{
    /// <summary>
    /// What the founder's hands did this frame (M1.5a, CANON's verb rule of 2026-08-26): the stick and the look, the
    /// buttons held, and the presses, each true or set for the one frame it happened in. Look is a delta in degrees;
    /// move is a stick in the founder's own frame (x right, y forward).
    /// </summary>
    public struct ControlsFrame
    {
        public Vector2 Move;
        public Vector2 LookDeltaDeg;
        public bool Jump;
        public bool Sprint;
        public bool Crouch;
        /// <summary>Left mouse held: work on what is looked at (the work verbs are M2's).</summary>
        public bool Work;
        /// <summary>Right mouse pressed: use what is looked at, with what is in hand.</summary>
        public bool Use;
        /// <summary>Tab pressed: the carrying window opens or closes.</summary>
        public bool Carrying;
        /// <summary>A key 1–9 pressed: that place is to be the hand; 0 when none was.</summary>
        public int HandPlace;
        /// <summary>The wheel or a shoulder: the hand moves on (+1) or back (-1) through what is carried.</summary>
        public int HandStep;
        /// <summary>Escape pressed: the controls let go of the mouse, or take it back.</summary>
        public bool Menu;
        public bool Screenshot;
    }

    /// <summary>
    /// Where the founder's intentions come from this frame: the controls asset when a person is at them, a script when a
    /// scenario is (ARCHITECTURE §8: the scripted-input seam).
    /// </summary>
    public interface IPlayerInputSource
    {
        void Sample(float dt, out ControlsFrame frame);
    }

    /// <summary>
    /// The actions the code reads, by name. The controls asset (<c>Assets/InputSystem_Actions.inputactions</c>, Unity's
    /// project-wide actions, its "Player" map) is the one owner of every binding, and code names actions, never keys
    /// (M1.5a): the build refuses an asset that lacks one of these (CIBuild), and a source rule refuses a key in code.
    /// </summary>
    public static class Controls
    {
        public const string Map = "Player";
        public const string Move = "Move";
        public const string Look = "Look";
        public const string Jump = "Jump";
        public const string Sprint = "Sprint";
        public const string Crouch = "Crouch";
        public const string Work = "Work";
        public const string Use = "Use";
        public const string Carrying = "Carrying";
        public const string HandScroll = "HandScroll";
        public const string HandNext = "HandNext";
        public const string HandPrevious = "HandPrevious";
        public const string Menu = "Menu";
        public const string Screenshot = "Screenshot";

        /// <summary>The action for a place's key: "Hand1" to "Hand9".</summary>
        public static string Hand(int place) => "Hand" + place;

        /// <summary>Whether an action is the gamepad's alone: the shoulders step the hand as the wheel does at the desk, and need no key of their own.</summary>
        public static bool IsGamepadOnly(string name) => name == HandNext || name == HandPrevious;

        public static IEnumerable<string> Names
        {
            get
            {
                yield return Move;
                yield return Look;
                yield return Jump;
                yield return Sprint;
                yield return Crouch;
                yield return Work;
                yield return Use;
                yield return Carrying;
                yield return HandScroll;
                yield return HandNext;
                yield return HandPrevious;
                for (int p = 1; p <= Hands.Places; p++) yield return Hand(p);
                yield return Menu;
                yield return Screenshot;
            }
        }

        /// <summary>The actions an asset's map lacks; every one of them when there is no asset or no map.</summary>
        public static List<string> Missing(InputActionAsset asset)
        {
            InputActionMap map = asset != null ? asset.FindActionMap(Map, false) : null;
            List<string> missing = new List<string>();
            foreach (string name in Names)
                if (map == null || map.FindAction(name, false) == null) missing.Add(name);
            return missing;
        }
    }

    /// <summary>
    /// The controls asset, read every frame. An action the asset lacks is named in the log once and reads as untouched:
    /// there are no keys of the code's own to fall back on (until 2026-09-11 there were, and a build that lost the asset
    /// moved on keys nobody had chosen), and the build refuses an asset that is not whole.
    /// </summary>
    public sealed class InputSystemSource : IPlayerInputSource
    {
        private const float MouseDegreesPerPixel = 0.08f;
        private const float StickDegreesPerSecond = 180f;

        private readonly InputAction _move;
        private readonly InputAction _look;
        private readonly InputAction _jump;
        private readonly InputAction _sprint;
        private readonly InputAction _crouch;
        private readonly InputAction _work;
        private readonly InputAction _use;
        private readonly InputAction _carrying;
        private readonly InputAction _handScroll;
        private readonly InputAction _handNext;
        private readonly InputAction _handPrevious;
        private readonly InputAction _menu;
        private readonly InputAction _screenshot;
        private readonly InputAction[] _hand = new InputAction[Hands.Places];

        public InputSystemSource()
        {
            InputActionAsset asset = InputSystem.actions;
            List<string> missing = Controls.Missing(asset);
            if (missing.Count > 0) Debug.LogError("[input] the controls asset lacks " + string.Join(", ", missing) + "; those controls do nothing");
            InputActionMap map = asset != null ? asset.FindActionMap(Controls.Map, false) : null;
            _move = Find(map, Controls.Move);
            _look = Find(map, Controls.Look);
            _jump = Find(map, Controls.Jump);
            _sprint = Find(map, Controls.Sprint);
            _crouch = Find(map, Controls.Crouch);
            _work = Find(map, Controls.Work);
            _use = Find(map, Controls.Use);
            _carrying = Find(map, Controls.Carrying);
            _handScroll = Find(map, Controls.HandScroll);
            _handNext = Find(map, Controls.HandNext);
            _handPrevious = Find(map, Controls.HandPrevious);
            _menu = Find(map, Controls.Menu);
            _screenshot = Find(map, Controls.Screenshot);
            for (int p = 1; p <= Hands.Places; p++) _hand[p - 1] = Find(map, Controls.Hand(p));
            map?.Enable();
        }

        private static InputAction Find(InputActionMap map, string name) => map?.FindAction(name, false);

        public void Sample(float dt, out ControlsFrame frame)
        {
            frame = default;
            if (_move != null) frame.Move = _move.ReadValue<Vector2>();
            if (_look != null)
            {
                Vector2 look = _look.ReadValue<Vector2>();
                // A mouse reports pixels since last frame; a stick reports a deflection. The active control decides.
                bool stick = _look.activeControl != null && _look.activeControl.device is Gamepad;
                frame.LookDeltaDeg = stick ? look * (StickDegreesPerSecond * dt) : look * MouseDegreesPerPixel;
            }
            frame.Jump = Pressed(_jump);
            frame.Sprint = Held(_sprint);
            frame.Crouch = Held(_crouch);
            frame.Work = Held(_work);
            frame.Use = Pressed(_use);
            frame.Carrying = Pressed(_carrying);
            for (int p = 1; p <= Hands.Places; p++)
                if (Pressed(_hand[p - 1])) frame.HandPlace = p;
            // The wheel turned towards the founder moves the hand on, as a row of places is read.
            float scroll = _handScroll != null ? _handScroll.ReadValue<float>() : 0f;
            frame.HandStep = (scroll < 0f ? 1 : scroll > 0f ? -1 : 0) + (Pressed(_handNext) ? 1 : 0) - (Pressed(_handPrevious) ? 1 : 0);
            frame.Menu = Pressed(_menu);
            frame.Screenshot = Pressed(_screenshot);
        }

        private static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();

        private static bool Held(InputAction action) => action != null && action.IsPressed();
    }

    /// <summary>
    /// A scenario's hands: whatever the script sets is what the founder does until it is changed, and a press is done
    /// once. The facing is the camera's own: yaw clockwise from north, pitch positive looking down (2026-09-11; before
    /// then a script's pitch was the mouse's push, positive looking up, and a scenario that asked for the ground ahead
    /// looked at the sky).
    /// </summary>
    public sealed class ScriptedInputSource : IPlayerInputSource
    {
        public Vector2 Move;
        public float YawTargetDeg;
        public float PitchTargetDeg;
        public bool Sprint;
        public bool Crouch;
        public bool Work;
        public float TurnDegreesPerSecond = 120f;
        private ControlsFrame _presses;

        /// <summary>The current facing, kept here so the script can turn towards a target at a stated rate.</summary>
        public float YawDeg;
        public float PitchDeg;

        public void Jump() => _presses.Jump = true;
        public void Use() => _presses.Use = true;
        public void ToggleCarrying() => _presses.Carrying = true;
        public void Hold(int place) => _presses.HandPlace = place;
        public void StepHand(int step) => _presses.HandStep += step;

        public void Sample(float dt, out ControlsFrame frame)
        {
            frame = _presses;
            _presses = default;
            frame.Move = Move;
            float maxTurn = TurnDegreesPerSecond * dt;
            float dyaw = Mathf.Clamp(Mathf.DeltaAngle(YawDeg, YawTargetDeg), -maxTurn, maxTurn);
            float dpitch = Mathf.Clamp(PitchTargetDeg - PitchDeg, -maxTurn, maxTurn);
            YawDeg += dyaw;
            PitchDeg += dpitch;
            // The body takes a look's push as the mouse gives it, up positive; the script's pitch is the camera's.
            frame.LookDeltaDeg = new Vector2(dyaw, -dpitch);
            frame.Sprint = Sprint;
            frame.Crouch = Crouch;
            frame.Work = Work;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace EarthGame.Client
{
    /// <summary>
    /// Where the founder's intentions come from this frame: the Input System when a person is at the controls,
    /// a script when a scenario is (ARCHITECTURE §8: the scripted-input seam). Look is a delta in degrees; move is
    /// a stick in the founder's own frame (x right, y forward); jump is a press latched until read.
    /// </summary>
    public interface IPlayerInputSource
    {
        void Sample(float dt, out Vector2 move, out Vector2 lookDeltaDeg, out bool jump, out bool sprint, out bool crouch);
    }

    /// <summary>
    /// The project's one actions asset (Unity's project-wide actions, "Player" map) with a code-built fallback so
    /// a build that lost the asset still moves and says so in the log.
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
        private bool _jumpLatched;

        public InputSystemSource()
        {
            InputActionAsset asset = InputSystem.actions;
            InputActionMap player = asset != null ? asset.FindActionMap("Player", false) : null;
            if (player != null)
            {
                _move = player.FindAction("Move", false);
                _look = player.FindAction("Look", false);
                _jump = player.FindAction("Jump", false);
                _sprint = player.FindAction("Sprint", false);
                _crouch = player.FindAction("Crouch", false);
                player.Enable();
            }
            if (_move == null || _look == null || _jump == null || _sprint == null || _crouch == null)
            {
                Debug.LogWarning("[input] the project-wide actions asset has no complete Player map; using the built-in bindings");
                InputActionMap map = new InputActionMap("Player-fallback");
                _move = map.AddAction("Move", InputActionType.Value);
                _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                _move.AddBinding("<Gamepad>/leftStick");
                _look = map.AddAction("Look", InputActionType.Value);
                _look.AddBinding("<Mouse>/delta");
                _look.AddBinding("<Gamepad>/rightStick");
                _jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
                _jump.AddBinding("<Gamepad>/buttonSouth");
                _sprint = map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
                _sprint.AddBinding("<Gamepad>/leftStickPress");
                _crouch = map.AddAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
                _crouch.AddBinding("<Gamepad>/buttonEast");
                map.Enable();
            }
            _jump.performed += _ => _jumpLatched = true;
        }

        public void Sample(float dt, out Vector2 move, out Vector2 lookDeltaDeg, out bool jump, out bool sprint, out bool crouch)
        {
            move = _move.ReadValue<Vector2>();
            Vector2 look = _look.ReadValue<Vector2>();
            // A mouse reports pixels since last frame; a stick reports a deflection. The active control decides.
            bool stick = _look.activeControl != null && _look.activeControl.device is Gamepad;
            lookDeltaDeg = stick ? look * (StickDegreesPerSecond * dt) : look * MouseDegreesPerPixel;
            jump = _jumpLatched;
            _jumpLatched = false;
            sprint = _sprint.IsPressed();
            crouch = _crouch.IsPressed();
        }
    }

    /// <summary>A scenario's hands: whatever the recorder sets is what the founder does until it is changed.</summary>
    public sealed class ScriptedInputSource : IPlayerInputSource
    {
        public Vector2 Move;
        public float YawTargetDeg;
        public float PitchTargetDeg;
        public bool Sprint;
        public bool Crouch;
        public float TurnDegreesPerSecond = 120f;
        private bool _jumpLatched;

        /// <summary>The current facing, kept here so the script can turn towards a target at a stated rate.</summary>
        public float YawDeg;
        public float PitchDeg;

        public void Jump() => _jumpLatched = true;

        public void Sample(float dt, out Vector2 move, out Vector2 lookDeltaDeg, out bool jump, out bool sprint, out bool crouch)
        {
            move = Move;
            float maxTurn = TurnDegreesPerSecond * dt;
            float dyaw = Mathf.Clamp(Mathf.DeltaAngle(YawDeg, YawTargetDeg), -maxTurn, maxTurn);
            float dpitch = Mathf.Clamp(PitchTargetDeg - PitchDeg, -maxTurn, maxTurn);
            YawDeg += dyaw;
            PitchDeg += dpitch;
            lookDeltaDeg = new Vector2(dyaw, dpitch);
            jump = _jumpLatched;
            _jumpLatched = false;
            sprint = Sprint;
            crouch = Crouch;
        }
    }
}

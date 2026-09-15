using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The founder's body on the client: the engine's <see cref="Mover"/> stepped at Unity's fixed rate (50 Hz)
    /// over <see cref="PhysxCollision"/>, mouse look sampled every render frame, the camera at eye height with
    /// its vertical motion smoothed (plan §4.8, CANON ruling 18), and the body reported to the server at its
    /// tick rate. A Correction from the server is adopted at once and counted; on a legal walk every one is a
    /// false positive the N2 budget charges. The eye's height is the engine's (<see cref="MoverConfig.EyeHeight"/>)
    /// since M1.5a, because the server measures a verb's reach from it. Since M1.5c the stride (<see cref="Stride"/>)
    /// is laid on top of the smoothed eye, a footfall's dip and a sway across, unless <see cref="Still"/>; in the water
    /// since M1.5e, a stroke's. A development game's founder can fly (M1.5e, <see cref="Flight"/>): the fly key takes the
    /// body out of the mover's hands, through the ground and all (CANON ruling 25), and gives it back.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        private const float SendIntervalSeconds = 0.05f;
        private const float EyeSmoothingPerSecond = 14f;
        private const float MaxPitchDeg = 89f;
        /// <summary>The soft boundary: the mover is held this far inside the region's edge until the vignette exists.</summary>
        private const double EdgeMarginM = 2.0;

        private IPlayerInputSource _input;
        private IWorldCollision _collision;
        private GameClient _client;
        private MoverConfig _config;
        /// <summary>What the founder's body can do, 0 to 1 (FP.1): the server's word, taken for the walk from the next step.</summary>
        private double _workCapacity = 1.0;
        private Region _region;
        private Camera _camera;
        private Vector2 _move;
        private bool _jumpQueued;
        private bool _flyQueued;
        private bool _rise;
        private bool _sprint;
        private bool _crouch;
        private float _sinceSend;
        private float _eyeY;
        private bool _eyeInitialised;
        private ControlsFrame _presses;
        private ControlsFrame _seen;
        private readonly Stride _stride = new Stride();

        public MoverState State;
        public float YawDeg;
        public float PitchDeg;

        /// <summary>
        /// The camera without the stride's dip and sway (M1.5c, set by <c>-eg-still</c>): the camera the owner found good
        /// in ruling 18, kept so that he can play the two and say which stands.
        /// </summary>
        public bool Still;

        /// <summary>
        /// A development game's (M1.5e, set by <c>-eg-dev</c>): the fly key takes the founder off the ground. The game's SOLO
        /// server allows the flight; any other corrects it back.
        /// </summary>
        public bool FlightAllowed;

        /// <summary>Whether the founder is flying (M1.5e).</summary>
        public bool Flying { get; private set; }

        /// <summary>
        /// Whether the flight passes through the ground and the trunks (ruling 25) or is stopped by them (M1.D, ruling 30's
        /// switch under the flight's). On, as the flight was built; the developer's panel turns it off.
        /// </summary>
        public bool Noclip { get; private set; } = true;

        /// <summary>A foot fell (M1.5c) on the ground the body covered, or a stroke was swum (M1.5e); the client sounds it.</summary>
        public event Action<Footfall> Stepped;

        /// <summary>
        /// While true the body neither steps nor reports: set until the ground under it exists (the streamed tile
        /// is built), so the founder does not fall through the coarse region while the kilometre is on its way.
        /// The first run of the join scenario (2026-09-08) fell 1.5 m onto the sunk coarse terrain in the second
        /// before the tiles arrived and was corrected every tick thereafter.
        /// </summary>
        public bool Frozen;

        /// <summary>
        /// The ground as the client holds it, for the one rule the mover does not have: a corrected body that the
        /// server holds below this ground (its tolerance allows a metre) is lifted onto it, or PhysX keeps it
        /// under the terrain forever. A flight that ends under it sets the founder on it too (M1.5e, ruling 25).
        /// </summary>
        public IHeightSource Ground;

        public int Corrections { get; private set; }
        public string LastCorrectionReason { get; private set; } = string.Empty;
        public uint MovesSent { get; private set; }

        /// <summary>A correction as applied: how far it moved the body, the move it answered, and why.</summary>
        public event Action<double, uint, string> CorrectionApplied;

        /// <summary>The scenario's hands when a scenario is driving, else null.</summary>
        public ScriptedInputSource Script => _input as ScriptedInputSource;

        public IPlayerInputSource Input
        {
            get => _input;
            set => _input = value;
        }

        /// <summary>Whether the left mouse is held this frame (work, which M2's verbs will give something to do).</summary>
        public bool Working { get; private set; }

        /// <summary>Where the founder's eye is as the server measures a verb's reach from it: the body's feet raised by the stance's eye height, unsmoothed.</summary>
        public Double3 Eye => new Double3(State.East, State.Up + (_config ?? MoverConfig.Default).EyeHeight(State.Stance), State.North);

        /// <summary>The water's surface where the founder is, as their body knows it; NaN where there is none (M1.5e: the swim scenario holds the eye against it).</summary>
        public double WaterSurface => _collision != null ? _collision.WaterSurfaceAt(State.East, State.North) : double.NaN;

        /// <summary>
        /// Whether the controls hold the view: always for a script or a windowless run, and for a person while the mouse is
        /// captured. Escape lets the mouse go (M1.5a), and a look at another window then turns nothing.
        /// </summary>
        public bool HoldsView => _input is ScriptedInputSource || Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked;

        /// <summary>The presses since the last take and the buttons held now, taken once a frame by whoever acts on them.</summary>
        public ControlsFrame TakePresses()
        {
            ControlsFrame taken = _presses;
            _presses = default;
            taken.Work = Working;
            taken.Sprint = _sprint;
            taken.Crouch = _crouch;
            return taken;
        }

        public double WorkCapacity => _workCapacity;

        /// <summary>The body's work capacity as the server tells it (FP.1); anything outside 0..1 is clamped, NaN is full.</summary>
        public void SetWorkCapacity(double capacity01) =>
            _workCapacity = double.IsNaN(capacity01) ? 1.0 : System.Math.Min(1.0, System.Math.Max(0.0, capacity01));

        public void Attach(GameClient client, IWorldCollision collision, MoverConfig config, Region region, Camera camera,
                           IPlayerInputSource input, Double3 spawn, float yawDeg, float pitchDeg)
        {
            _client = client;
            _collision = collision;
            _config = config ?? MoverConfig.Default;
            _region = region;
            _camera = camera;
            _input = input;
            State = MoverState.AtRest(spawn.X, spawn.Y, spawn.Z);
            YawDeg = yawDeg;
            PitchDeg = pitchDeg;
            if (_input is ScriptedInputSource scripted)
            {
                scripted.YawDeg = yawDeg;
                scripted.PitchDeg = pitchDeg;
                scripted.YawTargetDeg = yawDeg;
                scripted.PitchTargetDeg = pitchDeg;
            }
            transform.position = ToUnity(State.Feet);
            if (_client != null) _client.Corrected += OnCorrected;
        }

        private void OnDestroy()
        {
            if (_client != null) _client.Corrected -= OnCorrected;
        }

        private void OnCorrected(CorrectionMessage correction)
        {
            double dx = correction.Body.East - State.East, dy = correction.Body.Up - State.Up, dz = correction.Body.North - State.North;
            double displacement = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            State = correction.Body;
            if (Ground != null)
            {
                double ground = Ground.HeightAt(State.East, State.North);
                if (!double.IsNaN(ground) && State.Up < ground - 0.02)
                {
                    State.Up = ground + 0.01;
                    State.Grounded = false;
                }
            }
            Corrections++;
            LastCorrectionReason = correction.Reason ?? string.Empty;
            transform.position = ToUnity(State.Feet);
            Debug.Log("[player] corrected (move " + correction.Sequence + ", " + displacement.ToString("0.00") + " m): " + LastCorrectionReason);
            CorrectionApplied?.Invoke(displacement, correction.Sequence, LastCorrectionReason);
        }

        /// <summary>A rejoin: the body reports to a new client from where the server remembered it.</summary>
        public void Rebind(GameClient client, Double3 spawn)
        {
            if (_client != null) _client.Corrected -= OnCorrected;
            _client = client;
            if (_client != null) _client.Corrected += OnCorrected;
            State = MoverState.AtRest(spawn.X, spawn.Y, spawn.Z);
            transform.position = ToUnity(State.Feet);
            _sinceSend = 0f;
        }

        private void Update()
        {
            if (_input == null) return;
            _input.Sample(Time.unscaledDeltaTime, out ControlsFrame f);
            if (HoldsView)
            {
                YawDeg = Mathf.Repeat(YawDeg + f.LookDeltaDeg.x, 360f);
                PitchDeg = Mathf.Clamp(PitchDeg - f.LookDeltaDeg.y, -MaxPitchDeg, MaxPitchDeg);
            }
            _move = f.Move;
            if (f.Jump) _jumpQueued = true;
            if (f.Fly) _flyQueued = true;
            _rise = f.Rise;
            _sprint = f.Sprint;
            _crouch = f.Crouch;
            Working = f.Work;
            // Presses wait here until taken, so none is lost to the order Unity updates components in.
            _presses.Use |= f.Use;
            _presses.Carrying |= f.Carrying;
            if (f.HandPlace != 0) _presses.HandPlace = f.HandPlace;
            _presses.HandStep += f.HandStep;
            _presses.Menu |= f.Menu;
            _presses.Screenshot |= f.Screenshot;
            _presses.DevPanel |= f.DevPanel;
            _seen.Jump |= f.Jump;
            _seen.Use |= f.Use;
            _seen.Carrying |= f.Carrying;
            if (f.HandPlace != 0) _seen.HandPlace = f.HandPlace;
            _seen.HandStep += f.HandStep;
            _seen.Menu |= f.Menu;
            _seen.Screenshot |= f.Screenshot;
            _seen.Fly |= f.Fly;
            _seen.DevPanel |= f.DevPanel;
        }

        /// <summary>
        /// Takes off, or comes down (M1.5e): the fly key and the developer's panel both come here (M1.D). Coming down, the
        /// founder falls from where they are, and is set on the ground when the flight left them under it (ruling 25's
        /// noclip), which nothing else would lift them out of. A game not for development never flies.
        /// </summary>
        public void SetFlying(bool flying)
        {
            if (!FlightAllowed || Flying == flying) return;
            Flying = flying;
            if (!Flying)
            {
                State.VelEast = State.VelUp = State.VelNorth = 0.0;
                LiftOutOfTheGround();
            }
            Debug.Log("[player] " + (Flying ? "flying" : "flying no more"));
        }

        /// <summary>Lets the flight through the ground, or has it stopped there (M1.D): a founder under the ground when it is stopped is set on it.</summary>
        public void SetNoclip(bool noclip)
        {
            if (Noclip == noclip) return;
            Noclip = noclip;
            if (!noclip) LiftOutOfTheGround();
            Debug.Log("[player] noclip " + (noclip ? "on" : "off"));
        }

        private void LiftOutOfTheGround()
        {
            double below = Ground != null ? Ground.HeightAt(State.East, State.North) : double.NaN;
            if (!double.IsNaN(below) && State.Up < below)
            {
                State.Up = below + 0.01;
                State.Grounded = false;
            }
        }

        /// <summary>
        /// Every press the controls delivered since this was last called: how the controls scenario sees a press reach the
        /// body when its effect is one a windowless run withholds (M1.5d).
        /// </summary>
        public ControlsFrame TakeSeen()
        {
            ControlsFrame seen = _seen;
            _seen = default;
            return seen;
        }

        private void FixedUpdate()
        {
            if (_collision == null || Frozen) return;
            double dt = Time.fixedDeltaTime;
            // The wish is the stick turned into the world: yaw 0 faces north (+Z), 90 faces east (+X).
            double yaw = YawDeg * GeoMath.DegToRad;
            double sin = Math.Sin(yaw), cos = Math.Cos(yaw);
            MoverInput input = default;
            input.WishEast = cos * _move.x + sin * _move.y;
            input.WishNorth = -sin * _move.x + cos * _move.y;
            input.Jump = _jumpQueued;
            input.Sprint = _sprint;
            input.Crouch = _crouch;
            _jumpQueued = false;
            if (_flyQueued)
            {
                _flyQueued = false;
                SetFlying(!Flying);
            }

            // A flight with noclip off is stopped by what the walk is stopped by: the same collision, swept the same way (M1.D).
            State = Flying ? Flight.Step(State, _move.x, _move.y, _rise, _crouch, _sprint, YawDeg, PitchDeg, dt, Noclip ? null : _collision, _config)
                           : Mover.Step(State, input, dt, _collision, _config, _workCapacity);
            if (_region != null)
            {
                double edge = _region.HalfExtentM - EdgeMarginM;
                if (State.East > edge) State.East = edge; else if (State.East < -edge) State.East = -edge;
                if (State.North > edge) State.North = edge; else if (State.North < -edge) State.North = -edge;
            }
            transform.position = ToUnity(State.Feet);

            _sinceSend += (float)dt;
            if (_client != null && _client.State == ClientState.Connected && _sinceSend >= SendIntervalSeconds - 1e-4f)
            {
                // Carry the remainder: 50 ms is two and a half fixed steps, and resetting to zero sent every third.
                _sinceSend -= SendIntervalSeconds;
                _client.SendMove(input, YawDeg, PitchDeg, State);
                MovesSent++;
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            // The stride walks the ground the body covered in this frame's game time, or swims the water, which is what
            // steps the body; a frozen body covers none.
            if (_stride.Advance(Time.deltaTime, Frozen ? 0.0 : State.HorizontalSpeed, Frozen || State.Grounded, !Frozen && State.Swimming, out Footfall footfall))
                Stepped?.Invoke(footfall);
            float eye = (float)(_config ?? MoverConfig.Default).EyeHeight(State.Stance);
            float targetY = (float)State.Up + eye;
            if (!_eyeInitialised)
            {
                _eyeY = targetY;
                _eyeInitialised = true;
            }
            else
            {
                // Smoothed vertically only: steps and landings ease, but the feet never lag the hands sideways.
                float k = 1f - Mathf.Exp(-EyeSmoothingPerSecond * Time.unscaledDeltaTime);
                _eyeY = Mathf.Lerp(_eyeY, targetY, k);
                // A correction or a fall: no long slide. Nor in flight, which the smoothing would trail by more than that.
                if (Flying || Mathf.Abs(_eyeY - targetY) > 1.0f) _eyeY = targetY;
            }
            Vector3 at = new Vector3((float)State.East, _eyeY, (float)State.North);
            if (!Still)
            {
                // The stride is laid on the smoothed eye, never in place of its smoothing: a footfall's dip down, and the
                // sway across the way the founder faces (yaw 0 faces north, so the right hand is east).
                float yaw = YawDeg * Mathf.Deg2Rad;
                at += new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw)) * (float)_stride.SwayNowM;
                at.y -= (float)_stride.DipNowM;
            }
            _camera.transform.position = at;
            _camera.transform.rotation = Quaternion.Euler(PitchDeg, YawDeg, 0f);
        }

        public static Vector3 ToUnity(Double3 local) => new Vector3((float)local.X, (float)local.Y, (float)local.Z);
    }
}

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
    /// its vertical motion smoothed and nothing else (plan §4.8), and the body reported to the server at its
    /// tick rate. A Correction from the server is adopted at once and counted; on a legal walk every one is a
    /// false positive the N2 budget charges.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        private const float SendIntervalSeconds = 0.05f;
        private const float StandingEyeM = 1.65f;
        private const float CrouchEyeM = 1.1f;
        private const float EyeSmoothingPerSecond = 14f;
        private const float MaxPitchDeg = 89f;
        /// <summary>The soft boundary: the mover is held this far inside the region's edge until the vignette exists.</summary>
        private const double EdgeMarginM = 2.0;

        private IPlayerInputSource _input;
        private IWorldCollision _collision;
        private GameClient _client;
        private MoverConfig _config;
        private Region _region;
        private Camera _camera;
        private Vector2 _move;
        private bool _jumpQueued;
        private bool _sprint;
        private bool _crouch;
        private float _sinceSend;
        private float _eyeY;
        private bool _eyeInitialised;

        public MoverState State;
        public float YawDeg;
        public float PitchDeg;

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
        /// under the terrain forever.
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
            _input.Sample(Time.unscaledDeltaTime, out Vector2 move, out Vector2 look, out bool jump, out bool sprint, out bool crouch);
            YawDeg = Mathf.Repeat(YawDeg + look.x, 360f);
            PitchDeg = Mathf.Clamp(PitchDeg - look.y, -MaxPitchDeg, MaxPitchDeg);
            _move = move;
            if (jump) _jumpQueued = true;
            _sprint = sprint;
            _crouch = crouch;
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

            State = Mover.Step(State, input, dt, _collision, _config);
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
            float eye = State.Stance == Stance.Crouching ? CrouchEyeM : StandingEyeM;
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
                if (Mathf.Abs(_eyeY - targetY) > 1.0f) _eyeY = targetY; // a correction or a fall: no long slide
            }
            _camera.transform.position = new Vector3((float)State.East, _eyeY, (float)State.North);
            _camera.transform.rotation = Quaternion.Euler(PitchDeg, YawDeg, 0f);
        }

        public static Vector3 ToUnity(Double3 local) => new Vector3((float)local.X, (float)local.Y, (float)local.Z);
    }
}

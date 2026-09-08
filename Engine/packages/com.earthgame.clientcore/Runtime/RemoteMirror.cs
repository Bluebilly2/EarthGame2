using System;
using System.Collections.Generic;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.ClientCore
{
    /// <summary>Where a remote player is drawn this frame, and how the mirror arrived at it.</summary>
    public struct MirrorSample
    {
        public double East;
        public double Up;
        public double North;
        public float YawDeg;
        public float PitchDeg;
        public MoverState Latest;
        /// <summary>The server tick the sample stands for.</summary>
        public double AtTick;
        /// <summary>True when the sample is between two received states; false when it is held at the last one.</summary>
        public bool Interpolated;
    }

    /// <summary>
    /// A remote player as this client sees them: the states the server sent, kept by server tick, and a sample
    /// taken a stated delay behind the estimated server tick so that the draw always has a state either side of
    /// it (ARCHITECTURE §7; N4 measures this sample against the server's record). Past the newest state the
    /// mirror holds still rather than guessing; the delay is what makes that rare.
    /// </summary>
    public sealed class RemoteMirror
    {
        private const int Keep = 64;
        private readonly List<PlayerStateMessage> _states = new List<PlayerStateMessage>(Keep);

        public uint SessionId { get; }

        public RemoteMirror(uint sessionId)
        {
            SessionId = sessionId;
        }

        public int Count => _states.Count;

        /// <summary>The newest state received, valid when Count is above zero.</summary>
        public PlayerStateMessage Latest => _states[_states.Count - 1];

        /// <summary>Records a state in tick order; a state older than the newest held is dropped, a repeat tick replaces.</summary>
        public void Push(PlayerStateMessage state)
        {
            if (_states.Count > 0)
            {
                long newest = _states[_states.Count - 1].ServerTick;
                if (state.ServerTick < newest) return;
                if (state.ServerTick == newest)
                {
                    _states[_states.Count - 1] = state;
                    return;
                }
            }
            _states.Add(state);
            if (_states.Count > Keep) _states.RemoveAt(0);
        }

        /// <summary>The mirror at a server tick, between the two states around it.</summary>
        public MirrorSample Sample(double atTick)
        {
            MirrorSample s = default;
            if (_states.Count == 0) throw new InvalidOperationException("no state has been received for session " + SessionId);
            s.AtTick = atTick;
            s.Latest = Latest.Body;
            PlayerStateMessage before = _states[0];
            PlayerStateMessage after = _states[_states.Count - 1];
            if (atTick <= before.ServerTick)
            {
                Fill(ref s, before);
                return s;
            }
            if (atTick >= after.ServerTick)
            {
                Fill(ref s, after);
                return s;
            }
            for (int i = 1; i < _states.Count; i++)
            {
                if (_states[i].ServerTick >= atTick)
                {
                    before = _states[i - 1];
                    after = _states[i];
                    break;
                }
            }
            double span = after.ServerTick - before.ServerTick;
            double t = span > 0 ? (atTick - before.ServerTick) / span : 1.0;
            s.East = before.Body.East + (after.Body.East - before.Body.East) * t;
            s.Up = before.Body.Up + (after.Body.Up - before.Body.Up) * t;
            s.North = before.Body.North + (after.Body.North - before.Body.North) * t;
            s.YawDeg = LerpAngle(before.YawDeg, after.YawDeg, (float)t);
            s.PitchDeg = before.PitchDeg + (after.PitchDeg - before.PitchDeg) * (float)t;
            s.Interpolated = true;
            return s;
        }

        private static void Fill(ref MirrorSample s, PlayerStateMessage state)
        {
            s.East = state.Body.East;
            s.Up = state.Body.Up;
            s.North = state.Body.North;
            s.YawDeg = state.YawDeg;
            s.PitchDeg = state.PitchDeg;
            s.Interpolated = false;
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float delta = ((b - a) % 360f + 540f) % 360f - 180f;
            float r = a + delta * t;
            return ((r % 360f) + 360f) % 360f;
        }
    }
}

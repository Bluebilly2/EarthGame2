using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// The thing in the founder's hand (M1.5a), drawn in front of the camera from the mesh it lies on the ground as
    /// (<see cref="ItemLooks"/>): low and to the right, a stick held pointing ahead and a little up, casting no shadow;
    /// nothing when the hand is empty. Since M1.5c it moves as v1's did (<see cref="HandMotion"/>): it keeps only a
    /// shoulder's share of the head's pitch, walks with the stride, gets where it should be late by its weight, and swings
    /// through a use.
    /// </summary>
    public sealed class HandView
    {
        private static readonly Vector3 StickAt = new Vector3(0.30f, -0.32f, 0.55f);
        private static readonly Quaternion StickTurn = Quaternion.Euler(0f, -90f, 25f);
        private static readonly Vector3 StoneAt = new Vector3(0.24f, -0.21f, 0.42f);

        /// <summary>Where a use swings the hand, from where it rests, m in the camera's frame (v1's strike).</summary>
        private static readonly Vector3 StrikeThrough = new Vector3(-0.10f, -0.20f, 0.16f);

        private readonly GameObject _held;
        private readonly MeshFilter _filter;
        private readonly HandMotion _motion = new HandMotion();
        private Vector3 _restAt;
        private Quaternion _restTurn = Quaternion.identity;
        private double _massKg;

        /// <summary>The id of the thing drawn in the hand, 0 when the hand is drawn empty.</summary>
        public ulong Showing { get; private set; }

        public HandView(Camera camera, Material material)
        {
            _held = new GameObject("In hand");
            _held.transform.SetParent(camera.transform, false);
            _filter = _held.AddComponent<MeshFilter>();
            MeshRenderer renderer = _held.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _held.SetActive(false);
        }

        /// <summary>Draws a thing in the hand; one with no look, or none, empties it.</summary>
        public void Show(Definition definition, ulong id)
        {
            if (definition == null || !ItemLooks.TryLook(definition, id, out Mesh mesh, out float scale))
            {
                Hide();
                return;
            }
            _filter.sharedMesh = mesh;
            bool stick = ReferenceEquals(definition, DefinitionCatalogue.Stick);
            _restAt = stick ? StickAt : StoneAt;
            _restTurn = stick ? StickTurn : Quaternion.identity;
            _massKg = definition.MassKg;
            // A thing newly in hand starts where it rests, rather than swinging in from where the last one was.
            if (id != Showing)
            {
                _held.transform.localPosition = _restAt;
                _held.transform.localRotation = _restTurn;
            }
            _held.transform.localScale = Vector3.one * scale;
            _held.SetActive(true);
            Showing = id;
        }

        public void Hide()
        {
            _held.SetActive(false);
            Showing = 0;
        }

        /// <summary>A use (M1.5c): the hand swings through and back, whatever the server makes of it.</summary>
        public void Strike() => _motion.Strike();

        /// <summary>
        /// Where the thing in hand is this frame: where it rests, walked with the stride at a speed across the ground, m/s,
        /// swung through a use, and turned back against all but a shoulder's share of the camera's pitch (positive down),
        /// reached late by its weight.
        /// </summary>
        public void Place(float dt, double speedMs, float pitchDeg)
        {
            double swing = _motion.Swing(dt);
            _motion.Bob(dt, speedMs, out double side, out double down);
            if (!_held.activeSelf) return;
            Quaternion shoulder = Quaternion.Euler((float)HandMotion.CounterPitchDeg(pitchDeg), 0f, 0f);
            Vector3 target = shoulder * (_restAt + StrikeThrough * (float)swing + new Vector3((float)side, -(float)down, 0f));
            float follow = (float)HandMotion.Follow(dt, _massKg);
            Transform held = _held.transform;
            held.localPosition = Vector3.Lerp(held.localPosition, target, follow);
            held.localRotation = Quaternion.Slerp(held.localRotation, shoulder * _restTurn, follow);
        }

        public void Dispose()
        {
            if (_held != null) Object.Destroy(_held);
        }
    }
}

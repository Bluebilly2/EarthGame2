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
        /// <summary>A flake (FP.3) is pinched between finger and thumb, nearer the eye and tilted so its face is seen.</summary>
        private static readonly Vector3 FlakeAt = new Vector3(0.20f, -0.17f, 0.36f);
        private static readonly Quaternion FlakeTurn = Quaternion.Euler(-35f, 20f, 15f);
        /// <summary>
        /// A flake is drawn from the cobble's own mesh until it has a shard of its own (DEBTS), pressed flat: a plate about nine
        /// centimetres by seven and two thick, which is what a flake is, rather than a pebble.
        /// </summary>
        private static readonly Vector3 FlakeSquash = new Vector3(0.9f, 0.22f, 0.7f);

        /// <summary>Where a use swings the hand, from where it rests, m in the camera's frame (v1's strike).</summary>
        private static readonly Vector3 StrikeThrough = new Vector3(-0.10f, -0.20f, 0.16f);

        /// <summary>Where a full wind-up draws the hand back to, from where it rests (FP.3): up and back, the stone raised to strike.</summary>
        private static readonly Vector3 WindBack = new Vector3(0.06f, 0.14f, -0.12f);

        private readonly GameObject _held;
        private readonly MeshFilter _filter;
        private readonly HandMotion _motion = new HandMotion();
        private Vector3 _restAt;
        private Quaternion _restTurn = Quaternion.identity;
        private Vector3 _restScale = Vector3.one;
        private double _massKg;
        private float _windUp;

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

        /// <summary>Draws a thing in the hand, in the shape its state keeps (BF.1) and at its own mass; one with no look, or none, empties it.</summary>
        public void Show(Definition definition, ulong id, in ThingState state)
        {
            if (definition == null || !ItemLooks.TryLook(definition, id, state, out Mesh mesh, out float scale))
            {
                Hide();
                return;
            }
            _filter.sharedMesh = mesh;
            bool stick = definition.Substance == Substance.Wood || definition.Substance == Substance.Bark || definition.Substance == Substance.Cord;
            bool flake = DefinitionCatalogue.IsFlake(definition);
            _restAt = stick ? StickAt : flake ? FlakeAt : StoneAt;
            _restTurn = stick ? StickTurn : flake ? FlakeTurn : Quaternion.identity;
            _restScale = flake ? FlakeSquash * scale : Vector3.one * scale;
            _massKg = (float)ThingWords.MassOf(definition, state);
            // A thing newly in hand starts where it rests, rather than swinging in from where the last one was.
            if (id != Showing)
            {
                _held.transform.localPosition = _restAt;
                _held.transform.localRotation = _restTurn;
            }
            _held.transform.localScale = _restScale;
            _held.SetActive(true);
            Showing = id;
        }

        /// <summary>How far the arm is wound up for a blow, 0 to 1 (FP.3): the stone in hand is drawn back by that share of <see cref="WindBack"/>.</summary>
        public void WindUp(float windUp01) => _windUp = Mathf.Clamp01(windUp01);

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
            Vector3 target = shoulder * (_restAt + StrikeThrough * (float)swing + WindBack * _windUp + new Vector3((float)side, -(float)down, 0f));
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

using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// The thing in the founder's hand (M1.5a), drawn in front of the camera from the mesh it lies on the ground as
    /// (<see cref="ItemLooks"/>): low and to the right, a stick held pointing ahead and a little up, casting no shadow;
    /// nothing when the hand is empty.
    /// </summary>
    public sealed class HandView
    {
        private static readonly Vector3 StickAt = new Vector3(0.30f, -0.32f, 0.55f);
        private static readonly Quaternion StickTurn = Quaternion.Euler(0f, -90f, 25f);
        private static readonly Vector3 StoneAt = new Vector3(0.24f, -0.21f, 0.42f);

        private readonly GameObject _held;
        private readonly MeshFilter _filter;

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
            _held.transform.localPosition = stick ? StickAt : StoneAt;
            _held.transform.localRotation = stick ? StickTurn : Quaternion.identity;
            _held.transform.localScale = Vector3.one * scale;
            _held.SetActive(true);
            Showing = id;
        }

        public void Hide()
        {
            _held.SetActive(false);
            Showing = 0;
        }

        public void Dispose()
        {
            if (_held != null) Object.Destroy(_held);
        }
    }
}

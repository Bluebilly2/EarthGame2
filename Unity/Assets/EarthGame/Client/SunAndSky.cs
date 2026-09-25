using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthGame.Client
{
    /// <summary>
    /// The sun where <see cref="SolarClock"/> says it is: a directional light aimed along the sun's bearing and
    /// elevation, its strength and warmth from the elevation, the procedural skybox reading the same light, and
    /// the ambient following the sky. The clock it reads is the client's mirror of the server's one clock; no
    /// time is kept here. Colours and strengths are first values for the owner's eyes to judge (frames), not
    /// decisions.
    ///
    /// <para>And the air (M1.6h, 2026-09-25), in place of the pipeline's fog: the haze every shader of the land, what stands on
    /// it and the water fades by (Haze.hlsl), its depth from the weather's humidity at the sea (<see cref="Haze"/>), its colour
    /// the sky's own at the horizon, which this renders into a small cube whenever the sun has moved; the bowl of that colour
    /// below the horizon, beyond all the land, where the procedural sky would show its dark ground; and how far the eye sees,
    /// the camera's far plane, just past the bowl.</para>
    /// </summary>
    public sealed class SunAndSky : MonoBehaviour
    {
        private const float SunStrengthLinear = 2.2f;

        /// <summary>
        /// The sky's cube, texels a side, which the haze is told (<c>_EgSkyCubeTexels</c>): it reads the row of texels just above
        /// the horizon, whose middles stand a third to half a degree up at this size, where the
        /// sky is still its own: at 32 the nearest row was near two degrees up, and a sample a degree up took a fifth of its
        /// colour from the row below the horizon, where the procedural sky darkens toward its ground, and the haze came out
        /// grey against the white of the sky at the horizon (frames of 2026-09-25).
        /// </summary>
        private const int SkyCubeTexels = 128;

        /// <summary>How far the sun moves, degrees, or the sky's exposure changes before the sky's cube is rendered again.</summary>
        private const float RecaptureDeg = 0.25f, RecaptureExposure = 0.01f;

        /// <summary>The share of the land's half-width its last band fades wholly into the haze over.</summary>
        private const float RimShare = 0.125f;

        /// <summary>How far the eye sees with no land's edge to reckon from (a joiner without the region's bake), m.</summary>
        private const float ReachWithoutLandM = 100000f;

        private static readonly int HazePerMId = Shader.PropertyToID("_EgHazePerM"), HazeScaleId = Shader.PropertyToID("_EgHazeScaleM"),
            LandEdgeId = Shader.PropertyToID("_EgLandEdgeM"), LandRimId = Shader.PropertyToID("_EgLandRimM"), SkyCubeId = Shader.PropertyToID("_EgSkyCube"),
            SkyCubeTexelsId = Shader.PropertyToID("_EgSkyCubeTexels");

        private Light _sun;
        private Material _sky;
        private SolarClock _clock;
        private Camera _eye;
        private Camera _capture;
        private RenderTexture _skyCube;
        private Vector3 _capturedToSun;
        private float _capturedExposure = -1f;
        private bool _captureFailed;
        /// <summary>The face of the cube the next frame renders while a new sky is taken a face a frame; −1 between.</summary>
        private int _nextFace = -1;
        private readonly System.Diagnostics.Stopwatch _captureClock = new System.Diagnostics.Stopwatch();
        private int _capturesTold;
        /// <summary>The bowl below the horizon: its mesh at unit radius, its material, and the radius it is drawn at, m.</summary>
        private float _bowlRadius;
        private Material _bowlMaterial;
        private Mesh _bowlMesh;

        /// <summary>
        /// The relative humidity at the sea (0 to 1), which sets how deep the haze is; ClientRuntime reads it from the weather
        /// once a second. Unknown, it is the coast's usual.
        /// </summary>
        public double HumidityAtSea { get; set; } = double.NaN;

        /// <summary>
        /// Takes charge of the sun, the sky and the air. <paramref name="landEdgeM"/> is how far the land the client draws
        /// reaches from the world's origin each way (zero for none known): its last band fades wholly into the haze, and the
        /// bowl and the far plane stand beyond its farthest corner from any eye within it.
        /// </summary>
        public void Attach(SolarClock clock, Light sun, Material skyMaterial, Camera eye, double landEdgeM)
        {
            _clock = clock;
            _sun = sun;
            _sky = skyMaterial;
            _eye = eye;
            if (_sun != null)
            {
                _sun.type = LightType.Directional;
                _sun.shadows = LightShadows.Soft;
                RenderSettings.sun = _sun;
            }
            if (_sky != null)
            {
                RenderSettings.skybox = _sky;
                // The procedural sky's disc is far larger than the real half-degree sun at its default; the
                // first frames (2026-09-08) showed a disc a tenth of the frame high. Kept small and hard-edged.
                _sky.SetFloat("_SunSize", 0.012f);
                _sky.SetFloat("_SunSizeConvergence", 10f);
                _sky.SetFloat("_AtmosphereThickness", 1.0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // The haze is the air's (M1.6h); the pipeline's fog, one colour and a density picked by eye, is off.
            RenderSettings.fog = false;
            Shader.SetGlobalFloat(HazeScaleId, (float)Haze.ScaleHeightM);
            SetLand((float)landEdgeM);
            Apply();
        }

        private void Update()
        {
            Apply();
            DrawBowl();
        }

        /// <summary>
        /// The bowl drawn round the eye this frame, its rim at the eye's height so the horizon is where the sky's is, submitted as
        /// the stand's trees are (<see cref="StandViews"/>), so it is always round the eye the frame is drawn from and never an
        /// object in the scene to be found, hidden or left behind.
        /// </summary>
        private void DrawBowl()
        {
            if (_bowlMesh == null || _bowlMaterial == null || _eye == null) return;
            Vector3 eye = _eye.transform.position;
            RenderParams draw = new RenderParams(_bowlMaterial)
            {
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                worldBounds = new Bounds(eye, Vector3.one * (2f * _bowlRadius)),
            };
            Graphics.RenderMesh(draw, _bowlMesh, 0, Matrix4x4.TRS(eye, Quaternion.identity, Vector3.one * _bowlRadius));
        }

        private void Apply()
        {
            if (_clock == null) return;
            double elevation = _clock.SolarElevationDeg;
            double azimuth = _clock.SolarAzimuthDeg;
            double el = elevation * GeoMath.DegToRad;
            double az = azimuth * GeoMath.DegToRad;
            Vector3 toSun = new Vector3((float)(Math.Sin(az) * Math.Cos(el)), (float)Math.Sin(el), (float)(Math.Cos(az) * Math.Cos(el)));
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
                float daylight = Mathf.Clamp01((float)((elevation + 4.0) / 16.0));
                _sun.intensity = SunStrengthLinear * daylight;
                float warmth = Mathf.Clamp01((float)(elevation / 25.0));
                _sun.color = Color.Lerp(new Color(1.0f, 0.62f, 0.38f), new Color(1.0f, 0.97f, 0.92f), warmth);
            }
            float skyLight = Mathf.Clamp01((float)((elevation + 8.0) / 20.0));
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.02f, 0.03f, 0.06f), new Color(0.68f, 0.8f, 1.0f), skyLight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.02f, 0.02f, 0.03f), new Color(0.6f, 0.63f, 0.66f), skyLight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.01f, 0.01f, 0.01f), new Color(0.3f, 0.26f, 0.2f), skyLight);
            float exposure = Mathf.Lerp(0.15f, 1.2f, skyLight);
            if (_sky != null) _sky.SetFloat("_Exposure", exposure);
            Shader.SetGlobalFloat(HazePerMId, (float)Haze.PerMetre(HumidityAtSea));
            CaptureSky(toSun, exposure);
        }

        /// <summary>
        /// Renders the sky, and nothing else, into the haze's cube when the sun has moved or the sky's exposure changed since
        /// it last was: the sky's colour at the horizon is the procedural sky's own, and this is the one place it is read. The
        /// first time all six faces at once; after that one face a frame, so no one frame pays for six renders of the pipeline.
        /// </summary>
        private void CaptureSky(Vector3 toSun, float exposure)
        {
            if (_captureFailed || _sky == null) return;
            // A player with no graphics device (-nographics, the corpus's walkers) draws nothing and cannot render the sky: a render
            // forced there made the pipeline again and threw on every face, 10,608 exceptions in the soak of 2026-09-25.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                _captureFailed = true;
                Debug.Log("[sky] no graphics device: the sky is not rendered into the haze's cube");
                return;
            }
            int faces;
            if (_skyCube != null && _nextFace >= 0)
            {
                faces = 1 << _nextFace;
                _nextFace = _nextFace == 5 ? -1 : _nextFace + 1;
            }
            else if (_skyCube != null)
            {
                if (Vector3.Angle(toSun, _capturedToSun) < RecaptureDeg && Mathf.Abs(exposure - _capturedExposure) < RecaptureExposure) return;
                faces = 1;
                _nextFace = 1;
                _capturedToSun = toSun;
                _capturedExposure = exposure;
            }
            else
            {
                faces = 63;
                _capturedToSun = toSun;
                _capturedExposure = exposure;
                _skyCube = new RenderTexture(SkyCubeTexels, SkyCubeTexels, 24, RenderTextureFormat.DefaultHDR)
                {
                    dimension = TextureDimension.Cube, useMipMap = false, autoGenerateMips = false, name = "Sky cube",
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                _skyCube.Create();
                GameObject holder = new GameObject("Sky capture");
                holder.transform.SetParent(transform, false);
                _capture = holder.AddComponent<Camera>();
                _capture.enabled = false;
                _capture.cullingMask = 0;
                _capture.clearFlags = CameraClearFlags.Skybox;
                _capture.nearClipPlane = 0.1f;
                _capture.farClipPlane = 10f;
                _capture.allowHDR = true;
                _capture.allowMSAA = false;
                UniversalAdditionalCameraData data = _capture.GetUniversalAdditionalCameraData();
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
                Shader.SetGlobalTexture(SkyCubeId, _skyCube);
                Shader.SetGlobalFloat(SkyCubeTexelsId, SkyCubeTexels);
            }
            _captureClock.Restart();
            bool rendered;
            try
            {
                rendered = _capture.RenderToCubemap(_skyCube, faces);
            }
            catch (Exception ex)
            {
                // A render that throws fails as one that says so: once, not again on every face after.
                rendered = false;
                Debug.LogError("[sky] " + ex.Message);
            }
            if (!rendered)
            {
                _captureFailed = true;
                Debug.LogError("[sky] the sky could not be rendered into the haze's cube; the haze has no colour");
                return;
            }
            // What a render costs, told for the first few, the whole cube's and each face's.
            if (_capturesTold++ < 7)
                Debug.Log("[sky] the sky rendered into the haze's cube, " + (faces == 63 ? "all six faces" : "one face") + ", in "
                          + _captureClock.Elapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms");
        }

        /// <summary>
        /// The land's edge for the haze, the bowl below the horizon beyond it, and the far plane. The bowl stands beyond the land's
        /// farthest corner from any eye over the land, twice its half-diagonal, and the far plane a twentieth beyond the bowl.
        /// </summary>
        private void SetLand(float edgeM)
        {
            Shader.SetGlobalFloat(LandEdgeId, Mathf.Max(edgeM, 0f));
            Shader.SetGlobalFloat(LandRimId, Mathf.Max(edgeM, 0f) * RimShare);
            float reach = edgeM > 0f ? 2f * Mathf.Sqrt(2f) * edgeM : ReachWithoutLandM;
            if (_eye != null) _eye.farClipPlane = reach * 1.05f;
            Debug.Log("[sky] the land reaches " + edgeM.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m each way; the haze's bowl stands "
                      + reach.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m round the eye, the far plane at "
                      + (_eye != null ? _eye.farClipPlane.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m" : "no eye"));
            Shader horizon = Resources.Load<Shader>("EarthGame/Horizon");
            if (horizon == null)
            {
                Debug.LogError("[sky] no Horizon shader under Resources/EarthGame; below the horizon the sky's own ground shows past the land");
                return;
            }
            _bowlMaterial = new Material(horizon) { name = "Horizon" };
            _bowlMesh = Bowl();
            _bowlRadius = reach;
        }

        /// <summary>A bowl of unit radius round the origin, from its rim at the horizon down to the point below.</summary>
        private static Mesh Bowl()
        {
            const int around = 96, rings = 16;
            Vector3[] points = new Vector3[around * rings + 1];
            for (int ring = 0; ring < rings; ring++)
            {
                float down = 0.5f * Mathf.PI * ring / rings;
                for (int k = 0; k < around; k++)
                {
                    float a = 2f * Mathf.PI * k / around;
                    points[ring * around + k] = new Vector3(Mathf.Cos(down) * Mathf.Sin(a), -Mathf.Sin(down), Mathf.Cos(down) * Mathf.Cos(a));
                }
            }
            points[around * rings] = Vector3.down;
            int[] triangles = new int[around * (rings - 1) * 6 + around * 3];
            int t = 0;
            for (int ring = 0; ring + 1 < rings; ring++)
                for (int k = 0; k < around; k++)
                {
                    int a = ring * around + k, b = ring * around + (k + 1) % around, c = a + around, d = b + around;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                }
            for (int k = 0; k < around; k++)
            {
                triangles[t++] = (rings - 1) * around + k;
                triangles[t++] = (rings - 1) * around + (k + 1) % around;
                triangles[t++] = around * rings;
            }
            Mesh mesh = new Mesh { name = "Horizon bowl" };
            mesh.vertices = points;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        private void OnDestroy()
        {
            if (_skyCube != null) _skyCube.Release();
            if (_bowlMaterial != null) Destroy(_bowlMaterial);
            if (_bowlMesh != null) Destroy(_bowlMesh);
        }
    }
}

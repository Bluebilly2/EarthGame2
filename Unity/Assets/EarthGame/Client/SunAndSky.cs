using System;
using EarthGame.Engine;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The sun where <see cref="SolarClock"/> says it is: a directional light aimed along the sun's bearing and
    /// elevation, its strength and warmth from the elevation, the procedural skybox reading the same light, and
    /// the ambient following the sky. The clock it reads is the client's mirror of the server's one clock; no
    /// time is kept here. Colours and strengths are first values for the owner's eyes to judge (frames), not
    /// decisions.
    /// </summary>
    public sealed class SunAndSky : MonoBehaviour
    {
        private const float SunStrengthLinear = 1.6f;

        private Light _sun;
        private Material _sky;
        private SolarClock _clock;

        public void Attach(SolarClock clock, Light sun, Material skyMaterial)
        {
            _clock = clock;
            _sun = sun;
            _sky = skyMaterial;
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
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00025f;
            Apply();
        }

        private void Update() => Apply();

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
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.02f, 0.03f, 0.06f), new Color(0.55f, 0.68f, 0.9f), skyLight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.02f, 0.02f, 0.03f), new Color(0.45f, 0.48f, 0.5f), skyLight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.01f, 0.01f, 0.01f), new Color(0.18f, 0.16f, 0.12f), skyLight);
            RenderSettings.fogColor = Color.Lerp(new Color(0.03f, 0.04f, 0.07f), new Color(0.72f, 0.8f, 0.9f), skyLight);
            if (_sky != null) _sky.SetFloat("_Exposure", Mathf.Lerp(0.15f, 1.2f, skyLight));
        }
    }
}

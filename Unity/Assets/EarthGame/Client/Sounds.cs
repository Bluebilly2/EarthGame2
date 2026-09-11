using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The founder's footsteps and strokes, and the landing of a thing let go (M1.5c, M1.5e), played from clips made at
    /// load from <see cref="FootstepSynth"/>'s arithmetic; none is recorded. The footsteps and strokes are the founder's own
    /// and sound at the ear, one source a side so each keeps its own pitch; a landing sounds where the thing came down.
    /// Nothing here decides whether a run is heard: the runtime mutes every automated one.
    /// </summary>
    public sealed class Sounds
    {
        private readonly AudioClip[][] _steps;
        private readonly AudioClip[] _strokes;
        private readonly AudioClip _stone;
        private readonly AudioClip _wood;
        private readonly GameObject _feet;
        private readonly AudioSource _left;
        private readonly AudioSource _right;

        public Sounds(Transform ear)
        {
            FootingSound[] footings = (FootingSound[])Enum.GetValues(typeof(FootingSound));
            _steps = new AudioClip[footings.Length][];
            foreach (FootingSound footing in footings)
            {
                AudioClip[] set = new AudioClip[FootstepSynth.Variants];
                for (int v = 0; v < set.Length; v++) set[v] = Clip("step " + footing + " " + v, FootstepSynth.Step(footing, v));
                _steps[(int)footing] = set;
            }
            _strokes = new AudioClip[FootstepSynth.Variants];
            for (int v = 0; v < _strokes.Length; v++) _strokes[v] = Clip("stroke " + v, FootstepSynth.Stroke(v));
            _stone = Clip("stone landing", FootstepSynth.StoneLanding());
            _wood = Clip("wood landing", FootstepSynth.WoodLanding());
            _feet = new GameObject("Footsteps");
            _feet.transform.SetParent(ear, false);
            _left = Source();
            _right = Source();
        }

        /// <summary>A foot falling on a ground.</summary>
        public void Step(in Footfall footfall, FootingSound footing)
        {
            AudioSource source = footfall.Left ? _left : _right;
            source.pitch = (float)footfall.Pitch;
            source.PlayOneShot(_steps[(int)footing][footfall.Variant], (float)footfall.Loudness01);
        }

        /// <summary>A stroke swum (M1.5e): at the ear, as a footstep is.</summary>
        public void Stroke(in Footfall stroke)
        {
            AudioSource source = stroke.Left ? _left : _right;
            source.pitch = (float)stroke.Pitch;
            source.PlayOneShot(_strokes[stroke.Variant], (float)stroke.Loudness01);
        }

        /// <summary>A thing coming to rest on the ground, with the energy it came down with, J: a stick sounds of wood and anything else of stone.</summary>
        public void Landing(Definition definition, Vector3 at, double joules)
        {
            float loudness = (float)Loudness.Landing01(joules);
            if (loudness <= 0f) return;
            AudioSource.PlayClipAtPoint(ReferenceEquals(definition, DefinitionCatalogue.Stick) ? _wood : _stone, at, loudness);
        }

        public void Dispose()
        {
            if (_feet != null) UnityEngine.Object.Destroy(_feet);
            foreach (AudioClip[] set in _steps)
                foreach (AudioClip clip in set)
                    UnityEngine.Object.Destroy(clip);
            foreach (AudioClip clip in _strokes) UnityEngine.Object.Destroy(clip);
            UnityEngine.Object.Destroy(_stone);
            UnityEngine.Object.Destroy(_wood);
        }

        private AudioSource Source()
        {
            AudioSource source = _feet.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            // The founder's own feet are at the ear, not somewhere in the world.
            source.spatialBlend = 0f;
            return source;
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, FootstepSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

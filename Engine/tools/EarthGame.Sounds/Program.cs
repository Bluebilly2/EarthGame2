using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;

namespace EarthGame.Sounds
{
    /// <summary>
    /// The founder's sounds written out as WAV files for William's ears (M1.5j promise 5), from the arithmetic the client
    /// plays: each ground's four steps, a walk of ten steps on each ground at a walking pace, the strokes and the landings;
    /// and under blind/ the walks again lettered A to G by a shuffle, with the key beside them in key.txt, so the grounds
    /// can be named without being told. Arguments follow the server host's +key value convention: +out the folder
    /// (Artefacts/sounds/[stamp]), +blind the shuffle's seed (1). Exit 2 on a bad argument.
    /// </summary>
    public static class Program
    {
        private const double WalkSpacingS = 0.58;
        private const int WalkSteps = 10;

        public static int Main(string[] args)
        {
            Dictionary<string, string> a = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("+", StringComparison.Ordinal) || i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("usage: +out <folder> +blind <seed>");
                    return 2;
                }
                a[args[i].Substring(1)] = args[++i];
            }
            string root = a.TryGetValue("out", out string given) ? given
                : Path.Combine("Artefacts", "sounds", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
            ulong seed = a.TryGetValue("blind", out string s) && ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed) ? parsed : 1UL;
            Directory.CreateDirectory(root);
            FootingSound[] footings = (FootingSound[])Enum.GetValues(typeof(FootingSound));
            foreach (FootingSound footing in footings)
            {
                string name = footing.ToString().ToLowerInvariant();
                for (int v = 0; v < FootstepSynth.Variants; v++)
                    Write(Path.Combine(root, "step-" + name + "-" + v + ".wav"), FootstepSynth.Step(footing, v));
                Write(Path.Combine(root, "walk-" + name + ".wav"), Walk(footing));
            }
            for (int v = 0; v < FootstepSynth.Variants; v++) Write(Path.Combine(root, "stroke-" + v + ".wav"), FootstepSynth.Stroke(v));
            Write(Path.Combine(root, "landing-stone.wav"), FootstepSynth.StoneLanding());
            Write(Path.Combine(root, "landing-wood.wav"), FootstepSynth.WoodLanding());

            // The blind set: the walks again, lettered by a shuffle of the seed, the key kept in its own file.
            string blind = Path.Combine(root, "blind");
            Directory.CreateDirectory(blind);
            FootingSound[] order = (FootingSound[])footings.Clone();
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = (int)(StandLayout.Mix(seed ^ ((ulong)(i + 1) * 0x9E3779B97F4A7C15UL)) % (ulong)(i + 1));
                FootingSound t = order[i];
                order[i] = order[j];
                order[j] = t;
            }
            List<string> key = new List<string>();
            for (int i = 0; i < order.Length; i++)
            {
                string letter = ((char)('A' + i)).ToString();
                Write(Path.Combine(blind, letter + ".wav"), Walk(order[i]));
                key.Add(letter + " " + order[i].ToString().ToLowerInvariant());
            }
            File.WriteAllText(Path.Combine(blind, "key.txt"), string.Join("\n", key) + "\n");
            Console.WriteLine(Path.GetFullPath(root));
            return 0;
        }

        /// <summary>
        /// Ten steps on one ground at a walking pace, the variants and the feet alternating as the stride's would, each a
        /// little different in pitch and loudness by a hash, which is how no two steps are quite the same in the game.
        /// </summary>
        private static float[] Walk(FootingSound footing)
        {
            float[] mix = new float[(int)((WalkSteps * WalkSpacingS + 0.6) * FootstepSynth.SampleRate)];
            for (int n = 0; n < WalkSteps; n++)
            {
                ulong h = StandLayout.Mix(((ulong)footing << 32) ^ ((ulong)(n + 1) * 0x9E3779B97F4A7C15UL));
                double pitch = 0.96 + 0.08 * ((h & 0xFFFF) / 65536.0);
                double loud = 0.85 + 0.15 * (((h >> 16) & 0xFFFF) / 65536.0);
                double jitter = 0.03 * (((h >> 32) & 0xFFFF) / 65536.0 - 0.5);
                float[] step = FootstepSynth.Step(footing, (int)((h >> 48) % (ulong)FootstepSynth.Variants));
                int at = (int)((0.3 + n * WalkSpacingS + jitter) * FootstepSynth.SampleRate);
                for (int i = 0; i < step.Length; i++)
                {
                    double src = i * pitch;
                    int k = (int)src;
                    if (k + 1 >= step.Length || at + i >= mix.Length) break;
                    float x = (float)(step[k] + (step[k + 1] - step[k]) * (src - k));
                    mix[at + i] += (float)(loud * x);
                }
            }
            return mix;
        }

        /// <summary>A mono 16-bit WAV at the synthesiser's rate.</summary>
        private static void Write(string path, float[] samples)
        {
            using (FileStream file = File.Create(path))
            using (BinaryWriter w = new BinaryWriter(file))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(FootstepSynth.SampleRate);
                w.Write(FootstepSynth.SampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                foreach (float x in samples)
                {
                    float c = x < -1f ? -1f : x > 1f ? 1f : x;
                    w.Write((short)Math.Round(c * 32767.0));
                }
            }
        }
    }
}

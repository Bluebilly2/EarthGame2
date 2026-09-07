using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// Deterministic pseudo-random number generator for the simulation core.
    /// All randomness in the simulation flows through explicitly seeded instances of this
    /// class; the same seed and the same sequence of calls produce bit-identical results on
    /// every platform (GAME_DESIGN §21, §34). The generator is xoshiro256** (Blackman/Vigna),
    /// state-initialized via splitmix64 so that any 64-bit seed — including 0 — yields a
    /// well-mixed, non-degenerate state. Gaussian sampling uses the Marsaglia polar method
    /// with a cached spare deviate; log-normal sampling is parameterized by median and
    /// log-space sigma. Independent, decorrelated child streams are derived with
    /// <see cref="DeriveSeed"/> (FNV-1a over the stream name, mixed with the parent seed).
    /// No static mutable state; instances are not thread-safe and are intended for
    /// single-threaded deterministic stepping.
    ///
    /// <para>Ported verbatim from v1 (Assets/EarthGame/Sim/Core/SimRandom.cs), where its determinism
    /// tests pinned the behaviour that every seeded derivation in the world hangs off.</para>
    /// </summary>
    public sealed class SimRandom
    {
        // xoshiro256** internal state. Never all-zero after construction (splitmix64
        // cannot emit four consecutive zero outputs).
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        // Marsaglia polar method produces deviates in pairs; the second is cached here
        // so consecutive NextGaussian calls consume the underlying stream consistently.
        private bool _hasSpareGaussian;
        private double _spareGaussian;

        /// <summary>
        /// Creates a generator whose entire future output is a pure function of
        /// <paramref name="seed"/>.
        /// </summary>
        /// <param name="seed">The 64-bit seed. Any value is valid, including 0.</param>
        public SimRandom(ulong seed)
        {
            // Seed-expand with splitmix64 as recommended by the xoshiro authors: it
            // decorrelates similar seeds and guarantees a non-zero state vector.
            ulong x = seed;
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            _s2 = SplitMix64(ref x);
            _s3 = SplitMix64(ref x);
        }

        /// <summary>
        /// Returns the next uniformly distributed double in [0, 1). Uses the top 53 bits of
        /// the underlying 64-bit output, so every representable value is an exact multiple
        /// of 2^-53 and 1.0 is never returned.
        /// </summary>
        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0); // 2^-53
        }

        /// <summary>
        /// Returns a normally distributed sample with the given mean and standard deviation,
        /// via the Marsaglia polar method. A spare deviate is cached, so calls alternate
        /// between generating a fresh pair and consuming the cached value; the sequence is
        /// fully deterministic for a fixed seed and call order.
        /// </summary>
        /// <param name="mean">Distribution mean.</param>
        /// <param name="stdDev">Distribution standard deviation (0 returns the mean exactly).</param>
        public double NextGaussian(double mean, double stdDev)
        {
            return mean + stdDev * NextStandardGaussian();
        }

        /// <summary>
        /// Returns a log-normally distributed sample parameterized by its median and the
        /// standard deviation of the underlying normal in log space:
        /// sample = median * exp(sigmaLn * Z) with Z ~ N(0, 1). The median of the returned
        /// distribution is exactly <paramref name="median"/>.
        /// </summary>
        /// <param name="median">Median of the log-normal distribution (must be &gt; 0 for a
        /// proper log-normal; the formula is applied verbatim regardless).</param>
        /// <param name="sigmaLn">Standard deviation of ln(sample).</param>
        public double NextLogNormal(double median, double sigmaLn)
        {
            return median * Math.Exp(sigmaLn * NextStandardGaussian());
        }

        /// <summary>
        /// Returns true with the given probability. Always consumes exactly one uniform
        /// draw, so stream alignment does not depend on the probability value.
        /// Probabilities &lt;= 0 always return false; probabilities &gt;= 1 always return true.
        /// </summary>
        /// <param name="probabilityTrue">Probability of returning true, nominally in [0, 1].</param>
        public bool NextBool(double probabilityTrue)
        {
            return NextDouble() < probabilityTrue;
        }

        /// <summary>
        /// Derives a child seed from a parent seed and a stream name, so that independent
        /// subsystems ("plants", "fauna", ...) get decorrelated deterministic streams
        /// from one master seed. Computes 64-bit FNV-1a over the UTF-16 code units of
        /// <paramref name="streamName"/>, folds in the parent seed byte-by-byte through the
        /// same FNV-1a stream, then finalizes with the splitmix64 mixer for full avalanche
        /// (raw FNV-1a has weak diffusion in the low bits). Pure function: no shared state.
        /// </summary>
        /// <param name="parent">The parent seed being subdivided.</param>
        /// <param name="streamName">Stable name of the child stream (null treated as empty).</param>
        public static ulong DeriveSeed(ulong parent, string streamName)
        {
            const ulong FnvOffsetBasis = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;

            ulong hash = FnvOffsetBasis;

            if (streamName != null)
            {
                for (int i = 0; i < streamName.Length; i++)
                {
                    char c = streamName[i];
                    hash ^= (ulong)(c & 0xFF);
                    hash *= FnvPrime;
                    hash ^= (ulong)((uint)c >> 8);
                    hash *= FnvPrime;
                }
            }

            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (parent >> shift) & 0xFF;
                hash *= FnvPrime;
            }

            // splitmix64 finalizer: avalanche so that adjacent parents / similar names
            // yield unrelated child seeds.
            hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
            hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
            return hash ^ (hash >> 31);
        }

        /// <summary>
        /// Standard normal deviate N(0, 1) via Marsaglia polar, with pair caching.
        /// </summary>
        private double NextStandardGaussian()
        {
            if (_hasSpareGaussian)
            {
                _hasSpareGaussian = false;
                return _spareGaussian;
            }

            double u, v, s;
            do
            {
                u = 2.0 * NextDouble() - 1.0;
                v = 2.0 * NextDouble() - 1.0;
                s = u * u + v * v;
            }
            while (s >= 1.0 || s == 0.0);

            double factor = Math.Sqrt(-2.0 * Math.Log(s) / s);
            _spareGaussian = v * factor;
            _hasSpareGaussian = true;
            return u * factor;
        }

        /// <summary>
        /// Core xoshiro256** step: returns 64 uniformly distributed bits and advances state.
        /// </summary>
        private ulong NextUInt64()
        {
            ulong result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            ulong t = _s1 << 17;

            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);

            return result;
        }

        /// <summary>
        /// splitmix64 step: advances <paramref name="x"/> by the golden-ratio increment and
        /// returns a fully mixed 64-bit output.
        /// </summary>
        private static ulong SplitMix64(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }
    }
}

using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The small arithmetic every model needs, with the awkward cases decided once.
    ///
    /// <para>History, carried from v1 (2026-09): a review counted the hand-rolled clamps in the simulation and
    /// found <b>eighteen copies of <c>Clamp01</c></b> and three more of the two-bound form — one fact (what
    /// clamping does with a bad number) living in twenty-one places with nothing forcing agreement.
    /// Seventeen of the eighteen agreed with each other and the odd one out was the <b>correct</b> one, so
    /// anyone reading almost any file learned the wrong rule and anyone copying the pattern propagated it.
    /// This class exists so that the fact has one owner.</para>
    ///
    /// <para><b>The policy, decided once: NaN takes the low bound.</b> The common form
    /// <c>v &lt; 0 ? 0 : (v &gt; 1 ? 1 : v)</c> passes NaN straight through, because NaN fails both
    /// comparisons — so a single bad number upstream arrives intact in a heat balance, a mixer volume or a
    /// suitability score, none of which fail loudly when handed one. Written as a positive test instead, NaN
    /// falls to the floor.</para>
    ///
    /// <para><b>The cost of that choice, stated rather than buried:</b> a NaN that would once have propagated
    /// and been noticed now becomes a quiet zero. That is accepted deliberately — a NaN reaching a clamp is
    /// already a bug upstream, and of the two ways to learn about it, "the model produced silence" is
    /// recoverable where "the founder's core temperature is NaN" is not. Anywhere zero is the
    /// <i>dangerous</i> answer, the caller decides its own safe direction and does not ask this class.</para>
    ///
    /// <para>Not <see cref="Math"/>'s own <c>Clamp</c>, which carries the identical NaN hole — a test asserts
    /// that, so this paragraph is checkable rather than remembered.</para>
    /// </summary>
    public static class SimMath
    {
        /// <summary>
        /// Holds a value inside 0 and 1. NaN takes the low bound rather than passing through.
        /// </summary>
        public static double Clamp01(double v) => v >= 0.0 ? (v <= 1.0 ? v : 1.0) : 0.0;

        /// <summary>
        /// Holds a value inside two bounds. As <see cref="Clamp01"/>: NaN takes the low bound.
        /// </summary>
        public static double Clamp(double v, double lo, double hi)
            => v >= lo ? (v <= hi ? v : hi) : lo;
    }
}

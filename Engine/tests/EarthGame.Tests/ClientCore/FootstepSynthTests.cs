using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>The founder's sounds are made from arithmetic, the same every time, and like what makes them (M1.5c promise 4).</summary>
    public sealed class FootstepSynthTests
    {
        private static IEnumerable<float[]> Every()
        {
            foreach (FootingSound footing in (FootingSound[])Enum.GetValues(typeof(FootingSound)))
                for (int v = 0; v < FootstepSynth.Variants; v++)
                    yield return FootstepSynth.Step(footing, v);
            for (int v = 0; v < FootstepSynth.Variants; v++)
                yield return FootstepSynth.Stroke(v);
            yield return FootstepSynth.StoneLanding();
            yield return FootstepSynth.WoodLanding();
        }

        private static double Mean(float[] s, int from, int to)
        {
            double sum = 0.0;
            for (int i = from; i < to; i++) sum += Math.Abs(s[i]);
            return sum / (to - from);
        }

        [Test]
        public void AStrokeSwellsWhereAStepStrikes()
        {
            // M1.5e: water pulled builds before it lets go; a foot comes down at once.
            float[] stroke = FootstepSynth.Stroke(0), step = FootstepSynth.Step(FootingSound.Water, 0);
            int hundredth = FootstepSynth.SampleRate / 100;
            Assert.That(Mean(stroke, 0, hundredth), Is.LessThan(0.25 * Mean(stroke, 10 * hundredth, 11 * hundredth)), "a stroke's first hundredth of a second is quiet");
            Assert.That(Mean(step, 0, hundredth), Is.GreaterThan(Mean(step, 10 * hundredth, 11 * hundredth)), "a step's is its loudest");
            Assert.That(stroke.Length, Is.GreaterThan(step.Length), "and a stroke lasts longer than a wading step's splash");
            Assert.That(FootstepSynth.Stroke(1), Is.Not.EqualTo(stroke), "the strokes differ");
        }

        /// <summary>
        /// How much of a sound is in its top end: the share of its energy above about 1.4 kHz (what a one-pole low-pass at a
        /// fifth lets through, taken away). M1.5c measured the mean step from sample to sample instead, which read rock's ring
        /// as smooth once a rock step rang at its own pitches (M1.5j); a dull thud keeps this small either way.
        /// </summary>
        private static double Brightness(float[] s)
        {
            double low = 0.0, top = 0.0, all = 0.0;
            for (int i = 0; i < s.Length; i++)
            {
                low += 0.2 * (s[i] - low);
                double high = s[i] - low;
                top += high * high;
                all += (double)s[i] * s[i];
            }
            return all > 0.0 ? top / all : 0.0;
        }

        [Test]
        public void ASoundIsTheSameSoundEveryTime()
        {
            Assert.That(FootstepSynth.Step(FootingSound.Rock, 2), Is.EqualTo(FootstepSynth.Step(FootingSound.Rock, 2)));
            Assert.That(FootstepSynth.StoneLanding(), Is.EqualTo(FootstepSynth.StoneLanding()));
            Assert.That(FootstepSynth.Step(FootingSound.Grass, 0), Is.Not.EqualTo(FootstepSynth.Step(FootingSound.Grass, 1)), "the variants differ");
            Assert.That(FootstepSynth.Step(FootingSound.Grass, FootstepSynth.Variants + 1), Is.EqualTo(FootstepSynth.Step(FootingSound.Grass, 1)), "and go round");
        }

        [Test]
        public void EverySoundIsHeardInsideFullScaleAndStopsWithoutAClick()
        {
            int sounds = 0;
            foreach (float[] s in Every())
            {
                sounds++;
                float peak = 0f;
                foreach (float x in s) peak = Math.Max(peak, Math.Abs(x));
                Assert.That(s.Length, Is.GreaterThan(FootstepSynth.SampleRate / 10), "a tenth of a second at least");
                Assert.That(peak, Is.GreaterThan(0.05f).And.LessThanOrEqualTo(1f));
                Assert.That(s[s.Length - 1], Is.EqualTo(0f), "faded to nothing");
            }
            Assert.That(sounds, Is.EqualTo((Enum.GetValues(typeof(FootingSound)).Length + 1) * FootstepSynth.Variants + 2),
                "every ground's steps, the strokes and the two landings");
        }

        [Test]
        public void SandIsDullerThanRockAndSoilDullerThanGrass()
        {
            Assert.That(Brightness(FootstepSynth.Step(FootingSound.Sand, 0)), Is.LessThan(Brightness(FootstepSynth.Step(FootingSound.Rock, 0))));
            Assert.That(Brightness(FootstepSynth.Step(FootingSound.Soil, 0)), Is.LessThan(Brightness(FootstepSynth.Step(FootingSound.Grass, 0))));
        }
    }
}

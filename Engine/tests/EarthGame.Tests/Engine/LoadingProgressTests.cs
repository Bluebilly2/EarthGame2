using System;
using System.Collections.Generic;
using System.Linq;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The loading screen's bar (2026-09-25): how far a loading has got, from its reports and the caller's clock.</summary>
    public sealed class LoadingProgressTests
    {
        private static double Total(IReadOnlyList<LoadingStep> table) => table.Sum(s => s.Share);

        /// <summary>The place of a phase's first step in a table.</summary>
        private static int FirstOf(IReadOnlyList<LoadingStep> table, string phase) => table.Select(s => s.Phase).ToList().IndexOf(phase);

        /// <summary>The share of a table done before a step, the step's place in it.</summary>
        private static double DoneBefore(IReadOnlyList<LoadingStep> table, int index) => table.Take(index).Sum(s => s.Share) / Total(table);

        /// <summary>Reports a table's steps at the pace its shares give, a second a share, a layer at a time in the saving step.</summary>
        private static double ReportAll(LoadingProgress progress, IReadOnlyList<LoadingStep> table, int upTo)
        {
            double t = 0.0;
            for (int i = 0; i <= upTo; i++)
            {
                if (table[i].Name == LoadingSteps.Saving)
                {
                    for (int n = 0; n < LoadingSteps.SavedLayers; n++)
                        progress.Report(LoadingSteps.SavedPrefix + "layer" + n, t + table[i].Share * n / LoadingSteps.SavedLayers);
                }
                else
                {
                    progress.Report(table[i].Name, t);
                }
                if (i < upTo) t += table[i].Share;
            }
            return t;
        }

        [Test]
        public void EachStepBeginsWhereTheSharesBeforeItEnd()
        {
            IReadOnlyList<LoadingStep> table = LoadingSteps.Making;
            var progress = new LoadingProgress();
            double t = 0.0, before = 0.0;
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].Name == LoadingSteps.Saving) progress.Report(LoadingSteps.SavedPrefix + "heights", t);
                else progress.Report(table[i].Name, t);
                double at = progress.FractionAt(t);
                Assert.That(at, Is.EqualTo(DoneBefore(table, i)).Within(1e-9).Or.GreaterThan(DoneBefore(table, i)), table[i].Name);
                Assert.That(at, Is.GreaterThanOrEqualTo(before), "it never goes back");
                Assert.That(progress.StepNumber, Is.EqualTo(i + 1));
                before = at;
                t += table[i].Share;
            }
            Assert.That(progress.StepCount, Is.EqualTo(table.Count));
            Assert.That(progress.FractionAt(t + 1000.0), Is.LessThanOrEqualTo(1.0));
        }

        [Test]
        public void WithinAStepTheBarMovesAtThePaceSoFarAndHoldsShortOfItsEnd()
        {
            IReadOnlyList<LoadingStep> table = LoadingSteps.Making;
            int plants = table.Select(s => s.Name).ToList().IndexOf("Growing plant communities");
            var progress = new LoadingProgress();
            double start = ReportAll(progress, table, plants);
            double share = table[plants].Share / Total(table);
            double from = DoneBefore(table, plants);
            // The steps before it came a second a share, so the step's own share is expected to take its share in seconds.
            double half = progress.FractionAt(start + table[plants].Share / 2.0);
            Assert.That(half, Is.EqualTo(from + share / 2.0).Within(1e-9), "halfway through its expected time, halfway through its share");
            double overrun = progress.FractionAt(start + table[plants].Share * 10.0);
            Assert.That(overrun, Is.EqualTo(from + share * LoadingProgress.MostOfAStep).Within(1e-9), "a step that overruns waits short of its end");
            Assert.That(progress.FractionAt(start), Is.EqualTo(overrun), "the bar never goes back");
        }

        [Test]
        public void TheSavingStepCountsItsLayers()
        {
            IReadOnlyList<LoadingStep> table = LoadingSteps.Making;
            int saving = table.Select(s => s.Name).ToList().IndexOf(LoadingSteps.Saving);
            var progress = new LoadingProgress();
            ReportAll(progress, table, saving - 1);
            double from = DoneBefore(table, saving), share = table[saving].Share / Total(table);
            progress.Report(LoadingSteps.SavedPrefix + "heights", 100.0);
            progress.Report(LoadingSteps.SavedPrefix + "surface", 100.0);
            Assert.That(progress.FractionAt(100.0), Is.EqualTo(from + share * 2.0 / LoadingSteps.SavedLayers).Within(1e-9));
        }

        [Test]
        public void TheSecondReportTellsAnOpeningFromAMaking()
        {
            var progress = new LoadingProgress();
            progress.Report("Reading world", 0.0);
            Assert.That(progress.Steps, Is.Null, "both begin alike");
            progress.Report(LoadingSteps.Opening[1].Name, 0.1);
            Assert.That(progress.Steps, Is.SameAs(LoadingSteps.Opening));
            Assert.That(progress.StepNumber, Is.EqualTo(2));
            Assert.That(progress.FractionAt(0.1), Is.EqualTo(DoneBefore(LoadingSteps.Opening, 1)).Within(1e-9));
        }

        [Test]
        public void AJoinAndAReportNoTableHoldsLeaveTheBarEmpty()
        {
            var progress = new LoadingProgress();
            progress.Report(LoadingSteps.PreparingGround, 0.0);
            progress.Report("Something no table holds", 1.0);
            Assert.That(progress.StepCount, Is.Zero);
            Assert.That(progress.FractionAt(5.0), Is.Zero);
            Assert.That(double.IsNaN(progress.SecondsLeftAt(5.0)), Is.True, "nothing to judge the time by");
        }

        [Test]
        public void EachPhaseIsDoneWorkingOrWaitingWithItsTime()
        {
            IReadOnlyList<LoadingStep> table = LoadingSteps.Making;
            int plants = table.Select(s => s.Name).ToList().IndexOf("Growing plant communities");
            var progress = new LoadingProgress();
            double start = ReportAll(progress, table, plants);
            double now = start + table[plants].Share / 2.0;
            IReadOnlyList<LoadingPhase> phases = progress.PhasesAt(now);
            Assert.That(phases.Select(p => p.Name), Is.EqualTo(LoadingSteps.PhasesOf(table)), "every phase, in its order");
            foreach (LoadingPhase phase in phases)
            {
                double seconds = table.Where(s => s.Phase == phase.Name).Sum(s => s.Share);
                if (phase.Name == LoadingSteps.PlantsStoneAndTrees)
                {
                    Assert.That(phase.State, Is.EqualTo(LoadingPhaseState.Working));
                    Assert.That(phase.Seconds, Is.EqualTo(table[plants].Share / 2.0).Within(1e-9), "how long it has run");
                    Assert.That(phase.Step, Is.EqualTo("Growing plant communities"));
                    Assert.That(phase.Fraction, Is.EqualTo(table[plants].Share / 2.0 / seconds).Within(1e-9), "its share of the phase");
                }
                else if (FirstOf(table, phase.Name) < plants)
                {
                    Assert.That(phase.State, Is.EqualTo(LoadingPhaseState.Done), phase.Name);
                    Assert.That(phase.Seconds, Is.EqualTo(seconds).Within(1e-9), phase.Name + " took its shares, a second a share");
                    Assert.That(phase.Fraction, Is.EqualTo(1.0));
                }
                else
                {
                    Assert.That(phase.State, Is.EqualTo(LoadingPhaseState.Waiting), phase.Name);
                    Assert.That(phase.Seconds, Is.EqualTo(seconds).Within(1e-6), phase.Name + " is expected to take its shares at the pace so far");
                }
            }
            Assert.That(new LoadingProgress().PhasesAt(1.0), Is.Empty, "nothing before the first step");
        }

        [Test]
        public void TheTimeLeftIsThePaceSoFarOverWhatRemains()
        {
            IReadOnlyList<LoadingStep> table = LoadingSteps.Making;
            int plants = table.Select(s => s.Name).ToList().IndexOf("Growing plant communities");
            var progress = new LoadingProgress();
            double start = ReportAll(progress, table, plants);
            // At a second a share throughout, what is left is the rest of the shares in seconds.
            double now = start + table[plants].Share / 2.0;
            double done = progress.FractionAt(now);
            Assert.That(progress.SecondsLeftAt(now), Is.EqualTo(Total(table) * (1.0 - done)).Within(1e-6));
            var early = new LoadingProgress();
            early.Report("Reading world", 0.0);
            Assert.That(double.IsNaN(early.SecondsLeftAt(0.05)), Is.True, "too little done to judge");
        }
    }
}

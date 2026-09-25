using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// The loading screen's tables of steps (2026-09-25) held to what a world's loading reports: a world made and opened again
    /// on the made coast, every report taken in order, the saved layers counted.
    /// </summary>
    public sealed class LoadingStepsTests
    {
        private string _root, _data, _world;
        private static readonly Region Region = new Region("fixture", "Fixture", -35.14, 150.675, TestRasters.MadeExtentM, 237, 8);
        private const string Now = "2026-09-25T00:00:00Z";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "loading-steps", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_root, "data");
            _world = Path.Combine(_root, "world");
            RegionRaster raster = TestRasters.MadeCoast();
            RegionRaster.Write(_data, "heights", raster, "heights", "f32", 1, "m", raster.Values.ToArray(), "made coast", "test", Now);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        /// <summary>The steps a list of reports stands for, in order, each run of saved layers one step.</summary>
        private static List<string> StepsOf(IEnumerable<string> reports)
        {
            var steps = new List<string>();
            foreach (string report in reports)
            {
                string step = LoadingSteps.StepOf(report);
                if (steps.Count == 0 || steps[steps.Count - 1] != step) steps.Add(step);
            }
            return steps;
        }

        /// <summary>A table's steps the world's preparation reports: all but the last, the ground round the founder, which the game reports.</summary>
        private static List<string> Reported(IReadOnlyList<LoadingStep> table)
        {
            Assert.That(table[table.Count - 1].Name, Is.EqualTo(LoadingSteps.PreparingGround), "the game's own step ends the table");
            return table.Take(table.Count - 1).Select(s => s.Name).ToList();
        }

        [Test]
        public void ANewWorldReportsTheMakingsStepsInTheirOrderAndSavesItsCountOfLayers()
        {
            var reports = new List<string>();
            using (WorldPreparation.Result made = WorldPreparation.Load(_world, _data, Region, 1347, Now, reports.Add, CancellationToken.None))
            {
                Assert.That(made.Saved, Is.Null, "made, not opened");
            }
            Assert.That(StepsOf(reports), Is.EqualTo(Reported(LoadingSteps.Making)));
            Assert.That(reports.Count(r => r.StartsWith(LoadingSteps.SavedPrefix, StringComparison.Ordinal)), Is.EqualTo(LoadingSteps.SavedLayers),
                "the layers saved one by one");
        }

        [Test]
        public void ASavedWorldReportsTheOpeningsStepsInTheirOrder()
        {
            WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None).Dispose();
            var reports = new List<string>();
            using (WorldPreparation.Result opened = WorldPreparation.Load(_world, Path.Combine(_root, "absent"), Region, 1347, Now, reports.Add, CancellationToken.None))
            {
                Assert.That(opened.Saved, Is.Not.Null, "opened, not made");
            }
            Assert.That(StepsOf(reports), Is.EqualTo(Reported(LoadingSteps.Opening)));
        }

        [Test]
        public void EveryStepHasATimeAndBothLoadingsBeginAlikeAndEndOnTheGround()
        {
            foreach (IReadOnlyList<LoadingStep> table in new[] { LoadingSteps.Making, LoadingSteps.Opening })
            {
                Assert.That(table.All(s => s.Share > 0.0), "every step takes some time");
                Assert.That(table.Select(s => s.Name).Distinct().Count(), Is.EqualTo(table.Count), "a step's name is its own");
                Assert.That(table[table.Count - 1].Name, Is.EqualTo(LoadingSteps.PreparingGround));
                Assert.That(LoadingSteps.PhasesOf(table), Is.Unique, "a phase's steps stand together, so each phase is one row");
                Assert.That(table.All(s => !string.IsNullOrEmpty(s.Phase)), "every step is in a phase");
            }
            Assert.That(LoadingSteps.Making[0].Name, Is.EqualTo(LoadingSteps.Opening[0].Name), "the first report cannot tell them apart");
            Assert.That(LoadingSteps.Opening[1].Name, Is.Not.AnyOf(LoadingSteps.Making.Select(s => s.Name).ToArray()), "the second can");
        }
    }
}

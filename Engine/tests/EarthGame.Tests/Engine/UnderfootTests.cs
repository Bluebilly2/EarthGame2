using System;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The ground underfoot (BF.4 stage two): the cover read as Pandolf, Givoni and Goldman's grounds, their coefficients as
    /// the table prints them, the walk's pace on each at the same effort as their cost equation gives it, the mover and the
    /// validator's ceiling on the same ground, and the exertion a gait's whatever the ground.
    /// </summary>
    public sealed class UnderfootTests
    {
        private const double Dt = 0.02;
        private const double BodyKg = 70.0;

        private static byte Code(GroundCover cover, int quarter) => GroundCovers.Pack(cover, quarter);

        private sealed class Level : IHeightSource
        {
            public double HeightAt(double east, double north) => 0.0;
        }

        [Test]
        public void TheCoverIsReadAsTheTablesGrounds()
        {
            Assert.That(Underfoot.Of(Code(GroundCover.Rock, 0), 0.0), Is.EqualTo(GroundType.Made));
            Assert.That(Underfoot.Of(Code(GroundCover.BareEarth, 1), 0.0), Is.EqualTo(GroundType.Firm));
            Assert.That(Underfoot.Of(Code(GroundCover.Sand, 0), 0.0), Is.EqualTo(GroundType.Loose), "a beach's dry sand");
            Assert.That(Underfoot.Of(Code(GroundCover.Sand, GroundCovers.Quarters - 1), 0.0), Is.EqualTo(GroundType.Firm), "its wettest quarter packs");
            Assert.That(Underfoot.Of(Code(GroundCover.DuneSand, GroundCovers.Quarters - 1), 0.0), Is.EqualTo(GroundType.Loose), "a dune is loose however wet");
            Assert.That(Underfoot.Of(Code(GroundCover.Heath, 0), 0.0), Is.EqualTo(GroundType.HeavyBrush));
            Assert.That(Underfoot.Of(Code(GroundCover.Bracken, 2), 0.0), Is.EqualTo(GroundType.HeavyBrush));
            Assert.That(Underfoot.Of(Code(GroundCover.Sedge, 1), 0.0), Is.EqualTo(GroundType.LightBrush), "sedge in the drier half");
            Assert.That(Underfoot.Of(Code(GroundCover.Sedge, 2), 0.0), Is.EqualTo(GroundType.Bog), "sedge in the wetter half");
            Assert.That(Underfoot.Of(Code(GroundCover.SwampFloor, 0), 0.0), Is.EqualTo(GroundType.Bog));
            Assert.That(Underfoot.Of(Code(GroundCover.Grass, 3), 0.0), Is.EqualTo(GroundType.LightBrush));
            Assert.That(Underfoot.Of(Code(GroundCover.ForestFloor, 1), 0.0), Is.EqualTo(GroundType.LightBrush));
            Assert.That(Underfoot.Of(0, 0.0), Is.EqualTo(Locomotion.TableGround), "a cover nothing has said walks as the table's ground");
            Assert.That(Underfoot.Of(Code(GroundCover.DuneSand, 0), Underfoot.WadingFromM), Is.EqualTo(Locomotion.TableGround),
                        "water over a foot leaves the walk to the wading law");
        }

        [Test]
        public void TheCoefficientsAreTheTablesOwn()
        {
            Assert.That(Locomotion.TerrainFactor(GroundType.Made), Is.EqualTo(1.0));
            Assert.That(Locomotion.TerrainFactor(GroundType.Firm), Is.EqualTo(1.1));
            Assert.That(Locomotion.TerrainFactor(GroundType.LightBrush), Is.EqualTo(1.2));
            Assert.That(Locomotion.TerrainFactor(GroundType.HeavyBrush), Is.EqualTo(1.5));
            Assert.That(Locomotion.TerrainFactor(GroundType.Bog), Is.EqualTo(1.8));
            Assert.That(Locomotion.TerrainFactor(GroundType.Loose), Is.EqualTo(2.1));
            Assert.That(Locomotion.Faster(GroundType.Loose, GroundType.Made), Is.EqualTo(GroundType.Made));
            Assert.That(Locomotion.Faster(GroundType.Bog, GroundType.HeavyBrush), Is.EqualTo(GroundType.HeavyBrush));
        }

        [Test]
        public void EachGroundsPaceCostsWhatTheTablesGroundsDoes()
        {
            // Pandolf's own equation, not the pace's arithmetic: at the table's walking pace on the table's ground, and at each
            // ground's pace on that ground, the cost of moving on the flat is the same.
            double tablePace = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0);
            double tableCost = Locomotion.MetabolicCostW(BodyKg, 0.0, tablePace, 0.0, Locomotion.TableGround);
            foreach (GroundType ground in (GroundType[])Enum.GetValues(typeof(GroundType)))
            {
                double pace = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0, ground);
                Assert.That(pace, Is.LessThan(Locomotion.GaitChangeMs), "inside the walking range of the equation");
                Assert.That(Locomotion.MetabolicCostW(BodyKg, 0.0, pace, 0.0, ground), Is.EqualTo(tableCost).Within(1e-9), ground.ToString());
            }
            Assert.That(Locomotion.GroundPace(GroundType.Loose), Is.EqualTo(0.756).Within(0.001), "loose sand about three-quarters");
            Assert.That(Locomotion.GroundPace(GroundType.Bog), Is.EqualTo(0.816).Within(0.001));
            Assert.That(Locomotion.GroundPace(GroundType.HeavyBrush), Is.EqualTo(0.894).Within(0.001));
            Assert.That(Locomotion.GroundPace(GroundType.Firm), Is.EqualTo(1.044).Within(0.001));
            Assert.That(Locomotion.GroundPace(GroundType.Made), Is.EqualTo(1.095).Within(0.001));
            Assert.That(Locomotion.GroundPace(Locomotion.TableGround), Is.EqualTo(1.0), "the table's ground walks as it did");
        }

        [Test]
        public void TheMoverWalksEachGroundAtItsPace()
        {
            IWorldCollision level = HeightfieldCollision.For(new Level(), MoverConfig.Default, false);
            foreach (GroundType ground in new[] { GroundType.Loose, Locomotion.TableGround, GroundType.Made })
            {
                MoverState s = MoverState.AtRest(0.0, 0.0, 0.0);
                s.Grounded = true;
                for (int i = 0; i < 300; i++) s = Mover.Step(s, MoverInput.Walk(1.0, 0.0), Dt, level, null, 1.0, ground);
                Assert.That(s.HorizontalSpeed, Is.EqualTo(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0, ground)).Within(1e-6), ground.ToString());
            }
        }

        [Test]
        public void TheCeilingIsTheGroundsAndAFallsIsTheFastest()
        {
            MoverConfig mover = MoverConfig.Default;
            MovementRules rules = new MovementRules();
            double interval = 0.2;
            MoverState last = MoverState.AtRest(0.0, 0.0, 0.0);
            last.Grounded = true;
            // A run over rock is faster than the table's run, and passes on rock; the table's run on dry sand does not.
            double rockRun = mover.MaxHorizontalSpeedAt(1.0, GroundType.Made);
            MoverState reported = MoverState.AtRest(rockRun * interval, 0.0, 0.0);
            reported.Grounded = true;
            Assert.That(MovementValidator.Check(last, true, reported, interval, new Level(), 1000.0, mover, rules, 0.0, 1.0, 0.0, GroundType.Made), Is.Null);
            double tableRun = mover.MaxHorizontalSpeedAt(1.0);
            double overSand = tableRun * 1.0;
            Assert.That(overSand, Is.GreaterThan(mover.MaxHorizontalSpeedAt(1.0, GroundType.Loose) * rules.SpeedTolerance), "so the test is a real one");
            reported = MoverState.AtRest(overSand * interval, 0.0, 0.0);
            reported.Grounded = true;
            Assert.That(MovementValidator.Check(last, true, reported, interval, new Level(), 1000.0, mover, rules, 0.0, 1.0, 0.0, GroundType.Loose), Is.Not.Null,
                        "the table's run through dry sand is faster than a body can go there");
            Assert.That(MovementValidator.FallCeiling(0.0, 0.0, mover), Is.EqualTo(rockRun).Within(1e-12), "a fall is allowed the fastest ground's run");
        }

        [Test]
        public void ARunnerOffRockOntoSandBrakesWithoutACorrection()
        {
            // Rock to twenty metres east, dry sand beyond: a founder at a full run crosses, and their reports are judged as the
            // server judges them, on the faster of the grounds each leaves and reaches, with the stride's allowance.
            IWorldCollision level = HeightfieldCollision.For(new Level(), MoverConfig.Default, false);
            MoverConfig mover = MoverConfig.Default;
            MovementRules rules = new MovementRules();
            Func<double, GroundType> groundAt = e => e < 20.0 ? GroundType.Made : GroundType.Loose;
            MoverState body = MoverState.AtRest(0.0, 0.0, 0.0);
            body.Grounded = true;
            MoverState accepted = body;
            MovementValidator.Landing stride = default;
            MoverInput run = MoverInput.Walk(1.0, 0.0);
            run.Sprint = true;
            double sinceSend = 0.0, fastestOnSand = 0.0;
            uint sequence = 0;
            int withoutTheStride = 0;
            for (int step = 1; step <= 2000 && body.East < 40.0; step++)
            {
                body = Mover.Step(body, run, Dt, level, null, 1.0, groundAt(body.East));
                sinceSend += Dt;
                if (sinceSend < 0.05 - 1e-4) continue;
                sinceSend -= 0.05;
                sequence++;
                GroundType judged = Locomotion.Faster(groundAt(accepted.East), groundAt(body.East));
                string reason = MovementValidator.Check(accepted, true, body, 0.05, new Level(), 1000.0, mover, rules, 0.0, 1.0,
                                                        stride.AllowanceAt(sequence, 0.05, mover), judged);
                Assert.That(reason, Is.Null, "corrected at east " + body.East.ToString("0.0") + " at " + body.HorizontalSpeed.ToString("0.00") + " m/s: " + reason);
                if (MovementValidator.Check(accepted, true, body, 0.05, new Level(), 1000.0, mover, rules, 0.0, 1.0, 0.0, judged) != null) withoutTheStride++;
                double groundRun = mover.MaxHorizontalSpeedAt(1.0, judged);
                if (body.Grounded && groundRun > stride.AllowanceAt(sequence, 0.05, mover))
                    stride = new MovementValidator.Landing { CeilingMs = groundRun, Sequence = sequence };
                if (body.East > 20.0) fastestOnSand = Math.Max(fastestOnSand, body.HorizontalSpeed);
                accepted = body;
            }
            Assert.That(body.East, Is.GreaterThanOrEqualTo(40.0), "the runner crossed onto the sand and ran on");
            Assert.That(fastestOnSand, Is.GreaterThan(mover.MaxHorizontalSpeedAt(1.0, GroundType.Loose) * rules.SpeedTolerance),
                        "on the sand faster than its ceiling for a while, so the test is a real one");
            Assert.That(withoutTheStride, Is.GreaterThan(0), "and the rule without the stride's allowance corrects it");
        }

        [Test]
        public void TheExertionIsTheGaitsWhateverTheGround()
        {
            double sandRun = Locomotion.SpeedMs(Locomotion.FastestWalkSlope, Gait.Running, 1.0, GroundType.Loose);
            Assert.That(Warmth.ExertionOf(sandRun), Is.EqualTo(Exertion.Walking), "judged by the speed alone, a run through sand reads as a walk");
            Assert.That(Warmth.ExertionOf(sandRun / Locomotion.GroundPace(GroundType.Loose)), Is.EqualTo(Exertion.Running), "judged by the table's pace, it is a run");
            double sandWalk = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0, GroundType.Loose);
            Assert.That(Warmth.ExertionOf(sandWalk / Locomotion.GroundPace(GroundType.Loose)), Is.EqualTo(Exertion.Walking));
        }
    }
}

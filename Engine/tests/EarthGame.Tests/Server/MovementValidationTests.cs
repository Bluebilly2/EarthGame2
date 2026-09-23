using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// Client-authoritative movement, server-validated (ARCHITECTURE §7, §9), over the in-memory transport with
    /// every message serialised. The ground is the tiny fixture raster and the region is a 40 m square around it,
    /// so the spawn, the ground height and the edge are all numbers this file can state.
    /// </summary>
    public sealed class MovementValidationTests
    {
        /// <summary>A region the size of the fixture raster, waking at its centre: spawn is (0, 127, 0), the bump cell.</summary>
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 40.0, 237, 8.0);

        private static Heightfield Ground() => new Heightfield(RegionRaster.Load(TestPaths.Fixture("raster", "tiny.json")));

        private static WorldState World() => new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), Ground());

        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public IServerTransport ServerTransport;
            public long Ms;

            public void Pump(int rounds = 3, double dt = 0.05)
            {
                for (int i = 0; i < rounds; i++)
                {
                    Client.Update(Ms);
                    Server.Update(dt);
                    Client.Update(Ms);
                    Ms += (long)(dt * 1000);
                }
            }

            public PlayerSession Session => Server.Sessions[0];
        }

        private static Rig Connect(WorldState world = null, ServerConfig config = null)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig();
            rig.ServerTransport = st;
            rig.Server = new GameServer(config ?? new ServerConfig(), st, world ?? World());
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(5);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected));
            return rig;
        }

        private static MoverState GroundedAt(Heightfield ground, double east, double north)
        {
            MoverState s = MoverState.AtRest(east, ground.HeightAt(east, north), north);
            s.Grounded = true;
            return s;
        }

        [Test]
        public void AFlightStandsOnADevelopmentServerAndIsCorrectedOnAnyOther()
        {
            // Thirty metres over the ground at 25 m/s, as a developer's flight goes (M1.5e).
            Heightfield ground = Ground();
            double high = ground.HeightAt(0.0, 0.0) + 30.0;
            // Three servers: one not started for development; one that is, whose player has not switched developer mode on
            // (M1.E, 2026-09-20: a development server once let every player fly); and one whose player has.
            foreach ((bool development, bool switched) in new[] { (false, false), (true, false), (true, true) })
            {
                bool allowed = development && switched;
                Rig rig = Connect(config: new ServerConfig { Movement = new MovementRules { AllowFlight = development } });
                if (switched)
                {
                    rig.Client.SendDeveloperMode(true);
                    rig.Pump(2);
                }
                for (int i = 0; i <= 4; i++)
                {
                    MoverState flying = MoverState.AtRest(i * 1.25, high, 0.0);
                    flying.VelEast = 25.0;
                    rig.Client.SendMove(MoverInput.None, 90f, 0f, flying);
                    rig.Pump(1);
                }
                if (!allowed)
                {
                    Assert.That(rig.Client.CorrectionCount, Is.GreaterThan(0),
                        development ? "a development server corrects a player who has not switched developer mode on" : "any other server corrects it");
                    Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("speed"));
                    continue;
                }
                Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0), "a development server lets it stand");
                Assert.That(rig.Session.Body.East, Is.EqualTo(5.0).Within(1e-9));
                rig.Client.SendMove(MoverInput.None, 90f, 0f, MoverState.AtRest(25.0, high, 0.0));
                rig.Pump(2);
                Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("outside"), "and holds the region's edge even so");
            }
        }

        /// <summary>
        /// A founder sliding down a face too steep to stand on is only falling along it (the bug hunt of 2026-09-13): the
        /// mover gives the slide to gravity, and the server must believe what gravity gives. Reported as the game reports,
        /// every 0.05 s of fixed steps of 0.02 s, so after three steps and then after two, each judged over a server tick.
        /// </summary>
        [Test]
        public void ASlideDownAFaceTooSteepToStandOnIsNotCorrected()
        {
            const double grade = 1.8; // 61 degrees, the face of MoverTests' own slide
            IWorldCollision face = HeightfieldCollision.For(new Face(e => e * grade), MoverConfig.Default, false);
            MovementRules rules = new MovementRules();
            MoverState body = MoverState.AtRest(40.0, 40.0 * grade, 0.0);
            MoverState accepted = body;
            // Where the founder stood, as the server keeps it: the first report's height, then any report on their feet.
            double stoodUp = body.Up;
            double sinceSend = 0.0;
            for (int step = 1; step <= 150; step++)
            {
                body = Mover.Step(body, MoverInput.None, 0.02, face);
                sinceSend += 0.02;
                if (sinceSend < 0.05 - 1e-4) continue;
                sinceSend -= 0.05;
                string reason = MovementValidator.Check(accepted, true, body, 0.05, null, 1000.0, MoverConfig.Default, rules, stoodUp);
                Assert.That(reason, Is.Null, "corrected " + (step * 0.02).ToString("0.00") + " s into the slide at "
                                             + body.HorizontalSpeed.ToString("0.00") + " m/s: " + reason);
                accepted = body;
                if (body.Grounded) stoodUp = body.Up;
            }
            Assert.That(body.HorizontalSpeed, Is.GreaterThan(MoverConfig.Default.MaxHorizontalSpeed * rules.SpeedTolerance),
                "the slide outran the ceiling a run is held to, so the test is not passed by a slow slide");
        }

        /// <summary>
        /// A founder who slides down a face and lands on their feet lands faster than a run and brakes (2026-09-23, DEBTS "A
        /// slide's landing is corrected"): the corpus's walkers slid 1.8 m off a dune face at the walk's limit and were corrected
        /// three times each on landing. The landing report is judged as the fall it ends, and the reports after it by what the
        /// fall allowed less the brake's work since; the server keeps the landing as this test does (<see cref="MovementValidator.Landing"/>).
        /// </summary>
        [Test]
        public void ALandingFromASlideBrakesWithoutACorrection()
        {
            const double grade = 1.8;
            // A face falling west onto flat ground at the datum: the founder starts four metres up it.
            IWorldCollision face = HeightfieldCollision.For(new Face(e => e > 0.0 ? e * grade : 0.0), MoverConfig.Default, false);
            MovementRules rules = new MovementRules();
            MoverConfig mover = MoverConfig.Default;
            MoverState body = MoverState.AtRest(4.0 / grade, 4.0, 0.0);
            MoverState accepted = body;
            double stoodUp = body.Up, sinceSend = 0.0, fastestLanded = 0.0;
            uint sequence = 0;
            MovementValidator.Landing landing = default;
            int oldRuleWouldCorrect = 0;
            bool landed = false;
            for (int step = 1; step <= 250; step++)
            {
                body = Mover.Step(body, MoverInput.None, 0.02, face);
                sinceSend += 0.02;
                if (sinceSend < 0.05 - 1e-4) continue;
                sinceSend -= 0.05;
                sequence++;
                double allowance = landing.AllowanceAt(sequence, 0.05, mover);
                string reason = MovementValidator.Check(accepted, true, body, 0.05, null, 1000.0, mover, rules, stoodUp, 1.0, allowance);
                Assert.That(reason, Is.Null, "corrected " + (step * 0.02).ToString("0.00") + " s in at " + body.HorizontalSpeed.ToString("0.00") + " m/s, "
                                             + (body.Grounded ? "on their feet" : "off them") + ": " + reason);
                // What the rule before the landing's allowance would have said of the same report.
                if (MovementValidator.Check(accepted, true, body, 0.05, null, 1000.0, mover, rules, stoodUp) != null) oldRuleWouldCorrect++;
                if (!accepted.Grounded && body.Grounded)
                {
                    landing = new MovementValidator.Landing { CeilingMs = MovementValidator.FallCeiling(stoodUp, body.Up, mover), Sequence = sequence };
                    landed = true;
                }
                if (landed && body.Grounded) fastestLanded = System.Math.Max(fastestLanded, body.HorizontalSpeed);
                accepted = body;
                if (body.Grounded) stoodUp = body.Up;
            }
            Assert.That(landed, Is.True, "the founder came down on the flat");
            Assert.That(fastestLanded, Is.GreaterThan(mover.MaxHorizontalSpeed * rules.SpeedTolerance), "and on their feet faster than a run's ceiling, so the test is not passed by a slow landing");
            Assert.That(oldRuleWouldCorrect, Is.GreaterThan(0), "the rule without the landing's allowance corrects it");
            Assert.That(body.HorizontalSpeed, Is.LessThan(0.5), "and the brake has stopped the founder by the end");
            Assert.That(landing.AllowanceAt(sequence, 0.05, mover), Is.LessThan(mover.MaxHorizontalSpeed), "by when the landing allows nothing more than a run");
            Assert.That(landing.AllowanceAt(sequence + 100, 0.05, mover), Is.EqualTo(0.0), "and a little later nothing at all");
        }

        /// <summary>
        /// The ceiling is the body's (FP.1): a founder told a work capacity is held to the run that capacity allows, by the
        /// mover's own rule, and a full-speed run from a body told a tenth of its water gone is corrected.
        /// </summary>
        [Test]
        public void TheCeilingFallsWithTheCapacityTheFounderWasTold()
        {
            MoverConfig mover = MoverConfig.Default;
            MoverState grounded = MoverState.AtRest(0.0, 0.0, 0.0);
            grounded.Grounded = true;
            double full = MovementValidator.HorizontalCeiling(grounded, 0.0, mover);
            double weakened = MovementValidator.HorizontalCeiling(grounded, 0.0, mover, Hydration.CapacityOf(0.9));
            Assert.That(full, Is.EqualTo(mover.MaxHorizontalSpeed).Within(1e-9));
            Assert.That(weakened, Is.EqualTo(mover.MaxHorizontalSpeedAt(0.5)).Within(1e-9), "a tenth lost is half the capacity, by the table");
            Assert.That(weakened, Is.LessThan(full));
            Assert.That(weakened / full, Is.EqualTo((0.45 + 0.55 * 0.5) / 1.0).Within(1e-9), "the mover's own scaling of a body's speed");

            MovementRules rules = new MovementRules();
            MoverState next = grounded;
            next.East = full * 0.05 * 0.99;
            next.Grounded = true;
            Assert.That(MovementValidator.Check(grounded, true, next, 0.05, null, 1000.0, mover, rules, 0.0), Is.Null, "a full run for a full body");
            Assert.That(MovementValidator.Check(grounded, true, next, 0.05, null, 1000.0, mover, rules, 0.0, Hydration.CapacityOf(0.9)), Is.Not.Null, "and too fast for a body told half its capacity");
        }

        /// <summary>
        /// The server keeps where each founder last stood (M1.5f): off their feet below it, a founder is believed as fast as
        /// the height lost allows; level with it or above it, they are held to a run; and standing again moves it. Over the
        /// wire, on a world without terrain, so the heights are the ones stated here.
        /// </summary>
        [Test]
        public void OffTheirFeetAFounderMayCrossTheGroundAsFastAsTheHeightLostSinceTheyStood()
        {
            WorldState bare = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            Rig rig = Connect(bare);
            MoverState stood = MoverState.AtRest(0.0, 100.0, 0.0);
            stood.Grounded = true;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, stood);
            rig.Pump(1);
            Assert.That(rig.Session.StoodUp, Is.EqualTo(100.0), "where they stood");

            // Two and a half metres below it at 9 m/s, where a run (3.6 since M1.5h) and that fall allow 9.8 m/s with the tolerance.
            rig.Client.SendMove(MoverInput.None, 0f, 0f, MoverState.AtRest(0.45, 97.5, 0.0));
            rig.Pump(1);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0), "a fall's speed is believed");
            Assert.That(rig.Session.StoodUp, Is.EqualTo(100.0), "and a fall keeps where they stood");

            // Back up level with where they stood, off their feet at 13 m/s: nothing drives that but a cheat.
            rig.Client.SendMove(MoverInput.None, 0f, 0f, MoverState.AtRest(1.1, 100.0, 0.0));
            rig.Pump(1);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(1), "a body hanging level with where it stood is held to a run");
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("speed"));

            MoverState landed = MoverState.AtRest(0.8, 96.0, 0.0);
            landed.Grounded = true;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, landed);
            rig.Pump(1);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(1));
            Assert.That(rig.Session.StoodUp, Is.EqualTo(96.0), "standing again moves it");

            // Two metres above where they now stood, off their feet at 9 m/s: height gained buys no speed.
            rig.Client.SendMove(MoverInput.None, 0f, 0f, MoverState.AtRest(1.25, 98.0, 0.0));
            rig.Pump(1);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(2), "a body above where it stood is held to a run");
        }

        /// <summary>A founder who comes back off their feet is judged from the height their save held (M1.5f), not held to a run.</summary>
        [Test]
        public void AFounderWhoRejoinsOffTheirFeetIsJudgedFromTheHeightTheirSaveHeld()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            GameServer server = new GameServer(new ServerConfig(), st, new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock()));
            SavedPlayer saved = default;
            saved.Name = "William";
            saved.Body = MoverState.AtRest(0.0, 100.0, 0.0);
            server.RememberPlayers(new[] { saved });
            server.Listen(1);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 1, "William", "");
            long ms = 0;
            for (int i = 0; i < 5; i++)
            {
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            Assert.That(client.State, Is.EqualTo(ClientState.Connected));
            Assert.That(server.Sessions[0].StoodUp, Is.EqualTo(100.0), "where the save held them");

            client.SendMove(MoverInput.None, 0f, 0f, MoverState.AtRest(0.45, 97.5, 0.0));
            client.Update(ms);
            server.Update(0.05);
            client.Update(ms);
            Assert.That(client.CorrectionCount, Is.EqualTo(0), "falling on from where the save held them, at 9 m/s (a run of 3.6 and that fall allow 9.8 with the tolerance)");
        }

        private sealed class Face : IHeightSource
        {
            private readonly System.Func<double, double> _up;
            public Face(System.Func<double, double> up) { _up = up; }
            public double HeightAt(double east, double north) => _up(east);
        }

        [Test]
        public void WelcomeCarriesTheSpawnOnTheGround()
        {
            Rig rig = Connect();
            Assert.That(rig.Client.Welcome.SpawnEast, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(rig.Client.Welcome.SpawnNorth, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(rig.Client.Welcome.SpawnUp, Is.EqualTo(127.0).Within(1e-6), "the centre cell of the fixture, bump included");
        }

        [Test]
        public void ALegalWalkIsAcceptedWithoutCorrection()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverInput input = MoverInput.Walk(1.0, 0.0);
            for (int i = 1; i <= 20; i++)
            {
                double east = i * 0.05;
                rig.Client.SendMove(input, 90f, 0f, GroundedAt(ground, east, 0.0));
                rig.Pump(1);
            }
            Assert.That(rig.Session.MovesAccepted, Is.EqualTo(20));
            Assert.That(rig.Session.Corrections, Is.EqualTo(0));
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0));
            Assert.That(rig.Session.Body.East, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(rig.Session.YawDeg, Is.EqualTo(90f));
        }

        [Test]
        public void ReportsBunchedByJitterAreJudgedByTheirOwnSpacing()
        {
            // Three reports of a 4 m/s walk arrive in one server update, as 100 ms of jitter delivers them; measured
            // over one tick each they were 8 and 12 m/s against a 7 m/s ceiling (the first corpus run, 2026-09-08).
            Rig rig = Connect();
            Heightfield ground = Ground();
            rig.Client.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 0.0, 0.0));
            rig.Pump(2);
            for (int i = 1; i <= 3; i++)
                rig.Client.SendMove(MoverInput.Walk(1.0, 0.0), 90f, 0f, GroundedAt(ground, i * 0.2, 0.0));
            rig.Pump(2);
            Assert.That(rig.Session.Corrections, Is.EqualTo(0));
            Assert.That(rig.Session.MovesAccepted, Is.EqualTo(4));
            Assert.That(rig.Session.Body.East, Is.EqualTo(0.6).Within(1e-9));
        }

        [Test]
        public void AClaimedGapBuysNoMoreTimeThanReallyPassed()
        {
            // A client that skips forty sequence numbers per report claims two seconds of walking each time (eight
            // metres, back and forth inside the 40 m fixture: 4 m/s, under the run's ceiling of 4.5 since M1.5h's
            // walker's law); the credit it banked covers the first two claims,
            // and the third is judged over the second and a bit that really passed.
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            GameServer server = new GameServer(new ServerConfig(), st, World());
            server.Listen(1);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 1, "William", "");
            long ms = 0;
            for (int i = 0; i < 5; i++)
            {
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            Assert.That(client.State, Is.EqualTo(ClientState.Connected));
            Heightfield ground = Ground();
            PacketWriter w = new PacketWriter();
            uint sequence = 0;
            for (int i = 0; i <= 3; i++)
            {
                PlayerMoveMessage move;
                move.Sequence = sequence += 40;
                move.Input = MoverInput.Walk(1.0, 0.0);
                move.YawDeg = 90f;
                move.PitchDeg = 0f;
                move.Body = GroundedAt(ground, i % 2 == 0 ? 0.0 : 8.0, 0.0);
                w.Reset();
                move.Write(w);
                ct.Connection.Send(w.Written, Delivery.Unreliable);
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            PlayerSession session = server.Sessions[0];
            Assert.That(session.MovesAccepted, Is.EqualTo(3), "the first report and two seconds' worth twice");
            Assert.That(session.Corrections, Is.EqualTo(1));
            Assert.That(client.LastCorrection.Reason, Does.StartWith("speed"));
            Assert.That(session.Body.East, Is.EqualTo(0.0).Within(1e-9), "held where the last honest report left it");
        }

        [Test]
        public void ATeleportIsCorrectedToTheLastAcceptedPlace()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            rig.Client.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 0.0, 0.0));
            rig.Pump(1);
            uint offending = rig.Client.SendMove(MoverInput.Walk(1.0, 0.0), 0f, 0f, GroundedAt(ground, 15.0, 0.0));
            rig.Pump(2);
            Assert.That(rig.Session.Corrections, Is.EqualTo(1));
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(1));
            Assert.That(rig.Client.LastCorrection.Sequence, Is.EqualTo(offending), "the correction names the move it answers");
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("speed"));
            Assert.That(rig.Client.LastCorrection.Body.East, Is.EqualTo(0.0).Within(1e-9), "back to the last accepted place");
            Assert.That(rig.Client.LastCorrection.Body.Grounded, Is.True);
            Assert.That(rig.Session.Body.East, Is.EqualTo(0.0).Within(1e-9), "the server did not move the player");
        }

        [Test]
        public void FloatingWhileClaimingGroundedIsCorrected()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverState floating = GroundedAt(ground, 0.0, 0.0);
            floating.Up += 5.0;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, floating);
            rig.Pump(2);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(1));
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("ground"));
            Assert.That(rig.Client.LastCorrection.Body.Up, Is.EqualTo(127.0).Within(1e-9), "with no accepted body yet, the correction is the spawn");
        }

        [Test]
        public void OutsideTheRegionIsCorrected()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            rig.Client.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 25.0, 0.0));
            rig.Pump(2);
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("outside"));
        }

        [Test]
        public void BelowTheGroundIsCorrectedEvenWhenAirborne()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverState buried = MoverState.AtRest(0.0, ground.HeightAt(0.0, 0.0) - 3.0, 0.0);
            rig.Client.SendMove(MoverInput.None, 0f, 0f, buried);
            rig.Pump(2);
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("below"));
        }

        /// <summary>
        /// Why the solo server said nothing while William fell through a face on 2026-09-21 (DEBTS): he had developer mode on
        /// for the panel, and with it on a development server holds nothing but the region's edge on a founder's moves (M1.E,
        /// ruling 39), the ground included, because a report carries no word of whether the founder is in the noclip flight
        /// the mode allows. The same report from a founder who has not switched the mode on is corrected as below the ground.
        /// </summary>
        [Test]
        public void ADeveloperBelowTheGroundIsLetBeAndAPlayerIsCorrected()
        {
            Heightfield ground = Ground();
            MoverState buried = MoverState.AtRest(0.0, ground.HeightAt(0.0, 0.0) - 5.0, 0.0);
            foreach (bool switched in new[] { true, false })
            {
                Rig rig = Connect(config: new ServerConfig { Movement = new MovementRules { AllowFlight = true } });
                if (switched)
                {
                    rig.Client.SendDeveloperMode(true);
                    rig.Pump(2);
                }
                rig.Client.SendMove(MoverInput.None, 0f, 0f, buried);
                rig.Pump(2);
                if (switched) Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0), "developer mode on: five metres under the ground is let be");
                else Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("below"), "developer mode off: corrected as below the ground");
            }
        }

        [Test]
        public void AnAirborneReportAboveTheGroundIsFine()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverState falling = MoverState.AtRest(0.0, ground.HeightAt(0.0, 0.0) + 3.0, 0.0);
            falling.VelUp = -5.0;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, falling);
            rig.Pump(2);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0), "being in the air over the ground is not a violation");
            Assert.That(rig.Session.HasBody, Is.True);
        }

        /// <summary>One law, three readers (M1.5h): the ceiling the server holds a run to is the walker's table's fastest point times the run's multiple.</summary>
        [Test]
        public void TheCeilingIsTheWalkersLawsFastestRun()
        {
            Assert.That(MoverConfig.Default.MaxHorizontalSpeed, Is.EqualTo(1.39 * Locomotion.GaitMultiplier(Gait.Running)).Within(0.02));
        }

        [Test]
        public void WithoutTerrainTheServerStillHoldsTheEdgeAndTheSpeed()
        {
            WorldState bare = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            Rig rig = Connect(bare);
            MoverState anywhere = MoverState.AtRest(100.0, 50.0, -200.0);
            anywhere.Grounded = true;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, anywhere);
            rig.Pump(2);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0), "no ground to check against, so grounded at 50 m is taken on trust");
            rig.Client.SendMove(MoverInput.None, 0f, 0f, MoverState.AtRest(Region.Bherwerre.HalfExtentM + 1.0, 0.0, 0.0));
            rig.Pump(2);
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("outside"));
        }

        [Test]
        public void ATerrainOfAnotherSizeIsNotThisWorlds()
        {
            Assert.That(() => new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock(), Ground()),
                Throws.ArgumentException.With.Message.Contains("40"));
        }

        [Test]
        public void TwoClientsSeeEachOthersBodiesAndNotTheirOwn()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct1);
            GameServer server = new GameServer(new ServerConfig(), st, World());
            server.Listen(1);
            GameClient c1 = new GameClient(ct1);
            c1.Connect("memory", 1, "William", "");
            IClientTransport ct2 = InMemoryTransport.CreateClient(st);
            GameClient c2 = new GameClient(ct2);
            c2.Connect("memory", 1, "Guest", "");
            for (int i = 0; i < 6; i++)
            {
                c1.Update(i);
                c2.Update(i);
                server.Update(0.05);
                c1.Update(i);
                c2.Update(i);
            }
            Assert.That(c1.State, Is.EqualTo(ClientState.Connected));
            Assert.That(c2.State, Is.EqualTo(ClientState.Connected));
            Heightfield ground = Ground();
            c1.SendMove(MoverInput.None, 10f, 0f, GroundedAt(ground, 2.0, 3.0));
            c2.SendMove(MoverInput.None, 20f, 0f, GroundedAt(ground, -4.0, 1.0));
            for (int i = 0; i < 4; i++)
            {
                c1.Update(i);
                c2.Update(i);
                server.Update(0.05);
                c1.Update(i);
                c2.Update(i);
            }
            Assert.That(c1.Others.ContainsKey(c2.Welcome.SessionId), Is.True, "client 1 holds client 2's body");
            Assert.That(c1.Others.ContainsKey(c1.Welcome.SessionId), Is.False, "and not its own");
            Assert.That(c1.Others[c2.Welcome.SessionId].Body.East, Is.EqualTo(-4.0).Within(1e-9));
            Assert.That(c1.Others[c2.Welcome.SessionId].YawDeg, Is.EqualTo(20f));
            Assert.That(c2.Others[c1.Welcome.SessionId].Body.North, Is.EqualTo(3.0).Within(1e-9));
        }
    }
}

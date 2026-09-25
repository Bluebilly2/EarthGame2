using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The fire's burning (BF.5 part one, promises 1 to 5): the heat of wood and its water, the char and the gas, the crib
    /// rate by thickness, catching by heat and water, the flame's critical mass flux, tinder's spread, and a fire of pieces
    /// that builds from tinder through kindling to fuel, dies when starved, smoulders when wet, burns faster in wind, keeps
    /// its energy and does not care how its time is cut. The published numbers are written here again, not read from the
    /// classes, so a slip in a class is a red test.
    /// </summary>
    public sealed class FireBurningTests
    {
        private static readonly Wood Banksia = Wood.CoastBanksia;
        private static readonly Wood Blackbutt = Wood.Blackbutt;
        private static readonly Wood Paperbark = Wood.SwampPaperbark;

        private static FuelPiece Tinder(double moisture) => FuelPiece.Bundle(Paperbark, 0.010, 0.0003, 400.0, 0.10, moisture);

        /// <summary>How long a tinder bundle kindled alone keeps its flame, s, walked in twentieths of a second.</summary>
        private static double TinderFlameSeconds(in FireAir air)
        {
            Fire fire = new Fire();
            FuelPiece tinder = Tinder(0.08);
            fire.Kindle(tinder, air);
            double t = 0.0;
            while (tinder.Phase == BurnPhase.Flaming && t < 600.0)
            {
                fire.Advance(0.05, air);
                t += 0.05;
            }
            return t;
        }

        /// <summary>Tinder, twenty twigs, ten pencil sticks, six thumb-thick sticks and three wrist-thick logs: a lay as the manuals build one.</summary>
        private static Fire Ladder(double logMoisture, out FuelPiece[] logs, out FuelPiece tinder)
        {
            Fire fire = new Fire();
            tinder = Tinder(0.08);
            fire.Add(tinder);
            for (int i = 0; i < 20; i++) fire.Add(FuelPiece.Stick(Banksia, 0.004, 0.3, 0.12));
            for (int i = 0; i < 10; i++) fire.Add(FuelPiece.Stick(Banksia, 0.008, 0.4, 0.12));
            for (int i = 0; i < 6; i++) fire.Add(FuelPiece.Stick(Banksia, 0.015, 0.4, 0.12));
            logs = new FuelPiece[3];
            for (int i = 0; i < 3; i++)
            {
                logs[i] = FuelPiece.Stick(Blackbutt, 0.05, 0.5, logMoisture);
                fire.Add(logs[i]);
            }
            return fire;
        }

        [Test]
        public void TheHeatOfWoodIsItsNetValueLessWhatItsWaterCosts()
        {
            // ISO 1928 / ISO 18125: q_net,d = q_gr,d - 212.2 H - 0.8 (O + N), J/g with H, O, N in per cent; 19.0 MJ/kg gross, H 6, O+N 44.
            double net = 19.0 - (212.2 * 6.0 + 0.8 * 44.0) / 1000.0;
            Assert.That(Combustion.NetHeatMJPerKgDry(Banksia), Is.EqualTo(net).Within(1e-9));
            Assert.That(net, Is.InRange(17.5, 18.0), "dry wood's net heat, a little over 17.5 MJ/kg");
            // Per kilogram of dry wood its water costs 2.443 MJ/kg at 25 C, the standard's own constant.
            Assert.That(Combustion.HeatMJPerKgDry(Banksia, 0.15), Is.EqualTo(net - 2.443 * 0.15).Within(1e-9));
            // Per kilogram as it is, ISO 16993's own form: q_net,ar = q_net,d (1 - M/100) - 24.43 M, M per cent of the wet mass.
            foreach (double u in new[] { 0.0, 0.12, 0.25, 0.6, 1.0 })
            {
                double m = 100.0 * u / (1.0 + u);
                double iso = net * (1.0 - m / 100.0) - 24.43 * m / 1000.0;
                Assert.That(Combustion.HeatMJPerKgAsItIs(Banksia, u), Is.EqualTo(iso).Within(1e-9), "at " + u + " of dry mass");
            }
            // Green blackbutt at its own water (0.6 of dry mass) gives about two-thirds of what air-dry wood gives, kilogram for
            // kilogram as cut: most of the loss is the water's weight, the rest its latent heat.
            double green = Combustion.HeatMJPerKgAsItIs(Blackbutt, Blackbutt.GreenMoisture);
            double airDry = Combustion.HeatMJPerKgAsItIs(Blackbutt, 0.12);
            Assert.That(green / airDry, Is.InRange(0.6, 0.7));
            // The second source: the published tables of firewood by its water (wet basis). Krajnc's FAO handbook (2015, table 14,
            // from 18.5 MJ/kg dry): 14.31 MJ/kg at 20 per cent, 12.22 at 30, 8.03 at 50; Forest Research's 14.7 at 20. This wood's
            // net dry heat, from the table's gross 19.0, is lower than theirs (17.7 against 18.5 and 19), so its values sit lower
            // by that and no more: within 8 per cent of Krajnc's at every moisture.
            double[] wet = { 0.20, 0.30, 0.50 }, krajnc = { 14.31, 12.22, 8.03 };
            for (int i = 0; i < wet.Length; i++)
            {
                double u = wet[i] / (1.0 - wet[i]);
                Assert.That(Combustion.HeatMJPerKgAsItIs(Banksia, u) / krajnc[i], Is.InRange(0.92, 1.0), "at " + wet[i] + " wet basis");
            }
        }

        [Test]
        public void ThePiecesCharAndGasCarryTheWholeOfItsHeat()
        {
            double net = Combustion.NetHeatMJPerKgDry(Banksia);
            Assert.That(0.2 * 32.6 + 0.8 * Combustion.GasHeatMJPerKg(Banksia), Is.EqualTo(net).Within(1e-9), "a fifth to char at 32.6 MJ/kg, the rest to gas");
            // NIST's microscale calorimetry: Douglas fir's gas 12.8 +- 0.9 kJ/g, western red cedar's 13.9 (Leventon et al. 2025).
            Assert.That(Combustion.GasHeatMJPerKg(Banksia), Is.InRange(11.9, 14.8));
            // The heat a kilogram lost in flame gives, gas at the flame's efficiency: the cone calorimeter's effective heat of
            // combustion for wood, 0.057 q + 11.88 MJ/kg dry at 20 to 50 kW/m2 (Tran 1992), 13.0 to 14.7.
            Assert.That(Combustion.GasHeatMJPerKg(Banksia) * Combustion.FlameEfficiency, Is.InRange(0.057 * 20.0 + 11.88, 0.057 * 50.0 + 11.88));
        }

        [Test]
        public void ThinnerSticksBurnFasterAndDenserOnesLongerByTheCribLaw()
        {
            // McAllister and Finney 2015: R / A_s = C b^-0.5, C = 1.08e-3 g/(s cm^1.5), b in cm; 10.8 g/(m2 s) for a centimetre stick.
            foreach (double b in new[] { 0.004, 0.01, 0.0254, 0.05, 0.1 })
                Assert.That(Combustion.CribBurnKgM2s(b), Is.EqualTo(1.08e-3 * Math.Pow(b * 100.0, -0.5) * 10.0).Within(1e-12), "g/(s cm2) is ten kg/(s m2)");
            Assert.That(Combustion.CribBurnKgM2s(0.01) * 1000.0, Is.EqualTo(10.8).Within(1e-9));
            // The second source: Babrauskas's COMPF2 crib rate, v_p = 1.7e-6 D^-0.6 m/s, for woods of 450 kg/m3: within a fifth from
            // a twig to a four-inch log, though its exponent is -0.6 and theirs -0.5.
            foreach (double b in new[] { 0.004, 0.01, 0.0254, 0.05, 0.1 })
            {
                double compf2 = 1.7e-6 * Math.Pow(b, -0.6);
                Assert.That(Combustion.CribRegressionMs(b, 450.0) / compf2, Is.InRange(0.8, 1.2), "at " + b + " m");
            }
            // A third, for thick timber: Eurocode 5's one-dimensional charring rate in a furnace, 0.65 mm a minute for softwood and
            // 0.50 for hardwoods over 450 kg/m3 (EN 1995-1-2, table 3.1); a campfire's gentler heat burns a four-inch log no faster.
            double log = Combustion.CribRegressionMs(0.1016, 450.0) * 60000.0;
            Assert.That(log, Is.InRange(0.3, 0.65), "a four-inch log of 450 kg/m3, about half a millimetre a minute");
            Assert.That(Combustion.CribRegressionMs(0.05, Wood.Blackbutt.DensityDryKgM3), Is.LessThan(Combustion.CribRegressionMs(0.05, Banksia.DensityDryKgM3)),
                        "blackbutt, the denser, burns in slower: a long-burning fuel");
            // Water slows it as the charring rate is slowed: 8.3 per cent slower at 20 per cent than at 8 (Babrauskas 2005).
            Assert.That(Combustion.MoistureBurnFactor(0.08), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(Combustion.MoistureBurnFactor(0.20), Is.EqualTo(1.0 - 0.083).Within(0.002));
            Assert.That(Combustion.MoistureBurnFactor(0.6), Is.InRange(0.65, 0.8), "green wood at about 0.7 of an air-dry stick's pace");
            Assert.That(Combustion.MoistureBurnFactor(0.0), Is.InRange(1.0, 1.1));
        }

        [Test]
        public void WoodCatchesByItsHeatAndItsWaterAndNotBelowTheCriticalFlux()
        {
            // Below the critical flux nothing catches.
            Assert.That(Combustion.IgnitionSeconds(450.0, 0.05, 0.0, 10500.0), Is.EqualTo(double.PositiveInfinity));
            // A thick piece at 30 kW/m2: Babrauskas's 130 rho^0.73 / (q - 11)^1.82.
            double babrauskas = 130.0 * Math.Pow(450.0, 0.73) / Math.Pow(30.0 - 11.0, 1.82);
            Assert.That(Combustion.IgnitionSeconds(450.0, 0.05, 0.0, 30000.0), Is.EqualTo(babrauskas).Within(1e-9));
            // The second source: McAllister, Finney and Cohen's poplar (about 450 kg/m3), 12 mm, dry, at 30 kW/m2 caught at 28.0 s
            // (their table 1). The correlation is slower than their blackened, piloted samples; within a factor of two.
            Assert.That(babrauskas / 28.0, Is.InRange(1.0, 2.0));
            // Their samples at 18.5 per cent took 1.47 times as long as dry, on average over 20 to 50 kW/m2.
            double wet = Combustion.IgnitionSeconds(450.0, 0.05, 0.185, 30000.0) / Combustion.IgnitionSeconds(450.0, 0.05, 0.0, 30000.0);
            Assert.That(wet, Is.InRange(1.4, 1.5));
            // A twig heats through before a log's surface does: thin catches first.
            Assert.That(Combustion.IgnitionSeconds(550.0, 0.003, 0.1, 40000.0), Is.LessThan(Combustion.IgnitionSeconds(550.0, 0.05, 0.1, 40000.0)));
            // A fibre of tinder in a flame catches in about a second.
            Assert.That(Combustion.IgnitionSeconds(400.0, 0.0003, 0.08, 40000.0), Is.InRange(0.3, 2.0));
        }

        [Test]
        public void AFlameWantsGasEnoughAndWaterRaisesTheWant()
        {
            // McAllister, Finney and Cohen 2011, table 1, poplar at 1 m/s: dry 1.3 to 1.9 g/(m2 s) over 20 to 50 kW/m2; at 18.5 per
            // cent 2.0 to 3.0.
            Assert.That(Combustion.CriticalMassFluxKgM2s(0.0, 1.0) * 1000.0, Is.InRange(1.30, 1.88));
            Assert.That(Combustion.CriticalMassFluxKgM2s(0.185, 1.0) * 1000.0, Is.InRange(1.98, 2.98));
            Assert.That(Combustion.CriticalMassFluxKgM2s(0.6, 1.0), Is.GreaterThan(Combustion.CriticalMassFluxKgM2s(0.30, 1.0)), "their trend carried on to green wood");
            Assert.That(Combustion.CriticalMassFluxKgM2s(0.1, 9.0), Is.GreaterThan(Combustion.CriticalMassFluxKgM2s(0.1, 1.0)), "a strong wind strips a weak flame");
            Assert.That(Combustion.CriticalMassFluxKgM2s(0.1, 0.0), Is.EqualTo(Combustion.CriticalMassFluxKgM2s(0.1, 1.0)), "still air is no worse than their 1 m/s");
        }

        [Test]
        public void AFlameCrossesTinderAsRothermelAndAndersonMeasuredAndNotWhenDamp()
        {
            // INT-30: R = (1.04 - 0.044 M) e^(0.0038 U) ft/min, M per cent, U ft/min.
            const double FtMin = 0.3048 / 60.0;
            Assert.That(Combustion.BundleSpreadMs(0.0, 0.0), Is.EqualTo(1.04 * FtMin).Within(1e-12), "half a centimetre a second, dry and still");
            Assert.That(Combustion.BundleSpreadMs(0.10, 0.0), Is.EqualTo((1.04 - 0.44) * FtMin).Within(1e-12));
            Assert.That(Combustion.BundleSpreadMs(0.24, 0.0), Is.EqualTo(0.0), "none at 24 per cent");
            Assert.That(Combustion.BundleExtinctionMoisture, Is.EqualTo(0.2364).Within(0.0001));
            double breath = Combustion.BundleSpreadMs(0.10, 2.0) / Combustion.BundleSpreadMs(0.10, 0.0);
            Assert.That(breath, Is.EqualTo(Math.Exp(0.0038 * 2.0 * 196.8504)).Within(1e-9), "a breath of 2 m/s, their exponential");
            Assert.That(Combustion.BundleSpreadMs(0.10, 10.0), Is.EqualTo(Combustion.BundleSpreadMs(0.10, 3.6)), "held where their measurements end");
        }

        [Test]
        public void ALoneThickStickGoesOutAndALonePencilStickBurns()
        {
            Fire thumb = new Fire();
            FuelPiece stick = FuelPiece.Stick(Banksia, 0.025, 0.5, 0.12);
            Assert.That(thumb.Kindle(stick, FireAir.Still), Is.False, "a thumb-thick stick alone holds no flame");
            thumb.Advance(60.0, FireAir.Still);
            Assert.That(thumb.Phase, Is.EqualTo(FirePhase.Out));

            Fire pencil = new Fire();
            FuelPiece twig = FuelPiece.Stick(Banksia, 0.005, 0.3, 0.12);
            Assert.That(pencil.Kindle(twig, FireAir.Still), Is.True, "a pencil-thin stick keeps its own flame");
            pencil.Advance(60.0, FireAir.Still);
            Assert.That(pencil.Phase, Is.EqualTo(FirePhase.Flaming));
            pencil.Advance(600.0, FireAir.Still);
            Assert.That(twig.Phase, Is.Not.EqualTo(BurnPhase.Flaming), "and burns away in minutes");
            Assert.That(twig.WoodKg, Is.EqualTo(0.0));
        }

        [Test]
        public void TinderLightsKindlingAndKindlingLightsFuel()
        {
            Fire fire = Ladder(0.15, out FuelPiece[] logs, out FuelPiece tinder);
            Assert.That(fire.Kindle(tinder, FireAir.Still), Is.True);
            fire.Advance(600.0, FireAir.Still);
            Assert.That(fire.Phase, Is.EqualTo(FirePhase.Flaming));
            foreach (FuelPiece log in logs) Assert.That(log.Phase, Is.EqualTo(BurnPhase.Flaming), "the wrist-thick logs caught from the kindling's fire");
            Assert.That(fire.HeatReleaseW, Is.InRange(5000.0, 40000.0), "a campfire's kilowatts");
            // Starved, it goes to embers and then out.
            bool sawEmbers = false;
            for (int i = 0; i < 24 * 60 && fire.Phase != FirePhase.Out; i++)
            {
                fire.Advance(60.0, FireAir.Still);
                if (fire.Phase == FirePhase.Embers) sawEmbers = true;
            }
            Assert.That(sawEmbers, Is.True, "a bed of embers after the flames");
            Assert.That(fire.Phase, Is.EqualTo(FirePhase.Out));
            foreach (FuelPiece p in fire.Pieces) Assert.That(p.Phase, Is.EqualTo(BurnPhase.Spent), "burnt to ash");
        }

        [Test]
        public void EveryJouleIsTheWoodsHeatLessTheSmokeAndTheWater()
        {
            Fire fire = Ladder(0.15, out FuelPiece[] logs, out FuelPiece tinder);
            fire.Kindle(tinder, FireAir.Still);
            double released = 0.0;
            for (int i = 0; i < 24 * 60 && fire.Phase != FirePhase.Out; i++)
            {
                fire.Advance(60.0, FireAir.Still);
                released += (fire.FlameHeatW + fire.GlowHeatW - fire.WaterCostW) * 60.0;
            }
            // Burnt to ash, a piece whose gas all burnt in flame gives its dry wood's net heat, less the twentieth of that gas an
            // open flame leaves unburnt (an efficiency of 0.95, checked against Tran's effective heat), less its water; what
            // smouldered lost its gas as smoke, so the fire gives no more than that and, from a lay of dry wood burnt in flame,
            // nearly all of it.
            double ceiling = 0.0, given = 0.0;
            foreach (FuelPiece p in fire.Pieces)
            {
                double net = Combustion.NetHeatMJPerKgDry(p.Wood) * 1e6;
                double gas = 0.8 * Combustion.GasHeatMJPerKg(p.Wood) * 1e6;
                double u = p.IsBundle ? 0.08 : ReferenceEquals(p.Wood, Blackbutt) ? 0.15 : 0.12;
                ceiling += p.StartDryKg * (net - 0.05 * gas - 2.443e6 * u);
                given += p.HeatGivenJ;
            }
            Assert.That(released, Is.LessThanOrEqualTo(ceiling * (1.0 + 1e-9)));
            Assert.That(released, Is.GreaterThan(0.9 * ceiling), "dry wood burns mostly in flame");
            Assert.That(given, Is.EqualTo(released).Within(released * 1e-9), "each piece's account agrees with the fire's");
            Assert.That(released / 1e6, Is.InRange(30.0, 70.0), "about 3 kg of wood at 16 MJ/kg");
        }

        [Test]
        public void TinderAloneUnderLogsLightsNothing()
        {
            Fire fire = new Fire();
            FuelPiece tinder = Tinder(0.08);
            fire.Add(tinder);
            FuelPiece[] logs = { FuelPiece.Stick(Blackbutt, 0.05, 0.5, 0.15), FuelPiece.Stick(Blackbutt, 0.05, 0.5, 0.15) };
            foreach (FuelPiece log in logs) fire.Add(log);
            fire.Kindle(tinder, FireAir.Still);
            fire.Advance(600.0, FireAir.Still);
            Assert.That(fire.Phase, Is.EqualTo(FirePhase.Out));
            foreach (FuelPiece log in logs) Assert.That(log.Phase, Is.EqualTo(BurnPhase.Unlit), "a log wants kindling under it, not a handful of bark");
        }

        [Test]
        public void GreenWoodSmouldersWhereAirDryWoodFlamesAndTheWordsSayWhy()
        {
            Fire dry = Ladder(0.15, out FuelPiece[] dryLogs, out FuelPiece t1);
            dry.Kindle(t1, FireAir.Still);
            dry.Advance(600.0, FireAir.Still);
            Assert.That(dryLogs[0].Phase, Is.EqualTo(BurnPhase.Flaming));

            Fire green = Ladder(Blackbutt.GreenMoisture, out FuelPiece[] greenLogs, out FuelPiece t2);
            green.Kindle(t2, FireAir.Still);
            green.Advance(1200.0, FireAir.Still);
            Assert.That(greenLogs[0].Phase, Is.Not.EqualTo(BurnPhase.Flaming), "green blackbutt on a small fire does not flame");
            string why = green.WhyNot(greenLogs[0], FireAir.Still);
            Assert.That(why, Does.Contain("water").Or.Contain("fire"), why);
            Assert.That(dry.WhyNot(dryLogs[0], FireAir.Still), Does.Contain("burning"));
        }

        [Test]
        public void DampTinderWillNotFlameAndSaysWhy()
        {
            Fire fire = new Fire();
            FuelPiece damp = Tinder(0.27);
            Assert.That(fire.Kindle(damp, FireAir.Still), Is.False);
            fire.Advance(10.0, FireAir.Still);
            string why = fire.WhyNot(damp, FireAir.Still);
            Assert.That(why, Does.Contain("27 % water").And.Contain("24 %"), why);
            Assert.That(fire.Kindle(Tinder(0.10), FireAir.Still), Is.True, "dry tinder flames");
        }

        [Test]
        public void WindSpeedsTheFlamesAndBrightensTheEmbers()
        {
            // Two fires lit alike in still air and left ten minutes to take hold; then a breeze on one.
            Fire still = Ladder(0.15, out FuelPiece[] stillLogs, out FuelPiece t1);
            Fire windy = Ladder(0.15, out FuelPiece[] windyLogs, out FuelPiece t2);
            still.Kindle(t1, FireAir.Still);
            windy.Kindle(t2, FireAir.Still);
            still.Advance(600.0, FireAir.Still);
            windy.Advance(600.0, FireAir.Still);
            Assert.That(windy.WoodKg, Is.EqualTo(still.WoodKg));
            FireAir breeze = new FireAir(3.0);
            still.Advance(900.0, FireAir.Still);
            windy.Advance(900.0, breeze);
            Assert.That(windy.WoodKg, Is.LessThan(still.WoodKg), "the wind has eaten more of the wood");
            Assert.That(windyLogs[0].Phase, Is.EqualTo(BurnPhase.Flaming), "and a breeze does not put out a going fire");
            // A tinder bundle's flame crosses it at Rothermel and Anderson's rate and is gone: 10 cm at 0.69 ft/min in still air,
            // half a minute; in the breeze e^(0.0038 U), U in ft/min, faster, three seconds.
            double spread = (1.04 - 0.044 * 8.0) * 0.3048 / 60.0;
            Assert.That(TinderFlameSeconds(FireAir.Still), Is.EqualTo(0.10 / spread).Within(0.1));
            Assert.That(TinderFlameSeconds(breeze), Is.EqualTo(0.10 / spread / Math.Exp(0.0038 * 3.0 * 196.8504)).Within(0.1));
            Assert.That(Combustion.FlameWindFactor(3.0), Is.EqualTo(2.0).Within(1e-12), "the root of one plus three over the draught's 1 m/s");
            Assert.That(Combustion.CharGlowKgM2s(4.0) / Combustion.CharGlowKgM2s(0.0), Is.EqualTo(3.0).Within(1e-12), "a coal blown at 4 m/s burns three times as fast");
        }

        [Test]
        public void TheFireIsTheSameHoweverItsTimeIsCut()
        {
            Fire fine = Ladder(0.15, out _, out FuelPiece t1);
            Fire coarse = Ladder(0.15, out _, out FuelPiece t2);
            fine.Kindle(t1, FireAir.Still);
            coarse.Kindle(t2, FireAir.Still);
            // The server's step (0.05 s of world time a tick at 1x is 2.4 s; here 0.25 s) against a slow layer's minute.
            for (int i = 0; i < 4 * 1800; i++) fine.Advance(0.25, FireAir.Still);
            for (int i = 0; i < 30; i++) coarse.Advance(60.0, FireAir.Still);
            Assert.That(coarse.WoodKg, Is.EqualTo(fine.WoodKg).Within(0.02 * fine.WoodKg + 1e-6), "the same wood left after half an hour");
            Assert.That(coarse.CharKg, Is.EqualTo(fine.CharKg).Within(0.02 * fine.CharKg + 1e-6));
            Assert.That(coarse.Phase, Is.EqualTo(fine.Phase));
        }

        [Test]
        public void TheSameFireGivesTheSameFire()
        {
            Fire a = Ladder(0.15, out _, out FuelPiece t1);
            Fire b = Ladder(0.15, out _, out FuelPiece t2);
            a.Kindle(t1, new FireAir(1.5));
            b.Kindle(t2, new FireAir(1.5));
            for (int i = 0; i < 90; i++)
            {
                a.Advance(20.0, new FireAir(1.5));
                b.Advance(20.0, new FireAir(1.5));
            }
            for (int i = 0; i < a.Pieces.Count; i++)
            {
                Assert.That(b.Pieces[i].WoodKg, Is.EqualTo(a.Pieces[i].WoodKg));
                Assert.That(b.Pieces[i].CharKg, Is.EqualTo(a.Pieces[i].CharKg));
                Assert.That(b.Pieces[i].Phase, Is.EqualTo(a.Pieces[i].Phase));
            }
        }

        [Test]
        public void AStickOfTheWorldGoesOnTheFireAsItIs()
        {
            Definition stick = DefinitionCatalogue.StickOf(PlantSpecies.CoastBanksia);
            ThingState s = default;
            s.SetDiameter(0.02f);
            s.SetLength(0.6f);
            s.SetMoisture(0.22f);
            float dry = (float)(640.0 * Math.PI / 4.0 * 0.02 * 0.02 * 0.6);
            s.SetMass(dry * 1.22f);
            FuelPiece p = FuelPiece.OfStick(stick, s);
            Assert.That(p, Is.Not.Null);
            Assert.That(p.Wood, Is.SameAs(Banksia));
            Assert.That(p.StartDryKg, Is.EqualTo(dry).Within(1e-6), "the thing's mass is wet; the fire's is dry");
            Assert.That(p.Moisture, Is.EqualTo(0.22).Within(1e-6));
            Assert.That(p.StartThicknessM, Is.EqualTo(0.02).Within(1e-7));
            Assert.That(p.Class, Is.EqualTo(FuelClass.Kindling));
            Assert.That(FuelPiece.OfStick(null, s), Is.Null);
            Assert.That(FuelPiece.OfStick(DefinitionCatalogue.BarkOf(PlantSpecies.SwampPaperbark), s), Is.Null, "a strip of bark is not a stick; it is tinder once shredded (part three)");
        }

        [Test]
        public void TheFuelClassesAreTheFireDangerRatingsDiameters()
        {
            Assert.That(FuelClasses.Of(0.0003), Is.EqualTo(FuelClass.Tinder));
            Assert.That(FuelClasses.Of(0.006), Is.EqualTo(FuelClass.Kindling));
            Assert.That(FuelClasses.Of(0.0253), Is.EqualTo(FuelClass.Kindling), "the 10-hour class ends at one inch");
            Assert.That(FuelClasses.Of(0.0255), Is.EqualTo(FuelClass.Fuel));
            Assert.That(FuelClasses.FuelFromM, Is.EqualTo(0.0254));
        }
    }
}

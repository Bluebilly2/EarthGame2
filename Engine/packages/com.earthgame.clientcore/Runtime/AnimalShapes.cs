using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>The solid one piece of an animal is drawn as (M1.7b). Three are enough for a body made of rigid pieces.</summary>
    public enum AnimalPartShape
    {
        /// <summary>A rectangular block filling the part's size: a foot, an ear, a tail feather.</summary>
        Box,

        /// <summary>A rod along the part's own y, round-ended, of the part's whole length: a neck, a shank, a leg.</summary>
        Capsule,

        /// <summary>An ellipsoid inscribed in the part's size: a body, a haunch, a skull.</summary>
        Ellipsoid,
    }

    /// <summary>
    /// One rigid piece of an animal (M1.7b): what it is made of and how big it is, which never change, and where it sits
    /// and which way it points in the pose asked for.
    ///
    /// <para>The frame is the animal's own: <b>x</b> to its right, <b>y</b> up from the ground its feet stand on, <b>z</b>
    /// the way it faces. The client puts that frame where the server says the animal is and turns it by the animal's yaw,
    /// so nothing here knows a world position.</para>
    ///
    /// <para>The turn is stated as three angles taken in one order: roll about z, then pitch about x, then yaw about y,
    /// each turning clockwise seen from the origin along its positive axis. That is the order Unity's Euler angles are
    /// taken in, so the Unity layer hands them straight over; stated here so a second reader need not ask Unity.</para>
    /// </summary>
    public readonly struct AnimalPart
    {
        public readonly string Name;
        public readonly AnimalPartShape Shape;

        /// <summary>The whole extent along the part's own x, y and z before it is turned, m.</summary>
        public readonly float SizeX, SizeY, SizeZ;

        /// <summary>Where the middle of the part sits in the animal's frame, m.</summary>
        public readonly float X, Y, Z;

        public readonly float PitchDeg, YawDeg, RollDeg;
        public readonly Rgb Colour;

        public AnimalPart(string name, AnimalPartShape shape, float sizeX, float sizeY, float sizeZ,
                          float x, float y, float z, float pitchDeg, float yawDeg, float rollDeg, Rgb colour)
        {
            Name = name;
            Shape = shape;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            X = x;
            Y = y;
            Z = z;
            PitchDeg = pitchDeg;
            YawDeg = yawDeg;
            RollDeg = rollDeg;
            Colour = colour;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// What the two animals look like, made here in code (M1.7b, CANON ruling 29: "i dont want to outsource the animals
    /// looks, we will do them"). Each is a short list of rigid pieces — a box, a capsule or an ellipsoid — placed by a
    /// little skeleton of fixed bone lengths, so a piece is the same size in every pose and only its joints move. The one
    /// table of what a kangaroo and an oystercatcher are made of, as <see cref="StandForms"/> is the one table of what a
    /// tree is made of; the Unity layer turns these into meshes and knows none of the numbers.
    ///
    /// <para><b>Recognisable, not fancy</b> (William, 2026-09-15: "they dont have to be fancy, just recognisable as what
    /// they are"). What names a kangaroo at fifty metres is its silhouette: the deep haunches, the long foot flat on the
    /// ground, the thick tail out behind, the small arms held to the chest, the ears. What names a pied oystercatcher is
    /// black above, white below, a long straight orange-red bill and pink legs.</para>
    ///
    /// <para><b>The measurements.</b> An eastern grey kangaroo stands about 1.3 m hunched and reaches 1.6 to 1.8 m
    /// upright, with a tail about a metre long, and hops at about 7 m/s when it flees with strides of several metres and a
    /// cycle of roughly 0.45 s (the research of 2026-09-13 behind <see cref="AnimalFlightRules"/>). A pied oystercatcher
    /// is 45 to 50 cm from bill tip to tail tip with a span of 80 to 86 cm, and flies at 15 m/s. The bones below are
    /// chosen to land inside those bands and the tests hold them there.</para>
    ///
    /// <para><b>Only the flight is a function of time</b> in any strong sense: the hop's rise and the wing's beat. Grazing
    /// carries a slow lift of the head, so a feeding animal is not a statue. Resting does not move.</para>
    /// </summary>
    public static class AnimalShapes
    {
        // ------------------------------------------------------------------ the kangaroo's bones and colours

        /// <summary>Hip to shoulder, m, and the length of the ellipsoid drawn over it.</summary>
        public const double TorsoM = 0.62, TorsoDrawM = 0.70;
        public const double TorsoAcrossM = 0.34, TorsoThroughM = 0.42;
        public const double ChestM = 0.44, ChestAcrossM = 0.28, ChestThroughM = 0.24;
        public const double NeckM = 0.24, NeckAcrossM = 0.16;
        public const double HeadM = 0.28, HeadAcrossM = 0.13, HeadThroughM = 0.15;
        public const double MuzzleM = 0.12, MuzzleAcrossM = 0.075;
        public const double EarM = 0.15, EarAcrossM = 0.065, EarThroughM = 0.022;

        /// <summary>The thigh, m, and the haunch drawn over it: the muscle that names the animal.</summary>
        public const double ThighM = 0.40, HaunchDrawM = 0.46, HaunchAcrossM = 0.24, HaunchThroughM = 0.32;
        public const double ShankM = 0.46, ShankAcrossM = 0.10;

        /// <summary>The hind foot, m: 32 cm of it, which is why a kangaroo cannot walk without its tail.</summary>
        public const double FootM = 0.32, FootAcrossM = 0.10, FootThroughM = 0.075;
        public const double ArmM = 0.19, ArmAcrossM = 0.075, ForearmM = 0.19, ForearmAcrossM = 0.06;

        /// <summary>The two lengths of tail, m; together about the metre the species carries.</summary>
        public const double TailBaseM = 0.55, TailBaseAcrossM = 0.16, TailTipM = 0.50, TailTipAcrossM = 0.075;

        public const double HipHalfM = 0.145, ShoulderHalfM = 0.115, EarHalfM = 0.055;

        /// <summary>How far the body rises between two touchdowns of a hop, m.</summary>
        public const double HopLiftM = 0.32;

        /// <summary>
        /// How far a hop carries, m. The research of 2026-09-13 puts a fleeing kangaroo at about 7 m/s with a cycle of
        /// roughly 0.45 s, and this is the product: the stride is the number kept, and the cycle is worked out from it and
        /// the speed (<see cref="HopSeconds"/>), so a kangaroo hopping slower takes its hops slower and not shorter.
        /// </summary>
        public const double HopStrideM = 3.15;

        /// <summary>How long the head takes to lift and fall once while grazing, s: not a measurement, a sign of life.</summary>
        public const double GrazeBobSeconds = 3.2;

        private static readonly Rgb Coat = new Rgb(0.46f, 0.42f, 0.36f);
        private static readonly Rgb Chest = new Rgb(0.74f, 0.70f, 0.62f);
        private static readonly Rgb Dark = new Rgb(0.28f, 0.25f, 0.22f);

        // ------------------------------------------------------------------ the oystercatcher's bones and colours

        public const double BirdBodyM = 0.20, BirdBodyAcrossM = 0.13, BirdBodyThroughM = 0.105;
        public const double BirdBellyM = 0.17, BirdBellyAcrossM = 0.115, BirdBellyThroughM = 0.075;
        public const double BirdNeckM = 0.075, BirdNeckAcrossM = 0.055;
        public const double BirdHeadM = 0.075, BirdHeadAcrossM = 0.058, BirdHeadThroughM = 0.058;

        /// <summary>The bill, m: long, straight and blunt, and the one part of the bird nobody mistakes.</summary>
        public const double BirdBillM = 0.085, BirdBillAcrossM = 0.0135;
        public const double BirdTailM = 0.10, BirdTailAcrossM = 0.085, BirdTailThroughM = 0.014;

        /// <summary>The wing in two: shoulder to wrist, and the primaries beyond it, m. Spread they give the published span.</summary>
        public const double BirdWingM = 0.17, BirdWingChordM = 0.105, BirdPinionM = 0.20, BirdPinionChordM = 0.072, BirdWingThickM = 0.012;
        public const double BirdLegM = 0.16, BirdLegAcrossM = 0.013, BirdFootM = 0.05, BirdFootAcrossM = 0.035, BirdFootThroughM = 0.008;
        public const double BirdHalfM = 0.045, BirdWingHalfM = 0.052;

        /// <summary>How high off the ground a startled pair is drawn flying, m: inside the metre or two of a bird that has just gone up.</summary>
        public const double BirdFlightUpM = 1.35;

        /// <summary>How long one wingbeat takes, s: five and a half a second, the order a shorebird's wing beats at.</summary>
        public const double BirdWingbeatSeconds = 0.18;

        /// <summary>How far the wing swings either side of level, degrees.</summary>
        public const double BirdFlapDeg = 38.0;

        private static readonly Rgb Black = new Rgb(0.07f, 0.07f, 0.08f);
        private static readonly Rgb White = new Rgb(0.93f, 0.93f, 0.91f);
        private static readonly Rgb Bill = new Rgb(0.90f, 0.32f, 0.09f);
        private static readonly Rgb Pink = new Rgb(0.91f, 0.62f, 0.64f);

        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>Whether this build makes shapes for a kind. The fairy-wren is heard and not seen (M1.7a), and has none.</summary>
        public static bool Draws(AnimalSpecies species) =>
            species == AnimalSpecies.EasternGreyKangaroo || species == AnimalSpecies.PiedOystercatcher;

        /// <summary>How many pieces a kind is made of; nought for a kind this build draws no shapes for.</summary>
        public static int PartCount(AnimalSpecies species)
        {
            if (species == AnimalSpecies.EasternGreyKangaroo) return 19;
            if (species == AnimalSpecies.PiedOystercatcher) return 14;
            return 0;
        }

        /// <summary>
        /// How long one hop takes at a speed, s: the stride divided by the speed, so the cadence follows the pace. At the
        /// 7 m/s a startled mob runs (<see cref="AnimalFlightRules.KangarooRunMs"/>) it is the measured 0.45 s.
        /// </summary>
        public static double HopSeconds(double speedMs) => speedMs > 0.1 ? HopStrideM / speedMs : HopStrideM / 0.1;

        /// <summary>
        /// The pieces a kind is made of, with the shape, size and colour each keeps in every pose, taken from its resting
        /// pose: what the Unity layer builds its meshes from once.
        /// </summary>
        public static void PartsOf(AnimalSpecies species, List<AnimalPart> into) => Build(species, AnimalPose.Resting, 0.0, into);

        /// <summary>
        /// Every piece of one animal in one pose at one instant, in a fixed order. A pose this build does not know — nought,
        /// or one a later server gives a new meaning — is drawn resting, because a client that refuses to draw an animal
        /// shows nothing and says nothing.
        /// </summary>
        /// <param name="timeS">
        /// The client's own clock, s. The fleeing pose turns it into the phase of a hop or a wingbeat, and the grazing pose
        /// into the slow lift of a head; the resting pose ignores it. An animal is given a phase of its own by its id, so a
        /// mob does not hop as one machine.
        /// </param>
        public static void Build(AnimalSpecies species, byte pose, double timeS, List<AnimalPart> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (species == null) throw new ArgumentNullException(nameof(species));
            into.Clear();
            if (species == AnimalSpecies.EasternGreyKangaroo) Kangaroo(pose, timeS, into);
            else if (species == AnimalSpecies.PiedOystercatcher) Oystercatcher(pose, timeS, into);
            else throw new ArgumentException("no shapes are made here for the " + species.DisplayName, nameof(species));
        }

        // ------------------------------------------------------------------ the eastern grey kangaroo

        private static void Kangaroo(byte pose, double timeS, List<AnimalPart> into)
        {
            double hipY, hipZ, spine, neck, head, thigh, shank, foot, arm, forearm, tailBase, tailTip, ear;
            if (pose == AnimalPose.Fleeing)
            {
                // The hop: the body rises and falls once a stride, the legs swing forward to meet the ground and tuck up
                // behind, and the tail comes up as a counterweight at the top of the bound.
                double phase = Fraction(timeS / HopSeconds(AnimalFlightRules.KangarooRunMs));
                double lift = HopLiftM * Math.Sin(Math.PI * phase);
                // Nought at the touchdowns, one at the top of the hop.
                double up = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * phase);
                hipY = 0.74 + lift;
                hipZ = -0.30;
                spine = 12.0;
                neck = 40.0;
                head = 18.0;
                thigh = Mix(-56.0, -20.0, up);
                shank = Mix(-120.0, -150.0, up);
                foot = Mix(0.0, -120.0, up);
                arm = -80.0;
                forearm = -10.0;
                tailBase = Mix(196.0, 170.0, up);
                tailTip = Mix(190.0, 160.0, up);
                ear = 118.0;
            }
            else if (pose == AnimalPose.Grazing)
            {
                // Feeding: the back sloping down to a head near the sward, on all four feet with the tail out behind.
                double bob = Math.Sin(2.0 * Math.PI * timeS / GrazeBobSeconds);
                hipY = 0.80;
                hipZ = -0.30;
                spine = -17.0;
                neck = -52.0 + 6.0 * bob;
                head = -70.0 + 9.0 * bob;
                thigh = -64.0;
                shank = -122.0;
                foot = 0.0;
                arm = -75.0;
                forearm = -35.0;
                tailBase = 195.0;
                tailTip = 210.0;
                ear = 120.0;
            }
            else
            {
                // Lying up, which is what a mob does at noon: down on the belly, the long feet flat alongside, the tail
                // out along the ground, the head up and watching.
                hipY = 0.30;
                hipZ = -0.30;
                spine = 10.0;
                neck = 48.0;
                head = 8.0;
                thigh = -8.0;
                shank = 208.0;
                foot = 0.0;
                arm = -60.0;
                forearm = -20.0;
                tailBase = 200.0;
                tailTip = 184.0;
                ear = 112.0;
            }

            Double3 hip = new Double3(0.0, hipY, hipZ);
            Double3 shoulder = hip + Dir(spine) * TorsoM;
            Double3 headBase = shoulder + Dir(neck) * NeckM;
            Double3 nose = headBase + Dir(head) * HeadM;
            Double3 tailMid = hip + Dir(tailBase) * TailBaseM;
            Double3 tailEnd = tailMid + Dir(tailTip) * TailTipM;
            Double3 down = new Double3(0.0, -0.10, 0.0);

            into.Add(Along("torso", AnimalPartShape.Ellipsoid, hip, shoulder, TorsoDrawM, TorsoAcrossM, TorsoThroughM, Coat));
            into.Add(Along("chest", AnimalPartShape.Ellipsoid, Between(hip, shoulder, 0.30) + down, shoulder + down * 0.3,
                           ChestM, ChestAcrossM, ChestThroughM, Chest));
            into.Add(Along("neck", AnimalPartShape.Capsule, shoulder, headBase, NeckM, NeckAcrossM, NeckAcrossM, Coat));
            into.Add(Along("head", AnimalPartShape.Ellipsoid, headBase, nose, HeadM, HeadAcrossM, HeadThroughM, Coat));
            into.Add(Along("muzzle", AnimalPartShape.Ellipsoid, Between(headBase, nose, 0.65), nose, MuzzleM, MuzzleAcrossM, MuzzleAcrossM, Dark));

            for (int s = 0; s < 2; s++)
            {
                double side = s == 0 ? -1.0 : 1.0;
                string hand = s == 0 ? ".left" : ".right";
                Double3 across = new Double3(side, 0.0, 0.0);

                // The ears stand off the back of the skull, up and a little behind.
                Double3 earRoot = Between(headBase, nose, 0.18) + across * EarHalfM + new Double3(0.0, 0.035, 0.0);
                into.Add(Along("ear" + hand, AnimalPartShape.Box, earRoot, earRoot + Dir(ear) * EarM, EarM, EarAcrossM, EarThroughM, Dark));

                Double3 hipSide = hip + across * HipHalfM;
                Double3 knee = hipSide + Dir(thigh) * ThighM;
                Double3 heel = knee + Dir(shank) * ShankM;
                Double3 toe = heel + Dir(foot) * FootM;
                into.Add(Along("haunch" + hand, AnimalPartShape.Ellipsoid, hipSide, knee, HaunchDrawM, HaunchAcrossM, HaunchThroughM, Coat));
                into.Add(Along("shank" + hand, AnimalPartShape.Capsule, knee, heel, ShankM, ShankAcrossM, ShankAcrossM, Coat));
                into.Add(Along("foot" + hand, AnimalPartShape.Box, heel, toe, FootM, FootAcrossM, FootThroughM, Dark));

                Double3 shoulderSide = shoulder + across * ShoulderHalfM + new Double3(0.0, -0.02, -0.02);
                Double3 elbow = shoulderSide + Dir(arm) * ArmM;
                Double3 paw = elbow + Dir(forearm) * ForearmM;
                into.Add(Along("arm" + hand, AnimalPartShape.Capsule, shoulderSide, elbow, ArmM, ArmAcrossM, ArmAcrossM, Coat));
                into.Add(Along("forearm" + hand, AnimalPartShape.Capsule, elbow, paw, ForearmM, ForearmAcrossM, ForearmAcrossM, Dark));
            }

            into.Add(Along("tail.base", AnimalPartShape.Capsule, hip, tailMid, TailBaseM, TailBaseAcrossM, TailBaseAcrossM, Coat));
            into.Add(Along("tail.tip", AnimalPartShape.Capsule, tailMid, tailEnd, TailTipM, TailTipAcrossM, TailTipAcrossM, Dark));
        }

        // ------------------------------------------------------------------ the pied oystercatcher

        private static void Oystercatcher(byte pose, double timeS, List<AnimalPart> into)
        {
            double rumpY, rumpZ, body, neck, head, bill, tail, leg, foot, wing, pinion, flapDeg, sweepDeg;
            bool flying = pose == AnimalPose.Fleeing;
            if (flying)
            {
                // Gone up: the body a metre or so off the sand, the neck out, the legs trailing and the wings beating.
                double phase = Fraction(timeS / BirdWingbeatSeconds);
                flapDeg = BirdFlapDeg * Math.Sin(2.0 * Math.PI * phase);
                // The body rides a little on each beat, highest as the wings come down.
                rumpY = BirdFlightUpM + 0.05 * Math.Sin(2.0 * Math.PI * phase - 0.5 * Math.PI);
                rumpZ = -0.10;
                body = 3.0;
                neck = 20.0;
                head = 5.0;
                bill = -3.0;
                tail = 183.0;
                leg = 200.0;
                foot = 195.0;
                wing = 0.0;
                pinion = 0.0;
                sweepDeg = 14.0;
            }
            else if (pose == AnimalPose.Grazing)
            {
                // Working the tideline: head down and the bill into the wet sand, with the same slow lift the mob has.
                double bob = Math.Sin(2.0 * Math.PI * timeS / GrazeBobSeconds);
                rumpY = 0.205;
                rumpZ = -0.10;
                body = 10.0;
                neck = 6.0 + 14.0 * bob;
                head = -38.0 + 16.0 * bob;
                bill = -54.0 + 14.0 * bob;
                tail = 196.0;
                leg = -96.0;
                foot = 0.0;
                wing = -150.0;
                pinion = 155.0;
                flapDeg = 0.0;
                sweepDeg = 0.0;
            }
            else
            {
                // Standing on the tideline, head up.
                rumpY = 0.215;
                rumpZ = -0.10;
                body = 6.0;
                neck = 55.0;
                head = 18.0;
                bill = -8.0;
                tail = 190.0;
                leg = -95.0;
                foot = 0.0;
                wing = -150.0;
                pinion = 155.0;
                flapDeg = 0.0;
                sweepDeg = 0.0;
            }

            Double3 rump = new Double3(0.0, rumpY, rumpZ);
            Double3 breast = rump + Dir(body) * BirdBodyM;
            Double3 neckTop = breast + Dir(neck) * BirdNeckM;
            Double3 headFront = neckTop + Dir(head) * BirdHeadM;
            Double3 billTip = headFront + Dir(bill) * BirdBillM;
            Double3 tailEnd = rump + Dir(tail) * BirdTailM;
            Double3 under = new Double3(0.0, -0.035, 0.0);

            into.Add(Along("back", AnimalPartShape.Ellipsoid, rump, breast, BirdBodyM, BirdBodyAcrossM, BirdBodyThroughM, Black));
            into.Add(Along("belly", AnimalPartShape.Ellipsoid, rump + under, breast + under, BirdBellyM, BirdBellyAcrossM, BirdBellyThroughM, White));
            into.Add(Along("neck", AnimalPartShape.Capsule, breast, neckTop, BirdNeckM, BirdNeckAcrossM, BirdNeckAcrossM, Black));
            into.Add(Along("head", AnimalPartShape.Ellipsoid, neckTop, headFront, BirdHeadM, BirdHeadAcrossM, BirdHeadThroughM, Black));
            into.Add(Along("bill", AnimalPartShape.Capsule, headFront, billTip, BirdBillM, BirdBillAcrossM, BirdBillAcrossM, Bill));
            into.Add(Along("tail", AnimalPartShape.Box, rump, tailEnd, BirdTailM, BirdTailAcrossM, BirdTailThroughM, Black));

            for (int s = 0; s < 2; s++)
            {
                double side = s == 0 ? -1.0 : 1.0;
                string hand = s == 0 ? ".left" : ".right";
                Double3 across = new Double3(side, 0.0, 0.0);

                Double3 wingRoot = Between(rump, breast, 0.72) + across * BirdWingHalfM + new Double3(0.0, 0.03, 0.0);
                Double3 wrist, tip;
                if (flying)
                {
                    wrist = wingRoot + Out(side, flapDeg, sweepDeg) * BirdWingM;
                    tip = wrist + Out(side, flapDeg * 1.35, sweepDeg + 8.0) * BirdPinionM;
                }
                else
                {
                    // Folded: down and back to the wrist, then back and up again, so the wing lies along the flank with
                    // its tip about at the tail's, as a wader's does.
                    wrist = wingRoot + Dir(wing) * BirdWingM;
                    tip = wrist + Dir(pinion) * BirdPinionM;
                }
                into.Add(Along("wing" + hand, AnimalPartShape.Box, wingRoot, wrist, BirdWingM, BirdWingChordM, BirdWingThickM, Black));
                into.Add(Along("pinion" + hand, AnimalPartShape.Box, wrist, tip, BirdPinionM, BirdPinionChordM, BirdWingThickM, Black));

                Double3 hip = Between(rump, breast, 0.45) + across * BirdHalfM + new Double3(0.0, -0.04, 0.0);
                Double3 ankle = hip + Dir(leg) * BirdLegM;
                Double3 toes = ankle + Dir(foot) * BirdFootM;
                into.Add(Along("leg" + hand, AnimalPartShape.Capsule, hip, ankle, BirdLegM, BirdLegAcrossM, BirdLegAcrossM, Pink));
                into.Add(Along("foot" + hand, AnimalPartShape.Box, ankle, toes, BirdFootM, BirdFootAcrossM, BirdFootThroughM, Pink));
            }
        }

        // ------------------------------------------------------------------ the arithmetic

        /// <summary>
        /// A direction in the animal's own vertical plane: an angle from straight ahead, degrees, positive upwards, so
        /// 90° is straight up, 180° straight back and −90° straight down. Every bone but a spread wing lies in that plane,
        /// which is what makes an animal's two sides mirror each other exactly.
        /// </summary>
        private static Double3 Dir(double angleDeg)
        {
            double a = angleDeg * GeoMath.DegToRad;
            return new Double3(0.0, Math.Sin(a), Math.Cos(a));
        }

        /// <summary>
        /// A direction out to one side: <paramref name="side"/> −1 left or 1 right, raised by <paramref name="elevationDeg"/>
        /// and swept back by <paramref name="sweepDeg"/>. The spread wing's, and the one direction with an x of its own.
        /// </summary>
        private static Double3 Out(double side, double elevationDeg, double sweepDeg)
        {
            double e = elevationDeg * GeoMath.DegToRad, w = sweepDeg * GeoMath.DegToRad;
            double flat = Math.Cos(e);
            return new Double3(side * flat * Math.Cos(w), Math.Sin(e), -flat * Math.Sin(w));
        }

        private static Double3 Between(Double3 a, Double3 b, double t) => a + (b - a) * t;

        private static double Mix(double a, double b, double t) => a + (b - a) * t;

        /// <summary>The part of a number after the point, never negative: a phase that keeps running for a clock that does.</summary>
        private static double Fraction(double v)
        {
            double f = v - Math.Floor(v);
            return f < 0.0 ? f + 1.0 : f;
        }

        /// <summary>
        /// A piece laid along a bone: its middle at the bone's middle, its own y turned onto the bone, and its length
        /// stated rather than measured, so a piece is the same size in every pose however the joints move.
        /// </summary>
        private static AnimalPart Along(string name, AnimalPartShape shape, Double3 from, Double3 to,
                                        double lengthM, double acrossM, double throughM, Rgb colour)
        {
            Double3 middle = Between(from, to, 0.5);
            Double3 d = (to - from).Normalized;
            double pitch = Math.Acos(Math.Min(1.0, Math.Max(-1.0, d.Y))) * RadToDeg;
            double flat = Math.Sqrt(d.X * d.X + d.Z * d.Z);
            double yaw = flat > 1e-9 ? Math.Atan2(d.X, d.Z) * RadToDeg : 0.0;
            return new AnimalPart(name, shape, (float)acrossM, (float)lengthM, (float)throughM,
                                  (float)middle.X, (float)middle.Y, (float)middle.Z,
                                  (float)pitch, (float)yaw, 0f, colour);
        }
    }
}

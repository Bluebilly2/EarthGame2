using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// One bone of an animal's skeleton in one pose (M1.7b): the joint it turns about, which way it points, and the frame
    /// the skin hung on it turns with.
    ///
    /// <para>The frame is the animal's own: <b>x</b> to its right, <b>y</b> up from the ground its feet stand on, <b>z</b>
    /// the way it faces. The client puts that frame where the server says the animal is and turns it by the animal's yaw,
    /// so nothing here knows a world position.</para>
    ///
    /// <para><see cref="Along"/> is the bone's direction from its joint; <see cref="Across"/> lies to the animal's right for
    /// every bone but a wing, whose across is its chord — up the flank when the wing is folded, fore and aft when it is
    /// spread; <see cref="Deep"/> is Along × Across. The three are a proper frame, so the Unity layer turns them into one
    /// rotation with Along forward and Deep up, and the skin's points ride the bone by their coordinates in it
    /// (2026-09-16, when the rigid pieces became one skin).</para>
    /// </summary>
    public readonly struct AnimalBone
    {
        public readonly string Name;

        /// <summary>The joint the bone turns about, m, in the animal's frame.</summary>
        public readonly Double3 Joint;

        public readonly Double3 Along, Across, Deep;
        public readonly double LengthM;

        public AnimalBone(string name, Double3 joint, Double3 along, Double3 across, Double3 deep, double lengthM)
        {
            Name = name;
            Joint = joint;
            Along = along;
            Across = across;
            Deep = deep;
            LengthM = lengthM;
        }

        /// <summary>The bone's far end, m.</summary>
        public Double3 End => Joint + Along * LengthM;

        public override string ToString() => Name;
    }

    /// <summary>
    /// One ring of a shell (M1.7b, 2026-09-16): where its middle is, the frame it is drawn in, its two half-widths, the
    /// stretch a mitred bend gives it, the bone it rides and the one it is shared with, and the colours its faces wear.
    /// </summary>
    public readonly struct AnimalStation
    {
        public readonly Double3 Centre;

        /// <summary>The ring's own frame: the shell's direction of travel, and the two axes its half-widths lie along.</summary>
        public readonly Double3 Along, Across, Deep;

        /// <summary>Half-widths, m, along <see cref="Across"/> and <see cref="Deep"/>.</summary>
        public readonly double AcrossM, DeepM;

        /// <summary>
        /// How far the ring is stretched along <see cref="StretchDir"/>, one for a ring on a straight run: a ring at a bend
        /// lies in the plane between the two runs and is stretched by the secant of half the turn, so the tube keeps its
        /// width round the corner, as a mitred pipe does.
        /// </summary>
        public readonly double Stretch;

        public readonly Double3 StretchDir;

        /// <summary>The bone the ring rides, the bone it is shared with at a bend (−1 for none), and the first bone's share.</summary>
        public readonly int Bone, Blend;

        public readonly double Share;

        /// <summary>Where the ring's points begin in the body's point list; the shell's <c>Sides</c> follow in order round the ring.</summary>
        public readonly int FirstPoint;

        /// <summary>One of the rings that round an end off: its half-widths shrink toward the pole by the ellipse, not the profile.</summary>
        public readonly bool Cap;

        /// <summary>The colour worn above the belly line and below it, and the line: the sine of the angle round the ring below which the lower colour is worn.</summary>
        public readonly Rgb Above, Below;

        public readonly double BellyLine;

        public AnimalStation(Double3 centre, Double3 along, Double3 across, Double3 deep, double acrossM, double deepM,
                             double stretch, Double3 stretchDir, int bone, int blend, double share, int firstPoint, bool cap,
                             Rgb above, Rgb below, double bellyLine)
        {
            Centre = centre;
            Along = along;
            Across = across;
            Deep = deep;
            AcrossM = acrossM;
            DeepM = deepM;
            Stretch = stretch;
            StretchDir = stretchDir;
            Bone = bone;
            Blend = blend;
            Share = share;
            FirstPoint = firstPoint;
            Cap = cap;
            Above = above;
            Below = below;
            BellyLine = bellyLine;
        }
    }

    /// <summary>One face of an animal's skin: three of the body's points, wound so the face looks outward, and its colour.</summary>
    public readonly struct AnimalFace
    {
        public readonly int A, B, C;
        public readonly int Shell;
        public readonly Rgb Colour;

        public AnimalFace(int a, int b, int c, int shell, Rgb colour)
        {
            A = a;
            B = b;
            C = c;
            Shell = shell;
            Colour = colour;
        }
    }

    /// <summary>
    /// One closed surface of an animal's skin (M1.7b, 2026-09-16): a tube of rings run along a line of waypoints, rounded
    /// off at both ends by a pole and the cap rings before it. The whole body from the tail's tip to the nose is one; a
    /// limb is one or more, each rooted in its parent.
    /// </summary>
    public sealed class AnimalShell
    {
        public string Name { get; }

        /// <summary>How many points each ring has.</summary>
        public int Sides { get; }

        /// <summary>The shell this one's root is buried in, or null for the body itself.</summary>
        public string Parent { get; }

        /// <summary>
        /// Whether the root is a ball joint: the root cap nests inside the parent's end cap about the same centre, so a fold
        /// as sharp as a kangaroo's knee reads as one rounded knuckle in every pose. Otherwise the root cap is buried in the
        /// parent's flank.
        /// </summary>
        public bool BallJoint { get; }

        /// <summary>Every ring in order from the start pole's end to the end pole's, the cap rings among them.</summary>
        public IReadOnlyList<AnimalStation> Stations { get; }

        /// <summary>The two points that close the ends, in the body's point list.</summary>
        public int StartPole { get; }

        public int EndPole { get; }

        /// <summary>Where the shell's faces lie in the body's face list, and how many there are.</summary>
        public int FirstFace { get; }

        public int FaceCount { get; }

        /// <summary>How far each pole stands beyond its last profile ring, m.</summary>
        public double StartCapM { get; }

        public double EndCapM { get; }

        public AnimalShell(string name, int sides, string parent, bool ballJoint, IReadOnlyList<AnimalStation> stations,
                           int startPole, int endPole, int firstFace, int faceCount, double startCapM, double endCapM)
        {
            Name = name;
            Sides = sides;
            Parent = parent;
            BallJoint = ballJoint;
            Stations = stations;
            StartPole = startPole;
            EndPole = endPole;
            FirstFace = firstFace;
            FaceCount = faceCount;
            StartCapM = startCapM;
            EndCapM = endCapM;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The skin of one kind of animal, grown once in its resting pose (M1.7b, 2026-09-16): the shells and their rings, the
    /// points they share, the faces over them, which bone each point rides, and the corners the graphics card is handed.
    /// A pose moves the bones and the skin follows them, so one body serves every pose and every frame.
    /// </summary>
    public sealed class AnimalBody
    {
        public AnimalSpecies Species { get; }

        /// <summary>The skeleton the skin was hung on: the resting pose at nought seconds, in bone order.</summary>
        public IReadOnlyList<AnimalBone> Bind { get; }

        public IReadOnlyList<AnimalShell> Shells { get; }
        public IReadOnlyList<Double3> Points { get; }
        public IReadOnlyList<AnimalFace> Faces { get; }

        /// <summary>For each point: the bone it rides, the bone it is shared with (−1 for none), and the first's share.</summary>
        public int[] PointBone { get; }

        public int[] PointBlend { get; }
        public double[] PointShare { get; }

        /// <summary>
        /// The corners the graphics card is handed, three a face with the leading corner first. A corner stands at one of
        /// the points and carries the plane and colour of the face it leads; a face's other two corners are borrowed from
        /// whatever stands at those points, and their plane and colour are never read (the stand's shader takes both from
        /// the leading corner alone, StandMeshes, 2026-09-11), so the skin has about as many corners as faces.
        /// </summary>
        public int[] Triangles { get; }

        public int[] VertexPoint { get; }

        /// <summary>The face each corner leads, or −1 for one that only borrows.</summary>
        public int[] VertexFace { get; }

        public Double3[] VertexNormal { get; }
        public Rgb[] VertexColour { get; }

        public int VertexCount => VertexPoint.Length;

        public AnimalBody(AnimalSpecies species, IReadOnlyList<AnimalBone> bind, IReadOnlyList<AnimalShell> shells,
                          IReadOnlyList<Double3> points, IReadOnlyList<AnimalFace> faces, int[] pointBone, int[] pointBlend,
                          double[] pointShare, int[] triangles, int[] vertexPoint, int[] vertexFace, Double3[] vertexNormal,
                          Rgb[] vertexColour)
        {
            Species = species;
            Bind = bind;
            Shells = shells;
            Points = points;
            Faces = faces;
            PointBone = pointBone;
            PointBlend = pointBlend;
            PointShare = pointShare;
            Triangles = triangles;
            VertexPoint = vertexPoint;
            VertexFace = vertexFace;
            VertexNormal = vertexNormal;
            VertexColour = vertexColour;
        }

        /// <summary>The outward plane of one face, from its three points as they stand in the resting pose.</summary>
        public Double3 Normal(int face)
        {
            AnimalFace f = Faces[face];
            return Double3.Cross(Points[f.B] - Points[f.A], Points[f.C] - Points[f.A]).Normalized;
        }
    }

    /// <summary>
    /// What the two animals look like, made here in code (M1.7b, CANON ruling 29: "i dont want to outsource the animals
    /// looks, we will do them"). Each is a skeleton of fixed bone lengths whose joints a pose moves, and a skin grown once
    /// over that skeleton in its resting pose: a few closed shells, each a tube of rings run along the bones with a
    /// radius that changes smoothly, mitred where it bends a little and rounded off at its ends, its limbs rooted inside
    /// its body and its sharpest joints made as one ball inside another. The one table of what a kangaroo and an
    /// oystercatcher are made of, as <see cref="StandForms"/> is the one table of what a tree is made of; the Unity layer
    /// hangs the skin on the bones and knows none of the numbers.
    ///
    /// <para><b>Recognisable, not fancy</b> (William, 2026-09-15: "they dont have to be fancy, just recognisable as what
    /// they are"). What names a kangaroo at fifty metres is its silhouette: the deep haunches, the long foot flat on the
    /// ground, the thick tail out behind, the small arms held to the chest, the ears. What names a pied oystercatcher is
    /// black above, white below, a long straight orange-red bill and pink legs. On 2026-09-16 William saw the first
    /// frames, made of rigid boxes, capsules and ellipsoids, and said they looked like "a load of 3d shapes put together"
    /// and asked for them "more smooth and connected"; the skin of shells replaced the pieces that day.</para>
    ///
    /// <para><b>The measurements.</b> An eastern grey kangaroo stands about 1.3 m hunched and reaches 1.6 to 1.8 m
    /// upright, with a tail about a metre long, and hops at about 7 m/s when it flees with strides of several metres and a
    /// cycle of roughly 0.45 s (the research of 2026-09-13 behind <see cref="AnimalFlightRules"/>). A pied oystercatcher
    /// is 45 to 50 cm from bill tip to tail tip with a span of 80 to 86 cm, and flies at 15 m/s. The bones below are
    /// chosen to land inside those bands and the tests hold the skin there.</para>
    ///
    /// <para><b>Only the flight is a function of time</b> in any strong sense: the hop's rise and the wing's beat. Grazing
    /// carries a slow lift of the head, so a feeding animal is not a statue. Resting does not move.</para>
    /// </summary>
    public static class AnimalShapes
    {
        // ------------------------------------------------------------------ the kangaroo's bones

        /// <summary>Hip to shoulder, m.</summary>
        public const double TorsoM = 0.62;

        public const double NeckM = 0.24, HeadM = 0.28, EarM = 0.15;

        /// <summary>The thigh, m, which the haunch is grown over: the muscle that names the animal.</summary>
        public const double ThighM = 0.40;

        public const double ShankM = 0.46;

        /// <summary>The hind foot, m: 32 cm of it, which is why a kangaroo cannot walk without its tail.</summary>
        public const double FootM = 0.32;

        public const double ArmM = 0.19, ForearmM = 0.19;

        /// <summary>The two lengths of tail, m; together about the metre the species carries.</summary>
        public const double TailBaseM = 0.55, TailTipM = 0.50;

        /// <summary>How far to one side of the middle line the hips, the shoulders and the ears are, m.</summary>
        public const double HipHalfM = 0.115, ShoulderHalfM = 0.085, EarHalfM = 0.05;

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

        // ------------------------------------------------------------------ the oystercatcher's bones

        public const double BirdBodyM = 0.20, BirdNeckM = 0.075, BirdHeadM = 0.075;

        /// <summary>The bill, m: long, straight and blunt, and the one part of the bird nobody mistakes.</summary>
        public const double BirdBillM = 0.085;

        public const double BirdTailM = 0.10;

        /// <summary>The wing in two: shoulder to wrist, and the primaries beyond it, m. Spread they give the published span.</summary>
        public const double BirdWingM = 0.17, BirdPinionM = 0.19;

        public const double BirdLegM = 0.16, BirdFootM = 0.05;

        /// <summary>How far to one side of the middle line the legs and the wings are rooted, m: inside the body, so the roots never show.</summary>
        public const double BirdHalfM = 0.035, BirdWingHalfM = 0.06;

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

        // ------------------------------------------------------------------ the skin's own numbers

        /// <summary>How many rings round each end off between the last profile ring and the pole.</summary>
        public const int CapRings = 2;

        /// <summary>How far a face's colour is moved either way from its station's, as a share: the facets of a flank.</summary>
        private const float FaceJitter = 0.045f;

        private static readonly Double3 Right = new Double3(1.0, 0.0, 0.0);
        private static readonly Double3 Forward = new Double3(0.0, 0.0, 1.0);

        /// <summary>Whether this build makes shapes for a kind. The fairy-wren is heard and not seen (M1.7a), and has none.</summary>
        public static bool Draws(AnimalSpecies species) =>
            species == AnimalSpecies.EasternGreyKangaroo || species == AnimalSpecies.PiedOystercatcher;

        /// <summary>How many bones a kind's skeleton has; nought for a kind this build draws no shapes for.</summary>
        public static int BoneCount(AnimalSpecies species)
        {
            if (species == AnimalSpecies.EasternGreyKangaroo) return KangarooBones;
            if (species == AnimalSpecies.PiedOystercatcher) return OystercatcherBones;
            return 0;
        }

        /// <summary>
        /// How long one hop takes at a speed, s: the stride divided by the speed, so the cadence follows the pace. At the
        /// 7 m/s a startled mob runs (<see cref="AnimalFlightRules.KangarooRunMs"/>) it is the measured 0.45 s.
        /// </summary>
        public static double HopSeconds(double speedMs) => speedMs > 0.1 ? HopStrideM / speedMs : HopStrideM / 0.1;

        /// <summary>
        /// The box an animal of a kind never leaves in any pose at any moment, m, in its own frame: what the Unity layer
        /// gives the renderer as the bounds it is culled by, since a skin that follows its bones has no bounds of its own
        /// until it is drawn. Generous on purpose; a test holds every posed point inside it.
        /// </summary>
        public static void ReachOf(AnimalSpecies species, out Double3 low, out Double3 high)
        {
            if (species == AnimalSpecies.PiedOystercatcher)
            {
                low = new Double3(-0.6, -0.1, -0.5);
                high = new Double3(0.6, 1.9, 0.5);
                return;
            }
            low = new Double3(-0.6, -0.1, -1.6);
            high = new Double3(0.6, 2.0, 1.2);
        }

        /// <summary>The skeleton the skin is hung on: the resting pose at nought seconds, in bone order.</summary>
        public static void BindPose(AnimalSpecies species, List<AnimalBone> into) => Build(species, AnimalPose.Resting, 0.0, into);

        /// <summary>
        /// Every bone of one animal in one pose at one instant, in a fixed order. A pose this build does not know — nought,
        /// or one a later server gives a new meaning — is drawn resting, because a client that refuses to draw an animal
        /// shows nothing and says nothing.
        /// </summary>
        /// <param name="timeS">
        /// The client's own clock, s. The fleeing pose turns it into the phase of a hop or a wingbeat, and the grazing pose
        /// into the slow lift of a head; the resting pose ignores it. An animal is given a phase of its own by its id, so a
        /// mob does not hop as one machine.
        /// </param>
        public static void Build(AnimalSpecies species, byte pose, double timeS, List<AnimalBone> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (species == null) throw new ArgumentNullException(nameof(species));
            into.Clear();
            if (species == AnimalSpecies.EasternGreyKangaroo) Kangaroo(pose, timeS, into);
            else if (species == AnimalSpecies.PiedOystercatcher) Oystercatcher(pose, timeS, into);
            else throw new ArgumentException("no shapes are made here for the " + species.DisplayName, nameof(species));
        }

        /// <summary>
        /// The skin of a kind, grown over its resting skeleton: what the Unity layer builds its one mesh from. Deterministic,
        /// so two clients grow the same animal.
        /// </summary>
        public static AnimalBody BodyOf(AnimalSpecies species)
        {
            List<AnimalBone> bind = new List<AnimalBone>();
            BindPose(species, bind);
            Loft loft = new Loft();
            if (species == AnimalSpecies.EasternGreyKangaroo) KangarooSkin(loft, bind);
            else OystercatcherSkin(loft, bind);
            return loft.Finish(species, bind);
        }

        /// <summary>
        /// Where the skin's points stand once the bones have moved: each point keeps its coordinates in the frame of the
        /// bone it rides, and a point shared by two bones takes the blend of the two places, which is the arithmetic the
        /// graphics card does for the drawn animal (linear blend skinning) written out here so a test can measure a pose.
        /// </summary>
        /// <param name="into">One place for every point of the body, in point order.</param>
        public static void Skin(AnimalBody body, IReadOnlyList<AnimalBone> bind, IReadOnlyList<AnimalBone> posed, Double3[] into)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (into == null || into.Length < body.Points.Count) throw new ArgumentException("room for every point is needed", nameof(into));
            if (bind.Count != posed.Count) throw new ArgumentException("the posed skeleton has a different number of bones from the bound one", nameof(posed));
            for (int i = 0; i < body.Points.Count; i++)
            {
                Double3 p = body.Points[i];
                int bone = body.PointBone[i], blend = body.PointBlend[i];
                double share = body.PointShare[i];
                Double3 rode = Ride(p, bind[bone], posed[bone]);
                if (blend >= 0 && share < 1.0) rode = rode * share + Ride(p, bind[blend], posed[blend]) * (1.0 - share);
                into[i] = rode;
            }
        }

        private static Double3 Ride(Double3 p, in AnimalBone was, in AnimalBone now)
        {
            Double3 d = p - was.Joint;
            double x = Double3.Dot(d, was.Across), y = Double3.Dot(d, was.Deep), z = Double3.Dot(d, was.Along);
            return now.Joint + now.Across * x + now.Deep * y + now.Along * z;
        }

        // ------------------------------------------------------------------ the eastern grey kangaroo

        private const int KangarooBones = 17;

        /// <summary>The bones' places in the kangaroo's list; each side's follow <see cref="RooSide"/>, <see cref="RooPerSide"/> to a side, in the order of <see cref="RooEar"/> to <see cref="RooForearm"/>.</summary>
        private const int RooTorso = 0, RooNeck = 1, RooHead = 2, RooTailBase = 3, RooTailTip = 4, RooSide = 5,
                          RooEar = 0, RooThigh = 1, RooShank = 2, RooFoot = 3, RooArm = 4, RooForearm = 5, RooPerSide = 6;

        private static int Roo(int side, int part) => RooSide + side * RooPerSide + part;

        /// <summary>
        /// The paired bones' names by side, in the order of <see cref="RooEar"/> to <see cref="RooForearm"/>, made once: a
        /// pose is built every frame for every animal drawn, and must make no strings to do it (2026-09-18).
        /// </summary>
        private static readonly string[][] RooSideNames =
        {
            new[] { "ear.left", "thigh.left", "shank.left", "foot.left", "arm.left", "forearm.left" },
            new[] { "ear.right", "thigh.right", "shank.right", "foot.right", "arm.right", "forearm.right" },
        };

        private static void Kangaroo(byte pose, double timeS, List<AnimalBone> into)
        {
            double hipY, hipZ, spine, neck, head, thigh, shank, foot, arm, forearm, tailBase, tailTip, ear;
            // Every pose's hip height puts the skin's underside on the ground, not the bones': the shank's rounded end
            // reaches below the heel joint by its own radius (KangarooSkin), and the heels stood that far in the ground
            // until 2026-09-18.
            if (pose == AnimalPose.Fleeing)
            {
                // The hop: the body rises and falls once a stride, the legs swing forward to meet the ground and tuck up
                // behind, and the tail comes up as a counterweight at the top of the bound.
                double phase = Fraction(timeS / HopSeconds(AnimalFlightRules.KangarooRunMs));
                double lift = HopLiftM * Math.Sin(Math.PI * phase);
                // Nought at the touchdowns, one at the top of the hop.
                double up = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * phase);
                hipY = 0.775 + lift;
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
                // Feeding: the back sloping steeply down to a head near the sward, the forepaws almost to the ground
                // (a grazing kangaroo goes on all fours), the tail out behind. The slope was steepened and the arms
                // brought down when the skin replaced the pieces (2026-09-16): a grazer with its paws dangling at the
                // knee read as sitting up.
                double bob = Math.Sin(2.0 * Math.PI * timeS / GrazeBobSeconds);
                hipY = 0.795;
                hipZ = -0.30;
                spine = -24.0;
                neck = -50.0 + 6.0 * bob;
                head = -70.0 + 9.0 * bob;
                thigh = -64.0;
                shank = -122.0;
                foot = 0.0;
                arm = -85.0;
                forearm = -70.0;
                tailBase = 195.0;
                tailTip = 210.0;
                ear = 120.0;
            }
            else
            {
                // Lying up, which is what a mob does at noon: down on the belly, the long feet flat alongside, the tail
                // out along the ground, the head up and watching.
                hipY = 0.305;
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
            Double3 tailMid = hip + Dir(tailBase) * TailBaseM;

            into.Add(Bone("torso", hip, Dir(spine), TorsoM));
            into.Add(Bone("neck", shoulder, Dir(neck), NeckM));
            into.Add(Bone("head", headBase, Dir(head), HeadM));
            into.Add(Bone("tail.base", hip, Dir(tailBase), TailBaseM));
            into.Add(Bone("tail.tip", tailMid, Dir(tailTip), TailTipM));

            for (int s = 0; s < 2; s++)
            {
                double side = s == 0 ? -1.0 : 1.0;
                string[] names = RooSideNames[s];
                Double3 across = new Double3(side, 0.0, 0.0);

                // The ears stand off the back of the skull, up and a little behind.
                Double3 earRoot = headBase + Dir(head) * (HeadM * 0.18) + across * EarHalfM + new Double3(0.0, 0.03, 0.0);
                into.Add(Bone(names[RooEar], earRoot, Dir(ear), EarM));

                Double3 hipSide = hip + across * HipHalfM;
                Double3 knee = hipSide + Dir(thigh) * ThighM;
                Double3 heel = knee + Dir(shank) * ShankM;
                into.Add(Bone(names[RooThigh], hipSide, Dir(thigh), ThighM));
                into.Add(Bone(names[RooShank], knee, Dir(shank), ShankM));
                into.Add(Bone(names[RooFoot], heel, Dir(foot), FootM));

                Double3 shoulderSide = shoulder + across * ShoulderHalfM + new Double3(0.0, -0.02, -0.02);
                Double3 elbow = shoulderSide + Dir(arm) * ArmM;
                into.Add(Bone(names[RooArm], shoulderSide, Dir(arm), ArmM));
                into.Add(Bone(names[RooForearm], elbow, Dir(forearm), ForearmM));
            }
        }

        /// <summary>
        /// The kangaroo's skin over its resting bones. One shell runs from the tail's tip through the rump, the back, the
        /// neck and the skull to the nose, deepest at the belly and narrowing to the shoulders and again to the muzzle,
        /// pale underneath. Each haunch is a fat shell on the thigh, rooted in the rump and tapering to a knee that is a
        /// ball; the shank and the foot nest in it and in each other. An arm is one shell over both its bones with a mitred
        /// elbow, and an ear a flat one rooted in the skull.
        /// </summary>
        private static void KangarooSkin(Loft loft, List<AnimalBone> bind)
        {
            AnimalBone torso = bind[RooTorso], neck = bind[RooNeck], head = bind[RooHead], tailBase = bind[RooTailBase], tailTip = bind[RooTailTip];

            // Every shell's rounded end reaches to its bone's end and no further: the last ring stands a cap's length short
            // of it, at the tail's tip as at the nose (the tail's tip ring stood at the bone's end until 2026-09-18, so
            // the cap overshot it).
            loft.Begin("body", 12, Right, null, false, 0.05, 0.04);
            loft.Way(At(tailTip, 1.0 - 0.05 / TailTipM), 0.030, 0.030, 0.0, RooTailTip, -1, 1.0, Dark, Dark, -1.0);
            loft.Way(At(tailTip, 0.6), 0.045, 0.045, 0.0, RooTailTip, -1, 1.0, Coat, Coat, -1.0);
            loft.Way(At(tailTip, 0.2), 0.060, 0.060, 0.0, RooTailTip, RooTailBase, 0.75, Coat, Coat, -1.0);
            loft.Way(tailTip.Joint, 0.070, 0.070, 0.0, RooTailTip, RooTailBase, 0.5, Coat, Coat, -1.0);
            loft.Way(At(tailBase, 0.6), 0.085, 0.090, 0.0, RooTailBase, RooTailTip, 0.75, Coat, Coat, -1.0);
            loft.Way(At(tailBase, 0.25), 0.115, 0.125, 0.01, RooTailBase, RooTorso, 0.75, Coat, Coat, -1.0);
            loft.Way(torso.Joint, 0.155, 0.175, 0.02, RooTorso, RooTailBase, 0.5, Coat, Chest, -0.45);
            loft.Way(At(torso, 0.2), 0.170, 0.205, 0.045, RooTorso, -1, 1.0, Coat, Chest, -0.3);
            loft.Way(At(torso, 0.5), 0.165, 0.200, 0.05, RooTorso, -1, 1.0, Coat, Chest, -0.3);
            loft.Way(At(torso, 0.8), 0.135, 0.165, 0.035, RooTorso, RooNeck, 0.75, Coat, Chest, -0.25);
            loft.Way(torso.End, 0.100, 0.110, 0.015, RooTorso, RooNeck, 0.5, Coat, Chest, -0.2);
            loft.Way(At(neck, 0.5), 0.066, 0.072, 0.0, RooNeck, -1, 1.0, Coat, Chest, -0.2);
            loft.Way(neck.End, 0.064, 0.070, 0.0, RooNeck, RooHead, 0.5, Coat, Chest, -0.4);
            loft.Way(At(head, 0.3), 0.078, 0.088, 0.0, RooHead, -1, 1.0, Coat, Chest, -0.4);
            loft.Way(At(head, 0.62), 0.060, 0.064, 0.0, RooHead, -1, 1.0, Coat, Coat, -1.0);
            loft.Way(At(head, 1.0 - 0.04 / HeadM), 0.044, 0.042, 0.0, RooHead, -1, 1.0, Dark, Dark, -1.0);
            loft.Close();

            for (int s = 0; s < 2; s++)
            {
                string hand = s == 0 ? ".left" : ".right";
                AnimalBone ear = bind[Roo(s, RooEar)], thigh = bind[Roo(s, RooThigh)], shank = bind[Roo(s, RooShank)];
                AnimalBone foot = bind[Roo(s, RooFoot)], arm = bind[Roo(s, RooArm)], forearm = bind[Roo(s, RooForearm)];
                int ti = Roo(s, RooThigh), si = Roo(s, RooShank), fi = Roo(s, RooFoot), ai = Roo(s, RooArm), fa = Roo(s, RooForearm);

                loft.Begin("haunch" + hand, 10, Right, "body", false, 0.045, 0.055);
                loft.Way(thigh.Joint, 0.125, 0.150, 0.0, ti, -1, 1.0, Coat, Coat, -1.0);
                loft.Way(At(thigh, 0.35), 0.125, 0.150, 0.0, ti, -1, 1.0, Coat, Coat, -1.0);
                loft.Way(At(thigh, 0.7), 0.090, 0.100, 0.0, ti, -1, 1.0, Coat, Coat, -1.0);
                loft.Way(thigh.End, 0.055, 0.055, 0.0, ti, -1, 1.0, Coat, Coat, -1.0);
                loft.Close();

                loft.Begin("shank" + hand, 8, Right, "haunch" + hand, true, 0.05, 0.04);
                loft.Way(shank.Joint, 0.050, 0.050, 0.0, si, -1, 1.0, Coat, Coat, -1.0);
                loft.Way(At(shank, 0.5), 0.044, 0.044, 0.0, si, -1, 1.0, Coat, Coat, -1.0);
                loft.Way(shank.End, 0.040, 0.040, 0.0, si, -1, 1.0, Coat, Coat, -1.0);
                loft.Close();

                loft.Begin("foot" + hand, 8, Right, "shank" + hand, true, 0.035, 0.04);
                loft.Way(foot.Joint, 0.036, 0.034, 0.0, fi, -1, 1.0, Dark, Dark, -1.0);
                loft.Way(At(foot, 0.3), 0.046, 0.030, 0.0, fi, -1, 1.0, Dark, Dark, -1.0);
                loft.Way(At(foot, 0.7), 0.048, 0.028, 0.0, fi, -1, 1.0, Dark, Dark, -1.0);
                loft.Way(At(foot, 1.0 - 0.04 / FootM), 0.036, 0.022, 0.0, fi, -1, 1.0, Dark, Dark, -1.0);
                loft.Close();

                loft.Begin("arm" + hand, 8, Right, "body", false, 0.045, 0.035);
                loft.Way(arm.Joint, 0.040, 0.040, 0.0, ai, -1, 1.0, Coat, Coat, -1.0);
                loft.Way(At(arm, 0.5), 0.036, 0.036, 0.0, ai, fa, 0.75, Coat, Coat, -1.0);
                loft.Way(arm.End, 0.032, 0.032, 0.0, ai, fa, 0.5, Dark, Dark, -1.0);
                loft.Way(At(forearm, 0.5), 0.028, 0.028, 0.0, fa, ai, 0.75, Dark, Dark, -1.0);
                loft.Way(At(forearm, 1.0 - 0.035 / ForearmM), 0.030, 0.020, 0.0, fa, -1, 1.0, Dark, Dark, -1.0);
                loft.Close();

                loft.Begin("ear" + hand, 6, Right, "body", false, 0.02, 0.025);
                loft.Way(ear.Joint, 0.034, 0.011, 0.0, Roo(s, RooEar), -1, 1.0, Dark, Dark, -1.0);
                loft.Way(At(ear, 0.45), 0.038, 0.010, 0.0, Roo(s, RooEar), -1, 1.0, Dark, Dark, -1.0);
                loft.Way(At(ear, 1.0 - 0.025 / EarM), 0.022, 0.008, 0.0, Roo(s, RooEar), -1, 1.0, Dark, Dark, -1.0);
                loft.Close();
            }
        }

        // ------------------------------------------------------------------ the pied oystercatcher

        private const int OystercatcherBones = 13;

        /// <summary>The bones' places in the oystercatcher's list; each side's follow <see cref="BirdSide"/>, <see cref="BirdPerSide"/> to a side, in the order of <see cref="BirdWing"/> to <see cref="BirdFoot"/>.</summary>
        private const int BirdBody = 0, BirdNeck = 1, BirdHead = 2, BirdBill = 3, BirdTail = 4, BirdSide = 5,
                          BirdWing = 0, BirdPinion = 1, BirdLeg = 2, BirdFoot = 3, BirdPerSide = 4;

        private static int Bird(int side, int part) => BirdSide + side * BirdPerSide + part;

        /// <summary>The paired bones' names by side, in the order of <see cref="BirdWing"/> to <see cref="BirdFoot"/>, made once as the kangaroo's are.</summary>
        private static readonly string[][] BirdSideNames =
        {
            new[] { "wing.left", "pinion.left", "leg.left", "foot.left" },
            new[] { "wing.right", "pinion.right", "leg.right", "foot.right" },
        };

        private static void Oystercatcher(byte pose, double timeS, List<AnimalBone> into)
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
                rumpY = 0.18;
                rumpZ = -0.10;
                body = 10.0;
                neck = 6.0 + 14.0 * bob;
                head = -38.0 + 16.0 * bob;
                bill = -54.0 + 14.0 * bob;
                tail = 196.0;
                leg = -96.0;
                foot = 0.0;
                wing = -170.0;
                pinion = 168.0;
                flapDeg = 0.0;
                sweepDeg = 0.0;
            }
            else
            {
                // Standing on the tideline, head up. The rump's height, here and grazing, puts the sole of the foot on the
                // sand: the ankle is the leg's length below the hip and the foot's skin a few millimetres below that
                // (until 2026-09-18 the bird stood a finger's width above the ground it was given).
                rumpY = 0.19;
                rumpZ = -0.10;
                body = 6.0;
                neck = 55.0;
                head = 18.0;
                bill = -8.0;
                tail = 190.0;
                leg = -95.0;
                foot = 0.0;
                wing = -170.0;
                pinion = 168.0;
                flapDeg = 0.0;
                sweepDeg = 0.0;
            }

            Double3 rump = new Double3(0.0, rumpY, rumpZ);
            Double3 breast = rump + Dir(body) * BirdBodyM;
            Double3 neckTop = breast + Dir(neck) * BirdNeckM;
            Double3 headFront = neckTop + Dir(head) * BirdHeadM;

            into.Add(Bone("body", rump, Dir(body), BirdBodyM));
            into.Add(Bone("neck", breast, Dir(neck), BirdNeckM));
            into.Add(Bone("head", neckTop, Dir(head), BirdHeadM));
            into.Add(Bone("bill", headFront, Dir(bill), BirdBillM));
            into.Add(Bone("tail", rump, Dir(tail), BirdTailM));

            for (int s = 0; s < 2; s++)
            {
                double side = s == 0 ? -1.0 : 1.0;
                string[] names = BirdSideNames[s];
                Double3 across = new Double3(side, 0.0, 0.0);

                // The shoulder sits a little forward of the body's widest ring, where the wing's root stays buried in the
                // flank in every pose (at 0.72 of the body, until 2026-09-18, the root's rounded end broke the skin where
                // the body narrows to the breast).
                Double3 wingRoot = Between(rump, breast, 0.62) + across * BirdWingHalfM + new Double3(0.0, 0.005, 0.0);
                Double3 wingDir, pinionDir;
                if (flying)
                {
                    wingDir = Out(side, flapDeg, sweepDeg);
                    pinionDir = Out(side, flapDeg * 1.35, sweepDeg + 8.0);
                }
                else
                {
                    // Folded: back and a little down to the wrist, then back and a little up, so the wing lies flat along
                    // the flank with its tip over the tail, as a wader's does.
                    wingDir = Dir(wing);
                    pinionDir = Dir(pinion);
                }
                Double3 wrist = wingRoot + wingDir * BirdWingM;
                // A wing's frame keeps the flat of the wing where it lies — along the flank folded, level in the air — so
                // its across is the chord: up the flank when folded, fore and aft when spread; a real wing turns a quarter
                // about its own length as it opens. The folded bones lie almost along the bird's fore-and-aft line, which
                // gives no steady square to them (hung on it on 2026-09-16, the wrist's ring came out turned over against
                // the wing's and the band between them twisted), so they take the bird's up, and only the spread ones the
                // fore-and-aft line, which no spread bone comes near.
                Double3 chord = flying ? Forward : Double3.Up;
                into.Add(Bone(names[BirdWing], wingRoot, wingDir, BirdWingM, chord));
                into.Add(Bone(names[BirdPinion], wrist, pinionDir, BirdPinionM, chord));

                Double3 hip = Between(rump, breast, 0.45) + across * BirdHalfM + new Double3(0.0, -0.035, 0.0);
                Double3 ankle = hip + Dir(leg) * BirdLegM;
                into.Add(Bone(names[BirdLeg], hip, Dir(leg), BirdLegM));
                into.Add(Bone(names[BirdFoot], ankle, Dir(foot), BirdFootM));
            }
        }

        /// <summary>
        /// The oystercatcher's skin over its standing bones. One shell runs from the flat tail through the rump and the
        /// body, black above and white below, up the neck to the skull, all black; the bill is a thin orange shell rooted
        /// inside the face; each wing is one flat shell over both its bones with a mitred wrist, rooted in the back; each
        /// leg a thin pink shell rooted in the belly with a flat foot nested at the ankle.
        /// </summary>
        private static void OystercatcherSkin(Loft loft, List<AnimalBone> bind)
        {
            AnimalBone body = bind[BirdBody], neck = bind[BirdNeck], head = bind[BirdHead], bill = bind[BirdBill], tail = bind[BirdTail];

            loft.Begin("body", 12, Right, null, false, 0.02, 0.022);
            loft.Way(At(tail, 1.0 - 0.02 / BirdTailM), 0.036, 0.006, 0.0, BirdTail, -1, 1.0, Black, White, -0.3);
            loft.Way(At(tail, 0.5), 0.040, 0.009, 0.0, BirdTail, BirdBody, 0.75, Black, White, -0.3);
            loft.Way(tail.Joint, 0.052, 0.032, 0.005, BirdTail, BirdBody, 0.5, Black, White, -0.15);
            loft.Way(At(body, 0.22), 0.070, 0.054, 0.008, BirdBody, -1, 1.0, Black, White, -0.1);
            loft.Way(At(body, 0.5), 0.075, 0.058, 0.010, BirdBody, -1, 1.0, Black, White, -0.1);
            loft.Way(At(body, 0.78), 0.068, 0.052, 0.006, BirdBody, BirdNeck, 0.75, Black, White, -0.1);
            loft.Way(body.End, 0.045, 0.043, 0.0, BirdBody, BirdNeck, 0.5, Black, White, -0.35);
            loft.Way(At(neck, 0.5), 0.028, 0.028, 0.0, BirdNeck, -1, 1.0, Black, Black, -1.0);
            loft.Way(neck.End, 0.027, 0.027, 0.0, BirdNeck, BirdHead, 0.5, Black, Black, -1.0);
            loft.Way(At(head, 0.4), 0.030, 0.031, 0.0, BirdHead, -1, 1.0, Black, Black, -1.0);
            loft.Way(At(head, 1.0 - 0.022 / BirdHeadM), 0.026, 0.027, 0.0, BirdHead, -1, 1.0, Black, Black, -1.0);
            loft.Close();

            // The bill starts a little inside the face, so its root is hidden however the head turns.
            loft.Begin("bill", 6, Right, "body", false, 0.006, 0.012);
            loft.Way(bill.Joint - bill.Along * 0.02, 0.0068, 0.0068, 0.0, BirdBill, -1, 1.0, Bill, Bill, -1.0);
            loft.Way(At(bill, 0.7), 0.0065, 0.0065, 0.0, BirdBill, -1, 1.0, Bill, Bill, -1.0);
            loft.Way(At(bill, 1.0 - 0.012 / BirdBillM), 0.0050, 0.0050, 0.0, BirdBill, -1, 1.0, Bill, Bill, -1.0);
            loft.Close();

            for (int s = 0; s < 2; s++)
            {
                string hand = s == 0 ? ".left" : ".right";
                AnimalBone wing = bind[Bird(s, BirdWing)], pinion = bind[Bird(s, BirdPinion)], leg = bind[Bird(s, BirdLeg)], foot = bind[Bird(s, BirdFoot)];
                int wi = Bird(s, BirdWing), pi = Bird(s, BirdPinion), li = Bird(s, BirdLeg), fi = Bird(s, BirdFoot);

                // Folded, the flat of the wing lies along the flank with its chord up and down, so the shell's wide axis
                // is the bird's up squared to the bone, as the folded bones' own frames are (2026-09-18).
                loft.Begin("wing" + hand, 6, Double3.Up, "body", false, 0.02, 0.04);
                loft.Way(wing.Joint, 0.045, 0.006, 0.0, wi, -1, 1.0, Black, Black, -1.0);
                loft.Way(At(wing, 0.5), 0.052, 0.006, 0.0, wi, pi, 0.75, Black, Black, -1.0);
                loft.Way(wing.End, 0.046, 0.005, 0.0, wi, pi, 0.5, Black, Black, -1.0);
                loft.Way(At(pinion, 0.5), 0.036, 0.004, 0.0, pi, wi, 0.75, Black, Black, -1.0);
                loft.Way(At(pinion, 1.0 - 0.04 / BirdPinionM), 0.020, 0.003, 0.0, pi, -1, 1.0, Black, Black, -1.0);
                loft.Close();

                loft.Begin("leg" + hand, 6, Right, "body", false, 0.01, 0.006);
                loft.Way(leg.Joint, 0.0065, 0.0065, 0.0, li, -1, 1.0, Pink, Pink, -1.0);
                loft.Way(At(leg, 0.5), 0.0062, 0.0062, 0.0, li, -1, 1.0, Pink, Pink, -1.0);
                loft.Way(leg.End, 0.0060, 0.0060, 0.0, li, -1, 1.0, Pink, Pink, -1.0);
                loft.Close();

                loft.Begin("foot" + hand, 6, Right, "leg" + hand, true, 0.005, 0.012);
                loft.Way(foot.Joint, 0.0058, 0.0045, 0.0, fi, -1, 1.0, Pink, Pink, -1.0);
                loft.Way(At(foot, 0.5), 0.0170, 0.0040, 0.0, fi, -1, 1.0, Pink, Pink, -1.0);
                loft.Way(At(foot, 1.0 - 0.012 / BirdFootM), 0.0150, 0.0035, 0.0, fi, -1, 1.0, Pink, Pink, -1.0);
                loft.Close();
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

        /// <summary>A point along a bone, at a share of its length from its joint.</summary>
        private static Double3 At(in AnimalBone bone, double share) => bone.Joint + bone.Along * (bone.LengthM * share);

        /// <summary>The part of a number after the point, never negative: a phase that keeps running for a clock that does.</summary>
        private static double Fraction(double v)
        {
            double f = v - Math.Floor(v);
            return f < 0.0 ? f + 1.0 : f;
        }

        /// <summary>
        /// A bone from its joint along a direction, with a frame whose Across is the hint turned square to the bone: the
        /// animal's right for every bone in its vertical plane, and for a wing its chord (<see cref="Oystercatcher"/> says
        /// which line that is folded and which spread).
        /// </summary>
        private static AnimalBone Bone(string name, Double3 joint, Double3 direction, double lengthM) => Bone(name, joint, direction, lengthM, Right);

        private static AnimalBone Bone(string name, Double3 joint, Double3 direction, double lengthM, Double3 acrossHint)
        {
            Double3 along = direction.Normalized;
            Double3 across = Square(acrossHint, along);
            return new AnimalBone(name, joint, along, across, Double3.Cross(along, across), lengthM);
        }

        /// <summary>The hint with its part along the axis taken out, made unit; a hint along the axis falls back to the animal's up or right.</summary>
        private static Double3 Square(Double3 hint, Double3 axis)
        {
            Double3 u = hint - axis * Double3.Dot(hint, axis);
            if (u.SqrLength < 1e-10) u = Double3.Up - axis * axis.Y;
            if (u.SqrLength < 1e-10) u = Right - axis * axis.X;
            return u.Normalized;
        }

        /// <summary>A value in [0, 1) from two whole numbers, so a face's shade never depends on the order it was built in.</summary>
        private static float Hash01(int a, int b)
        {
            uint h = (uint)a * 2654435761u ^ (uint)b * 2246822519u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }

        private static Rgb Shade(Rgb colour, int seed, int face)
        {
            float f = 1f + (Hash01(seed, face) - 0.5f) * 2f * FaceJitter;
            return new Rgb(Clamp01(colour.R * f), Clamp01(colour.G * f), Clamp01(colour.B * f));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ------------------------------------------------------------------ the loft

        /// <summary>
        /// Grows the shells of one skin. A shell is begun, given its waypoints in order along its line, and closed: the
        /// rings are set square to the line, mitred where it bends, rounded off at both ends, skinned with faces wound
        /// outward, and coloured by station; then <see cref="Finish"/> shares the corners between faces and hands the body
        /// over.
        /// </summary>
        private sealed class Loft
        {
            private struct Waypoint
            {
                public Double3 At;
                public double AcrossM, DeepM, DropM;
                public int Bone, Blend;
                public double Share;
                public Rgb Above, Below;
                public double BellyLine;
            }

            private struct Ring
            {
                public Double3 Centre, Along, Across, Deep, StretchDir;
                public double AcrossM, DeepM, Stretch;
                public int Bone, Blend;
                public double Share;
                public bool Cap;
                public Rgb Above, Below;
                public double BellyLine;
            }

            private readonly List<Double3> _points = new List<Double3>();
            private readonly List<int> _pointBone = new List<int>(), _pointBlend = new List<int>();
            private readonly List<double> _pointShare = new List<double>();
            private readonly List<AnimalFace> _faces = new List<AnimalFace>();
            private readonly List<AnimalShell> _shells = new List<AnimalShell>();
            private readonly List<Waypoint> _way = new List<Waypoint>();
            private string _name, _parent;
            private int _sides;
            private Double3 _hint;
            private bool _ball;
            private double _startCap, _endCap;

            public void Begin(string name, int sides, Double3 acrossHint, string parent, bool ballJoint, double startCapM, double endCapM)
            {
                if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides));
                if (startCapM <= 0.0 || endCapM <= 0.0) throw new ArgumentOutOfRangeException(nameof(startCapM), "a shell is rounded off at both ends");
                _name = name;
                _sides = sides;
                _hint = acrossHint;
                _parent = parent;
                _ball = ballJoint;
                _startCap = startCapM;
                _endCap = endCapM;
                _way.Clear();
            }

            /// <param name="dropM">How far the ring's middle hangs below the line, m: a belly under a spine.</param>
            /// <param name="bellyLine">The sine of the angle round the ring below which <paramref name="below"/> is worn; −1 for none of it.</param>
            public void Way(Double3 at, double acrossM, double deepM, double dropM, int bone, int blend, double share, Rgb above, Rgb below, double bellyLine)
            {
                if (acrossM <= 0.0 || deepM <= 0.0) throw new ArgumentOutOfRangeException(nameof(acrossM), "a ring has a width");
                if (bone < 0) throw new ArgumentOutOfRangeException(nameof(bone));
                if (share <= 0.0 || share > 1.0) throw new ArgumentOutOfRangeException(nameof(share));
                _way.Add(new Waypoint
                {
                    At = at, AcrossM = acrossM, DeepM = deepM, DropM = dropM, Bone = bone, Blend = blend, Share = blend < 0 ? 1.0 : share,
                    Above = above, Below = below, BellyLine = bellyLine,
                });
            }

            public void Close()
            {
                int n = _way.Count;
                if (n < 2) throw new InvalidOperationException("a shell needs two waypoints at least: " + _name);
                Double3[] dirIn = new Double3[n], dirOut = new Double3[n];
                for (int i = 0; i + 1 < n; i++)
                {
                    Double3 d = (_way[i + 1].At - _way[i].At).Normalized;
                    dirOut[i] = d;
                    dirIn[i + 1] = d;
                }
                dirIn[0] = dirOut[0];
                dirOut[n - 1] = dirIn[n - 1];

                Ring[] main = new Ring[n];
                for (int i = 0; i < n; i++)
                {
                    Waypoint w = _way[i];
                    Double3 sum = dirIn[i] + dirOut[i];
                    Double3 along = sum.Normalized;
                    // Half the turn's cosine is half the sum's length; its secant keeps the tube its width round the bend.
                    // On a straight run the two directions are one and the ring is left exactly as wide as stated:
                    // rounding can put the cosine a hair over one, and a ring a hair narrower than its neighbours is a
                    // step in the profile (2026-09-18).
                    double cosHalf = Math.Min(1.0, 0.5 * sum.Length);
                    double stretch = cosHalf > 1e-6 ? 1.0 / cosHalf : 1.0;
                    Double3 bend = dirOut[i] - dirIn[i];
                    Double3 stretchDir = bend.SqrLength > 1e-12 ? bend.Normalized : along;
                    Double3 across = Square(_hint, along);
                    Double3 deep = Double3.Cross(along, across);
                    main[i] = new Ring
                    {
                        Centre = w.At - deep * w.DropM, Along = along, Across = across, Deep = deep, StretchDir = stretchDir,
                        AcrossM = w.AcrossM, DeepM = w.DeepM, Stretch = stretch, Bone = w.Bone, Blend = w.Blend, Share = w.Share,
                        Cap = false, Above = w.Above, Below = w.Below, BellyLine = w.BellyLine,
                    };
                }

                // The rings in order: the start's caps from the pole inward, the profile, the end's caps outward.
                List<Ring> rings = new List<Ring>();
                for (int k = CapRings; k >= 1; k--) rings.Add(CapRing(main[0], -main[0].Along, _startCap, k));
                rings.AddRange(main);
                for (int k = 1; k <= CapRings; k++) rings.Add(CapRing(main[n - 1], main[n - 1].Along, _endCap, k));

                int startPole = Point(main[0].Centre - main[0].Along * _startCap, main[0]);
                List<AnimalStation> stations = new List<AnimalStation>(rings.Count);
                foreach (Ring ring in rings)
                {
                    int first = _points.Count;
                    for (int k = 0; k < _sides; k++)
                    {
                        double a = k * (2.0 * Math.PI / _sides);
                        Double3 local = ring.Across * (ring.AcrossM * Math.Cos(a)) + ring.Deep * (ring.DeepM * Math.Sin(a));
                        if (ring.Stretch != 1.0) local += ring.StretchDir * ((ring.Stretch - 1.0) * Double3.Dot(ring.StretchDir, local));
                        Point(ring.Centre + local, ring);
                    }
                    stations.Add(new AnimalStation(ring.Centre, ring.Along, ring.Across, ring.Deep, ring.AcrossM, ring.DeepM, ring.Stretch,
                                                   ring.StretchDir, ring.Bone, ring.Blend, ring.Share, first, ring.Cap, ring.Above, ring.Below, ring.BellyLine));
                }
                int endPole = Point(main[n - 1].Centre + main[n - 1].Along * _endCap, main[n - 1]);

                int shell = _shells.Count, firstFace = _faces.Count;
                Fan(startPole, stations[0], rings[0], main[0].Centre, shell, false);
                for (int i = 0; i + 1 < stations.Count; i++)
                {
                    // A band wears the colours of the ring it starts from; a cap ring wears its profile ring's.
                    AnimalStation lower = stations[i], upper = stations[i + 1];
                    Ring wearing = rings[i];
                    Double3 inside = (lower.Centre + upper.Centre) * 0.5;
                    for (int k = 0; k < _sides; k++)
                    {
                        int k2 = (k + 1) % _sides;
                        double mid = (k + 0.5) * (2.0 * Math.PI / _sides);
                        Rgb colour = Wear(wearing, Math.Sin(mid), shell);
                        Face(lower.FirstPoint + k, upper.FirstPoint + k, upper.FirstPoint + k2, inside, shell, colour);
                        Face(lower.FirstPoint + k, upper.FirstPoint + k2, lower.FirstPoint + k2, inside, shell, colour);
                    }
                }
                Fan(endPole, stations[stations.Count - 1], rings[rings.Count - 1], main[n - 1].Centre, shell, true);
                _shells.Add(new AnimalShell(_name, _sides, _parent, _ball, stations.AsReadOnly(), startPole, endPole, firstFace, _faces.Count - firstFace, _startCap, _endCap));
            }

            /// <summary>A ring of a rounded end: on the half-ellipsoid beyond the last profile ring, at the k-th of the cap's angles.</summary>
            private static Ring CapRing(Ring end, Double3 outward, double capM, int k)
            {
                double a = k * (0.5 * Math.PI / (CapRings + 1));
                Ring ring = end;
                ring.Centre = end.Centre + outward * (capM * Math.Sin(a));
                ring.AcrossM = end.AcrossM * Math.Cos(a);
                ring.DeepM = end.DeepM * Math.Cos(a);
                ring.Stretch = 1.0;
                ring.StretchDir = end.Along;
                ring.Cap = true;
                return ring;
            }

            private int Point(Double3 at, in Ring ring)
            {
                _points.Add(at);
                _pointBone.Add(ring.Bone);
                _pointBlend.Add(ring.Blend);
                _pointShare.Add(ring.Share);
                return _points.Count - 1;
            }

            /// <summary>The colour a face wears at an angle round its ring: the lower colour under the belly line, the upper above it, shaded a little by face.</summary>
            private Rgb Wear(in Ring ring, double sine, int shell) => Shade(sine < ring.BellyLine ? ring.Below : ring.Above, shell, _faces.Count);

            private void Fan(int pole, in AnimalStation ring, in Ring wearing, Double3 inside, int shell, bool end)
            {
                for (int k = 0; k < _sides; k++)
                {
                    int k2 = (k + 1) % _sides;
                    double mid = (k + 0.5) * (2.0 * Math.PI / _sides);
                    Face(pole, ring.FirstPoint + k, ring.FirstPoint + k2, inside, shell, Wear(wearing, Math.Sin(mid), shell));
                }
            }

            /// <summary>A face over three points, wound away from a point inside the shell so it looks outward.</summary>
            private void Face(int a, int b, int c, Double3 inside, int shell, Rgb colour)
            {
                Double3 pa = _points[a], pb = _points[b], pc = _points[c];
                Double3 n = Double3.Cross(pb - pa, pc - pa);
                if (Double3.Dot(n, (pa + pb + pc) / 3.0 - inside) < 0.0)
                {
                    int swap = b;
                    b = c;
                    c = swap;
                }
                _faces.Add(new AnimalFace(a, b, c, shell, colour));
            }

            /// <summary>
            /// Shares the corners between the faces as <c>StandMeshes</c> does: a face leads with a corner that carries its
            /// own plane and colour and borrows whatever corners already stand at its other two points. A corner leads one
            /// face at most; one that stands at a point without leading yet is given to the next face that can lead from
            /// there, so the skin has about as many corners as faces rather than three times as many.
            /// </summary>
            public AnimalBody Finish(AnimalSpecies species, List<AnimalBone> bind)
            {
                int[] cornerAt = new int[_points.Count];
                for (int i = 0; i < cornerAt.Length; i++) cornerAt[i] = -1;
                // For each corner: the point it stands at, the face it was made for, and whether it leads that face.
                List<int> vertexPoint = new List<int>(_faces.Count + _points.Count), vertexFace = new List<int>(_faces.Count + _points.Count);
                List<bool> leads = new List<bool>(_faces.Count + _points.Count);
                int[] triangles = new int[_faces.Count * 3];
                for (int f = 0; f < _faces.Count; f++)
                {
                    int a = _faces[f].A, b = _faces[f].B, c = _faces[f].C;
                    // Turn the three round, keeping the winding, so the face leads from a point whose corner leads nothing yet.
                    if (Taken(cornerAt, leads, a) && !Taken(cornerAt, leads, b))
                    {
                        int t = a;
                        a = b;
                        b = c;
                        c = t;
                    }
                    else if (Taken(cornerAt, leads, a) && !Taken(cornerAt, leads, c))
                    {
                        int t = a;
                        a = c;
                        c = b;
                        b = t;
                    }
                    triangles[3 * f] = Lead(cornerAt, leads, vertexPoint, vertexFace, a, f);
                    triangles[3 * f + 1] = Borrow(cornerAt, leads, vertexPoint, vertexFace, b, f);
                    triangles[3 * f + 2] = Borrow(cornerAt, leads, vertexPoint, vertexFace, c, f);
                }
                int[] leadingFace = new int[vertexPoint.Count];
                for (int v = 0; v < leadingFace.Length; v++) leadingFace[v] = leads[v] ? vertexFace[v] : -1;
                AnimalBody body = new AnimalBody(species, bind.AsReadOnly(), _shells.AsReadOnly(), _points.AsReadOnly(), _faces.AsReadOnly(),
                                                 _pointBone.ToArray(), _pointBlend.ToArray(), _pointShare.ToArray(), triangles,
                                                 vertexPoint.ToArray(), leadingFace, new Double3[vertexPoint.Count], new Rgb[vertexPoint.Count]);
                for (int v = 0; v < vertexPoint.Count; v++)
                {
                    // A borrowed corner carries the plane and colour of the face it was made for; nothing reads them.
                    int face = vertexFace[v];
                    body.VertexNormal[v] = body.Normal(face);
                    body.VertexColour[v] = _faces[face].Colour;
                }
                return body;
            }

            private static bool Taken(int[] cornerAt, List<bool> leads, int point) => cornerAt[point] >= 0 && leads[cornerAt[point]];

            private static int Lead(int[] cornerAt, List<bool> leads, List<int> vertexPoint, List<int> vertexFace, int point, int face)
            {
                int v = cornerAt[point];
                if (v >= 0 && !leads[v])
                {
                    leads[v] = true;
                    vertexFace[v] = face;
                    return v;
                }
                v = Corner(leads, vertexPoint, vertexFace, point, face, true);
                if (cornerAt[point] < 0) cornerAt[point] = v;
                return v;
            }

            private static int Borrow(int[] cornerAt, List<bool> leads, List<int> vertexPoint, List<int> vertexFace, int point, int face)
            {
                int v = cornerAt[point];
                if (v >= 0) return v;
                v = Corner(leads, vertexPoint, vertexFace, point, face, false);
                cornerAt[point] = v;
                return v;
            }

            private static int Corner(List<bool> leads, List<int> vertexPoint, List<int> vertexFace, int point, int face, bool lead)
            {
                vertexPoint.Add(point);
                vertexFace.Add(face);
                leads.Add(lead);
                return vertexPoint.Count - 1;
            }
        }
    }
}

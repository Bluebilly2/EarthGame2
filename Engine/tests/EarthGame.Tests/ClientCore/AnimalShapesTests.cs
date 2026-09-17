using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// What the two animals are made of (M1.7b): a skeleton of fixed bones whose joints a pose moves, and one skin of
    /// closed shells grown over it that follows the bones. The skin is held to be one smooth, connected body — every shell
    /// closed with no open edge, its rings sharing their points, its radius changing gently, every limb rooted inside its
    /// parent and every sharp joint one ball nested in another, in every pose — and to the published measurements of the
    /// species; the two sides mirror each other; the hop's cadence comes from the speed a mob flees at and the stride it
    /// covers; and the flight takes the bird off the ground with its wings out.
    ///
    /// <para>Until 2026-09-16 the tests here held a list of rigid pieces — boxes, capsules and ellipsoids — to the same
    /// measurements; William saw the first frames of those and asked for the animals "more smooth and connected", and the
    /// pieces became the skin these tests hold. Every measurement of a posed animal is taken from the skin's own points,
    /// moved by the same blend-by-bone arithmetic the graphics card does (<see cref="AnimalShapes.Skin"/>), and every
    /// judgement of the skin — closed, wound outward, inside its parent — is worked out here from the points and faces
    /// alone, never from the shapes' own frames or radii.</para>
    /// </summary>
    public sealed class AnimalShapesTests
    {
        private static readonly AnimalSpecies Roo = AnimalSpecies.EasternGreyKangaroo;
        private static readonly AnimalSpecies Bird = AnimalSpecies.PiedOystercatcher;
        private static readonly AnimalSpecies[] Both = { Roo, Bird };
        private static readonly byte[] Poses = { AnimalPose.Resting, AnimalPose.Grazing, AnimalPose.Fleeing };

        /// <summary>How fast a shell's radius may change along it, m a metre: a taper of about thirty degrees at the steepest.</summary>
        private const double ProfileSlopeMax = 0.6;

        /// <summary>How far a mitred ring may be stretched: the secant of half a sixty-degree bend.</summary>
        private const double StretchMax = 1.15;

        private static readonly Dictionary<AnimalSpecies, AnimalBody> Bodies = new Dictionary<AnimalSpecies, AnimalBody>();

        private static List<AnimalBone> Built(AnimalSpecies species, byte pose, double timeS = 0.0)
        {
            List<AnimalBone> bones = new List<AnimalBone>();
            AnimalShapes.Build(species, pose, timeS, bones);
            return bones;
        }

        private static AnimalBody Body(AnimalSpecies species)
        {
            if (!Bodies.TryGetValue(species, out AnimalBody body))
            {
                body = AnimalShapes.BodyOf(species);
                Bodies[species] = body;
            }
            return body;
        }

        /// <summary>The skin's points once the bones have taken a pose.</summary>
        private static Double3[] Posed(AnimalSpecies species, byte pose, double timeS = 0.0)
        {
            AnimalBody body = Body(species);
            Double3[] points = new Double3[body.Points.Count];
            AnimalShapes.Skin(body, body.Bind, Built(species, pose, timeS), points);
            return points;
        }

        private static Double3[] Bind(AnimalSpecies species)
        {
            AnimalBody body = Body(species);
            Double3[] points = new Double3[body.Points.Count];
            for (int i = 0; i < points.Length; i++) points[i] = body.Points[i];
            return points;
        }

        // ------------------------------------------------------------------ the skeleton

        [Test]
        public void EachKindIsTheSameBonesInTheSameOrderInEveryPose()
        {
            foreach (AnimalSpecies species in Both)
            {
                List<AnimalBone> resting = Built(species, AnimalPose.Resting);
                Assert.That(resting.Count, Is.EqualTo(AnimalShapes.BoneCount(species)), species.Name + " makes a different number of bones than it says");
                Assert.That(AnimalShapes.Draws(species), Is.True, species.Name + " is drawn and says it is not");
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 1.0; t += 0.13)
                    {
                        List<AnimalBone> bones = Built(species, pose, t);
                        Assert.That(bones.Count, Is.EqualTo(resting.Count), species.Name + " in pose " + pose + " at " + t + " s has a different number of bones");
                        for (int i = 0; i < bones.Count; i++)
                            Assert.That(bones[i].Name, Is.EqualTo(resting[i].Name), species.Name + " bone " + i + " in pose " + pose + " is named differently");
                    }
            }
        }

        [Test]
        public void TheBonesAndShellsAreTheNamedPartsOfEachAnimal()
        {
            List<string> roo = Names(Built(Roo, AnimalPose.Grazing));
            foreach (string named in new[] { "torso", "neck", "head", "tail.base", "tail.tip" })
                Assert.That(roo, Does.Contain(named), "the kangaroo has no " + named);
            foreach (string paired in new[] { "ear", "thigh", "shank", "foot", "arm", "forearm" })
            {
                Assert.That(roo, Does.Contain(paired + ".left"), "the kangaroo has no left " + paired);
                Assert.That(roo, Does.Contain(paired + ".right"), "the kangaroo has no right " + paired);
            }
            List<string> rooSkin = ShellNames(Body(Roo));
            Assert.That(rooSkin, Does.Contain("body"), "the kangaroo has no body");
            foreach (string paired in new[] { "haunch", "shank", "foot", "arm", "ear" })
            {
                Assert.That(rooSkin, Does.Contain(paired + ".left"), "the kangaroo's skin has no left " + paired);
                Assert.That(rooSkin, Does.Contain(paired + ".right"), "the kangaroo's skin has no right " + paired);
            }

            List<string> bird = Names(Built(Bird, AnimalPose.Grazing));
            foreach (string named in new[] { "body", "neck", "head", "bill", "tail" })
                Assert.That(bird, Does.Contain(named), "the oystercatcher has no " + named);
            foreach (string paired in new[] { "wing", "pinion", "leg", "foot" })
            {
                Assert.That(bird, Does.Contain(paired + ".left"), "the oystercatcher has no left " + paired);
                Assert.That(bird, Does.Contain(paired + ".right"), "the oystercatcher has no right " + paired);
            }
            List<string> birdSkin = ShellNames(Body(Bird));
            foreach (string named in new[] { "body", "bill" })
                Assert.That(birdSkin, Does.Contain(named), "the oystercatcher's skin has no " + named);
            foreach (string paired in new[] { "wing", "leg", "foot" })
            {
                Assert.That(birdSkin, Does.Contain(paired + ".left"), "the oystercatcher's skin has no left " + paired);
                Assert.That(birdSkin, Does.Contain(paired + ".right"), "the oystercatcher's skin has no right " + paired);
            }
        }

        /// <summary>A pose moves the joints and never resizes anything: every bone is its stated length in every pose.</summary>
        [Test]
        public void ABoneKeepsItsLengthThroughEveryPose()
        {
            foreach (AnimalSpecies species in Both)
            {
                List<AnimalBone> first = Built(species, AnimalPose.Resting);
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 0.9; t += 0.17)
                    {
                        List<AnimalBone> bones = Built(species, pose, t);
                        for (int i = 0; i < bones.Count; i++)
                        {
                            string what = species.Name + "'s " + bones[i].Name + " in pose " + pose + " at " + t + " s";
                            Assert.That(bones[i].LengthM, Is.EqualTo(first[i].LengthM).Within(1e-9), what + " changed length");
                            Assert.That((bones[i].End - bones[i].Joint).Length, Is.EqualTo(bones[i].LengthM).Within(1e-9), what + " does not reach its own end");
                        }
                    }
            }
            List<AnimalBone> roo = Built(Roo, AnimalPose.Resting);
            Assert.That(Find(roo, "torso").LengthM, Is.EqualTo(AnimalShapes.TorsoM).Within(1e-12), "the torso is not the stated length");
            Assert.That(Find(roo, "tail.base").LengthM + Find(roo, "tail.tip").LengthM, Is.EqualTo(AnimalShapes.TailBaseM + AnimalShapes.TailTipM).Within(1e-12), "the tail is not the stated length");
            Assert.That(Find(roo, "foot.left").LengthM, Is.EqualTo(AnimalShapes.FootM).Within(1e-12), "the foot is not the stated length");
            List<AnimalBone> bird = Built(Bird, AnimalPose.Resting);
            Assert.That(Find(bird, "bill").LengthM, Is.EqualTo(AnimalShapes.BirdBillM).Within(1e-12), "the bill is not the stated length");
            Assert.That(Find(bird, "wing.left").LengthM + Find(bird, "pinion.left").LengthM, Is.EqualTo(AnimalShapes.BirdWingM + AnimalShapes.BirdPinionM).Within(1e-12), "the wing is not the stated length");
        }

        /// <summary>Every bone has a real place and an honest frame: three unit axes, square to each other, Across × Deep giving Along.</summary>
        [Test]
        public void EveryBoneHasARealPlaceAndAnHonestFrame()
        {
            foreach (AnimalSpecies species in Both)
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 1.1; t += 0.11)
                        foreach (AnimalBone bone in Built(species, pose, t))
                        {
                            string what = species.Name + "'s " + bone.Name + " in pose " + pose + " at " + t + " s";
                            foreach (double number in new[] { bone.Joint.X, bone.Joint.Y, bone.Joint.Z, bone.LengthM })
                                Assert.That(double.IsFinite(number), Is.True, what + " is placed at a number that is not one");
                            Assert.That(bone.LengthM, Is.GreaterThan(0.0).And.LessThan(3.0), what + " has a length that is nought or absurd");
                            Assert.That(bone.Along.Length, Is.EqualTo(1.0).Within(1e-9), what + "'s Along is not a unit");
                            Assert.That(bone.Across.Length, Is.EqualTo(1.0).Within(1e-9), what + "'s Across is not a unit");
                            Assert.That(bone.Deep.Length, Is.EqualTo(1.0).Within(1e-9), what + "'s Deep is not a unit");
                            Assert.That(Double3.Dot(bone.Along, bone.Across), Is.EqualTo(0.0).Within(1e-9), what + "'s Across is not square to it");
                            Assert.That(Double3.Dot(bone.Along, bone.Deep), Is.EqualTo(0.0).Within(1e-9), what + "'s Deep is not square to it");
                            Assert.That((Double3.Cross(bone.Across, bone.Deep) - bone.Along).Length, Is.EqualTo(0.0).Within(1e-9), what + "'s frame is not a proper one");
                        }
        }

        [Test]
        public void TheTwoSidesOfAnAnimalMirrorEachOther()
        {
            foreach (AnimalSpecies species in Both)
            {
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 0.5; t += 0.07)
                    {
                        List<AnimalBone> bones = Built(species, pose, t);
                        int pairs = 0;
                        foreach (AnimalBone left in bones)
                        {
                            if (!left.Name.EndsWith(".left", StringComparison.Ordinal)) continue;
                            AnimalBone right = Find(bones, left.Name.Substring(0, left.Name.Length - 5) + ".right");
                            string what = species.Name + "'s " + left.Name + " and " + right.Name + " in pose " + pose + " at " + t + " s";
                            pairs++;
                            Assert.That(right.LengthM, Is.EqualTo(left.LengthM).Within(1e-9), what + ": different lengths");
                            Assert.That((right.Joint - Mirror(left.Joint)).Length, Is.EqualTo(0.0).Within(1e-9), what + ": not mirrored across the animal");
                            Assert.That((right.Along - Mirror(left.Along)).Length, Is.EqualTo(0.0).Within(1e-9), what + ": pointed differently");
                            Assert.That(left.Joint.X, Is.Not.EqualTo(0.0), what + ": a paired bone stands on the middle line");
                        }
                        Assert.That(pairs, Is.GreaterThanOrEqualTo(4), species.Name + " has fewer paired bones than it should");
                    }

                // The skin too: every point of a left shell has its mirror among the right shell's points.
                AnimalBody body = Body(species);
                Double3[] points = Bind(species);
                int shells = 0;
                foreach (AnimalShell left in body.Shells)
                {
                    if (!left.Name.EndsWith(".left", StringComparison.Ordinal)) continue;
                    AnimalShell right = Shell(body, left.Name.Substring(0, left.Name.Length - 5) + ".right");
                    shells++;
                    List<Double3> rightPoints = PointsOf(right, points);
                    foreach (Double3 p in PointsOf(left, points))
                    {
                        Double3 wanted = Mirror(p);
                        double nearest = double.PositiveInfinity;
                        foreach (Double3 q in rightPoints) nearest = Math.Min(nearest, (q - wanted).Length);
                        Assert.That(nearest, Is.LessThan(1e-9), species.Name + "'s " + left.Name + " has a point at " + p + " with no mirror on the " + right.Name);
                    }
                    Assert.That(rightPoints.Count, Is.EqualTo(PointsOf(left, points).Count), species.Name + "'s two " + left.Name.Substring(0, left.Name.Length - 5) + " shells have different numbers of points");
                }
                Assert.That(shells, Is.GreaterThanOrEqualTo(3), species.Name + "'s skin has fewer paired shells than it should");
            }
        }

        // ------------------------------------------------------------------ one smooth, connected skin

        /// <summary>Every shell is one closed surface: each edge is shared by exactly two faces, wound the opposite ways, so no gap or seam can open anywhere on it.</summary>
        [Test]
        public void EveryShellIsClosedWithNoOpenEdge()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                foreach (AnimalShell shell in body.Shells)
                {
                    Dictionary<long, int> forward = new Dictionary<long, int>();
                    for (int f = shell.FirstFace; f < shell.FirstFace + shell.FaceCount; f++)
                    {
                        AnimalFace face = body.Faces[f];
                        foreach ((int a, int b) in new[] { (face.A, face.B), (face.B, face.C), (face.C, face.A) })
                        {
                            long key = ((long)a << 32) | (uint)b;
                            forward.TryGetValue(key, out int seen);
                            forward[key] = seen + 1;
                        }
                    }
                    foreach (KeyValuePair<long, int> edge in forward)
                    {
                        int a = (int)(edge.Key >> 32), b = (int)(edge.Key & 0xFFFFFFFF);
                        string what = species.Name + "'s " + shell.Name + " edge " + a + "-" + b;
                        Assert.That(edge.Value, Is.EqualTo(1), what + " is walked the same way by two faces");
                        Assert.That(forward.TryGetValue(((long)b << 32) | (uint)a, out int back) && back == 1, Is.True, what + " is open: no face walks it back");
                    }
                    Assert.That(shell.FaceCount, Is.GreaterThan(0), species.Name + "'s " + shell.Name + " has no faces");
                }
            }
        }

        /// <summary>
        /// Adjacent rings share their points: every point of a ring is used both by the faces of the band before it and by
        /// those of the band after it (or the fan to the pole at an end), so the skin runs unbroken from ring to ring.
        /// </summary>
        [Test]
        public void AdjacentRingsShareTheirPoints()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                foreach (AnimalShell shell in body.Shells)
                {
                    int rings = shell.Stations.Count;
                    // Which ring each of the shell's points belongs to; −1 for the poles.
                    Dictionary<int, int> ringOf = new Dictionary<int, int> { [shell.StartPole] = -1, [shell.EndPole] = -1 };
                    for (int r = 0; r < rings; r++)
                        for (int k = 0; k < shell.Sides; k++)
                            ringOf[shell.Stations[r].FirstPoint + k] = r;
                    Assert.That(ringOf.Count, Is.EqualTo(rings * shell.Sides + 2), species.Name + "'s " + shell.Name + " has points that are not on its rings");

                    HashSet<int>[] before = new HashSet<int>[rings], after = new HashSet<int>[rings];
                    for (int r = 0; r < rings; r++)
                    {
                        before[r] = new HashSet<int>();
                        after[r] = new HashSet<int>();
                    }
                    for (int f = shell.FirstFace; f < shell.FirstFace + shell.FaceCount; f++)
                    {
                        AnimalFace face = body.Faces[f];
                        int[] corners = { face.A, face.B, face.C };
                        foreach (int p in corners)
                        {
                            Assert.That(ringOf.ContainsKey(p), Is.True, species.Name + "'s " + shell.Name + " has a face on a point of another shell");
                            int r = ringOf[p];
                            if (r < 0) continue;
                            foreach (int q in corners)
                            {
                                int other = ringOf[q];
                                if (q == shell.StartPole || other == r - 1) before[r].Add(p);
                                if (q == shell.EndPole || other == r + 1) after[r].Add(p);
                            }
                        }
                    }
                    for (int r = 0; r < rings; r++)
                    {
                        Assert.That(before[r].Count, Is.EqualTo(shell.Sides), species.Name + "'s " + shell.Name + " ring " + r + " has points no face joins to the ring before it");
                        Assert.That(after[r].Count, Is.EqualTo(shell.Sides), species.Name + "'s " + shell.Name + " ring " + r + " has points no face joins to the ring after it");
                    }
                }
            }
        }

        /// <summary>
        /// The radius changes gently along a shell: no step steeper than the stated slope between one ring and the next,
        /// caps aside (a cap is round by construction); and no mitred ring stretched past a sixty-degree bend's. The old
        /// tail's two capsules met with a step of four centimetres in radius, and that step is what this refuses.
        /// </summary>
        [Test]
        public void TheRadiusProfileHasNoSteps()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                Double3[] points = Bind(species);
                foreach (AnimalShell shell in body.Shells)
                {
                    int steps = 0;
                    for (int r = 0; r + 1 < shell.Stations.Count; r++)
                    {
                        AnimalStation a = shell.Stations[r], b = shell.Stations[r + 1];
                        Assert.That(a.Stretch, Is.GreaterThanOrEqualTo(1.0).And.LessThanOrEqualTo(StretchMax), species.Name + "'s " + shell.Name + " ring " + r + " is stretched " + a.Stretch.ToString("0.000") + " times");
                        if (a.Cap || b.Cap) continue;
                        // Measured from the rings' own points: how wide each is, and how far apart they stand.
                        double ra = MeanRadius(shell, r, points), rb = MeanRadius(shell, r + 1, points);
                        double apart = (Centre(shell, r + 1, points) - Centre(shell, r, points)).Length;
                        Assert.That(apart, Is.GreaterThan(0.0), species.Name + "'s " + shell.Name + " has two rings in one place");
                        double slope = Math.Abs(rb - ra) / apart;
                        Assert.That(slope, Is.LessThanOrEqualTo(ProfileSlopeMax), species.Name + "'s " + shell.Name + " steps from " + ra.ToString("0.000") + " to " + rb.ToString("0.000") + " m in " + apart.ToString("0.000") + " m between rings " + r + " and " + (r + 1));
                        steps++;
                    }
                    Assert.That(steps, Is.GreaterThanOrEqualTo(1), species.Name + "'s " + shell.Name + " has no profile to speak of");
                }
            }
        }

        /// <summary>
        /// Every face looks outward in every pose: each shell encloses a positive volume, every band's faces point away from
        /// the band's own axis, and every ring keeps its turning sense — so no ring is turned inside out by a bend and no
        /// face is drawn from the wrong side.
        /// </summary>
        [Test]
        public void EveryFaceIsWoundOutwardInEveryPose()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                foreach ((byte pose, double t) in Moments(species))
                {
                    Double3[] points = Posed(species, pose, t);
                    foreach (AnimalShell shell in body.Shells)
                    {
                        string where = species.Name + "'s " + shell.Name + " in pose " + pose + " at " + t.ToString("0.000") + " s";
                        double volume = 0.0;
                        for (int f = shell.FirstFace; f < shell.FirstFace + shell.FaceCount; f++)
                        {
                            AnimalFace face = body.Faces[f];
                            volume += Double3.Dot(points[face.A], Double3.Cross(points[face.B], points[face.C])) / 6.0;
                        }
                        Assert.That(volume, Is.GreaterThan(1e-8), where + " encloses no volume, or is wound inward");

                        int rings = shell.Stations.Count;
                        Double3[] centres = new Double3[rings];
                        for (int r = 0; r < rings; r++) centres[r] = Centre(shell, r, points);
                        for (int r = 0; r < rings; r++)
                        {
                            Double3 axis = (r + 1 < rings ? centres[r + 1] : points[shell.EndPole]) - (r > 0 ? centres[r - 1] : points[shell.StartPole]);
                            Assert.That(Turning(shell, r, points, centres[r], axis), Is.GreaterThan(0.0), where + " has ring " + r + " turned inside out");
                        }
                        for (int f = shell.FirstFace; f < shell.FirstFace + shell.FaceCount; f++)
                        {
                            AnimalFace face = body.Faces[f];
                            Double3 a = points[face.A], b = points[face.B], c = points[face.C];
                            Double3 normal = Double3.Cross(b - a, c - a);
                            Double3 centroid = (a + b + c) / 3.0;
                            Assert.That(Double3.Dot(normal, centroid - Inside(shell, face, points, centres)), Is.GreaterThan(0.0), where + " has face " + (f - shell.FirstFace) + " looking inward");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A limb is rooted inside its parent in every pose: its root pole and the middle of its first ring lie inside the
        /// parent shell, worked out here against the parent's own posed rings, so the join is hidden however the joint
        /// turns. The same holds of a ball-jointed shell, whose root lies within the parent's end.
        /// </summary>
        [Test]
        public void EveryLimbIsRootedInsideItsParentInEveryPose()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                int rooted = 0;
                foreach ((byte pose, double t) in Moments(species))
                {
                    Double3[] points = Posed(species, pose, t);
                    foreach (AnimalShell shell in body.Shells)
                    {
                        if (shell.Parent == null) continue;
                        AnimalShell parent = Shell(body, shell.Parent);
                        string where = species.Name + "'s " + shell.Name + " in pose " + pose + " at " + t.ToString("0.000") + " s";
                        rooted++;
                        Assert.That(IsInside(body, parent, points, points[shell.StartPole]), Is.True, where + " has its root pole outside the " + parent.Name);
                        Assert.That(IsInside(body, parent, points, Centre(shell, AnimalShapes.CapRings, points)), Is.True, where + " has the middle of its first ring outside the " + parent.Name);
                    }
                }
                Assert.That(rooted, Is.GreaterThan(0), species.Name + " has no limb rooted in anything");
                Assert.That(Shell(body, "body").Parent, Is.Null, species.Name + "'s body is rooted in something");
            }
        }

        /// <summary>
        /// A sharp joint is one ball inside another: the child's first ring and the parent's last share their middle exactly
        /// in every pose, the child's root is no wider than the parent's end and within a fifth of it, and its cap no longer,
        /// so a knee folded flat reads as one rounded knuckle with no gap.
        /// </summary>
        [Test]
        public void ASharpJointIsABallSharedByBothBones()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                int balls = 0;
                foreach ((byte pose, double t) in Moments(species))
                {
                    Double3[] points = Posed(species, pose, t);
                    foreach (AnimalShell shell in body.Shells)
                    {
                        if (!shell.BallJoint) continue;
                        AnimalShell parent = Shell(body, shell.Parent);
                        string where = species.Name + "'s " + shell.Name + " on the " + parent.Name + " in pose " + pose + " at " + t.ToString("0.000") + " s";
                        balls++;
                        int childRing = AnimalShapes.CapRings, parentRing = parent.Stations.Count - 1 - AnimalShapes.CapRings;
                        Assert.That((Centre(shell, childRing, points) - Centre(parent, parentRing, points)).Length, Is.LessThan(1e-6), where + ": the two balls have different middles");
                        double child = MeanRadius(shell, childRing, points), end = MeanRadius(parent, parentRing, points);
                        Assert.That(child, Is.LessThanOrEqualTo(end + 1e-9), where + ": the root is wider than the end it sits in");
                        Assert.That(child, Is.GreaterThanOrEqualTo(0.8 * end), where + ": the root is more than a fifth narrower than the end it sits in, a visible step");
                        Assert.That(shell.StartCapM, Is.LessThanOrEqualTo(parent.EndCapM), where + ": the root's cap is longer than the end's");
                    }
                }
                Assert.That(balls, Is.GreaterThan(0), species.Name + " has no ball joint");
            }
        }

        /// <summary>
        /// The corners the graphics card is handed: every face leads with a corner of its own carrying the face's plane and
        /// colour, its other corners are borrowed from the points they stand at, and the sharing is real — about as many
        /// corners as faces, not three times as many.
        /// </summary>
        [Test]
        public void EveryFaceLeadsWithACornerCarryingItsOwnPlaneAndColour()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                Assert.That(body.Triangles.Length, Is.EqualTo(3 * body.Faces.Count), species.Name + " hands over a different number of triangles than it has faces");
                Assert.That(body.VertexCount, Is.LessThanOrEqualTo(1.2 * body.Faces.Count), species.Name + " has " + body.VertexCount + " corners for " + body.Faces.Count + " faces: the corners are not shared");
                Assert.That(body.VertexCount, Is.GreaterThanOrEqualTo(body.Faces.Count), species.Name + " has fewer corners than faces, so some face leads with another's");
                bool[] led = new bool[body.Faces.Count];
                for (int f = 0; f < body.Faces.Count; f++)
                {
                    AnimalFace face = body.Faces[f];
                    int lead = body.Triangles[3 * f];
                    Assert.That(body.VertexFace[lead], Is.EqualTo(f), species.Name + " face " + f + " leads with a corner that leads face " + body.VertexFace[lead]);
                    Assert.That(led[f], Is.False, species.Name + " face " + f + " is led twice");
                    led[f] = true;
                    Double3 plane = Double3.Cross(body.Points[face.B] - body.Points[face.A], body.Points[face.C] - body.Points[face.A]).Normalized;
                    Assert.That((body.VertexNormal[lead] - plane).Length, Is.LessThan(1e-9), species.Name + " face " + f + "'s leading corner carries another plane");
                    Assert.That(body.VertexColour[lead].R, Is.EqualTo(face.Colour.R).Within(1e-6), species.Name + " face " + f + "'s leading corner wears another colour");
                    Assert.That(body.VertexColour[lead].G, Is.EqualTo(face.Colour.G).Within(1e-6), species.Name + " face " + f + "'s leading corner wears another colour");
                    Assert.That(body.VertexColour[lead].B, Is.EqualTo(face.Colour.B).Within(1e-6), species.Name + " face " + f + "'s leading corner wears another colour");
                    int[] expected = { face.A, face.B, face.C };
                    // The three corners stand at the face's three points, in the face's winding, starting anywhere.
                    int start = Array.IndexOf(expected, body.VertexPoint[lead]);
                    Assert.That(start, Is.GreaterThanOrEqualTo(0), species.Name + " face " + f + " leads from a point it does not have");
                    for (int k = 0; k < 3; k++)
                        Assert.That(body.VertexPoint[body.Triangles[3 * f + k]], Is.EqualTo(expected[(start + k) % 3]), species.Name + " face " + f + "'s corners do not follow its winding");
                }
                for (int v = 0; v < body.VertexCount; v++)
                {
                    Assert.That(body.VertexPoint[v], Is.InRange(0, body.Points.Count - 1), species.Name + " corner " + v + " stands at no point");
                    Assert.That(body.VertexNormal[v].Length, Is.EqualTo(1.0).Within(1e-9), species.Name + " corner " + v + " carries a plane that is not a unit");
                }
            }
        }

        /// <summary>The skin follows its bones as the graphics card will move it: a shell riding one bone keeps its shape exactly, and a point at a joint stays at the joint.</summary>
        [Test]
        public void TheSkinFollowsItsBonesRigidlyWhereItRidesOne()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody body = Body(species);
                Double3[] bind = Bind(species);
                int rigid = 0;
                foreach ((byte pose, double t) in Moments(species))
                {
                    Double3[] points = Posed(species, pose, t);
                    List<AnimalBone> bones = Built(species, pose, t);
                    foreach (AnimalShell shell in body.Shells)
                    {
                        if (!RidesOneBone(body, shell, out int bone)) continue;
                        rigid++;
                        List<int> indices = IndicesOf(shell);
                        for (int i = 0; i < indices.Count; i += 7)
                            for (int j = i + 1; j < indices.Count; j += 11)
                            {
                                double was = (bind[indices[j]] - bind[indices[i]]).Length, now = (points[indices[j]] - points[indices[i]]).Length;
                                Assert.That(now, Is.EqualTo(was).Within(1e-9), species.Name + "'s " + shell.Name + " changed shape in pose " + pose + " at " + t.ToString("0.000") + " s");
                            }
                        // The root's middle sits on the bone's line in the bind pose, and the posed root sits on the posed bone's line.
                        Double3 root = Centre(shell, AnimalShapes.CapRings, points);
                        Double3 offset = root - bones[bone].Joint;
                        double along = Double3.Dot(offset, bones[bone].Along);
                        Assert.That((offset - bones[bone].Along * along).Length, Is.LessThan(0.06), species.Name + "'s " + shell.Name + " has left its bone in pose " + pose);
                    }
                }
                Assert.That(rigid, Is.GreaterThan(0), species.Name + " has no shell riding one bone");
            }
        }

        /// <summary>Two clients grow the same animal: the skin is the same every time it is built.</summary>
        [Test]
        public void ABuiltSkinIsTheSameEveryTime()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalBody first = AnimalShapes.BodyOf(species), second = AnimalShapes.BodyOf(species);
                Assert.That(second.Points.Count, Is.EqualTo(first.Points.Count), species.Name + " grew a different number of points");
                Assert.That(second.Faces.Count, Is.EqualTo(first.Faces.Count), species.Name + " grew a different number of faces");
                for (int i = 0; i < first.Points.Count; i++)
                    Assert.That((second.Points[i] - first.Points[i]).Length, Is.EqualTo(0.0), species.Name + " grew point " + i + " somewhere else");
                for (int f = 0; f < first.Faces.Count; f++)
                {
                    Assert.That(second.Faces[f].A, Is.EqualTo(first.Faces[f].A), species.Name + " grew face " + f + " over other points");
                    Assert.That(second.Faces[f].B, Is.EqualTo(first.Faces[f].B), species.Name + " grew face " + f + " over other points");
                    Assert.That(second.Faces[f].C, Is.EqualTo(first.Faces[f].C), species.Name + " grew face " + f + " over other points");
                    Assert.That(second.Faces[f].Colour.R, Is.EqualTo(first.Faces[f].Colour.R), species.Name + " coloured face " + f + " differently");
                }
                Assert.That(second.Triangles, Is.EqualTo(first.Triangles), species.Name + " handed over different corners");
            }
        }

        /// <summary>The box a kind never leaves, which the renderer is culled by: every point of the skin stays inside it in every pose at every moment.</summary>
        [Test]
        public void TheSkinNeverLeavesTheBoxItIsCulledBy()
        {
            foreach (AnimalSpecies species in Both)
            {
                AnimalShapes.ReachOf(species, out Double3 low, out Double3 high);
                Assert.That(high.X - low.X, Is.LessThan(2.0), species.Name + "'s box is absurdly wide");
                Assert.That(high.Z - low.Z, Is.LessThan(4.0), species.Name + "'s box is absurdly long");
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 2.1; t += 0.0311)
                        foreach (Double3 p in Posed(species, pose, t))
                        {
                            string what = species.Name + " in pose " + pose + " at " + t.ToString("0.000") + " s has a point at " + p;
                            Assert.That(p.X, Is.GreaterThanOrEqualTo(low.X).And.LessThanOrEqualTo(high.X), what + ", outside its box");
                            Assert.That(p.Y, Is.GreaterThanOrEqualTo(low.Y).And.LessThanOrEqualTo(high.Y), what + ", outside its box");
                            Assert.That(p.Z, Is.GreaterThanOrEqualTo(low.Z).And.LessThanOrEqualTo(high.Z), what + ", outside its box");
                        }
            }
        }

        /// <summary>Colours plain: a black-and-white oystercatcher, black above and white below, with an orange-red bill and pink legs; a kangaroo paler underneath.</summary>
        [Test]
        public void TheColoursAreTheSpeciesOwn()
        {
            AnimalBody bird = Body(Bird);
            Double3[] birdPoints = Bind(Bird);
            AnimalShell body = Shell(bird, "body");
            AnimalBone birdSpine = Find(Built(Bird, AnimalPose.Resting), "body");
            int dark = 0, light = 0;
            for (int f = body.FirstFace; f < body.FirstFace + body.FaceCount; f++)
            {
                AnimalFace face = bird.Faces[f];
                Double3 centroid = (birdPoints[face.A] + birdPoints[face.B] + birdPoints[face.C]) / 3.0;
                // Over the middle of the body: well above the spine's line is black, well below it is white. The height is
                // taken from the spine where the face is, and the lines sit within the depth of a wader's own shallow
                // body (until 2026-09-18 it was taken from the rump with a lower line no face of a body this shallow
                // reached, so the belly was never judged).
                if (centroid.Z < -0.05 || centroid.Z > 0.05) continue;
                double up = centroid.Y - NearestOn(birdSpine, centroid).Y;
                if (up > 0.03)
                {
                    dark++;
                    Assert.That(Brightness(face.Colour), Is.LessThan(0.2), "the oystercatcher's back is not black at " + centroid);
                }
                else if (up < -0.04)
                {
                    light++;
                    Assert.That(Brightness(face.Colour), Is.GreaterThan(0.8), "the oystercatcher's belly is not white at " + centroid);
                }
            }
            Assert.That(dark, Is.GreaterThan(4), "no face of the oystercatcher's back was judged");
            Assert.That(light, Is.GreaterThan(4), "no face of the oystercatcher's belly was judged");
            foreach (AnimalFace face in FacesOf(bird, "bill"))
                Assert.That(face.Colour.R > 0.75 && face.Colour.G < 0.45 && face.Colour.B < 0.2, Is.True, "the bill is not orange-red");
            foreach (AnimalFace face in FacesOf(bird, "leg.left"))
                Assert.That(face.Colour.R > 0.8 && face.Colour.G > 0.5 && face.Colour.G < 0.75 && face.Colour.B > 0.5, Is.True, "the legs are not pink");

            AnimalBody roo = Body(Roo);
            Double3[] rooPoints = Bind(Roo);
            AnimalShell rooBody = Shell(roo, "body");
            AnimalBone rooSpine = Find(Built(Roo, AnimalPose.Resting), "torso");
            double above = 0.0, below = 0.0;
            int aboveCount = 0, belowCount = 0;
            for (int f = rooBody.FirstFace; f < rooBody.FirstFace + rooBody.FaceCount; f++)
            {
                AnimalFace face = roo.Faces[f];
                Double3 centroid = (rooPoints[face.A] + rooPoints[face.B] + rooPoints[face.C]) / 3.0;
                if (centroid.Z < -0.25 || centroid.Z > 0.2) continue;
                double up = centroid.Y - NearestOn(rooSpine, centroid).Y;
                if (up > 0.1)
                {
                    above += Brightness(face.Colour);
                    aboveCount++;
                }
                else if (up < -0.15)
                {
                    below += Brightness(face.Colour);
                    belowCount++;
                }
            }
            Assert.That(aboveCount, Is.GreaterThan(4).And.LessThan(rooBody.FaceCount), "no face of the kangaroo's back was judged");
            Assert.That(belowCount, Is.GreaterThan(4), "no face of the kangaroo's belly was judged");
            Assert.That(below / belowCount, Is.GreaterThan(above / aboveCount + 0.1), "the kangaroo is not paler underneath");
        }

        // ------------------------------------------------------------------ the kangaroo against the measurements

        /// <summary>
        /// The tail is about a metre (the research of 2026-09-13), and is the one measurement of the animal that is read
        /// straight off the bones rather than out of a pose.
        /// </summary>
        [Test]
        public void TheKangaroosTailIsAboutAMetre()
        {
            double tail = AnimalShapes.TailBaseM + AnimalShapes.TailTipM;
            Assert.That(tail, Is.GreaterThanOrEqualTo(0.90).And.LessThanOrEqualTo(1.15), "the kangaroo's tail is " + tail.ToString("0.00") + " m");
        }

        /// <summary>
        /// An eastern grey stands about 1.3 m hunched and reaches 1.6 to 1.8 m upright, and is about 2 m from nose to tail
        /// tip (a head and body of 0.9 to 1.3 m and the metre of tail). Grazing it is lower than hunched, lying up lower
        /// again, and at the top of a hop it is up among the standing heights. Measured on the skin's own points.
        /// </summary>
        [Test]
        public void TheKangarooKeepsToItsPublishedSize()
        {
            Box grazing = Bound(Posed(Roo, AnimalPose.Grazing));
            Assert.That(grazing.MaxY, Is.GreaterThanOrEqualTo(0.80).And.LessThanOrEqualTo(1.30), "a grazing kangaroo stands " + grazing.MaxY.ToString("0.00") + " m");
            double length = grazing.MaxZ - grazing.MinZ;
            Assert.That(length, Is.GreaterThanOrEqualTo(1.70).And.LessThanOrEqualTo(2.50), "a kangaroo is " + length.ToString("0.00") + " m nose to tail");
            double width = grazing.MaxX - grazing.MinX;
            Assert.That(width, Is.GreaterThanOrEqualTo(0.30).And.LessThanOrEqualTo(0.75), "a kangaroo is " + width.ToString("0.00") + " m across");

            Box resting = Bound(Posed(Roo, AnimalPose.Resting));
            Assert.That(resting.MaxY, Is.GreaterThanOrEqualTo(0.45).And.LessThanOrEqualTo(0.95), "a kangaroo lying up stands " + resting.MaxY.ToString("0.00") + " m");
            Assert.That(resting.MaxY, Is.LessThan(grazing.MaxY), "a kangaroo lying up is no lower than one grazing");

            // The top of the hop, half a stride after a touchdown.
            double hop = AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs);
            Box top = Bound(Posed(Roo, AnimalPose.Fleeing, 0.5 * hop));
            Assert.That(top.MaxY, Is.GreaterThanOrEqualTo(1.20).And.LessThanOrEqualTo(1.80), "a bounding kangaroo reaches " + top.MaxY.ToString("0.00") + " m");
        }

        /// <summary>
        /// Every pose but the middle of a hop stands on the ground: no animal floats, and none is buried, the skin's lowest
        /// point within two centimetres of the ground it is given (until 2026-09-18 five below and six above were allowed,
        /// and the bird stood a finger's width off the sand while the kangaroo's heels stood in the ground).
        /// </summary>
        [Test]
        public void TheAnimalsStandOnTheGroundTheyAreGiven()
        {
            foreach (AnimalSpecies species in Both)
                foreach (byte pose in new[] { AnimalPose.Resting, AnimalPose.Grazing })
                    for (double t = 0.0; t < 3.3; t += 0.31)
                    {
                        Box box = Bound(Posed(species, pose, t));
                        Assert.That(box.MinY, Is.GreaterThanOrEqualTo(-0.02).And.LessThanOrEqualTo(0.02),
                                    species.Name + " in pose " + pose + " has its lowest point at " + box.MinY.ToString("0.000") + " m");
                    }

            // A hopping kangaroo touches down once a stride and is off the ground the rest of it.
            double hop = AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs);
            Assert.That(Bound(Posed(Roo, AnimalPose.Fleeing, 0.0)).MinY, Is.LessThanOrEqualTo(0.02), "a hop never touches the ground");
            Assert.That(Bound(Posed(Roo, AnimalPose.Fleeing, 0.5 * hop)).MinY, Is.GreaterThan(0.2), "a hop never leaves the ground");
        }

        // ------------------------------------------------------------------ the oystercatcher against the measurements

        /// <summary>
        /// A pied oystercatcher is 45 to 50 cm from the tip of the bill to the tip of the tail, stands about a third of a
        /// metre, and spans 80 to 86 cm with its wings out. Measured on the skin's own points.
        /// </summary>
        [Test]
        public void TheOystercatcherKeepsToItsPublishedSize()
        {
            AnimalBody bird = Body(Bird);
            Double3[] standing = Posed(Bird, AnimalPose.Resting);
            Box bill = Bound(PointsOf(Shell(bird, "bill"), standing)), body = Bound(PointsOf(Shell(bird, "body"), standing));
            double length = bill.MaxZ - body.MinZ;
            Assert.That(length, Is.GreaterThanOrEqualTo(0.44).And.LessThanOrEqualTo(0.53), "an oystercatcher is " + length.ToString("0.000") + " m bill to tail");

            Box whole = Bound(standing);
            Assert.That(whole.MaxY, Is.GreaterThanOrEqualTo(0.25).And.LessThanOrEqualTo(0.45), "a standing oystercatcher is " + whole.MaxY.ToString("0.000") + " m tall");
            double folded = whole.MaxX - whole.MinX;
            Assert.That(folded, Is.LessThanOrEqualTo(0.26), "a standing oystercatcher is " + folded.ToString("0.000") + " m across with its wings folded");

            // The wings are widest as they pass level, which is where a beat starts.
            Box flying = Bound(Posed(Bird, AnimalPose.Fleeing, 0.0));
            double span = flying.MaxX - flying.MinX;
            Assert.That(span, Is.GreaterThanOrEqualTo(0.78).And.LessThanOrEqualTo(0.90), "an oystercatcher spans " + span.ToString("0.000") + " m");
            Assert.That(span, Is.GreaterThan(2.5 * folded), "the wings are hardly wider out than folded");
        }

        /// <summary>A startled pair goes up: the body is a metre or two off the sand through the whole of a wingbeat.</summary>
        [Test]
        public void TheFlightTakesTheBirdOffTheGround()
        {
            for (double t = 0.0; t < 2.0 * AnimalShapes.BirdWingbeatSeconds; t += 0.01)
            {
                AnimalBone body = Find(Built(Bird, AnimalPose.Fleeing, t), "body");
                Assert.That(body.Joint.Y, Is.GreaterThanOrEqualTo(1.0).And.LessThanOrEqualTo(2.0), "the bird's body is " + body.Joint.Y.ToString("0.00") + " m up at " + t.ToString("0.000") + " s");
                Assert.That(Bound(Posed(Bird, AnimalPose.Fleeing, t)).MinY, Is.GreaterThan(0.8), "something of the flying bird still touches the ground at " + t.ToString("0.000") + " s");
            }
        }

        /// <summary>The wings beat: the wingtip passes well above and below the shoulder within one beat, and the beat repeats.</summary>
        [Test]
        public void TheWingsBeatOnceAWingbeat()
        {
            double beat = AnimalShapes.BirdWingbeatSeconds;
            double highest = double.NegativeInfinity, lowest = double.PositiveInfinity;
            for (double t = 0.0; t < beat; t += beat / 64.0)
            {
                double tip = Find(Built(Bird, AnimalPose.Fleeing, t), "pinion.right").End.Y;
                highest = Math.Max(highest, tip);
                lowest = Math.Min(lowest, tip);
            }
            Assert.That(highest - lowest, Is.GreaterThan(0.25), "the wingtip moves " + (highest - lowest).ToString("0.000") + " m in a beat");
            AnimalBone now = Find(Built(Bird, AnimalPose.Fleeing, 0.37), "pinion.right");
            AnimalBone later = Find(Built(Bird, AnimalPose.Fleeing, 0.37 + beat), "pinion.right");
            Assert.That(later.End.Y, Is.EqualTo(now.End.Y).Within(1e-6), "a wingbeat does not come round again");
        }

        // ------------------------------------------------------------------ the hop

        /// <summary>
        /// The cadence is the stride divided by the pace, so a mob fleeing at the 7 m/s of <see cref="AnimalFlightRules"/>
        /// hops at the measured 0.45 s and one going slower hops slower rather than shorter.
        /// </summary>
        [Test]
        public void TheHopsCadenceComesFromTheFleeSpeedAndTheStride()
        {
            Assert.That(AnimalShapes.HopStrideM, Is.GreaterThanOrEqualTo(2.5).And.LessThanOrEqualTo(4.0),
                        "a hop covers " + AnimalShapes.HopStrideM.ToString("0.00") + " m, which is not several metres");
            double seconds = AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs);
            Assert.That(seconds, Is.GreaterThanOrEqualTo(0.42).And.LessThanOrEqualTo(0.48), "a hop at 7 m/s takes " + seconds.ToString("0.000") + " s");
            Assert.That(AnimalShapes.HopSeconds(2.0 * AnimalFlightRules.KangarooRunMs), Is.EqualTo(0.5 * seconds).Within(1e-9), "twice the pace does not halve the hop");
        }

        /// <summary>The hop lifts the body by its stated rise and comes round again on its own cadence, and the feet swing through it.</summary>
        [Test]
        public void TheHopRaisesAndLowersTheBody()
        {
            double hop = AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs);
            double down = Find(Built(Roo, AnimalPose.Fleeing, 0.0), "torso").Joint.Y;
            double up = Find(Built(Roo, AnimalPose.Fleeing, 0.5 * hop), "torso").Joint.Y;
            Assert.That(up - down, Is.EqualTo(AnimalShapes.HopLiftM).Within(1e-6), "the body rises " + (up - down).ToString("0.000") + " m in a hop");
            Assert.That(Find(Built(Roo, AnimalPose.Fleeing, 0.3 * hop + hop), "torso").Joint.Y,
                        Is.EqualTo(Find(Built(Roo, AnimalPose.Fleeing, 0.3 * hop), "torso").Joint.Y).Within(1e-6), "a hop does not come round again");
            // The toes come forward under the body to meet the ground and trail behind at the top of the bound.
            double touchdown = Find(Built(Roo, AnimalPose.Fleeing, 0.0), "foot.right").End.Z;
            double tucked = Find(Built(Roo, AnimalPose.Fleeing, 0.5 * hop), "foot.right").End.Z;
            Assert.That(touchdown, Is.GreaterThan(tucked + 0.15), "the feet do not swing through the hop");
        }

        /// <summary>A grazing animal is not a statue: its head lifts and falls, and nothing else about it moves.</summary>
        [Test]
        public void AGrazingAnimalLiftsItsHead()
        {
            double low = Find(Built(Roo, AnimalPose.Grazing, 0.75 * AnimalShapes.GrazeBobSeconds), "head").Joint.Y;
            double high = Find(Built(Roo, AnimalPose.Grazing, 0.25 * AnimalShapes.GrazeBobSeconds), "head").Joint.Y;
            Assert.That(high - low, Is.GreaterThan(0.02), "a grazing kangaroo's head moves " + (high - low).ToString("0.000") + " m");
            Assert.That(Find(Built(Roo, AnimalPose.Grazing, 0.25 * AnimalShapes.GrazeBobSeconds + AnimalShapes.GrazeBobSeconds), "head").Joint.Y,
                        Is.EqualTo(high).Within(1e-6), "the head's lift does not come round again");
            // A resting animal is still.
            Assert.That(Find(Built(Roo, AnimalPose.Resting, 7.3), "head").Joint.Y,
                        Is.EqualTo(Find(Built(Roo, AnimalPose.Resting, 0.0), "head").Joint.Y).Within(1e-9), "a resting kangaroo moves");
        }

        // ------------------------------------------------------------------ what this build does not draw

        [Test]
        public void AKindWithNoShapesIsRefusedAndSaysSo()
        {
            Assert.That(AnimalShapes.Draws(AnimalSpecies.SuperbFairyWren), Is.False, "the fairy-wren has shapes it should not");
            Assert.That(AnimalShapes.BoneCount(AnimalSpecies.SuperbFairyWren), Is.EqualTo(0), "the fairy-wren counts bones it has none of");
            Assert.That(() => AnimalShapes.Build(AnimalSpecies.SuperbFairyWren, AnimalPose.Resting, 0.0, new List<AnimalBone>()),
                        Throws.ArgumentException, "a kind with no shapes was drawn anyway");
        }

        /// <summary>A pose this build has no meaning for is drawn resting: a client that refuses to draw shows nothing and says nothing.</summary>
        [Test]
        public void APoseThisBuildDoesNotKnowIsDrawnResting()
        {
            foreach (AnimalSpecies species in Both)
                foreach (byte pose in new byte[] { 0, 4, 200 })
                {
                    List<AnimalBone> odd = Built(species, pose), resting = Built(species, AnimalPose.Resting);
                    Assert.That(odd.Count, Is.EqualTo(resting.Count), species.Name + " in pose " + pose + " has a different number of bones");
                    for (int i = 0; i < odd.Count; i++)
                    {
                        Assert.That((odd[i].Joint - resting[i].Joint).Length, Is.EqualTo(0.0).Within(1e-9), species.Name + "'s " + odd[i].Name + " in pose " + pose + " is not where it rests");
                        Assert.That((odd[i].Along - resting[i].Along).Length, Is.EqualTo(0.0).Within(1e-9), species.Name + "'s " + odd[i].Name + " in pose " + pose + " does not point where it rests");
                    }
                }
        }

        // ------------------------------------------------------------------ the measuring, worked out here

        private struct Box
        {
            public double MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
        }

        /// <summary>The poses and moments a skin is judged in: each pose at a few moments through its cycle.</summary>
        private static IEnumerable<(byte, double)> Moments(AnimalSpecies species)
        {
            double cycle = species == Roo ? AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs) : AnimalShapes.BirdWingbeatSeconds;
            yield return (AnimalPose.Resting, 0.0);
            yield return (AnimalPose.Grazing, 0.0);
            yield return (AnimalPose.Grazing, 0.25 * AnimalShapes.GrazeBobSeconds);
            yield return (AnimalPose.Grazing, 0.75 * AnimalShapes.GrazeBobSeconds);
            for (int i = 0; i < 8; i++) yield return (AnimalPose.Fleeing, cycle * i / 8.0);
        }

        private static List<string> Names(List<AnimalBone> bones)
        {
            List<string> names = new List<string>();
            foreach (AnimalBone bone in bones) names.Add(bone.Name);
            return names;
        }

        private static List<string> ShellNames(AnimalBody body)
        {
            List<string> names = new List<string>();
            foreach (AnimalShell shell in body.Shells) names.Add(shell.Name);
            return names;
        }

        private static AnimalBone Find(List<AnimalBone> bones, string name)
        {
            foreach (AnimalBone bone in bones)
                if (bone.Name == name) return bone;
            Assert.Fail("no bone named " + name);
            return default;
        }

        private static AnimalShell Shell(AnimalBody body, string name)
        {
            foreach (AnimalShell shell in body.Shells)
                if (shell.Name == name) return shell;
            Assert.Fail("no shell named " + name);
            return null;
        }

        private static Double3 Mirror(Double3 v) => new Double3(-v.X, v.Y, v.Z);

        private static double Brightness(Rgb c) => (c.R + c.G + c.B) / 3.0;

        /// <summary>The point of a bone's own length nearest to a point.</summary>
        private static Double3 NearestOn(in AnimalBone bone, Double3 p)
        {
            double along = Math.Max(0.0, Math.Min(bone.LengthM, Double3.Dot(p - bone.Joint, bone.Along)));
            return bone.Joint + bone.Along * along;
        }

        private static List<int> IndicesOf(AnimalShell shell)
        {
            List<int> indices = new List<int> { shell.StartPole, shell.EndPole };
            foreach (AnimalStation station in shell.Stations)
                for (int k = 0; k < shell.Sides; k++) indices.Add(station.FirstPoint + k);
            return indices;
        }

        private static List<Double3> PointsOf(AnimalShell shell, Double3[] points)
        {
            List<Double3> list = new List<Double3>();
            foreach (int i in IndicesOf(shell)) list.Add(points[i]);
            return list;
        }

        private static List<AnimalFace> FacesOf(AnimalBody body, string shellName)
        {
            AnimalShell shell = Shell(body, shellName);
            List<AnimalFace> faces = new List<AnimalFace>();
            for (int f = shell.FirstFace; f < shell.FirstFace + shell.FaceCount; f++) faces.Add(body.Faces[f]);
            return faces;
        }

        private static bool RidesOneBone(AnimalBody body, AnimalShell shell, out int bone)
        {
            bone = body.PointBone[shell.StartPole];
            foreach (int i in IndicesOf(shell))
                if (body.PointBone[i] != bone || (body.PointBlend[i] >= 0 && body.PointShare[i] < 1.0)) return false;
            return true;
        }

        /// <summary>The middle of a ring, as the mean of its points.</summary>
        private static Double3 Centre(AnimalShell shell, int ring, Double3[] points)
        {
            Double3 sum = Double3.Zero;
            for (int k = 0; k < shell.Sides; k++) sum += points[shell.Stations[ring].FirstPoint + k];
            return sum / shell.Sides;
        }

        private static double MeanRadius(AnimalShell shell, int ring, Double3[] points)
        {
            Double3 centre = Centre(shell, ring, points);
            double sum = 0.0;
            for (int k = 0; k < shell.Sides; k++) sum += (points[shell.Stations[ring].FirstPoint + k] - centre).Length;
            return sum / shell.Sides;
        }

        /// <summary>Twice the signed area of a ring's polygon about its middle, seen along an axis: positive while the ring keeps its turning sense.</summary>
        private static double Turning(AnimalShell shell, int ring, Double3[] points, Double3 centre, Double3 axis)
        {
            Double3 sum = Double3.Zero;
            int first = shell.Stations[ring].FirstPoint;
            for (int k = 0; k < shell.Sides; k++)
                sum += Double3.Cross(points[first + k] - centre, points[first + (k + 1) % shell.Sides] - centre);
            return Double3.Dot(sum, axis.Normalized);
        }

        /// <summary>
        /// A point inside the shell for a face to look away from: for a face of a band, the nearest point of the axis between
        /// the band's two ring middles; for a face of a fan, the middle of the ring the fan is on.
        /// </summary>
        private static Double3 Inside(AnimalShell shell, AnimalFace face, Double3[] points, Double3[] centres)
        {
            int RingOf(int p)
            {
                if (p == shell.StartPole || p == shell.EndPole) return -1;
                for (int r = 0; r < shell.Stations.Count; r++)
                    if (p >= shell.Stations[r].FirstPoint && p < shell.Stations[r].FirstPoint + shell.Sides) return r;
                return -1;
            }
            int a = RingOf(face.A), b = RingOf(face.B), c = RingOf(face.C);
            int low = Math.Min(a < 0 ? int.MaxValue : a, Math.Min(b < 0 ? int.MaxValue : b, c < 0 ? int.MaxValue : c));
            int high = Math.Max(a, Math.Max(b, c));
            if (a < 0 || b < 0 || c < 0) return centres[high];
            if (low == high) return centres[low];
            Double3 from = centres[low], to = centres[high];
            Double3 centroid = (points[face.A] + points[face.B] + points[face.C]) / 3.0;
            Double3 axis = to - from;
            double t = Math.Max(0.0, Math.Min(1.0, Double3.Dot(centroid - from, axis) / axis.SqrLength));
            return from + axis * t;
        }

        /// <summary>
        /// Three lines in unrelated directions for <see cref="IsInside"/> to cast along: none lies in the animal's vertical
        /// plane or along any bone, so a line that grazes an edge or a corner of the skin is outvoted by the other two.
        /// </summary>
        private static readonly Double3[] Casts =
        {
            new Double3(0.3183, 0.7071, 0.6317).Normalized,
            new Double3(-0.5772, 0.2236, -0.7854).Normalized,
            new Double3(0.8660, -0.4142, 0.2718).Normalized,
        };

        /// <summary>
        /// Whether a point lies inside a shell, judged on the shell's own posed faces alone: a line cast from the point
        /// crosses a closed surface an odd number of times when the point is inside it and an even number when it is
        /// outside, whatever shape the surface has, so the crossings of each of the <see cref="Casts"/> are counted and the
        /// three vote. Until 2026-09-18 this cut the shell into slabs square to the line between the ring middles, which
        /// left a wedge uncovered on the outside of every mitred bend and called the kangaroo's own hip outside its rump.
        /// </summary>
        private static bool IsInside(AnimalBody body, AnimalShell shell, Double3[] points, Double3 p)
        {
            int votes = 0;
            foreach (Double3 direction in Casts)
            {
                int crossings = 0;
                for (int f = shell.FirstFace; f < shell.FirstFace + shell.FaceCount; f++)
                {
                    AnimalFace face = body.Faces[f];
                    if (Crosses(p, direction, points[face.A], points[face.B], points[face.C])) crossings++;
                }
                if ((crossings & 1) == 1) votes++;
            }
            return votes >= 2;
        }

        /// <summary>Whether a line from a point in a direction meets a triangle ahead of the point (Möller and Trumbore, 1997).</summary>
        private static bool Crosses(Double3 from, Double3 direction, Double3 a, Double3 b, Double3 c)
        {
            Double3 ab = b - a, ac = c - a;
            Double3 h = Double3.Cross(direction, ac);
            double det = Double3.Dot(ab, h);
            if (Math.Abs(det) < 1e-18) return false;
            double inv = 1.0 / det;
            Double3 s = from - a;
            double u = inv * Double3.Dot(s, h);
            if (u < 0.0 || u > 1.0) return false;
            Double3 q = Double3.Cross(s, ab);
            double v = inv * Double3.Dot(direction, q);
            if (v < 0.0 || u + v > 1.0) return false;
            return inv * Double3.Dot(ac, q) > 1e-12;
        }

        private static Box Bound(IEnumerable<Double3> points)
        {
            Box box = new Box
            {
                MinX = double.PositiveInfinity, MinY = double.PositiveInfinity, MinZ = double.PositiveInfinity,
                MaxX = double.NegativeInfinity, MaxY = double.NegativeInfinity, MaxZ = double.NegativeInfinity,
            };
            foreach (Double3 p in points)
            {
                box.MinX = Math.Min(box.MinX, p.X);
                box.MaxX = Math.Max(box.MaxX, p.X);
                box.MinY = Math.Min(box.MinY, p.Y);
                box.MaxY = Math.Max(box.MaxY, p.Y);
                box.MinZ = Math.Min(box.MinZ, p.Z);
                box.MaxZ = Math.Max(box.MaxZ, p.Z);
            }
            return box;
        }
    }
}

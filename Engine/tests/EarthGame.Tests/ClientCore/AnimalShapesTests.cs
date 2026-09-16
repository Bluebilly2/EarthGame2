using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// What the two animals are made of (M1.7b): the same pieces in every pose, each the same size and colour wherever it
    /// goes; sizes inside the published measurements of the species; the two sides of an animal mirroring each other; the
    /// hop's cadence worked out from the speed a mob flees at and the stride it covers; and the flight taking the bird off
    /// the ground with its wings out.
    ///
    /// <para>The bounds are worked out here from each piece's own size and turn, never from the shapes' own code: the
    /// rotation is rebuilt from the three angles the table states (roll about z, then pitch about x, then yaw about y) and
    /// each solid's reach along a world axis is taken by its own rule — a box by the sum of its turned half-sides, an
    /// ellipsoid by the root of their squares, a capsule by its straight part plus its radius.</para>
    /// </summary>
    public sealed class AnimalShapesTests
    {
        private static readonly AnimalSpecies Roo = AnimalSpecies.EasternGreyKangaroo;
        private static readonly AnimalSpecies Bird = AnimalSpecies.PiedOystercatcher;
        private static readonly byte[] Poses = { AnimalPose.Resting, AnimalPose.Grazing, AnimalPose.Fleeing };

        private static List<AnimalPart> Built(AnimalSpecies species, byte pose, double timeS = 0.0)
        {
            List<AnimalPart> parts = new List<AnimalPart>();
            AnimalShapes.Build(species, pose, timeS, parts);
            return parts;
        }

        // ------------------------------------------------------------------ the pieces themselves

        [Test]
        public void EachKindIsTheSamePiecesInTheSameOrderInEveryPose()
        {
            foreach (AnimalSpecies species in new[] { Roo, Bird })
            {
                List<AnimalPart> resting = Built(species, AnimalPose.Resting);
                Assert.That(resting.Count, Is.EqualTo(AnimalShapes.PartCount(species)), species.Name + " makes a different number of pieces than it says");
                Assert.That(AnimalShapes.Draws(species), Is.True, species.Name + " is drawn and says it is not");
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 1.0; t += 0.13)
                    {
                        List<AnimalPart> parts = Built(species, pose, t);
                        Assert.That(parts.Count, Is.EqualTo(resting.Count), species.Name + " in pose " + pose + " at " + t + " s has a different number of pieces");
                        for (int i = 0; i < parts.Count; i++)
                            Assert.That(parts[i].Name, Is.EqualTo(resting[i].Name), species.Name + " piece " + i + " in pose " + pose + " is named differently");
                    }
            }
        }

        [Test]
        public void ThePiecesAreTheNamedPartsOfEachAnimal()
        {
            List<string> roo = Names(Built(Roo, AnimalPose.Grazing));
            foreach (string named in new[] { "torso", "chest", "neck", "head", "muzzle", "tail.base", "tail.tip" })
                Assert.That(roo, Does.Contain(named), "the kangaroo has no " + named);
            foreach (string paired in new[] { "ear", "haunch", "shank", "foot", "arm", "forearm" })
            {
                Assert.That(roo, Does.Contain(paired + ".left"), "the kangaroo has no left " + paired);
                Assert.That(roo, Does.Contain(paired + ".right"), "the kangaroo has no right " + paired);
            }

            List<string> bird = Names(Built(Bird, AnimalPose.Grazing));
            foreach (string named in new[] { "back", "belly", "neck", "head", "bill", "tail" })
                Assert.That(bird, Does.Contain(named), "the oystercatcher has no " + named);
            foreach (string paired in new[] { "wing", "pinion", "leg", "foot" })
            {
                Assert.That(bird, Does.Contain(paired + ".left"), "the oystercatcher has no left " + paired);
                Assert.That(bird, Does.Contain(paired + ".right"), "the oystercatcher has no right " + paired);
            }
        }

        [Test]
        public void APieceKeepsItsShapeSizeAndColourThroughEveryPose()
        {
            foreach (AnimalSpecies species in new[] { Roo, Bird })
            {
                List<AnimalPart> first = new List<AnimalPart>();
                AnimalShapes.PartsOf(species, first);
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 0.9; t += 0.17)
                    {
                        List<AnimalPart> parts = Built(species, pose, t);
                        for (int i = 0; i < parts.Count; i++)
                        {
                            string what = species.Name + "'s " + parts[i].Name + " in pose " + pose;
                            Assert.That(parts[i].Shape, Is.EqualTo(first[i].Shape), what + " changed shape");
                            Assert.That(parts[i].SizeX, Is.EqualTo(first[i].SizeX).Within(1e-5f), what + " changed width");
                            Assert.That(parts[i].SizeY, Is.EqualTo(first[i].SizeY).Within(1e-5f), what + " changed length");
                            Assert.That(parts[i].SizeZ, Is.EqualTo(first[i].SizeZ).Within(1e-5f), what + " changed depth");
                            Assert.That(parts[i].Colour.R, Is.EqualTo(first[i].Colour.R).Within(1e-6f), what + " changed colour");
                            Assert.That(parts[i].Colour.G, Is.EqualTo(first[i].Colour.G).Within(1e-6f), what + " changed colour");
                            Assert.That(parts[i].Colour.B, Is.EqualTo(first[i].Colour.B).Within(1e-6f), what + " changed colour");
                        }
                    }
            }
        }

        [Test]
        public void EveryPieceHasARealSizeAndARealPlace()
        {
            foreach (AnimalSpecies species in new[] { Roo, Bird })
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 1.1; t += 0.11)
                        foreach (AnimalPart part in Built(species, pose, t))
                        {
                            string what = species.Name + "'s " + part.Name + " in pose " + pose + " at " + t + " s";
                            foreach (float size in new[] { part.SizeX, part.SizeY, part.SizeZ })
                            {
                                Assert.That(size, Is.GreaterThan(0f), what + " has a size of nought or less");
                                Assert.That(float.IsFinite(size), Is.True, what + " has a size that is not a number");
                                Assert.That(size, Is.LessThan(3f), what + " is larger than any part of either animal");
                            }
                            foreach (float number in new[] { part.X, part.Y, part.Z, part.PitchDeg, part.YawDeg, part.RollDeg })
                                Assert.That(float.IsFinite(number), Is.True, what + " is placed at a number that is not one");
                        }
        }

        [Test]
        public void TheTwoSidesOfAnAnimalMirrorEachOther()
        {
            foreach (AnimalSpecies species in new[] { Roo, Bird })
                foreach (byte pose in Poses)
                    for (double t = 0.0; t < 0.5; t += 0.07)
                    {
                        List<AnimalPart> parts = Built(species, pose, t);
                        int pairs = 0;
                        foreach (AnimalPart left in parts)
                        {
                            if (!left.Name.EndsWith(".left", StringComparison.Ordinal)) continue;
                            string wanted = left.Name.Substring(0, left.Name.Length - 5) + ".right";
                            AnimalPart right = Find(parts, wanted);
                            string what = species.Name + "'s " + left.Name + " and " + wanted + " in pose " + pose + " at " + t + " s";
                            pairs++;
                            Assert.That(right.Shape, Is.EqualTo(left.Shape), what + ": different shapes");
                            Assert.That(right.SizeX, Is.EqualTo(left.SizeX).Within(1e-6f), what + ": different widths");
                            Assert.That(right.SizeY, Is.EqualTo(left.SizeY).Within(1e-6f), what + ": different lengths");
                            Assert.That(right.SizeZ, Is.EqualTo(left.SizeZ).Within(1e-6f), what + ": different depths");
                            Assert.That(right.X, Is.EqualTo(-left.X).Within(1e-5f), what + ": not mirrored across the animal");
                            Assert.That(right.Y, Is.EqualTo(left.Y).Within(1e-5f), what + ": one stands higher than the other");
                            Assert.That(right.Z, Is.EqualTo(left.Z).Within(1e-5f), what + ": one stands further forward than the other");
                            Assert.That(right.PitchDeg, Is.EqualTo(left.PitchDeg).Within(1e-3f), what + ": pitched differently");
                            Assert.That(Turn(right.YawDeg + left.YawDeg), Is.EqualTo(0.0).Within(1e-3), what + ": turned differently");
                            Assert.That(Turn(right.RollDeg + left.RollDeg), Is.EqualTo(0.0).Within(1e-3), what + ": rolled differently");
                            Assert.That(left.X, Is.Not.EqualTo(0f), what + ": a paired piece stands on the middle line");
                        }
                        Assert.That(pairs, Is.GreaterThanOrEqualTo(4), species.Name + " has fewer paired pieces than it should");
                    }
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
        /// again, and at the top of a hop it is up among the standing heights.
        /// </summary>
        [Test]
        public void TheKangarooKeepsToItsPublishedSize()
        {
            Box grazing = Bound(Built(Roo, AnimalPose.Grazing));
            Assert.That(grazing.MaxY, Is.GreaterThanOrEqualTo(0.80).And.LessThanOrEqualTo(1.30), "a grazing kangaroo stands " + grazing.MaxY.ToString("0.00") + " m");
            double length = grazing.MaxZ - grazing.MinZ;
            Assert.That(length, Is.GreaterThanOrEqualTo(1.70).And.LessThanOrEqualTo(2.50), "a kangaroo is " + length.ToString("0.00") + " m nose to tail");
            double width = grazing.MaxX - grazing.MinX;
            Assert.That(width, Is.GreaterThanOrEqualTo(0.30).And.LessThanOrEqualTo(0.75), "a kangaroo is " + width.ToString("0.00") + " m across");

            Box resting = Bound(Built(Roo, AnimalPose.Resting));
            Assert.That(resting.MaxY, Is.GreaterThanOrEqualTo(0.45).And.LessThanOrEqualTo(0.95), "a kangaroo lying up stands " + resting.MaxY.ToString("0.00") + " m");
            Assert.That(resting.MaxY, Is.LessThan(grazing.MaxY), "a kangaroo lying up is no lower than one grazing");

            // The top of the hop, half a stride after a touchdown.
            double hop = AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs);
            Box top = Bound(Built(Roo, AnimalPose.Fleeing, 0.5 * hop));
            Assert.That(top.MaxY, Is.GreaterThanOrEqualTo(1.20).And.LessThanOrEqualTo(1.80), "a bounding kangaroo reaches " + top.MaxY.ToString("0.00") + " m");
        }

        /// <summary>Every pose but the middle of a hop stands on the ground: no animal floats, and none is buried.</summary>
        [Test]
        public void TheAnimalsStandOnTheGroundTheyAreGiven()
        {
            foreach (AnimalSpecies species in new[] { Roo, Bird })
                foreach (byte pose in new[] { AnimalPose.Resting, AnimalPose.Grazing })
                    for (double t = 0.0; t < 3.3; t += 0.31)
                    {
                        Box box = Bound(Built(species, pose, t));
                        Assert.That(box.MinY, Is.GreaterThanOrEqualTo(-0.05).And.LessThanOrEqualTo(0.06),
                                    species.Name + " in pose " + pose + " has its lowest point at " + box.MinY.ToString("0.000") + " m");
                    }

            // A hopping kangaroo touches down once a stride and is off the ground the rest of it.
            double hop = AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs);
            Assert.That(Bound(Built(Roo, AnimalPose.Fleeing, 0.0)).MinY, Is.LessThanOrEqualTo(0.06), "a hop never touches the ground");
            Assert.That(Bound(Built(Roo, AnimalPose.Fleeing, 0.5 * hop)).MinY, Is.GreaterThan(0.2), "a hop never leaves the ground");
        }

        // ------------------------------------------------------------------ the oystercatcher against the measurements

        /// <summary>
        /// A pied oystercatcher is 45 to 50 cm from the tip of the bill to the tip of the tail, stands about a third of a
        /// metre, and spans 80 to 86 cm with its wings out.
        /// </summary>
        [Test]
        public void TheOystercatcherKeepsToItsPublishedSize()
        {
            List<AnimalPart> standing = Built(Bird, AnimalPose.Resting);
            Box bill = Bound(Only(standing, "bill")), tail = Bound(Only(standing, "tail"));
            double length = bill.MaxZ - tail.MinZ;
            Assert.That(length, Is.GreaterThanOrEqualTo(0.44).And.LessThanOrEqualTo(0.53), "an oystercatcher is " + length.ToString("0.000") + " m bill to tail");

            Box whole = Bound(standing);
            Assert.That(whole.MaxY, Is.GreaterThanOrEqualTo(0.25).And.LessThanOrEqualTo(0.45), "a standing oystercatcher is " + whole.MaxY.ToString("0.000") + " m tall");
            double folded = whole.MaxX - whole.MinX;
            Assert.That(folded, Is.LessThanOrEqualTo(0.26), "a standing oystercatcher is " + folded.ToString("0.000") + " m across with its wings folded");

            // The wings are widest as they pass level, which is where a beat starts.
            Box flying = Bound(Built(Bird, AnimalPose.Fleeing, 0.0));
            double span = flying.MaxX - flying.MinX;
            Assert.That(span, Is.GreaterThanOrEqualTo(0.76).And.LessThanOrEqualTo(0.92), "an oystercatcher spans " + span.ToString("0.000") + " m");
            Assert.That(span, Is.GreaterThan(2.5 * folded), "the wings are hardly wider out than folded");
        }

        /// <summary>A startled pair goes up: the body is a metre or two off the sand through the whole of a wingbeat.</summary>
        [Test]
        public void TheFlightTakesTheBirdOffTheGround()
        {
            for (double t = 0.0; t < 2.0 * AnimalShapes.BirdWingbeatSeconds; t += 0.01)
            {
                List<AnimalPart> parts = Built(Bird, AnimalPose.Fleeing, t);
                AnimalPart back = Find(parts, "back");
                Assert.That(back.Y, Is.GreaterThanOrEqualTo(1.0).And.LessThanOrEqualTo(2.0), "the bird's body is " + back.Y.ToString("0.00") + " m up at " + t.ToString("0.000") + " s");
                Assert.That(Bound(parts).MinY, Is.GreaterThan(0.8), "something of the flying bird still touches the ground at " + t.ToString("0.000") + " s");
            }
        }

        /// <summary>The wings beat: they pass above and below the shoulder within one beat, and the beat repeats.</summary>
        [Test]
        public void TheWingsBeatOnceAWingbeat()
        {
            double beat = AnimalShapes.BirdWingbeatSeconds;
            double highest = double.NegativeInfinity, lowest = double.PositiveInfinity;
            for (double t = 0.0; t < beat; t += beat / 64.0)
            {
                double tip = Find(Built(Bird, AnimalPose.Fleeing, t), "pinion.right").Y;
                highest = Math.Max(highest, tip);
                lowest = Math.Min(lowest, tip);
            }
            Assert.That(highest - lowest, Is.GreaterThan(0.25), "the wingtip moves " + (highest - lowest).ToString("0.000") + " m in a beat");
            AnimalPart now = Find(Built(Bird, AnimalPose.Fleeing, 0.37), "pinion.right");
            AnimalPart later = Find(Built(Bird, AnimalPose.Fleeing, 0.37 + beat), "pinion.right");
            Assert.That(later.Y, Is.EqualTo(now.Y).Within(1e-5f), "a wingbeat does not come round again");
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

        /// <summary>The hop lifts the body by its stated rise and comes round again on its own cadence.</summary>
        [Test]
        public void TheHopRaisesAndLowersTheBody()
        {
            double hop = AnimalShapes.HopSeconds(AnimalFlightRules.KangarooRunMs);
            float down = Find(Built(Roo, AnimalPose.Fleeing, 0.0), "torso").Y;
            float up = Find(Built(Roo, AnimalPose.Fleeing, 0.5 * hop), "torso").Y;
            Assert.That(up - down, Is.EqualTo((float)AnimalShapes.HopLiftM).Within(1e-3f), "the body rises " + (up - down).ToString("0.000") + " m in a hop");
            Assert.That(Find(Built(Roo, AnimalPose.Fleeing, 0.3 * hop + hop), "torso").Y,
                        Is.EqualTo(Find(Built(Roo, AnimalPose.Fleeing, 0.3 * hop), "torso").Y).Within(1e-4f), "a hop does not come round again");
            // The hind feet come forward under the body to meet the ground and tuck up behind at the top of the bound.
            float touchdown = Find(Built(Roo, AnimalPose.Fleeing, 0.0), "foot.right").Z;
            float tucked = Find(Built(Roo, AnimalPose.Fleeing, 0.5 * hop), "foot.right").Z;
            Assert.That(touchdown, Is.GreaterThan(tucked + 0.15f), "the feet do not swing through the hop");
        }

        /// <summary>A grazing animal is not a statue: its head lifts and falls, and nothing else about it moves.</summary>
        [Test]
        public void AGrazingAnimalLiftsItsHead()
        {
            float low = Find(Built(Roo, AnimalPose.Grazing, 0.75 * AnimalShapes.GrazeBobSeconds), "head").Y;
            float high = Find(Built(Roo, AnimalPose.Grazing, 0.25 * AnimalShapes.GrazeBobSeconds), "head").Y;
            Assert.That(high - low, Is.GreaterThan(0.02f), "a grazing kangaroo's head moves " + (high - low).ToString("0.000") + " m");
            Assert.That(Find(Built(Roo, AnimalPose.Grazing, 0.25 * AnimalShapes.GrazeBobSeconds + AnimalShapes.GrazeBobSeconds), "head").Y,
                        Is.EqualTo(high).Within(1e-4f), "the head's lift does not come round again");
            // A resting animal is still.
            Assert.That(Find(Built(Roo, AnimalPose.Resting, 7.3), "head").Y,
                        Is.EqualTo(Find(Built(Roo, AnimalPose.Resting, 0.0), "head").Y).Within(1e-6f), "a resting kangaroo moves");
        }

        // ------------------------------------------------------------------ what this build does not draw

        [Test]
        public void AKindWithNoShapesIsRefusedAndSaysSo()
        {
            Assert.That(AnimalShapes.Draws(AnimalSpecies.SuperbFairyWren), Is.False, "the fairy-wren has shapes it should not");
            Assert.That(AnimalShapes.PartCount(AnimalSpecies.SuperbFairyWren), Is.EqualTo(0), "the fairy-wren counts pieces it has none of");
            Assert.That(() => AnimalShapes.Build(AnimalSpecies.SuperbFairyWren, AnimalPose.Resting, 0.0, new List<AnimalPart>()),
                        Throws.ArgumentException, "a kind with no shapes was drawn anyway");
        }

        /// <summary>A pose this build has no meaning for is drawn resting: a client that refuses to draw shows nothing and says nothing.</summary>
        [Test]
        public void APoseThisBuildDoesNotKnowIsDrawnResting()
        {
            foreach (AnimalSpecies species in new[] { Roo, Bird })
                foreach (byte pose in new byte[] { 0, 4, 200 })
                {
                    List<AnimalPart> odd = Built(species, pose), resting = Built(species, AnimalPose.Resting);
                    Assert.That(odd.Count, Is.EqualTo(resting.Count), species.Name + " in pose " + pose + " has a different number of pieces");
                    for (int i = 0; i < odd.Count; i++)
                    {
                        Assert.That(odd[i].Y, Is.EqualTo(resting[i].Y).Within(1e-6f), species.Name + "'s " + odd[i].Name + " in pose " + pose + " is not where it rests");
                        Assert.That(odd[i].Z, Is.EqualTo(resting[i].Z).Within(1e-6f), species.Name + "'s " + odd[i].Name + " in pose " + pose + " is not where it rests");
                    }
                }
        }


        // ------------------------------------------------------------------ the measuring, worked out here

        private struct Box
        {
            public double MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
        }

        private static List<string> Names(List<AnimalPart> parts)
        {
            List<string> names = new List<string>();
            foreach (AnimalPart part in parts) names.Add(part.Name);
            return names;
        }

        private static AnimalPart Find(List<AnimalPart> parts, string name)
        {
            foreach (AnimalPart part in parts)
                if (part.Name == name) return part;
            Assert.Fail("no piece named " + name);
            return default;
        }

        private static List<AnimalPart> Only(List<AnimalPart> parts, string name)
        {
            return new List<AnimalPart> { Find(parts, name) };
        }

        /// <summary>An angle folded onto (−180, 180], so two turns that differ by a whole circle read as one.</summary>
        private static double Turn(double degrees)
        {
            double t = degrees % 360.0;
            if (t > 180.0) t -= 360.0;
            if (t <= -180.0) t += 360.0;
            return t;
        }

        /// <summary>
        /// The box a set of pieces fills, each piece measured from its own size and its three angles. The rotation is
        /// rebuilt here: roll about z, then pitch about x, then yaw about y, each clockwise seen along its positive axis.
        /// </summary>
        private static Box Bound(List<AnimalPart> parts)
        {
            Box box = new Box
            {
                MinX = double.PositiveInfinity, MinY = double.PositiveInfinity, MinZ = double.PositiveInfinity,
                MaxX = double.NegativeInfinity, MaxY = double.NegativeInfinity, MaxZ = double.NegativeInfinity,
            };
            foreach (AnimalPart part in parts)
            {
                double p = part.PitchDeg * Math.PI / 180.0, y = part.YawDeg * Math.PI / 180.0, r = part.RollDeg * Math.PI / 180.0;
                double[][] rows = Rotation(p, y, r);
                double hx = 0.5 * part.SizeX, hy = 0.5 * part.SizeY, hz = 0.5 * part.SizeZ;
                double[] centre = { part.X, part.Y, part.Z };
                for (int axis = 0; axis < 3; axis++)
                {
                    double[] row = rows[axis];
                    double reach;
                    switch (part.Shape)
                    {
                        case AnimalPartShape.Box:
                            reach = Math.Abs(hx * row[0]) + Math.Abs(hy * row[1]) + Math.Abs(hz * row[2]);
                            break;
                        case AnimalPartShape.Ellipsoid:
                            reach = Math.Sqrt(hx * hx * row[0] * row[0] + hy * hy * row[1] * row[1] + hz * hz * row[2] * row[2]);
                            break;
                        default:
                            // A capsule: a straight part of its length less its two caps, plus the cap's radius in every direction.
                            double radius = Math.Max(hx, hz);
                            reach = Math.Abs(Math.Max(0.0, hy - radius) * row[1]) + radius;
                            break;
                    }
                    double low = centre[axis] - reach, high = centre[axis] + reach;
                    if (axis == 0)
                    {
                        box.MinX = Math.Min(box.MinX, low);
                        box.MaxX = Math.Max(box.MaxX, high);
                    }
                    else if (axis == 1)
                    {
                        box.MinY = Math.Min(box.MinY, low);
                        box.MaxY = Math.Max(box.MaxY, high);
                    }
                    else
                    {
                        box.MinZ = Math.Min(box.MinZ, low);
                        box.MaxZ = Math.Max(box.MaxZ, high);
                    }
                }
            }
            return box;
        }

        /// <summary>The three rows of the turn a piece's angles make, so a reach along a world axis can be taken from one row.</summary>
        private static double[][] Rotation(double pitch, double yaw, double roll)
        {
            double cp = Math.Cos(pitch), sp = Math.Sin(pitch);
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
            double cr = Math.Cos(roll), sr = Math.Sin(roll);
            // About z, then about x, then about y.
            double[][] z = { new[] { cr, -sr, 0.0 }, new[] { sr, cr, 0.0 }, new[] { 0.0, 0.0, 1.0 } };
            double[][] x = { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, cp, -sp }, new[] { 0.0, sp, cp } };
            double[][] yr = { new[] { cy, 0.0, sy }, new[] { 0.0, 1.0, 0.0 }, new[] { -sy, 0.0, cy } };
            return Times(yr, Times(x, z));
        }

        private static double[][] Times(double[][] a, double[][] b)
        {
            double[][] m = { new double[3], new double[3], new double[3] };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < 3; k++) sum += a[i][k] * b[k][j];
                    m[i][j] = sum;
                }
            return m;
        }
    }
}

using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>A colour as the client's tables hold it, 0 to 1 a channel; the Unity layer makes its own of it.</summary>
    public readonly struct Rgb
    {
        public readonly float R, G, B;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>
    /// What one tall plant looks like, as the numbers a tree is grown from (M1.6a). Ported from v1's <c>TreeForm</c>,
    /// which gave each species its silhouette because a founder reads a stand from the skyline long before the bark:
    /// every number is a share of the tree's height.
    ///
    /// <para>Shape, not physics. These answer to the real habit of each plant and to the eye, and a person who knows
    /// the trees can dispute them line by line. The one number the world reads as well — how wide the crown is — is
    /// not here: it is <see cref="PlantSpecies.CrownShare"/>, and a drawn crown is scaled to it, so the width the
    /// server spaced the trunks by is the width a client draws.</para>
    /// </summary>
    public sealed class TreeForm
    {
        public string Name { get; }

        /// <summary>Trunk as a share of height, before the crown is stacked on it.</summary>
        public float TrunkLength { get; }

        /// <summary>Butt radius as a share of height: slender or stout.</summary>
        public float TrunkRadius { get; }

        /// <summary>Where the limbs leave, as a share of height.</summary>
        public float ForkShare { get; }

        /// <summary>How far off the trunk's line a limb leaves, degrees.</summary>
        public float LimbSplayDeg { get; }

        /// <summary>A limb's length as a share of height.</summary>
        public float LimbLength { get; }

        /// <summary>How many limbs, at most: one leans the tree, three fan it.</summary>
        public int MaxLimbs { get; }

        /// <summary>How many clumps of leaves the crown is made of, and how big and how far off their tips they hang.</summary>
        public int ClumpMin { get; }
        public int ClumpMax { get; }
        public float ClumpRadius { get; }
        public float ClumpOffset { get; }

        /// <summary>Crown proportion: below one a flattened dome, above one a spire.</summary>
        public float CrownSquash { get; }

        /// <summary>How far the whole tree leans, degrees.</summary>
        public float LeanDeg { get; }

        /// <summary>How far up the rough bark runs, as a share of the trunk: 0 none, 1 all of it.</summary>
        public float StockingShare { get; }

        public Rgb BarkLow { get; }
        public Rgb BarkHigh { get; }
        public Rgb Foliage { get; }

        public TreeForm(string name, float trunkLength, float trunkRadius, float forkShare,
                        float limbSplayDeg, float limbLength, int maxLimbs,
                        int clumpMin, int clumpMax, float clumpRadius, float clumpOffset,
                        float crownSquash, float leanDeg, float stockingShare,
                        Rgb barkLow, Rgb barkHigh, Rgb foliage)
        {
            Name = name;
            TrunkLength = trunkLength;
            TrunkRadius = trunkRadius;
            ForkShare = forkShare;
            LimbSplayDeg = limbSplayDeg;
            LimbLength = limbLength;
            MaxLimbs = maxLimbs;
            ClumpMin = clumpMin;
            ClumpMax = clumpMax;
            ClumpRadius = clumpRadius;
            ClumpOffset = clumpOffset;
            CrownSquash = crownSquash;
            LeanDeg = leanDeg;
            StockingShare = stockingShare;
            BarkLow = barkLow;
            BarkHigh = barkHigh;
            Foliage = foliage;
        }
    }

    /// <summary>
    /// What stands and lies on the ground looks like (M1.6a): the one table the client asks, so that a tree drawn near
    /// and the same tree drawn far, and a stick lying and a stick dropped, are one plant and one stick.
    /// </summary>
    public static class StandForms
    {
        /// <summary>Blackbutt: a long clean trunk, rough fibrous bark on the lower part and smooth pale grey above, the crown held high.</summary>
        public static readonly TreeForm Blackbutt = new TreeForm(
            "Blackbutt", trunkLength: 0.74f, trunkRadius: 0.030f, forkShare: 0.66f,
            limbSplayDeg: 32f, limbLength: 0.30f, maxLimbs: 2,
            clumpMin: 5, clumpMax: 7, clumpRadius: 0.090f, clumpOffset: 0.15f,
            crownSquash: 0.70f, leanDeg: 3f, stockingShare: 0.40f,
            barkLow: new Rgb(0.36f, 0.30f, 0.25f), barkHigh: new Rgb(0.80f, 0.78f, 0.72f), foliage: new Rgb(0.25f, 0.34f, 0.20f));

        /// <summary>Bangalay: shorter and often crooked by the sea, rough fibrous bark to the branches, a broad dense crown.</summary>
        public static readonly TreeForm Bangalay = new TreeForm(
            "Bangalay", trunkLength: 0.58f, trunkRadius: 0.042f, forkShare: 0.50f,
            limbSplayDeg: 46f, limbLength: 0.36f, maxLimbs: 3,
            clumpMin: 6, clumpMax: 9, clumpRadius: 0.110f, clumpOffset: 0.13f,
            crownSquash: 0.62f, leanDeg: 7f, stockingShare: 0.95f,
            barkLow: new Rgb(0.42f, 0.32f, 0.25f), barkHigh: new Rgb(0.46f, 0.36f, 0.28f), foliage: new Rgb(0.22f, 0.31f, 0.19f));

        /// <summary>Old-man banksia: a short gnarled trunk in thick grey bark, and a low crown spread wide off crooked limbs.</summary>
        public static readonly TreeForm OldManBanksia = new TreeForm(
            "OldManBanksia", trunkLength: 0.42f, trunkRadius: 0.060f, forkShare: 0.34f,
            limbSplayDeg: 55f, limbLength: 0.36f, maxLimbs: 3,
            clumpMin: 6, clumpMax: 9, clumpRadius: 0.120f, clumpOffset: 0.16f,
            crownSquash: 0.58f, leanDeg: 10f, stockingShare: 0.95f,
            barkLow: new Rgb(0.30f, 0.28f, 0.26f), barkHigh: new Rgb(0.34f, 0.32f, 0.29f), foliage: new Rgb(0.29f, 0.35f, 0.22f));

        /// <summary>Coast banksia: grey bark and a dense rounded crown whose leaves are white beneath, so it reads silver-green.</summary>
        public static readonly TreeForm CoastBanksia = new TreeForm(
            "CoastBanksia", trunkLength: 0.48f, trunkRadius: 0.048f, forkShare: 0.40f,
            limbSplayDeg: 45f, limbLength: 0.30f, maxLimbs: 3,
            clumpMin: 7, clumpMax: 10, clumpRadius: 0.120f, clumpOffset: 0.12f,
            crownSquash: 0.75f, leanDeg: 8f, stockingShare: 0.90f,
            barkLow: new Rgb(0.44f, 0.42f, 0.39f), barkHigh: new Rgb(0.48f, 0.46f, 0.42f), foliage: new Rgb(0.38f, 0.45f, 0.36f));

        /// <summary>Swamp paperbark: slender stems in pale papery bark and a narrow, dense crown of fine dark leaves.</summary>
        public static readonly TreeForm SwampPaperbark = new TreeForm(
            "SwampPaperbark", trunkLength: 0.52f, trunkRadius: 0.030f, forkShare: 0.32f,
            limbSplayDeg: 18f, limbLength: 0.30f, maxLimbs: 3,
            clumpMin: 7, clumpMax: 10, clumpRadius: 0.085f, clumpOffset: 0.08f,
            crownSquash: 1.10f, leanDeg: 5f, stockingShare: 0.90f,
            barkLow: new Rgb(0.78f, 0.74f, 0.64f), barkHigh: new Rgb(0.82f, 0.78f, 0.68f), foliage: new Rgb(0.24f, 0.31f, 0.19f));

        /// <summary>A fallen stick's weathered grey-brown.</summary>
        public static readonly Rgb Stick = new Rgb(0.45f, 0.38f, 0.30f);

        /// <summary>A cobble's grey; which stone it is waits for the stone to travel with it.</summary>
        public static readonly Rgb Cobble = new Rgb(0.56f, 0.53f, 0.48f);

        /// <summary>
        /// What a tuft of the understorey is coloured at the ground (M1.6c): the heath's shrubs dark and woody, the
        /// sedge's clumps grey-green, bracken's fronds a fresher green, and grass drier than any of them, as this coast's
        /// is by the end of summer.
        /// </summary>
        public static Rgb TuftLow(TuftShape shape)
        {
            switch (shape)
            {
                case TuftShape.Shrub: return new Rgb(0.21f, 0.24f, 0.16f);
                case TuftShape.Clump: return new Rgb(0.25f, 0.30f, 0.20f);
                case TuftShape.Frond: return new Rgb(0.23f, 0.30f, 0.16f);
                case TuftShape.Herb: return new Rgb(0.20f, 0.26f, 0.14f);
                default: return new Rgb(0.33f, 0.35f, 0.20f);
            }
        }

        /// <summary>And at the tips, where the light gets in.</summary>
        public static Rgb TuftHigh(TuftShape shape)
        {
            switch (shape)
            {
                case TuftShape.Shrub: return new Rgb(0.30f, 0.36f, 0.22f);
                case TuftShape.Clump: return new Rgb(0.40f, 0.45f, 0.28f);
                case TuftShape.Frond: return new Rgb(0.38f, 0.46f, 0.24f);
                case TuftShape.Herb: return new Rgb(0.34f, 0.42f, 0.22f);
                default: return new Rgb(0.56f, 0.52f, 0.29f);
            }
        }

        /// <summary>
        /// How much of its butt radius a trunk keeps where its crown begins: the taper a tree is drawn with. The mesh owned
        /// this number alone until M1.6b, when the bodies that stop a founder began taking their radius from the same line.
        /// </summary>
        public const double TrunkTipRadiusShare = 0.35;

        /// <summary>
        /// A trunk's radius at a height above its foot, m (M1.6b): the butt radius — the form's share of the tree's whole
        /// height — tapering straight to <see cref="TrunkTipRadiusShare"/> of itself where the trunk ends, and no narrower
        /// above that. What stops a founder is the wood that is drawn.
        /// </summary>
        public static double TrunkRadiusAt(TreeForm form, double heightM, double upM)
        {
            if (form == null || !(heightM > 0.0)) return 0.0;
            double trunk = form.TrunkLength * heightM;
            double up = trunk > 0.0 ? SimMath.Clamp01(upM / trunk) : 1.0;
            return form.TrunkRadius * heightM * (1.0 + (TrunkTipRadiusShare - 1.0) * up);
        }

        private static readonly TreeForm[] Forms = { Blackbutt, Bangalay, OldManBanksia, CoastBanksia, SwampPaperbark };

        /// <summary>The form of a tall plant, or null for a plant that does not stand as a tree.</summary>
        public static TreeForm For(PlantSpecies species)
        {
            if (species == null) return null;
            foreach (TreeForm form in Forms)
                if (form.Name == species.Name) return form;
            return null;
        }

        /// <summary>The form of the tall plant a stand code's index names (<see cref="StandCodes.Tall"/>).</summary>
        public static TreeForm ForTall(int tall)
        {
            IReadOnlyList<PlantSpecies> all = StandCodes.Tall;
            if (tall < 0 || tall >= all.Count) throw new ArgumentOutOfRangeException(nameof(tall), "no tall plant " + tall);
            TreeForm form = For(all[tall]);
            if (form == null) throw new InvalidOperationException(all[tall].Name + " stands as a tree and has no form to be drawn by");
            return form;
        }
    }
}

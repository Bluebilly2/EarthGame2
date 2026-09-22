using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// The numbers a tall plant's shape is grown from (M1.6a, ported from v1's <c>TreeForm</c>; here in the engine since
    /// BF.3, 2026-09-23): every one a share of the tree's height. The client draws a tree by them and, since BF.3, the server
    /// judges by the same numbers what a standing trunk gives — its girth at breast height for the bark that strips, its
    /// taper for the logs it falls into — so the wood a founder is offered is the wood that is drawn. The colours a tree is
    /// drawn in stay the client's (<c>StandForms</c>): they are how it looks, not what it is.
    ///
    /// <para>Shape, not physics. These answer to the real habit of each plant and to the eye, and a person who knows the
    /// trees can dispute them line by line. The one number the world reads as well — how wide the crown is — is not
    /// here: it is <see cref="PlantSpecies.CrownShare"/>, and a drawn crown is scaled to it.</para>
    /// </summary>
    public sealed class TreeGeometry
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

        public TreeGeometry(string name, float trunkLength, float trunkRadius, float forkShare,
                            float limbSplayDeg, float limbLength, int maxLimbs,
                            int clumpMin, int clumpMax, float clumpRadius, float clumpOffset,
                            float crownSquash, float leanDeg, float stockingShare)
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
        }
    }

    /// <summary>The one table of what each tall plant is shaped like, and the trunk's taper, read by the client to draw and by the server to judge.</summary>
    public static class TreeGeometries
    {
        /// <summary>Blackbutt: a long clean trunk, rough fibrous bark on the lower part and smooth pale grey above, the crown held high.</summary>
        public static readonly TreeGeometry Blackbutt = new TreeGeometry(
            "Blackbutt", trunkLength: 0.74f, trunkRadius: 0.030f, forkShare: 0.66f,
            limbSplayDeg: 32f, limbLength: 0.30f, maxLimbs: 2,
            clumpMin: 5, clumpMax: 7, clumpRadius: 0.090f, clumpOffset: 0.15f,
            crownSquash: 0.70f, leanDeg: 3f, stockingShare: 0.40f);

        /// <summary>Bangalay: shorter and often crooked by the sea, rough fibrous bark to the branches, a broad dense crown.</summary>
        public static readonly TreeGeometry Bangalay = new TreeGeometry(
            "Bangalay", trunkLength: 0.58f, trunkRadius: 0.042f, forkShare: 0.50f,
            limbSplayDeg: 46f, limbLength: 0.36f, maxLimbs: 3,
            clumpMin: 6, clumpMax: 9, clumpRadius: 0.110f, clumpOffset: 0.13f,
            crownSquash: 0.62f, leanDeg: 7f, stockingShare: 0.95f);

        /// <summary>Old-man banksia: a short gnarled trunk in thick grey bark, and a low crown spread wide off crooked limbs.</summary>
        public static readonly TreeGeometry OldManBanksia = new TreeGeometry(
            "OldManBanksia", trunkLength: 0.42f, trunkRadius: 0.060f, forkShare: 0.34f,
            limbSplayDeg: 55f, limbLength: 0.36f, maxLimbs: 3,
            clumpMin: 6, clumpMax: 9, clumpRadius: 0.120f, clumpOffset: 0.16f,
            crownSquash: 0.58f, leanDeg: 10f, stockingShare: 0.95f);

        /// <summary>Coast banksia: grey bark and a dense rounded crown whose leaves are white beneath, so it reads silver-green.</summary>
        public static readonly TreeGeometry CoastBanksia = new TreeGeometry(
            "CoastBanksia", trunkLength: 0.48f, trunkRadius: 0.048f, forkShare: 0.40f,
            limbSplayDeg: 45f, limbLength: 0.30f, maxLimbs: 3,
            clumpMin: 7, clumpMax: 10, clumpRadius: 0.120f, clumpOffset: 0.12f,
            crownSquash: 0.75f, leanDeg: 8f, stockingShare: 0.90f);

        /// <summary>Swamp paperbark: slender stems in pale papery bark and a narrow, dense crown of fine dark leaves.</summary>
        public static readonly TreeGeometry SwampPaperbark = new TreeGeometry(
            "SwampPaperbark", trunkLength: 0.52f, trunkRadius: 0.030f, forkShare: 0.32f,
            limbSplayDeg: 18f, limbLength: 0.30f, maxLimbs: 3,
            clumpMin: 7, clumpMax: 10, clumpRadius: 0.085f, clumpOffset: 0.08f,
            crownSquash: 1.10f, leanDeg: 5f, stockingShare: 0.90f);

        /// <summary>
        /// How much of its butt radius a trunk keeps where its crown begins: the taper a tree is drawn with. The mesh owned
        /// this number alone until M1.6b, when the bodies that stop a founder began taking their radius from the same line;
        /// since BF.3 the server's logs are cut to it too.
        /// </summary>
        public const double TrunkTipRadiusShare = 0.35;

        /// <summary>The height a person's chest is at, m: where a trunk's girth is measured, as foresters measure it.</summary>
        public const double BreastHeightM = 1.3;

        /// <summary>
        /// A trunk's radius at a height above its foot, m (M1.6b): the butt radius — the geometry's share of the tree's whole
        /// height — tapering straight to <see cref="TrunkTipRadiusShare"/> of itself where the trunk ends, and no narrower
        /// above that. What stops a founder is the wood that is drawn, and what is stripped or cut is the same wood.
        /// </summary>
        public static double TrunkRadiusAt(TreeGeometry geometry, double heightM, double upM)
        {
            if (geometry == null || !(heightM > 0.0)) return 0.0;
            double trunk = geometry.TrunkLength * heightM;
            double up = trunk > 0.0 ? SimMath.Clamp01(upM / trunk) : 1.0;
            return geometry.TrunkRadius * heightM * (1.0 + (TrunkTipRadiusShare - 1.0) * up);
        }

        /// <summary>How far up the trunk runs before the crown begins, m.</summary>
        public static double TrunkLengthM(TreeGeometry geometry, double heightM) => geometry == null ? 0.0 : geometry.TrunkLength * heightM;

        private static readonly TreeGeometry[] Geometries = { Blackbutt, Bangalay, OldManBanksia, CoastBanksia, SwampPaperbark };

        /// <summary>Every geometry, in the stand's order of tall plants.</summary>
        public static IReadOnlyList<TreeGeometry> All => Geometries;

        /// <summary>The geometry of a tall plant, or null for a plant that does not stand as a tree.</summary>
        public static TreeGeometry For(PlantSpecies species)
        {
            if (species == null) return null;
            foreach (TreeGeometry g in Geometries)
                if (g.Name == species.Name) return g;
            return null;
        }

        /// <summary>The geometry of the tall plant a stand code's index names (<see cref="StandCodes.Tall"/>).</summary>
        public static TreeGeometry ForTall(int tall)
        {
            IReadOnlyList<PlantSpecies> all = StandCodes.Tall;
            if (tall < 0 || tall >= all.Count) throw new ArgumentOutOfRangeException(nameof(tall), "no tall plant " + tall);
            TreeGeometry g = For(all[tall]);
            if (g == null) throw new InvalidOperationException(all[tall].Name + " stands as a tree and has no geometry to be drawn by");
            return g;
        }
    }
}

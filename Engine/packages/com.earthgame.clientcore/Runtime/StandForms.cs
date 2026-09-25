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
    /// What one tall plant looks like (M1.6a): the numbers a tree is grown from, which are the engine's
    /// (<see cref="TreeGeometry"/>, one owner since BF.3, 2026-09-23, because the server judges a trunk's girth and its logs
    /// by them), and the colours it is drawn in, which are the client's alone. Every geometric number is a share of the
    /// tree's height; a founder reads a stand from the skyline long before the bark.
    /// </summary>
    public sealed class TreeForm
    {
        /// <summary>The numbers the tree is grown from, the engine's.</summary>
        public TreeGeometry Geometry { get; }

        public string Name => Geometry.Name;
        public float TrunkLength => Geometry.TrunkLength;
        public float TrunkRadius => Geometry.TrunkRadius;
        public float ForkShare => Geometry.ForkShare;
        public float LimbSplayDeg => Geometry.LimbSplayDeg;
        public float LimbLength => Geometry.LimbLength;
        public int MaxLimbs => Geometry.MaxLimbs;
        public int ClumpMin => Geometry.ClumpMin;
        public int ClumpMax => Geometry.ClumpMax;
        public float ClumpRadius => Geometry.ClumpRadius;
        public float ClumpOffset => Geometry.ClumpOffset;
        public float CrownSquash => Geometry.CrownSquash;
        public float LeanDeg => Geometry.LeanDeg;
        public float StockingShare => Geometry.StockingShare;
        public CrownKind Crown => Geometry.Crown;

        public Rgb BarkLow { get; }
        public Rgb BarkHigh { get; }
        public Rgb Foliage { get; }

        public TreeForm(TreeGeometry geometry, Rgb barkLow, Rgb barkHigh, Rgb foliage)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
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
        /// <summary>Blackbutt: rough fibrous bark on the lower part and smooth pale grey above.</summary>
        public static readonly TreeForm Blackbutt = new TreeForm(TreeGeometries.Blackbutt,
            barkLow: new Rgb(0.36f, 0.30f, 0.25f), barkHigh: new Rgb(0.80f, 0.78f, 0.72f), foliage: new Rgb(0.25f, 0.34f, 0.20f));

        /// <summary>Bangalay: rough fibrous bark to the branches, a dense dark crown.</summary>
        public static readonly TreeForm Bangalay = new TreeForm(TreeGeometries.Bangalay,
            barkLow: new Rgb(0.42f, 0.32f, 0.25f), barkHigh: new Rgb(0.46f, 0.36f, 0.28f), foliage: new Rgb(0.22f, 0.31f, 0.19f));

        /// <summary>Old-man banksia: thick grey bark, a dull crown.</summary>
        public static readonly TreeForm OldManBanksia = new TreeForm(TreeGeometries.OldManBanksia,
            barkLow: new Rgb(0.30f, 0.28f, 0.26f), barkHigh: new Rgb(0.34f, 0.32f, 0.29f), foliage: new Rgb(0.29f, 0.35f, 0.22f));

        /// <summary>Coast banksia: grey bark and leaves white beneath, so the crown reads silver-green.</summary>
        public static readonly TreeForm CoastBanksia = new TreeForm(TreeGeometries.CoastBanksia,
            barkLow: new Rgb(0.44f, 0.42f, 0.39f), barkHigh: new Rgb(0.48f, 0.46f, 0.42f), foliage: new Rgb(0.38f, 0.45f, 0.36f));

        /// <summary>Swamp paperbark: pale papery bark and fine dark leaves.</summary>
        public static readonly TreeForm SwampPaperbark = new TreeForm(TreeGeometries.SwampPaperbark,
            barkLow: new Rgb(0.78f, 0.74f, 0.64f), barkHigh: new Rgb(0.82f, 0.78f, 0.68f), foliage: new Rgb(0.24f, 0.31f, 0.19f));

        // ---- the Kangaroo Valley's trees (WG.2c, 2026-09-25): first colours from PlantNET's descriptions, in sRGB as the rows
        // above are (the meshes make them linear), for William's eyes ----

        /// <summary>Sydney blue gum: rough grey-brown bark at the foot, smooth pale blue-grey above, a glossy dark crown.</summary>
        public static readonly TreeForm SydneyBlueGum = new TreeForm(TreeGeometries.SydneyBlueGum,
            barkLow: new Rgb(0.40f, 0.35f, 0.29f), barkHigh: new Rgb(0.78f, 0.80f, 0.79f), foliage: new Rgb(0.22f, 0.32f, 0.20f));

        /// <summary>Cabbage tree palm: a grey-brown ringed stem and glossy green fronds.</summary>
        public static readonly TreeForm CabbageTreePalm = new TreeForm(TreeGeometries.CabbageTreePalm,
            barkLow: new Rgb(0.44f, 0.40f, 0.35f), barkHigh: new Rgb(0.50f, 0.46f, 0.40f), foliage: new Rgb(0.28f, 0.40f, 0.22f));

        /// <summary>Silvertop ash: rough, dark compact bark on the trunk, smooth white upper limbs, a glossy crown.</summary>
        public static readonly TreeForm SilvertopAsh = new TreeForm(TreeGeometries.SilvertopAsh,
            barkLow: new Rgb(0.30f, 0.27f, 0.24f), barkHigh: new Rgb(0.80f, 0.78f, 0.73f), foliage: new Rgb(0.26f, 0.34f, 0.21f));

        /// <summary>River oak: grey-brown fissured bark, and a dark grey-green crown of fine branchlets.</summary>
        public static readonly TreeForm RiverOak = new TreeForm(TreeGeometries.RiverOak,
            barkLow: new Rgb(0.37f, 0.32f, 0.27f), barkHigh: new Rgb(0.40f, 0.35f, 0.30f), foliage: new Rgb(0.22f, 0.28f, 0.21f));

        /// <summary>Scribbly gum: smooth white bark all over, a grey-green crown.</summary>
        public static readonly TreeForm ScribblyGum = new TreeForm(TreeGeometries.ScribblyGum,
            barkLow: new Rgb(0.80f, 0.79f, 0.74f), barkHigh: new Rgb(0.84f, 0.83f, 0.78f), foliage: new Rgb(0.34f, 0.40f, 0.28f));

        /// <summary>Lilly pilly: a smooth light brown trunk and a deep, dark, glossy crown.</summary>
        public static readonly TreeForm LillyPilly = new TreeForm(TreeGeometries.LillyPilly,
            barkLow: new Rgb(0.48f, 0.42f, 0.36f), barkHigh: new Rgb(0.52f, 0.46f, 0.40f), foliage: new Rgb(0.16f, 0.29f, 0.15f));

        /// <summary>A fallen stick's weathered grey-brown.</summary>
        public static readonly Rgb Stick = new Rgb(0.45f, 0.38f, 0.30f);

        /// <summary>A cobble's grey; which stone it is waits for the stone to travel with it.</summary>
        public static readonly Rgb Cobble = new Rgb(0.56f, 0.53f, 0.48f);

        /// <summary>
        /// A rock that stands, by its stone (BF.4 stage three), a first model for William's eyes: the sandstone weathered grey-buff,
        /// between the ground palette's dry and wet rock, with a rust stain; quartzite and quartz pale, silcrete and chert grey-brown,
        /// rhyolite pinkish, basalt near black, granite a speckled grey, shale dark. Written as the ground palette is, in sRGB; the
        /// meshes make them linear for the stand shader (StandMeshes). The first frames (2026-09-25) drew the sandstone a fresh buff,
        /// and on the valley's shaded walls every boulder stood out as a pale spot on the grey rock it lay on.
        /// </summary>
        public static Rgb RockOf(StoneType stone)
        {
            if (ReferenceEquals(stone, StoneType.Sandstone)) return new Rgb(0.52f, 0.47f, 0.40f);
            if (ReferenceEquals(stone, StoneType.Quartzite)) return new Rgb(0.74f, 0.72f, 0.68f);
            if (ReferenceEquals(stone, StoneType.Quartz)) return new Rgb(0.84f, 0.83f, 0.80f);
            if (ReferenceEquals(stone, StoneType.Silcrete)) return new Rgb(0.50f, 0.47f, 0.43f);
            if (ReferenceEquals(stone, StoneType.Chert)) return new Rgb(0.50f, 0.47f, 0.44f);
            if (ReferenceEquals(stone, StoneType.Flint)) return new Rgb(0.40f, 0.38f, 0.36f);
            if (ReferenceEquals(stone, StoneType.Rhyolite)) return new Rgb(0.62f, 0.52f, 0.50f);
            if (ReferenceEquals(stone, StoneType.Basalt)) return new Rgb(0.28f, 0.28f, 0.29f);
            if (ReferenceEquals(stone, StoneType.Granite)) return new Rgb(0.63f, 0.60f, 0.58f);
            if (ReferenceEquals(stone, StoneType.Shale)) return new Rgb(0.36f, 0.34f, 0.32f);
            if (ReferenceEquals(stone, StoneType.Obsidian)) return new Rgb(0.12f, 0.12f, 0.13f);
            return Cobble;
        }

        /// <summary>The rust a sandstone's iron leaves in streaks down its faces.</summary>
        public static readonly Rgb RockStain = new Rgb(0.52f, 0.38f, 0.26f);

        /// <summary>The pale grey-green of the lichen on a rock's sunlit top.</summary>
        public static readonly Rgb Lichen = new Rgb(0.56f, 0.58f, 0.50f);

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

        /// <summary>The taper a tree is drawn with: the engine's (<see cref="TreeGeometries.TrunkTipRadiusShare"/>), since the server's logs are cut to it too.</summary>
        public const double TrunkTipRadiusShare = TreeGeometries.TrunkTipRadiusShare;

        /// <summary>A trunk's radius at a height above its foot, m (M1.6b): the engine's line, so what stops a founder, what is stripped and what is cut is the wood that is drawn.</summary>
        public static double TrunkRadiusAt(TreeForm form, double heightM, double upM) => TreeGeometries.TrunkRadiusAt(form?.Geometry, heightM, upM);

        private static readonly TreeForm[] Forms =
        {
            Blackbutt, Bangalay, OldManBanksia, CoastBanksia, SwampPaperbark,
            SydneyBlueGum, CabbageTreePalm, SilvertopAsh, RiverOak, ScribblyGum, LillyPilly,
        };

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

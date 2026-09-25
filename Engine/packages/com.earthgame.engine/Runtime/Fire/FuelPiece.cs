using System;

namespace EarthGame.Engine
{
    /// <summary>Where a piece of fuel is in its burning (BF.5). Never renumbered: part two saves and sends it.</summary>
    public enum BurnPhase : byte
    {
        /// <summary>Laid and not yet caught; it may be heating toward catching.</summary>
        Unlit = 0,
        /// <summary>Its gases burn in a flame above it.</summary>
        Flaming = 1,
        /// <summary>No flame of its own: its char glows, and any wood left under the char smoulders and smokes.</summary>
        Glowing = 2,
        /// <summary>Burnt to ash.</summary>
        Spent = 3,
    }

    /// <summary>
    /// How big a piece of fuel is, in the words a person lights a fire by (BF.5): tinder catches from a coal, kindling from
    /// tinder's flame, fuel from kindling's fire. Only the words read it: what catches from what falls out of each piece's
    /// thickness and water in <see cref="Fire"/> and <see cref="Tinder"/>, never out of this label (GAME_DESIGN §8).
    ///
    /// <para>The boundary between kindling and fuel is the US National Fire Danger Rating System's between its 10-hour and
    /// 100-hour dead fuels, one inch (Deeming, Burgan and Cohen 1977, "The National Fire-Danger Rating System — 1978", USDA
    /// Forest Service General Technical Report INT-39; the classes are Fosberg's time-lags of 1970, a dead stick's water
    /// coming to the air's in about 1, 10, 100 or 1000 hours by its thickness). The boundary between tinder and kindling,
    /// 1 mm, is an estimate: the grass blades, needles and shredded bark that glowing embers light in the firebrand studies
    /// are all under it.</para>
    /// </summary>
    public enum FuelClass : byte
    {
        Tinder = 0,
        Kindling = 1,
        Fuel = 2,
    }

    /// <summary>The fuel classes by thickness (BF.5), the one owner of their boundaries.</summary>
    public static class FuelClasses
    {
        /// <summary>Fibres, grass blades and shavings finer than this are tinder, m (an estimate; see <see cref="FuelClass"/>).</summary>
        public const double TinderUnderM = 0.001;
        /// <summary>Sticks thicker than this are fuel rather than kindling, m: the NFDRS's 10-hour class ends at one inch.</summary>
        public const double FuelFromM = 0.0254;

        public static FuelClass Of(double thicknessM) =>
            thicknessM < TinderUnderM ? FuelClass.Tinder : thicknessM < FuelFromM ? FuelClass.Kindling : FuelClass.Fuel;

        public static string WordFor(FuelClass c) => c == FuelClass.Tinder ? "tinder" : c == FuelClass.Kindling ? "kindling" : "fuel wood";
    }

    /// <summary>
    /// One piece of fuel on a fire (BF.5): a stick, or a bundle of fine fibre, with its wood, its size, the dry wood and the
    /// water in it, the char its burning has left, and where it is in its burning. What it does is decided by
    /// <see cref="Fire.Advance"/> from these numbers and nothing else; a piece is never told what kind of fuel it is.
    ///
    /// <para><b>A stick</b> is a cylinder of the wood's density: it burns from its surface inward, so its thickness shrinks
    /// and its mass goes as the square of it. <b>A bundle</b> (tinder) is fibres of a fibre's thickness held loosely in a ball
    /// of <see cref="BundleM"/> across: each fibre would burn through in a moment, so the bundle burns as fast as a flame can
    /// cross it (<see cref="Combustion.BundleSpreadMs"/>), and it shows the fire its outside, not its fibres.</para>
    ///
    /// <para>The mass is dry mass. A thing of BF.1 carries its mass wet (its dry mass × (1 + moisture), as
    /// <c>LyingProperties</c> weighs a stick); <see cref="OfStick"/> takes the water back out.</para>
    /// </summary>
    public sealed class FuelPiece
    {
        /// <summary>The wood it is, or null for fibre of no wood in the table (a grass, a sedge, a bark of no row).</summary>
        public Wood Wood { get; }
        /// <summary>The dry density of the solid, kg/m³: a stick's wood, a fibre's own substance (not the loose bundle's).</summary>
        public double DensityKgM3 { get; }
        public double LengthM { get; }
        /// <summary>Its thickness when laid on the fire, m: a stick's diameter, a bundle's fibres'.</summary>
        public double StartThicknessM { get; }
        /// <summary>A bundle's size across, m; zero for a stick.</summary>
        public double BundleM { get; }
        public double StartDryKg { get; }

        /// <summary>Its thickness now, m: a stick's burns down from its surface; a bundle's fibres stay as they were.</summary>
        public double ThicknessM { get; internal set; }
        /// <summary>Dry wood not yet burnt or charred, kg.</summary>
        public double WoodKg { get; internal set; }
        /// <summary>Water still in the wood, kg.</summary>
        public double WaterKg { get; internal set; }
        /// <summary>Char the flame has left on it and the glow has not yet burnt, kg: its embers.</summary>
        public double CharKg { get; internal set; }
        /// <summary>How far it has heated toward catching, 0 to 1; it catches at 1 and cools back when the fire around it fails.</summary>
        public double Ignition01 { get; internal set; }
        public BurnPhase Phase { get; internal set; }
        /// <summary>The heat it has given the fire so far, J: what its flame and its glow released, less what its water cost.</summary>
        public double HeatGivenJ { get; internal set; }

        private FuelPiece(Wood wood, double densityKgM3, double lengthM, double thicknessM, double bundleM, double dryKg, double moisture)
        {
            if (!(densityKgM3 > 0.0) || !(lengthM > 0.0) || !(thicknessM > 0.0) || !(dryKg > 0.0) || !(moisture >= 0.0) || !(bundleM >= 0.0)
                || double.IsInfinity(densityKgM3) || double.IsInfinity(lengthM) || double.IsInfinity(thicknessM) || double.IsInfinity(dryKg) || double.IsInfinity(moisture) || double.IsInfinity(bundleM))
                throw new ArgumentOutOfRangeException(nameof(dryKg), "a piece of fuel wants a positive density, length, thickness and mass, and a moisture of none or more");
            Wood = wood;
            DensityKgM3 = densityKgM3;
            LengthM = lengthM;
            StartThicknessM = thicknessM;
            ThicknessM = thicknessM;
            BundleM = bundleM;
            StartDryKg = dryKg;
            WoodKg = dryKg;
            WaterKg = dryKg * moisture;
            Phase = BurnPhase.Unlit;
        }

        /// <summary>The dry density of a stick of no wood in the table, kg/m³: <c>Work</c>'s middling hardwood.</summary>
        public const double PlainDensityKgM3 = 600.0;

        /// <summary>
        /// A stick of a wood, its mass what the wood's density gives a cylinder of that size (as <c>LyingProperties</c> weighs one).
        /// The density read is the table's <see cref="Wood.DensityDryKgM3"/>, read as dry mass to volume, as BF.1 reads it.
        /// </summary>
        public static FuelPiece Stick(Wood wood, double diameterM, double lengthM, double moisture)
        {
            double density = wood != null ? wood.DensityDryKgM3 : PlainDensityKgM3;
            double dry = density * Math.PI / 4.0 * diameterM * diameterM * lengthM;
            return new FuelPiece(wood, density, lengthM, diameterM, 0.0, dry, moisture);
        }

        /// <summary>A stick whose dry mass is known (a thing's own), its density what that mass makes of its size.</summary>
        public static FuelPiece Stick(Wood wood, double diameterM, double lengthM, double moisture, double dryKg)
        {
            double volume = Math.PI / 4.0 * diameterM * diameterM * lengthM;
            return new FuelPiece(wood, volume > 0.0 ? dryKg / volume : 0.0, lengthM, diameterM, 0.0, dryKg, moisture);
        }

        /// <summary>
        /// A bundle of tinder: <paramref name="dryKg"/> of fibre <paramref name="fibreM"/> thick, of a substance
        /// <paramref name="fibreDensityKgM3"/> dense, held in a ball <paramref name="bundleM"/> across.
        /// </summary>
        public static FuelPiece Bundle(Wood wood, double dryKg, double fibreM, double fibreDensityKgM3, double bundleM, double moisture)
        {
            if (!(bundleM > 0.0)) throw new ArgumentOutOfRangeException(nameof(bundleM), "a bundle has a size");
            return new FuelPiece(wood, fibreDensityKgM3, bundleM, fibreM, bundleM, dryKg, moisture);
        }

        /// <summary>
        /// A thing of BF.1 as a stick on a fire: its wood, and its own diameter, length, water and mass where it has them (the
        /// kind's where not, as <c>Work</c> reads them); null for a thing that is not wood.
        /// </summary>
        public static FuelPiece OfStick(Definition definition, in ThingState state)
        {
            if (definition == null || definition.Substance != Substance.Wood) return null;
            Wood wood = DefinitionCatalogue.WoodOf(definition);
            double d = state.Has(ThingFields.Diameter) ? state.DiameterM : 2.0 * definition.RadiusM;
            double length = state.Has(ThingFields.Length) ? state.LengthM : 1.0;
            double moisture = state.Has(ThingFields.Moisture) ? state.Moisture : 0.15;
            if (!(d > 0.0) || !(length > 0.0)) return null;
            if (!state.Has(ThingFields.Mass)) return Stick(wood, d, length, moisture);
            return Stick(wood, d, length, moisture, state.MassKg / (1.0 + moisture));
        }

        /// <summary>Whether it is a bundle of fibre rather than a stick.</summary>
        public bool IsBundle => BundleM > 0.0;

        /// <summary>Its water as a share of the dry wood left: the moisture it burns at.</summary>
        public double Moisture => WoodKg > 0.0 ? WaterKg / WoodKg : 0.0;

        /// <summary>Its fuel class by the thickness it was laid at.</summary>
        public FuelClass Class => FuelClasses.Of(StartThicknessM);

        /// <summary>Whether it burns: in flame or glowing.</summary>
        public bool IsAlight => Phase == BurnPhase.Flaming || Phase == BurnPhase.Glowing;

        /// <summary>
        /// A stick's thickness over its char, m: the unburnt wood and the char round it, the char taking half the space of the
        /// wood it came from (<see cref="Combustion.CharVolumeShare"/>). What glows and what the fire sees.
        /// </summary>
        public double OuterThicknessM
        {
            get
            {
                if (IsBundle) return BundleM;
                double charM3 = CharKg > 0.0 ? Combustion.CharVolumeShare * CharKg / (Combustion.CharYield * DensityKgM3) : 0.0;
                return Math.Sqrt(ThicknessM * ThicknessM + 4.0 * charM3 / (Math.PI * LengthM));
            }
        }

        /// <summary>
        /// The outside it shows the fire, m²: a stick's round surface over its char (its ends left out, a stick being long for
        /// its thickness); a bundle's ball, since its fibres face one another and not the fire.
        /// </summary>
        public double SurfaceM2 => IsBundle ? Math.PI * BundleM * BundleM : Math.PI * OuterThicknessM * LengthM;

        /// <summary>Its mass as a thing would weigh it now, kg: the wood, its water and its char.</summary>
        public double MassKg => WoodKg + WaterKg + CharKg;
    }
}

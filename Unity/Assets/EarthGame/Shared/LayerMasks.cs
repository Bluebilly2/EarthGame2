using UnityEngine;

namespace EarthGame.Shared
{
    /// <summary>
    /// The one place a layer is named. Rust's gameplay code is full of raw integer masks nobody can read or
    /// verify; here every query names its mask, and the project setup writes these names into the TagManager so
    /// the numbers and the names cannot drift apart (an edit-mode test checks them).
    /// </summary>
    public static class Layers
    {
        public const int Terrain = 8;
        public const int Props = 9;
        public const int Player = 10;
        public const int Fauna = 11;
        public const int Water = 12;

        public static readonly string[] Names = { "Terrain", "Props", "Player", "Fauna", "Water" };
        public static readonly int[] Indices = { Terrain, Props, Player, Fauna, Water };

        /// <summary>What the mover collides with: the ground and the things standing on it, never other bodies.</summary>
        public static int Walkable => (1 << Terrain) | (1 << Props);

        /// <summary>What the crosshair may look at.</summary>
        public static int Interactable => (1 << Props) | (1 << Fauna) | (1 << Water) | (1 << Terrain);

        public static int Mask(int layer) => 1 << layer;
    }
}

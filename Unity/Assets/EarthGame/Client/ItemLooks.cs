using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// What an item looks like (M1.5a): the stand's own meshes (<see cref="StandMeshes"/>, from <c>StandForms</c>), so a
    /// stick picked up, carried and put down is drawn as the sticks lying in the litter are. It replaced M1.3's prefab
    /// registry, whose placeholder prefabs drew a spawned stick unlike a lying one with nothing to make the two agree
    /// (DEBTS, 2026-09-11). A thing's variant is the look its state carries — the shape it lay in, kept wherever it goes
    /// (BF.1) — and its id's for a thing that lay nowhere.
    /// </summary>
    public static class ItemLooks
    {
        /// <summary>The mesh an item is drawn by and the scale it is drawn at; false for a definition that has no look.</summary>
        public static bool TryLook(Definition definition, ulong id, in ThingState state, out Mesh mesh, out float scale)
        {
            int variant = state.Has(ThingFields.Look) ? state.Look % StandPreparation.Variants : (int)(id % (ulong)StandPreparation.Variants);
            if (definition != null && definition.Substance == Substance.Wood)
            {
                mesh = StandMeshes.Stick(variant);
                scale = 1f;
                return true;
            }
            if (definition != null && definition.Substance == Substance.Bark)
            {
                mesh = StandMeshes.Strip(variant);
                scale = 1f;
                return true;
            }
            if (definition != null && definition.Substance == Substance.Cord)
            {
                mesh = StandMeshes.Cord(variant);
                scale = 1f;
                return true;
            }
            // The plain cobble and every stone's cobble (M1.5b) are one shape; a cobble's stone is in what it is, not how it looks yet.
            if (definition != null && definition.Substance == Substance.Stone)
            {
                mesh = StandMeshes.Cobble(variant);
                scale = StandViews.CobbleSizeM;
                return true;
            }
            mesh = null;
            scale = 1f;
            return false;
        }

        /// <summary>The mesh a thing lying in the litter is drawn by, in the variant its cell gives it, and the scale it is drawn at (M1.5b): the stand's own.</summary>
        public static void LyingLook(StandLayout.Kind kind, int variant, out Mesh mesh, out float scale)
        {
            if (kind == StandLayout.Kind.Cobble)
            {
                mesh = StandMeshes.Cobble(variant);
                scale = StandViews.CobbleSizeM;
            }
            else
            {
                mesh = StandMeshes.Stick(variant);
                scale = 1f;
            }
        }

        /// <summary>Every spawnable definition without a look, one line each; empty when every one has one.</summary>
        public static List<string> Audit()
        {
            List<string> wrong = new List<string>();
            foreach (Definition d in DefinitionCatalogue.Spawnable)
                if (!TryLook(d, 1, default, out Mesh mesh, out _) || mesh == null) wrong.Add("spawnable '" + d.Key + "' has no look");
            return wrong;
        }
    }
}

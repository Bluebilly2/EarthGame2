using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// What an animal looks like, grown as geometry (M1.7b, CANON ruling 29: the looks are made here, nothing is bought or
    /// downloaded). <see cref="AnimalShapes"/> says what a kangaroo and an oystercatcher are made of — a skeleton of fixed
    /// bones, and one skin of closed shells grown over it in its resting pose with its colours in its corners — and this
    /// turns that skin into one mesh a kind, built once and kept as <see cref="StandMeshes"/> keeps the trees, and hangs it
    /// on a skinned renderer whose bones are the skeleton's: every frame the bones are placed for the animal's pose and the
    /// graphics card moves the skin with them.
    ///
    /// <para>Until 2026-09-16 an animal was a child object with a rigid mesh for each piece, and William saw "a load of 3d
    /// shapes put together" in the first frames. A skin that bends with its bones is what "smooth and connected" costs:
    /// the shells overlap and nest at the joints so no gap can open, and a fleeing mob costs a transform per bone rather
    /// than a mesh rebuilt a frame.</para>
    ///
    /// <para>Every face is one plane of one colour, faceted like everything else this game grows: the stand's shader takes
    /// a face's normal and colour from its leading corner and interpolates neither (ARCHITECTURE §8), so the skin's corners
    /// are shared between faces as the trees' are, each face leading with a corner of its own.</para>
    /// </summary>
    public static class AnimalLooks
    {
        /// <summary>
        /// How far from the eye the animals' material draws, m. The stand's shader puts an instance in the near band when
        /// it lies within this of the eye the client hands it and folds it away otherwise (<c>EarthGame/StandLit</c>); the
        /// animals have one band and no far form, so the split is set past any distance a world holds and the eye the
        /// material carries is never read. The trees' two bands are the reason that machinery exists, not this.
        /// </summary>
        public const float ReachM = 1.0e6f;

        /// <summary>How far apart in its own cycle two animals start, s: enough that a mob does not hop as one machine.</summary>
        private const double SpreadSeconds = 4.0;

        private static readonly Dictionary<AnimalSpecies, Mesh> Bodies = new Dictionary<AnimalSpecies, Mesh>();

        /// <summary>The kind a definition names, or null for anything that is not an animal this build has shapes for.</summary>
        public static AnimalSpecies SpeciesOf(Definition definition)
        {
            if (definition == null || definition.Kind != DefinitionKind.Animal) return null;
            return definition.Row is AnimalSpecies species && AnimalShapes.Draws(species) ? species : null;
        }

        /// <summary>
        /// Where in its own hop or wingbeat an animal of an id is: a start of its own, drawn from the id, so the members of
        /// a mob bound out of step with each other as real ones do.
        /// </summary>
        public static double StartOf(ulong id)
        {
            ulong h = id * 0x9E3779B97F4A7C15UL;
            h ^= h >> 29;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 32;
            return (h >> 11) * (1.0 / 9007199254740992.0) * SpreadSeconds;
        }

        /// <summary>
        /// The mesh a kind is drawn by, built on first use and kept: the skin's corners with their planes and colours, the
        /// bone each corner rides and its share of a second, and the bind poses of the resting skeleton the skin was grown
        /// over, so the renderer can move the skin by where the bones are now against where they were then.
        /// </summary>
        public static Mesh Body(AnimalSpecies species)
        {
            if (Bodies.TryGetValue(species, out Mesh mesh)) return mesh;
            AnimalBody body = AnimalShapes.BodyOf(species);
            int count = body.VertexCount;
            Vector3[] vertices = new Vector3[count], normals = new Vector3[count];
            Color[] colours = new Color[count];
            BoneWeight[] weights = new BoneWeight[count];
            for (int v = 0; v < count; v++)
            {
                int point = body.VertexPoint[v];
                vertices[v] = ToVector(body.Points[point]);
                normals[v] = ToVector(body.VertexNormal[v]);
                Rgb c = body.VertexColour[v];
                colours[v] = new Color(c.R, c.G, c.B, 1f);
                BoneWeight w = new BoneWeight { boneIndex0 = body.PointBone[point], weight0 = 1f };
                if (body.PointBlend[point] >= 0 && body.PointShare[point] < 1.0)
                {
                    w.weight0 = (float)body.PointShare[point];
                    w.boneIndex1 = body.PointBlend[point];
                    w.weight1 = 1f - w.weight0;
                }
                weights[v] = w;
            }
            mesh = new Mesh { name = species.Name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(body.Triangles, 0);
            mesh.boneWeights = weights;
            Matrix4x4[] bind = new Matrix4x4[body.Bind.Count];
            for (int b = 0; b < bind.Length; b++)
                bind[b] = Matrix4x4.TRS(ToVector(body.Bind[b].Joint), TurnOf(body.Bind[b]), Vector3.one).inverse;
            mesh.bindposes = bind;
            mesh.bounds = ReachOf(species);
            Bodies[species] = mesh;
            return mesh;
        }

        /// <summary>
        /// Dresses one animal's object (M1.7b): a child transform for each bone, placed in the resting pose the skin was
        /// grown over, and a skinned renderer on the object itself drawing the kind's mesh over those bones in the animals'
        /// material, casting a shadow (a kangaroo without one floats over the ground it stands on). Its bounds are the box
        /// the kind never leaves, since a skin that follows its bones has none of its own until it is drawn. The bones are
        /// returned in the skeleton's order for <see cref="Place"/> to move every frame.
        /// </summary>
        public static Transform[] Dress(GameObject root, AnimalSpecies species, Material material)
        {
            Mesh mesh = Body(species);
            List<AnimalBone> bind = new List<AnimalBone>();
            AnimalShapes.BindPose(species, bind);
            Transform[] bones = new Transform[bind.Count];
            for (int b = 0; b < bones.Length; b++)
            {
                GameObject bone = new GameObject(bind[b].Name);
                bone.transform.SetParent(root.transform, false);
                bones[b] = bone.transform;
            }
            Place(bones, bind);
            SkinnedMeshRenderer skin = root.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = bones;
            skin.rootBone = root.transform;
            skin.localBounds = mesh.bounds;
            skin.updateWhenOffscreen = false;
            // Two bones a corner is all the skin uses; asking for the quality setting's count could give it one and crease every bend.
            skin.quality = SkinQuality.Bone2;
            skin.sharedMaterial = material;
            skin.shadowCastingMode = ShadowCastingMode.On;
            skin.lightProbeUsage = LightProbeUsage.Off;
            skin.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return bones;
        }

        /// <summary>Puts each bone's transform at its joint, turned so its forward is the bone and its up the bone's deep axis, as the bind poses were taken.</summary>
        public static void Place(Transform[] bones, List<AnimalBone> pose)
        {
            int count = System.Math.Min(bones.Length, pose.Count);
            for (int b = 0; b < count; b++)
            {
                if (bones[b] == null) continue;
                bones[b].SetLocalPositionAndRotation(ToVector(pose[b].Joint), TurnOf(pose[b]));
            }
        }

        /// <summary>
        /// The material the animals are drawn in: a copy of the stand's, which is an asset so that a build keeps its
        /// shader's instanced variants (ProjectSetup, ARCHITECTURE §8). The caller owns it and destroys it.
        /// </summary>
        public static Material MaterialFrom(Material template)
        {
            Material material = new Material(template) { name = "Animals", enableInstancing = true };
            material.SetFloat("_Band", 0f);
            material.SetFloat("_SplitM", ReachM);
            return material;
        }

        /// <summary>
        /// The one rotation a bone's frame is: its Along forward and its Deep up, which puts its Across on the local x as
        /// the frame is a proper one. The same call places the bones for the bind poses and for every frame, so whatever
        /// convention the look rotation keeps cancels between the two.
        /// </summary>
        private static Quaternion TurnOf(in AnimalBone bone) => Quaternion.LookRotation(ToVector(bone.Along), ToVector(bone.Deep));

        private static Bounds ReachOf(AnimalSpecies species)
        {
            AnimalShapes.ReachOf(species, out Double3 low, out Double3 high);
            Vector3 min = ToVector(low), max = ToVector(high);
            return new Bounds(0.5f * (min + max), max - min);
        }

        private static Vector3 ToVector(Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}

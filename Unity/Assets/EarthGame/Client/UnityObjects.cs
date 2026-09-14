using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// Frees a Unity object the client made and is done with (M1.4f, 2026-09-14). An object made with <c>new</c>, a
    /// TerrainData or a Mesh, is not collected when nothing refers to it and does not go with the GameObject that drew
    /// it: the bug hunt of 2026-09-13 found every tile a walk let go of leaving its terrain data and its water's mesh
    /// behind for the life of the process. In play the object goes at the end of the frame, as Unity destroys; in the
    /// editor's edit mode, where the tests run, it goes at once, since Destroy is refused there.
    /// </summary>
    public static class UnityObjects
    {
        public static void Free(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}

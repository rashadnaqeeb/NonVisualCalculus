using System.Numerics;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// A named world position the player saved to walk back to later. Scoped to one scene (the Unity
    /// scene name of the map it was saved on): the bookmarks menu only offers the current scene's.
    /// The position is in the mod's world frame (the frame every Core world type shares; the module's
    /// WorldConvert is the one boundary to Unity's), so a stored bookmark round-trips into the walk
    /// verb with no conversion.
    /// </summary>
    public sealed class Bookmark
    {
        public string Scene { get; }
        public string Name { get; }
        public Vector3 Position { get; }

        public Bookmark(string scene, string name, Vector3 position)
        {
            Scene = scene;
            Name = name;
            Position = position;
        }
    }
}

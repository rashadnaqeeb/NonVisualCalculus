using Sunshine;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The game's own world-click gate, shared by everything mod-driven that acts on the game. Any held
    /// input lock (<c>GameController.inputLocks</c> - a scripted scene still animating after a dialogue's
    /// last line, a sequencer camera move, a door transition, quicktravel) means the game would ignore a
    /// world click right now: its own click paths check this before moving, so a mod-driven move, screen
    /// open, or item use must refuse too (spoken, never a silent dead key). No controller at all reads as
    /// locked - the world cannot be driven without one.
    /// </summary>
    internal static class GameInputLock
    {
        public static bool Held
        {
            get
            {
                GameController gc = GameController.Singleton;
                return gc == null || gc.IsWorldInputDisabled();
            }
        }
    }
}

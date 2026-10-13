namespace NonVisualCalculus.Core.World
{
    /// <summary>A world thing with a stable data key the authored descriptions address: an entity's exact
    /// GameObject name, an orb's name. Separate from <see cref="IWorldItem.Name"/>, which is the cleaned
    /// spoken name and may be shared by several things.</summary>
    public interface IKeyedWorldItem
    {
        string Key { get; }
    }
}

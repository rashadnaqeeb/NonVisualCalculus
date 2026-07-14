namespace NonVisualCalculus.Core.Settings
{
    /// <summary>
    /// The persistence seam behind the mod's settings: load a value (returning the default when nothing is
    /// stored) and save one. Kept an interface so Core stays free of any BepInEx/file reference - the host
    /// implements it over its BepInEx <c>ConfigFile</c>, and the tests over an in-memory fake.
    /// </summary>
    public interface ISettingsStore
    {
        bool GetBool(string key, bool defaultValue);
        void SetBool(string key, bool value);

        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
    }
}

using System.Collections.Generic;
using DiscoAccess.Core.Settings;
using Xunit;

namespace DiscoAccess.Tests
{
    public class ModSettingsTests
    {
        // An in-memory stand-in for the host's BepInEx-backed store.
        private sealed class FakeStore : ISettingsStore
        {
            public readonly Dictionary<string, bool> Saved = new Dictionary<string, bool>();
            public readonly Dictionary<string, int> SavedInts = new Dictionary<string, int>();
            public bool GetBool(string key, bool defaultValue) => Saved.TryGetValue(key, out bool v) ? v : defaultValue;
            public void SetBool(string key, bool value) => Saved[key] = value;
            public int GetInt(string key, int defaultValue) => SavedInts.TryGetValue(key, out int v) ? v : defaultValue;
            public void SetInt(string key, int value) => SavedInts[key] = value;
        }

        [Fact]
        public void AutoReadDialogue_DefaultsOn_WhenNothingStored()
        {
            var settings = new ModSettings(new FakeStore());
            Assert.True(settings.AutoReadDialogue.Value);
        }

        [Fact]
        public void Setting_LoadsStoredValue_OverDefault()
        {
            var store = new FakeStore();
            store.Saved["auto_read_dialogue"] = false;

            var settings = new ModSettings(store);

            Assert.False(settings.AutoReadDialogue.Value);
        }

        [Fact]
        public void Toggle_FlipsAndPersists()
        {
            var store = new FakeStore();
            var settings = new ModSettings(store);

            bool now = settings.AutoReadDialogue.Toggle();

            Assert.False(now);
            Assert.False(settings.AutoReadDialogue.Value);
            Assert.False(store.Saved["auto_read_dialogue"]);
            // A fresh ModSettings over the same store reads the persisted value back.
            Assert.False(new ModSettings(store).AutoReadDialogue.Value);
        }

        [Fact]
        public void All_ListsEverySetting()
        {
            var settings = new ModSettings(new FakeStore());
            Assert.Contains(settings.AutoReadDialogue, settings.All);
            Assert.Contains(settings.WallToneVolume, settings.All);
            Assert.Contains(settings.WallTonesContinuous, settings.All);
        }

        [Fact]
        public void WallToneVolume_DefaultsToHalfAndConvertsToFraction()
        {
            var settings = new ModSettings(new FakeStore());
            Assert.Equal(50, settings.WallToneVolume.Value);
            Assert.Equal(0.5f, settings.WallToneVolume.Fraction);
        }

        [Fact]
        public void WallTonesContinuous_DefaultsOff()
        {
            var settings = new ModSettings(new FakeStore());
            Assert.False(settings.WallTonesContinuous.Value);
        }

        [Fact]
        public void RangeSetting_StepsClampPersistAndReportBounds()
        {
            var store = new FakeStore();
            store.SavedInts["wall_tone_volume"] = 100; // start at the ceiling to exercise the upper bound
            var settings = new ModSettings(store);
            var vol = settings.WallToneVolume;

            // At the maximum, a further increase changes nothing and reports no move (the menu reads "maximum").
            Assert.False(vol.Increase());
            Assert.Equal(100, vol.Value);

            // A decrease steps by 5 and persists.
            Assert.True(vol.Decrease());
            Assert.Equal(95, vol.Value);
            Assert.Equal(95, store.SavedInts["wall_tone_volume"]);
            Assert.Equal(0.95f, vol.Fraction);

            // A fresh ModSettings over the same store reads the persisted value back.
            Assert.Equal(95, new ModSettings(store).WallToneVolume.Value);
        }

        [Fact]
        public void RangeSetting_FloorsAtZero()
        {
            var store = new FakeStore();
            store.SavedInts["wall_tone_volume"] = 5;
            var vol = new ModSettings(store).WallToneVolume;

            Assert.True(vol.Decrease());
            Assert.Equal(0, vol.Value);
            Assert.False(vol.Decrease()); // already at the floor
            Assert.Equal(0, vol.Value);
        }

        [Fact]
        public void Sonar_Defaults_WhenMovingAllCategoriesWotrRest()
        {
            var settings = new ModSettings(new FakeStore());
            Assert.False(settings.SonarContinuous.Value);
            Assert.True(settings.SonarNpcs.Value);
            Assert.True(settings.SonarInteractables.Value);
            Assert.True(settings.SonarContainers.Value);
            Assert.True(settings.SonarOrbs.Value);
            Assert.True(settings.SonarExits.Value);
            Assert.Equal(400, settings.SonarRest.Value); // WOTR's rest-between-sweeps default
            Assert.Equal(RangeUnit.Milliseconds, settings.SonarRest.Unit);
            foreach (var s in new ModSetting[]
                     {
                         settings.SonarContinuous, settings.SonarRest, settings.SonarNpcs,
                         settings.SonarInteractables, settings.SonarContainers, settings.SonarOrbs,
                         settings.SonarExits,
                     })
                Assert.Contains(s, settings.All);
        }

        [Fact]
        public void ScannerFromCursor_DefaultsOff()
        {
            var settings = new ModSettings(new FakeStore());
            Assert.False(settings.ScannerFromCursor.Value);
            Assert.Contains(settings.ScannerFromCursor, settings.All);
        }

        [Fact]
        public void SonarCategoryEnabled_MapsEveryScanCategoryToItsToggle()
        {
            var settings = new ModSettings(new FakeStore());
            settings.SonarContainers.Toggle();

            Assert.False(settings.SonarCategoryEnabled(DiscoAccess.Core.World.WorldTaxonomy.Container));
            foreach (string cat in DiscoAccess.Core.World.WorldTaxonomy.Scan)
                if (cat != DiscoAccess.Core.World.WorldTaxonomy.Container)
                    Assert.True(settings.SonarCategoryEnabled(cat));
        }

        [Fact]
        public void SonarRest_ClampsToItsOwnSpan_NotThePercentScale()
        {
            var store = new FakeStore();
            store.SavedInts["sonar_rest"] = 9999; // a stale/hand-edited store value
            var rest = new ModSettings(store).SonarRest;

            Assert.Equal(1500, rest.Value); // clamped to the setting's own maximum
            Assert.False(rest.Increase());
            Assert.True(rest.Decrease());
            Assert.Equal(1450, rest.Value); // steps by 50
        }

        [Fact]
        public void Fraction_ThrowsForANonPercentRange()
        {
            var settings = new ModSettings(new FakeStore());
            Assert.Throws<System.InvalidOperationException>(() => _ = settings.SonarRest.Fraction);
        }
    }
}

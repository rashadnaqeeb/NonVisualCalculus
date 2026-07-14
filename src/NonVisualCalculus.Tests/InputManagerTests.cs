using System.Collections.Generic;
using NonVisualCalculus.Core.Input;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class InputManagerTests
    {
        // A binding with directly settable phase state and an id-derived chord, so two bindings sharing an
        // id collide on the same chord (the shadowing case).
        private sealed class FakeBinding : InputBinding
        {
            private readonly string _id;
            public bool Down, IsHeld;

            public FakeBinding(string id) { _id = id; }

            public override string DisplayName => _id;
            public override bool JustPressed() => Down;
            public override bool Held() => IsHeld;
            public override bool Released() => false; // no current code path dispatches on release
            public override string Type => "fake";
            public override string Serialize() => _id;
        }

        [Fact]
        public void JustPressed_FiresPerformed()
        {
            var mgr = new InputManager();
            int fired = 0;
            var binding = new FakeBinding("a");
            mgr.Register("g", "g", InputCategory.Global, () => fired++).AddBinding(binding);

            binding.Down = binding.IsHeld = true;
            mgr.Tick(0);

            Assert.Equal(1, fired);
        }

        [Fact]
        public void Dispatcher_Consuming_SuppressesPerformed()
        {
            var mgr = new InputManager();
            int fired = 0;
            var binding = new FakeBinding("a");
            mgr.Register("g", "g", InputCategory.Global, () => fired++).AddBinding(binding);
            mgr.JustPressedDispatcher = _ => true; // navigator swallows it

            binding.Down = binding.IsHeld = true;
            mgr.Tick(0);

            Assert.Equal(0, fired);
        }

        [Fact]
        public void SuppressAll_StandsDownEntirely()
        {
            var mgr = new InputManager();
            int fired = 0;
            var binding = new FakeBinding("a");
            mgr.Register("g", "g", InputCategory.Global, () => fired++).AddBinding(binding);
            mgr.SuppressAll = () => true;

            binding.Down = binding.IsHeld = true;
            mgr.Tick(0);

            Assert.Equal(0, fired);
        }

        [Fact]
        public void HigherCategory_ShadowsSameChord_InLowerCategory()
        {
            var mgr = new InputManager();
            int uiFired = 0, globalFired = 0;
            // Same id -> same chord on both bindings; both report the key down this frame.
            var uiBinding = new FakeBinding("shared") { Down = true, IsHeld = true };
            var globalBinding = new FakeBinding("shared") { Down = true, IsHeld = true };
            mgr.Register("ui", "ui", InputCategory.UI, () => uiFired++).AddBinding(uiBinding);
            mgr.Register("g", "g", InputCategory.Global, () => globalFired++).AddBinding(globalBinding);
            // UI declared (highest priority); Global is always appended below it.
            mgr.ActiveCategoriesProvider = () => new[] { InputCategory.UI };

            mgr.Tick(0);

            Assert.Equal(1, uiFired);     // higher-priority category owns the chord
            Assert.Equal(0, globalFired); // its lower twin is shadowed
        }

        [Fact]
        public void UndeclaredCategory_IsNotLive()
        {
            var mgr = new InputManager();
            int uiFired = 0;
            var binding = new FakeBinding("a") { Down = true, IsHeld = true };
            mgr.Register("ui", "ui", InputCategory.UI, () => uiFired++).AddBinding(binding);
            // No provider: only Global is live, so a UI action never fires.

            mgr.Tick(0);

            Assert.Equal(0, uiFired);
        }

        [Fact]
        public void Held_ReflectsLiveState()
        {
            var mgr = new InputManager();
            var binding = new FakeBinding("a");
            mgr.Register("g", "g", InputCategory.Global).AddBinding(binding);

            mgr.Tick(0);
            Assert.False(mgr.Held("g"));

            binding.IsHeld = true;
            mgr.Tick(0);
            Assert.True(mgr.Held("g"));
        }

        [Fact]
        public void Repeating_FiresAfterDelay_ThenAtInterval()
        {
            var mgr = new InputManager { InitialDelay = 0.4, RepeatInterval = 0.06 };
            int fired = 0;
            var binding = new FakeBinding("a");
            mgr.Register("g", "g", InputCategory.Global, () => fired++).AddBinding(binding).Repeating();

            binding.Down = binding.IsHeld = true;
            mgr.Tick(0.0);   // initial press
            Assert.Equal(1, fired);

            binding.Down = false; // JustPressed is true for one frame only; the key stays held
            mgr.Tick(0.1);   // before the initial delay
            Assert.Equal(1, fired);

            mgr.Tick(0.4);   // delay elapsed -> first repeat
            Assert.Equal(2, fired);

            mgr.Tick(0.45);  // before the next interval
            Assert.Equal(2, fired);

            mgr.Tick(0.46);  // interval elapsed -> second repeat
            Assert.Equal(3, fired);
        }

        [Fact]
        public void Repeating_DoesNotFire_WhenHeldWithoutInitialPress()
        {
            var mgr = new InputManager();
            int fired = 0;
            // Held from the start but never JustPressed under this manager (e.g. a shared key whose
            // modifier was released): must not auto-repeat a press that never happened.
            var binding = new FakeBinding("a") { IsHeld = true };
            mgr.Register("g", "g", InputCategory.Global, () => fired++).AddBinding(binding).Repeating();

            mgr.Tick(0.0);
            mgr.Tick(1.0);

            Assert.Equal(0, fired);
        }
    }
}

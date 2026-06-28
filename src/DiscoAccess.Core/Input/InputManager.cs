using System;
using System.Collections.Generic;

namespace DiscoAccess.Core.Input
{
    /// <summary>
    /// Registry plus per-frame poll, ticked from the module's pump. Actions live in CATEGORIES
    /// (<see cref="InputCategory"/>); each frame the live categories are the union of the active screens'
    /// declarations (via <see cref="ActiveCategoriesProvider"/>, in priority order) plus
    /// <see cref="InputCategory.Global"/>. Categories are walked in priority order marking bindings live
    /// and shadowing any identical chord already claimed by a higher-priority category, so the same chord
    /// in two live categories resolves to the higher one. A live JustPressed is offered to
    /// <see cref="JustPressedDispatcher"/> first (the navigator, for UI keys); if unconsumed it fires the
    /// action's handler.
    ///
    /// An INSTANCE, not a static singleton: the feature module owns it, builds it in its load, and drops
    /// it on reload, so registrations never accumulate across hot-reloads. Engine-free (the Unity poll
    /// lives in the module's concrete <see cref="InputBinding"/>); the screen/navigation couplings are
    /// the delegate seams below, set by the module.
    /// </summary>
    public sealed class InputManager
    {
        private readonly List<InputAction> _actions = new List<InputAction>();
        public IReadOnlyList<InputAction> Actions => _actions;

        /// <summary>The active screens' input categories this frame, highest-priority first, EXCLUDING
        /// Global (always appended). Null = no screens live, so only Global is live. Set by the module
        /// once a screen stack exists.</summary>
        public Func<IEnumerable<InputCategory>>? ActiveCategoriesProvider;

        /// <summary>First crack at a fired action (the navigator, for UI-category keys). Returns true if
        /// it consumed the press, suppressing the action's own handler. Null = nothing consumes.</summary>
        public Func<InputAction, bool>? JustPressedDispatcher;

        /// <summary>When this returns true the whole tick stands down so keys reach the game (the player
        /// is typing in a game text field, or a screen is capturing raw input). Null = never suppress.</summary>
        public Func<bool>? SuppressAll;

        /// <summary>Key-repeat timing (seconds). Defaults match a typical OS; the module may overwrite
        /// these from the real OS typematic settings.</summary>
        public double InitialDelay = 0.4;
        public double RepeatInterval = 0.06;

        public InputAction Register(string key, string label, InputCategory category, Action? onPerformed = null)
        {
            var action = new InputAction(key, label) { Category = category };
            if (onPerformed != null) action.Performed += onPerformed;
            _actions.Add(action);
            return action;
        }

        // The frame's live state, rebuilt at the top of Tick (cheap: a handful of actions x ~1 binding).
        private readonly List<InputCategory> _activeCats = new List<InputCategory>();
        private readonly HashSet<InputBinding> _live = new HashSet<InputBinding>();
        private readonly Dictionary<string, int> _chordRank = new Dictionary<string, int>();

        /// <summary>Whether the action with this key is currently held via a LIVE (unshadowed, active-
        /// category) binding - for per-frame polling (e.g. a held-arrow vector). A held key stops counting
        /// the instant a higher-priority claim takes its chord.</summary>
        public bool Held(string key)
        {
            for (int i = 0; i < _actions.Count; i++)
                if (_actions[i].Key == key) return HeldLive(_actions[i]);
            return false;
        }

        private bool JustPressedLive(InputAction a)
        {
            for (int i = 0; i < a.Bindings.Count; i++)
                if (_live.Contains(a.Bindings[i]) && a.Bindings[i].JustPressed()) return true;
            return false;
        }

        private bool HeldLive(InputAction a)
        {
            for (int i = 0; i < a.Bindings.Count; i++)
                if (_live.Contains(a.Bindings[i]) && a.Bindings[i].Held()) return true;
            return false;
        }

        // Live categories = the provider's declaration (priority order) + Global. Then walk categories in
        // priority order marking bindings live, shadowing any identical chord already claimed by an
        // earlier (higher-priority) category. Same-category duplicates are both live.
        private void RebuildLive()
        {
            _activeCats.Clear();
            var provided = ActiveCategoriesProvider?.Invoke();
            if (provided != null)
                foreach (var c in provided)
                    if (!_activeCats.Contains(c)) _activeCats.Add(c);
            if (!_activeCats.Contains(InputCategory.Global)) _activeCats.Add(InputCategory.Global);

            _live.Clear();
            _chordRank.Clear();
            for (int rank = 0; rank < _activeCats.Count; rank++)
            {
                var cat = _activeCats[rank];
                for (int i = 0; i < _actions.Count; i++)
                {
                    var a = _actions[i];
                    if (a.Category != cat) continue;
                    for (int j = 0; j < a.Bindings.Count; j++)
                    {
                        var b = a.Bindings[j];
                        if (_chordRank.TryGetValue(b.Chord, out int owner))
                        {
                            if (owner < rank) continue; // shadowed by a higher category
                        }
                        else _chordRank[b.Chord] = rank;
                        _live.Add(b);
                    }
                }
            }
        }

        public void Tick(double now)
        {
            if (SuppressAll != null && SuppressAll()) return;

            RebuildLive(); // this frame's category claims + chord shadowing

            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                bool held = HeldLive(action);

                bool fire = false;
                if (JustPressedLive(action))
                {
                    fire = true;
                    action.NextRepeatTime = now + InitialDelay;
                }
                else if (action.Repeats && held && action.NextRepeatTime > 0 && now >= action.NextRepeatTime)
                {
                    // Held past the delay -> auto-repeat. The NextRepeatTime > 0 guard means we only repeat
                    // an action that was actually JustPressed this hold, not one that just became held
                    // because a shared key's modifier was released (e.g. releasing Shift while holding Tab
                    // must not fire a stray forward Tab).
                    fire = true;
                    action.NextRepeatTime = now + RepeatInterval;
                }
                if (!held) action.NextRepeatTime = 0; // reset on release (disarms repeat until next press)

                if (!fire) continue;
                bool consumed = JustPressedDispatcher != null && JustPressedDispatcher(action);
                if (!consumed) action.InvokePerformed();
            }
        }
    }
}

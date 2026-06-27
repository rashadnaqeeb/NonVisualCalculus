using System;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiscoAccess.Module
{
    /// <summary>
    /// The reloadable focus reader, driven each frame by the host pump. Polls DE's NavigationManager for
    /// the current uGUI selection and, on a change, announces it through the host's speech pipeline
    /// (navigation interrupts, per the house rule): an options control reads as label, type, value, and
    /// tooltip via <see cref="OptionAdapter"/>; anything else via the generic <see cref="FocusReader"/>.
    /// It also announces an options tab switch, and re-announces just the value when a focused control
    /// changes in place (a slider adjusted, a toggle flipped). This is the implementor the host loads by
    /// interface scan; future dialogue/inventory/world readers and any Harmony patches join it here.
    /// </summary>
    public sealed class FocusModule : IModModule
    {
        private IModHost _host;
        private Harmony _harmony;
        private IntPtr _lastSelected = IntPtr.Zero;
        // The value-only readout of the focused options control, for detecting an in-place change
        // (adjusting a slider, toggling) where focus does not move. Null when the focus is not an
        // options control.
        private string _lastOptionValue;
        // The localized name of the active options tab, for announcing a tab switch. Null when the
        // options screen is not shown.
        private string _lastTab;

        public void Load(IModHost host)
        {
            _host = host;
            // A per-load id so a reload's Dispose unpatches exactly this load's patches. No patches yet;
            // future readers register them through this instance.
            _harmony = new Harmony("com.rashad.discoaccess.module");
        }

        public void Tick()
        {
            // A tab switch is announced before the focus it reveals, and makes that focus queue behind
            // it (both happen the same frame) so "Settings tab" is not cut off by the new control.
            bool tabAnnounced = TickTabs();

            Selectable selected = CurrentSelectable();
            // Dedup on the native address. (A destroyed Selectable's address can in principle be
            // reused by a new one; in practice a menu rebuild passes through a null-selection frame,
            // which resets this, so that collision effectively can't slip through.)
            IntPtr ptr = selected != null ? selected.Pointer : IntPtr.Zero;

            if (ptr != _lastSelected)
            {
                if (selected == null)
                {
                    _lastSelected = ptr;
                    _lastOptionValue = null;
                    return;
                }

                OptionState option = OptionAdapter.TryRead(selected, withTooltip: true);
                if (option != null)
                {
                    _host.Speech.Speak(OptionAnnouncer.Compose(option), interrupt: !tabAnnounced);
                    _lastOptionValue = OptionAnnouncer.ComposeValue(option);
                }
                else
                {
                    // A save/load entry and an archetype button each read cleanly through their own
                    // structured adapter; the generic sweep would speak a save entry's uppercased name and
                    // "| " timestamp dividers, or an archetype's stacked flip-clock animation layers as
                    // duplicated digits. Other focus falls through to the generic reader.
                    string text = Compose(selected);
                    if (!string.IsNullOrEmpty(text))
                        _host.Speech.Speak(text, interrupt: !tabAnnounced);
                    _lastOptionValue = null;
                }

                // Advance only after a successful read/speak, so an exception in the speech path (caught
                // and logged by the host pump) leaves the change un-acknowledged and retried next frame
                // rather than permanently suppressed.
                _lastSelected = ptr;
                return;
            }

            // Same control still focused: re-announce just the value when it changed in place (a slider
            // adjusted, a toggle flipped) so the user hears the result of their own input.
            if (selected != null && _lastOptionValue != null)
            {
                OptionState option = OptionAdapter.TryRead(selected, withTooltip: false);
                if (option != null)
                {
                    string value = OptionAnnouncer.ComposeValue(option);
                    if (value != _lastOptionValue)
                    {
                        _host.Speech.Speak(value, interrupt: true);
                        _lastOptionValue = value;
                    }
                }
            }
        }

        // Route a freshly focused control to its structured adapter, falling back to the generic label
        // sweep. Options are handled by the caller (their value is also polled for in-place changes).
        private static string Compose(Selectable selected)
        {
            SaveEntryState save = SaveEntryAdapter.TryRead(selected);
            if (save != null)
                return SaveEntryAnnouncer.Compose(save);

            ArchetypeState archetype = ArchetypeAdapter.TryRead(selected);
            if (archetype != null)
                return ArchetypeAnnouncer.Compose(archetype);

            return FocusReader.Read(selected);
        }

        // Announce the active options tab when it changes (and when options first opens), speaking the
        // game's localized tab name plus the authored "tab" word. Returns whether it spoke this frame.
        private bool TickTabs()
        {
            string tab = OptionAdapter.ActiveTabName();
            if (tab == _lastTab)
                return false;

            _lastTab = tab;
            if (string.IsNullOrEmpty(tab))
                return false;

            _host.Speech.Speak(tab + " " + Core.Strings.Strings.Tab, interrupt: true);
            return true;
        }

        // DE drives focus through NavigationManager, but at a freshly opened menu the uGUI EventSystem
        // records the selection a frame or more before NavigationManager does; fall back to it so the
        // initial focus is announced during that window (the dev FocusInspector/InputInjector do the same).
        private static Selectable CurrentSelectable()
        {
            var nav = NavigationManager.Singleton;
            Selectable sel = nav != null ? nav.GetCurrentSelectedSelectable() : null;
            if (sel != null)
                return sel;

            EventSystem es = EventSystem.current;
            GameObject go = es != null ? es.currentSelectedGameObject : null;
            return go != null ? go.GetComponent<Selectable>() : null;
        }

        public void Dispose()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            _host = null;
        }
    }
}

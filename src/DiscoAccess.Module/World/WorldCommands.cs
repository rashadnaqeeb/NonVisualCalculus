using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using Sunshine.Metric;
using Sunshine.Views;
using UnityEngine.UI;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The world hotkeys that act on the GAME rather than the mod's cursor: opening the information screens
    /// and the pause/help menus, the status readouts (time, money, health), and the gameplay quick-actions
    /// (heal items, hand items, quicksave/quickload, language). Kept separate from <see cref="WorldReader"/>,
    /// which owns the cursor and the sensing overlay, so each stays one concern.
    ///
    /// Because the world keyboard mutes InControl wholesale, each of these re-provides one muted game action
    /// by calling the game's own method directly (the same call its native input handler would make), never by
    /// pressing a key. The handlers are fired from the input pump, which logs any throw, so they trust the
    /// live singletons to exist (they do, in the in-world view this category is live in - except the global
    /// quicksave/quickload pair, which can fire before a world exists and guard for it).
    /// </summary>
    internal sealed class WorldCommands
    {
        private readonly IModHost _host;

        public WorldCommands(IModHost host) { _host = host; }

        // ---- Information screens: invoke the HUD menu button's own click, the path a sighted player takes,
        // so the game opens the view and our screen reader picks it up. Closing is Escape (the screen's Back).
        public void OpenInventory() => ClickHudMenu(HudMenuController.Current.inventory);
        public void OpenCharacterSheet() => ClickHudMenu(HudMenuController.Current.charsheet);
        public void OpenJournal() => ClickHudMenu(HudMenuController.Current.journal);
        public void OpenThoughtCabinet() => ClickHudMenu(HudMenuController.Current.thoughtcabinet);

        private static void ClickHudMenu(HudMenuButton button)
            => button.GetComponent<Button>().onClick.Invoke();

        // Pause and help open through the game's own view controller (the call its Escape/F1 handlers make).
        public void OpenPauseMenu() => ViewController.ToggleView(ViewType.MAINMENU, false);
        public void OpenHelp() => ViewController.ToggleView(ViewType.HELPOVERLAY, false);

        // ---- Status readouts. Time reuses the game's own localized day-and-hour string; money and health
        // are composed in Core from the raw model values.
        public void ReadTime()
            => _host.Speech.Speak(SunshineClock.Singleton.Time.ToDayHourString(), interrupt: true);

        public void ReadMoney()
            => _host.Speech.Speak(Strings.WorldMoney(PlayerCharacter.Singleton.Money), interrupt: true);

        // The two bars are named by the game's own HEALTH/MORALE terms (not the Endurance/Volition skills that
        // set their maximums), with the current-of-maximum value and the count of assigned healing charges.
        public void ReadHealth()
        {
            var you = global::World.Singleton.you;
            var pools = PlayerCharacter.Singleton.healingPools;
            _host.Speech.Speak(Strings.WorldHealth(
                GameLocalization.Translate(HealthTerm),
                you.endurance.value, you.endurance.maximumValue,
                pools.GetHealingChargetsForSkill(SkillType.ENDURANCE),
                GameLocalization.Translate(MoraleTerm),
                you.volition.value, you.volition.maximumValue,
                pools.GetHealingChargetsForSkill(SkillType.VOLITION)), interrupt: true);
        }

        // ---- Quick-actions ----

        // Heal a bar by clicking its HUD healing button, the same call the controller dpad makes (left heals
        // Health, right heals Morale). The button's own click both spends the charge and applies the heal, and
        // raises the game's health/morale notification that NotificationReader speaks - so this adds no line of
        // its own. It refuses first, with spoken feedback, when no charge is assigned or the bar is already
        // full (the button alone only plays an unspoken failure sound), so a blind player hears why nothing
        // happened.
        public void HealEndurance() => Heal(SkillType.ENDURANCE, HealthTerm);
        public void HealVolition() => Heal(SkillType.VOLITION, MoraleTerm);

        private void Heal(SkillType skill, string barTerm)
        {
            string bar = GameLocalization.Translate(barTerm);
            var pools = PlayerCharacter.Singleton.healingPools;
            if (pools.GetHealingChargetsForSkill(skill) <= 0) { _host.Speech.Speak(Strings.WorldNoBarHeal(bar), interrupt: true); return; }
            if (!BarHasDamage(skill)) { _host.Speech.Speak(Strings.WorldBarFull(bar), interrupt: true); return; }
            FindHealingButton(skill).OnPointerClick(null);
        }

        // The game's localization terms for the two bars (the skills set their values; the bars carry these names).
        private const string HealthTerm = "HEALTH";
        private const string MoraleTerm = "MORALE";

        // The HUD healing button for a bar, matched by the pool it heals (Endurance = Health, Volition =
        // Morale). Re-found each press rather than held, since the HUD is rebuilt across scene loads.
        private static HealingButton FindHealingButton(SkillType skill)
        {
            foreach (var button in UnityEngine.Object.FindObjectsOfType<HealingButton>())
                if (button.HealingPoolType == skill) return button;
            return null;
        }

        // The game's own heal-eligibility gate (a bar can be healed only while it carries damage), reused so
        // the mod's refusal stays in lockstep with the button's.
        private static bool BarHasDamage(SkillType skill)
            => skill == SkillType.ENDURANCE
                ? Sunshine.Dialogue.CharacterLuaFunctions.HasEnduranceDamage()
                : Sunshine.Dialogue.CharacterLuaFunctions.HasVolitionDamage();

        // Use the item held in a hand by clicking its HUD held-item button, the same call the controller stick
        // clicks make (left stick = left hand, right stick = right hand): the button's own click runs the real
        // substance-use (or equips the orb). Empty hand reads as such rather than a misleading "used".
        public void UseLeftHand()
            => UseHand(EquipmentSlotType.HELDLEFT, HudHeldPanelController.Current.leftHandHeldButton, Strings.WorldUsedLeftHand, Strings.WorldLeftHandEmpty);
        public void UseRightHand()
            => UseHand(EquipmentSlotType.HELDRIGHT, HudHeldPanelController.Current.rightHandHeldButton, Strings.WorldUsedRightHand, Strings.WorldRightHandEmpty);

        private void UseHand(EquipmentSlotType slot, HudHeldButton button, string used, string empty)
        {
            if (InventoryViewData.Singleton.GetItemInSlot(slot) == null) { _host.Speech.Speak(empty, interrupt: true); return; }
            button.OnHeldButtonClicked();
            _host.Speech.Speak(used, interrupt: true);
        }

        // Global keys, so both can fire anywhere - a menu, a conversation, even the title screen, where
        // World.Singleton does not exist yet (the one place these handlers cannot trust the singletons).
        // The game gates both (CanSave refuses dialogue, cutscenes, most views; CanQuickLoad adds
        // transitions) and DoQuickSave silently no-ops when refused, so the refusal is spoken here.
        // The game raises its own QuicksaveComplete notification when the save lands, which NotificationReader
        // speaks, so an accepted press only triggers the save (a line here would double-speak it).
        public void QuickSave()
        {
            if (global::World.Singleton == null || !SunshinePersistence.CanSave())
            { _host.Speech.Speak(Strings.WorldQuickSaveUnavailable, interrupt: true); return; }
            SunshinePersistence.Singleton.DoQuickSave();
        }

        // CanQuickLoad gates on context only, not on a quicksave existing (DoQuickLoad would hand Load a
        // null save name), so the two refusals are distinguished: nothing to load vs wrong moment.
        public void QuickLoad()
        {
            if (global::World.Singleton == null || !SunshinePersistence.CanQuickLoad())
            { _host.Speech.Speak(Strings.WorldQuickLoadUnavailable, interrupt: true); return; }
            if (string.IsNullOrEmpty(SunshinePersistenceFileManager.GetLastSaveWithNamePart(SunshinePersistenceFileManager.QUICK_SAVE_SLOT_NAME)))
            { _host.Speech.Speak(Strings.WorldNoQuickSave, interrupt: true); return; }
            _host.Speech.Speak(Strings.WorldQuickLoading, interrupt: true);
            SunshinePersistence.Singleton.DoQuickLoad();
        }

        // The game's language quick-switch: swap to the secondary language configured in options, then
        // speak the new language's own name as confirmation. Goes through SmoothLanguageSwitch (the
        // handler behind the game's own quick-switch keys), which also swaps the primary/secondary
        // settings via OnLanguagesSwitched so the next press switches back. Snap rather than the smooth
        // fade: the fade is visual-only and delays the actual language change past the confirmation
        // speech. The game gates the switch (title menu, settings view, photo mode, mid-save); a
        // refused press says so rather than going silent.
        public void SwitchLanguage()
        {
            var switcher = UnityEngine.Object.FindObjectOfType<LocalizationCustomSystem.SmoothLanguageSwitch>();
            if (switcher == null || !switcher.CanSwitchLanguage())
            {
                _host.Speech.Speak(Strings.WorldNoLanguageSwitch, interrupt: true);
                return;
            }
            switcher.ToggleLanguage(snapToggle: true);
            string lang = I2.Loc.LocalizationManager.CurrentLanguage;
            _host.Speech.Speak(string.IsNullOrEmpty(lang) ? Strings.WorldLanguageChanged : lang, interrupt: true);
        }
    }
}

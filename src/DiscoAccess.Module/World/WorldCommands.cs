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
    /// live singletons to exist (they do, in the in-world view this category is live in).
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
                you.endurance.maximumValue - you.endurance.damageValue, you.endurance.maximumValue,
                pools.GetHealingChargetsForSkill(SkillType.ENDURANCE),
                GameLocalization.Translate(MoraleTerm),
                you.volition.maximumValue - you.volition.damageValue, you.volition.maximumValue,
                pools.GetHealingChargetsForSkill(SkillType.VOLITION)), interrupt: true);
        }

        // ---- Quick-actions ----

        // Use a healing charge on a bar (matching the controller dpad: left heals Health, right heals Morale).
        // Refuses when no charge is assigned, and when the bar is already full (so a charge is never wasted),
        // each with spoken feedback named by the game's own bar term.
        public void HealEndurance() => Heal(SkillType.ENDURANCE, HealthTerm, BarDamage(SkillType.ENDURANCE));
        public void HealVolition() => Heal(SkillType.VOLITION, MoraleTerm, BarDamage(SkillType.VOLITION));

        private void Heal(SkillType skill, string barTerm, int damage)
        {
            string bar = GameLocalization.Translate(barTerm);
            var pools = PlayerCharacter.Singleton.healingPools;
            if (pools.GetHealingChargetsForSkill(skill) <= 0) { _host.Speech.Speak(Strings.WorldNoBarHeal(bar), interrupt: true); return; }
            if (damage <= 0) { _host.Speech.Speak(Strings.WorldBarFull(bar), interrupt: true); return; }
            pools.UseHealingCharge(skill);
            _host.Speech.Speak(Strings.WorldBarHealed(bar), interrupt: true);
        }

        // The game's localization terms for the two bars (the skills set their values; the bars carry these names).
        private const string HealthTerm = "HEALTH";
        private const string MoraleTerm = "MORALE";

        private static int BarDamage(SkillType skill)
        {
            var you = global::World.Singleton.you;
            return skill == SkillType.ENDURANCE ? you.endurance.damageValue : you.volition.damageValue;
        }

        // Use the item equipped to a hand. Empty hand reads as such rather than a misleading "used".
        public void UseLeftHand()
            => UseHand(EquipmentSlotType.HELDLEFT, "left", Strings.WorldUsedLeftHand, Strings.WorldLeftHandEmpty);
        public void UseRightHand()
            => UseHand(EquipmentSlotType.HELDRIGHT, "right", Strings.WorldUsedRightHand, Strings.WorldRightHandEmpty);

        private void UseHand(EquipmentSlotType slot, string side, string used, string empty)
        {
            if (InventoryViewData.Singleton.GetItemInSlot(slot) == null) { _host.Speech.Speak(empty, interrupt: true); return; }
            Sunshine.Dialogue.InventoryLuaFunctions.UseSubstanceInHand(side);
            _host.Speech.Speak(used, interrupt: true);
        }

        // The game raises its own QuicksaveComplete notification when the save lands, which NotificationReader
        // speaks, so this only triggers the save (a line here would double-speak it).
        public void QuickSave() => SunshinePersistence.Singleton.DoQuickSave();

        public void QuickLoad()
        {
            if (!SunshinePersistence.CanQuickLoad()) { _host.Speech.Speak(Strings.WorldNoQuickSave, interrupt: true); return; }
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

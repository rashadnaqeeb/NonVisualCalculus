using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using Sunshine.Metric;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The world hotkeys that act on the GAME rather than the mod's cursor: opening the information screens
    /// and the pause/help menus, the status readouts (time, money, health), and the gameplay quick-actions
    /// (heal items, hand items, quicksave/quickload, language). Kept separate from <see cref="WorldReader"/>,
    /// which owns the cursor and the sensing overlay, so each stays one concern.
    ///
    /// Because the world keyboard mutes the game's action set, each of these re-provides one muted game
    /// action by writing a real press onto it (<see cref="Input.GameActionPress"/>), so the game's own
    /// handlers act on it with all their gating. The handlers are fired from the input pump, which logs any throw, so they trust the
    /// live singletons to exist (they do, in the in-world view this category is live in - except the global
    /// quicksave/quickload pair, which can fire before a world exists and guard for it).
    /// </summary>
    internal sealed class WorldCommands
    {
        private readonly IModHost _host;

        public WorldCommands(IModHost host) { _host = host; }

        // ---- Information screens and pause/help: the mod does not open these itself. It hands the game the
        // matching keystroke and lets the game's own input handlers do everything - opening, refusing, the
        // transition - with all their gating intact. Reconstructing the open (ToggleView, a button's onClick)
        // bypasses that gating and can wedge the view state when it lands mid-animation.
        //
        // The world keyboard mutes the game's action set (see WorldReader), so PressGameKey injects the
        // game action as a pressed one-frame edge that reads through the mute (see GameActionPress).
        public void OpenInventory() { if (MayAct()) PressGameKey(Actions.Inventory); }
        public void OpenCharacterSheet() { if (MayAct()) PressGameKey(Actions.CharacterSheet); }
        public void OpenJournal() { if (MayAct()) PressGameKey(Actions.Journal); }
        public void OpenThoughtCabinet() { if (MayAct()) PressGameKey(Actions.ThoughtCabinet); }
        public void OpenHelp() { if (MayAct()) PressGameKey(Actions.Help); }

        // The game's Escape/back action, context-sensitive in the game's own handler: it opens the pause menu
        // in free-roam and closes an open view or world container in a menu. Used both for the free-roam pause
        // key and for Escape on any game screen, so the game closes screens the way it does for a sighted player.
        public void Escape() => PressGameKey(Actions.Pause);

        // The game's own controller cycle between the open info screens (character sheet, inventory,
        // journal, thought cabinet): hand the game its trigger actions and let its handler decide where
        // the cycle applies, like Escape.
        public void CycleScreenPrev() => PressGameKey(Actions.UITopTabLeft);
        public void CycleScreenNext() => PressGameKey(Actions.UITopTabRight);

        // The live game action set (MyCharacterActions), the source every game menu hotkey reads.
        private static MyCharacterActions Actions => CrossPlatformInputManager.mCPIM.InputActions;

        // Queue the game action as a keypress; GameActionPress writes it onto the action for the game's
        // handlers to read as a one-frame edge (see there).
        private static void PressGameKey(InControl.PlayerAction action)
            => Input.GameActionPress.Request(action);

        // While a scripted scene holds the game's input locks the world keyboard is ours but SUSPENDED
        // (see WorldReader): a key that acts on the game - opening a screen, using a hand item - refuses
        // aloud, the walk verbs' rule, instead of firing into a scene the game would not let a click reach.
        // Escape stays ungated (pausing is always the player's), as do the heals, the status readouts, and
        // the global save/load/language keys, which fire outside world ownership too and are gated by the
        // game itself.
        private bool MayAct()
        {
            if (!GameInputLock.Held) return true;
            // A paralyzer or unresolved thought orb holds the input lock itself, so name the orb - the
            // player must interact with it - rather than a scene that would pass by itself.
            _host.Speech.Speak(GlobalOrbManager.HasOrbsBlockingTequilaMovement()
                ? Strings.WorldOrbHolds : Strings.WorldNoControl, interrupt: true);
            return false;
        }

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

        // The leveling readout, composed from the live player-character model rather than the character
        // sheet's XP panel (which is only up when that screen is open, and reads a tweened value mid
        // level-up). XpAmount is the experience earned into the current level; GetCostForLevel is that
        // level's requirement, together the game's own "51/100"; SkillPoints is the unspent pool. The
        // "Experience" word reuses the game's own THC_TOOLTIP_EXP term the XP panel labels itself with.
        public void ReadExperience()
        {
            var pc = PlayerCharacter.Singleton;
            _host.Speech.Speak(Strings.WorldExperience(
                GameLocalization.Term(ExperienceTerm, Strings.WorldExperienceLabel),
                pc.XpAmount, LevelingUtils.GetCostForLevel(pc.Level), pc.SkillPoints), interrupt: true);
        }

        // The game's tooltip term for the "Experience" word (the XP panel's own label source).
        private const string ExperienceTerm = "THC_TOOLTIP_EXP";

        // ---- Quick-actions: hand the game the action and let it heal / use the hand item with its own gating
        // (a no-op with only a sound when there is no charge, a full bar, or an empty hand). Heal maps to the
        // game's Endurance/Volition heal actions (left heals Health, right heals Morale); the hand keys to its
        // LeftHand/RightHand actions. The heal and substance notifications the game raises on a real action are
        // spoken by NotificationReader.
        public void HealEndurance() => PressGameKey(Actions.Endurance);
        public void HealVolition() => PressGameKey(Actions.Volition);
        public void UseLeftHand() { if (MayAct()) UseHand(Actions.LeftHand); }
        public void UseRightHand() { if (MayAct()) UseHand(Actions.RightHand); }

        // The game's hand-key handler clicks the HUD's held button, which acts on a CACHED item
        // reference, and some equip paths never refresh it (a save load swaps the equipment
        // dictionary in without touching the panel) - the click then no-ops on an empty cache. A
        // sighted player sees the missing hand icon; for a blind player it is a silent dead key. So
        // resync the game's cache from its own inventory model (its refresh routine, a pure re-read)
        // before handing it the action.
        private static void UseHand(InControl.PlayerAction action)
        {
            var hud = HudHeldPanelController.Current;
            if (hud != null) hud.UpdateHeldPanel();
            PressGameKey(action);
        }

        // The game's localization terms for the two bars, used by the health readout above.
        private const string HealthTerm = "HEALTH";
        private const string MoraleTerm = "MORALE";

        // Quicksave, quickload, and the language quick-switch: hand the game its own action and let its
        // handlers do everything, exactly like the screen and heal keys. The game gates each itself
        // (CanSave, CanQuickLoad, CanSwitchLanguage) and no-ops silently when refused; the mod neither
        // gates nor announces - its checks went stale against the game's, and a refusal is the game's to
        // decide. A completed save raises the game's own QuicksaveComplete notification, spoken by
        // NotificationReader; LanguageSync follows the game language after a switch. Pressing LanguageSwitch0
        // fires the game's SmoothLanguageSwitch, which toggles primary/secondary so the next press reverts.
        public void QuickSave() => PressGameKey(Actions.QuickSave);
        public void QuickLoad() => PressGameKey(Actions.QuickLoad);
        public void SwitchLanguage() => PressGameKey(Actions.LanguageSwitch0);
    }
}

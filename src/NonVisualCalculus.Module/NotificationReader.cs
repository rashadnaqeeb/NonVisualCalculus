using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.Text;
using HarmonyLib;
using NotificationSystem;   // Notification, NotificationType, NotificationPanel
using Sunshine.Metric;      // Skill, SkillType, PlayerCharacter
// ExistentialChrisis (the game's spelling) lives in the global namespace.

namespace NonVisualCalculus.Module
{
    /// <summary>
    /// Speaks the game's own HUD pop-up notifications, which a sighted player reads off the corner of the
    /// screen and a blind player otherwise never hears. Two thin Harmony feeders push plain data into queues
    /// the pump drains: every notification the game actually displays (money, health/morale changes, items,
    /// thoughts, tasks, XP, effects, ...) read once and queued; and the existential crisis (a bar hit zero
    /// and the game paused for a heal-or-die window) read with interrupt, because it is genuinely timed.
    ///
    /// The crisis is split off from the general feed deliberately. Its own start hook fires the instant the
    /// bar empties, ahead of the slower panel-display path a queued damage notification would delay, and
    /// carries the dying skill so we can name the bar and the matching heal key. Its HEALTH/MORALE-critical
    /// notification is therefore skipped in the general feed, so it is never spoken twice or spoken late.
    ///
    /// Composition (what words are said) lives in Core (<see cref="NotificationText"/>, <see cref="Strings"/>);
    /// this side only extracts live state and speaks, per the adapter/composition split. Holds no native
    /// handle and injects no IL2CPP type, so it tears down cleanly on reload (its patches go with the
    /// module's Harmony instance, and the static back-reference dies with the collectible load context).
    /// </summary>
    internal sealed class NotificationReader : IDisposable
    {
        // The live reader while patched, so the static Harmony postfixes can reach the instance queues;
        // cleared on dispose. The module reloads into a collectible context, so this static dies with it.
        private static NotificationReader _active;

        private readonly IModHost _host;
        // Plain spoken lines queued by the patches (on the Unity main thread, the game's notification path)
        // and drained by the pump. No Unity reference crosses this boundary.
        private readonly Queue<string> _general = new Queue<string>();
        private readonly Queue<string> _crises = new Queue<string>();

        public NotificationReader(IModHost host) { _host = host; }

        /// <summary>Patch the game's notification display and the crisis start through the module's own
        /// Harmony instance, so a reload's <c>UnpatchSelf</c> removes them.</summary>
        public void Apply(Harmony harmony)
        {
            _active = this;
            harmony.Patch(
                AccessTools.Method(typeof(NotificationPanel), nameof(NotificationPanel.PlayNotification)),
                postfix: new HarmonyMethod(typeof(NotificationReader), nameof(OnNotificationShown)));
            harmony.Patch(
                AccessTools.Method(typeof(ExistentialChrisis), nameof(ExistentialChrisis.Execute)),
                postfix: new HarmonyMethod(typeof(NotificationReader), nameof(OnCrisisStarted)));
        }

        /// <summary>Speak everything queued since last frame: the timed crisis first (interrupt, so it cuts
        /// through), then the rest queued. Called from the pump each frame.</summary>
        public void Drain()
        {
            while (_crises.Count > 0)
                _host.Speech.Speak(_crises.Dequeue(), interrupt: true);
            while (_general.Count > 0)
                _host.Speech.Speak(_general.Dequeue(), interrupt: false);
        }

        public void Dispose() => _active = null;

        // --- Harmony feeders. Static (they reach the live reader through _active), and each guards its body
        // and logs any throw: this runs on the game's notification path, where an unlogged failure vanishes. ---

        // Every notification the game shows lands here once, in display order, already deduped and queued by
        // the game's own manager. Read its title and detail; skip the crisis types, which OnCrisisStarted owns.
        private static void OnNotificationShown(Notification notification)
        {
            NotificationReader self = _active;
            if (self == null || notification == null) return;
            try
            {
                NotificationType type = notification.Type;
                // HealthCritical/MoraleCritical are the existential crisis, owned by OnCrisisStarted (spoken
                // there with interrupt). Success/Failure are skill-check pass/fail, which the dialogue layer
                // already reads as the outcome line and its roll. Skip both so neither is double-spoken.
                if (type == NotificationType.HealthCritical || type == NotificationType.MoraleCritical
                    || type == NotificationType.Success || type == NotificationType.Failure)
                    return;
                string line = NotificationText.Compose(notification.HeaderText, notification.DescriptionText);
                if (!string.IsNullOrEmpty(line))
                    self._general.Enqueue(line);
            }
            catch (Exception e)
            {
                self._host.LogWarning("NotificationReader: reading a shown notification failed: " + e);
            }
        }

        // A bar hit zero. The game opens a heal window only when a charge is assigned for that bar; with none
        // it skips straight to death and shows no prompt, so we stay silent too (the death screen speaks).
        private static void OnCrisisStarted(Skill skill)
        {
            NotificationReader self = _active;
            if (self == null || skill == null) return;
            try
            {
                SkillType type = skill.skillType;
                if (PlayerCharacter.Singleton.healingPools.GetHealingChargetsForSkill(type) <= 0)
                    return;

                // Endurance drives the Health bar (heals with Left); Volition drives Morale (heals with Right).
                bool isHealth = type == SkillType.ENDURANCE;
                string bar = GameLocalization.Translate(isHealth ? "HEALTH" : "MORALE");
                string gameMessage = GameLocalization.Translate("Messages/NOTIFICATION_HEAL_YOURSELF");
                self._crises.Enqueue(Strings.CrisisHeal(bar, isHealth, gameMessage));
            }
            catch (Exception e)
            {
                self._host.LogWarning("NotificationReader: reading the existential crisis failed: " + e);
            }
        }
    }
}

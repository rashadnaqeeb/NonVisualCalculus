using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using FortressOccident;
using Sunshine;
using Snv = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The Enter verb. An entity's <c>Interact()</c> IS the game's whole click - it prices the approach to
    /// the entity's authored FormationMarker stand-spots, walks the whole party there (re-pathing live
    /// toward a moving target), and fires the interaction on arrival; false means the pricing refused
    /// (unreachable from here), never "too far". So for a self-driving target
    /// (<see cref="IWalkTarget.InteractWalks"/>) this verb just fires the click and speaks the outcome:
    /// "walking" when a walk is due, "can't reach" on refusal, nothing when in range (the game acts
    /// instantly and its own readers speak).
    ///
    /// An orb triggers only in place, so there the verb still owns the walk: target the stand-point, drive
    /// <c>SetDestination</c>, watch <c>movementStatus</c>, and <c>Interact</c> on arrival - a small
    /// arrival-watching state machine ticked each frame by the <see cref="WorldReader"/>, cancellable
    /// mid-path. A no-target Enter is a plain walk to bare ground handled by <see cref="BeginWalk"/>.
    /// </summary>
    internal sealed class WalkInteract
    {
        // Consecutive non-moving frames tolerated before giving up. Generous before the walk first moves
        // (SetDestination can read IDLE for several frames while it engages a long path), and shorter once it
        // has moved (a halt then is a real stall: a dynamic obstacle, an off-mesh sliver). At ~60 fps these
        // are about a second and three-quarters of a second.
        private const int StartupGraceFrames = 60;
        private const int StallGraceFrames = 45;
        // How many times a stalled walk is re-issued while the game's own oracle still says the target is
        // reachable, before giving up. A stall short of a reachable target is usually transient - a wandering
        // NPC briefly blocking a crowded doorway (the Whirling entrance), or a degenerate path the game handed
        // back - and clears on a fresh attempt from where the character stopped; this bounds that so a genuinely
        // stuck walk still ends rather than looping.
        private const int MaxStallRetries = 2;
        // How close (metres) to a bare-ground destination counts as arrived, when no interaction radius applies.
        private const float GroundArrivalDistance = 0.6f;

        private readonly IModHost _host;

        private IWalkTarget _target; // null => bare-ground walk (arrival is the whole action, no interact)
        private string _label;       // the target's name (or "ground"), for logs only
        private Snv _dest;           // the issued destination, for bare-ground arrival distance
        private bool _active;
        private bool _movedOnce;     // movementStatus has been MOVING/ADJUSTING since this walk began
        private int _stalledTicks;   // consecutive frames the character has been non-moving and not arrived
        private int _retries;        // stalled-walk re-issues spent on the current target (see Stall)

        public WalkInteract(IModHost host) { _host = host; }

        /// <summary>Whether a committed walk is in flight (being watched for arrival).</summary>
        public bool Active => _active;

        /// <summary>Walk to <paramref name="target"/> and interact, approaching from <paramref name="from"/>
        /// (the character's current position). A self-driving target is the game's own click, fired directly;
        /// an orb is walked to by this verb and triggered on arrival.</summary>
        public bool BeginInteract(IWalkTarget target, Snv from)
        {
            // A paralyzer or unresolved thought orb freezes the character where they stand (the game's own
            // HasOrbsBlockingTequilaMovement gate, which its click flow honours silently and our direct
            // SetDestination bypasses). Refuse to walk away while held - except an in-place interact with the
            // holding orb itself (a player-anchored orb travels nowhere), which is how the block is released.
            // Checked for self-driving targets too, so the hold is SPOKEN rather than the game's mute refusal.
            if (MovementBlocked() && !target.RidesPlayer)
            {
                _host.Speech.Speak(Strings.WorldOrbHolds, interrupt: true);
                return false;
            }

            if (target.InteractWalks)
            {
                // The click prices the approach itself and refuses when nothing walkable reaches an authored
                // stand-spot - speak that as can't-reach. In range it acts this same frame and the reaction's
                // own readers speak, so "walking" is announced only when an actual walk begins.
                bool inRange = target.WithinInteractionRadius(from);
                if (!target.Interact(_host.Settings.RunToDestinations.Value))
                {
                    _host.Speech.Speak(Strings.WorldUnreachable(target.Name), interrupt: true);
                    return false;
                }
                if (!inRange) _host.Speech.Speak(Strings.WorldWalkingTo(target.Name), interrupt: true);
                return true;
            }

            Snv stand = target.Approach(from, out float heading);
            if (!Drive(stand, heading)) return false;
            _target = target;
            _label = string.IsNullOrEmpty(target.Name) ? "target" : target.Name;
            _retries = 0;
            _host.Speech.Speak(Strings.WorldWalkingTo(target.Name), interrupt: true);
            return true;
        }

        /// <summary>Walk to a bare-ground spot with nothing to interact with; arrival is the whole action.
        /// <paramref name="announcement"/> is what to speak on committing (the plain "walking", or a "can't
        /// reach" naming the unreachable thing the cursor was near, since the cursor's ground is still
        /// walkable and getting closer can make that thing reachable for a follow-up).</summary>
        public bool BeginWalk(Snv point, string announcement)
        {
            // Bare-ground walk carries the character off with no target to resolve the hold, so it is always
            // refused while a paralyzer or unresolved thought orb holds them in place (see BeginInteract).
            if (MovementBlocked())
            {
                _host.Speech.Speak(Strings.WorldOrbHolds, interrupt: true);
                return false;
            }
            if (!Drive(point, null)) return false;
            _target = null;
            _label = "ground";
            _host.Speech.Speak(announcement, interrupt: true);
            return true;
        }

        /// <summary>Player-initiated cancel (the Stop key): halt the character and say so. Covers both this
        /// verb's own watched walks and a click-driven walk the game is running (a self-driving interact),
        /// which this verb does not track - the controller's isMoving is its live state.</summary>
        public void Cancel()
        {
            if (!_active && !GameMoving()) return;
            StopCharacter();
            _active = false;
            _host.Speech.Speak(Strings.WorldStopped, interrupt: true);
        }

        private static bool GameMoving()
        {
            GameController gc = GameController.Singleton;
            return gc != null && gc.isMoving;
        }

        /// <summary>Silent abandon when the world reader loses control (a script grabbed the character, the
        /// area unloaded): only drop the watch, never halt the character. The game (a conversation, a scripted
        /// sequence) owns the character's movement now, so issuing StopMovement here would fight it; the player
        /// did not ask to stop, so there is nothing to say.</summary>
        public void Abandon() => _active = false;

        /// <summary>Advance the walk: when the character finishes its path (or already stands in range),
        /// interact once and finish. A broken path, or a walk that stalls (never starts, or moves then halts
        /// short of the target) is handed to <see cref="Stall"/> rather than left hanging - which retries a
        /// still-reachable target and, once that is exhausted, speaks "can't reach" so the player is never left
        /// in silence after the "walking to" line. Each outcome is logged.</summary>
        public void Tick()
        {
            if (!_active) return;
            Character main = Main;
            if (main == null) { _active = false; return; } // character gone (load/teleport): drop the walk

            Snv player = WorldConvert.ToSnv(main.transform.position);
            Character.MovementStatus status = main.movementStatus;
            bool moving = status == Character.MovementStatus.MOVING || status == Character.MovementStatus.ADJUSTING;
            if (moving) { _movedOnce = true; _stalledTicks = 0; } else _stalledTicks++;

            if (HasArrived(status, player)) { Arrive(player); _active = false; return; }

            if (status == Character.MovementStatus.BROKEN)
            {
                _host.LogWarning($"WalkInteract: path to {_label} broke; abandoning the walk.");
                Stall();
                return;
            }

            // Non-moving and not arrived for longer than the grace: either SetDestination never engaged (a
            // longer startup grace, since a long path can read IDLE for several frames) or the character moved
            // then halted short (a dynamic obstacle, an off-mesh sliver). Give up rather than watch forever.
            int grace = _movedOnce ? StallGraceFrames : StartupGraceFrames;
            if (_stalledTicks > grace)
            {
                _host.LogWarning($"WalkInteract: walk to {_label} stalled ({status}); abandoning.");
                Stall();
            }
        }

        // End a stalled walk (orb targets only; a self-driving target never enters the watch). The character
        // may have halted close enough anyway - just inside the orb's interaction circle without a COMPLETED
        // status - so try the trigger first. If it refuses (out of range), ask the target's reachability from
        // where the character actually stopped: when it still says actionable, the stall was transient (a
        // wandering NPC briefly blocking a crowded doorway, or a degenerate path the game handed back), so
        // recompute the stand-point from here and walk again rather than falsely reporting can't-reach. Only
        // when that says no, or the retries are spent, is it genuinely unreachable, and we say so rather than
        // leave the player in silence. A bare-ground walk (no target) just stops being watched.
        private void Stall()
        {
            if (_target == null) { _active = false; return; }
            if (_target.Interact()) { _active = false; SpeakPostInteract(); return; }

            Character main = Main;
            if (main != null && _retries < MaxStallRetries)
            {
                Snv here = WorldConvert.ToSnv(main.transform.position);
                if (_target.IsActionable(here))
                {
                    _retries++;
                    Snv stand = _target.Approach(here, out float heading);
                    if (Drive(stand, heading))
                    {
                        _host.LogWarning($"WalkInteract: walk to {_label} stalled but still actionable; retry {_retries}/{MaxStallRetries}.");
                        return;
                    }
                }
            }

            _active = false;
            _host.Speech.Speak(Strings.WorldUnreachable(_target.Name), interrupt: true);
        }

        // Arrived when the game reports the move completed, or the character already stands within the
        // target's interaction radius (a stand-point you already occupy never enters MOVING). For bare
        // ground, completion or simple proximity to the spot.
        private bool HasArrived(Character.MovementStatus status, Snv player)
        {
            if (status == Character.MovementStatus.COMPLETED) return true;
            if (_target != null) return _target.WithinInteractionRadius(player);
            return Snv.Distance(player, _dest) <= GroundArrivalDistance;
        }

        private void Arrive(Snv player)
        {
            if (_target == null) return; // bare-ground walk: nothing to interact with
            // Gate the interact on the game's own arrival-range test. At a COMPLETED stand-point this holds;
            // if somehow short, Interact() refuses in place rather than acting at the wrong spot - logged so
            // the miss is visible rather than silent.
            if (!_target.WithinInteractionRadius(player))
                _host.LogWarning($"WalkInteract: arrived near {_label} but outside its interaction radius; interacting anyway.");
            if (!_target.Interact())
                _host.LogWarning($"WalkInteract: Interact on {_label} returned false at the stand-point.");
            else
                SpeakPostInteract();
        }

        // Speak whatever the target says after a successful interact (a simple orb's floated clue text); most
        // targets say nothing. Queued so it follows the interaction without cutting off the walk feedback.
        private void SpeakPostInteract()
        {
            string line = _target.PostInteractLine();
            if (!string.IsNullOrEmpty(line))
                _host.Speech.Speak(line, interrupt: false);
        }

        private bool Drive(Snv point, float? heading)
        {
            Character main = Main;
            if (main == null) return false;
            // The game's heading argument is an Il2Cpp nullable: a value faces the character that way on
            // arrival, the empty case leaves the heading to the game (a bare-ground walk).
            Il2CppSystem.Nullable<float> h = heading.HasValue
                ? new Il2CppSystem.Nullable<float>(heading.Value)
                : new Il2CppSystem.Nullable<float>();
            // The Run-to-destinations setting picks the pace: RUN drives run speed directly, while AUTOMATIC
            // defers to the game's own distance policy, which walks (its auto-run distance is unbounded),
            // matching a vanilla single click. This is the same primitive the game's click path bottoms out in.
            MovementMode mode = _host.Settings.RunToDestinations.Value ? MovementMode.RUN : MovementMode.AUTOMATIC;
            main.SetDestination(WorldConvert.ToUnity(point), h, mode, false);
            _dest = point;
            _active = true;
            _movedOnce = false;
            _stalledTicks = 0;
            return true;
        }

        private static void StopCharacter()
        {
            GameController gc = GameController.Singleton;
            if (gc != null) gc.StopMovement(force: false);
        }

        // The game's own "orbs are freezing the player" verdict: true while any paralyzer or unresolved
        // thought orb sits on the character. Read live (never cached) so the block lifts the instant the orb
        // is resolved.
        private static bool MovementBlocked() => GlobalOrbManager.HasOrbsBlockingTequilaMovement();

        private static Character Main
        {
            get { Party p = Party.Player; return p != null ? p.Main : null; }
        }
    }
}

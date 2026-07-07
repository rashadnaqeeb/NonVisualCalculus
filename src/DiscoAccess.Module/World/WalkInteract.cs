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
    /// An undrawn orb triggers only in place, so there the verb still owns the walk: target the
    /// stand-point, drive the game's party ground move (<see cref="Drive"/>), watch <c>movementStatus</c>,
    /// and <c>Interact</c> on arrival - a small arrival-watching state machine ticked each frame by the
    /// <see cref="WorldReader"/>, cancellable mid-path. A no-target Enter is a plain walk to bare ground
    /// handled by <see cref="BeginWalk"/>.
    /// </summary>
    internal sealed class WalkInteract
    {
        // Consecutive non-moving frames tolerated before giving up. Generous before the walk first moves
        // (the move command can read IDLE for several frames while it engages a long path), and shorter once it
        // has moved (a halt then is a real stall: a dynamic obstacle, an off-mesh sliver). At ~60 fps these
        // are about a second and three-quarters of a second.
        private const int StartupGraceFrames = 60;
        private const int StallGraceFrames = 45;
        // How many times a stalled walk is re-issued before giving up. A stall is usually transient - a wandering
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
            // The game ignores world clicks while any input lock is held (a scripted scene still
            // animating after a dialogue's last line, a camera move, a transition): its own click paths
            // check this before moving, so mirror it at command time - Drive is the raw MoveToTarget,
            // which skips that gate, and a bookmark walk arrives from the menu without the world
            // reader's ownership check. Spoken, so the refusal is never a silent dead key.
            if (InputLocked())
            {
                _host.Speech.Speak(Strings.WorldNoControl, interrupt: true);
                return false;
            }

            // A paralyzer or unresolved thought orb freezes the character where they stand (the game's own
            // HasOrbsBlockingTequilaMovement gate, which its move paths honour silently). Refuse first, so
            // the hold is SPOKEN rather than the game's mute refusal - except an in-place interact with the
            // holding orb itself (a player-anchored orb travels nowhere), which is how the block is released.
            if (MovementBlocked() && !target.RidesPlayer)
            {
                _host.Speech.Speak(Strings.WorldOrbHolds, interrupt: true);
                return false;
            }

            if (target.InteractWalks)
            {
                // The click prices the approach itself and refuses when nothing walkable reaches an authored
                // stand-spot - speak that as can't-reach. Otherwise it either started a party walk (the
                // interaction fires on arrival, where the reaction's own readers speak) or acted in place
                // this same frame (an in-range entity, a drawn orb's Open from any distance) - announce the
                // walk, or speak the in-place result (an orb's float text; an entity has none).
                if (!target.Interact(_host.Settings.RunToDestinations.Value))
                {
                    _host.Speech.Speak(Strings.WorldUnreachable(target.Name), interrupt: true);
                    return false;
                }
                // A drawn orb's click acts in place - Open floats its clue this same frame, and nothing but
                // PostInteractLine carries that text - so speak it whenever there is one, even while the party
                // is mid-walk: a residual move must not swallow the float (and the orb was opened where the
                // character stands, so "moving to it" would be a lie). Only a target silent in place (an
                // entity, whose reaction its own readers speak on arrival) falls back to announcing the walk.
                string inPlace = target.PostInteractLine();
                if (!string.IsNullOrEmpty(inPlace)) _host.Speech.Speak(inPlace, interrupt: false);
                else if (GameMoving()) _host.Speech.Speak(Strings.WorldMovingTo(target.Name), interrupt: true);
                return true;
            }

            Snv stand = target.Approach(from, out _); // the facing is the game's own (walk direction)
            if (!Drive(stand)) return false;
            _target = target;
            _label = string.IsNullOrEmpty(target.Name) ? "target" : target.Name;
            _retries = 0;
            _host.Speech.Speak(Strings.WorldMovingTo(target.Name), interrupt: true);
            return true;
        }

        /// <summary>Walk to a bare-ground spot with nothing to interact with; arrival is the whole action.
        /// <paramref name="announcement"/> is what to speak on committing (the plain "walking", or a "can't
        /// reach" naming the unreachable thing the cursor was near, since the cursor's ground is still
        /// walkable and getting closer can make that thing reachable for a follow-up).</summary>
        public bool BeginWalk(Snv point, string announcement)
        {
            // The game's input-lock gate, exactly as in BeginInteract.
            if (InputLocked())
            {
                _host.Speech.Speak(Strings.WorldNoControl, interrupt: true);
                return false;
            }
            // Bare-ground walk carries the character off with no target to resolve the hold, so it is always
            // refused while a paralyzer or unresolved thought orb holds them in place (see BeginInteract).
            if (MovementBlocked())
            {
                _host.Speech.Speak(Strings.WorldOrbHolds, interrupt: true);
                return false;
            }
            // The game priced the spot unwalkable (off-mesh, or inside something's footprint - likelier for
            // the Walk verb, which aims at occupied ground on purpose). Refusing silently would leave the
            // player waiting on a walk that never started.
            if (!Drive(point))
            {
                _host.Speech.Speak(Strings.WorldUnreachable(null), interrupt: true);
                return false;
            }
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

            // Non-moving and not arrived for longer than the grace: either the move never engaged (a
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
        // status - so try the trigger first. If it refuses (out of range), the stall may be transient (a
        // wandering NPC briefly blocking a crowded doorway, or a degenerate path the game handed back), so
        // recompute the stand-point from where the character actually stopped and walk again, bounded by the
        // retry budget; once spent, say can't-reach rather than leave the player in silence. A bare-ground
        // walk (no target) has no interaction to retry toward: say it ended short, so its "moving" is
        // likewise never left dangling in silence.
        private void Stall()
        {
            if (_target == null)
            {
                _active = false;
                _host.Speech.Speak(Strings.WorldStoppedShort, interrupt: true);
                return;
            }
            if (_target.Interact()) { _active = false; SpeakPostInteract(_target); return; }

            Character main = Main;
            if (main != null && _retries < MaxStallRetries)
            {
                _retries++;
                Snv here = WorldConvert.ToSnv(main.transform.position);
                Snv stand = _target.Approach(here, out _);
                if (Drive(stand))
                {
                    _host.LogWarning($"WalkInteract: walk to {_label} stalled; retry {_retries}/{MaxStallRetries}.");
                    return;
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
            // A bare-ground walk's arrival IS the whole action, so it is the one move that announces it;
            // a targeted walk ends in its interaction, whose own readers speak instead.
            if (_target == null) { _host.Speech.Speak(Strings.WorldArrived, interrupt: true); return; }
            // Gate the interact on the game's own arrival-range test. At a COMPLETED stand-point this holds;
            // if somehow short, Interact() refuses in place rather than acting at the wrong spot - logged so
            // the miss is visible rather than silent.
            if (!_target.WithinInteractionRadius(player))
                _host.LogWarning($"WalkInteract: arrived near {_label} but outside its interaction radius; interacting anyway.");
            if (!_target.Interact())
                _host.LogWarning($"WalkInteract: Interact on {_label} returned false at the stand-point.");
            else
                SpeakPostInteract(_target);
        }

        // Speak whatever the target says after a successful interact (a simple orb's floated clue text); most
        // targets say nothing. Queued so it follows the interaction without cutting off the walk feedback.
        private void SpeakPostInteract(IWalkTarget target)
        {
            string line = target.PostInteractLine();
            if (!string.IsNullOrEmpty(line))
                _host.Speech.Speak(line, interrupt: false);
        }

        private bool Drive(Snv point)
        {
            GameController gc = GameController.Singleton;
            if (gc == null || Main == null) return false;
            // The game's own ground move (NavMeshClickHandler.Interact bottoms out here): the point boxed
            // into MoveToTarget walks the WHOLE PARTY formation - Kim follows, exactly as on a vanilla
            // ground click - where a bare Character.SetDestination moved the main character alone and
            // stranded him (proven live: a 9 m SetDestination walk left Kim standing). AUTOMATIC and RUN
            // are the click's own single/double-click values, and the arrival heading is the game's (the
            // walk direction). False means the move was priced unreachable and never started.
            MovementMode mode = _host.Settings.RunToDestinations.Value ? MovementMode.RUN : MovementMode.AUTOMATIC;
            if (!gc.MoveToTarget(WorldConvert.ToUnity(point).BoxIl2CppObject(), mode,
                                 Formation.Purpose.UNIVERSAL, true, false))
                return false;
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

        // The game's own world-click gate: any held input lock (GameController.inputLocks - a scripted
        // scene, a camera move, a transition) means the game would ignore a click right now, so a
        // mod-driven move must not start either. No controller at all reads as locked - the world
        // cannot be driven without one.
        private static bool InputLocked()
        {
            GameController gc = GameController.Singleton;
            return gc == null || gc.IsWorldInputDisabled();
        }

        private static Character Main
        {
            get { Party p = Party.Player; return p != null ? p.Main : null; }
        }
    }
}

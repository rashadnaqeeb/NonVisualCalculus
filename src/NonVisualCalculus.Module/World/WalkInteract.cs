using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.Text;
using FortressOccident;
using Sunshine;
using Snv = System.Numerics.Vector3;

namespace NonVisualCalculus.Module.World
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
    ///
    /// A refused walk gets one more try as a two-leg detour (<see cref="GateDetour"/>): when the game
    /// prices the destination infinite because a self-opening passage is sealed until the character stands
    /// beside it, the verb walks to a spot inside that passage's trigger box first, then re-issues the real
    /// destination once the gate has opened - a sighted player's click-into-the-passage-then-on, with the
    /// game opening its own gate. The gate open is waited for, briefly, since the mesh re-carves a frame or
    /// two after the trigger fires; if it never opens the walk ends in the same "can't reach".
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
        // Frames the character waits at a detour's gate spot for the passage to price finite. The trigger
        // fires on entry and the obstacle lifts the same frame, but the mesh re-carves a frame or two later;
        // at ~60 fps this is a second and a half, generous for that and short enough that a gate that never
        // opens is reported promptly.
        private const int GateOpenGraceFrames = 90;

        private readonly IModHost _host;
        private readonly GateDetour _gates;

        private IWalkTarget _target; // null => bare-ground walk (arrival is the whole action, no interact)
        private string _label;       // the target's name (or "ground"), for logs only
        private Snv _dest;           // the issued destination, for bare-ground arrival distance
        private bool _active;
        private bool _movedOnce;     // movementStatus has been MOVING/ADJUSTING since this walk began
        private int _stalledTicks;   // consecutive frames the character has been non-moving and not arrived
        private int _retries;        // stalled-walk re-issues spent on the current target (see Stall)
        private bool _detour;        // the walk in flight is a detour's first leg, or its wait at the gate spot
        private Snv _final;          // a detour's real destination, re-issued once the gate has opened
        private bool _atGate;        // the detour's first leg has arrived; waiting for the gate to open
        private int _gateTicks;      // frames spent waiting at the gate spot

        public WalkInteract(IModHost host, GateDetour gates)
        {
            _host = host;
            _gates = gates;
        }

        /// <summary>Whether a committed walk is in flight (being watched for arrival).</summary>
        public bool Active => _active;

        /// <summary>Walk to <paramref name="target"/> and interact, approaching from <paramref name="from"/>
        /// (the character's current position). A self-driving target is the game's own click, fired directly;
        /// an orb is walked to by this verb and triggered on arrival.</summary>
        public bool BeginInteract(IWalkTarget target, Snv from)
        {
            // A paralyzer or unresolved thought orb freezes the character where they stand (the game's own
            // HasOrbsBlockingTequilaMovement gate, which its move paths honour silently) - and the holding
            // orb keeps a GameController input lock of its own, so this is judged BEFORE the generic
            // input-lock refusal: the hold is spoken as what it is, and an interact with the holding orb
            // itself (a player-anchored orb travels nowhere) passes through - its Open is the game's own
            // orb-UI click, which the world-click gate never applied to, and is what releases the hold.
            if (MovementBlocked())
            {
                if (!target.RidesPlayer)
                {
                    _host.Speech.Speak(Strings.WorldOrbHolds, interrupt: true);
                    return false;
                }
            }
            // The game ignores world clicks while any input lock is held (see GameInputLock): its own
            // click paths check this before moving, so mirror it at command time - Drive is the raw
            // MoveToTarget, which skips that gate, and a bookmark walk arrives from the menu without the
            // world reader's ownership check. Spoken, so the refusal is never a silent dead key.
            else if (GameInputLock.Held)
            {
                _host.Speech.Speak(Strings.WorldNoControl, interrupt: true);
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
                    // Refused: a sealed self-opening passage may be all that is in the way. Its stand-spot
                    // is where the click would have walked, so that is what the detour must reach.
                    if (BeginDetour(target, from, target.Approach(from, out _)))
                    {
                        _host.Speech.Speak(SpokenLine.Join(Strings.WorldMovingTo(target.Name), Strings.WorldDetour), interrupt: true);
                        return true;
                    }
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
            if (!Drive(stand))
            {
                if (BeginDetour(target, from, stand))
                {
                    _host.Speech.Speak(SpokenLine.Join(Strings.WorldMovingTo(target.Name), Strings.WorldDetour), interrupt: true);
                    return true;
                }
                return false;
            }
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
            // Bare-ground walk carries the character off with no target to resolve the hold, so it is always
            // refused while a paralyzer or unresolved thought orb holds them in place - named before the
            // generic input-lock refusal, since the holding orb keeps that lock held too (see BeginInteract).
            if (MovementBlocked())
            {
                _host.Speech.Speak(Strings.WorldOrbHolds, interrupt: true);
                return false;
            }
            // The game's input-lock gate, exactly as in BeginInteract.
            if (GameInputLock.Held)
            {
                _host.Speech.Speak(Strings.WorldNoControl, interrupt: true);
                return false;
            }
            // The game priced the spot unwalkable (off-mesh, or inside something's footprint - likelier for
            // the Walk verb, which aims at occupied ground on purpose). Refusing silently would leave the
            // player waiting on a walk that never started.
            if (!Drive(point))
            {
                Character main = Main;
                if (main != null && BeginDetour(null, WorldConvert.ToSnv(main.transform.position), point))
                {
                    _host.Speech.Speak(SpokenLine.Join(announcement, Strings.WorldDetour), interrupt: true);
                    return true;
                }
                _host.Speech.Speak(Strings.WorldUnreachable(null), interrupt: true);
                return false;
            }
            _target = null;
            _label = "ground";
            _host.Speech.Speak(announcement, interrupt: true);
            return true;
        }

        // Commit the first leg of a two-leg detour to <paramref name="final"/> (a bare spot, or the
        // stand-spot of <paramref name="target"/>) from <paramref name="from"/>, when a self-opening gate
        // bridges them: walk to the gate spot and watch it (TickDetour). False when no gate bridges the two
        // or the leg itself refuses - then the refusal stands as the plain can't-reach.
        private bool BeginDetour(IWalkTarget target, Snv from, Snv final)
        {
            if (!_gates.TryFindVia(from, final, out Snv via, out string gate)) return false;
            if (!Drive(via)) return false;
            _target = target;
            _label = target == null ? "ground" : string.IsNullOrEmpty(target.Name) ? "target" : target.Name;
            _retries = 0;
            _detour = true;
            _final = final;
            _atGate = false;
            _gateTicks = 0;
            _host.LogInfo($"WalkInteract: {_label} at {final} is behind gate '{gate}'; detouring via {via}.");
            return true;
        }

        // A detour's watch: the first leg arrives at the gate spot, then the real destination is re-issued
        // each frame until the game prices it finite (the gate having opened) or the wait runs out. A first
        // leg that breaks or stalls ends as a bare walk would ("stopped short"): the player heard "moving,
        // detour" and is never left in silence.
        private void TickDetour(Character.MovementStatus status, Snv player)
        {
            if (!_atGate)
            {
                if (status == Character.MovementStatus.COMPLETED || Snv.Distance(player, _dest) <= GroundArrivalDistance)
                {
                    _atGate = true;
                    _gateTicks = 0;
                }
                else
                {
                    int grace = _movedOnce ? StallGraceFrames : StartupGraceFrames;
                    if (status == Character.MovementStatus.BROKEN || _stalledTicks > grace)
                    {
                        _host.LogWarning($"WalkInteract: detour leg toward {_label} ended short ({status}); abandoning.");
                        EndDetour();
                        _host.Speech.Speak(Strings.WorldStoppedShort, interrupt: true);
                    }
                    return;
                }
            }
            if (ResumeFromGate(player))
            {
                _host.LogInfo($"WalkInteract: gate open; resuming toward {_label}.");
                _detour = false;
                return;
            }
            if (++_gateTicks > GateOpenGraceFrames)
            {
                _host.LogWarning($"WalkInteract: gate spot reached but {_label} still prices unreachable; giving up.");
                EndDetour();
                _host.Speech.Speak(Strings.WorldUnreachable(_target?.Name), interrupt: true);
            }
        }

        // Re-issue the real destination from the gate spot: a self-driving target's own click (which walks
        // the party and acts on arrival, so the watch ends here), an orb's stand-spot recomputed from where
        // the character now stands, or the bare spot itself - the latter two continue under the ordinary
        // arrival watch. False while the game still prices it infinite.
        private bool ResumeFromGate(Snv player)
        {
            if (_target == null) return Drive(_final);
            if (_target.InteractWalks)
            {
                if (!_target.Interact(_host.Settings.RunToDestinations.Value)) return false;
                _active = false;
                SpeakPostInteract(_target);
                return true;
            }
            return Drive(_target.Approach(player, out _));
        }

        private void EndDetour()
        {
            _active = false;
            _detour = false;
        }

        /// <summary>Player-initiated cancel (the Stop key): halt the character and say so. Covers both this
        /// verb's own watched walks and a click-driven walk the game is running (a self-driving interact),
        /// which this verb does not track - the controller's isMoving is its live state.</summary>
        public void Cancel()
        {
            if (!_active && !GameMoving()) return;
            StopCharacter();
            _active = false;
            _detour = false;
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
        public void Abandon()
        {
            _active = false;
            _detour = false;
        }

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

            if (_detour) { TickDetour(status, player); return; }

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
            // Gate the interact on the game's own arrival-range test. At a COMPLETED stand-point this
            // holds; if somehow short, Interact() refuses in place rather than acting at the wrong spot.
            // An in-range Interact() can refuse too (the game declines to engage an undrawn orb - see
            // OrbProxy.Interact). Either refusal is spoken, not just logged: the player heard "moving to"
            // and must never be left waiting in silence - short of range is a can't-reach, an in-range
            // refusal a can't-trigger.
            bool inRange = _target.WithinInteractionRadius(player);
            if (!inRange)
                _host.LogWarning($"WalkInteract: arrived near {_label} but outside its interaction radius; interacting anyway.");
            if (_target.Interact()) { SpeakPostInteract(_target); return; }
            _host.LogWarning($"WalkInteract: Interact on {_label} returned false at the stand-point.");
            _host.Speech.Speak(inRange ? Strings.WorldTriggerRefused(_target.Name)
                                       : Strings.WorldUnreachable(_target.Name), interrupt: true);
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

        private static Character Main
        {
            get { Party p = Party.Player; return p != null ? p.Main : null; }
        }
    }
}

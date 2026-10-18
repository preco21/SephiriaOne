# Zombie Survival design

User-requested host-only team mode, reusing Play Tag infrastructure. Confirmed:
one-second respawn; optional conversion on any Human death, default off;
an unconverted Human counts as surviving at timeout even while awaiting respawn.

## Rules and controls

Capture the selected current connections (empty selection means all); require at
least two ready players. After a three-second countdown choose one living entrant
uniformly as Zombie. Everyone else is Human. One round defaults to 120 seconds,
configurable 3..3600 for the next match. Zombies persist after death. A confirmed
Zombie kill infects a Human; the new Zombie becomes combat-active only after a
successful native revival. Optional any-death conversion observes completed native
deaths, not polling alone, and applies to future deaths when edited live.

At expiry, any unconverted Human wins for the Humans, including temporarily dead
Humans. Infection must complete before the deadline. All Humans infected before
expiry wins for Zombies; pending infected players count toward that outcome.
Finish results include every entrant's name, final Human/Zombie role and shared
K/D/A. Cancellation is labelled stopped, not a fabricated victory.

Add `/one zombie start|stop|status|help|resetkda`, `duration N|reset`,
`any-death on|off|reset`, `refill on|off|xN|N|reset` and
`damage on|off|N%|N|reset`, plus a compact Zombie tab and shared participant editor.
Zombie modifiers independently default enabled, x2 refill and 75% incoming PvP
damage. Refill requires global PvP balancing; incoming scaling follows existing
Tag semantics even with balancing off. Numeric ranges and parsing match Tag.
Changing settings does not replenish damage budgets or reset scores/roles.

## Architecture and alternatives

Use a small pure team-state model and a `CombatMatchRuntime` partial adapter,
retaining the existing countdown, receipt roster, death attribution, revival queue,
chat/native FX, match token and cleanup. Generalize chaser modifier value/parsing
and impact-time modifier selection for both Tag and Zombie. Persistent policy
holds only configuration; transient roles and matches are never saved.

Cloning Tag controllers would duplicate lifecycle fixes; reject it. Changing
native faction/player stats would risk PvE and require restoration; reject it.
The selected approach extends the existing hostility policy: only opposite teams
are hostile, including companions, homing, auto-targeting, debuffs and reflection
through their current owner mapping. Pending conversions cannot attack as their
old team. Old callbacks cannot apply a conversion to a new run/connection.

An explicit role revision conservatively limits elapsed refill when roles or
settings change. No mutable native stats are applied. On conversion, clear stale
life/assist state at the existing life boundary without destroying final K/D/A.

## Lifetimes and failure handling

Selection/master-toggle operations stop the match. Any captured connection
retirement cancels the match rather than silently replacing an essential team.
Late joins are spectators until next match. Authority/run/generation replacement,
unload and stop clear all roles/timers and recover pending eligible players using
existing deferred cleanup. Loading blocks respawn, not the active round timer;
warmup waits for living ready entrants before choosing the initial Zombie.
Revival/death errors must not activate an incompletely restored Zombie.

## Validation

Exercise real shared-runtime code with native fixtures: random initial role,
opposing-team policy, both conversion policies, one-second/manual revival,
deadline races, last conversion, environmental timeout survival, callbacks and
reconnects, live role bonuses, no replenishment on edits, K/D/A results, commands,
preset compatibility, EN/KO parity and UI wiring. Run Tag/Tournament/Deathmatch
regressions and native compatibility checks. Do not deploy or launch the game.
Live stock-guest presentation remains a separate verification step.

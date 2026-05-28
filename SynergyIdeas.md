# Synergy Design Notes

Living design doc for color-synergy ideas. **Built systems are documented in CLAUDE.md;
this file holds favored-but-unbuilt ideas and discarded directions for reference.**

Design constraints:
- Each colour synergy should *fundamentally change how you play*, not just stat-boost flavoured differently.
- Each should be a distinct playstyle from the others.
- Each should be visually striking — feel cool to trigger.
- Each should be unique to a chain-based puzzle game (use the chain's geometry / movement).

---

## Status overview

| Color  | Theme                                | Status                       |
|--------|--------------------------------------|------------------------------|
| Red    | Explosion (AoE blast)                | **Built** (Phase B)          |
| Purple | Homing + Rage meter                  | **In progress** (Phase C)    |
| Blue   | Ice / time control                   | Favored — see below          |
| Green  | Cascade / chain reactions            | Favored — see below          |
| Orange | Powered chain-consumer (deathray-ish)| Favored — see below          |
| Yellow | (open)                               | Removed from roster — could return as an unlock |

---

## Blue — Ice synergy

### Core direction
Where Red *destroys*, Blue *delays* and *self-clears*. A blue match plants a
slow-acting time-bomb in the chain: balls passing through the icy aftermath
accumulate frost, eventually freeze, and when destroyed launch homing icicles
that can chain-react into other frozen balls.

### V1 — Ice Patches loop (the core build)

Anchor upgrade **`IcePatches`** unlocks the whole loop:

1. **Blue match → ice patch.** Spawns a world-space disc at the match centroid
   (radius and duration in RunConfig; defaults ~2.5 world units, ~5 seconds).
   The patch is purely an effect — it does not block movement.
2. **Pass-through stacks frost.** A ball entering the patch (first contact)
   gets +1 freeze stack. While the ball stays inside, no further stacks accrue
   from that patch. After it leaves AND a re-entry cooldown elapses
   (~2 seconds, RunConfig), re-entering can apply another stack. So slow-moving
   balls / curved paths can still rack stacks over time, but no per-frame spam.
3. **3 stacks → freeze.** Threshold lives in RunConfig (default 3). When a
   ball hits the threshold, it becomes frozen and its stack counter zeroes
   (no double-freeze accumulation). **Frozen balls still move** — they're just
   visibly icy and tagged for the icicle hook.
4. **Frozen ball destroyed (by anything) → icicle.** Match, bomb, pierce, red
   explosion, another icicle — every removal path checks
   `BallNode.isFrozen` and fires `OnFrozenBallDestroyed(worldPos)` from
   `BallChainManager`. The Ice synergy listens and spawns one icicle at the
   destroyed ball's position.
5. **Icicle = single-target homing projectile.** Locks onto a random ball not
   currently being targeted by another icicle, homes to it, destroys on contact.
   `IceSynergy` keeps a `HashSet<Ball>` of currently-targeted balls so multiple
   icicles spread out instead of dogpiling.
6. **Chain reaction.** Icicles can target frozen balls; destroying a frozen
   ball spawns another icicle. One blue match can cascade into a self-clearing
   chunk of the chain.

*Implementation hooks:* `IcePatch` is a plain data class (center, radius,
expiryTime, `Dictionary<BallNode, BallState>` for per-ball entry/exit
tracking). `IceSynergy` is a scene MonoBehaviour mirroring `ExplosionSynergy`'s
pattern — subscribes to `OnMatchVisual` (spawn patches) and to
`OnFrozenBallDestroyed` on `BallChainManager` (spawn icicles). `Icicle` is a
lightweight pooled MonoBehaviour with a locked `Ball` target.

### Documented-but-unbuilt follow-up upgrades

These are designed-and-locked but **not in V1** — separate scoped builds.

- **Cryo Burst** *(anchor, prereq: `IcePatches`)* — blue matches gain an
  AoE freezing ring on top of the normal destruction. Each ball in the AoE
  takes +1 freeze stack (one-shot, not pass-through). Further upgrade:
  destroyed frozen balls *also* emit a cryo burst, compounding the chain
  reaction.

- **Chain Slowdown** *(count-scaled, every blue synergy upgrade)* — for X
  seconds after any blue match, the chain moves slower (e.g. baseline 1.0×
  → 0.8× per blue upgrade owned). Slows speed, *not* duration.

- **Longer Slow** *(stacking card)* — extends the slowdown window after a
  blue match (e.g. 1.0s → 1.2s per rank). Pairs with Chain Slowdown.

- **Freeze the Hunted** *(stacking card, prereq: `IcePatches`)* — icicles
  prioritise targeting already-frozen balls when one is available, falling
  back to random untargeted otherwise. Makes the chain-reaction fantasy more
  reliable.

- **Frost-stack threshold reduction** *(stacking card)* — lowers the
  stacks-to-freeze count from 3 (RunConfig default), floored at 1. Mirrors
  RedExplosionThreshold.

### Discarded / superseded notes
- The original "Cryo burst freezes balls in place" reading (chain advances
  past stationary balls) was the wrong mental model — the V1 design above
  is what's authoritative. Frozen balls still move.
- "Don't freeze the entire chain" still applies — V1 doesn't pause anything;
  the slowdown is a separate follow-up upgrade with bounded magnitude.

---

## Green — Cascade synergy

### Core direction
Cascades are the payoff — *plan one shot that fires a sequence*. Goal: make the
"big-cascade" moments feel cinematic, not just rewarding numerically.

### Favored mechanics — visually epic

- **Verdant bloom.** A green match plants a seed. Over the next ~0.5–1 second, adjacent
  non-green non-power-up balls in the same segment visibly *turn green* (vines / growth
  animation creeping from match site to neighbours). Newly-greened balls can then match
  in cascade. Player plants a seed; chain reaction unfolds.
  *Implementation hook:* deferred colour conversion on `BallNode`s adjacent to a removed
  green match; respects power-up balls.

- **Resonance burst.** Each cascade in a sequence builds a "resonance" charge. When the
  sequence ends, the *chain itself rings* — one ball per resonance point pops in a
  rapid sequence, each with a tiny delay for the cascade-of-cascades feel.
  *Visual:* glowing pulse on the chain during sequence; on resolution, a cinematic
  rapid pop-sequence ball by ball, slight time-dilation, camera kick.
  *Implementation hook:* listen to `MatchProcessor.OnMatchSequenceComplete(cascadeCount, ...)`;
  queue N random ball destructions on a coroutine, one per ~80ms.

- **Echo match.** Each green match has a chance to "echo" — repeat itself on adjacent
  positions a beat later as a free cascade match.
  *Visual:* ghostly translucent second match plays after the first; same VFX, lower
  opacity.

### Visual epicness pass
- Screen-shake scales with cascade count (1 = tiny, 5+ = noticeable).
- Match SFX pitch-shifts up with each cascade in a sequence.
- Verdant Bloom's plant/vine theme is visually distinct from red's blast — they
  shouldn't read alike.
- Resonance burst should feel like a *finisher* — slow time briefly, camera focus pull.

### Count-scaled idea
Each green synergy upgrade extends the bloom radius (more spread per match) *or*
increases echo chance *or* multiplies the resonance-per-cascade rate.

---

## Orange — Powered chain-consumer / Deathray

### Core direction
Other shots *insert* a ball into the chain. The orange power *eats* the chain —
unique to a chain-based game because it leverages the chain's geometry as the playfield.

### Variant A — Travelling consumer ★ preferred
A specially-charged orange ball that, on hitting the chain, **does not insert**.
Instead it switches modes and *travels along the chain spline*, destroying every ball
it touches, then dissipates after a fixed distance / N balls.

- *Visual:* projectile transitions into a comet/flame ball that surfs the spline; each
  consumed ball gets a fiery vanish; trailing sparks; slight screen distortion.
- *Implementation hook:* on collision, the projectile transitions from free-flight to
  "path-rider" mode — it tracks the chain segment's path, advances at a high speed
  along path-progress, calls `RemoveBallAtIndex` for each ball whose collider it
  overlaps along the way, stops when its budget runs out.
- *Self-contained scope:* bounded budget = no runaway. Doesn't suffer the deathray
  gap-close problem because each "step" along the chain is finite and the budget caps
  total destruction.

### Variant B — Player-controlled deathray
A sustained beam from Yuumi that the player aims; any ball touching the beam is
destroyed.

- *Visual:* dramatic continuous beam from Yuumi to the cursor; balls that touch it
  burn and pop.
- *Known issue (flagged):* gap-close runaway. After balls are destroyed, the front
  segment closes the gap — with a sustained beam in place, the gap-close drags MORE
  balls into the beam, which destroys them, which closes the gap further, …
  Possible mitigations to evaluate when building:
  - Pause gap-close while the beam is active.
  - Single-pulse only — destroys balls touching the beam at the *instant* it fires.
  - Finite "fuel" pool that depletes with destruction.
- Higher complexity / balance risk than Variant A.

### Favored
**Variant A (travelling consumer)** — bounded, dramatic, fits the chain theme, no
gap-close runaway. Build this first; consider Variant B later as a higher-tier
alternative.

### Triggering options
- Chargeable: matches build a meter; full meter unleashes the consumer on next shot.
- Power-up type: added to the inventory like Bomb/Pierce, equipped before firing.
- Match-driven: an orange match itself spawns a consumer ball that's already in the
  chain and rolls forward (no aiming required — fire-and-forget AoE through the chain).

### Count-scaled idea
Each orange synergy upgrade extends the travel distance / consumption budget.

---

## Yellow — open

Drawing a blank on a distinctly-themed yellow synergy that meets the "fundamentally
changes the game" bar. Open prompts to revisit later:

- **Yuumi-themed buff layer** — a "blessing" / temporary buff system that yellow
  matches charge and unleash. Different from purple rage in scope.
- **Chain-length payoff** — yellow rewards *surviving with a long chain*: bigger chain
  → bigger yellow effects. Inverts the "destroy as much as possible" pressure.
- **Ball conversion / anti-destruction** — yellow matches don't destroy; instead they
  pluck balls *off* the chain (saved for later) or convert them to "blessed" balls
  with bonus effects.

---

## Meta-progression ideas

### Colour unlock progression
Instead of starting with the full colour roster, the player begins runs with
only 3 colours active. Beating the game unlocks the 4th colour and all its
associated synergy upgrades; beating it again unlocks the 5th. Unlocks could
alternatively (or additionally) be gated behind essence cost in the meta shop
— buy the new colour with essence as a backup path for players who can't yet
clear the final floor.

**Why this works:**
- Adds a long-term progression arc on top of per-run unlocks — players watch
  the game *grow* as they win.
- Reduces early-game decision paralysis: fewer colours = fewer synergy paths
  to weigh per draft.
- Gives each newly-unlocked colour a spotlight moment — the first run after
  unlocking feels like a fresh experience of the game.
- Lets us tune synergy difficulty per colour: lock the "weirder" / higher-skill
  synergies (Blue ice timing, Orange consumer routing) behind later unlocks,
  while the starter three are the most readable (e.g. Red blast, Green
  cascade, Purple rage).

**Open questions:**
- Unlock trigger: first clear of the final floor? Essence cost in the meta
  shop? Both (essence as a backup if a player struggles to win)?
- Are the 3 starting colours fixed (e.g. Red/Green/Purple) or does the player
  pick at run start?
- Upgrade pool size — early runs have fewer synergy upgrades available, so
  draft pools shrink. Is that fine, or do we need extra non-synergy stat
  cards to fill the gap?
- Does the `LevelData.colorCount` cap get gated by unlocks, or does the level
  stay authored at the full count and the spawner filters to unlocked
  colours? (Probably the latter — keeps authoring and progression cleanly
  separated.)

### Pre-run colour loadout (a.k.a. "player-built ball chain")

Replace the fixed first-N-of-the-enum unlock rule with a **player-built
loadout** screen between runs. As the player unlocks colours, they can
choose which ones go into the active palette for the next run.

- **Loadout UI**: a stationary or slowly-rotating ring of "slots" — a visual
  ball chain on the main menu. The player drags unlocked colour orbs in to
  add them to the run, or out to remove them. Acts as the inventory + run
  config in one widget.
- **Minimum 3 colours.** Below that, the puzzle stops working — no matches
  to be made.
- **Higher colour count = harder run.** Spawn pool widens, fewer 3-in-a-row
  chances per draw. The colour ring is effectively a self-set difficulty
  slider.
- **Synergy-aware build crafting.** Player picks their synergy strategy at
  loadout time: drop in Red+Purple for explosive homing, swap Purple for
  Blue for an ice deck, etc. Pairs with the colour-synergy upgrade pool —
  drafting a Purple synergy upgrade is only useful if Purple is in the
  loadout.
- **Reward hook**: more colours in the run could grant a passive bonus
  (extra essence at run end? a bonus draft? bigger gold rewards?) so the
  player isn't always incentivised to play the easiest 3-colour run.

**Implementation notes:**
- `PlayerProfile.unlockedColorCount` becomes `unlockedColors[]` (per-colour
  bool array) — generalises the count to an explicit set.
- A new `ActiveColors[]` runtime palette on `RuntimeStats` — the loadout
  writes this at run-start.
- `BallSpawner` / `ProjectileSpawner` colour pickers consult `ActiveColors`
  instead of "first N enum entries". `BallColorUtils.GetRandomColor` and
  `GetWeightedRandomColor` need a "from this allowed-set" overload.
- Loadout UI lives on the main menu scene. Drag-and-drop pattern (Unity UI
  drag handlers) on coloured orb prefabs.
- Reward bonus hooks tie into the existing essence-reward formula in
  `RunManager.GrantEssenceReward`.

Lots of upside — gives meta-progression a tactile, player-driven shape and
naturally introduces "deck building" without needing a separate system.
Holdup is UI work + the enum-to-bool-array migration.

---

## Discarded directions

| Idea                            | Why discarded                                              |
|---------------------------------|------------------------------------------------------------|
| Yellow econ (shop discounts, gold scaling) | Too flat — "more gold" doesn't reshape moment-to-moment play. |
| Orange recoil (push chain back) | Boring; just amplifies an existing passive behaviour.      |
| Generic per-card stat boosts    | The whole synergy point is changing *how* you play, not just numbers up. |

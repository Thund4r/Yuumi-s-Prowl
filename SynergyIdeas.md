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
| Yellow | (open)                               | No favored direction yet     |

---

## Blue — Ice synergy

### Core direction
Where Red *destroys*, Blue *delays* — the chain becomes a malleable timeline,
not a relentless conveyor. The player is buying time and reshaping pressure.

### Favored mechanics (mix and match)

- **Ice patches on the path.** A blue match leaves a frozen segment of the spline path
  at the match site. Balls passing through it pick up frost stacks (visual: balls
  visibly frosting/cracking). At N stacks (say 3) the ball shatters. Multiple matches
  near each other build *kill zones* along the chain.
  *Implementation hook:* a path-progress range marker; per-`Ball` stack counter that
  ticks up when its progress crosses an ice patch; remove at threshold.

- **Cryo burst.** Blue match emits a freezing ring at the match site; every ball inside
  the ring freezes in place for a window. The frozen balls don't move while the rest
  of the chain still does — creates a gap as the chain advances past them. Frozen
  balls remain matchable.
  *Visual:* expanding ice ring; frozen balls tint icy and stop.
  *Implementation hook:* per-`BallNode` "frozen until time T" flag respected by
  `BallChainManager.MoveChain`.

- **Icicle launchers (from frozen balls).** *Icicles aren't spawned by matches — they're
  spawned by frozen balls themselves.* While a ball is frozen, it periodically launches
  1–2 homing icicles. Each icicle tracks a random ball in the chain (any colour) and
  destroys it on contact. Reuses Step 8 homing + pass-through.
  - **Chain reaction:** when a frozen ball is *destroyed* (by anything — match, icicle,
    bomb), it bursts out a final volley of icicles before vanishing. So one icicle that
    hits a frozen ball triggers more icicles → those can hit other frozen balls → more
    bursts → runaway cascade.
  - **Scaling:** more frozen balls = more icicle launchers in flight. The "fundamental
    change" is that the chain becomes self-clearing once enough freeze stacks up.
  - *Visual:* icicles materialise off the frozen ball's surface and dart toward targets;
    death-burst is a radial spray.
  - *Implementation hook:* per-`Ball` frozen-state timer that ticks icicle launches; a
    death-burst hook fires extra icicles when a frozen ball leaves the chain; reuses the
    `Projectile` homing core as a lightweight icicle projectile.

### Tweaks noted
- Don't freeze the *entire* chain — too dominant. Freeze a segment, or a path-range,
  not everything.
- Frozen balls should still be matchable — opens cool interactions with red explosions.
- Question: should freeze duration scale with match size, blue synergy count, or both?

### Count-scaled idea
Each blue synergy upgrade extends freeze duration *or* adds +1 to the shatter-stack
count from ice patches *or* adds 1 icicle to the volley.

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

## Discarded directions

| Idea                            | Why discarded                                              |
|---------------------------------|------------------------------------------------------------|
| Yellow econ (shop discounts, gold scaling) | Too flat — "more gold" doesn't reshape moment-to-moment play. |
| Orange recoil (push chain back) | Boring; just amplifies an existing passive behaviour.      |
| Generic per-card stat boosts    | The whole synergy point is changing *how* you play, not just numbers up. |

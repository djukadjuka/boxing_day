# Box Carry, Stack, Drop, Throw & Rotate System

Reference for the box interaction system in **Boxing Day** — how a player picks up,
carries, stacks, drops, throws, and rotates boxes, plus the weight model that ties it all
together. This is the player's core verb set, so most of the game's feel lives here.

All behavior is in two scripts plus a couple of look/input helpers:

| File | Role |
|---|---|
| `Assets/Scripts/Behaviours/GenericBoxBehaviour.cs` | The box itself: carry physics, stacking, drop, throw, rotate, weight, placement preview. |
| `Assets/Scripts/StateManagement/Interaction/InteractionStateManager.cs` | Input + ownership of what's carried (`PickUp` / `Drop` / `Throw` / `HandleRotateInput` / `HandleVerticalInput`). |
| `Assets/Scripts/StateManagement/Interaction/{Idle,Focused}InteractionState.cs` | Call the per-frame input handlers (drop/throw/rotate/vertical) regardless of focus. |
| `Assets/Scripts/StateManagement/Look/AimStateManager.cs` + `NormalAimstate.cs` | `LookSuppressed` flag freezes the camera while rotating a box. |
| `Assets/Scripts/Options/KeyBindings.cs` | Key constants. |

> **Render pipeline:** Built-in. **Camera:** Cinemachine vcam following `PlayerForward`,
> tuned to read first-person (see CLAUDE.md → "Camera / First-Person View Tuning").

---

## Controls

| Action | Key | Notes |
|---|---|---|
| Pick up / interact | **F** | Picks up the box you're looking at — and the **whole column** above it. |
| Drop | **G** | Lets go: the box (or stack) free-falls straight down. |
| Throw | **T** | Hurls the box/stack forward; weight decides how far. |
| Rotate held box | **R** (hold) + move mouse | Single box only (not a stack). Tumbles the box; camera holds still. |
| Raise / lower held box | **V** (hold) + move mouse | Works for a whole stack. Nudges the box/stack straight up/down; camera holds still. Too-heavy stacks can't be adjusted. |
| Free look | **Mouse2** (hold) | Box freezes out of view while you look around. |
| Peek L / R | **Q** / **E** | Same view-clearing behavior as free look. |

---

## Core concepts

### Weight (the single source of truth)

Every box has an authored **`weight`** field (default `1`), exposed as `public float Weight`.
`ApplyWeight()` mirrors it onto the `Rigidbody.mass` in `Start` (runtime) and `OnValidate`
(edit-time, so the Mass updates live as you type).

- **Set weight via the `Weight` field**, *not* the Rigidbody Mass — Mass just follows it.
- **Physics** (push/resist, throw distance) reads `rb.mass`, which always mirrors `Weight`.
- **Gameplay** code (carry feel, and future stamina/lift/HUD/crush rules) should read
  `Weight`, because the carry system may temporarily mutate `rb.mass` while `Weight` stays
  the true value.

### Orientation-independent top & bottom

`TopCenter()` / `BottomCenter()` are derived from the collider's **live world AABB**
(`bounds.max.y` / `bounds.min.y`), not fixed child anchors. So "top" is always the real
upward face — flip a box on its side and the stackable top follows automatically. This is
what makes flipping and rotating boxes "just work" for stacking. (Exact for 90° flips,
slightly over-estimates for diagonal tilts.)

---

## Carry

Pick up a box (F) and it becomes a **camera-driven, smoothed, physical** held object.

- **Dynamic, velocity-driven.** The held box stays a real dynamic Rigidbody (gravity off),
  driven each `FixedUpdate` by setting `rb.velocity` toward a hold point in front of
  `Camera.main` (`forward * holdDistance - up * holdDrop`). Look up → it rises; look down →
  it lowers. Because it's dynamic, **collisions are mass-weighted**: ram a light box and it
  knocks aside, a heavy one resists.
- **Smoothed / weighty.** The target is eased with `Vector3.SmoothDamp` (`carrySmoothTime`)
  so the box lifts and lowers with a sense of weight; the body chases it up to
  `maxCarrySpeed`.
- **Weight scales the feel.** On pickup, `ComputeWeightedCarry()` scales `maxCarrySpeed`
  (heavier → slower) and `carrySmoothTime` (heavier → floatier) relative to
  `referenceWeight`, dialed by `speedWeightInfluence` / `smoothWeightInfluence`. At
  `weight == referenceWeight` the feel is unchanged.
- **Clearance clamp.** Each frame the box's underside is clamped so it can't sink below the
  surface beneath it (`TryGetSurfaceBelow` + `carryClearance`). The upward clamp is instant,
  lowering is smoothed — this is what lets you *pull a box up out of a stack* without
  shoving the box below. (Key detail: the surface ray casts *down from above the box top*,
  because a ray from the flush underside starts inside the box below and Unity skips it.)
- **Orientation preserved (no auto-upright).** Picking a box up does **not** straighten it.
  A box resting on its side stays on its side; its rotation is captured *relative to camera
  yaw* (`carryYawOffsetRot`) and re-applied each frame, so it keeps its tilt/flip while
  **yaw-following** your view. Rotation is left unconstrained and driven by `MoveRotation` +
  zeroed angular velocity (freezing axes would fight a flipped box or block the yaw-follow).
- **View-clearing.** While peeking (Q/E) or free-looking (Mouse2), the box freezes relative
  to the player body so the camera can look past it.
- **Player collision ignored** while carried, so the held box never shoves the player's
  CharacterController. Restored on release.

### Sleep gotcha

A box resting on the floor is asleep, and a sleeping Rigidbody ignores `rb.velocity`. On
pickup the box is woken (`WakeUp()`) and its `sleepThreshold` set to 0 (restored on
release), or it would just stick to the ground.

---

## Stacking

`GetStackAbove()` overlaps a thin slab above the box's top face (recursively) to find the
whole column. Picking up the bottom box carries the entire stack; picking a middle box
takes it plus everything above.

- **Riders** (everything above the carried box) are made **kinematic and parented in place**
  to the carrier (`OnPickedUpAsRider`), kept exactly where they were stacked — **no
  re-centering or uprighting** — so the column's real arrangement and each box's orientation
  are preserved. This is what keeps a box stacked on a side-face glued correctly.
- Each rider's collision with the carrier is **ignored** while carried. (A kinematic rider
  has infinite mass; left colliding, a rider on top would act as an immovable lid and stop
  the dynamic carrier from rising.) Riders stay kinematic+parented rather than joint-welded
  because force-setting the carrier's velocity inside a stiff joint makes the solver explode.
- On drop, `OnDroppedAsRider()` just detaches each rider back to physics in place — the
  carrier was already positioned and the parented riders moved with it, so the column lands
  exactly as it was carried.

---

## Drop (G) — free fall

Dropping is intentionally simple: **just let go.** `OnDropped()` calls `ReleaseCarry()` —
gravity restored, velocity zeroed, no impulse, **no snapping** to the surface below — so the
box falls straight down from where it's held, keeping its orientation and X/Z position.
The whole stack releases in place and falls together.

`TryGetDropPlacement` (raycast straight down, keep orientation) now only feeds the
**placement preview**, predicting that straight-down landing.

---

## Throw (T)

While carrying, T hurls the box (or whole stack) forward.

- `LaunchThrow` applies a constant forward+up impulse (`throwImpulse`, `throwUpFactor`).
  Since `rb.mass == Weight`, a heavier box is thrown **less far automatically** — the throw
  is weight-based with no extra math.
- Riders are un-kinematic'd, unparented, and freed of their collision-ignores, so a thrown
  stack **scatters** into independent dynamic boxes.
- The throw is deliberately **unaligned** ("it's on you") with one exception: while a thrown
  box is armed, if its first real impact is **landing on another box's top face**
  (most-upward contact normal ≥ `topHitNormalThreshold`, and the target isn't itself
  mid-throw), `OnCollisionEnter` zeroes its velocity and `SnapOntoBox` for a neat stack.
  Any other first impact (floor, wall, box side) just disarms it.
- A `throwArmDelay` ignores the launch-moment shove as a stack separates, and the
  "target not thrown" guard stops two tossed siblings snapping onto each other mid-air.

`OnDropped` and the throw share the `ReleaseCarry()` teardown helper.

---

## Rotate a held box (hold R)

Tumble a single carried box to reorient it (e.g. lay it on its side, then stack onto its
new top).

- **Only** when carrying **exactly one box** (no stacks) and not already looking around.
- `InteractionStateManager.HandleRotateInput` sets `AimStateManager.LookSuppressed = true`,
  which makes `NormalAimstate` **freeze the look axes** (camera holds still), and feeds the
  mouse delta to `GenericBoxBehaviour.ApplyManualRotation(mouseX, mouseY)`.
- Mouse **X** spins the box around world-up; mouse **Y** tips it around the camera's right
  axis. The result is baked into `carryYawOffsetRot`, so the new orientation is held and
  **still yaw-follows the camera** after release.
- Because top/bottom come from the live AABB, the box's **new top automatically becomes the
  stackable face**.
- Releasing R — or dropping, throwing, or starting a peek/free-look — clears `LookSuppressed`
  and hands the mouse back to the camera.

Handled as a per-frame modal (like drop/throw), not a separate `BaseState`.

---

## Raise / lower a held box or stack (hold V)

Nudge the carried box — or the **whole carried stack** — straight up or down, independent of
where you're looking. Lets you line a column up with a shelf or a target stack without having
to tilt the camera (which would also move it horizontally).

- Works while carrying **one box or a stack** (unlike rotate, which is single-box only).
- `InteractionStateManager.HandleVerticalInput` sets `AimStateManager.LookSuppressed = true`
  (camera holds still, same as rotate), sums the **combined `Weight` of the whole column**,
  and feeds the mouse-Y delta + that total to `GenericBoxBehaviour.ApplyVerticalAdjust`.
- The carrier accumulates a world-vertical `verticalCarryOffset` that's added to the hold
  point each `FixedUpdate`. It persists for the rest of the carry (reset to 0 on pickup) and,
  because the riders are parented to the carrier, the **whole stack rises/lowers together**.
- **Weight matters.** A heavier column adjusts **slower** (`raiseLowerWeightInfluence`, relative
  to `referenceWeight`), and past `raiseLowerMaxWeight` the stack is **too heavy to handle** —
  `ApplyVerticalAdjust` is a no-op. You can still *lift and carry* such a stack; you just
  can't finesse it up/down.
- The offset is clamped to `±raiseLowerMaxOffset`, and the existing clearance clamp still
  applies, so you can't drive the box down through the surface beneath it.
- Won't run while rotating (R) or looking around (Q/E/Mouse2); releasing V hands the mouse
  back to the camera.

Per-frame modal (like drop/throw/rotate), not a separate `BaseState`.

---

## Placement preview

While carrying, a flat marker shows where the box will land (the straight-down free-fall
prediction from `TryGetDropPlacement`). By default it's an auto-created translucent quad; an
optional `placementIndicatorPrefab` can replace it (author it lying flat, facing +Z).

`ComputeGroundFootprint` sizes/orients the quad to the box's **actual resting footprint in
its carried orientation** — it drops the most-vertical of the three oriented edges and
projects the other two onto the ground. So a box carried on its side shows the correct
face dimensions, not just the upright footprint. Exact for yaw and any 90° flip; approximate
for diagonal tilts.

---

## Tuning knobs (per box, on `GenericBoxBehaviour`)

| Field | Default | Effect |
|---|---|---|
| **Box** | | |
| `weight` | `1` | Source of truth; mirrored onto Rigidbody mass. Heavier = harder to push, slower/floatier to carry, thrown less far. |
| **Carry** | | |
| `holdDistance` | `1.5` | How far in front of the camera the box is held. |
| `holdDrop` | `0.5` | How far below the crosshair it hangs. |
| `carrySmoothTime` | `0.08` | Lift/lower smoothing. Higher = floatier/heavier. |
| `carryClearance` | `0.02` | Gap kept above the surface beneath while carried. |
| `maxCarrySpeed` | `8` | Top speed the held box is driven at. |
| `referenceWeight` | `1` | Weight at which the carry values above apply unchanged. |
| `speedWeightInfluence` | `1` | How strongly weight scales carry speed (0 = ignore, 1 = inverse-proportional). |
| `smoothWeightInfluence` | `0.5` | How strongly weight scales the lift float. |
| **Throw** | | |
| `throwImpulse` | `6` | Forward impulse on throw (mass decides distance). |
| `throwUpFactor` | `0.2` | Upward arc added to the throw. |
| `topHitNormalThreshold` | `0.5` | How "top-down" a landing must be to snap-stack. |
| `throwArmDelay` | `0.08` | Seconds after a throw during which collisions are ignored. |
| **Rotate** | | |
| `rotateSensitivity` | `3` | Degrees the box turns per unit of mouse movement while rotating. |
| **Vertical (hold V)** | | |
| `raiseLowerSensitivity` | `0.2` | Metres the box/stack moves per unit of mouse movement while raising/lowering. |
| `raiseLowerMaxOffset` | `2` | Max distance the hold point can be nudged up/down from default. |
| `raiseLowerMaxWeight` | `8` | Total carried weight above which the stack is too heavy to raise/lower (still carryable). A single heavy box (~6) is adjustable; a stack of them isn't. |
| `raiseLowerWeightInfluence` | `0.5` | How strongly total weight slows the raise/lower (0 = ignore, 1 = inverse-proportional). At 0.5 a weight-6 box moves ~2.4× slower than a weight-1 box. |
| **Placement Preview** | | |
| `placementIndicatorPrefab` | — | Optional custom marker; auto-quad if empty. |

---

## Box art & types

Box face textures are hand-authored **SVG** in `Assets/Textures/Boxes/` (one square texture
maps to every cube face). See that folder's `README.md` for converting them to PNG and the
retro import settings (Point filter, no compression, low res).

| Type | Texture | Material | Weight | Look |
|---|---|---|---|---|
| Regular | `box_regular.svg` | `GenericBoxMaterial` | 1 | Tan cardboard, tape, shipping label, barcode. |
| Heavy | `box_heavy.svg` | `box_heavy_material` | ~6 | Darker, strapping bands, metal corners, "HEAVY / TEAM LIFT". |
| Fragile | `box_fragile.svg` | `box_fragile_material` | 1 | Red FRAGILE markings, broken-glass icon, hazard stripes. |

Heavier boxes are slower/floatier to carry and thrown less far automatically (mass mirrors
`Weight`). Fragile is a hook for a future "breaks if dropped/thrown too hard" rule.

The first-level boxes live in `SampleScene` at +X (open area): `Box_Heavy_1/2`,
`Box_Fragile_1/2`, `Box_Regular_1` — `GenericBox` prefab instances with per-box material,
`weight`, and prompt overrides, dropped from y≈1 to settle on the floor (top at y≈0.05).

## Editor gotchas

- **Script edits during Play don't apply** until you stop, let it recompile, and re-enter
  Play.
- **Esc force-frees the cursor** in the editor; it re-locks on the next click into the Game
  view. Editor-only — a build re-locks immediately. (Relevant to the pause menu, not boxes,
  but worth knowing.)
- The Player root is uniform scale `(1,1,1)` so carried/childed objects don't inherit a
  vertical stretch.

---

## Known trade-offs & future upgrades

- **Combined stack mass on throw/carry.** Riders ride kinematically, so a stack's mass does
  *not* add to the carrier's knock-over force — only the bottom (dynamic) box is
  mass-correct. The clean upgrade (no joints): on pickup, add the riders' `Weight`s to the
  carrier's `Rigidbody.mass` and capture/restore it like the other `prev*` fields.
- **Diagonal-tilt footprint.** The placement-preview footprint is a rectangle, so a box held
  at a non-90° tilt only approximates the true (parallelogram) footprint. Position is still
  correct.
- **Rotate feel.** `rotateSensitivity` depends on the project's mouse-axis scaling; tune to
  taste. Flip the sign on the `mouseX`/`mouseY` term in `ApplyManualRotation` if either axis
  feels inverted.

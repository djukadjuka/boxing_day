# Boxing Day — Project Context for Claude

## What This Is

**Boxing Day** is a first-person simulation/puzzle game built in Unity (C#). The player works as a warehouse worker at *Cartwright Trading Co.* in the fictional town of Harbor Glen, Bellmont, set in 1990. The core loop is: stack boxes correctly before your shift ends → earn money → pay survival bills → upgrade gear → repeat until promoted (win condition).

## ⚠️ Standing TODOs — surface these at the start of every session

> **Instruction to Claude:** Whenever this file is loaded, list the open TODOs below to the user as a brief reminder. When a TODO is done, remove it (or have the user confirm) instead of leaving it stale.

- [ ] **Create the heavy and light (fragile) boxes in Blender** the same way the regular box was done — assign the per-face materials, then make their prefabs (duplicate `Box_Regular`, swap the 6 materials, set `Weight`). Full pipeline in [`tutorials/png_to_materials_and_uv_unwrap.md`](tutorials/png_to_materials_and_uv_unwrap.md).

## Repository Layout

```
boxing_day/
├── BoxingDay/                  # Unity project root
│   ├── Assets/
│   │   ├── Scripts/            # All game C# code (see below)
│   │   ├── Prefabs/
│   │   ├── Scenes/
│   │   └── Materials/
│   ├── BoxingDay.sln
│   └── Assembly-CSharp.csproj
├── GDD.md                      # Full Game Design Document
├── Notes and Ideas.md          # Lore/NPC notes
├── progress.md                 # Dev specs and TODO tracking
└── Universe_history.md         # World lore
```

## Scripts Structure

```
Assets/Scripts/
├── CursorManager.cs               # Static helper: Lock()/Unlock() cursor (lock+hide / free+show)
├── PauseMenuManager.cs            # In-scene pause menu: Esc toggles, freeze-frame blur bg, timeScale=0
├── Behaviours/
│   ├── GenericBoxBehaviour.cs      # Carryable box: camera-driven hold, stacking, snap-on-drop, placement preview
│   ├── GenericObjectBehaviour.cs   # Stub for generic interactables (not yet implemented)
│   └── PlayerPointerBehaviour.cs   # Legacy raycast+UI script (superseded by InteractionStateManager)
├── DataHolders/
│   └── PlayerData.cs               # Serialized player stats: runningSpeed, sprintingSpeed, MaxStamina, CurrentStamina
├── Interactables/
│   ├── IInteractable.cs            # Interface: Interact(), GetPrompt(), OnFocusEnter(), OnFocusExit()
│   └── InteractableBase.cs         # Abstract MonoBehaviour implementing IInteractable; holds cursorPromptText
├── Options/
│   └── KeyBindings.cs              # Static key constants (WASD, Sprint, FreeLook, Peek L/R, Interact=F, Drop=G, Throw=T, Rotate=R, CursorUnlock=Esc)
└── StateManagement/
    ├── BaseState.cs                # Abstract: Enter/Exit/Update(BaseStateManager)
    ├── BaseStateManager.cs         # Abstract MonoBehaviour: currentState, previousState, ChangeState()
    ├── MasterStateManager.cs       # Holds refs to all sub-managers + PlayerData
    ├── Interaction/
    │   ├── InteractionStateManager.cs  # Raycast, UI prompt, PickUp (whole stack) / Drop / Throw / Rotate (single box), IsCarrying, HandleDropInput/HandleThrowInput/HandleRotateInput, holdPoint
    │   ├── IdleInteractionState.cs     # Waiting; also calls HandleDropInput; transitions to Focused on raycast hit
    │   └── FocusedInteractionState.cs  # Focused on an interactable; calls HandleDropInput; fires Interact() on KEY_INTERACT
    ├── Look/
    │   ├── AimStateManager.cs      # Cinemachine xAxis/yAxis, peek (Q/E), Normal↔FreeLook, IsLookingAround/IsPeeking/IsFreeLooking, LookSuppressed (pauses look while rotating a box)
    │   ├── NormalAimstate.cs       # Standard mouse-look; enters FreeLook on Mouse2 hold
    │   └── FreeLookAimState.cs     # Body locked, camera free; exits on Mouse2 release, restores axes
    └── Movement/
        ├── MovementStateManager.cs # CharacterController, gravity, IsGrounded sphere check, IsMoving()
        ├── IdleMovementState.cs    # No input → Idle; transitions to Running or Sprinting
        ├── RunningState.cs         # Sets speed = runningSpeed from PlayerData
        └── SprintingState.cs       # Sets speed = sprintingSpeed from PlayerData

Assets/Shaders/
└── FreezeFrameBlur.shader         # Built-in pipeline Kawase blur, used by PauseMenuManager via Graphics.Blit
```

**Render pipeline:** Built-in (no URP/HDRP). Relevant for any screen/post effects (e.g. the pause blur uses `Graphics.Blit` + a `Hidden/` shader, not a Renderer Feature).

## Architecture Patterns

- **State Machine pattern** throughout: each system (Movement, Look, Interaction) has a `BaseStateManager` subclass and a set of `BaseState` subclasses. `ChangeState()` calls `Exit` on the old state and `Enter` on the new one.
- **MasterStateManager** is the top-level hub — all sub-managers grab a reference to it via `GetComponent<MasterStateManager>()` in their `Start()`. Sub-managers access shared data (e.g. `PlayerData`) through it.
- **IInteractable / InteractableBase** form the interaction contract. Any world object that can be interacted with implements `IInteractable` (via `InteractableBase`).
- **Cinemachine** is used for the camera. The Virtual Camera *Follows and LookAts* `PlayerForward` (a child of the Player root) with a ThirdPersonFollow body, so `PlayerForward` pitching is what produces the vertical look. `AimStateManager` drives `xAxis` (root yaw) and `yAxis` (PlayerForward pitch).
- `PlayerPointerBehaviour` is a legacy script — the interaction logic has been moved into `InteractionStateManager`. Treat it as dead code unless told otherwise.

## Box Carry / Stack System (current — rebuilt this iteration)

> **Full standalone reference:** [`BoxSystem.md`](BoxSystem.md) — controls, all mechanics, the complete per-box tuning table, and gotchas in one place. The summary below is the architecture-level view.

The earlier "anchor each box to the previous box's `BoxTop`, parented to PlayerForward" approach was replaced. Current behavior, all in `GenericBoxBehaviour` + `InteractionStateManager`:

- **Carry is camera-driven, smoothed, and physical (Stage 2).** On pickup the carried (bottom) box is unparented and kept a **real dynamic Rigidbody** (gravity off, **rotation unconstrained** — driven each step by `MoveRotation` with angular velocity zeroed, interpolation on), then driven in **`FixedUpdate`** by setting `rb.velocity` toward a hold point in front of `Camera.main` (`forward * holdDistance - up * holdDrop`). The *target* is eased (`Vector3.SmoothDamp` into `easedTargetBottom`, `carrySmoothTime`) for weight; the body chases it at up to `maxCarrySpeed`. Look up → box rises; look down → it lowers. Because the box is dynamic, **collisions are mass-weighted** — ram it into a light box and it knocks aside, a heavy one resists. Driving from the real camera (not the rig) is deliberate — robust to the Cinemachine setup, avoids the Player root's scale.
  - **Sleep gotcha:** a box resting on the floor is asleep, and a sleeping Rigidbody ignores `rb.velocity`. On pickup we `WakeUp()` and set `sleepThreshold = 0` (restored on drop), or the box just sticks to the ground.
- **Clearance clamp (no more pickup shove).** Each frame the carried box's underside is clamped so it can never sink below the surface currently beneath it (`TryGetSurfaceBelow` + `carryClearance`). Lowering is smoothed but the upward clamp is instant, so the box can't penetrate whatever is under it. This is what lets you pull a box out of a stack — it floats at the top of the box below until it's pulled clear, then lowers to the hold point — without shoving the lower box. **Key detail:** `TryGetSurfaceBelow` casts *down from above the box's top*, because a ray started at the flush underside begins *inside* the box below and Unity skips it (the ray falls through to the floor — that was the original shove bug).
- **Orientation-independent top/bottom.** `TopCenter()` / `BottomCenter()` are derived from the collider's live world `bounds` (`max.y` / `min.y`), not from fixed child anchors. So "top" is always the real upward face even when a box is flipped on its side — any face is stackable and the topmost is always chosen. (The old `BoxTop`/`BoxBottom` child transforms are no longer referenced; AABB is exact for 90° flips, slightly over-estimates for diagonal tilts.)
- **Carry preserves orientation (no auto-upright).** Picking a box up does **not** straighten it — a box resting on its side stays on its side while carried, so whatever face is up stays the top. On pickup the box's rotation is captured *relative to the camera yaw* (`carryYawOffsetRot`); each `FixedUpdate` that offset is re-applied on top of the live camera yaw, so the box keeps its tilt/flip while still **yaw-following** your view. (Rotation is left **unconstrained** and driven each `FixedUpdate` by zeroing angular velocity then `MoveRotation`: with an arbitrary held orientation, freezing X/Z would fight a flipped box, and freezing Y would block `MoveRotation` from yaw-following.) Drop/throw also keep the carried orientation — it lands the way it was held. Earlier builds force-uprighted on pickup, which left a box stacked on a side-face stranded on what was now a side; preserving orientation fixes that.
- **Peek / free-look clears the view.** While `AimStateManager.IsLookingAround` (peek Q/E or free-look Mouse2), the box freezes relative to the player body (snapshot pose on entry) instead of tracking the camera, so the camera can look around it. The ease velocity is reset on resume so it doesn't lurch.
- **Player collision ignored while carried.** `Physics.IgnoreCollision` between the held box and the player's colliders, restored on drop — stops the held box shoving the CharacterController (e.g. when held low looking down). `TryGetSurfaceBelow` also skips the player.
- **Stacked pickup.** `GetStackAbove()` overlaps a thin slab above the box's top face (recursively) to find the column. Picking the bottom box carries the whole stack; "riders" are made **kinematic and parented in place** to the carrier (`OnPickedUpAsRider`) so they follow rigidly, kept exactly where they were stacked — **no re-centering or uprighting**, so the column's real arrangement and each box's orientation are preserved (this is what keeps a box stacked on a side-face glued correctly). Picking a middle box takes it + everything above. On drop, `OnDroppedAsRider()` just detaches each rider back to physics in place (the carrier was already positioned and the parented riders moved with it), so the column lands exactly as carried.
  - **Critical:** each rider's collision with the carrier is **ignored** (`Physics.IgnoreCollision`, restored on drop). A kinematic rider has infinite mass, so a rider sitting on top would act as an immovable lid and stop the dynamic carrier from rising — the whole stack would refuse to lift. (Riders stay kinematic+parented rather than `FixedJoint`-welded because force-setting the carrier's velocity while it's in a stiff joint made the solver explode and fling the stack across the level.)
- **Drop = just let go (free fall).** `OnDropped` simply calls `ReleaseCarry()` — gravity restored, velocity zeroed, no impulse — so the box **falls straight down** from where it's held, keeping its carried orientation and X/Z position. No snapping or centering onto the surface/box below (a deliberate user choice — it reads more naturally than the old teleport-snap). Boxes stay dynamic. `TryGetDropPlacement` now only feeds the **preview**, predicting that straight-down landing (raycast down from the bottom-center to the first surface, keeping orientation). (`SnapOntoBox` — center on a box's top while keeping our own orientation — is used only by the throw-landing snap.)
- **Placement preview.** While carrying, a flat marker (auto-created translucent quad, or an optional prefab) shows where the box will land, using `TryGetDropPlacement` (the straight-down free-fall prediction). `ComputeGroundFootprint` sizes/orients the quad to the box's **actual resting footprint in its carried orientation** (drops the most-vertical of the three oriented edges, projects the other two onto the ground) — so a box carried on its side shows the correct face dimensions, not just the upright footprint. Exact for yaw and any 90° flip; approximate for diagonal tilts.
- `KEY_DROP` (G) drops; `IsCarrying` guards against picking up more while holding.
- **Rotate a held box (`KEY_ROTATE` = R, hold).** Only when carrying **exactly one box** (no stacks) and not already looking around: `InteractionStateManager.HandleRotateInput` sets `AimStateManager.LookSuppressed = true` so `NormalAimstate` freezes the look axes (camera holds still), and feeds the mouse delta to `GenericBoxBehaviour.ApplyManualRotation(mouseX, mouseY)`. That spins the box around world-up (mouse X) and tips it around the camera's right axis (mouse Y), **baking the result into `carryYawOffsetRot`** — so the new orientation is held and still yaw-follows the camera after release, and because top/bottom come from the live AABB, the box's new top automatically becomes the stackable face. Releasing R (or dropping/throwing, or starting a peek/free-look) clears `LookSuppressed` and hands the mouse back to the camera. Tuning: `rotateSensitivity` on `GenericBoxBehaviour`. (Per-frame modal like drop/throw, not a separate `BaseState`.)
- **Raise/lower a held box or stack (`KEY_VERTICAL` = V, hold).** While carrying (one box *or* a stack) and not rotating/looking around, `InteractionStateManager.HandleVerticalInput` sets `AimStateManager.LookSuppressed = true` (camera holds still, like rotate), sums the **combined `Weight` of the whole carried column**, and feeds mouse-Y + that total to `GenericBoxBehaviour.ApplyVerticalAdjust`. The carrier accumulates a world-vertical `verticalCarryOffset` added to the hold point each `FixedUpdate` (reset to 0 on pickup); since riders are parented to it the **whole stack rises/lowers together**. A heavier column adjusts **slower** (`raiseLowerWeightInfluence` relative to `referenceWeight`), and past `raiseLowerMaxWeight` it's **too heavy to handle** — the adjust is a no-op (you can still lift/carry it, just not nudge it up/down). Offset clamped to `±raiseLowerMaxOffset`, and the carry clearance clamp still prevents driving down through the surface. Per-frame modal, not a `BaseState`. Knobs: `raiseLowerSensitivity`, `raiseLowerMaxOffset`, `raiseLowerMaxWeight`, `raiseLowerWeightInfluence`.
- **Throw (`KEY_THROW` = T).** While carrying, T hurls the box (or whole stack) forward via `InteractionStateManager.Throw()` → `GenericBoxBehaviour.OnThrown` / `OnThrownAsRider`. `LaunchThrow` applies a constant forward+up impulse (`throwImpulse`, `throwUpFactor`); since `rb.mass == Weight`, a heavier box is thrown less far automatically (weight-based). Riders are un-kinematic'd, unparented, and freed of their carrier/player collision-ignores, so a thrown stack **scatters** into independent dynamic boxes. The throw is deliberately **unaligned** ("it's on you") with one exception: while a thrown box is armed, if its first real impact is **landing on another box's top face** (most-upward contact normal ≥ `topHitNormalThreshold`, and that target isn't itself mid-throw), `OnCollisionEnter` zeroes its velocity and `SnapOntoBox` for a neat stack; any other first impact (floor, wall, box side) just disarms it. A `throwArmDelay` ignores the launch-moment shove as a stack separates, and the "target not thrown" guard stops two tossed siblings snapping onto each other mid-air. `OnDropped` and the throw share a `ReleaseCarry()` teardown helper. Knobs: `throwImpulse`, `throwUpFactor`, `topHitNormalThreshold`, `throwArmDelay`.
- **Stage 2 status (done):** the held box is a dynamic, velocity-driven body, so it knocks lighter boxes aside and is checked by heavier ones. **Remaining trade-off:** riders ride kinematically, so a stack's mass does *not* add to the carrier's knock-over force — only the bottom (dynamic) box is mass-correct. The clean future upgrade for combined mass without joints: add the riders' `Weight`s to the carrier's `Rigidbody.mass` on pickup and restore on drop (capture/restore `rb.mass` like the other `prev*` fields). Tuning knobs live on `GenericBoxBehaviour`: `maxCarrySpeed`, `carrySmoothTime`, `carryClearance`.
- **Weight property (design source of truth).** Each box has an authored `weight` field (default `1`), exposed as `public float Weight`. `ApplyWeight()` mirrors it onto `Rigidbody.mass` in `Start` (runtime) and `OnValidate` (edit-time, so Mass updates live as you type) — so **set the box's weight via the `Weight` field, not the Rigidbody Mass**, which now just follows it. The mass-weighted carry physics already read `rb.mass`; *gameplay* systems (carry speed, stamina drain, lift thresholds, HUD, crush/stacking rules) should read `Weight` instead of `rb.mass`, because the carry system may temporarily mutate `rb.mass` (e.g. the rider-mass upgrade above) while `Weight` stays the true value.
- **Weight scales the carry feel.** On pickup, `ComputeWeightedCarry()` scales `maxCarrySpeed` (heavier → slower) and `carrySmoothTime` (heavier → floatier/laggier lift) by `weight` relative to `referenceWeight`. Influence is dialed by `speedWeightInfluence` (default 1 = speed inversely proportional to weight) and `smoothWeightInfluence` (default 0.5). At `weight == referenceWeight` the feel is identical to pre-weight behavior. Computed once per carry (weight is constant during a carry), so no per-frame cost; tiny/zero weights are clamped so nothing divides to infinity.

## Pause / Cursor

- `CursorManager` is a **static** utility (`Lock()` = lock+hide, `Unlock()` = free+show) — not a component.
- `PauseMenuManager` (attach to one gameplay-scene object, e.g. a `GameplayManager`): Esc toggles pause → snapshots the camera to a RenderTexture, blurs it (downsample + `FreezeFrameBlur` iterations), shows it on a full-screen `RawImage` behind a dark tint + placeholder "PAUSED" text, frees the cursor, sets `Time.timeScale = 0`. It locks the cursor on `Start` and re-asserts the lock each gameplay frame (needed because the editor force-frees a locked cursor on Esc — see Gotchas).
- The UI is built in code (no Canvas to wire up). No `EventSystem` yet (text-only placeholder); add one when buttons are introduced.

## Player Rig / Editor Gotchas

- The Player root was changed from scale `(1, 1.5, 1)` to uniform `(1,1,1)`, with `CharacterController`/`CapsuleCollider` height bumped `2 → 3` and the Virtual Camera local Y `0.6667 → 1.0` to preserve collision/eye height. This stops carried/childed objects inheriting a vertical stretch.
- **Editor cursor quirk:** in the Unity editor, pressing **Esc** force-frees a locked cursor and the lock only re-engages on the next click into the Game view — so after closing the pause menu the cursor stays visible until you click. This is editor-only; in a build `Cursor.lockState = Locked` re-locks immediately on resume. The pause key is intentionally kept as Esc despite this.
- Script edits made while in Play mode don't apply until you stop, let it recompile, and re-enter Play.

## Camera / First-Person View Tuning

The view is a Cinemachine Virtual Camera with a **ThirdPersonFollow** body that Follows/LookAts `PlayerForward` (see Architecture Patterns). It's tuned to read as first-person. Adjust these on the assets, not in code.

**Current values (SampleScene):**
- Virtual Camera → Lens → **Field of View = 60** (vertical). *This is the value that controls in-game FOV* — the CinemachineBrain copies the live vcam lens onto Main Camera every frame, so changing Main Camera's own FOV does nothing in Play. Main Camera FOV is also set to 60 just to match the Scene/edit view.
- `cm` child → ThirdPersonFollow → **Damping = (0,0,0)** (crisp, no lag — right for first-person).
- ThirdPersonFollow → **ShoulderOffset = (0, 1, -0.5)**: `y=1` sets eye height (≈ world y3, head height given Player root y2 + capsule height 3); `z=-0.5` is a slight rear pullback.
- Near clip plane 0.1, far 5000.

**Knobs / suggestions for later tweaks:**
- **FOV feel:** ~50 = cozy/retro, **60 = standard FPS** (≈90° horizontal at 16:9), 70 = open/modern. Always change it on the *Virtual Camera* lens, never Main Camera.
- **Pure dead-on first-person:** set `ShoulderOffset.z` from `-0.5` → `0` to remove the rear pullback so the camera sits exactly on the eye point.
- **Eye height:** raise/lower `ShoulderOffset.y`.
- **Re-add a touch of smoothing:** if `Damping = 0` feels too stiff, bump `Damping.y` to ~0.05.
- Editing these in the `.unity`/vcam YAML on disk works, but close/reopen the scene in Unity afterward — a loaded scene will overwrite on-disk edits when saved.

## Key Bindings (KeyBindings.cs)

| Action | Key |
|---|---|
| Move | WASD |
| Sprint | Left Shift |
| Interact / Pick up | F |
| Drop | G |
| Throw | T |
| Rotate held box (hold + move mouse) | R |
| Raise/lower held box or stack (hold + move mouse) | V |
| Free Look | Middle Mouse (Mouse2) |
| Peek Left | Q |
| Peek Right | E |
| Pause menu / Cursor unlock | Esc |

## Game Design Summary (from GDD.md)

- **Win condition**: Get a promotion after enough days of successful shifts.
- **Lose conditions**: Starve (2 days without food), or get sick 5 times in a row.
- **Shift loop**: Clock in → pick boxes from truck → stack in designated zones → shift ends → receive pay → manage survival costs (electricity, water, heat, food) → buy upgrades → next day.
- **Difficulty scaling**: Each day adds new warehouse zones, new box types (heavy, fragile, temperature-sensitive), random events (power outage, truck breakdown, goose honk easter egg), and tighter constraints.
- **Art style**: Low-poly models with pixelated textures, 1st-person 3D, set in 1990.
- **Engine**: Unity (latest free version).

## NPCs

- **Stan Cobb** — Foreman/tutorial NPC
- **Kate** — Part-time cleaner/supply manager; stocks cafeteria, cleans gear
- **Jack Stone** — Night shift guard; fixes broken stuff, keeps drivers in line
- **Larry / Terry** — Truck drivers

## Development Status (from progress.md)

All state managers are listed as `[TODO]` in the progress tracker, but `MovementStateManager`, `AimStateManager`, and `InteractionStateManager` all have working implementations in code. The progress doc is behind the actual code.

## What's Not Yet Implemented

- Player Status state manager (stamina drain, hunger, sickness debuffs)
- Shift timer and end-of-shift scoring
- Money / economy system
- Apartment / end-of-day screen
- NPC dialogue and behavior
- Warehouse layout / stacking zones
- Box types with varying weights and constraints
- Upgrade/equipment system
- Random events (power outage, truck breakdown, etc.)
- Art, audio (largely placeholder/TBD)
- UI: pause menu exists (placeholder text only — no working buttons/EventSystem yet); HUD, menus, end-of-day screen still TBD

## Implemented this iteration (not in progress.md)

- Camera-driven box carry, look-up/down vertical control, orientation-preserving hold (keeps the box's tilt/flip, yaw-follows the view via `carryYawOffsetRot`; no auto-upright)
- Smoothed carry (`SmoothDamp`, `carrySmoothTime`) — box eases/lifts/lowers with weight
- Clearance clamp (`TryGetSurfaceBelow`, casts from above the box) — box can't sink into what's below; pull-out-of-stack feel; fixed the pickup-shove bug
- Orientation-independent top/bottom via collider world bounds (`TopCenter()`/`BottomCenter()`) — flipped boxes stack correctly; old `BoxTop`/`BoxBottom` anchors retired
- Peek/free-look moves the carried box out of view (ease velocity reset on resume)
- Stacked box pickup (whole column) with rigid riders kept in place (orientation/arrangement preserved, no re-snap); pick from bottom/middle. Fixes the side-stacked box getting stranded when the carrier used to auto-upright
- Stage 2 dynamic carry — held box is a velocity-driven Rigidbody (mass-weighted; knocks lighter boxes); riders kinematic+parented with carrier-collision ignored; wakes the body so it doesn't stick to the floor
- Box `Weight` property (authored design source of truth, default 1) auto-synced onto `Rigidbody.mass` via `ApplyWeight()` (Start + OnValidate); gameplay reads `Weight`, physics reads the mirrored mass
- Weight scales the carry feel (`ComputeWeightedCarry()`): heavier → slower `maxCarrySpeed` + floatier `carrySmoothTime`, relative to `referenceWeight`, dialed by `speedWeightInfluence`/`smoothWeightInfluence`
- Throw (T) — hurls the carried box/stack forward with a weight-scaled impulse (`OnThrown`/`OnThrownAsRider`/`LaunchThrow`); unaligned except a thrown box landing on another box's top face snaps into a neat stack (`OnCollisionEnter`); stacks scatter; shares `ReleaseCarry()` with drop
- Rotate held box (hold R, single box only) — `LookSuppressed` freezes the camera and the mouse tumbles the box (`ApplyManualRotation`, baked into `carryYawOffsetRot`); new top becomes stackable via the live-AABB top/bottom
- Raise/lower held box or stack (hold V) — `LookSuppressed` freezes the camera and mouse-Y nudges a world-vertical `verticalCarryOffset` on the carrier (`ApplyVerticalAdjust`); works for whole stacks (riders parented), speed scales with the column's combined `Weight`, and a stack past `raiseLowerMaxWeight` is too heavy to adjust (still carryable)
- Drop (G) just lets go — box falls straight down under gravity from where it's held (no snap/impulse), keeping orientation; boxes remain dynamic
- Placement preview marker under the carried box
- Pause menu with freeze-frame blur background; static `CursorManager` lock/hide
- Player rig scale fix (uniform root); Drop (G) key; cursor lock (Esc)
- Camera/first-person view tuning pass (vcam FOV 40→60, ThirdPersonFollow damping 0); see "Camera / First-Person View Tuning"

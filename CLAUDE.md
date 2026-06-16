# Boxing Day — Project Context for Claude

## What This Is

**Boxing Day** is a first-person simulation/puzzle game built in Unity (C#). The player works as a warehouse worker at *Cartwright Trading Co.* in the fictional town of Harbor Glen, Bellmont, set in 1990. The core loop is: stack boxes correctly before your shift ends → earn money → pay survival bills → upgrade gear → repeat until promoted (win condition).

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
│   └── KeyBindings.cs              # Static key constants (WASD, Sprint, FreeLook, Peek L/R, Interact=F, Drop=G, CursorUnlock=Esc)
└── StateManagement/
    ├── BaseState.cs                # Abstract: Enter/Exit/Update(BaseStateManager)
    ├── BaseStateManager.cs         # Abstract MonoBehaviour: currentState, previousState, ChangeState()
    ├── MasterStateManager.cs       # Holds refs to all sub-managers + PlayerData
    ├── Interaction/
    │   ├── InteractionStateManager.cs  # Raycast, UI prompt, PickUp (whole stack) / Drop, IsCarrying, HandleDropInput, holdPoint
    │   ├── IdleInteractionState.cs     # Waiting; also calls HandleDropInput; transitions to Focused on raycast hit
    │   └── FocusedInteractionState.cs  # Focused on an interactable; calls HandleDropInput; fires Interact() on KEY_INTERACT
    ├── Look/
    │   ├── AimStateManager.cs      # Cinemachine xAxis/yAxis, peek (Q/E), Normal↔FreeLook, IsLookingAround/IsPeeking/IsFreeLooking
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

The earlier "anchor each box to the previous box's `BoxTop`, parented to PlayerForward" approach was replaced. Current behavior, all in `GenericBoxBehaviour` + `InteractionStateManager`:

- **Carry is camera-driven, smoothed, and physical (Stage 2).** On pickup the carried (bottom) box is unparented and kept a **real dynamic Rigidbody** (gravity off, `FreezeRotationX|Z`, interpolation on, uprighted), then driven in **`FixedUpdate`** by setting `rb.velocity` toward a hold point in front of `Camera.main` (`forward * holdDistance - up * holdDrop`). The *target* is eased (`Vector3.SmoothDamp` into `easedTargetBottom`, `carrySmoothTime`) for weight; the body chases it at up to `maxCarrySpeed`. Look up → box rises; look down → it lowers. Because the box is dynamic, **collisions are mass-weighted** — ram it into a light box and it knocks aside, a heavy one resists. Driving from the real camera (not the rig) is deliberate — robust to the Cinemachine setup, avoids the Player root's scale.
  - **Sleep gotcha:** a box resting on the floor is asleep, and a sleeping Rigidbody ignores `rb.velocity`. On pickup we `WakeUp()` and set `sleepThreshold = 0` (restored on drop), or the box just sticks to the ground.
- **Clearance clamp (no more pickup shove).** Each frame the carried box's underside is clamped so it can never sink below the surface currently beneath it (`TryGetSurfaceBelow` + `carryClearance`). Lowering is smoothed but the upward clamp is instant, so the box can't penetrate whatever is under it. This is what lets you pull a box out of a stack — it floats at the top of the box below until it's pulled clear, then lowers to the hold point — without shoving the lower box. **Key detail:** `TryGetSurfaceBelow` casts *down from above the box's top*, because a ray started at the flush underside begins *inside* the box below and Unity skips it (the ray falls through to the floor — that was the original shove bug).
- **Orientation-independent top/bottom.** `TopCenter()` / `BottomCenter()` are derived from the collider's live world `bounds` (`max.y` / `min.y`), not from fixed child anchors. So "top" is always the real upward face even when a box is flipped on its side — any face is stackable and the topmost is always chosen. (The old `BoxTop`/`BoxBottom` child transforms are no longer referenced; AABB is exact for 90° flips, slightly over-estimates for diagonal tilts.)
- **Peek / free-look clears the view.** While `AimStateManager.IsLookingAround` (peek Q/E or free-look Mouse2), the box freezes relative to the player body (snapshot pose on entry) instead of tracking the camera, so the camera can look around it. The ease velocity is reset on resume so it doesn't lurch.
- **Player collision ignored while carried.** `Physics.IgnoreCollision` between the held box and the player's colliders, restored on drop — stops the held box shoving the CharacterController (e.g. when held low looking down). `TryGetSurfaceBelow` also skips the player.
- **Stacked pickup.** `GetStackAbove()` overlaps a thin slab above the box's top face (recursively) to find the column. Picking the bottom box carries the whole stack; "riders" are snapped neatly centered on the box below (so the column is always tidy, and flipped riders get uprighted), then made **kinematic and parented** to the carrier (`OnPickedUpAsRider`) so they follow rigidly. Picking a middle box takes it + everything above.
  - **Critical:** each rider's collision with the carrier is **ignored** (`Physics.IgnoreCollision`, restored on drop). A kinematic rider has infinite mass, so a rider sitting on top would act as an immovable lid and stop the dynamic carrier from rising — the whole stack would refuse to lift. (Riders stay kinematic+parented rather than `FixedJoint`-welded because force-setting the carrier's velocity while it's in a stiff joint made the solver explode and fling the stack across the level.)
- **Drop = raycast down from the box's bottom.** If it lands on another box it `SnapOntoBox` (centered onto that box's `TopCenter()`, matching yaw); otherwise it rests on the surface. Then released to **normal physics** (`isKinematic = false`) — boxes stay dynamic (a deliberate user choice; the bolted-down/auto-kinematic experiment was reverted). `TryGetDropPlacement` is the single predictor shared by the real drop and the preview.
- **Placement preview.** While carrying, a flat marker (auto-created translucent quad, or an optional prefab) shows where the box will land, using `TryGetDropPlacement`, so it matches snapping exactly.
- `KEY_DROP` (G) drops; `IsCarrying` guards against picking up more while holding.
- **Stage 2 status (done):** the held box is a dynamic, velocity-driven body, so it knocks lighter boxes aside and is checked by heavier ones. **Remaining trade-off:** riders ride kinematically, so a stack's mass does *not* add to the carrier's knock-over force — only the bottom (dynamic) box is mass-correct. The clean future upgrade for combined mass without joints: add the riders' masses to the carrier's `Rigidbody.mass` on pickup and restore on drop. Tuning knobs live on `GenericBoxBehaviour`: `maxCarrySpeed`, `carrySmoothTime`, `carryClearance`.

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

- Camera-driven box carry, look-up/down vertical control, upright hold
- Smoothed carry (`SmoothDamp`, `carrySmoothTime`) — box eases/lifts/lowers with weight
- Clearance clamp (`TryGetSurfaceBelow`, casts from above the box) — box can't sink into what's below; pull-out-of-stack feel; fixed the pickup-shove bug
- Orientation-independent top/bottom via collider world bounds (`TopCenter()`/`BottomCenter()`) — flipped boxes stack correctly; old `BoxTop`/`BoxBottom` anchors retired
- Peek/free-look moves the carried box out of view (ease velocity reset on resume)
- Stacked box pickup (whole column) with rigid riders; pick from bottom/middle
- Stage 2 dynamic carry — held box is a velocity-driven Rigidbody (mass-weighted; knocks lighter boxes); riders kinematic+parented with carrier-collision ignored; wakes the body so it doesn't stick to the floor
- Snap-on-drop neat stacking (centered, aligned), boxes remain dynamic
- Placement preview marker under the carried box
- Pause menu with freeze-frame blur background; static `CursorManager` lock/hide
- Player rig scale fix (uniform root); Drop (G) key; cursor lock (Esc)
- Camera/first-person view tuning pass (vcam FOV 40→60, ThirdPersonFollow damping 0); see "Camera / First-Person View Tuning"

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

- **Carry is camera-driven.** On pickup the carried (bottom) box is unparented, made kinematic, and each `LateUpdate` repositioned in front of `Camera.main` (`forward * holdDistance - up * holdDrop`), kept upright (yaw only). Look up → box rises in world; look down → it lowers. Driving from the real camera (not the rig) is deliberate — it's robust to the Cinemachine setup and avoids inheriting the Player root's scale.
- **Peek / free-look clears the view.** While `AimStateManager.IsLookingAround` (peek Q/E or free-look Mouse2), the box freezes relative to the player body (snapshot pose on entry) instead of tracking the camera, so the camera can look around it.
- **Player collision ignored while carried.** `Physics.IgnoreCollision` between the held box and the player's colliders, restored on drop — stops the held box shoving the CharacterController (e.g. when held low looking down).
- **Stacked pickup.** `GetStackAbove()` overlaps a thin slab above the box's top face (recursively) to find the column. Picking the bottom box carries the whole stack; "riders" are parented rigidly to the bottom box (`OnPickedUpAsRider`), so they inherit all motion and can't slip. Picking a middle box takes it + everything above.
- **Drop = raycast down from the box's bottom.** If it lands on another box it `SnapOntoBox` (centered + same facing on the top); otherwise it rests on the surface. Then released to **normal physics** (`isKinematic = false`) — boxes stay dynamic (a deliberate user choice; the bolted-down/auto-kinematic experiment was reverted). `TryGetDropPlacement` is the single predictor shared by the real drop and the preview.
- **Placement preview.** While carrying, a flat marker (auto-created translucent quad, or an optional prefab) shows where the box will land, using `TryGetDropPlacement`, so it matches snapping exactly.
- `KEY_DROP` (G) drops; `IsCarrying` guards against picking up more while holding.

## Pause / Cursor

- `CursorManager` is a **static** utility (`Lock()` = lock+hide, `Unlock()` = free+show) — not a component.
- `PauseMenuManager` (attach to one gameplay-scene object, e.g. a `GameplayManager`): Esc toggles pause → snapshots the camera to a RenderTexture, blurs it (downsample + `FreezeFrameBlur` iterations), shows it on a full-screen `RawImage` behind a dark tint + placeholder "PAUSED" text, frees the cursor, sets `Time.timeScale = 0`. It locks the cursor on `Start` and re-asserts the lock each gameplay frame (needed because the editor force-frees a locked cursor on Esc — see Gotchas).
- The UI is built in code (no Canvas to wire up). No `EventSystem` yet (text-only placeholder); add one when buttons are introduced.

## Player Rig / Editor Gotchas

- The Player root was changed from scale `(1, 1.5, 1)` to uniform `(1,1,1)`, with `CharacterController`/`CapsuleCollider` height bumped `2 → 3` and the Virtual Camera local Y `0.6667 → 1.0` to preserve collision/eye height. This stops carried/childed objects inheriting a vertical stretch.
- **Editor cursor quirk:** in the Unity editor, pressing **Esc** force-frees a locked cursor and the lock only re-engages on the next click into the Game view — so after closing the pause menu the cursor stays visible until you click. This is editor-only; in a build `Cursor.lockState = Locked` re-locks immediately on resume. The pause key is intentionally kept as Esc despite this.
- Script edits made while in Play mode don't apply until you stop, let it recompile, and re-enter Play.

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
- Peek/free-look moves the carried box out of view
- Stacked box pickup (whole column) with rigid riders; pick from bottom/middle
- Snap-on-drop neat stacking (centered, aligned), boxes remain dynamic
- Placement preview marker under the carried box
- Pause menu with freeze-frame blur background; static `CursorManager` lock/hide
- Player rig scale fix (uniform root); Drop (G) key; cursor lock (Esc)

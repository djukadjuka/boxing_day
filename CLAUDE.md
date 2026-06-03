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
├── Behaviours/
│   ├── GenericBoxBehaviour.cs      # Interactable box; pick-up/drop, BoxTop/BoxBottom anchors
│   ├── GenericObjectBehaviour.cs   # Stub for generic interactables (not yet implemented)
│   └── PlayerPointerBehaviour.cs   # Legacy raycast+UI script (superseded by InteractionStateManager)
├── DataHolders/
│   └── PlayerData.cs               # Serialized player stats: runningSpeed, sprintingSpeed, MaxStamina, CurrentStamina
├── Interactables/
│   ├── IInteractable.cs            # Interface: Interact(), GetPrompt(), OnFocusEnter(), OnFocusExit()
│   └── InteractableBase.cs         # Abstract MonoBehaviour implementing IInteractable; holds cursorPromptText
├── Options/
│   └── KeyBindings.cs              # Static key constants (WASD, Sprint, FreeLook, Peek L/R, Interact=F)
└── StateManagement/
    ├── BaseState.cs                # Abstract: Enter/Exit/Update(BaseStateManager)
    ├── BaseStateManager.cs         # Abstract MonoBehaviour: currentState, previousState, ChangeState()
    ├── MasterStateManager.cs       # Holds refs to all sub-managers + PlayerData
    ├── Interaction/
    │   ├── InteractionStateManager.cs  # Raycast, UI prompt, PickUp/Drop stack logic, holdPoint
    │   ├── IdleInteractionState.cs     # Waiting; transitions to Focused when raycast hits IInteractable
    │   └── FocusedInteractionState.cs  # Focused on an interactable; fires Interact() on KEY_INTERACT
    ├── Look/
    │   ├── AimStateManager.cs      # Cinemachine xAxis/yAxis, peek (Q/E), transitions Normal↔FreeLook
    │   ├── NormalAimstate.cs       # Standard mouse-look; enters FreeLook on Mouse2 hold
    │   └── FreeLookAimState.cs     # Body locked, camera free; exits on Mouse2 release, restores axes
    └── Movement/
        ├── MovementStateManager.cs # CharacterController, gravity, IsGrounded sphere check, IsMoving()
        ├── IdleMovementState.cs    # No input → Idle; transitions to Running or Sprinting
        ├── RunningState.cs         # Sets speed = runningSpeed from PlayerData
        └── SprintingState.cs       # Sets speed = sprintingSpeed from PlayerData
```

## Architecture Patterns

- **State Machine pattern** throughout: each system (Movement, Look, Interaction) has a `BaseStateManager` subclass and a set of `BaseState` subclasses. `ChangeState()` calls `Exit` on the old state and `Enter` on the new one.
- **MasterStateManager** is the top-level hub — all sub-managers grab a reference to it via `GetComponent<MasterStateManager>()` in their `Start()`. Sub-managers access shared data (e.g. `PlayerData`) through it.
- **IInteractable / InteractableBase** form the interaction contract. Any world object that can be interacted with implements `IInteractable` (via `InteractableBase`).
- **Box stacking** is handled as a linked list of `GenericBoxBehaviour` objects in `InteractionStateManager.carriedBoxes`. Each box anchors to the previous box's `BoxTop` transform. Dropping releases all boxes in reverse order.
- **Cinemachine** is used for the camera (`AimStateManager` drives `xAxis`/`yAxis`).
- `PlayerPointerBehaviour` is a legacy script — the interaction logic has been moved into `InteractionStateManager`. Treat it as dead code unless told otherwise.

## Key Bindings (KeyBindings.cs)

| Action | Key |
|---|---|
| Move | WASD |
| Sprint | Left Shift |
| Interact | F |
| Free Look | Middle Mouse (Mouse2) |
| Peek Left | Q |
| Peek Right | E |

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
- Art, audio, UI (largely placeholder/TBD)

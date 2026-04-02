# CNC Workflow System for Unity VR Carpentry Workshop

A complete CNC workflow implementation for a Unity VR carpentry simulation. This system provides end-to-end automation: **Wood Blank Spawning → Conveyor Transfer → CNC Operation → Mesh Deformation → Task Progression + Safety + Scoring**.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Dependencies & Setup](#dependencies--setup)
4. [Script Reference](#script-reference)
   - [Data Scripts](#data-scripts)
   - [GameState Scripts](#gamestate-scripts)
   - [Machine Scripts](#machine-scripts)
   - [UI Scripts](#ui-scripts)
   - [Utility Scripts](#utility-scripts)
5. [Event System Reference](#event-system-reference)
6. [Quick Start Guide](#quick-start-guide)
7. [Creating Content](#creating-content)
8. [Testing Checklist](#testing-checklist)

---

## Overview

### Design Decisions

| Feature | Implementation |
|---------|----------------|
| **Mesh Cutting** | Real-time vertex deformation + EzySlice for boolean operations |
| **Task Progression** | Fully guided mode (machines locked until required step) |
| **Safety System** | Basic (score penalties + audio warnings only, no machine slowdowns) |
| **Path Creation** | Inspector-based (no visual editor tool) |

### What This System Does

- Spawns wood blanks with configurable properties
- Moves workpieces via conveyor belts to machines
- Operates CNC in Manual (joystick) or Auto (path-following) modes
- Deforms meshes in real-time to show cutting results
- Guides players through tasks with step-by-step instructions
- Tracks score, errors, and applies safety penalties
- Provides audio/visual feedback throughout

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      GameStateEvents (Static Event Bus)         │
│   All systems communicate through this central event system     │
└─────────────────────────────────────────────────────────────────┘
        │                    │                    │
        ▼                    ▼                    ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│  TaskManager  │   │ ScoreManager  │   │FeedbackManager│
│  (Singleton)  │   │  (Singleton)  │   │  (Singleton)  │
└───────────────┘   └───────────────┘   └───────────────┘
        │
        ▼
┌───────────────────────────────────────────────────────────────┐
│                        Machine Layer                           │
│  ObjectSpawner → ConveyorBelt → CNCMachineExtended            │
│                                  ├── CNCCutterExtended        │
│                                  └── CNCResultGenerator       │
└───────────────────────────────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────────────────────────────┐
│                         UI Layer                               │
│  TaskDisplayPanel    CNCControlPanelExtended                  │
└───────────────────────────────────────────────────────────────┘
```

---

## Folder Structure

```
Assets/Scripts/
├── Data/                    # ScriptableObject definitions
│   ├── WorkpieceData.cs     # Wood blank properties
│   ├── PathData.cs          # CNC cutting paths
│   └── WorkAreaBounds.cs    # CNC work area limits (renamed from CuttingPath)
├── GameState/               # Core game systems
│   ├── GameStateEvents.cs   # Static event bus
│   ├── ScoreManager.cs      # Score tracking singleton
│   ├── FeedbackManager.cs   # Audio feedback singleton
│   ├── SafetyEvent.cs       # Safety violation data
│   ├── Task.cs              # Task definition + progress
│   ├── TaskStep.cs          # Individual task steps
│   ├── TaskManager.cs       # Task orchestrator singleton
│   └── ConsequenceSystem.cs # Safety penalty system
├── Machines/                # Machine components
│   ├── CNCMachine.cs        # Base CNC controller
│   ├── CNCMachineExtended.cs # Extended with path following
│   ├── CNCCutter.cs         # Base cutter movement
│   ├── CNCCutterExtended.cs # Extended with auto mode
│   ├── CNCResultGenerator.cs # Mesh deformation
│   ├── JoystickController.cs # Joystick input
│   ├── Workpiece.cs         # Runtime workpiece data
│   ├── ObjectSpawner.cs     # Workpiece spawning
│   ├── TransferPoint.cs     # Destination detection
│   └── ConveyorBelt.cs      # Object movement
├── UI/                      # User interface
│   ├── CNCControlPanel.cs   # Base control panel
│   ├── CNCControlPanelExtended.cs # Extended with path selection
│   ├── CNCScreenDisplay.cs  # CNC preview screen
│   ├── ControlPanel.cs      # Generic panel base
│   └── TaskDisplayPanel.cs  # Task progress HUD
└── Utilities/               # Helper classes
    ├── JoinMeshes.cs        # Mesh combining
    └── MeshCutter.cs        # Mesh cutting operations
```

---

## Dependencies & Setup

### Required Unity Version
- Unity **2021.3 LTS** or newer (tested on 2022.3)

### Required Packages

#### 1. TextMeshPro (Required for UI)

TextMeshPro is usually included by default. If not:

1. Open **Window → Package Manager**
2. Click **+** → **Add package by name**
3. Enter: `com.unity.textmeshpro`
4. Click **Add**
5. When prompted, click **Import TMP Essentials**

#### 2. EzySlice (Required for Mesh Cutting)

EzySlice is a free, MIT-licensed mesh slicing library.

**Option A: Via Git URL (Recommended)**
1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL**
3. Enter: `https://github.com/DavidArayan/ezy-slice.git`
4. Click **Add**

**Option B: Manual Import**
1. Download from: https://github.com/DavidArayan/ezy-slice
2. Extract and copy the `EzySlice` folder into your `Assets/Plugins/` directory

**Note:** The `MeshCutter.cs` script includes placeholder code that works without EzySlice for basic testing. For actual mesh boolean operations, you'll need to uncomment the EzySlice integration code and add `using EzySlice;` at the top.

---

## Script Reference

### Data Scripts
**Location:** `Assets/Scripts/Data/`

These are **ScriptableObject** definitions. Create instances via **Assets → Create → CarpentryWorkshopVR**.

---

#### `WorkpieceData.cs`
**Purpose:** Defines properties of a wood blank (dimensions, material, physics).

**Type:** ScriptableObject

**Create via:** Assets → Create → CarpentryWorkshopVR → Workpiece Data

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `workpieceName` | Display name (e.g., "Pine Board 50x50cm") |
| `workpieceId` | Unique ID for code references |
| `dimensions` | Size in meters (Vector3: width, height, depth) |
| `density` | Wood density in kg/m³ (affects mass calculation) |
| `prefab` | The GameObject prefab to spawn |
| `surfaceMaterial` | Material for the wood surface |
| `crossSectionMaterial` | Material for cut/exposed surfaces |
| `isCuttable` | Can this be cut by CNC? |
| `maxCuts` | Maximum cuts before workpiece is waste |

**Usage Notes:**
- Create different WorkpieceData assets for each wood type (pine, oak, plywood, etc.)
- The `prefab` should have MeshFilter, MeshRenderer, and Collider components
- `crossSectionMaterial` is applied to newly exposed surfaces after cutting

---

#### `PathData.cs`
**Purpose:** Defines a CNC cutting path (waypoints, speed, depth, tool settings).

**Type:** ScriptableObject

**Create via:** Assets → Create → CarpentryWorkshopVR → Path Data

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `pathName` | Display name (e.g., "Coaster Circle") |
| `pathType` | Line, Rectangle, Circle, or Custom |
| `waypoints` | List of Vector3 points defining the path |
| `isClosedLoop` | Does the path connect end to start? |
| `feedRate` | Cutting speed in m/s (0.01 - 0.5) |
| `plungeDepth` | Cut depth per pass in meters |
| `passes` | Number of passes (for deeper cuts) |
| `toolDiameter` | Diameter of cutting tool in meters |

**Built-in Shape Generation:**
- Set `pathType` to Rectangle or Circle
- Set `shapeWidth`, `shapeHeight`, `shapeCenter`
- Right-click the asset → **Regenerate Shape**

**Usage Notes:**
- Waypoints are in **local CNC space** (XZ plane, Y typically 0)
- `TotalDepth` = `plungeDepth × passes`
- Use `previewColor` to differentiate paths in the editor

---

#### `WorkAreaBounds.cs`
**Purpose:** Defines the CNC machine's work area boundaries (clamping limits).

**Type:** ScriptableObject

**Create via:** Assets → Create → CarpentryWorkshopVR → Work Area Bounds

> **Note:** This was previously named `CuttingPath` but renamed to avoid confusion with `PathData`.

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_workAreaMin` | Minimum X and Z coordinates the cutter can reach |
| `_workAreaMax` | Maximum X and Z coordinates the cutter can reach |
| `_maxCutDepth` | Maximum cutting depth on Y axis |
| `_idleHeight` | Default Y position when not plunging |

---

### GameState Scripts
**Location:** `Assets/Scripts/GameState/`

Core systems that manage game state, events, and progression.

---

#### `GameStateEvents.cs`
**Purpose:** Static event bus for cross-system communication. ALL systems communicate through here.

**Type:** Static class (not a MonoBehaviour)

**Integration:** No setup required. Just subscribe to events.

**Event Categories:**

| Category | Events |
|----------|--------|
| Task | `OnTaskStarted`, `OnTaskCompleted`, `OnStepStarted`, `OnStepCompleted` |
| CNC | `OnCNCStateChanged`, `OnPathLoaded`, `OnCutProgress` |
| Workpiece | `OnWorkpieceSpawned`, `OnWorkpieceTransferred`, `OnWorkpieceCut`, `OnWorkpieceDespawned` |
| Safety | `OnSafetyViolation` |
| Score | `OnScoreChanged`, `OnErrorRecorded` |
| Conveyor | `OnConveyorStateChanged` |

**Subscription Pattern:**
```csharp
void OnEnable() {
    GameStateEvents.OnWorkpieceSpawned += HandleWorkpieceSpawned;
}

void OnDisable() {
    GameStateEvents.OnWorkpieceSpawned -= HandleWorkpieceSpawned;
}

void HandleWorkpieceSpawned(GameObject workpiece) {
    // React to event
}
```

**Important:** Call `GameStateEvents.ClearAllSubscribers()` on scene unload to prevent memory leaks.

---

#### `ScoreManager.cs`
**Purpose:** Singleton that tracks player score, errors, and performance metrics.

**Type:** MonoBehaviour (Singleton)

**Setup:**
1. Create empty GameObject named "ScoreManager"
2. Add `ScoreManager` component
3. Configure starting score and limits in Inspector

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_startingScore` | Score at game start |
| `_minimumScore` | Floor (can't go below this) |
| `_maximumScore` | Ceiling (-1 = unlimited) |

**Access via:** `ScoreManager.Instance`

**Key Methods:**
- `AddScore(int points, string reason)` - Award points
- `SubtractScore(int points, string reason)` - Deduct points
- `RecordError(string errorType)` - Log an error
- `GetSessionSummary()` - Get formatted stats string

---

#### `FeedbackManager.cs`
**Purpose:** Singleton that plays audio feedback (success, error, warning sounds).

**Type:** MonoBehaviour (Singleton)

**Setup:**
1. Create empty GameObject named "FeedbackManager"
2. Add `FeedbackManager` component
3. Add `AudioSource` component (or it creates one automatically)
4. Assign AudioClips in Inspector

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_successSound` | Played on correct actions |
| `_errorSound` | Played on mistakes |
| `_warningSound` | Played for safety violations |
| `_taskCompleteSound` | Played when task finishes |
| `_cncStartSound` | Played when CNC starts |
| `_cncCuttingLoop` | Looping sound during cutting |

**Access via:** `FeedbackManager.Instance`

**Key Methods:**
- `PlaySuccess()` / `PlayError()` / `PlayWarning()`
- `PlayTaskComplete()` / `PlayStepComplete()`
- `PlayCNCStartSound()` / `StartCNCCuttingLoop()` / `StopCNCCuttingLoop()`

---

#### `SafetyEvent.cs`
**Purpose:** Data class representing a safety violation (not a MonoBehaviour).

**Type:** Plain C# class (POCO)

**Usage:** Created when violations occur, passed through events.

**Key Properties:**
| Property | Description |
|----------|-------------|
| `safetyType` | Enum: SpeedTooHigh, PathDeviation, Emergency, etc. |
| `severity` | 1 (minor), 2 (moderate), 3 (severe) |
| `warningMessage` | Human-readable message |
| `position` | World position of violation |
| `DefaultPenalty` | Suggested score penalty |

**Factory Methods (convenience):**
```csharp
SafetyEvent.SpeedViolation(currentSpeed, maxSpeed, position);
SafetyEvent.PathDeviation(deviationAmount, position);
SafetyEvent.EmergencyStop(position);
SafetyEvent.NoWorkpiece(position);
```

---

#### `TaskStep.cs`
**Purpose:** Defines a single step within a task.

**Type:** ScriptableObject

**Create via:** Assets → Create → CarpentryWorkshopVR → Task Step

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `stepId` | Unique identifier |
| `stepName` | Display name (e.g., "Load the workpiece") |
| `instructions` | Detailed instructions for player |
| `hint` | Help text shown after delay |
| `completionTrigger` | What completes this step (see below) |
| `requiredMachine` | Machine needed for this step |
| `autoComplete` | Auto-advance when trigger fires? |
| `completionPoints` | Points awarded |
| `lockOtherMachines` | Lock unneeded machines? (guided mode) |

**Completion Triggers:**
- `Manual` - Completed via code/UI
- `WorkpieceSpawned` - When workpiece spawns
- `WorkpieceTransferred` - When workpiece reaches destination
- `CNCStateChange` - When CNC enters specific state
- `PathLoaded` - When path is loaded
- `CuttingComplete` - When CNC finishes
- `WorkpieceCut` - When cut is made

---

#### `Task.cs`
**Purpose:** Defines a complete task containing multiple TaskSteps.

**Type:** ScriptableObject

**Create via:** Assets → Create → CarpentryWorkshopVR → Task

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `taskId` | Unique identifier |
| `taskName` | Display name (e.g., "Create a Wooden Coaster") |
| `description` | Full description |
| `steps` | List of TaskStep assets (in order) |
| `requireStepOrder` | Must complete steps in order? |
| `completionPoints` | Bonus points for task completion |
| `difficulty` | 1-5 star rating |
| `prerequisites` | Tasks that must be done first |

**Also Includes:** `TaskProgress` class for runtime tracking (created automatically).

---

#### `TaskManager.cs`
**Purpose:** Singleton orchestrator for task progression. Controls which machines are available, validates step completion, awards points.

**Type:** MonoBehaviour (Singleton)

**Setup:**
1. Create empty GameObject named "TaskManager"
2. Add `TaskManager` component
3. Add Task assets to `_availableTasks` list
4. Optionally set `_autoStartTask` for testing

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_availableTasks` | List of Task assets players can choose |
| `_autoStartTask` | Task to start automatically on scene load |
| `_guidedMode` | Enable machine locking based on current step |
| `_autoAdvance` | Auto-complete steps when triggers fire |

**Access via:** `TaskManager.Instance`

**Key Properties:**
- `CurrentTask` / `CurrentStep` / `CurrentStepIndex`
- `HasActiveTask` - Is a task in progress?
- `IsGuidedMode` - Is guided mode enabled?

**Key Methods:**
- `StartTask(Task task)` - Begin a task
- `CompleteCurrentStep()` - Manually complete step
- `IsMachineLocked(MachineType)` - Check if machine is locked
- `TryUseMachine(MachineType)` - Attempt to use machine (plays error if locked)

---

#### `ConsequenceSystem.cs`
**Purpose:** Listens to safety violations and applies penalties (score deduction + audio warnings).

**Type:** MonoBehaviour

**Setup:**
1. Create empty GameObject named "ConsequenceSystem"
2. Add `ConsequenceSystem` component
3. Configure penalty amounts and cooldowns
4. Optionally assign warning AudioClips

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_penaltyMultiplier` | Global penalty scaling (0.5 - 3.0) |
| `_samePenaltyCooldown` | Seconds before same violation can trigger again |
| `_globalPenaltyCooldown` | Minimum seconds between any penalties |
| `_enableAudioWarnings` | Play warning sounds? |
| `_enableVisualWarnings` | Flash screen on violations? |
| `_flashImage` | UI Image for screen flash effect |

**Automatic Behavior:** Subscribes to `GameStateEvents.OnSafetyViolation` automatically.

---

### Machine Scripts
**Location:** `Assets/Scripts/Machines/`

Components for physical machines in the workshop.

---

#### `Workpiece.cs`
**Purpose:** Runtime component attached to workpiece GameObjects. Tracks cut count, thickness, processing state.

**Type:** MonoBehaviour (added automatically by ObjectSpawner)

**Setup:** No manual setup needed. Automatically added when workpieces spawn.

**Key Properties:**
- `Data` - Reference to WorkpieceData asset
- `CutCount` / `IsCut` / `CanBeCut`
- `CurrentThickness` - Remaining material
- `IsBeingProcessed` - Currently in a machine?

**Key Methods:**
- `Initialize(WorkpieceData)` - Called by spawner
- `RecordCut(float depth)` - Log a cut operation
- `SetProcessing(bool)` - Mark as being processed
- `UpdateMesh(Mesh)` - Apply deformed mesh
- `Freeze()` / `Unfreeze()` - Control physics

---

#### `ObjectSpawner.cs`
**Purpose:** Spawns workpieces with object pooling support.

**Type:** MonoBehaviour

**Setup:**
1. Create empty GameObject where workpieces should spawn
2. Add `ObjectSpawner` component
3. Assign `_workpieceData` (what to spawn)
4. Set `_spawnPoint` (where to spawn)

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_workpieceData` | WorkpieceData asset to spawn |
| `_spawnPoint` | Transform for spawn position |
| `_autoSpawn` | Spawn automatically on start? |
| `_spawnOnEvent` | Spawn when triggered by event? |
| `_useObjectPool` | Enable pooling for performance |
| `_poolSize` | Number of pre-instantiated objects |

**Key Methods:**
- `SpawnWorkpiece()` - Spawn one workpiece
- `DespawnWorkpiece(GameObject)` - Return to pool or destroy

---

#### `TransferPoint.cs`
**Purpose:** Detects when workpieces arrive at a destination (e.g., CNC loading zone).

**Type:** MonoBehaviour

**Setup:**
1. Create empty GameObject at destination
2. Add `TransferPoint` component
3. Add Collider (Box/Sphere) and set as **Trigger**
4. Set layer/tag filtering if needed

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_pointId` | Unique identifier |
| `_pointName` | Display name |
| `_acceptedTags` | Only detect objects with these tags |
| `_stopOnArrival` | Freeze workpiece physics on arrival? |
| `_snapToPosition` | Align workpiece to this transform? |

**Events:**
- `OnObjectArrived` - Fires when object enters
- `OnObjectLeft` - Fires when object exits

---

#### `ConveyorBelt.cs`
**Purpose:** Moves objects along a conveyor surface.

**Type:** MonoBehaviour

**Setup:**
1. Create conveyor GameObject with collider
2. Add `ConveyorBelt` component
3. Set movement direction and speed
4. Optionally add waypoints for curved conveyors

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_speed` | Movement speed in m/s |
| `_direction` | Local direction of movement |
| `_isRunning` | Is conveyor active? |
| `_useWaypoints` | Follow waypoint path? |
| `_waypoints` | List of Transform waypoints |

**Key Methods:**
- `StartConveyor()` / `StopConveyor()` / `ToggleConveyor()`
- `SetSpeed(float)` - Adjust speed at runtime

---

#### `CNCMachineExtended.cs`
**Purpose:** Main CNC machine controller with state machine and path following.

**Type:** MonoBehaviour

**Setup:**
1. Add to CNC machine root GameObject
2. Assign `_cutter` reference (CNCCutterExtended)
3. Assign `_loadingZone` (TransferPoint for workpiece detection)
4. Add PathData assets to `_availablePaths`

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_cutter` | Reference to CNCCutterExtended |
| `_resultGenerator` | Reference to CNCResultGenerator |
| `_loadingZone` | TransferPoint for workpiece loading |
| `_positioningDuration` | Seconds in positioning state |
| `_defaultMode` | Manual or Auto |
| `_availablePaths` | List of PathData for selection |
| `_requireWorkpiece` | Must have workpiece to start? |

**States:** `Idle` → `Positioning` → `FollowingPath`/`Cutting` → `Done` → `Idle`

**Key Methods:**
- `LoadPath(PathData)` - Load a cutting path
- `StartAutoCut()` / `StartManualCut()` / `StartCut()`
- `StopCut()` / `EmergencyStop()` / `Reset()`
- `SetMode(CutterMode)` - Switch Manual/Auto

**Events:**
- `OnStateChanged` / `OnCutComplete` / `OnPathLoaded` / `OnCutProgress`

---

#### `CNCCutterExtended.cs`
**Purpose:** The cutting head that moves along paths or via joystick control.

**Type:** MonoBehaviour

**Setup:**
1. Add to cutter head GameObject (child of CNC machine)
2. Set movement bounds and speeds
3. Reference is assigned in CNCMachineExtended

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_moveSpeed` | Manual mode movement speed |
| `_workAreaBounds` | WorkAreaBounds defining movement limits |
| `_plungeSpeed` | Vertical plunge speed |
| `_retractSpeed` | Vertical retract speed |
| `_pathFollowTolerance` | Distance to waypoint before advancing |

**Key Methods:**
- `SetMode(CutterMode)` - Switch Manual/Auto
- `SetEnabled(bool)` - Enable/disable movement
- `MoveToStart(PathData)` - Position at path start
- `FollowPathStep(PathData, ref index, out progress)` - Advance along path
- `Plunge(float depth)` / `Retract()`
- `GetRecordedPath()` - Get list of positions visited

---

#### `CNCResultGenerator.cs`
**Purpose:** Generates deformed meshes showing cutting results. Handles real-time vertex deformation and sawdust effects.

**Type:** MonoBehaviour

**Setup:**
1. Add to same GameObject as CNCMachineExtended
2. Assign `_cutter` and `_cutterTip` references
3. Assign `_crossSectionMaterial` for cut surfaces
4. Optionally assign `_sawdustParticles` system

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_cutter` | Reference to CNCCutterExtended |
| `_cutterTip` | Transform at tip of cutting bit |
| `_crossSectionMaterial` | Material for cut surfaces |
| `_enableRealTimeDeformation` | Deform mesh during cutting? |
| `_deformationResolution` | Detail level (10-100) |
| `_deformationUpdateInterval` | Seconds between updates |
| `_sawdustParticles` | Particle system for debris |
| `_spawnDebris` | Spawn debris pieces on completion? |

**Key Methods:**
- `SetCurrentWorkpiece(GameObject)` - Set workpiece to modify
- `GenerateResult(GameObject, List<Vector3>, PathData)` - Apply final deformation
- `StartDeformation(PathData)` / `StopDeformation()`
- `ResetMesh()` - Restore original mesh

---

### UI Scripts
**Location:** `Assets/Scripts/UI/`

User interface components for the workshop.

---

#### `TaskDisplayPanel.cs`
**Purpose:** HUD panel showing current task, step, score, and progress.

**Type:** MonoBehaviour

**Setup:**
1. Create UI Canvas (World Space for VR, Screen Space for desktop)
2. Create panel with text elements and progress bar
3. Add `TaskDisplayPanel` component
4. Assign all UI element references

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_taskNameText` | TMP_Text for task name |
| `_stepNameText` | TMP_Text for current step |
| `_instructionsText` | TMP_Text for step instructions |
| `_hintText` | TMP_Text for hints (hidden initially) |
| `_taskProgressBar` | Slider for overall progress |
| `_scoreText` | TMP_Text for score display |
| `_timerText` | TMP_Text for elapsed time |
| `_stepCompletePanel` | GameObject to show on step completion |
| `_taskCompletePanel` | GameObject to show on task completion |

**Automatic Behavior:** Subscribes to TaskManager events and updates automatically.

---

#### `CNCControlPanelExtended.cs`
**Purpose:** In-world control panel for operating the CNC machine.

**Type:** MonoBehaviour

**Setup:**
1. Create control panel UI (World Space Canvas for VR)
2. Add buttons for Start, Stop, Emergency Stop, Reset
3. Add path selection dropdown or buttons
4. Add `CNCControlPanelExtended` component
5. Assign `_cncMachine` reference
6. Assign all UI element references

**Key Inspector Fields:**
| Field | Description |
|-------|-------------|
| `_cncMachine` | Reference to CNCMachineExtended |
| `_pathDropdown` | TMP_Dropdown for path selection |
| `_pathButtons` | Alternative: List of buttons for VR |
| `_manualModeButton` / `_autoModeButton` | Mode selection |
| `_startButton` / `_stopButton` | Operation controls |
| `_emergencyStopButton` | Emergency stop |
| `_statusText` | Current machine state |
| `_progressBar` | Cutting progress |
| `_statusLight` | Image for status indicator |

**Automatic Behavior:** 
- Updates button states based on machine state
- Checks with TaskManager if machine is allowed before starting
- Updates path list from CNCMachineExtended

---

### Utility Scripts
**Location:** `Assets/Scripts/Utilities/`

Helper classes for mesh operations.

---

#### `MeshCutter.cs`
**Purpose:** Static utility class for mesh cutting operations.

**Type:** Static class

**Integration:** Used by CNCResultGenerator internally.

**Key Methods:**
- `SliceMesh(Mesh, planePoint, planeNormal)` - Cut mesh along plane
- `SliceGameObject(GameObject, planePoint, planeNormal)` - Cut and create new GameObjects
- `CarveChannel(Mesh, pathPoints, toolDiameter, depth, transform)` - CNC-style carving
- `DeformAlongPath(Mesh, pathPoints, PathData)` - Deform vertices along path
- `GenerateBoxMesh(Vector3 dimensions)` - Create box mesh
- `GenerateSubdividedPlane(width, height, resX, resZ)` - Create high-res plane for deformation

**EzySlice Integration:**
The `SliceMesh` method includes placeholder code. To enable real mesh boolean operations:
1. Install EzySlice package
2. Add `using EzySlice;` at the top of the file
3. Uncomment the EzySlice integration code (marked with comments)

---

## Event System Reference

### Quick Reference Table

| Event | Parameters | Fired When |
|-------|------------|------------|
| `OnTaskStarted` | `string taskName` | Task begins |
| `OnTaskCompleted` | `string taskName` | Task finished successfully |
| `OnStepStarted` | `string stepName` | New step begins |
| `OnStepCompleted` | `string stepName, int index` | Step completed |
| `OnCNCStateChanged` | `CNCState state` | CNC state machine transitions |
| `OnPathLoaded` | `PathData path` | Path loaded into CNC |
| `OnCutProgress` | `float progress` | During cutting (0-1) |
| `OnWorkpieceSpawned` | `GameObject workpiece` | Workpiece created |
| `OnWorkpieceTransferred` | `GameObject, TransferPoint` | Workpiece reaches destination |
| `OnWorkpieceCut` | `GameObject workpiece` | Cut operation completed |
| `OnWorkpieceDespawned` | `GameObject workpiece` | Workpiece destroyed/pooled |
| `OnSafetyViolation` | `SafetyEvent event` | Safety rule violated |
| `OnScoreChanged` | `int score, string reason` | Score updated |
| `OnErrorRecorded` | `string errorType` | Error logged |
| `OnConveyorStateChanged` | `ConveyorBelt, bool running` | Conveyor started/stopped |

### Raising Events (for custom code)

```csharp
// Examples of raising events from your code
GameStateEvents.RaiseWorkpieceSpawned(myWorkpiece);
GameStateEvents.RaiseSafetyViolation(SafetyEvent.SpeedViolation(speed, maxSpeed, pos));
GameStateEvents.RaiseScoreChanged(newScore, "Bonus points!");
```

---

## Quick Start Guide

### Minimum Setup for Testing

1. **Create Singletons GameObject**
   - Create empty GameObject: "GameManagers"
   - Add components: `ScoreManager`, `FeedbackManager`, `TaskManager`, `ConsequenceSystem`

2. **Create a Workpiece Prefab**
   - Create Cube GameObject
   - Add MeshFilter, MeshRenderer, BoxCollider, Rigidbody
   - Save as prefab

3. **Create WorkpieceData**
   - Assets → Create → CarpentryWorkshopVR → Workpiece Data
   - Set dimensions to match your cube
   - Assign the prefab

4. **Create PathData**
   - Assets → Create → CarpentryWorkshopVR → Path Data
   - Set pathType to "Rectangle", shapeWidth/Height to 0.1
   - Right-click → Regenerate Shape

5. **Setup CNC Machine**
   - Create empty GameObject: "CNCMachine"
   - Add `CNCMachineExtended` component
   - Create child "CutterHead" with `CNCCutterExtended`
   - Create child "LoadingZone" with `TransferPoint` (add BoxCollider as trigger)
   - Add `CNCResultGenerator` to CNCMachine
   - Wire up references in Inspector
   - Add your PathData to Available Paths list

6. **Create Spawner**
   - Create empty GameObject: "Spawner"
   - Add `ObjectSpawner` component
   - Assign your WorkpieceData
   - Enable Auto Spawn for testing

7. **Play and Test**
   - Press Play
   - Workpiece should spawn
   - Drag workpiece to CNC loading zone
   - CNC should detect it
   - Use Debug menu or add UI to start cutting

---

## Creating Content

### Creating a New Task

1. **Create TaskStep assets first:**
   ```
   Assets → Create → CarpentryWorkshopVR → Task Step
   ```
   Create one for each action:
   - "Spawn Workpiece" (trigger: WorkpieceSpawned)
   - "Load into CNC" (trigger: WorkpieceTransferred)
   - "Select Path" (trigger: PathLoaded)
   - "Start Cutting" (trigger: CNCStateChange, requiredState: FollowingPath)
   - "Complete Cut" (trigger: CuttingComplete)

2. **Create Task asset:**
   ```
   Assets → Create → CarpentryWorkshopVR → Task
   ```
   - Add all TaskSteps to the `steps` list in order
   - Set completion points and time bonus

3. **Add to TaskManager:**
   - Select your TaskManager GameObject
   - Add your Task to `_availableTasks`

### Creating a Custom Cutting Path

1. **Create PathData:**
   ```
   Assets → Create → CarpentryWorkshopVR → Path Data
   ```

2. **For basic shapes:**
   - Set `pathType` to Rectangle or Circle
   - Set `shapeWidth`, `shapeHeight`, `shapeCenter`
   - Right-click → Regenerate Shape

3. **For custom paths:**
   - Set `pathType` to Custom
   - Add waypoints manually in the `waypoints` list
   - Each waypoint is a Vector3 in local CNC space (XZ plane)

4. **Configure cutting parameters:**
   - `feedRate`: How fast (start with 0.05 m/s)
   - `plungeDepth`: How deep per pass (0.005m = 5mm)
   - `passes`: Number of passes for total depth
   - `toolDiameter`: Affects cut width (0.006m = 6mm)

---

## Testing Checklist

### Core Systems
- [ ] ScoreManager tracks points correctly
- [ ] FeedbackManager plays sounds
- [ ] GameStateEvents fire and can be subscribed to

### Spawning & Transfer
- [ ] ObjectSpawner creates workpieces
- [ ] Workpiece component initializes correctly
- [ ] TransferPoint detects arrivals
- [ ] ConveyorBelt moves objects

### CNC Operation
- [ ] CNCMachineExtended state transitions work
- [ ] Manual mode allows joystick control
- [ ] Auto mode follows path waypoints
- [ ] Path loads and validates correctly
- [ ] Progress reports during cutting
- [ ] Emergency stop works

### Mesh Deformation
- [ ] CNCResultGenerator deforms mesh during cutting
- [ ] Mesh updates visible in real-time
- [ ] Final result preserves deformation
- [ ] Particle effects work (if assigned)

### Task System
- [ ] TaskManager starts tasks
- [ ] Steps auto-complete on triggers
- [ ] Machine locking works in guided mode
- [ ] Score awarded on step/task completion
- [ ] Task completion detected correctly

### Safety System
- [ ] Safety violations detected
- [ ] ConsequenceSystem applies penalties
- [ ] Warning sounds play
- [ ] Screen flash works (if UI assigned)
- [ ] Cooldowns prevent spam

### UI
- [ ] TaskDisplayPanel shows current task/step
- [ ] Progress bar updates
- [ ] CNCControlPanelExtended controls machine
- [ ] Button states update correctly
- [ ] Path dropdown populates

---

## Troubleshooting

### Common Issues

**"No instance found" warnings on play:**
- Ensure singleton GameObjects exist in scene (ScoreManager, FeedbackManager, TaskManager)

**CNC doesn't detect workpiece:**
- Check TransferPoint has a trigger collider
- Verify workpiece has correct tag if tag filtering is enabled

**Mesh deformation not visible:**
- Ensure workpiece has MeshFilter with valid mesh
- Check `_enableRealTimeDeformation` is true
- Verify CNCResultGenerator has cutter reference

**Task steps don't auto-complete:**
- Verify `autoComplete` is true on TaskStep
- Check `completionTrigger` matches expected event
- Ensure TaskManager `_autoAdvance` is true

**Buttons always disabled:**
- Check CNCControlPanelExtended has `_cncMachine` reference
- Verify CNC is in correct state for action

---

## Migration Notes

### Changes from Previous Version

**Renamed Classes:**
- `CuttingPath` → `WorkAreaBounds` (to avoid confusion with `PathData`)

**Updated References:**
- `CNCCutter._cuttingPath` → `CNCCutter._workAreaBounds`
- `CNCCutterExtended._cuttingPath` → `CNCCutterExtended._workAreaBounds`
- `CNCScreenDisplay._cuttingPath` → `CNCScreenDisplay._workAreaBounds`

**If upgrading from old CuttingPath assets:**
1. Create new WorkAreaBounds asset
2. Copy values from old CuttingPath
3. Re-assign references in CNCCutter, CNCCutterExtended, CNCScreenDisplay
4. Delete old CuttingPath assets

---

## Version History

- **v1.0** - Initial release with complete CNC workflow system
  - 20 scripts across Data, GameState, Machines, UI, Utilities layers
  - Event-driven architecture via GameStateEvents
  - Task progression with guided mode
  - Real-time mesh deformation support

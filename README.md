# CarpentryWorkshopVR

A virtual reality simulation of a carpentry workshop built in Unity 6, using the Universal Render Pipeline (URP). Players learn and practice woodworking tasks — operating a CNC machine, using hand tools, assembling parts, and navigating safety protocols — in an interactive VR environment.

---

## Project Overview

| Property | Value |
|---|---|
| Engine | Unity 6 (6000.3.5f1) |
| Render Pipeline | Universal Render Pipeline (URP) 17.3.0 |
| Target Platform | VR (OpenXR / Meta Quest) |
| Input System | Unity Input System 1.14+ |
| Color Space | Linear |
| Repository | https://github.com/carpentryWorkshop/CarpentryWorkshopVR |

---

## Team Responsibilities

### Scripts
| Area | Description |
|---|---|
| VR player movement & interaction | How the player moves in VR, grabs objects, points at tools, and interacts with the environment |
| Tool usage logic | What happens when tools are used — hammering nails, pressing CNC buttons, triggering safety reactions |
| Game states & task progression | Manages task steps (start → task → completion) and unlocks the next action when goals are met |
| Debugging & performance optimization | Bug fixing and ensuring smooth VR performance without lag or crashes |
| Machine control panels | Buttons, sliders, and screens that control machines like the CNC |
| CNC cutting system logic | Controls wood cutting: positioning, starting, stopping, and generating the result shape |
| Conveyor & object transfer | How objects move between machines via conveyors or automatic systems |
| Scoring, feedback & consequence system | Feedback to the player (success sounds, warnings) and consequences for mistakes (slowdowns, retries, safety alerts) |

### Graphics
| Area | Description |
|---|---|
| Workshop environment | Workshop room, walls, floors, and machine placement in realistic positions |
| Machine & tool 3D models | CNC machines, hammers, wood parts, and hand tools |
| Lighting, materials & scene setup | Lights, colors, and textures for a realistic, readable VR environment |
| Asset optimization for VR | Reducing model size, textures, and polygon count for VR performance |
| Object assembly visuals | Visual feedback when parts connect, snap together, or are assembled correctly |
| Animations | Hands, machines, tools, and safety reactions (e.g. pulling hand away) |
| UI/UX visuals for panels | Clean, readable control panels and menus for VR |
| Sound integration & visual feedback | Cutting, hammering, alert sounds, and visual effects (sparks, highlights) |

---

## Project Roadmap

### Phase 1 — Core Prototype `09/02 → 14/03`
**Goal: Basic VR workshop running**
- [ ] Player movement, grab system, basic tool interaction
- [ ] Simple CNC control panel + object placement logic
- [ ] Workshop environment + main machine models
- [ ] Basic animations + UI mockups

### Phase 2 — Functional Systems `15/03 → 04/04`
**Goal: Core gameplay systems complete**
- [ ] Safety system (finger hit reaction), task flow
- [ ] Conveyor system + CNC cutting workflow
- [ ] Improved assets, textures, lighting
- [ ] UI visuals + tool animations + sound effects
- [ ] Object assembly system
- [ ] Robotic arm integration

### Phase 3 — Polished Build `05/04 → 25/04`
**Goal: Final playable version**
- [ ] Bug fixing, performance, VR optimization
- [ ] Final system tuning + testing scripts
- [ ] Final environment polish + asset optimization
- [ ] Final animations, VFX, UI polish

---

## Repository Structure

```
CarpentryWorkshopVR/
├── Assets/
│   ├── Models/                  # Custom 3D models (CNC, ControlPanel, Conveyor, RoboticArm, Table)
│   ├── Scenes/
│   │   └── CarpentryWorkshop.unity   # Main scene
│   ├── Scripts/
│   │   ├── Player/              # Player movement and interaction
│   │   └── Utilities/           # Editor and runtime utilities
│   ├── Settings/                # URP render pipeline assets, input action map
│   └── ThirdParty/              # Purchased and free asset packs (do not modify)
│       ├── CoolWorks_Studio/    # Carpentry hand tools (FBX + materials)
│       ├── FactoryTools/        # Factory tool set (FBX + materials)
│       ├── FreeIndustrialModels/# Pallets, pallet jack, wood planks
│       ├── LeartesStudios/      # Interior demo scenes (HDRP — reference only)
│       └── UrbanAmericanAssets/ # Paint booth scene (reference only)
├── Packages/
│   └── manifest.json            # Unity package dependencies
├── ProjectSettings/             # Unity project configuration
├── CONTRIBUTING.md              # Git workflow and team conventions
└── README.md                    # This file
```

### Script Map

| Script | Location | Purpose |
|---|---|---|
| `PlayerController.cs` | `Scripts/Player/` | First-person fly-mode controller (WASD + mouse look) — will be replaced/extended for VR |
| `JoinMeshes.cs` | `Scripts/Utilities/` | Combines child mesh filters into a single merged mesh at runtime |

> Third-party scripts in `ThirdParty/` are not to be modified.

---

## Setup

### Prerequisites
- Unity 6 (6000.3.5f1) or later
- Git
- (Optional) GitHub CLI (`gh`) for PR management

### First-Time Setup

```bash
git clone https://github.com/carpentryWorkshop/CarpentryWorkshopVR.git
cd CarpentryWorkshopVR
git config user.name "Your Name"
git config user.email "you@example.com"
```

Open the project folder in Unity Hub. Unity will import packages automatically on first open.

### Opening the Main Scene

In the Unity Project window, navigate to:
```
Assets/Scenes/CarpentryWorkshop.unity
```
Double-click to open.

---

## Packages & Dependencies

| Package | Version | Purpose |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 | URP rendering |
| `com.unity.inputsystem` | 1.17.0 | New Input System |
| `com.unity.ai.navigation` | 2.0.9 | NavMesh for NPCs / automation |
| `com.unity.timeline` | 1.8.10 | Cutscenes and animation sequencing |
| `com.unity.visualscripting` | 1.9.9 | Optional visual scripting |
| `com.unity.modules.vr` | 1.0.0 | VR module |
| `com.unity.modules.xr` | 1.0.0 | XR module |

---

## Conventions

- Follow the branch and commit conventions in [CONTRIBUTING.md](CONTRIBUTING.md)
- Branch prefix: `feature/`, `fix/`, `chore/`, `refactor/`
- Commit format: `feat:`, `fix:`, `chore:`, `refactor:` + short description
- Never push directly to `main`
- Always commit `.meta` files alongside their asset
- Always close Unity before running git commands
- Never move or rename assets outside of the Unity Project window

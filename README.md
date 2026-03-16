# WFC Tech Test — Unity 3D Procedural Map Generator

A Unity 3D project implementing a **Semantic Wave Function Collapse (WFC)** pipeline for generating playable, blockout-style multiplayer combat maps.

## Overview

This system goes beyond a vanilla WFC solver. It uses a four-layer architecture to guarantee that every generated map is structurally sound and navigable:

| Layer | Responsibility |
|---|---|
| **Core** | Movement rules, grid primitives, occupancy modelling |
| **Semantic** | High-level archetype constraints (Floor, Wall, Portal, Stair…) |
| **Compile** | Semantic → voxel grid translation |
| **Runtime** | Unity MonoBehaviour wiring, prefab spawning, batch runner |

The generator supports:
- Deterministic reproduction via seed
- Multi-floor layouts (up to 3 levels)
- Obstacle variety (1–3 cell heights, variable widths)
- Portal / archway structures
- Stair and ramp connectors
- Post-generation connectivity validation
- In-editor tuning window and fuzz-test runner

## Project Structure

```
Assets/
├── _Project/
│   └── WFC/
│       ├── Core/           # Grid types, movement rules, solver interface
│       ├── Semantic/       # Semantic grid & adjacency rules
│       ├── Compile/        # SemanticToVoxelCompiler
│       ├── Validation/     # ConnectivityValidator, GenerationValidator
│       ├── Runtime/        # WfcGenerationPipeline, WfcGenerationRunner, BatchRunner
│       ├── Data/           # ScriptableObject assets (TileSet, GenerationConfig)
│       ├── Diagnostics/    # GenerationReport, BatchGenerationReport
│       └── Editor/         # WfcTuningWindow, WfcFuzzTestWindow, WfcAssetFactory
├── Tests/
│   └── Editor/             # EditMode unit tests (NUnit + Unity Test Framework)
├── Scenes/
├── Prefabs/
└── Settings/               # URP render pipeline settings
Packages/
ProjectSettings/
```

## Requirements

- **Unity** 2023.2.x (URP)
- **Packages**: Universal Render Pipeline 16, Unity Test Framework 1.3, AI Navigation 2.0, Timeline 1.8

## Getting Started

1. Clone the repo and open the project in Unity 2023.2.x.
2. Open `Assets/Scenes/` and load the main scene.
3. In the Unity menu, go to **WFC → Tuning Window** to adjust generation parameters.
4. Press **Generate** (or run the scene) to produce a map.
5. Use **WFC → Fuzz Test** to run batch generations and inspect the pass/fail report.

## Running Tests

Open **Window → General → Test Runner**, select **EditMode**, and click **Run All**.

Tests cover:
- `SemanticWfcSolverTests` — solver constraint propagation
- `SemanticToVoxelCompilerTests` — semantic-to-voxel translation
- `ConnectivityValidatorTests` — graph reachability checks
- `WfcGenerationPipelineTests` — end-to-end pipeline integration

## Architecture Notes

The core solver (`IWfcSolver`) is a pure C# interface with no Unity dependencies, making it independently unit-testable. Unity-specific concerns (MonoBehaviours, prefab spawning, ScriptableObjects) are isolated in the `Unity/Runtime` and `Editor` sub-namespaces.

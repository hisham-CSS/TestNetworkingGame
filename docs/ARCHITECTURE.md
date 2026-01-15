# Bomberman Architecture Documentation

## Overview
This project is a deterministic, networked multiplayer Bomberman clone built with **Monogame** (Rendering/Loop) and **.NET 9**. It uses a custom ECS-based core with rollback networking (GGPO-style) to handle latency and synchronization.

## Project Structure
- **Bomberman.Core**: Pure C# logic library. Contains ECS, Simulation, Physics, and Game Rules. **No Monogame dependencies.**
- **Bomberman.Net**: Networking layer. Handling Transport (UDP), Packets, and Rollback logic.
- **Bomberman.App**: Monogame application. Handles Input, Rendering, Audio, and Game State Management.
- **Bomberman.Tests**: NUnit test suite covering Core logic, Networking, and State/Input systems.

## Key Subsystems

### 1. ECS (Entity-Component-System) in Core
Located in `Bomberman.Core.ECS` and `Bomberman.Core.Game`.
- **World**: Container for all entities and component pools.
- **Components**: Plain Old Data (POD) structs (e.g., `TransformComponent`, `VelocityComponent`).
- **Systems**: Stateful logic processors (e.g., `MovementSystem`, `BombSystem`) that iterate over components and update `World`.
- **Simulation**: The orchestra conductor. It has a `Tick()` method that runs all systems in a deterministic order.

### 2. Networking & Rollback
Located in `Bomberman.Net` and `Bomberman.Rollback`.
- **Protocol**: Custom UDP protocol with packet types defined in `Bomberman.Net.Packets`.
- **Components**:
    - `NetworkController`: Manages connections and routes packets.
    - `PacketReassembler`: Handles reassembly of fragmented `StateChunk` packets.
- **Rollback Architecture**:
    - **RollbackSystem**: Coordinator for the rollback process.
    - **SnapshotStore**: Manages history of `World` snapshots.
    - **MispredictionDetector**: Compares local prediction vs remote inputs/state hash.
    - **ResimulationRunner**: Handles restoring state and fast-forwarding simulation.
    - **Input Prediction**: Local inputs applied immediately; remote inputs predicted/corrected.

### 3. State Management
Located in `Bomberman.App.States`.
- **GameStateManager**: Switch-based state machine.
- **IGameState**: Interface for `MenuState`, `LobbyState`, `PlayState`.
- **GameContext**: DI container holding `IRenderer`, `IInputService`, `NetworkController`.

### 4. Rendering & Input
Located in `Bomberman.App.Rendering` and `Bomberman.App.Input`.
- **IRenderer**: Abstraction over Monogame's `SpriteBatch`.
- **WorldRenderer**: Specialized renderer for the ECS `World` (Tiles, Bombs, Players). Decouples visual logic from `PlayState`.
- **IInputService**: Abstraction over `KeyboardState`. Maps keys to game actions.

## Data Flow
1. **Input**: `InputService` captures hardware input -> `InputState`.
2. **Network**: `NetworkController` bundles input -> UDP.
3. **Simulation**: `GameSession` (via `RollbackSystem`) feeds inputs to `Simulation`.
4. **ECS Update**: `Simulation.Tick()` runs Systems.
5. **Rendering**: `PlayState` calls `WorldRenderer.DrawWorld(World)`.

## Determinism
To ensure all clients see the same game:
- **Integer Math**: All physics/positions use `IntVector2` and fixed-point math (subpixel scale). floats are strictly avoid for logic.
- **Seeded RNG**: `Simulation` owns a `Random` instance synced via seed at start.
- **Execution Order**: Systems always run in the same order.

## Build & Deploy
- **Target**: .NET 9
- **Platform**: Windows (dx), Linux/Mac (DesktopGL) - *Project currently targeted for WindowsDX*.

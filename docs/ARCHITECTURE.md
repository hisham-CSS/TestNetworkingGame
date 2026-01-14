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
Located in `Bomberman.Net` and `Bomberman.Core.Rollback`.
- **Protocol**: Custom UDP protocol with packet types defined in `Bomberman.Net.Packets`.
- **Input Prediction**: Local inputs are applied immediately. Remote inputs are predicted (using last known input) and corrected later.
- **RollbackSystem**:
    - Stores snapshots of `World` state every frame.
    - If a remote input arrives for a past frame that differs from prediction, it:
        1. Restores the valid snapshot before that frame.
        2. Re-simulates frames up to current time using the new correct input.
    - **StateSync**: Periodic or forced full-state synchronization to handle late-joiners or desyncs.

### 3. State Management
Located in `Bomberman.App.States`.
- **GameStateManager**: Stack-based or switch-based state machine (currently simple switch).
- **IGameState**: Interface for states (`MenuState`, `LobbyState`, `PlayState`).
- **GameContext**: Dependency Injection container passed to all states. Holds references to `IRenderer`, `IInputService`, `NetworkController`, etc.

### 4. Rendering & Input
Located in `Bomberman.App.Rendering` and `Bomberman.App.Input`.
- **IRenderer**: Abstraction over Monogame's `SpriteBatch`. Allows decoupling logic from drawing.
- **IInputService**: Abstraction over `KeyboardState`. Maps keys to game actions (`Up`, `Down`, `PlaceBomb`). Support for Recording/Replaying inputs.

## Data Flow
1. **Input**: `InputService` captures hardware input -> `InputState` struct.
2. **Network**: `NetworkController` bundles local input -> Sends to peers via UDP.
3. **Simulation**: `PlayState` feeds inputs (Local + Remote/Predicted) to `GameSession`.
4. **ECS Update**: `GameSession` runs `Simulation.Tick()`. Systems update World components.
5. **Rendering**: `PlayState` reads World components (read-only) -> Calls `IRenderer` to draw.

## Determinism
To ensure all clients see the same game:
- **Integer Math**: All physics/positions use `IntVector2` and fixed-point math (subpixel scale). floats are strictly avoid for logic.
- **Seeded RNG**: `Simulation` owns a `Random` instance synced via seed at start.
- **Execution Order**: Systems always run in the same order.

## Build & Deploy
- **Target**: .NET 9
- **Platform**: Windows (dx), Linux/Mac (DesktopGL) - *Project currently targeted for WindowsDX*.

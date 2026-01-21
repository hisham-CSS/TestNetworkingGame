# Architecture Documentation

## Overview

This project is divided into two main layers:
1.  **Chronos**: A generic, reusable framework for deterministic networked games (GGPO-style).
2.  **Bomberman**: A concrete implementation of a Bomberman clone using Chronos and Monogame.

This separation allows the core networking and rollback logic to be reused for future projects (e.g., a Fighting Game) without modification.

## 1. Chronos Framework

The `Chronos` library handles the complex "transport" and "time travel" logic required for responsive multiplayer games. It is completely agnostic of the game rules.

### Chronos.Core
Contains the contracts that the game must implement.
- **`IGameSimulation<TInput, TState>`**: The bridge to the game. It demands methods to `Update()` (single frame), `CaptureState()`, and `RestoreState()`.
- **`IInputState<T>`**: Interface for input structs (must be serializable).
- **`IDeterministicState`**: Interface for game state snapshots (requires hashing).

### Chronos.Rollback
Implements the predictive rollback logic.
- **`RollbackSystem<TInput, TState>`**: The main engine. It manages the current frame, inputs from local/remote players, and decides when to rollback.
- **`SnapshotStore<TState>`**: A specialized circular buffer for storing game states.
- **`InputRecorder<TInput>`**: Handles storage and retrieval of input history.

### Chronos.Net
Handles the physical networking.
- **`NetworkController<TInput>`**: High-level manager. Handles connections, packet routing, and synchronizing the game start.
- **`ITransport`**: Abstraction for UDP. Includes `UdpTransport` (Production) and `SimulatedLagTransport` (Testing).
- **`NetworkProtocol`**: Defines the packet structure (Join, Start, Input, StateSync).

---

## 2. Bomberman Implementation

The game logic is built on top of Chronos, using a custom Entity-Component-System (ECS).

### Bomberman.Core
Pure C# game logic. No dependencies on Monogame or Graphics.
- **`Simulation`**: Implements `IGameSimulation`. It holds the ECS `World` and runs `Systems`.
- **`ECS`**: Custom lightweight ECS.
    - **Components**: Data-only structs (`TransformComponent`, `BombComponent`).
    - **Systems**: Logic (`MovementSystem`, `BombSystem`) that operates on Components.
- **`InputState`**: Implements `IInputState`. Contains buttons (Up, Down, Bomb) and movement vectors.

### Bomberman.App
The "Shell" or "View" layer, built with Monogame.
- **`Game1` / `GameContext`**: Entry point. Initializes the `Simulation` and `NetworkController`.
- **`States`**: Game loop states (`MenuState`, `LobbyState`, `PlayState`).
- **`Rendering`**: Reacts to the ECS World state. `WorldRenderer` draws entities based on their `TransformComponent` and `Type`.
- **`Input`**: Maps keyboard/gamepad to `InputState`.

---

## 3. Data Flow

### The Game Loop (PlayState)
1.  **Poll Network**: `NetworkController.Update()` receives packets and fires callbacks.
2.  **Read Input**: `InputService` reads local hardware input.
3.  **Step Rollback**: `RollbackSystem.Update(localInput)` is called.
    *   **Predict**: Used local input immediately.
    *   **Send**: Broadcasts local input to other players.
    *   **Check**: If remote inputs arrive for a past frame and mismatch the prediction (Desync), `RollbackSystem` restores the old state and resimulates up to the present.
    *   **Advance**: Calls `Simulation.Update()` to advance the game world.
4.  **Render**: `WorldRenderer` draws the current state of `Simulation.World`.

## 4. Determinism
Determinism is critical for Rollback.
- **Fixed-Point Math**: Logic uses `IntVector2` and integer math (sub-pixel coordinates) to avoid floating-point drift across architectures.
- **Seeded RNG**: `Simulation` uses a synchronized random seed.
- **Strict Ordering**: Systems update in a fixed order.

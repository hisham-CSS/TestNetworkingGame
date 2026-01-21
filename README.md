# Bomberman Clone

A deterministic, networked multiplayer Bomberman clone built with **Monogame** and **.NET 9**.
Developed using a custom Entity-Component-System (ECS) and Rollback Networking (GGPO-style).

## Requirements
- .NET 9.0 SDK
- Windows (DirectX) - *Primary Target*
- (Optional) Linux/Mac (DesktopGL) - *Support experimental*

## Build Instructions
1. Clone the repository.
2. Open terminal in the root directory.
3. Run the following command:
   ```bash
   dotnet build
   ```

## Running the Game

### Single Instance (Manual)
Run the application directly:
```bash
cd src/Bomberman.App
dotnet run
```

### Local Multiplayer Test Session
Use the provided PowerShell script to launch 4 instances (1 Host + 3 Clients) automatically:
```powershell
.\launch_test_session.ps1
```

## Controls

### Menus
- **UP / DOWN**: Navigate
- **ENTER**: Select / Confirm
- **ESC**: Back / Exit

### Gameplay
- **W, A, S, D**: Movement (P1)
- **Arrow Keys**: Movement (P2)
- **SPACE**: Place Bomb
- **ESC**: Open Menu / Leave Game
- **F1**: Toggle Debug Overlay
- **F5**: Decrease Simulated Latency (50ms)
- **F6**: Increase Simulated Latency (50ms)

## Networking
- The game uses UDP for communication.
- **Port**: Host binds to UDP port 5000 by default. Clients use random ports.
- **Localhost**: The game supports loopback networking for local testing.

## Logging
- Logs are written to `gamelog.txt` in the execution directory.
- Debug logs are also printed to the console window.

## Architecture

The project follows a strict separation of concerns:

- **Chronos**: A generic, framework-agnostic rollback networking library.
    - **Chronos.Core**: core interfaces (`IGameSimulation`, `IInputState`).
    - **Chronos.Rollback**: Generic GGPO-style rollback engine.
    - **Chronos.Net**: UDP networking and protocol layer.
- **Bomberman.Core**: Pure C# Game Logic (ECS, Simulation) implementing `Chronos` interfaces.
- **Bomberman.App**: Monogame implementation (Rendering, Input, Game Loop).
- **Bomberman.Tests**: Unit tests for Core and Networking.

### Rollback Networking
The game uses a predictive rollback architecture:
1.  **Input Prediction**: Local inputs are applied immediately.
2.  **State Synchronization**: Hash checks are exchanged to verify consistency.
3.  **Resimulation**: On divergence, the game rolls back to the last confirmed frame and resimulates with correct inputs (handled by `ResimulationRunner`).

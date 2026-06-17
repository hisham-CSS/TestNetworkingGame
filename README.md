# Week 2 — Multithreading & Library Extraction  (branch week2-architecture)

Built on the Week 1 final (week1-foundation). The flat single-player project is refactored into a
reusable, framework-agnostic engine library and given a producer-consumer threaded game loop.

## Solution layout
    src/Bomberman.Core/     reusable engine library (no rendering dependency for logic)
        ECS/                Entity, Components (ComponentPool<T>), World
        Game/               Simulation (: IGameSimulation), GameSession, RenderSnapshot
        Input/              IInputState, InputState (serializable), InputBuffer
        Determinism/        StateHasher, DeterminismHarness
        Threading/          SimulationLoop (worker-thread sim + double-buffered snapshots)
    src/Bomberman.Net/      networking library SKELETON — ITransport seam only (impl in Week 3)
    src/Bomberman.App/      MonoGame View — submits input, draws the latest published snapshot
    Bomberman.Tests/        NUnit tests

## What changed from Week 1
    EXTRACT   flat project  ->  Bomberman.App (View) + Bomberman.Core (engine) + Bomberman.Net (seam)
    NEW       IGameSimulation<TInput>      clean Update boundary the engine is driven through
    NEW       IInputState<T>               serializable input (sets up Week 3 networking)
    NEW       GameSession                  Core-side owner of a match (sim + input buffer + frame)
    NEW       RenderSnapshot               immutable, render-ready copy of the world (the double buffer)
    NEW       SimulationLoop               fixed-timestep sim on a worker thread (producer-consumer)
    NEW       ITransport (Bomberman.Net)   transport seam; concrete UdpTransport lands in Week 3

The simulation moved onto its own thread without being rewritten — the payoff of Week 1's
isolated, deterministic design. The View now only submits input and draws published snapshots.

## Build · run · test  (.NET 9 SDK + MonoGame DesktopGL)
    dotnet build Bomberman.sln
    dotnet run --project src/Bomberman.App      # determinism check, then threaded gameplay
    dotnet test                                 # GameLogicTests, InputStateTests, ThreadingTests
Controls: WASD / arrows to move, Space to place a bomb.

## Interface note
The production Chronos IGameSimulation also has CaptureState / RestoreState — those are snapshot &
rollback functionality and are introduced in Weeks 4–5. Week 2 introduces the Update boundary only.
The "Chronos" module rename (Chronos.Core / Chronos.Net / Chronos.Rollback) also happens in Week 5;
here the library is still named Bomberman.Core / Bomberman.Net.

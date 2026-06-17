# Learning Activity 1 — Single-Player Bomberman with ECS  (STUDENT STARTER)

Implement the `TODO (LA1 ...)` hooks until the game is fully playable and the unit tests pass.

## What's already provided (scaffolding)
- ECS framework: `Entity`, `ComponentPool<T>`, `World`, and all component structs.
- Fixed 60 Hz game loop and the Menu / Playing / Game Over screens (drawn with `PixelFont`).
- Map generation, player spawn, movement + collision, and all rendering.
- The `InputState` struct and the determinism-harness wiring.

## Your hooks  (search the code for `TODO (LA1`)
Simulation.cs
  - Powerup pickup (inside `UpdatePlayers`)        ECS systems
  - `TryPlaceBomb`                                 bomb mechanics
  - `UpdateBombs`  (countdown -> explosion)        bomb mechanics
  - `Explode` + `ExplosionHit`                     propagation, crate destruction, powerup drops
  - `CheckPlayerDeaths`                            lose condition (hit by blast)
InputBuffer.cs
  - `Record`, `TryGet`                             256-frame circular input buffer
StateHasher.cs
  - `Hash`                                         determinism via state hashing

## "Done" is defined by the tests
    dotnet test
  - GameLogicTests.Test_InitialPlayerStats_AreCorrect   passes already (no hook needed)
  - GameLogicTests.Test_Powerup_IncreasesStats          passes once powerup pickup works

On launch the console prints a determinism report. While `Hash` returns 0 it "passes" trivially;
once you implement `Hash` and the gameplay systems, the record -> replay check becomes meaningful.

## Build & run  (.NET 9 SDK + MonoGame DesktopGL)
    dotnet run        # Menu: press ENTER to play.  In game: WASD/arrows move, SPACE drops a bomb.
    dotnet test Bomberman.Tests

## Submission (per the LA1 rubric)
Tag your completed repo `v1.0-bomberman-ecs`; submit the playable build, source, and the 1-2 page
design document (four-layer architecture, ECS design, fixed 60 Hz + determinism, 256-frame buffer).

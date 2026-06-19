# Learning Activity 1: Building Single-Player Bomberman with ECS

**Weight:** 15% of final grade  ·  **Due:** Week 3  ·  **Assessed CLOs:** 1, 2

## Overview
You will build a complete single-player Bomberman game using a data-oriented Entity Component System
(ECS) and a deterministic, fixed-timestep simulation. This is the architectural foundation for the
entire course: a clean four-layer split (Data, Logic, Input, View), a 60 Hz game loop decoupled from
rendering, an input buffer that enables replays, and a state hash that lets you verify the simulation is
perfectly deterministic. Everything that follows (networking, rollback) depends on this determinism.

You are given the ECS scaffolding (Entity, ComponentPool, World, components), the fixed-timestep loop,
the View, deterministic map generation, a determinism self-check harness, and a test. The gameplay rules,
the input buffer, and the state hash have been removed and replaced with `TODO (LA1 ...)` stubs. Your job
is to implement those stubs until the game is fully playable, the determinism check passes, and the tests
are green.

## Learning outcomes
1. Design and implement a data-oriented ECS with component pools and system organization.
2. Construct a fixed-timestep game loop (60 Hz) decoupled from the rendering framerate.
3. Develop an input buffering system that supports a replay system.
4. Verify game determinism through state-hash comparison.

## The definition of done
Three signals tell you the activity is complete:

- **`dotnet test`:** the unit tests pass.
- **Determinism check:** the self-check that runs at startup prints `DETERMINISM CHECK: PASS`. It records
  a session, replays the recorded inputs, and confirms the per-frame state hashes are identical. This
  exercises your input buffer and your state hash.
- **Playable:** you can place bombs, watch explosions propagate and destroy crates, pick up powerups that
  raise your stats, and be killed by a blast.

The graded test suite is `GameLogicTests` (`Bomberman.Tests/GameLogicTests.cs`).
`Test_InitialPlayerStats_AreCorrect` passes already; `Test_Powerup_IncreasesStats` passes once your
powerup pickup works.

## Your tasks
Each task is marked in code with `// TODO (LA1 ...)`. Search for `TODO (LA1` to find all nine.

### Task 1 - Core gameplay  (`Simulation.cs`)
- **Powerup pickup:** when a player overlaps a powerup, apply it (Range → `BombRange + 1`, Capacity →
  `BombCapacity + 1`) and remove the powerup entity.
- **Place a bomb (`TryPlaceBomb`):** respect `BombCapacity` (count this owner's active bombs first), snap
  the bomb to the tile grid under the player, do not stack two bombs on one tile, and spawn a bomb
  (`Timer = 180`, `Range = player.BombRange`, `OwnerId`).
- **Tick bombs (`UpdateBombs`):** decrement each bomb's timer; when it reaches zero, trigger its explosion
  and remove the bomb.
- **Explosion propagation (`Explode`):** spawn explosion tiles outward from the bomb up to its `Range`,
  stopping at solid blocks and destroying destructible crates.
- **Blast-blocked check:** return true when the blast is blocked at a position (a solid tile), so
  propagation stops correctly.

### Task 2 - Lose condition  (`Simulation.cs`)
- **Player death:** if an active explosion overlaps a living player, set that player's `Alive` flag to
  false.

### Task 3 - Input buffer  (`InputBuffer.cs`)
- **`Record`:** store the inputs for a frame in the 256-frame ring (`slot = frame & 255`); clone the
  array, record the frame number, and update `LatestFrame`.
- **`TryGet`:** return the inputs for a frame ONLY if the slot still holds that exact frame (`frame >= 0`
  and the recorded frame number matches). This guard is what keeps replays correct.

### Task 4 - Determinism hash  (`StateHasher.cs`)
- **`Hash`:** produce a deterministic, order-sensitive hash of the whole `World` (player positions, stats,
  and alive flags; bomb positions and timers; destroyed crates; counts). Two runs of the same inputs must
  produce the same hash every frame.

## Build, run, test  (.NET 9 SDK + MonoGame DesktopGL)
```
dotnet run            # Menu: ENTER to play.  In game: WASD / arrows move, SPACE drops a bomb.
dotnet test           # GameLogicTests (your grade target)
```
The determinism self-check prints to the console at startup; it reads `PASS` once your input buffer and
state hash are correct.

## Grading
| Criterion | Weight |
|-----------|--------|
| Core gameplay (bomb placement, explosion propagation, powerup pickup) | 50% |
| Input buffer and determinism (replay self-check passes; state hash correct) | 30% |
| `GameLogicTests` pass | 10% |
| Code quality and comments | 10% |

## Hints
- The simulation must be deterministic: do not use randomness or wall-clock time inside `Update`. The map
  is generated once from a fixed seed.
- The 256-frame ring uses `slot = frame & 255` (the length is a power of two). A slot is valid only if it
  still records the exact frame you asked for.
- Build axis-aligned bounding boxes (`Rectangle`) from transforms and use `Rectangle.Intersects` for
  overlaps (powerup pickup, explosion hits).
- Bomb timers are in frames: `Timer = 180` is 3 seconds at 60 Hz. Decrement each tick; explode at zero.
- The state hash must include everything that can differ between two runs. A simple order-sensitive mix
  (for example Jenkins one-at-a-time) is enough.

## AI assistance policy
Per the syllabus, you may use LLMs as a tool, but you must disclose what was AI-assisted, attribute it,
critically evaluate and refine any generated code, and ensure the submission reflects your own
understanding. Passing tests with code you cannot explain will not serve you in the long run.

## Submission
Implement the hooks until the game is playable, the determinism check passes, and the tests are green. Tag
your completed repository `v1.0-bomberman-ecs` and submit the repository, including the playable build,
source, and a short (1 to 2 page) writeup of your design and any AI use.

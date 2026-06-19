# Learning Activity 3: Implementing Rollback Networking & Extracting Chronos

**Weight:** 20% of final grade  ·  **Due:** Week 7  ·  **Assessed CLOs:** 4, 5, 6

## Overview
You will implement client-side prediction and rollback networking, the core of modern competitive netplay
(the GGPO model). Lockstep (LA2) is correct but it waits for everyone: one slow or frozen peer stalls the
whole match. Rollback stops waiting. It predicts the remote player's input so the local game stays
responsive, then, when the real input arrives, detects whether the guess was wrong and rolls back and
re-simulates to correct it.

You work inside the **Chronos** library, the framework-agnostic netcode that has already been extracted
from the game into three modules: `Chronos.Core` (interfaces), `Chronos.Net` (transport and protocol),
and `Chronos.Rollback` (the rollback engine, generic over the input type `TInput` and the state snapshot
type `TState`). Your code lives in `Chronos.Rollback` and must stay generic: it may not reference
Bomberman types. The game (`Bomberman.Core`) is just one client of the library.

The rollback engine's central pieces have been removed and replaced with `TODO (LA3 ...)` stubs. Implement
them until the tests pass.

## Learning outcomes
1. Implement client-side prediction based on input history.
2. Design misprediction detection and correction.
3. Build a rollback and re-simulation system to restore previous states.
4. Apply frame-advantage time synchronization to keep two peers aligned.

## The definition of done
The repository already contains the tests that define success. On the starter they fail; when your
implementation is correct they pass:

```
dotnet test
```

The two suites that grade this activity, both in `src/Bomberman.Tests/`:

| Suite | File | What it checks |
|-------|------|----------------|
| `RollbackTests` | `Rollback/RollbackTests.cs` | the system steps, predicts, detects a misprediction, and rolls back |
| `SynchronizationTests` | `Net/SynchronizationTests.cs` | the frame-advantage step calculation (catch up / stall) |

A correct implementation also plays cleanly over LAN: two peers stay in sync, and a brief freeze on one
side is absorbed and corrected rather than stalling the other.

## Your tasks
Each task is marked in code with `// TODO (LA3 ...)`. Search for `TODO (LA3` to find all four.

### Task 1 - Client-side prediction  (`Chronos.Rollback/RollbackSystem.cs`)
- **`PredictInputForPlayer`**: when the remote input for the current frame has not arrived, predict it.
  The standard prediction is "repeat the peer's last confirmed input" (a player usually holds a direction
  for many frames). If there is no confirmed input yet, return a default `TInput`. The simulation always
  has a full input set, so it never stalls on the network.

*Exercised by `RollbackTests` (the system cannot step until prediction is implemented).*

### Task 2 - Misprediction detection  (`Chronos.Rollback/MispredictionDetector.cs`)
- **`DetectInputMisprediction`**: a received packet carries the peer's REAL inputs for a run of past
  frames. For each frame already simulated, compare the real input against the input you actually used
  (predicted) for that player, read from the `InputRecorder`. If they differ, that frame was mispredicted:
  record the EARLIEST such frame, and correct the recorded history with the authoritative input.

*Exercised by `RollbackTests.HandleRemoteInput_TriggersRollback_OnMisprediction`.*

### Task 3 - Rollback and re-simulation  (`Chronos.Rollback/ResimulationRunner.cs`)
- **`PerformRollback`**: restore the snapshot from just BEFORE the earliest mispredicted frame
  (`mispredictedFrame - 1`), then replay every frame up to the present. For each replayed frame, build the
  input array from the real remote inputs where known and predictions where not, call
  `simulation.Update(...)`, and re-save the snapshot. If the needed snapshot is missing, bail out safely.

*Exercised by `RollbackTests.HandleRemoteInput_TriggersRollback_OnMisprediction`.*

### Task 4 - Frame-advantage time sync  (`Chronos.Rollback/RollbackSystem.cs`)
- **`CalculateTargetSteps`**: given the local frame and the host's last confirmed frame, return how many
  steps to take this tick. If we are behind, take extra steps to catch up (capped). If we are too far
  ahead, stall (zero steps). Otherwise take one normal step. This keeps both peers within a few frames of
  each other, which keeps rollbacks short.

*Exercised by `SynchronizationTests` (in-sync, behind, ahead).*

## Build, run, test  (.NET 9 SDK + MonoGame DesktopGL)
```
dotnet build Bomberman.sln
dotnet test                                 # your grade target: RollbackTests + SynchronizationTests
dotnet run --project src/Bomberman.App      # menu -> HOST / JOIN; in a match, press K to force a desync
```
To play locally, run two copies and join one to the other. Rollback corrections are usually invisible; a
mispredicted frame snaps to the correct state within a frame or two.

## Grading
| Criterion | Weight |
|-----------|--------|
| `SynchronizationTests` pass (frame-advantage time sync) | 20% |
| `RollbackTests` pass (prediction + misprediction + rollback) | 55% |
| Chronos.Rollback stays generic (no Bomberman references) | 15% |
| Code quality and comments | 10% |

## Hints
- Prediction is per remote player: use `_lastConfirmedRemoteInputs[playerId]` if present, else `new TInput()`.
- Determinism is the whole game: rollback only works because the same inputs always produce the same
  world. Re-simulating a frame with the corrected inputs MUST reproduce the authoritative state.
- Rollback restores the snapshot BEFORE the wrong frame (`mispredictedFrame - 1`), then replays forward.
  Re-save a snapshot for each replayed frame so the history stays consistent.
- Keep the prediction window small relative to the snapshot buffer (see `RollbackConfig`): if you predict
  further than the buffer can rewind, a rollback target can be evicted and the client desyncs.
- `Chronos.Rollback` is generic over `TInput` and `TState`. Do not reach for a Bomberman type; everything
  you need is on the generic parameters and the constructor-injected dependencies.

## AI assistance policy
Per the syllabus, you may use LLMs as a tool, but you must disclose what was AI-assisted, attribute it,
critically evaluate and refine any generated code, and ensure the submission reflects your own
understanding. Passing tests with code you cannot explain will not serve you in the long run.

## Submission
Implement the hooks until both test suites are green, tag your completed repository `v3.0-rollback`, and
submit the repository, including a short (1 to 2 page) writeup of your design and any AI use.

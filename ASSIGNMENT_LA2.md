# Learning Activity 2: Implementing Lockstep Networking

**Weight:** 20% of final grade  ·  **Due:** Week 5  ·  **Assessed CLOs:** 4, 5

## Overview
You will extend the single-player Bomberman engine (LA1) into a two-player game over LAN using a
**lockstep** networking model. Lockstep is the foundation of deterministic networked games: both peers
exchange only their INPUTS each frame and recompute the world locally, so identical inputs produce
identical worlds. You will implement the binary protocol that puts inputs on the wire, the lockstep rule
that keeps the two simulations in step, input-delay scheduling to hide latency, and the redundancy that
survives packet loss.

You are given a working engine, a complete UDP transport and connection layer, a lobby, and a full test
suite. The pieces that make lockstep actually work have been removed and replaced with `TODO (LA2 ...)`
stubs. Your job is to implement those stubs until the tests pass.

## Learning outcomes
1. Design and implement a network protocol with packet structures and serialization.
2. Implement a lockstep networking model where players wait for each other's input.
3. Calculate and apply input delay based on network latency.
4. Handle packet loss through input redundancy.

## The definition of "done"
The repository already contains the tests that define success. On the starter they fail; when your
implementation is correct they all pass:

```
dotnet test
```

The three suites that grade this activity:

| Suite | File | What it checks |
|-------|------|----------------|
| `ProtocolTests` | `Bomberman.Tests/Net/ProtocolTests.cs` | packets serialize and deserialize losslessly |
| `LobbyTests` | `Bomberman.Tests/Net/LobbyTests.cs` | ready status propagates over a real UDP socket |
| `LockstepTests` | `Bomberman.Tests/Net/LockstepTests.cs` | the lockstep rule, input delay, and loss recovery |

## Your tasks

Each task is marked in the code with `// TODO (LA2 ...)`. Search for `TODO (LA2` to find all of them.

### Task 1 - Packet serialization  (`src/Bomberman.Net/Packets/`)
- **`InputPacket<TInput>.Serialize` / `Deserialize`** (`InputPacket.cs`): write, then read back, a packet
  that carries a run of inputs plus the player id, start frame, position, and state hash. The format is
  a leading `PacketType` byte, then the fields, then the input count, then each input. Deserialize must
  reverse it exactly, and must reject an absurd input count rather than allocating wildly.
- **`LobbyReadyPacket.Serialize` / `Deserialize`** (`LobbyPackets.cs`): the lobby ready toggle on the wire.

*Verified by `ProtocolTests` and `LobbyTests`.*

### Task 2 - The lockstep rule  (`src/Bomberman.Net/Lockstep/LockstepSession.cs`)
- **`TryAdvance`**: advance the simulation by exactly one frame ONLY when both players' inputs for the
  current frame are in hand. If either is missing, return `Stalled` and change nothing. When both are
  present, build the two-player input array (local input at `LocalPlayerId`, remote at `RemotePlayerId`),
  call `_session.Step(...)`, and return `Stepped`.

*Verified by `LockstepTests` (PureStall, AdvancesOnceBothInputsPresent).*

### Task 3 - Input delay and scheduling  (`LockstepSession.cs`)
- **`CalculateInputDelay`**: turn a measured round-trip time into a number of frames of input delay that
  covers the one-way latency, clamped to `[minDelay, maxDelay]`.
- **`SubmitLocalInput`**: schedule this tick's input to APPLY `InputDelay` frames in the future, and send
  it to the peer together with a short run of recent frames (redundancy) so one packet can cover dropped
  ones.

*Verified by `LockstepTests` (InputDelay prefill, CalculateInputDelay, RedundantHistory).*

### Task 4 - Remote input and packet loss  (`LockstepSession.cs`)
- **`HandleRemoteInput`**: store the remote inputs from a received packet. Each packet carries a run
  starting at `startFrame`; keep the FIRST value seen for each frame so duplicates are ignored and a
  later packet can fill a gap left by an earlier lost one. Ignore echoes of your own input.

*Verified by `LockstepTests` (RedundantHistory, DuplicateRemoteInput).*

## Build, run, test  (.NET 9 SDK + MonoGame DesktopGL)
```
dotnet build Bomberman.sln
dotnet test                                 # your grade target: all green
dotnet run --project src/Bomberman.App      # [H]ost  [J]oin 127.0.0.1  [F]ind LAN  [R] ready
```
To play locally once implemented, run two copies: press H in one, J in the other, R in both to start.

## Grading
| Criterion | Weight |
|-----------|--------|
| `ProtocolTests` pass (serialization) | 25% |
| `LockstepTests` pass (lockstep rule, delay, loss) | 50% |
| `LobbyTests` pass (lobby propagation) | 15% |
| Code quality and comments | 10% |

## Hints
- Use `BinaryWriter` / `BinaryReader`; write and read fields in the SAME order.
- The input type is generic: write each input with `input.Serialize(writer)` and read with
  `TInput.Deserialize(reader)`.
- In lockstep, the frame the simulation is ON is `_session.CurrentFrame`. Inputs are indexed by the
  frame they APPLY to.
- Determinism is the whole game: both peers must apply the same inputs in the same order. If you ever
  guess a missing input, the worlds diverge.

## AI assistance policy
Per the syllabus, you may use LLMs as a tool, but you must disclose what was AI-assisted, attribute it,
critically evaluate and refine any generated code, and ensure the submission reflects your own
understanding. Passing tests with code you cannot explain will not serve you on the exam or LA3.

## Submission
Commit your implementation and push your branch (or submit the repository) with all tests passing.

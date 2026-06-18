# Week 5 - Client-Side Prediction & Rollback  (branch week5-rollback)

This is the course's final netcode: the real, framework-agnostic **Chronos** library plus the Bomberman
game that drives it. The checkpoint is `ba2e936` ("split up network and rollback code into its own
generic library"), with one addition for this week: run-length input compression.

> Note on lineage: Weeks 1-4 built Bomberman on a deliberately simplified engine to teach each idea in
> isolation. Week 5 is the capstone and works in the production Chronos codebase, where those ideas come
> together at full fidelity. `main` builds further on this commit by adding a relay server for internet
> play (a deployment topic beyond this unit).

## The Chronos library (framework-agnostic)
    src/Chronos.Core/        interfaces only, no game or engine dependency
        IInputState, IGameState, IDeterministicState, IGameSimulation<TInput,TState>
    src/Chronos.Net/         transport, binary protocol, packets, NetworkController (generic over TInput)
        Protocol/InputCompression.cs   Week 5: run-length input compression
        Packets/CompressedInputPacket  Week 5: the compressed input packet (PacketType 14)
    src/Chronos.Rollback/    the rollback engine (generic over TInput, TState)
        RollbackSystem        prediction, snapshot history, misprediction-driven rollback, time sync
        MispredictionDetector input-history divergence + state-hash desync detection
        ResimulationRunner    restore a snapshot, replay corrected inputs to the present
        SnapshotStore, RollbackConfig, InputRecorder, telemetry
    src/Bomberman.Core/      the GAME: ECS, Simulation, GameStateSnapshot, StateHasher. Implements the
                             Chronos.Core interfaces (InputState : IInputState, etc.)
    src/Bomberman.App/       MonoGame view: menu, lobby, and PlayState driving the RollbackSystem
    src/Bomberman.Tests/     unit + integration tests (rollback, sync, protocol, lobby, ...)

## How rollback works (the heart of the week)
1. **Predict.** When the remote input for the current frame has not arrived, predict it (repeat their
   last confirmed input) and keep simulating. The local game never stalls waiting for the network.
2. **Snapshot.** Every simulated frame is captured into the SnapshotStore.
3. **Detect.** When the real remote input arrives (`HandleRemoteInput`), the MispredictionDetector finds
   the earliest frame where our prediction was wrong (input history) or where state hashes disagree.
4. **Roll back & resimulate.** The ResimulationRunner restores the snapshot just before that frame and
   replays the corrected inputs forward to the present. If predictions were right, nothing visibly
   changes; if wrong, the world snaps to the correct state.
5. **Time sync.** A bounded prediction window (`MaxPredictionFrames`) and frame-advantage stepping
   (`CalculateTargetSteps`) keep the two peers' frame counts close.

This is also the proper fix for the lockstep "window-drag freeze": a stalled peer no longer blocks the
other, because the other predicts ahead and reconciles when the real inputs arrive.

## Week 5 addition: input delta compression
`Chronos.Net.Protocol.InputCompression` run-length encodes the redundant input history that rollback
sends each frame. A player's input rarely changes frame to frame, so encoding runs instead of every
frame shrinks input packets by 70%+ (often 90%+) with no loss. `NetworkController.SendInput` now sends
the `CompressedInputPacket`; the raw `InputPacket` is kept as the uncompressed reference.

## Build / run / test  (.NET 9 SDK + MonoGame DesktopGL)
    dotnet build Bomberman.sln
    dotnet run --project src/Bomberman.App
    dotnet test                                # incl. RollbackTests, SynchronizationTests, DeltaCompressionTests

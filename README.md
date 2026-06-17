# Week 3 — Input Synchronization & Lockstep Networking  (branch week3-networking)

Built on the Week 2 final (week2-architecture). The `Bomberman.Net` seam is filled in: two players
connect over UDP and stay in perfect sync by exchanging INPUTS (not state) under a lockstep model.

## Solution layout
    src/Bomberman.Core/     reusable engine library (unchanged from Week 2)
    src/Bomberman.Net/      the networking library — now implemented
        ITransport.cs           transport seam (from Week 2)
        UdpTransport.cs         concrete non-blocking UDP socket (+ broadcast for discovery)
        Packets/                tagged binary packets (Input, Welcome, Lobby, Ping, ...)
        Protocol/               NetworkProtocol<TInput>: serialize/parse every packet
        Handlers/               PacketReassembler: rebuild chunked snapshots
        NetworkController.cs    high-level routing, heartbeat/ping/timeout, lobby, input send
        Lockstep/               LockstepSession: the stall-then-delay lockstep rule
    src/Bomberman.App/      MonoGame View — single-player loop OR networked lockstep
    Bomberman.Tests/Net/    ProtocolTests, LobbyTests, LockstepTests
    docs/NetworkProtocol.md the wire format

## What changed from Week 2
    NEW   UdpTransport          concrete ITransport: non-blocking UDP, port-retry, broadcast
    NEW   PacketType + Packets  14 tagged binary packet types (1 leading type byte each)
    NEW   NetworkProtocol<T>    one place that knows the wire format (Create* / Read*)
    NEW   NetworkController<T>  connection lifecycle: join/welcome/lobby/start, ping, timeouts
    NEW   LockstepSession       advance frame F only when BOTH players' inputs for F are in hand;
                                otherwise STALL. InputDelay=0 is pure stall; InputDelay=d hides
                                latency by scheduling local input d frames ahead.
    NEW   PacketReassembler     reassembles snapshots split across datagrams (used Weeks 4-5)

## The lockstep rule (the heart of the week)
Send inputs, not state. Each peer schedules its captured input `InputDelay` frames ahead and sends a
short history (loss insurance). The simulation only steps a frame once both players' inputs for that
frame have arrived; a missing input means STALL, never guess. Because the sim is deterministic
(Weeks 1-2) and both peers share a seed (delivered in `Welcome`), identical inputs produce identical
worlds on both machines.

## Build · run · test  (.NET 9 SDK + MonoGame DesktopGL)
    dotnet build Bomberman.sln
    dotnet run --project src/Bomberman.App      # [H]ost  [J]oin 127.0.0.1  [F]ind LAN  [R] ready
    dotnet test                                 # incl. ProtocolTests, LobbyTests, LockstepTests
In-game (single-player): WASD / arrows to move, Space to place a bomb.
To play locally: run two copies — press H in one, J in the other, R in both to start.

## Interface note
Still named `Bomberman.Net` (not `Chronos.Net`) and `NetworkController` is generic over the input
type as a first step toward framework independence. The `Chronos.Core / Chronos.Net / Chronos.Rollback`
rename, client-side prediction, and rollback all arrive in Week 5; snapshot state sync (StateSync /
StateChunk packets) is wired here but exercised in Weeks 4-5.

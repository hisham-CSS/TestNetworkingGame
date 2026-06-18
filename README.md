# Week 4 - State Hashing & Desync Detection  (branch week4-desync)

Built on the Week 3 final (week3-networking). Two networked peers now prove, every frame, that their
deterministic simulations still agree - and recover when they don't.

## Solution layout (new/changed this week)
    src/Bomberman.Core/
        Game/GameStateSnapshot.cs     complete, restorable world copy + BINARY serialize/deserialize
        Game/Simulation.cs            owns the Frame counter; implements CaptureState / RestoreState
        Game/IGameSimulation.cs       extended with the snapshot boundary
        Determinism/StateHasher.cs    Jenkins per-frame fingerprint (from Week 1)
        Determinism/Crc32.cs          CRC-32 checksum (lecture comparison vs Jenkins)
        Snapshots/SnapshotStore.cs    bounded ring of recent snapshots, looked up by frame
    src/Bomberman.Net/
        Packets/ChecksumPacket.cs     a peer's state hash for a confirmed frame
        Desync/DesyncDetector.cs      compares remote vs local hash; emits a DesyncReport
        Lockstep/LockstepSession.cs   snapshots each frame, exchanges checksums, detects + resyncs
        NetworkController.cs          SendChecksum / OnChecksumReceived / BroadcastStateSync
    Bomberman.Tests/
        Core/SnapshotTests.cs         capture/restore + binary round-trip + ring eviction
        Core/SerializationTests.cs    CRC-32 check value + Jenkins determinism
        Net/DesyncTests.cs            detection, and resync convergence

## How desync detection works
1. After each confirmed frame, lockstep captures a snapshot (its hash is the frame's fingerprint) and
   stores it in the SnapshotStore.
2. It announces that hash to the peer in a Checksum packet.
3. On receiving a remote checksum, DesyncDetector compares it to the local hash for that frame. Equal =
   in sync; different = a desync, reported with frame, both hashes, and the remote position.
4. Resync: the host (authoritative, player 0) ships its snapshot of the diverged frame via StateSync;
   the client deserializes and Restores it, then lockstep resumes from the authoritative state.

The StateSync / StateChunk packets and the PacketReassembler that Week 3 defined but left idle are
exactly what carries the snapshot here.

## Build / run / test  (.NET 9 SDK + MonoGame DesktopGL)
    dotnet build Bomberman.sln
    dotnet run --project src/Bomberman.App     # in a match, press K to force a desync and watch it recover
    dotnet test                                # incl. SnapshotTests, SerializationTests, DesyncTests
In a networked match the HUD shows the current frame, ping, "DESYNC Fn" when detected, and a resync
count. Pressing K on the CLIENT is the clearest demo: it corrupts the client's state and the host
corrects it.

## Hashing choices (lecture)
Jenkins one-at-a-time hashes the world's fields directly (no serialization, very fast). CRC-32 hashes
the serialized bytes (classic integrity check). SHA-256 is cryptographically strong but ~100x slower
and unnecessary for trusted-peer desync checks, where we only need to NOTICE divergence. The course
uses Jenkins as the working checksum and CRC-32 as the point of comparison.

## Looking ahead
Week 5 reuses this snapshot buffer and per-frame hash to do client-side prediction and full rollback:
instead of a hard resync to the host, mispredicted frames are rolled back and re-simulated locally.

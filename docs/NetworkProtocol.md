# Bomberman Network Protocol (Week 3)

UDP, protocol version 1, little-endian (`BinaryWriter` default). Every packet begins with a single
`PacketType` byte, followed by a fixed payload.

## Packet types
| ID | Name | Direction | Purpose |
|----|------|-----------|---------|
| 0 | Input | client->host / host->all | Player input history (+ pos/hash sync proxy) |
| 1 | JoinRequest | client->host | Ask to join (carries protocol version) |
| 2 | Welcome | host->client | Accept: assigned id, **shared seed**, player count |
| 3 | StartGame | host->all | Begin the match with seed + count |
| 4 | LobbyUpdate | host->all | Connected count + slot bitmask |
| 5 | DiscoveryRequest | client->broadcast | "Any servers?" |
| 6 | DiscoveryResponse | server->client | Name + current/max players |
| 7 | Heartbeat | both | Keep-alive (resets timeout) |
| 8 | Disconnect | both | Explicit goodbye |
| 9 | LobbyReady | client->host->all | Ready toggle |
| 10 | StateSync | host->client | Full snapshot (Weeks 4-5) |
| 11 | StateChunk | host->client | Snapshot fragment (Weeks 4-5) |
| 12 | Ping | both | Latency probe (timestamp) |
| 13 | Pong | both | Echo of a Ping timestamp |
| 14 | Checksum | both | Week 4: a peer's state hash for a confirmed frame (desync detection) |

## Connection lifecycle
1. **Discovery (optional):** client broadcasts `DiscoveryRequest` over a LAN port range; hosts answer with `DiscoveryResponse`.
2. **Join:** client sends `JoinRequest(version)`; host replies `Welcome(assignedId, seed, totalPlayers)` (or `Disconnect("Full")`).
3. **Lobby:** host broadcasts `LobbyUpdate`; players send `LobbyReady`; host re-broadcasts ready state.
4. **Start:** host broadcasts `StartGame(seed)`; both peers seed their RNG identically and enter lockstep.

## Gameplay (lockstep)
- Each peer captures local input and **schedules it `InputDelay` frames ahead**, sending it (with a few
  frames of history for loss recovery) via `InputPacket`.
- A peer advances frame **F only when it holds both players' inputs for F**; otherwise it **stalls**.
- `Input` packets also carry `PosX/PosY/StateHash` as a sync-check proxy — Week 4 uses these to detect desync.

## Checksum (Week 4)
`byte Type(14)` · `int Frame` · `int Hash` · `int PosX` · `int PosY`. A peer announces its state
hash for a confirmed frame; the receiver compares against its own stored hash for that frame. On a
mismatch the host pushes its authoritative snapshot back via `StateSync` and the client restores it.

## InputPacket schema
`byte Type(0)` · `int PlayerId` · `int StartFrame` · `int Count` · `Count × InputState` · `int PosX` · `int PosY` · `int StateHash`
where `InputState = float MoveX, float MoveY, bool PlaceBomb`.

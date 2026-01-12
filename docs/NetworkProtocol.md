# Network Protocol Definition

This document describes the binary protocol used by Bomberman.

## Overview
- **Transport**: UDP
- **Protocol Version**: 1
- **Endianness**: Little-Endian (BinaryWriter default)

## Packet Structure
All packets start with a single byte `PacketType`.

### Packet Types
| ID | Name | Description |
|----|------|-------------|
| 0 | Input | Input history from client or broadcast from host |
| 1 | JoinRequest | Client requesting to join |
| 2 | Welcome | Host accepting connection |
| 3 | StartGame | Host signalling game start |
| 4 | LobbyUpdate | Host broadcasting lobby slots |
| 5 | DiscoveryRequest | LAN discovery broadcast |
| 6 | DiscoveryResponse | Server responding to discovery |
| 7 | Heartbeat | Keep-alive |
| 8 | Disconnect | Explicit disconnection notice |
| 9 | LobbyReady | Player ready status |
| 10 | StateSync | Full state snapshot |
| 11 | StateChunk | Chunked state snapshot |

## Connection Flow
1. **Discovery** (Optional):
   - Client Broadcasts `DiscoveryRequest`.
   - Servers respond with `DiscoveryResponse`.
2. **Joining**:
   - Client sends `JoinRequest(Version)`.
   - Host responds with `Welcome(AssignedId, Seed, TotalPlayers)` OR `Disconnect("Full")`.
3. **Lobby**:
   - Host broadcasts `LobbyUpdate` periodically.
   - Client sends `LobbyReady`.
   - Host broadcasts `LobbyReady`.
4. **Game Start**:
   - Host broadcasts `StartGame`.
   - Game initializes with `Seed`.

## Gameplay Flow
- **Input**:
    - Clients send `InputPacket` to Host every frame.
    - Host aggregates and broadcasts `InputPacket` (Player 0 input includes all checks?) -> No, Host sends its own input as "Broadcast"? No, Host broadcasts ALL inputs?
    - Currently: `InputPacket` contains inputs for `PlayerId`.
    - Host broadcasts packets it receives? Or Host acts as Player 0?
    - `NetworkController.SendInput`: 
        - If Client: Sends to Host.
        - If Host: Broadcasts to Clients.

- **State Sync**:
    - Host can send `StateSync` (Full) or `StateChunk` (Split) to resync clients.
    - Used for late joiners or lag correction (Rollback).

## Packet Schema

### JoinRequest
- `byte` Type (1)
- `int` Version

### Welcome
- `byte` Type (2)
- `int` AssignedId
- `int` Seed
- `int` TotalPlayers

### Input
- `byte` Type (0)
- `int` PlayerId
- `int` StartFrame
- `int` Count
- `int` X, `int` Y (CurrentPos)
- `int` StateHash
- List of `InputState`:
    - `int` MoveX, `int` MoveY
    - `bool` PlaceBomb
    - `int` TargetX, `int` TargetY

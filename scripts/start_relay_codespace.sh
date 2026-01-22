#!/bin/bash
set -e

# Ensure we are in the repo root
if [ ! -f "Bomberman.sln" ]; then
    echo "Error: Please run this script from the repository root (e.g., ./scripts/start_relay_codespace.sh)"
    exit 1
fi

echo "=== Chronos Relay Server Setup for Codespaces ==="

# 1. Build Relay Server
echo "[1/3] Building Relay Server..."
dotnet build src/Chronos.RelayServer/Chronos.RelayServer.csproj -c Release

# 2. Download Playit.gg if not present
if [ ! -f "playit" ]; then
    echo "[2/3] Downloading Playit.gg agent..."
    # Using the static binary for Linux AMD64 (standard for Codespaces)
    curl -SsL https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-linux-amd64 -o playit
    chmod +x playit
    echo "Playit agent downloaded."
else
    echo "[2/3] Playit agent already present."
fi

# 3. Start Services
echo "[3/3] Starting Services..."

# Start Relay Server in background
echo "Starting Relay Server on port 7777..."
dotnet run --project src/Chronos.RelayServer/Chronos.RelayServer.csproj -c Release --no-build &
RELAY_PID=$!

echo "Relay Server started (PID: $RELAY_PID)."

echo "----------------------------------------------------------------"
echo "Starting Playit Tunnel. Follow the instructions below to connect."
echo "If this is your first time, you will see a link to claim this tunnel."
echo "Configure the tunnel to forward to: 127.0.0.1:7777 (UDP)"
echo "----------------------------------------------------------------"

# Start Playit
./playit

# Cleanup on exit
kill $RELAY_PID

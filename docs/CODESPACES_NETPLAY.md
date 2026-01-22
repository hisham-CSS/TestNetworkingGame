# Codespaces Netplay Setup

This guide explains how to host the `Chronos.RelayServer` on GitHub Codespaces to test internet netplay for free.

## Why Codespaces?
GitHub Codespaces provides a free cloud environment. However, it only natively forwards TCP ports. Since our game uses UDP (port 7777), we use a tunneling tool called [Playit.gg](https://playit.gg) to expose the UDP port to the internet.

## Prerequisites
- A GitHub account.
- This repository forked or accessible to you.

## Instructions

### 1. Start a Codespace
1. Go to the GitHub repository page.
2. Click **Code** -> **Codespaces** -> **Create codespace on main**.
3. Wait for the container to build and the VS Code web interface to load.

### 2. Run the Setup Script
1. In the terminal (Ctrl+`), run the following command:
   ```bash
   chmod +x scripts/start_relay_codespace.sh
   ./scripts/start_relay_codespace.sh
   ```
2. The script will:
   - Build the Relay Server.
   - Download the Playit.gg agent.
   - Start the Relay Server in the background.
   - Start the Playit agent.

### 3. Connect the Tunnel
1. The script output will provide a **Claim URL** (e.g., `https://playit.gg/claim/...`).
2. Ctrl+Click that link to open it in your browser.
3. Login/Sign up for Playit.gg (free).
4. The agent will automatically detect the local service.
   - **Important**: Ensure you configure the tunnel for **UDP**.
   - If prompted for a "Local Address", enter: `127.0.0.1:7777`.
   - If prompted for "Tunnel Type", choose **UDP**.

### 4. Connect Your Game
1. Playit.gg will give you a public address (e.g., `123.456.789.10:12345` or `uranium-potato.playit.gg:12345`).
2. Launch your local game client.
3. Enter this address in the "Join IP" field.
4. Enjoy netplay!

### 5. Cleanup
1. Stop the script in the Codespace terminal (Ctrl+C).
2. Stop/Delete the Codespace from your GitHub dashboard to save free hours.

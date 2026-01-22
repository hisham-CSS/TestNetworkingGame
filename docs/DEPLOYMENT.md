# Deploying Chronos Relay Server

This guide explains how to deploy the `Chronos.RelayServer` to a public Virtual Private Server (VPS) so your game can be played over the internet.

## 1. Prerequisites

*   A Cloud VPS provider (DigitalOcean, Linode, AWS EC2, Azure VM, etc.). A small instance (e.g., 1GB RAM) is sufficient.
*   **SSH Access** to your server.
*   **Docker** and **Docker Compose** installed on the server.

## 2. Server Setup (Ubuntu Example)

If you have a fresh Ubuntu server, run these commands to install Docker:

```bash
# Update repositories
sudo apt update
sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Install Docker Compose (if not included)
sudo apt install docker-compose -y
```

## 3. Deploying the Relay

You have two main options: **Manual File Transfer** (easiest for small projects) or **Container Registry** (better for automation). We'll use the Manual method here.

### Option A: Manual Transfer

1.  **Copy Files**: You need to copy the source code and docker config to your server. You can use `scp` (Secure Copy).

    Run this from your local machine (PowerShell or Terminal):

    ```powershell
    # Copy the src folder
    scp -r ./src root@<YOUR_SERVER_IP>:/root/bomberman/src

    # Copy docker-compose.yml
    scp ./docker-compose.yml root@<YOUR_SERVER_IP>:/root/bomberman/
    ```

    *Replace `<YOUR_SERVER_IP>` with your actual server IP address.*

2.  **Start the Server**:

    SSH into your server:
    ```bash
    ssh root@<YOUR_SERVER_IP>
    ```

    Navigate to the folder and start the service:
    ```bash
    cd /root/bomberman
    docker-compose up -d --build
    ```

    *   `up`: Starts the containers.
    *   `-d`: Detached mode (runs in background).
    *   `--build`: Forces a rebuild of the image.

3.  **Verify**:
     Run `docker-compose logs -f` to see the output. You should see:
    ```
    [Chronos Relay] Starting on port 7777...
    [Chronos Relay] Ready to accept connections.
    ```

### Option B: Self-Hosting (Home Network)

Yes, you can host this on your own spare laptop or PC! However, people on the internet cannot "see" your laptop directly because it's behind your home router.

To make it work, you must set up **Port Forwarding**.

1.  **Find your Local IP**:
    *   On the host machine, open PowerShell/Terminal.
    *   Run `ipconfig` (Windows) or `ifconfig` (Mac/Linux).
    *   Note the IPv4 Address (e.g., `192.168.1.50`).

2.  **Configure your Router**:
    *   Log in to your router's admin page (usually `192.168.1.1` or `192.168.0.1`).
    *   Look for "Port Forwarding", "Virtual Server", or "NAT" settings.
    *   **Create a Rule**:
        *   **Service Name**: BombermanRelay
        *   **Protocol**: **UDP** (Crucial!)
        *   **External Port**: 7777
        *   **Internal Port**: 7777
        *   **Internal IP**: Your computer's IP found in step 1 (e.g., `192.168.1.50`).
    *   Save/Apply.

3.  **Find your Public IP**:
    *   Go to Google and search "What is my IP".
    *   This IP (e.g., `45.12.x.x`) is what your friends will use to connect.

4.  **Run the Server**:
    *   Simply run the server on your machine (via Docker or `dotnet run`).
    *   Host game using `127.0.0.1` (since you are on the same machine/network).
    *   Friends join using your **Public IP**.

> [!NOTE]
> Home Public IPs change dynamically. If it stops working a few days later, check if your Public IP changed.

## 4. Firewall Configuration

You **MUST** allow UDP traffic on port 7777.

**For DigitalOcean/Cloud Firewalls:**
*   Log in to your provider's dashboard.
*   Find the Networking/Firewall section.
*   Add a temporary rule:
    *   **Protocol**: UDP
    *   **Port**: 7777
    *   **Source**: All IPv4 (0.0.0.0/0) and IPv6 (::/0)

**For Ubuntu (UFW):**
If you are using the internal firewall (UFW) on the server:
```bash
sudo ufw allow 7777/udp
```

## 5. Connecting Players

1.  **Host**:
    *   Select **HOST GAME (RELAY)**.
    *   Enter your **Relay Server Public IP**.
    *   Enter a **Session ID** (e.g., `1234`).
    *   Get in the Lobby.

2.  **Client**:
    *   Select **JOIN GAME (RELAY)**.
    *   Enter the same **Relay Server Public IP**.
    *   Enter the same **Session ID** (`1234`).

You should now be connected over the internet!

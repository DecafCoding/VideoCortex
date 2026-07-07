# Deploying Video Cortex to an Ubuntu Server

The app is built **on the server** from a clone of this repo, runs as a systemd service
bound to loopback, and sits behind nginx, which terminates TLS, proxies the Blazor Server
WebSocket, and serves the OKF libraries directly from disk at `/library/`. `Program.cs`
already trusts loopback `X-Forwarded-*` headers, so no code changes are needed per
environment.

Paths used below (adjust to taste):

| What | Where |
| --- | --- |
| Repo clone (build source) | `~/videocortex` (your admin user's home) |
| Published app | `/opt/videocortex` |
| Service user | `videocortex` (needs a real home dir) |
| State DB + config overlay | `/home/videocortex/.videocortex/` |
| OKF libraries | `/home/videocortex/SecondBrain/` |

## 1. Server setup (once)

```bash
sudo apt update
sudo apt install nginx git dotnet-sdk-10.0
```

If `dotnet-sdk-10.0` isn't in your Ubuntu release's feed, add the backports PPA
(`sudo add-apt-repository ppa:dotnet/backports`) or Microsoft's package feed, then retry.

Create the service user and the publish directory (owned by your admin user so
publishing doesn't need sudo; the service user only needs to read/execute):

```bash
sudo adduser --disabled-password --gecos "Video Cortex" videocortex
sudo mkdir -p /opt/videocortex
sudo chown "$USER": /opt/videocortex
```

## 2. Clone, build, install

```bash
git clone https://github.com/DecafCoding/VideoCortex.git ~/videocortex
cd ~/videocortex
dotnet publish VideoCortex/VideoCortex.csproj -c Release -o /opt/videocortex

sudo cp deploy/videocortex.service /etc/systemd/system/
sudo cp deploy/nginx/videocortex.conf /etc/nginx/sites-available/videocortex
sudo ln -s /etc/nginx/sites-available/videocortex /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
sudo systemctl daemon-reload && sudo systemctl enable --now videocortex
```

## 3. Updating to a new version

```bash
~/videocortex/deploy/update.sh
```

(That's just `git pull` + `dotnet publish` + `sudo systemctl restart videocortex`.)

## 4. API keys

Secrets are supplied server-side via the systemd `EnvironmentFile` — the Settings page
shows whether they are set but cannot edit them:

```bash
sudo mkdir -p /etc/videocortex
sudo tee /etc/videocortex/env >/dev/null <<'EOF'
Apify__Token=apify_api_...
Llm__ApiKey=sk-...
EOF
sudo chmod 600 /etc/videocortex/env
sudo systemctl restart videocortex
```

(On the dev machine, `dotnet user-secrets` still supplies these in Development.)

## 5. Migrate existing data (from the Windows install)

Copy to the **service user's** home on the server:

- `%USERPROFILE%\.videocortex\app.db`            → `/home/videocortex/.videocortex/app.db`
- `%USERPROFILE%\.videocortex\appsettings.Local.json` → same folder — **edit it first**:
  - If it contains a `Library:RootPath` like `C:\Users\...`, change it to
    `/home/videocortex/SecondBrain` (a Windows path won't fail on Linux — it silently
    creates a literal `C:\Users\...` directory).
  - **Delete any `ApiKey` / `Token` entries** left over from when the Settings page wrote
    secrets. The overlay is layered *after* environment variables, so a stale secret here
    would silently override `/etc/videocortex/env`.
- `Documents\SecondBrain\<each project folder>`  → `/home/videocortex/SecondBrain/`

Then:

```bash
sudo chown -R videocortex:videocortex /home/videocortex/.videocortex /home/videocortex/SecondBrain
sudo systemctl restart videocortex
```

## 6. Lock it down (LAN-only)

The app has **no authentication**, so restrict who can reach it. With no router port
forwarding it is already unreachable from the internet; make that explicit with ufw so a
misconfigured router can't change it (adjust the subnet to your LAN):

```bash
sudo ufw allow OpenSSH
sudo ufw allow from 192.168.1.0/24 to any port 80 proto tcp
sudo ufw enable
sudo ufw status verbose
```

Everything on your LAN (guests, IoT devices) can then open the app; the API keys are no
longer exposed in the UI, so the blast radius is queuing videos that spend API credits.
Uncomment the `auth_basic` lines in the nginx config for a cheap extra layer if wanted.

Want access from outside the house later? Add Tailscale — the server stays LAN-reachable
and also gets a tailnet name (`tailscale serve --bg 80` adds HTTPS for free). Never
port-forward the app to the open internet without auth + TLS.

## 7. Verify

- `systemctl status videocortex` — running; `journalctl -u videocortex -f` for logs.
- `http://<server>/` — the app loads and stays connected (no reconnect banner).
- `http://<server>/library/<Project>/` — the report renders with styling (nginx serves
  this even when the app is stopped).

# Deploying Video Cortex to an Ubuntu Server

The app runs as a systemd service bound to loopback, with nginx in front terminating
TLS, proxying the Blazor Server WebSocket, and serving the OKF libraries directly from
disk at `/library/`. `Program.cs` already trusts loopback `X-Forwarded-*` headers, so no
code changes are needed per environment.

Paths used below (adjust to taste):

| What | Where |
| --- | --- |
| App binaries | `/opt/videocortex` |
| Service user | `videocortex` (needs a real home dir) |
| State DB + config overlay | `/home/videocortex/.videocortex/` |
| OKF libraries | `/home/videocortex/SecondBrain/` |

## 1. Publish (on the dev machine)

Self-contained, so the server needs no .NET runtime install:

```bash
dotnet publish VideoCortex/VideoCortex.csproj -c Release -r linux-x64 --self-contained -o publish
```

(Framework-dependent also works — drop `-r`/`--self-contained` and `apt install
aspnetcore-runtime-10.0` from the Microsoft package feed on the server instead.)

## 2. Server setup (once)

```bash
sudo adduser --disabled-password --gecos "Video Cortex" videocortex
sudo mkdir -p /opt/videocortex
sudo apt install nginx
```

## 3. Copy the app and configs

From the dev machine:

```bash
rsync -av --delete publish/ server:/tmp/videocortex-publish/
```

On the server:

```bash
sudo rsync -av --delete /tmp/videocortex-publish/ /opt/videocortex/
sudo chmod +x /opt/videocortex/VideoCortex
sudo cp deploy/videocortex.service /etc/systemd/system/
sudo cp deploy/nginx/videocortex.conf /etc/nginx/sites-available/videocortex
sudo ln -s /etc/nginx/sites-available/videocortex /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
sudo systemctl daemon-reload && sudo systemctl enable --now videocortex
```

## 4. Migrate existing data (from the Windows install)

Copy to the **service user's** home on the server:

- `%USERPROFILE%\.videocortex\app.db`            → `/home/videocortex/.videocortex/app.db`
- `%USERPROFILE%\.videocortex\appsettings.Local.json` → same folder — **edit it first**: if it
  contains a `Library:RootPath` like `C:\Users\...`, change it to
  `/home/videocortex/SecondBrain` (a Windows path won't fail on Linux — it silently creates
  a literal `C:\Users\...` directory).
- `Documents\SecondBrain\<each project folder>`  → `/home/videocortex/SecondBrain/`

Then:

```bash
sudo chown -R videocortex:videocortex /home/videocortex/.videocortex /home/videocortex/SecondBrain
sudo chmod 600 /home/videocortex/.videocortex/appsettings.Local.json   # holds API keys
sudo systemctl restart videocortex
```

Secrets (Apify token, LLM key) can be entered on the Settings page (persisted to the
overlay) or supplied via `EnvironmentFile=` in the unit — see comments in
`videocortex.service`. `dotnet user-secrets` is Development-only and does not apply here.

## 5. Lock it down

The app has **no authentication** and its Settings page exposes your API keys. Pick one:

- **Tailscale (recommended):** install on the server, keep nginx on port 80, and only the
  tailnet can reach it. `tailscale serve --bg 80` adds HTTPS with a MagicDNS name for free.
- **Basic auth:** uncomment the `auth_basic` lines in the nginx config and create the
  htpasswd file. Pair with certbot TLS if exposed beyond the LAN.

Never port-forward it to the open internet without auth + TLS.

## 6. Verify

- `systemctl status videocortex` — running; `journalctl -u videocortex -f` for logs.
- `http://<server>/` — the app loads and stays connected (no reconnect banner).
- `http://<server>/library/<Project>/` — the report renders with styling (nginx serves
  this even when the app is stopped).

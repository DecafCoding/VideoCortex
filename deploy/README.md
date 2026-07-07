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

⚠️ `update.sh` does **not** reinstall the configs. If a pull changed
`deploy/videocortex.service` or `deploy/nginx/videocortex.conf`, re-copy them as in §2
(then `sudo systemctl daemon-reload && sudo systemctl restart videocortex` for the unit,
`sudo nginx -t && sudo systemctl reload nginx` for nginx). A stale unit fails silently —
new `Environment`/`EnvironmentFile` lines simply never load. Verify what systemd is
actually running with `systemctl cat videocortex`.

## 4. Environment file (API keys + library root)

Secrets are supplied server-side via the systemd `EnvironmentFile` — the Settings page
shows whether they are set but cannot edit them. Set the library root here too:

```bash
sudo mkdir -p /etc/videocortex
sudo tee /etc/videocortex/env >/dev/null <<'EOF'
Apify__Token=apify_api_...
Llm__ApiKey=sk-...
Library__RootPath=/home/videocortex/SecondBrain
EOF
sudo chmod 600 /etc/videocortex/env
sudo systemctl restart videocortex
```

`Library__RootPath` is not optional in practice: the app's default derives from the
.NET "My Documents" folder, and on a fresh service account (no `~/Documents`) that
resolves to an **empty string** — the root then lands relative to the working directory
(`/opt/videocortex/SecondBrain`), which the service user can't create, and the app
crash-loops at startup. Setting it explicitly also keeps it aligned with the nginx
`/library/` alias.

(On the dev machine, `dotnet user-secrets` still supplies the secrets in Development.)

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
misconfigured router can't change it. Adjust `192.168.1.0/24` to your LAN — it's the
server's IP with the last octet as `0` (server at `192.168.50.102` → `192.168.50.0/24`):

```bash
sudo ufw allow OpenSSH
sudo ufw allow from 192.168.1.0/24 to any port 80 proto tcp
sudo ufw enable
sudo ufw status verbose
```

⚠️ Enabling ufw blocks **every other service on this box** (an Ollama/llama.cpp endpoint,
Samba, etc.). Add an equivalent `allow from <subnet> to any port <port>` rule for each
one you use. Loopback traffic is unaffected, so the app reaching a local LLM on
`localhost` keeps working regardless. Always add the OpenSSH rule **before**
`ufw enable` or you'll drop your own SSH session.

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

## Troubleshooting

**502 Bad Gateway** — nginx is fine; the app isn't listening on `127.0.0.1:5000`. Check
`systemctl status videocortex`.

**Crash loop masquerading as "running"** — a startup crash takes ~6 seconds, and systemd
restarts 5 s later, so `status` often catches a freshly restarted process. The tells:
`Active: active (running) since … <a few seconds> ago` and a climbing
`restart counter is at N`. It's only healthy once the uptime passes ~15 s. Get the actual
error with:

```bash
journalctl -u videocortex -n 60 --no-pager | grep -A 8 "Unhandled exception"
```

Common causes:

- `Access to the path '/opt/videocortex/SecondBrain' is denied` → `Library__RootPath`
  missing from `/etc/videocortex/env` (see §4), or the installed unit predates the
  `EnvironmentFile` line (see §3 — `systemctl cat videocortex | grep EnvironmentFile`
  must show it uncommented).
- Permission errors under `/home/videocortex` → rerun the `chown -R` from §5.
- Config parse error at startup → malformed hand-edited
  `/home/videocortex/.videocortex/appsettings.Local.json`.

**Settings page shows keys "not set"** — same stale-unit cause as above, or a typo in
the env file (the separator is a **double underscore**: `Apify__Token`, not
`Apify:Token`; `user-secrets` does not work in production).

**Run it in the foreground** to see errors directly, bypassing the restart loop:

```bash
sudo systemctl stop videocortex
sudo -u videocortex ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5000 \
  Library__RootPath=/home/videocortex/SecondBrain /opt/videocortex/VideoCortex
```

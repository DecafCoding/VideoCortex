#!/usr/bin/env bash
# Update Video Cortex on the server: pull latest, publish over /opt/videocortex, restart.
# Run as your admin user from anywhere; assumes the layout in deploy/README.md.
set -euo pipefail

cd "$(dirname "$(readlink -f "$0")")/.."

git pull --ff-only
dotnet publish VideoCortex/VideoCortex.csproj -c Release -o /opt/videocortex
sudo systemctl restart videocortex
systemctl --no-pager --lines 5 status videocortex

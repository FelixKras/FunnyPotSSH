#!/bin/bash
set -e
echo "[INFO] user logged in via SSH"
cd /home/pi/app
exec /usr/bin/dotnet FunnyPot.dll

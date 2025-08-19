#!/bin/bash
set -e
echo "[INFO] user logged in via SSH"
cd /home/test/app
exec /usr/bin/dotnet FunnyPot.dll

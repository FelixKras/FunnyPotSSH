#!/bin/bash

set -e
# Set the SSH password (only once when container starts)
if [ "$1" == "start-sshd" ]; then
    echo "sshuser:$SSH_PASSWORD" | chpasswd
    echo "[INFO] Password set for sshuser"

    # Start SSH server in foreground
    exec /usr/sbin/sshd -D -e
fi

# Default behavior (when called via SSH session as ForceCommand)
echo "[INFO] SSH user logged in"
cd /home/sshuser/app
exec /usr/bin/dotnet FunnyPot.dll

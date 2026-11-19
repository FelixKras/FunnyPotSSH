"""Root-owned health probe: do not execute app-owned assemblies as root."""
import socket
import os
import urllib.request

try:
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    if os.environ.get("FUNNYPOT_MAINTENANCE") != "1":
        with opener.open("http://127.0.0.1:22723/health", timeout=2) as response:
            if response.status != 200:
                raise RuntimeError("SSH not ready")
    with socket.socket(socket.AF_UNIX) as connection:
        connection.settimeout(2)
        connection.connect("/run/funnypot-subscription/socket/bridge.sock")
except Exception:
    raise SystemExit(1)

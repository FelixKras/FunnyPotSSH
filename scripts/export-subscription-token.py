#!/usr/bin/env python3
"""Host-only credential refresh/export. Never run in the honeypot or bridge container."""
import argparse
import base64
import fcntl
import json
import os
from pathlib import Path
import tempfile
import time
import urllib.request
import urllib.parse

CLIENT_ID = "app_EMoamEEZ73f0CkXaXp7hrann"


def atomic_json(path, data):
    fd, name = tempfile.mkstemp(prefix=".credential-", dir=path.parent)
    try:
        with os.fdopen(fd, "w") as f:
            os.fchmod(f.fileno(), 0o600)
            json.dump(data, f)
            f.flush()
            os.fsync(f.fileno())
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def claims(token):
    try:
        encoded = token.split(".")[1]
        return json.loads(base64.urlsafe_b64decode(encoded + "=" * (-len(encoded) % 4)))
    except (ValueError, IndexError):
        return {}


def export(auth_path, output):
    os.umask(0o077)
    output.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    with open(auth_path.parent / ".funnypot-refresh.lock", "a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        original = json.loads(auth_path.read_text())
        auth = original["openai"]
        if auth.get("type") != "oauth":
            raise RuntimeError("An OpenAI OAuth login in Kilo is required")
        expiry = min(auth.get("expires", 0) / 1000, claims(auth.get("access", "")).get("exp", float("inf")))
        if not auth.get("access") or expiry <= time.time() + 120:
            request = urllib.request.Request("https://auth.openai.com/oauth/token", method="POST",
                headers={"Content-Type": "application/x-www-form-urlencoded"},
                data=urllib.parse.urlencode({"grant_type": "refresh_token", "refresh_token": auth["refresh"],
                                            "client_id": CLIENT_ID}).encode())
            # No redirects: credentials must only go to the fixed OAuth endpoint.
            class NoRedirect(urllib.request.HTTPRedirectHandler):
                def redirect_request(self, *args, **kwargs):
                    raise RuntimeError("OAuth redirect rejected")
            opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
            with opener.open(request, timeout=30) as response:
                tokens = json.load(response)
            updated = dict(auth, access=tokens["access_token"], refresh=tokens.get("refresh_token", auth["refresh"]),
                           expires=int((time.time() + tokens.get("expires_in", 3600)) * 1000))
            # Preserve other providers and avoid overwriting a concurrent Kilo refresh.
            latest = json.loads(auth_path.read_text())
            if latest.get("openai") != auth:
                raise RuntimeError("Kilo authentication changed during refresh; retry on next timer tick")
            latest["openai"] = updated
            atomic_json(auth_path, latest)
            auth = updated
        decoded = claims(auth["access"])
        account = auth.get("accountId") or decoded.get("https://api.openai.com/auth", {}).get("chatgpt_account_id")
        expires = min(auth["expires"] / 1000, decoded.get("exp", float("inf")))
        if expires <= time.time() + 15:
            raise RuntimeError("Refreshed access credential is already expired")
        atomic_json(output, {"access": auth["access"], "expires": expires, "account_id": account})
        print(f"Access credential exported (valid for {int(expires - time.time())} seconds); refresh token remains on host", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--auth", type=Path, default=Path.home() / ".local/share/kilo/auth.json")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        export(args.auth, args.output)
    except Exception:
        # No exception text: network/auth library exceptions can contain private data.
        print("Credential export failed; check host OAuth login and file permissions", flush=True)
        raise SystemExit(1)

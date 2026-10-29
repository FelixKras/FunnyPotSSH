"""Minimal PID 1: isolate credentials, drop child privileges, supervise both processes.

Only this root-owned script runs privileged. Neither network-facing process does.
The root-only mount is copied into bridge-owned tmpfs; the honeypot cannot read
either location. Token refresh itself remains on the host.
"""
import ctypes
import json
import os
from pathlib import Path
import pwd
import resource
import signal
import socket
import subprocess
import tempfile
import time

SOURCE = Path("/run/host-subscription/access/access.json")
RUNTIME = Path("/run/funnypot-subscription")
PRIVATE = RUNTIME / "credentials"
SOCKET_DIR = RUNTIME / "socket"
stopping = False


def private_directory(path, uid, gid, mode):
    path.mkdir(exist_ok=True)
    if path.is_symlink():
        raise RuntimeError("Runtime directory must not be a symlink")
    # With no CAP_FOWNER, chmod must happen while the supervisor owns the inode.
    os.chown(path, 0, 0)
    os.chmod(path, mode)
    os.chown(path, uid, gid)


def copy_access(uid, gid):
    with SOURCE.open("rb") as f:
        raw = f.read(65537)
    if len(raw) > 65536:
        raise RuntimeError("Credential export too large")
    auth = json.loads(raw)
    if set(auth) != {"access", "expires", "account_id"} or not isinstance(auth["access"], str):
        raise RuntimeError("Only access-only exports are accepted")
    fd, name = tempfile.mkstemp(prefix=".access-", dir=PRIVATE)
    try:
        with os.fdopen(fd, "w") as f:
            os.fchmod(f.fileno(), 0o600)
            os.fchown(f.fileno(), uid, gid)
            json.dump(auth, f)
        os.replace(name, PRIVATE / "access.json")
    finally:
        if os.path.exists(name):
            os.unlink(name)


def reap(children):
    for child in children:
        child.poll()
    # PID 1 must also collect any orphaned descendants.
    while True:
        try:
            pid, status = os.waitpid(-1, os.WNOHANG)
        except ChildProcessError:
            break
        if not pid:
            break
        for child in children:
            if child.pid == pid:
                child.returncode = os.waitstatus_to_exitcode(status)


def stop_children(children):
    for child in children:
        try:
            os.killpg(child.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        reap(children)
        if all(child.returncode is not None for child in children):
            return
        time.sleep(0.1)
    for child in children:
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    for child in children:
        child.wait()


def run():
    if os.geteuid() != 0:
        raise RuntimeError("The supervisor must start as root to separate child UIDs")
    os.umask(0o077)
    resource.setrlimit(resource.RLIMIT_CORE, (0, 0))
    # Disable dumps and prevent either child gaining privileges via exec.
    libc = ctypes.CDLL(None, use_errno=True)
    if libc.prctl(4, 0, 0, 0, 0) or libc.prctl(38, 1, 0, 0, 0):
        raise RuntimeError("Cannot enforce supervisor process protections")
    app = pwd.getpwnam(os.environ.get("FUNNYPOT_RUN_USER", "test"))
    bridge = pwd.getpwnam("subscription-bridge")
    if app.pw_uid == bridge.pw_uid or app.pw_uid == 0 or bridge.pw_uid == 0:
        raise RuntimeError("Application and bridge must have distinct nonroot UIDs")
    protected = Path("/run/host-subscription")
    if protected.stat().st_uid != 0 or protected.stat().st_mode & 0o777 != 0o700:
        raise RuntimeError("Credential mount parent must be root-owned mode 0700")
    RUNTIME.mkdir(exist_ok=True)
    private_directory(PRIVATE, bridge.pw_uid, app.pw_gid, 0o700)
    private_directory(SOCKET_DIR, bridge.pw_uid, app.pw_gid, 0o750)
    copy_access(bridge.pw_uid, app.pw_gid)

    def stop(signum, frame):
        global stopping
        stopping = True
    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    children = []
    failed = False
    try:
        # Deliberately do not inherit app/GitHub credentials into the bridge.
        env = {"PATH": "/usr/bin:/bin", "PYTHONUNBUFFERED": "1",
               "BRIDGE_SOCKET": str(SOCKET_DIR / "bridge.sock"),
               "ACCESS_TOKEN_FILE": str(PRIVATE / "access.json"),
               "SUBSCRIPTION_MODEL": "gpt-6.1-terra"}
        children.append(subprocess.Popen(["python3", "-I", "-B", "/opt/subscription-bridge/bridge.py"],
            user=bridge.pw_uid, group=app.pw_gid, extra_groups=[], env=env, start_new_session=True,
            cwd="/opt/subscription-bridge"))
        deadline = time.monotonic() + 15
        while not stopping and time.monotonic() < deadline:
            reap(children)
            if children[0].returncode is not None:
                raise RuntimeError("Bridge exited during startup")
            try:
                with socket.socket(socket.AF_UNIX) as connection:
                    connection.settimeout(1)
                    connection.connect(env["BRIDGE_SOCKET"])
                break
            except OSError:
                time.sleep(0.1)
        else:
            raise RuntimeError("Bridge startup timed out")
        children.append(subprocess.Popen(["dotnet", "FunnyPot.dll"], user=app.pw_uid,
            group=app.pw_gid, extra_groups=[], start_new_session=True))
        print("FunnyPot and subscription bridge running as separate nonroot users", flush=True)
        next_copy = time.monotonic() + 30
        while not stopping:
            reap(children)
            if any(child.returncode is not None for child in children):
                failed = True
                break
            if time.monotonic() >= next_copy:
                try:
                    copy_access(bridge.pw_uid, app.pw_gid)
                except Exception:
                    print("Access export sync failed; retaining last access credential", flush=True)
                next_copy = time.monotonic() + 30
            time.sleep(0.25)
    finally:
        stop_children(children)
    return 1 if failed else 0


if __name__ == "__main__":
    try:
        result = run()
    except Exception as error:
        print(f"Single-container supervisor failed ({type(error).__name__}, errno={getattr(error, 'errno', None)}); check configuration and permissions", flush=True)
        result = 1
    raise SystemExit(result)

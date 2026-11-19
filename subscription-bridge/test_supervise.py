import json
import os
from pathlib import Path
import tempfile
import subprocess
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch, Mock, call
import supervise


class SupervisorTests(unittest.TestCase):
    def test_app_environment_matches_user_and_preserves_app_configuration(self):
        app = SimpleNamespace(pw_dir="/home/test", pw_name="test", pw_shell="/bin/bash")
        inherited = {"HOME": "/root", "USER": "root", "LOGNAME": "root",
                     "XDG_CONFIG_HOME": "/root/.config", "XDG_RUNTIME_DIR": "/run/user/0",
                     "DOTNET_CLI_HOME": "/root", "GITHUB_TOKEN": "fake-publication-token",
                     "LLM_PROVIDER": "subscription-bridge"}
        env = supervise.application_environment(app, inherited)
        self.assertEqual("/home/test", env["HOME"])
        self.assertEqual("test", env["USER"])
        self.assertEqual("test", env["LOGNAME"])
        self.assertEqual("/bin/bash", env["SHELL"])
        self.assertEqual("/home/test/.config", env["XDG_CONFIG_HOME"])
        self.assertEqual("/home/test/.cache", env["XDG_CACHE_HOME"])
        self.assertEqual("/home/test/.local/share", env["XDG_DATA_HOME"])
        self.assertEqual("/home/test/.local/state", env["XDG_STATE_HOME"])
        self.assertEqual("/home/test", env["DOTNET_CLI_HOME"])
        self.assertNotIn("XDG_RUNTIME_DIR", env)
        self.assertEqual("fake-publication-token", env["GITHUB_TOKEN"])
        self.assertEqual("subscription-bridge", env["LLM_PROVIDER"])
        self.assertEqual("/root", inherited["HOME"])

    def test_real_child_receives_correct_home(self):
        app = SimpleNamespace(pw_dir="/home/test", pw_name="test", pw_shell="/bin/bash")
        output = subprocess.check_output([sys.executable, "-I", "-c",
            "import os; print(os.environ['HOME']); print(os.environ['XDG_CONFIG_HOME'])"],
            env=supervise.application_environment(app, {"HOME": "/root"}), text=True)
        self.assertEqual(["/home/test", "/home/test/.config"], output.splitlines())

    def test_permissions_set_while_root_owns_directory_without_fowner_capability(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "private"
            order = Mock()
            with patch.object(supervise.os, "chown") as chown, patch.object(supervise.os, "chmod") as chmod:
                order.attach_mock(chown, "chown")
                order.attach_mock(chmod, "chmod")
                supervise.private_directory(path, 20001, 1000, 0o700)
            self.assertEqual([call.chown(path, 0, 0), call.chmod(path, 0o700), call.chown(path, 20001, 1000)], order.mock_calls)

    def test_access_copy_is_atomic_private_and_access_only(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source.json"
            private = Path(directory) / "private"
            private.mkdir(mode=0o700)
            source.write_text(json.dumps({"access": "fake-access", "expires": 9999999999, "account_id": "test"}))
            with patch.object(supervise, "SOURCE", source), patch.object(supervise, "PRIVATE", private):
                supervise.copy_access(os.getuid(), os.getgid())
            output = private / "access.json"
            self.assertEqual(json.loads(source.read_text()), json.loads(output.read_text()))
            self.assertEqual(0o600, output.stat().st_mode & 0o777)
            self.assertEqual(["access.json"], [p.name for p in private.iterdir()])

    def test_refresh_token_is_rejected_not_copied(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source.json"
            private = Path(directory) / "private"
            private.mkdir()
            source.write_text(json.dumps({"access": "fake-access", "refresh": "fake-refresh", "expires": 1, "account_id": None}))
            with patch.object(supervise, "SOURCE", source), patch.object(supervise, "PRIVATE", private), self.assertRaises(RuntimeError):
                supervise.copy_access(os.getuid(), os.getgid())
            self.assertFalse((private / "access.json").exists())

    def test_oversized_export_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source.json"
            source.write_bytes(b"x" * 65537)
            with patch.object(supervise, "SOURCE", source), self.assertRaises(RuntimeError):
                supervise.copy_access(os.getuid(), os.getgid())


if __name__ == "__main__":
    unittest.main()

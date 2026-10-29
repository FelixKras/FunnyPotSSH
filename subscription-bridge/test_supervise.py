import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch, Mock, call
import supervise


class SupervisorTests(unittest.TestCase):
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

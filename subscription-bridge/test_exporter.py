import base64
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("exporter", Path(__file__).parent.parent / "scripts/export-subscription-token.py")
exporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exporter)


def token(exp):
    return "test." + base64.urlsafe_b64encode(json.dumps({"exp": exp}).encode()).decode().rstrip("=") + ".test"


class ExporterTests(unittest.TestCase):
    def test_export_never_includes_refresh_or_other_providers(self):
        with tempfile.TemporaryDirectory() as directory:
            auth = Path(directory) / "auth.json"
            data = {"openai": {"type": "oauth", "access": token(time.time() + 3600),
                "refresh": "fake-refresh-secret", "expires": (time.time() + 3600) * 1000},
                "other": {"key": "other-provider-secret"}}
            auth.write_text(json.dumps(data))
            output = Path(directory) / "access/access.json"
            exporter.export(auth, output)
            exported = json.loads(output.read_text())
            self.assertEqual({"access", "expires", "account_id"}, set(exported))
            self.assertNotIn("fake-refresh-secret", output.read_text())
            self.assertNotIn("other-provider-secret", output.read_text())
            self.assertEqual(0o600, output.stat().st_mode & 0o777)
            self.assertEqual(0o700, output.parent.stat().st_mode & 0o777)
            self.assertEqual(data, json.loads(auth.read_text()))

    def test_refresh_updates_only_openai_and_preserves_rotated_refresh_on_host(self):
        with tempfile.TemporaryDirectory() as directory:
            auth = Path(directory) / "auth.json"
            auth.write_text(json.dumps({"openai": {"type": "oauth", "access": "expired", "refresh": "old-refresh",
                "expires": 0}, "other": {"key": "unchanged"}}))
            new_access = token(time.time() + 3600)
            response = io.BytesIO(json.dumps({"access_token": new_access, "refresh_token": "rotated-refresh",
                "expires_in": 3600}).encode())
            with patch.object(exporter.urllib.request, "build_opener") as build:
                build.return_value.open.return_value = response
                output = Path(directory) / "access/access.json"
                exporter.export(auth, output)
                request = build.return_value.open.call_args.args[0]
                self.assertEqual("https://auth.openai.com/oauth/token", request.full_url)
            updated = json.loads(auth.read_text())
            self.assertEqual("rotated-refresh", updated["openai"]["refresh"])
            self.assertEqual({"key": "unchanged"}, updated["other"])
            self.assertNotIn("rotated-refresh", output.read_text())
            self.assertEqual(0o600, auth.stat().st_mode & 0o777)

    def test_no_export_for_api_key_auth(self):
        with tempfile.TemporaryDirectory() as directory:
            auth = Path(directory) / "auth.json"
            auth.write_text(json.dumps({"openai": {"type": "api", "key": "not-oauth"}}))
            output = Path(directory) / "access/access.json"
            with self.assertRaises(RuntimeError):
                exporter.export(auth, output)
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()

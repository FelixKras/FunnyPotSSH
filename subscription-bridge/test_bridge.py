import io
import json
import unittest
import tempfile
import os
import time
import urllib.error
from unittest.mock import patch
import bridge


def event(kind, **body):
    return ("data: " + json.dumps(dict(type=kind, **body)) + "\n\n").encode()


class BridgeTests(unittest.TestCase):
    def test_roles_are_preserved_and_tools_disabled(self):
        result = bridge.translate({"messages": [{"role": "system", "content": "persona"},
            {"role": "user", "content": "pwd"}, {"role": "assistant", "content": "/root"},
            {"role": "user", "content": "uname -s"}]})
        self.assertFalse(result["store"])
        self.assertTrue(result["stream"])
        self.assertEqual([], result["tools"])
        self.assertEqual("none", result["tool_choice"])
        self.assertIn("persona", result["instructions"])
        self.assertEqual("output_text", result["input"][1]["content"][0]["type"])

    def test_rejects_arbitrary_options_and_nontext_input(self):
        for body in ({"messages": [], "url": "https://evil.test"},
                     {"messages": [{"role": "tool", "content": "x"}]},
                     {"messages": [{"role": "user", "content": []}]},
                     {"messages": [{"role": "assistant", "content": "x"}]},
                     {"messages": [{"role": "user", "content": "x", "tools": []}]}):
            with self.subTest(body=body), self.assertRaises(bridge.BridgeError):
                bridge.translate(body)

    def test_completed_stream_ignores_reasoning_and_counts_usage(self):
        data = event("response.created") + event("response.completed", response={
            "status": "completed", "output": [{"type": "reasoning", "summary": []},
                {"type": "message", "role": "assistant", "content": [{"type": "output_text", "text": "Linux\n"}]}],
            "usage": {"input_tokens": 12, "output_tokens": 3, "total_tokens": 15}})
        result = bridge.parse_response(io.BytesIO(data))
        self.assertEqual("Linux\n", result["text"])
        self.assertEqual(15, result["usage"]["total_tokens"])

    def test_valid_empty_text_preserved(self):
        data = event("response.completed", response={"status": "completed", "output": [
            {"type": "message", "role": "assistant", "content": [{"type": "output_text", "text": ""}]}]})
        self.assertEqual("", bridge.parse_response(io.BytesIO(data))["text"])

    def test_failed_and_truncated_streams_rejected(self):
        for data in (b"", event("response.created"), event("response.incomplete"), event("error"),
                     event("response.failed"), event("response.completed", response={"status": "incomplete"})):
            with self.subTest(data=data), self.assertRaises(bridge.BridgeError):
                bridge.parse_response(io.BytesIO(data))

    def test_tool_and_refusal_output_rejected(self):
        for output in ([{"type": "function_call", "name": "bash"}],
                       [{"type": "message", "role": "assistant", "content": [{"type": "refusal", "refusal": "no"}]}]):
            data = event("response.completed", response={"status": "completed", "output": output})
            with self.assertRaises(bridge.BridgeError):
                bridge.parse_response(io.BytesIO(data))

    def test_response_size_bounded(self):
        with patch.object(bridge, "MAX_RESPONSE", 10), self.assertRaises(bridge.BridgeError):
            bridge.parse_response(io.BytesIO(event("response.created")))

    def test_redirects_rejected(self):
        with self.assertRaises(bridge.BridgeError):
            bridge.NoRedirect().redirect_request(None, None, 307, "redirect", {}, "https://evil.test")

    def test_expired_access_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "access.json")
            with open(path, "w") as f:
                json.dump({"access": "fake-access-secret", "expires": 0}, f)
            with patch.dict(os.environ, ACCESS_TOKEN_FILE=path), self.assertRaises(bridge.BridgeError) as error:
                bridge.request_completion({"messages": [{"role": "user", "content": "pwd"}]})
            self.assertEqual(503, error.exception.status)
            self.assertNotIn("fake-access-secret", error.exception.message)

    def test_upstream_auth_errors_do_not_expose_headers_or_body(self):
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "access.json")
            with open(path, "w") as f:
                json.dump({"access": "fake-access-secret", "expires": time.time() + 3600}, f)
            with patch.dict(os.environ, ACCESS_TOKEN_FILE=path), patch.object(bridge.urllib.request, "build_opener") as build:
                build.return_value.open.side_effect = urllib.error.HTTPError(bridge.ENDPOINT, 401,
                    "sensitive upstream text", {}, io.BytesIO(b"fake-access-secret"))
                with self.assertRaises(bridge.BridgeError) as error:
                    bridge.request_completion({"messages": [{"role": "user", "content": "pwd"}]})
                self.assertEqual(503, error.exception.status)
                self.assertNotIn("secret", error.exception.message)
                self.assertNotIn("sensitive", error.exception.message)


if __name__ == "__main__":
    unittest.main()

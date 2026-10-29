"""Text-only subscription transport. No agents, tools, token refresh, or TCP listener."""
import http.server
import json
import os
import socketserver
import threading
import time
import urllib.error
import urllib.request

ENDPOINT = "https://chatgpt.com/backend-api/codex/responses"
MAX_BODY = 1024 * 1024
MAX_RESPONSE = 2 * 1024 * 1024
MODEL = os.environ.get("SUBSCRIPTION_MODEL", "gpt-6.1-terra")
SLOTS = threading.BoundedSemaphore(2)


class BridgeError(Exception):
    def __init__(self, status, message):
        self.status, self.message = status, message


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        # Never forward an access token to another endpoint.
        raise BridgeError(502, "Upstream redirect rejected")


def translate(body, model=MODEL):
    if not isinstance(body, dict) or set(body) - {"messages"}:
        raise BridgeError(400, "Only messages are accepted")
    messages = body.get("messages")
    if not isinstance(messages, list) or not 1 <= len(messages) <= 128:
        raise BridgeError(400, "Invalid message list")
    instructions, transcript = [], []
    for msg in messages:
        if not isinstance(msg, dict) or set(msg) != {"role", "content"}:
            raise BridgeError(400, "Invalid message")
        role, text = msg["role"], msg["content"]
        if role not in ("system", "user", "assistant") or not isinstance(text, str):
            raise BridgeError(400, "Only text conversation messages are accepted")
        if role == "system":
            instructions.append(text)
        else:
            kind = "output_text" if role == "assistant" else "input_text"
            transcript.append({"role": role, "content": [{"type": kind, "text": text}]})
    if not transcript or transcript[-1]["role"] != "user":
        raise BridgeError(400, "Conversation must end with a user command")
    instructions.append("Simulate terminal output only. Never execute commands, use tools, or access real files.")
    return {"model": model, "instructions": "\n".join(instructions), "input": transcript,
            "store": False, "stream": True, "tools": [], "tool_choice": "none"}


def parse_response(stream):
    size = 0
    deadline = time.monotonic() + 45
    event_lines = []
    for line in stream:
        if time.monotonic() > deadline:
            raise BridgeError(504, "Subscription generation deadline exceeded")
        size += len(line)
        if size > MAX_RESPONSE:
            raise BridgeError(502, "Upstream response exceeded limit")
        if line.strip():
            if line.startswith(b"data:"):
                event_lines.append(line[5:].strip())
            continue
        if not event_lines:
            continue
        data = b"\n".join(event_lines)
        event_lines = []
        if data == b"[DONE]":
            continue
        event = json.loads(data)
        if event.get("type") in ("error", "response.failed", "response.incomplete"):
            raise BridgeError(502, "Subscription response failed or incomplete")
        if event.get("type") != "response.completed":
            continue
        response = event.get("response", {})
        if response.get("status") != "completed" or response.get("error"):
            raise BridgeError(502, "Subscription response failed or incomplete")
        output = []
        for item in response.get("output", []):
            if item.get("type") == "reasoning":
                continue
            if item.get("type") != "message" or item.get("role") != "assistant":
                raise BridgeError(502, "Non-text output rejected")
            for part in item.get("content", []):
                if part.get("type") != "output_text" or not isinstance(part.get("text"), str):
                    raise BridgeError(502, "Non-text output rejected")
                output.append(part["text"])
        if not output:
            raise BridgeError(502, "Missing terminal response")
        usage = response.get("usage") or {}
        return {"text": "".join(output), "model": "openai/" + MODEL,
                "usage": {k: int(usage.get(k, 0)) for k in ("input_tokens", "output_tokens", "total_tokens")}}
    raise BridgeError(502, "Incomplete subscription stream")


def request_completion(body):
    payload = translate(body)
    # Only an access token is mounted here. The refresh token stays on the host.
    # Access-token lifetime is controlled by OpenAI and may span days.
    with open(os.environ.get("ACCESS_TOKEN_FILE", "/run/credentials/access.json"), encoding="utf-8") as f:
        auth = json.load(f)
    if auth.get("expires", 0) <= time.time() + 15:
        raise BridgeError(503, "Subscription credential expired; host exporter must refresh it")
    headers = {"Authorization": "Bearer " + auth["access"], "Content-Type": "application/json",
               "Accept": "text/event-stream", "originator": "opencode", "User-Agent": "FunnyPot-subscription-bridge/1"}
    if auth.get("account_id"):
        headers["ChatGPT-Account-Id"] = auth["account_id"]
    request = urllib.request.Request(ENDPOINT, data=json.dumps(payload).encode(), headers=headers, method="POST")
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    try:
        with opener.open(request, timeout=45) as response:
            return parse_response(response)
    except urllib.error.HTTPError as ex:
        print(f"Subscription upstream HTTP status: {ex.code}", flush=True)
        # Upstream bodies and response headers may contain private information.
        if ex.code in (401, 403):
            raise BridgeError(503, "Subscription authentication unavailable") from None
        if ex.code == 429:
            raise BridgeError(429, "Subscription rate limit reached") from None
        raise BridgeError(502, "Subscription upstream request failed") from None


class Handler(http.server.BaseHTTPRequestHandler):
    server_version = "FunnyPotBridge"

    def setup(self):
        super().setup()
        self.connection.settimeout(55)

    def log_message(self, *args):
        pass  # Never log requests, headers, model output, or credentials.

    def reply(self, status, body):
        if status >= 400:
            print(f"Bridge request failed (HTTP {status})", flush=True)
        data = json.dumps(body).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_POST(self):
        acquired = False
        try:
            if self.path != "/v1/terminal":
                raise BridgeError(404, "Not found")
            if self.headers.get("Transfer-Encoding"):
                raise BridgeError(400, "Transfer encoding rejected")
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= MAX_BODY:
                raise BridgeError(413, "Invalid request size")
            acquired = SLOTS.acquire(blocking=False)
            if not acquired:
                raise BridgeError(429, "Bridge concurrency limit reached")
            self.reply(200, request_completion(json.loads(self.rfile.read(length))))
        except BridgeError as ex:
            self.reply(ex.status, {"error": ex.message})
        except (ValueError, TypeError):
            self.reply(400, {"error": "Invalid request"})
        except Exception:
            self.reply(502, {"error": "Subscription bridge unavailable"})
        finally:
            if acquired:
                SLOTS.release()


class Server(socketserver.ThreadingMixIn, socketserver.UnixStreamServer):
    daemon_threads = True
    request_queue_size = 8
    connections = threading.BoundedSemaphore(8)

    def process_request(self, request, client_address):
        if not self.connections.acquire(blocking=False):
            request.close()
            return
        try:
            super().process_request(request, client_address)
        except Exception:
            self.connections.release()
            raise

    def process_request_thread(self, request, client_address):
        try:
            super().process_request_thread(request, client_address)
        finally:
            self.connections.release()

    def handle_error(self, request, client_address):
        # Standard socketserver tracebacks can expose request and local context.
        print("Bridge connection failed", flush=True)


if __name__ == "__main__":
    path = os.environ.get("BRIDGE_SOCKET", "/run/bridge/bridge.sock")
    os.umask(0o077)
    if os.path.exists(path):
        os.unlink(path)
    with Server(path, Handler) as server:
        # The bridge UID owns the socket; the honeypot's group can connect only.
        os.chmod(path, 0o660)
        print("Text-only subscription bridge ready (Unix socket)", flush=True)
        server.serve_forever()

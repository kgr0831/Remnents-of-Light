"""
Voice-to-prompt web UI. Runs on the laptop next to executor.py, exposed to the
phone via a Cloudflare Tunnel (cloudflared) rather than any cloud host - avoids
new hosting cost and lets the cleanup step reuse the existing `claude` CLI
subscription instead of a separate Anthropic API key.

Flow: phone browser records speech (Web Speech API, client-side) -> raw
transcript POSTed here -> Haiku cleans it up via `claude -p` -> shown back to
the user to review/edit -> on submit, posted to #claude-queue exactly like any
other CMD: loop, so it goes through the same FIFO/approval/video-report
pipeline as everything else.

Usage:
  tools/.venv/Scripts/python.exe tools/voice_web.py
  (separately, to expose it) cloudflared tunnel --url http://localhost:8765
"""
import json
import secrets
from pathlib import Path

from aiohttp import web

import claude_bridge
import discord_bot

PORT = 8765
SECRETS_PATH = Path(__file__).parent / ".secrets" / "voice_config.json"
HTML_PATH = Path(__file__).parent / "voice_page.html"

CLEAN_PROMPT = """\
다음은 사용자가 음성으로 두서없이 말한 개발 작업 지시다. 필러 단어·중복·말버릇을 걷어내고,
핵심 요구사항만 남긴 명확하고 간결한 작업 지시문으로 정리해라. 정리된 지시문만 출력해라 -
설명이나 인사말은 붙이지 마라.

원본: {transcript}
"""


def load_or_create_password() -> str:
    if SECRETS_PATH.exists():
        return json.loads(SECRETS_PATH.read_text(encoding="utf-8"))["password"]
    password = secrets.token_urlsafe(12)
    SECRETS_PATH.write_text(json.dumps({"password": password}), encoding="utf-8")
    return password


PASSWORD = load_or_create_password()


def _authorized(request: web.Request) -> bool:
    return request.headers.get("X-Voice-Password") == PASSWORD


async def handle_index(request: web.Request) -> web.Response:
    return web.Response(text=HTML_PATH.read_text(encoding="utf-8"), content_type="text/html")


async def handle_clean(request: web.Request) -> web.Response:
    if not _authorized(request):
        return web.json_response({"error": "unauthorized"}, status=401)
    data = await request.json()
    transcript = (data.get("transcript") or "").strip()
    if not transcript:
        return web.json_response({"error": "빈 내용"}, status=400)
    result = await claude_bridge.run_claude(
        CLEAN_PROMPT.format(transcript=transcript),
        allowed_tools=[], session_id=None, timeout=60,
        model="claude-haiku-4-5-20251001",
    )
    return web.json_response({"cleaned": result["text"].strip()})


async def handle_submit(request: web.Request) -> web.Response:
    if not _authorized(request):
        return web.json_response({"error": "unauthorized"}, status=401)
    data = await request.json()
    text = (data.get("text") or "").strip()
    if not text:
        return web.json_response({"error": "빈 내용"}, status=400)
    config = discord_bot.load_config()
    q_id = discord_bot.get_or_create_channel("personal", "claude-queue", config)
    cmd = {"type": "loop", "text": text}
    discord_bot.send_message(q_id, f"CMD: {json.dumps(cmd, ensure_ascii=False)}", config)
    return web.json_response({"ok": True})


def main() -> None:
    print(f"voice web running on http://127.0.0.1:{PORT}  (password: {PASSWORD})")
    app = web.Application()
    app.router.add_get("/", handle_index)
    app.router.add_post("/api/clean", handle_clean)
    app.router.add_post("/api/submit", handle_submit)
    web.run_app(app, host="127.0.0.1", port=PORT)


if __name__ == "__main__":
    main()

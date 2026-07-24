"""
Discord REST API client for the bot used by report.py / inbox.py (SETUP_PLAN STEP5).
No gateway/websocket connection, no resident process - plain REST calls run on demand
(session start, or whenever report/inbox is invoked). Config + token live in
tools/.secrets/discord_config.json (gitignored).

Source: https://discord.com/developers/docs/reference (REST base URL, auth header format;
checked 2026-07-20)
"""
import json
import time
import urllib.error
import urllib.request
from pathlib import Path

API_BASE = "https://discord.com/api/v10"
CONFIG_PATH = Path(__file__).parent / ".secrets" / "discord_config.json"
REQUEST_TIMEOUT_S = 15

CHANNEL_TYPE_TEXT = 0
CHANNEL_TYPE_FORUM = 15


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(
            f"{CONFIG_PATH} not found. Create it with: "
            '{"token": "...", "guilds": {"personal": "...", "team": "..."}, "channels": {}}'
        )
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))


def save_config(config: dict) -> None:
    CONFIG_PATH.write_text(json.dumps(config, ensure_ascii=False, indent=2), encoding="utf-8")


def _request(method: str, path: str, token: str, body: dict | None = None) -> dict | list:
    url = f"{API_BASE}{path}"
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Bot {token}")
    req.add_header("Content-Type", "application/json")
    req.add_header("User-Agent", "RemnentsOfLight-DevTools (local, 1.0)")

    for attempt in range(2):
        try:
            with urllib.request.urlopen(req, timeout=REQUEST_TIMEOUT_S) as resp:
                raw = resp.read()
                return json.loads(raw) if raw else {}
        except urllib.error.HTTPError as e:
            if e.code == 429 and attempt == 0:
                retry_after = 1.0
                try:
                    retry_after = json.loads(e.read()).get("retry_after", 1.0)
                except Exception:
                    pass
                time.sleep(retry_after)
                continue
            detail = e.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"Discord API {method} {path} -> {e.code}: {detail}") from e
    raise RuntimeError(f"Discord API {method} {path}: rate limited twice, giving up")


def list_channels(guild_id: str, token: str) -> list:
    return _request("GET", f"/guilds/{guild_id}/channels", token)


def get_or_create_channel(guild_key: str, name: str, config: dict, channel_type: int = CHANNEL_TYPE_TEXT) -> str:
    """guild_key is 'personal' or 'team'. Caches result in config['channels'][f'{guild_key}:{name}']."""
    token = config["token"]
    guild_id = config["guilds"][guild_key]
    cache_key = f"{guild_key}:{name}"

    cached = config.get("channels", {}).get(cache_key)
    if cached:
        return cached

    for ch in list_channels(guild_id, token):
        if ch.get("name") == name and ch.get("type") == channel_type:
            config.setdefault("channels", {})[cache_key] = ch["id"]
            save_config(config)
            return ch["id"]

    created = _request("POST", f"/guilds/{guild_id}/channels", token, {"name": name, "type": channel_type})
    config.setdefault("channels", {})[cache_key] = created["id"]
    save_config(config)
    return created["id"]


def send_message(channel_id: str, content: str, config: dict) -> dict:
    return _request("POST", f"/channels/{channel_id}/messages", config["token"], {"content": content})


def create_forum_post(forum_channel_id: str, title: str, content: str, config: dict) -> dict:
    return _request(
        "POST",
        f"/channels/{forum_channel_id}/threads",
        config["token"],
        {"name": title, "message": {"content": content}},
    )


def get_recent_messages(channel_id: str, config: dict, limit: int = 50) -> list:
    return _request("GET", f"/channels/{channel_id}/messages?limit={limit}", config["token"])


def edit_message(channel_id: str, message_id: str, content: str, config: dict) -> dict:
    return _request("PATCH", f"/channels/{channel_id}/messages/{message_id}", config["token"], {"content": content})


def send_chunked(channel_id: str, text: str, config: dict) -> None:
    text = text or "(empty response)"
    for i in range(0, len(text), 1900):
        send_message(channel_id, text[i:i + 1900], config)


def send_embed(channel_id: str, embed: dict, config: dict) -> dict:
    return _request("POST", f"/channels/{channel_id}/messages", config["token"], {"embeds": [embed]})


def edit_embed(channel_id: str, message_id: str, embed: dict, config: dict) -> dict:
    return _request("PATCH", f"/channels/{channel_id}/messages/{message_id}", config["token"], {"embeds": [embed]})


def make_button_row(buttons: list[tuple[str, str]]) -> list[dict]:
    """buttons: list of (label, custom_id), max 5. Returns a components array
    (one action row) for the REST message-create/edit body's "components" field."""
    return [{
        "type": 1,
        "components": [{"type": 2, "style": 1, "label": label[:80], "custom_id": custom_id[:100]} for label, custom_id in buttons[:5]],
    }]


def send_embed_with_components(channel_id: str, embed: dict, components: list[dict], config: dict) -> dict:
    return _request("POST", f"/channels/{channel_id}/messages", config["token"], {"embeds": [embed], "components": components})


def trigger_typing(channel_id: str, config: dict) -> None:
    """POSTs the native Discord 'X is typing...' indicator (lasts ~10s per call)."""
    _request("POST", f"/channels/{channel_id}/typing", config["token"])


def make_embed(title: str, description: str, color: int, fields: list | None = None) -> dict:
    embed = {"title": title, "description": description[:4096], "color": color}
    if fields:
        embed["fields"] = [{"name": n, "value": v[:1024], "inline": False} for n, v in fields]
    return embed

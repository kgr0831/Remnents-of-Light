"""
Pull recent messages from a Discord channel into a local jsonl file (SETUP_PLAN STEP5).
Run on demand (e.g. session start) - no resident process, no gateway connection.

Usage:
    python tools/inbox.py [--server personal|team] [--channel NAME] [--limit 50]

Appends only new messages (deduped by Discord message id) to
tools/inbox/<server>_<channel>.jsonl, oldest-first, and prints the new ones.
"""
import argparse
import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

sys.path.insert(0, str(Path(__file__).parent))
import discord_bot  # noqa: E402

INBOX_DIR = Path(__file__).parent / "inbox"


def main() -> None:
    parser = argparse.ArgumentParser(description="Pull recent Discord messages into a local jsonl inbox")
    parser.add_argument("--server", choices=["personal", "team"], default="personal")
    parser.add_argument("--channel", default="claude-reports")
    parser.add_argument("--limit", type=int, default=50)
    args = parser.parse_args()

    config = discord_bot.load_config()
    channel_id = discord_bot.get_or_create_channel(args.server, args.channel, config)
    messages = discord_bot.get_recent_messages(channel_id, config, limit=args.limit)

    INBOX_DIR.mkdir(exist_ok=True)
    out_path = INBOX_DIR / f"{args.server}_{args.channel}.jsonl"

    seen_ids = set()
    if out_path.exists():
        for line in out_path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                seen_ids.add(json.loads(line)["id"])

    new_entries = []
    for m in reversed(messages):  # API returns newest-first; write oldest-first
        if m["id"] in seen_ids:
            continue
        new_entries.append({
            "id": m["id"],
            "author": m.get("author", {}).get("username", "unknown"),
            "content": m.get("content", ""),
            "ts": m.get("timestamp", ""),
        })

    if new_entries:
        with out_path.open("a", encoding="utf-8") as f:
            for entry in new_entries:
                f.write(json.dumps(entry, ensure_ascii=False) + "\n")

    print(f"{len(new_entries)} new message(s) from {args.server}/#{args.channel}")
    for entry in new_entries:
        print(f"  [{entry['author']}] {entry['content']}")


if __name__ == "__main__":
    main()

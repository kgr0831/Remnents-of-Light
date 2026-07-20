"""
Push a report/message to Discord (SETUP_PLAN STEP5). Run on demand, no resident process.

Usage:
    python tools/report.py "message text" [--server personal|team] [--channel NAME] [--forum]

Auto-creates the channel (text or forum) if it doesn't exist yet and caches its ID
in tools/.secrets/discord_config.json.
"""
import argparse
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

sys.path.insert(0, str(Path(__file__).parent))
import discord_bot  # noqa: E402


def main() -> None:
    parser = argparse.ArgumentParser(description="Push a message to Discord")
    parser.add_argument("message", help="Message text to send")
    parser.add_argument("--server", choices=["personal", "team"], default="personal")
    parser.add_argument("--channel", default="claude-reports")
    parser.add_argument("--forum", action="store_true", help="Post as a new forum thread instead of a text message")
    args = parser.parse_args()

    config = discord_bot.load_config()
    channel_type = discord_bot.CHANNEL_TYPE_FORUM if args.forum else discord_bot.CHANNEL_TYPE_TEXT
    channel_id = discord_bot.get_or_create_channel(args.server, args.channel, config, channel_type)

    if args.forum:
        title = args.message.splitlines()[0][:80] or "report"
        discord_bot.create_forum_post(channel_id, title, args.message, config)
    else:
        discord_bot.send_message(channel_id, args.message, config)

    print(f"Sent to {args.server}/#{args.channel}")


if __name__ == "__main__":
    main()

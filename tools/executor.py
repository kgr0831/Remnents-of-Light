"""
Laptop-side component of the Discord -> Claude Code bridge (SETUP_PLAN STEP5 ext).
Runs on the dev machine, next to the Unity project. Polls the internal queue
channel over REST (no gateway needed here - only relay_bot.py, deployed
elsewhere, needs the real-time connection) every few seconds, executes commands
via headless `claude -p`, and posts results + a periodic heartbeat.

Usage: tools/.venv/Scripts/python.exe tools/executor.py
"""
import asyncio
import json
from pathlib import Path

import claude_bridge
import discord_bot

STATE_PATH = Path(__file__).parent / ".secrets" / "executor_state.json"
POLL_INTERVAL_S = 3
HEARTBEAT_INTERVAL_S = 60


def load_state() -> dict:
    if STATE_PATH.exists():
        return json.loads(STATE_PATH.read_text(encoding="utf-8"))
    return {"last_queue_msg_id": None}


def save_state(state: dict) -> None:
    STATE_PATH.write_text(json.dumps(state), encoding="utf-8")


COLOR_INFO = 0x5865F2
COLOR_APPROVAL = 0xF5A623
COLOR_DONE = 0x57F287


async def handle_command(cmd: dict, config: dict, command_channel_id: str, queue_channel_id: str) -> None:
    cmd_type = cmd["type"]
    if cmd_type == "quick":
        result = await claude_bridge.run_claude(cmd["text"], claude_bridge.QUICK_ALLOWED_TOOLS, None, timeout=180)
        embed = discord_bot.make_embed("응답", result["text"], COLOR_INFO)
        discord_bot.send_embed(command_channel_id, embed, config)
        return

    if cmd_type == "loop":
        prompt = claude_bridge.LOOP_SYSTEM_PREAMBLE.format(task=cmd["text"])
        session_id = None
    elif cmd_type == "loop_auto":
        prompt = claude_bridge.AUTO_SYSTEM_PREAMBLE
        session_id = None
    else:  # resume
        prompt = f"방금 물어본 것에 대한 답: {cmd['text']}\n\n이 답을 반영해서 루프 모드로 계속 진행해."
        session_id = cmd.get("session_id")

    result = await claude_bridge.run_claude(prompt, claude_bridge.LOOP_ALLOWED_TOOLS, session_id, timeout=1800)
    approval = claude_bridge.extract_approval(result["text"])
    done = claude_bridge.extract_marker(result["text"], "DONE:")

    if approval:
        fields = [("선택지", "\n".join(f"{i + 1}. {opt}" for i, opt in enumerate(approval["options"])))] if approval["options"] else None
        embed = discord_bot.make_embed("승인 필요", approval["question"] + "\n\n답장으로 알려주면 이어서 진행할게.", COLOR_APPROVAL, fields)
        discord_bot.send_embed(command_channel_id, embed, config)
        status = {"pending": True, "session_id": result["session_id"]}
    else:
        title = "완료" if done else "결과"
        body = done or result["text"]
        embed = discord_bot.make_embed(title, body, COLOR_DONE if done else COLOR_INFO)
        discord_bot.send_embed(command_channel_id, embed, config)
        status = {"pending": False, "session_id": result["session_id"]}

    discord_bot.send_message(queue_channel_id, f"STATUS: {json.dumps(status, ensure_ascii=False)}", config)


async def poll_loop(config: dict) -> None:
    command_channel_id = discord_bot.get_or_create_channel("personal", config.get("command_channel", "claude-reports"), config)
    queue_channel_id = discord_bot.get_or_create_channel("personal", "claude-queue", config)
    state = load_state()

    while True:
        try:
            messages = discord_bot.get_recent_messages(queue_channel_id, config, limit=20)
            new_cmds = []
            for m in reversed(messages):  # oldest-first
                if state["last_queue_msg_id"] and int(m["id"]) <= int(state["last_queue_msg_id"]):
                    continue
                if m.get("content", "").startswith("CMD:"):
                    try:
                        new_cmds.append((m["id"], json.loads(m["content"][len("CMD:"):])))
                    except json.JSONDecodeError:
                        pass
                state["last_queue_msg_id"] = m["id"]

            if new_cmds:
                save_state(state)
            for _, cmd in new_cmds:
                await handle_command(cmd, config, command_channel_id, queue_channel_id)
        except Exception as e:
            print(f"[poll_loop] transient error, continuing: {e}")

        await asyncio.sleep(POLL_INTERVAL_S)


async def heartbeat_loop(config: dict) -> None:
    heartbeat_channel_id = discord_bot.get_or_create_channel("personal", "claude-heartbeat", config)
    state = load_state()
    msg_id = state.get("heartbeat_msg_id")

    while True:
        try:
            try:
                if msg_id:
                    discord_bot.edit_message(heartbeat_channel_id, msg_id, "alive", config)
                else:
                    raise RuntimeError("no heartbeat message yet")
            except Exception:
                msg = discord_bot.send_message(heartbeat_channel_id, "alive", config)
                msg_id = msg["id"]
                state["heartbeat_msg_id"] = msg_id
                save_state(state)
        except Exception as e:
            print(f"[heartbeat_loop] transient error, continuing: {e}")

        await asyncio.sleep(HEARTBEAT_INTERVAL_S)


async def main_async() -> None:
    config = discord_bot.load_config()
    await asyncio.gather(poll_loop(config), heartbeat_loop(config))


if __name__ == "__main__":
    asyncio.run(main_async())

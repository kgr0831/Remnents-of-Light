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
COLOR_STOPPED = 0xED4245
COLOR_PROGRESS = 0x99AAB5

PROGRESS_LABELS = {
    "quick": "질답 처리 중",
    "loop": "루프 진행 중",
    "loop_auto": "다음 작업 결정 중",
    "memory": "기억 저장 중",
    "fix": "시스템 수정 중",
    "resume": "이어서 진행 중",
}

# Set while a background task (asyncio.create_task) is running handle_command,
# so poll_loop can detect it's busy and so /claude-stop knows what to cancel.
active_task: asyncio.Task | None = None


TOOL_PROFILES = {
    "loop": claude_bridge.LOOP_ALLOWED_TOOLS,
    "loop_auto": claude_bridge.LOOP_ALLOWED_TOOLS,
    "memory": claude_bridge.MEMORY_ALLOWED_TOOLS,
    "fix": claude_bridge.FIX_ALLOWED_TOOLS,
}


async def _keep_typing(channel_id: str, config: dict) -> None:
    """Re-triggers Discord's native typing indicator every 8s (it decays after ~10s)
    for as long as this task lives, so long-running loop tasks show visible, animated
    activity instead of going silent until the final embed lands."""
    while True:
        try:
            discord_bot.trigger_typing(channel_id, config)
        except Exception:
            pass
        await asyncio.sleep(8)


async def handle_command(cmd: dict, config: dict, command_channel_id: str, queue_channel_id: str) -> None:
    cmd_type = cmd["type"]
    progress_label = PROGRESS_LABELS.get(cmd_type, "처리 중")
    progress_embed = discord_bot.make_embed(f"⏳ {progress_label}...", cmd.get("text") or "진행 중...", COLOR_PROGRESS)
    progress_msg = discord_bot.send_embed(command_channel_id, progress_embed, config)
    typing_task = asyncio.create_task(_keep_typing(command_channel_id, config))

    try:
        if cmd_type == "quick":
            prompt = claude_bridge.QUICK_SYSTEM_PREAMBLE.format(question=cmd["text"])
            result = await claude_bridge.run_claude(prompt, claude_bridge.QUICK_ALLOWED_TOOLS, None, timeout=180)
            discord_bot.edit_embed(command_channel_id, progress_msg["id"], discord_bot.make_embed("✅ 처리 완료", "아래 참고", COLOR_DONE), config)
            embed = discord_bot.make_embed("응답", result["text"], COLOR_INFO)
            discord_bot.send_embed(command_channel_id, embed, config)  # new message so Discord actually notifies
            return

        if cmd_type == "loop":
            prompt = claude_bridge.LOOP_SYSTEM_PREAMBLE.format(task=cmd["text"])
            session_id = None
            origin_type = "loop"
        elif cmd_type == "loop_auto":
            prompt = claude_bridge.AUTO_SYSTEM_PREAMBLE
            session_id = None
            origin_type = "loop_auto"
        elif cmd_type == "memory":
            prompt = claude_bridge.MEMORY_SYSTEM_PREAMBLE.format(note=cmd["text"], memory_dir=claude_bridge.MEMORY_DIR)
            session_id = None
            origin_type = "memory"
        elif cmd_type == "fix":
            prompt = claude_bridge.FIX_SYSTEM_PREAMBLE.format(issue=cmd["text"])
            session_id = None
            origin_type = "fix"
        else:  # resume
            prompt = f"방금 물어본 것에 대한 답: {cmd['text']}\n\n이 답을 반영해서 루프 모드로 계속 진행해."
            session_id = cmd.get("session_id")
            origin_type = cmd.get("origin_type", "loop")

        allowed_tools = TOOL_PROFILES.get(origin_type, claude_bridge.LOOP_ALLOWED_TOOLS)
        result = await claude_bridge.run_claude(prompt, allowed_tools, session_id, timeout=1800)
        approval = claude_bridge.extract_approval(result["text"])
        done = claude_bridge.extract_marker(result["text"], "DONE:")

        if approval:
            discord_bot.edit_embed(command_channel_id, progress_msg["id"], discord_bot.make_embed("⚠️ 승인 대기로 전환", "아래 참고", COLOR_APPROVAL), config)
            fields = [("선택지", "\n".join(f"{i + 1}. {opt}" for i, opt in enumerate(approval["options"])))] if approval["options"] else None
            embed = discord_bot.make_embed("승인 필요", approval["question"] + "\n\n답장으로 알려주면 이어서 진행할게.", COLOR_APPROVAL, fields)
            discord_bot.send_embed(command_channel_id, embed, config)  # new message so Discord actually notifies
            status = {"pending": True, "session_id": result["session_id"], "origin_type": origin_type}
        else:
            title = "완료" if done else "결과"
            body = done or result["text"]
            discord_bot.edit_embed(command_channel_id, progress_msg["id"], discord_bot.make_embed("✅ 처리 완료", "아래 참고", COLOR_DONE), config)
            embed = discord_bot.make_embed(title, body, COLOR_DONE if done else COLOR_INFO)
            discord_bot.send_embed(command_channel_id, embed, config)  # new message so Discord actually notifies
            status = {"pending": False, "session_id": result["session_id"], "origin_type": origin_type}

        discord_bot.send_message(queue_channel_id, f"STATUS: {json.dumps(status, ensure_ascii=False)}", config)
    except asyncio.CancelledError:
        embed = discord_bot.make_embed("중단됨", "이 작업은 중단됐어.", COLOR_STOPPED)
        try:
            discord_bot.edit_embed(command_channel_id, progress_msg["id"], embed, config)
        except Exception:
            pass
        raise
    finally:
        typing_task.cancel()


async def handle_stop(config: dict, command_channel_id: str) -> None:
    global active_task
    was_running = active_task is not None and not active_task.done()

    if claude_bridge.current_proc is not None:
        try:
            claude_bridge.current_proc.kill()
        except ProcessLookupError:
            pass
    if was_running:
        active_task.cancel()

    body = "진행 중이던 작업을 중단했어." if was_running else "지금 실행 중인 작업이 없어."
    embed = discord_bot.make_embed("중단됨", body, COLOR_STOPPED)
    discord_bot.send_embed(command_channel_id, embed, config)


async def poll_loop(config: dict) -> None:
    global active_task
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
                # Handled inline (not via create_task) so a stop always takes effect on
                # the very next poll tick, even while a task is mid-flight.
                if cmd.get("type") == "stop":
                    await handle_stop(config, command_channel_id)
                    continue
                if active_task is not None and not active_task.done():
                    embed = discord_bot.make_embed("바쁨", "이미 다른 작업을 처리 중이야. 끝날 때까지 기다리거나 /claude-stop으로 중단해줘.", COLOR_APPROVAL)
                    discord_bot.send_embed(command_channel_id, embed, config)
                    continue
                active_task = asyncio.create_task(handle_command(cmd, config, command_channel_id, queue_channel_id))
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

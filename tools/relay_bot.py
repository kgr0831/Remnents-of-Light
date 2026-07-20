"""
Deployed component of the Discord -> Claude Code bridge (SETUP_PLAN STEP5 ext).
Runs on a small always-on host (NOT the dev laptop). Holds the real-time Discord
gateway connection and owns the native slash commands (/claude, /claude-loop,
/claude-auto) so starting a task feels like using Discord, not typing a
text-prefix command.
Never touches Unity or Claude Code itself - it only routes messages between the
owner's command channel and an internal queue channel that tools/executor.py
(running on the laptop) polls.

Also does offline detection: before forwarding a command, it checks how recently
the laptop posted to the heartbeat channel. If it's stale, the laptop is presumably
off/asleep, and the relay says so immediately instead of silently queueing.

Answering a pending approval question is a plain reply in the command channel
(not a slash command) - simplest UX, no extra command to learn.

Deploy target: dishost.kr (git-import, workdir=tools, see ../dishost.yml), or any
other host that can run a persistent Python process. Does NOT need the claude CLI
or the Unity project - only this file + requirements.txt.

Config comes from environment variables when present (DISCORD_TOKEN,
GUILD_PERSONAL, GUILD_TEAM, OWNER_ID, COMMAND_CHANNEL - set via the host's env var
tab), falling back to tools/.secrets/discord_config.json for local testing on the
laptop. This means the real token never needs to be uploaded as a file to a
third-party host.
"""
import json
import os
from datetime import datetime, timezone
from pathlib import Path

from dotenv import load_dotenv

import discord
from discord import app_commands

load_dotenv()

CONFIG_PATH = Path(__file__).parent / ".secrets" / "discord_config.json"
HEARTBEAT_STALE_SECONDS = 180

COLOR_INFO = 0x5865F2
COLOR_APPROVAL = 0xF5A623
COLOR_OFFLINE = 0xED4245


def load_config() -> dict:
    token = os.environ.get("DISCORD_TOKEN")
    if token:
        return {
            "token": token,
            "guilds": {
                "personal": os.environ["GUILD_PERSONAL"],
                "team": os.environ.get("GUILD_TEAM", ""),
            },
            "owner_id": os.environ["OWNER_ID"],
            "command_channel": os.environ.get("COMMAND_CHANNEL", "claude-reports"),
        }
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))


class Relay(discord.Client):
    def __init__(self, *, intents: discord.Intents):
        super().__init__(intents=intents)
        self.tree = app_commands.CommandTree(self)

    async def setup_hook(self):
        config = load_config()
        guild = discord.Object(id=int(config["guilds"]["personal"]))
        self.tree.copy_global_to(guild=guild)
        await self.tree.sync(guild=guild)

    async def on_ready(self):
        config = load_config()
        guild = self.get_guild(int(config["guilds"]["personal"]))
        self.command_channel = discord.utils.get(guild.text_channels, name=config.get("command_channel", "claude-reports"))
        self.queue_channel = await self._get_or_create(guild, "claude-queue")
        self.heartbeat_channel = await self._get_or_create(guild, "claude-heartbeat")
        print(f"relay online as {self.user}, watching #{self.command_channel.name}")

    async def _get_or_create(self, guild: discord.Guild, name: str) -> discord.TextChannel:
        existing = discord.utils.get(guild.text_channels, name=name)
        return existing or await guild.create_text_channel(name)

    async def _latest_status(self) -> dict | None:
        async for msg in self.queue_channel.history(limit=20):
            if msg.content.startswith("STATUS:"):
                try:
                    return json.loads(msg.content[len("STATUS:"):])
                except json.JSONDecodeError:
                    return None
        return None

    async def _heartbeat_fresh(self) -> bool:
        async for msg in self.heartbeat_channel.history(limit=1):
            last_seen = msg.edited_at or msg.created_at
            age = (datetime.now(timezone.utc) - last_seen).total_seconds()
            return age < HEARTBEAT_STALE_SECONDS
        return False

    def _authorized(self, user_id: int, channel_id: int) -> bool:
        config = load_config()
        owner_id = config.get("owner_id")
        return bool(owner_id) and str(user_id) == str(owner_id) and channel_id == self.command_channel.id

    async def start_task(self, interaction: discord.Interaction, kind: str, text: str) -> None:
        if not self._authorized(interaction.user.id, interaction.channel_id):
            await interaction.response.send_message("여기서는 이 명령을 쓸 수 없어.", ephemeral=True)
            return
        if not await self._heartbeat_fresh():
            embed = discord.Embed(title="노트북 오프라인", description="하트비트가 없어. executor.py가 켜져 있는지 확인해줘.", color=COLOR_OFFLINE)
            await interaction.response.send_message(embed=embed)
            return

        cmd = {"type": kind, "text": text}
        await self.queue_channel.send(f"CMD: {json.dumps(cmd, ensure_ascii=False)}")
        labels = {"loop": "루프 시작", "loop_auto": "다음 작업 스스로 결정 중", "quick": "질문 접수"}
        description = text if kind != "loop_auto" else "기획안 + 현재 상황(task.md) 보고 다음 작업을 정하는 중..."
        embed = discord.Embed(title=labels[kind], description=description, color=COLOR_INFO)
        await interaction.response.send_message(embed=embed)

    async def on_message(self, message: discord.Message):
        # Only handles replies to a pending approval question - fresh commands
        # go through the /claude and /claude-loop slash commands instead.
        if message.author.bot or not message.guild:
            return
        if not self._authorized(message.author.id, message.channel.id):
            return

        status = await self._latest_status()
        if not (status and status.get("pending")):
            return
        if not await self._heartbeat_fresh():
            embed = discord.Embed(title="노트북 오프라인", description="하트비트가 없어. executor.py가 켜져 있는지 확인해줘.", color=COLOR_OFFLINE)
            await message.channel.send(embed=embed)
            return

        cmd = {"type": "resume", "text": message.content.strip(), "session_id": status["session_id"]}
        await self.queue_channel.send(f"CMD: {json.dumps(cmd, ensure_ascii=False)}")
        embed = discord.Embed(title="답변 접수", description="이어서 진행할게...", color=COLOR_INFO)
        await message.channel.send(embed=embed)


def main() -> None:
    config = load_config()
    if not config.get("owner_id"):
        raise SystemExit("discord_config.json 에 owner_id를 먼저 채워줘.")

    intents = discord.Intents.default()
    intents.message_content = True
    client = Relay(intents=intents)

    @client.tree.command(name="claude", description="가벼운 질답 (읽기 전용, 코드 수정 없음)")
    @app_commands.describe(질문="물어볼 내용")
    async def claude_quick(interaction: discord.Interaction, 질문: str):
        await client.start_task(interaction, "quick", 질문)

    @client.tree.command(name="claude-loop", description="실제 개발 작업을 루프 모드로 원격 실행")
    @app_commands.describe(할일="수행할 작업 설명")
    async def claude_loop(interaction: discord.Interaction, 할일: str):
        await client.start_task(interaction, "loop", 할일)

    @client.tree.command(name="claude-auto", description="기획안 + 현황(task.md) 보고 다음 작업을 스스로 정해서 승인받고 진행")
    async def claude_auto(interaction: discord.Interaction):
        await client.start_task(interaction, "loop_auto", "")

    client.run(config["token"])


if __name__ == "__main__":
    main()

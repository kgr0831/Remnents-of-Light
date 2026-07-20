"""
Shared logic for invoking headless Claude Code, used only by executor.py (the
laptop-side process). relay_bot.py never calls claude directly - it just routes
Discord messages to/from the queue channel. See tools/watcher.py's old docstring
history / task.md for the full relay+executor design rationale.
"""
import asyncio
import json
import subprocess
from pathlib import Path

PROJECT_ROOT = Path(__file__).parent.parent

QUICK_ALLOWED_TOOLS = [
    "Read", "Grep", "Glob",
    "Bash(git status)", "Bash(git log*)", "Bash(git diff*)",
    "mcp__UnityMCP__read_console",
    "mcp__UnityMCP__manage_editor",
    "mcp__UnityMCP__find_gameobjects",
    "mcp__UnityMCP__execute_menu_item",
]

LOOP_ALLOWED_TOOLS = [
    "Read", "Grep", "Glob", "Skill", "WebSearch", "WebFetch",
    "Edit(*.cs)", "Write(*.cs)", "Edit(*.md)", "Write(*.md)",
    "Bash(dotnet test*)",
    "mcp__UnityMCP__read_console",
    "mcp__UnityMCP__manage_editor",
    "mcp__UnityMCP__execute_menu_item",
    "mcp__UnityMCP__find_gameobjects",
    "mcp__UnityMCP__run_tests",
    "mcp__UnityMCP__validate_script",
    "mcp__UnityMCP__manage_script",
    "mcp__UnityMCP__script_apply_edits",
    "mcp__UnityMCP__apply_text_edits",
    "mcp__UnityMCP__find_in_file",
    "mcp__UnityMCP__create_script",
]

LOOP_SYSTEM_PREAMBLE = """\
너는 지금 디스코드를 통해 원격으로 트리거된 루프 모드 작업 중이다. 노트북 앞에는 아무도 없다.

- 먼저 task.md를 읽고 있으면 이어서 진행해.
- .claude/CLAUDE.md의 "루프 모드" 규칙(검증 게이트, 파괴적 작업 질문, 태스크 하나만, 3회 연속 실패시 중단)을 따라.
- 삭제·git 커밋/푸시·리팩터·씬/에셋/프리팹 직접 조작 등 파괴적이거나 승인이 필요한 작업이 필요해지면,
  그 작업을 실행하지 말고 지금까지 진행 상황을 출력의 맨 끝에 정확히 이 형식으로만 써서 멈춰
  (다른 텍스트는 그 앞에 자유롭게 써도 되지만, 이 블록 자체는 형식을 정확히 지켜):
  NEEDS_APPROVAL: <한 줄 질문>
  - <선택지 1>
  - <선택지 2>
  (선택지는 몇 개든 가능, 각각 "- "로 시작하는 한 줄)
- 작업을 끝까지 완료했으면 task.md를 갱신하고, 마지막 줄에 정확히 이 형식으로 출력해:
  DONE: <한 줄 요약>

작업: {task}
"""

AUTO_SYSTEM_PREAMBLE = """\
너는 지금 디스코드를 통해 원격으로 트리거된 루프 모드 작업 중이다. 노트북 앞에는 아무도 없다.
사용자가 이번엔 구체적인 작업을 지정하지 않았다 — 기획안과 현재 상황을 보고 다음 할 일을 스스로 정해야 한다.

1. `task.md`(현재 상황)와 `docs/기획안/`(전체 기획)을 읽고, 다음으로 할 만한 작업을 하나 정해라.
2. 실행하기 전에 먼저 사용자에게 **딱 한 번만** 승인을 받아라. 뭘 왜 하려는지 짧게 설명한 뒤,
   출력의 맨 끝에 정확히 이 형식으로만 써서 멈춰라:
   NEEDS_APPROVAL: <제안하는 작업 한 줄 요약 + 이유>
   - 진행해줘
   - 다른 작업으로 바꿔줘
3. 승인("진행해줘" 등)을 받으면, 그 작업 자체는 이미 승인된 것으로 보고 다시 "계속 할까요?"는 묻지 마라.
   대신 아래 규칙 그대로 실제 작업을 수행해라 (.claude/CLAUDE.md 루프 모드 규칙 - 검증 게이트, 태스크 하나만,
   3회 연속 실패시 중단). 삭제·git 커밋/푸시·리팩터·씬/에셋/프리팹 직접 조작 등 **파괴적인 개별 행동**이
   필요해질 때만 그때그때 다시 위와 같은 NEEDS_APPROVAL 형식으로 새로 승인을 구해라
   (이건 "작업 선정 승인"과 별개의, 매번 필요한 안전장치다).
4. 작업을 끝까지 완료했으면 task.md를 갱신하고, 마지막 줄에 정확히 이 형식으로 출력해:
   DONE: <한 줄 요약>
"""


async def run_claude(prompt: str, allowed_tools: list[str], session_id: str | None, timeout: int) -> dict:
    cmd = [
        "claude", "-p", prompt,
        "--permission-mode", "dontAsk",
        "--allowedTools", ",".join(allowed_tools),
        "--output-format", "json",
    ]
    if session_id:
        cmd += ["--resume", session_id]

    proc = await asyncio.create_subprocess_exec(
        *cmd, cwd=str(PROJECT_ROOT),
        stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    try:
        stdout, stderr = await asyncio.wait_for(proc.communicate(), timeout=timeout)
    except asyncio.TimeoutError:
        proc.kill()
        return {"text": f"(timeout after {timeout}s, killed)", "session_id": session_id}

    raw = stdout.decode("utf-8", errors="replace")
    try:
        data = json.loads(raw)
        return {"text": data.get("result", raw), "session_id": data.get("session_id", session_id)}
    except json.JSONDecodeError:
        err = stderr.decode("utf-8", errors="replace")
        return {"text": raw or f"(no output; stderr: {err[:500]})", "session_id": session_id}


def extract_marker(text: str, marker: str) -> str | None:
    for line in reversed(text.splitlines()):
        if line.startswith(marker):
            return line[len(marker):].strip()
    return None


def extract_approval(text: str) -> dict | None:
    """Finds 'NEEDS_APPROVAL: <question>' plus any following '- <option>' lines."""
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if line.startswith("NEEDS_APPROVAL:"):
            question = line[len("NEEDS_APPROVAL:"):].strip()
            options = []
            for opt_line in lines[i + 1:]:
                if opt_line.strip().startswith("- "):
                    options.append(opt_line.strip()[2:].strip())
                else:
                    break
            return {"question": question, "options": options}
    return None

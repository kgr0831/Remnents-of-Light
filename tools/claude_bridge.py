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
MEMORY_DIR = "C:/Users/kimga/.claude/projects/C--Users-kimga-Remnents-of-Light/memory"

QUICK_ALLOWED_TOOLS = [
    "Read", "Grep", "Glob",
    "Bash(git status)", "Bash(git log*)", "Bash(git diff*)",
    "mcp__UnityMCP__read_console",
    "mcp__UnityMCP__manage_editor",
    "mcp__UnityMCP__find_gameobjects",
    "mcp__UnityMCP__execute_menu_item",
]

# Quick mode is a fresh session every time (no --resume) and previously got just
# the raw question with no framing, so it had no idea a loop task had, say, just
# finished verifying something in Play mode a minute earlier - it would silently
# re-derive an answer from static code alone instead of citing the real result.
# task.md is the one thing every command type can cheaply check for "what's
# actually true/current right now" - checked 2026-07-21 after this caused a
# visibly wrong answer (see task.md 왼쪽 이동 안 됨 entry vs the quick-mode reply
# right after it).
QUICK_SYSTEM_PREAMBLE = """\
너는 지금 디스코드를 통해 원격으로 트리거된 가벼운 질답 중이다. 이 세션은 방금 시작된 새 세션이라
직전 대화 기록이 없다 - 답하기 전에 먼저 task.md를 Read로 훑어서, 방금 다른 작업(루프 모드 등)이
이미 확인/검증/결정해둔 게 있으면 그 사실을 우선해서 답해라 (다시 추측하거나 재검증하지 말 것).

너는 이 세션에서 파일 수정이나 실행 코드(execute_code) 같은 도구가 없다 - 요청이 "고쳐줘/수정해줘"처럼
실제 코드 변경이나 런타임 재현·디버깅을 필요로 하는 게 명백하면, 시도하다 시간 초과로 멈추지 말고
**즉시** 이렇게 짧게 답하고 끝내라: "이건 quick 모드로는 못 해 - /claude-loop로 다시 보내줘." 그리고
왜 그런지(어떤 도구/권한이 필요한지) 한 줄만 덧붙여라. 단순 질문/확인이면 평소처럼 바로 답해라.

질문: {question}
"""

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
    # Runs arbitrary C# inside the Editor process - broader than any single MCP
    # tool (can touch scenes/assets/settings same as the excluded manage_* tools
    # would). Added anyway 2026-07-21: real runtime debugging (e.g. inspecting
    # InputSystem device/settings state) is not possible without it, and quick
    # mode stays read-only-ish by not getting this. Loop mode's other safeguards
    # (task.md logging, NEEDS_APPROVAL for destructive intent, no git/Bash access)
    # still apply - this doesn't bypass those, it just also allows inspection code.
    "mcp__UnityMCP__execute_code",
]

LOOP_SYSTEM_PREAMBLE = """\
너는 지금 디스코드를 통해 원격으로 트리거된 루프 모드 작업 중이다. 노트북 앞에는 아무도 없다.

- 먼저 task.md를 읽고 있으면 이어서 진행해.
- .claude/CLAUDE.md의 "루프 모드" 규칙(검증 게이트, 파괴적 작업 질문, 태스크 하나만, 3회 연속 실패시 중단)을 따라.
- `execute_code`(Unity 에디터 안에서 임의 C# 실행)는 상태 확인·디버깅 등 **읽기/진단 목적으로만** 써라.
  이걸로 씬 오브젝트 생성/삭제, 에셋 변경, 프로젝트 설정 변경 같은 걸 하려면 아래와 똑같이 승인부터 구해라 —
  "도구가 허용 목록에 있다"는 게 "그 행동에 승인이 필요없다"는 뜻이 아니다.
- **다음 중 하나라도 해당하면 계속 진행하지 말고 NEEDS_APPROVAL로 멈춰서 사용자 결정을 기다려라**:
  (a) 삭제·git 커밋/푸시·리팩터·씬/에셋/프리팹 직접 조작 등 파괴적이거나 승인이 필요한 작업이 필요해질 때
  (b) 조사/리서치 결과 여러 방향(레퍼런스, 구현 방식 등) 중 사용자가 골라야 진행할 수 있을 때 -
      "옵션을 제시하고 세션이 끝나버리면" 다음 세션은 이 대화를 기억 못 해서 사용자의 "2번" 같은 답을
      영영 못 알아듣는다. 옵션을 제시하는 순간이 바로 이 형식으로 멈춰야 하는 시점이다.
  이 블록 자체는 형식을 정확히 지켜라(다른 텍스트는 그 앞에 자유롭게 써도 된다):
  NEEDS_APPROVAL: <한 줄 질문>
  - <선택지 1>
  - <선택지 2>
  (선택지는 몇 개든 가능, 각각 "- "로 시작하는 한 줄 - 사용자가 "1번/2번"으로 답할 수 있도록 항상
  숫자로 세는 목록으로 제시해라. A/B/C 같은 글자 목록은 쓰지 마라.)
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
   3회 연속 실패시 중단). 다음 중 하나라도 해당하면 그때그때 다시 NEEDS_APPROVAL 형식으로 새로 승인/결정을
   구해라 (이건 "작업 선정 승인"과 별개의, 매번 필요한 안전장치다):
   (a) 삭제·git 커밋/푸시·리팩터·씬/에셋/프리팹 직접 조작 등 **파괴적인 개별 행동**이 필요해질 때
   (b) 조사 결과 여러 방향 중 사용자가 골라야 진행할 수 있을 때 - 이 순간 세션이 끊기면 다음 세션은
       제시한 옵션 자체를 기억 못 하니, 옵션을 숫자 목록(1/2/3 - A/B/C 금지, 사용자가 "1번/2번"으로
       답할 수 있게)으로 제시하며 반드시 이 형식으로 멈춰라.
4. 작업을 끝까지 완료했으면 task.md를 갱신하고, 마지막 줄에 정확히 이 형식으로 출력해:
   DONE: <한 줄 요약>
"""


MEMORY_ALLOWED_TOOLS = [
    "Read", "Grep", "Glob",
    f"Write({MEMORY_DIR}/*)", f"Edit({MEMORY_DIR}/*)",
]

MEMORY_SYSTEM_PREAMBLE = """\
너는 지금 디스코드를 통해 원격으로 트리거된 "영구 기억 저장" 작업 중이다. 노트북 앞에는 아무도 없다.

사용자가 다음 내용을 영구 기억으로 남기고 싶어한다: "{note}"

Claude Code의 파일 기반 메모리 시스템({memory_dir})에 저장해라:
- 먼저 그 디렉터리의 MEMORY.md(인덱스)와 기존 메모리 파일들을 Read/Glob으로 훑어서, 비슷한 주제의
  기존 파일이 있으면 그걸 갱신하고, 없으면 새로 만들어라 (kebab-case 파일명).
- frontmatter 형식: `name`, `description`, `metadata.type`(user/feedback/project/reference 중 하나).
- feedback·project 타입 본문에는 **Why:**와 **How to apply:** 줄을 포함해라.
- 다른 메모리와 관련 있으면 본문에 `[[파일명]]` 형식으로 링크해라.
- MEMORY.md 인덱스에도 한 줄(`- [제목](파일.md) — 한줄 설명`) 추가하거나 갱신해라. 200줄 넘지 않게 간결히 유지.
- 코드/설정 파일에서 바로 확인 가능한 사실(파일 경로, 이미 존재하는 아키텍처 설명 등)은 저장하지 마라 -
  코드로 유도 불가능한 것만 저장.
- 저장 후 마지막 줄에 정확히 이 형식으로 출력해: DONE: <저장/갱신한 파일명과 한 줄 요약>
"""

FIX_ALLOWED_TOOLS = [
    "Read", "Grep", "Glob",
    "Edit(*.py)", "Write(*.py)", "Edit(*.md)", "Write(*.md)",
]

FIX_SYSTEM_PREAMBLE = """\
너는 지금 디스코드를 통해 원격으로 트리거된 "루프 시스템 자체 수정" 작업 중이다. 노트북 앞에는 아무도 없다.

이 디스코드 원격 제어 시스템 자체(`tools/relay_bot.py`, `tools/executor.py`, `tools/claude_bridge.py`,
`tools/discord_bot.py`, `.claude/CLAUDE.md`의 루프 모드 규칙 등)에 대한 요청이다: "{issue}"

- 관련 파일들을 먼저 Read로 읽고 구조를 파악해라.
- 필요한 수정을 `tools/*.py` 또는 관련 `.md` 파일에 직접 해라 (Edit/Write 허용됨).
- **중요**: 여기서 고친 건 로컬 파일만 바뀐다. git commit/push나 `bot-deploy` 브랜치 재배포는 안전장치상
  자동으로 안 되고, 사용자가 직접 검토 후 커밋/배포해야 한다 - 이 사실을 결과에 반드시 언급해라.
- Bash 실행 권한이 없어서 컴파일/실행 검증은 못 한다. 대신 수정 후 Read로 다시 읽어서 문법·로직을 스스로
  재검토해라.
- 이 파일들 외의 다른 작업(git 명령, 삭제, 다른 확장자 파일 등)이 필요해지면 평소처럼 멈추고 정확히 이
  형식으로 물어봐라: NEEDS_APPROVAL: <질문> / - <선택지>
- 수정을 마쳤으면 마지막 줄에 정확히 이 형식으로 출력해:
  DONE: <무엇을 고쳤는지 요약 - 커밋/배포 필요하다는 안내 포함>
"""


# Tracks the in-flight `claude -p` subprocess (if any) so a /claude-stop command
# arriving mid-task can kill it. Single-slot: executor.py only ever runs one
# headless task at a time.
current_proc: asyncio.subprocess.Process | None = None


async def run_claude(prompt: str, allowed_tools: list[str], session_id: str | None, timeout: int) -> dict:
    global current_proc
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
    current_proc = proc
    try:
        stdout, stderr = await asyncio.wait_for(proc.communicate(), timeout=timeout)
    except asyncio.TimeoutError:
        proc.kill()
        return {"text": f"(timeout after {timeout}s, killed)", "session_id": session_id}
    finally:
        current_proc = None

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

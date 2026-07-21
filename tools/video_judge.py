"""
Judges a test-recording video (already uploaded to YouTube) by asking agy
(Antigravity CLI, Gemini 3.1 Pro) to watch it and verdict PASS/FAIL against
given criteria - 3 independent calls (fresh context each), majority vote.

Genuine video analysis (actually downloading/watching frames, not just
paraphrasing the YouTube title) requires a tool agy gates behind the
"command" permission, which headless mode auto-denies unless
--dangerously-skip-permissions is passed - confirmed empirically 2026-07-21
(without the flag, agy either returned nothing or answered from the video
title alone; with it, it correctly described unscripted visual details like
background color and on-screen movement direction).

Since that flag disables all tool-permission prompting for the agy process,
each call is run with cwd pointed at an empty, disposable sandbox directory
(not the project root) so there's nothing of value for it to read or write
even in the worst case.
"""
import asyncio
import subprocess
import tempfile
from pathlib import Path

MODEL = "Gemini 3.1 Pro (High)"
_SANDBOX_DIR = Path(tempfile.gettempdir()) / "rol_video_judge_sandbox"

JUDGE_PROMPT = """\
다음 유튜브 영상을 보고 테스트 성공 여부를 판단해라: {youtube_url}

검증 기준: {criteria}

영상을 실제로 보고 판단 근거를 1~2문장으로 적은 뒤, 마지막 줄에 정확히 이 형식으로만 출력해라:
VERDICT: PASS
또는
VERDICT: FAIL
"""


async def _judge_once(youtube_url: str, criteria: str, timeout: int) -> tuple[bool | None, str]:
    prompt = JUDGE_PROMPT.format(youtube_url=youtube_url, criteria=criteria)
    _SANDBOX_DIR.mkdir(exist_ok=True)
    proc = await asyncio.create_subprocess_exec(
        "agy", "-p", prompt, "--model", MODEL, "--dangerously-skip-permissions",
        cwd=str(_SANDBOX_DIR),
        stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    try:
        stdout, stderr = await asyncio.wait_for(proc.communicate(), timeout=timeout)
    except asyncio.TimeoutError:
        proc.kill()
        return None, "(timeout)"

    text = stdout.decode("utf-8", errors="replace")
    verdict = None
    for line in reversed(text.splitlines()):
        if line.strip().startswith("VERDICT:"):
            v = line.split(":", 1)[1].strip().upper()
            verdict = v == "PASS"
            break
    return verdict, text


async def judge_video(youtube_url: str, criteria: str, rounds: int = 3, timeout: int = 120) -> dict:
    results = await asyncio.gather(*[_judge_once(youtube_url, criteria, timeout) for _ in range(rounds)])
    votes = [v for v, _ in results if v is not None]
    passed = votes.count(True) > len(votes) / 2 if votes else False
    return {
        "passed": passed,
        "votes": votes,
        "pass_count": votes.count(True),
        "total": len(votes),
        "details": [text for _, text in results],
    }

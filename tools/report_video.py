"""
Combines youtube_upload.py + video_judge.py into one call for /claude-loop tasks:
upload a just-recorded test clip, have agy watch it and verdict, print both so
the calling Claude Code process can fold them into its final DONE: report.

Usage: tools/.venv/Scripts/python.exe tools/report_video.py <video_path> <title> <criteria>
"""
import asyncio
import sys

from video_judge import judge_video
from youtube_upload import upload_video


async def main() -> None:
    video_path, title, criteria = sys.argv[1], sys.argv[2], sys.argv[3]
    url = upload_video(video_path, title)
    result = await judge_video(url, criteria)
    verdict = "PASS" if result["passed"] else "FAIL"
    print(f"URL: {url}")
    print(f"VERDICT: {verdict} ({result['pass_count']}/{result['total']})")


if __name__ == "__main__":
    asyncio.run(main())

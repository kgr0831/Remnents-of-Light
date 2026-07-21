"""
Uploads a video file to YouTube as unlisted, using the refresh token saved by
youtube_auth_setup.py. Used by the test-verification pipeline to publish a
recorded PlayTestRunner clip so agy/Gemini can watch it via a plain link.

Usage: tools/.venv/Scripts/python.exe tools/youtube_upload.py <video_path> <title> [description]
"""
import sys
from pathlib import Path

from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

TOKEN_PATH = Path(__file__).parent / ".secrets" / "youtube_token.json"


def _load_credentials() -> Credentials:
    creds = Credentials.from_authorized_user_file(str(TOKEN_PATH))
    if creds.expired and creds.refresh_token:
        creds.refresh(Request())
        TOKEN_PATH.write_text(creds.to_json(), encoding="utf-8")
    return creds


def upload_video(video_path: str, title: str, description: str = "") -> str:
    youtube = build("youtube", "v3", credentials=_load_credentials())
    body = {
        "snippet": {"title": title, "description": description},
        "status": {"privacyStatus": "unlisted"},
    }
    media = MediaFileUpload(video_path, chunksize=-1, resumable=True)
    request = youtube.videos().insert(part="snippet,status", body=body, media_body=media)
    response = None
    while response is None:
        _, response = request.next_chunk()
    video_id = response["id"]
    return f"https://youtu.be/{video_id}"


if __name__ == "__main__":
    path, title = sys.argv[1], sys.argv[2]
    desc = sys.argv[3] if len(sys.argv) > 3 else ""
    print(upload_video(path, title, desc))

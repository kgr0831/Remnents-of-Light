"""
One-time interactive OAuth setup for YouTube uploads. Opens a browser for the
owner to log in and consent, then saves the resulting refresh token to
tools/.secrets/youtube_token.json for youtube_upload.py to reuse headlessly.

Re-run this whenever the token expires (every 7 days while the OAuth consent
screen is in "Testing" publish status - see task.md).

Usage: tools/.venv/Scripts/python.exe tools/youtube_auth_setup.py
"""
from pathlib import Path

from google_auth_oauthlib.flow import InstalledAppFlow

SECRETS_DIR = Path(__file__).parent / ".secrets"
CLIENT_SECRET_PATH = SECRETS_DIR / "youtube_client_secret.json"
TOKEN_PATH = SECRETS_DIR / "youtube_token.json"
SCOPES = ["https://www.googleapis.com/auth/youtube.upload"]


def main() -> None:
    flow = InstalledAppFlow.from_client_secrets_file(str(CLIENT_SECRET_PATH), SCOPES)
    creds = flow.run_local_server(port=0)
    TOKEN_PATH.write_text(creds.to_json(), encoding="utf-8")
    print(f"Saved refresh token to {TOKEN_PATH}")


if __name__ == "__main__":
    main()

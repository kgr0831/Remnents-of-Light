# 음성 지시 사이트 배포 (사용자 실행)

> 2026-08-04 · 원격 루프 세션이 "배포하고 도메인 주소 알려줘"를 받고 준비한 문서.
> **사용자 선택: 1번(Cloudflare 퀵 터널)** — 주소는 무작위이고 재시작마다 바뀐다.
>
> **1단계(설치)는 루프 세션이 대신 끝냈다.** 남은 건 아래 2단계 한 줄뿐이다.
> 프로세스 기동만은 루프가 못 한다 — 실측으로 두 지점 다 거부됨:
> `Bash(tools/.secrets/cloudflared.exe --version)` ❌ · `PowerShell(Start-Process ... voice_web.py)` ❌.
> 루프 모드 Bash 허용 목록은 `dotnet test*` · `report_video.py*` 2개뿐이다(`claude_bridge.py:56-80`).
> `docs/notion/APPLY_PERMISSION.md`와 같은 구조적 제약이며, 루프로 다시 보내도 결과는 같다.

## 지금 상태 (2026-08-04 실측)

| 항목 | 확인 방법 | 결과 |
|---|---|---|
| `voice_web.py` 코드 | 정적 검토 | ✅ 정상 — `run_claude` 시그니처 · `discord_bot` 함수 3개 · `CMD:` 포맷 전부 `relay_bot.py`와 일치 |
| `aiohttp` | `tools/.venv/Lib/site-packages` | ✅ 3.14.1 설치됨 (discord.py 의존으로 딸려옴, `requirements.txt`엔 없음) |
| 비밀번호 | `tools/.secrets/voice_config.json` | ✅ 이미 생성돼 있음 (첫 실행 때 만들어짐) |
| 서버 기동 여부 | `curl http://127.0.0.1:8765/` | ❌ 안 떠 있음 (연결 실패) |
| `cloudflared` | `which cloudflared` | ❌ 설치 안 됨 |

---

## 1단계 — cloudflared 설치 (최초 1회)

```powershell
winget install --id Cloudflare.cloudflared
```

winget이 막히면 exe 직접 받기 (확인 2026-08-04, 200 OK):

```powershell
curl.exe -L -o "$env:USERPROFILE\bin\cloudflared.exe" `
  https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe
```

## 2단계 — 두 프로세스 기동

**창 2개를 띄워서 각각 실행** (창을 닫으면 죽는다. 앞에 앉아 있는 동안 쓰는 용도라면 이게 제일 단순하다.)

```powershell
# 창 A — 웹서버 (비밀번호를 여기 찍어준다)
cd "C:\Users\kimga\Remnents of Light"
tools\.venv\Scripts\python.exe tools\voice_web.py
```

```powershell
# 창 B — 터널. 출력에 https://<무작위>.trycloudflare.com 이 찍힌다. 그게 주소다.
cloudflared tunnel --url http://localhost:8765
```

창 없이 백그라운드로 돌리려면:

```powershell
cd "C:\Users\kimga\Remnents of Light"
Start-Process tools\.venv\Scripts\pythonw.exe -ArgumentList "tools\voice_web.py" -WindowStyle Hidden
Start-Process cloudflared -ArgumentList "tunnel --url http://localhost:8765" -WindowStyle Hidden `
  -RedirectStandardError tools\.secrets\tunnel.log -RedirectStandardOutput tools\.secrets\tunnel.out.log
Start-Sleep 8
Select-String "trycloudflare.com" tools\.secrets\tunnel.log   # 주소 확인 (cloudflared는 stderr로 찍는다)
```

## 3단계 — 폰에서 접속

1. 위에서 얻은 `https://….trycloudflare.com` 을 폰 브라우저로 연다.
2. 비밀번호를 넣는다 — `tools/.secrets/voice_config.json`의 `password` 값.
3. 🎤 → 🧹 정리하기 → 🚀 제출. 제출은 `#claude-queue`에 `CMD: {"type":"loop",...}`로 들어가므로
   **로컬 `executor.py`가 떠 있어야** 실제로 작업이 돈다(안 떠 있으면 큐에 쌓였다가 재시작 때 처리됨).

---

## 알아둘 제약

- **주소가 매번 바뀐다.** 퀵 터널은 재시작할 때마다 새 무작위 서브도메인을 받는다. 고정 주소가 필요하면 아래 참고.
- **노트북이 켜져 있어야 한다.** `/api/clean`이 로컬 `claude -p`(Haiku)를 호출하므로 클라우드 호스팅으로 못 옮긴다.
- **비밀번호가 곧 실행 권한이다.** `/api/submit`은 루프 작업을 큐에 넣고, 그 작업은 노트북에서 파일을 고친다.
  URL을 남에게 주지 말 것. (`/` HTML은 인증 없이 열리지만 두 API는 `X-Voice-Password` 헤더를 검사한다.)
- 퀵 터널은 동시 요청 200개 제한 · SSE 미지원 — 이 앱은 단순 POST라 무관.
- 정리하기 버튼이 엉뚱한 텍스트(에러 문자열)를 뱉으면 `voice_web.py:67`의 `allowed_tools=[]`가
  `--allowedTools ""`로 넘어가는 걸 먼저 의심해라. `["Read"]` 정도로 바꾸면 회피된다.

## 고정 도메인이 필요하면 (네임드 터널)

브라우저 로그인이 필요해서 원격 세션이 대신 못 해준다. Cloudflare 계정에 도메인이 등록돼 있어야 한다.

```powershell
cloudflared tunnel login                      # 브라우저 열림 - 도메인 선택
cloudflared tunnel create voice
cloudflared tunnel route dns voice voice.<내도메인>
cloudflared tunnel run --url http://localhost:8765 voice
```

이후 주소는 `https://voice.<내도메인>` 으로 고정된다.

## 출처

- TryCloudflare 퀵 터널(무작위 주소 · 무료 · 200 동시요청 제한 · SSE 미지원):
  <https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/do-more-with-tunnels/trycloudflare/> (확인 2026-08-04)
- Windows 설치(winget `Cloudflare.cloudflared`, 자동 업데이트 없음):
  <https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/downloads/> (확인 2026-08-04)

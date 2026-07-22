# 루프 엔지니어링 — 디스코드 원격 제어 시스템 (작업 현황)

> task.md에서 분리(2026-07-22). 게임 개발 진행상황은 task.md, **이 시스템 자체의 구조·상태·이슈**는 여기.
> 최종 업데이트: 2026-07-22 · main HEAD `271aa6a` / bot-deploy HEAD `7ca9ef1`

## 📍 현재 위치
슬래시 커맨드 6종 + FIFO 큐 + 승인 버튼 UI + 영상 검증 파이프라인까지 한 바퀴 구현 완료. 방금 executor.py가
네트워크 요청 타임아웃 부재로 수 시간 멈춰있던 버그를 고침(아래 "겪은 버그" 표 마지막 항목). **영상 파이프라인을
`/claude-loop`·`/claude-auto` 표준 흐름에 실제로 연결**(2026-07-22) — 그 전까지는 구현만 돼 있고 한 번도
자동으로 트리거된 적이 없었음(사용자가 "왜 영상 보고를 안 하냐"고 지적해서 발견). 열려있는 건 agy 폴백
체인(미착수, 사용자 확인 대기)과 이번에 만든 파일들 커밋 여부.

### 🎬 영상 보고 연결 (2026-07-22)
- **문제**: 영상 파이프라인(Recorder→YouTube→agy 판단)이 스크립트로는 완성돼 있었지만, `LOOP_ALLOWED_TOOLS`엔
  파이썬 실행 권한이 없고 프리앰블에도 "돌려라"는 지시가 없어서 **모든 `/claude-loop` 작업이 그냥 `task.md`
  갱신 + 한 줄 요약(`DONE:`)만 하고 끝났음**. 실제 사례: 대시 VFX(산데비스탄 잔상) 구현 완료 보고가 영상 없이
  한 줄로만 나감 → 사용자가 즉시 "보고를 제대로 안 했어"로 지적.
- **연결**: `tools/report_video.py` 신규(업로드+판단 통합 CLI: `report_video.py <mp4경로> <제목> <판단기준>` →
  stdout에 `URL: ...` / `VERDICT: PASS|FAIL (n/m)`). `LOOP_ALLOWED_TOOLS`에
  `Bash(tools/.venv/Scripts/python.exe tools/report_video.py*)` 추가. `execute_code`의 "읽기/진단 전용" 제한에
  `TestRecorder.StartRecording/StopRecording`(씬/에셋 불변경) 예외 명시. `LOOP_SYSTEM_PREAMBLE`/
  `AUTO_SYSTEM_PREAMBLE` 둘 다에 "**코드 변경이 있었던 완료 작업은 영상 보고 필수**(조사만 하고 끝난 건 제외)"
  규칙 추가.
- **DONE: 형식을 멀티라인으로 변경**: `DONE: <요약>` 아래에 `- 영상: <url>` / `- 다음: <작업>` 줄을 붙이는
  구조로 확장. `claude_bridge.extract_marker`(한 줄만 반환) 대신 `extract_done()` 신규(요약+`- key: value`
  줄들을 dict로 파싱, `NEEDS_APPROVAL`용 `extract_approval`과 동일 패턴). `executor.py`가 이 dict의 `extras`를
  디스코드 embed 필드로 렌더링.
- 아직 안 한 것: 다음 `/claude-loop` 완료 건에서 실제로 영상 링크가 딸려오는지 라이브 검증 필요(코드상 완료,
  실사용 미확인).

## 🏗️ 아키텍처
```
[휴대폰 디스코드]
      │ 슬래시 커맨드 / 버튼 클릭 / 답장
      ▼
relay_bot.py  ── dishost.kr에 상시 배포(gateway 연결, 슬래시커맨드 소유)
      │ #claude-queue 채널에 "CMD: {...}" 메시지로 적재 (디스코드 자체를 큐로 재사용)
      ▼
executor.py   ── 노트북 상주, REST 폴링(3초 간격), claude -p 헤드리스 실행
      │
      ▼
claude_bridge.py ── run_claude() 공통 로직, allowedTools 프로필, 프리앰블, 트랜지언트 재시도
      │
      ▼
discord_bot.py ── 순수 REST 클라이언트 (relay_bot.py는 안 씀, discord.py 라이브러리 직접 사용)
```
- 채널: `#claude-reports`(사용자 대면 command channel) · `#claude-queue`(내부 큐+STATUS) · `#claude-heartbeat`(생존 신호)
- 배포 분리 이유: dishost.kr가 서브폴더/512MB 제한 → `bot-deploy` 브랜치는 `relay_bot.py`+`requirements.txt`만 루트에 두는 별도 구조. **항상 격리된 스크래치 클론에서 동기화**(작업 트리 직접 건드리지 않음). dishost "Push 시 자동 재배포"는 **신뢰 불가(2회 확인)** → `relay_bot.py` 바뀔 때마다 대시보드에서 수동 재배포 필요.

## ✅ 완료된 기능
- **슬래시 커맨드 6종**: `/claude`(읽기전용 질답) · `/claude-loop`(개발작업) · `/claude-auto`(기획안+task.md 보고 다음 작업 스스로 결정, 승인 1회) · `/claude-memory`(영구 기억 저장) · `/claude-fix`(이 시스템 자체 수정) · `/claude-stop`(즉시 중단)
- **안전 게이트**: `--allowedTools` 화이트리스트로 헤드리스 호출 도구 제한(대화형 세션엔 영향 없음). 목록 밖 작업·판단 필요한 선택지 제시는 전부 `NEEDS_APPROVAL:` → 디스코드 질문(버튼 or 답장) → `--resume`으로 세션 이어서 재개
- **오프라인 감지**: executor.py가 heartbeat 채널 메시지를 60초마다 edit(스팸 방지), relay_bot.py가 신선도(180초) 체크 후 명령 전달 전 "노트북 오프라인" 즉시 안내
- **FIFO 큐**: 여러 요청 연달아 보내도 순차 실행, busy 중 들어온 요청도 유실 없이 대기(마지막 처리 msg_id만 커서로 저장 — busy라 건너뛴 건 커서 전진 안 시킴)
- **Discord 네이티브 UI 활용**: 진행 중엔 typing indicator(8초마다 재트리거) + `⏳` progress embed, 완료 시 그 embed는 "완료" 마커로 edit하고 **실제 결과는 새 메시지로 전송**(Discord가 edit엔 알림을 안 주는 문제 회피), 승인 옵션 1~5개는 클릭 버튼(`custom_id=approve:{session}:{origin}:{번호}`)으로도 응답 가능
- **트랜지언트 에러 자동 재시도**: 529/503/rate_limit 패턴 감지 시 15초 대기 후 최대 3회 재시도(`claude_bridge.TRANSIENT_ERROR_PATTERNS`)
- **컨텍스트 유지**: quick 모드도 task.md를 읽고 답변(세션 새로 열려도 맥락 유지), "선택지 제시" 상황도 NEEDS_APPROVAL로 승격 + 항상 숫자 목록 강제(문자 A/B/C 금지 — 답장 매칭용)
- **네트워크 요청 타임아웃**(2026-07-22, `271aa6a`): `discord_bot.py`의 `urlopen`에 15초 타임아웃 추가 — 아래 버그 표 참고

## 🎥 영상 검증 파이프라인 (구현 완료 + 루프 흐름에 연결 완료, 커밋 대기)
- 흐름: Unity Recorder(`TestRecorder.cs`, `CoreEncoderSettings`) → `report_video.py`가 YouTube 업로드(`youtube_upload.py`, OAuth2) + agy/Gemini 영상 판단(`video_judge.py`, 격리 샌드박스 디렉터리에서 `--dangerously-skip-permissions`로 실행)을 한 번에 처리 → `/claude-loop`가 그 결과(URL+판정)를 `DONE:` 블록에 넣어 디스코드 보고(영상 링크 + 요약 + 다음 작업)
- 관련 파일(전부 uncommitted): `Assets/Scripts/Testing/TestRecorder.cs(.meta)`, `tools/video_judge.py`, `tools/youtube_auth_setup.py`, `tools/youtube_upload.py`, `tools/report_video.py`(신규), `tools/claude_bridge.py`/`tools/executor.py`(수정), `Recordings/`(생성물, 커밋 대상 아님)
- **미해결**: 이번에 만든/고친 파일들 커밋 여부 — 사용자에게 물어볼 것

## 🐞 겪은 버그 & 교훈
| # | 증상 | 원인 | 해결 |
|---|---|---|---|
| 1 | 승인 질문이 뜬금없이 강제됨 | 자체 검증 가능한 것도 무조건 질문, 순수 코드 옵션도 승인 옵션과 묶어 제시 | 프리앰블에 "자체 검증(Play+execute_code) 먼저, 코드만으로 되는 안전한 수정 우선 시도" 명시 |
| 2 | 여러 요청 보내면 나중 것이 씹힘 | busy 중 거부한 명령도 "처리됨"으로 커서 전진시켜 영구 유실 | FIFO 재작성: 실제로 시작된 명령까지만 커서 전진 |
| 3 | 결과/승인 와도 알림 안 옴 | progress embed를 그 자리에서 edit만 함(디스코드는 edit엔 push 알림 안 줌) | edit는 "완료" 짧은 마커로만, 실제 내용은 새 메시지로 전송 |
| 4 | 529 Overloaded가 그대로 최종 결과로 보고됨 | 트랜지언트 에러 재시도 로직 없었음 | 패턴 매칭 + 15초 대기 재시도(최대 3회) |
| 5 | executor 재시작하며 진행 중이던 작업을 실수로 죽임(2회) | Discord 큐 스냅샷만 보고 "안전하다" 판단, 실제 OS 프로세스 확인 안 함 | `Get-CimInstance Win32_Process`로 실제 실행중 프로세스(커맨드라인의 `permission-mode`, `ParentProcessId`) 확인 후에만 재시작 |
| 6 | **executor.py가 몇 시간째 완전 정지**(heartbeat도 멈춤, 큐 폴링도 안 됨) | `discord_bot.py`의 `urllib.request.urlopen`에 **타임아웃 없음** → 네트워크 요청 하나가 걸리면 단일 스레드 asyncio 이벤트 루프 전체가 무한정 블록(비동기 코드인데 동기 urllib를 직접 호출) | `urlopen(req, timeout=15)` 추가(`271aa6a`). 재발 방지 확인: 재시작 후 heartbeat 정상 재개 |
| 7 | 루프 작업이 30분 넘으면 그냥 죽고 진행상황(코드 수정·녹화 등) 전부 증발, "continue"로 재시도해도 새 세션이라 맥락 없음 | `run_claude`에 고정 타임아웃(1800s)이 있었는데, 영상 녹화·업로드·agy 판단 몇 번 겹치면 30분으로는 부족한 게 정상 | loop/loop_auto/resume 호출의 타임아웃을 아예 제거(`timeout=None`) - 대신 `/claude-stop`으로 수동 중단. (타임아웃 재도입 대비, 죽은 세션을 `.jsonl` 생성시각으로 복구하는 `_find_new_session_id`도 같이 추가해뒀지만 지금은 안 씀) |
| 8 | **executor.py 재시작할 때마다 예전에 이미 끝난 명령들이 유령처럼 재실행됨**(전혀 지시하지 않은 "더미 몬스터 만들까요?" 같은 승인이 뜸, `/claude-stop`해도 다음 폴링 주기에 그다음 옛날 명령을 또 집어서 처음부터 다시 시작 - 두더지잡기) | `heartbeat_loop`가 시작할 때 자기만의 `state` 사본을 한 번 로드해서, 60초마다 그 낡은 사본을 그대로 재저장 → `poll_loop`가 실제로 진행시킨 `last_queue_msg_id` 커서를 계속 "프로세스 시작 시점 값"으로 덮어씀. 프로세스가 살아있는 동안은 메모리상 진행이 정상이라 안 보이다가, **재시작 순간 디스크의 낡은 커서를 읽어서 이미 끝난 옛 메시지들을 처음부터 다시 훑는다.** 오늘 여러 번 재시작하면서 정확히 이 상황을 만들어냄. | `main_async`에서 `state` 딕셔너리 하나를 만들어 `poll_loop`/`heartbeat_loop`에 그대로(참조로) 넘김 - 각자 사본을 만들지 않으므로 어느 쪽이 저장해도 서로의 최신 진행상황을 덮어쓸 수 없음. 발견 당시 이미 커서가 몇 시간 뒤처져 있어서, 재시작 전에 큐 채널의 실제 최신 메시지 ID로 `executor_state.json`을 수동으로 맞춰준 뒤 재시작. |

## ⚠️ 알려진 한계 / 열린 항목
- **agy 폴백 체인 (미착수)**: 사용량/한도 초과 시 코딩은 opus→gemini 3.1 pro→gemini 3.5 flash, 검증은 gemini 3.1 pro→gemini 3.5 flash 순으로 자동 전환하는 일반 정책. **agy의 권한 스코핑이 `--dangerously-skip-permissions`(전체 허용) 아니면 전부 거부만 있어서, `--allowedTools`급 세밀 제어가 안 됨** — 이 문제 어떻게 풀지 먼저 사용자와 상의 필요. 착수 순서: "지금 되는 것부터 안정화 → 그 다음 폴백 체인" (사용자 지시).
- **dishost.kr 자동 재배포 안 됨**(2회 확인) → `bot-deploy` 바뀔 때마다 수동 재배포 필수
- dishost 무료 티어는 7일마다 수동 연장 필요
- 버튼 클릭 → 승인 응답 흐름을 실제 왕복으로 아직 라이브 검증 못함(코드상으론 완료)

## 🗂️ 관련 파일
- `tools/relay_bot.py` — dishost.kr 배포 대상(게이트웨이, 슬래시커맨드, 버튼 인터랙션)
- `tools/executor.py` — 노트북 상주(폴링, 커맨드 실행, heartbeat)
- `tools/claude_bridge.py` — 헤드리스 `claude -p` 공통 호출 로직(allowedTools 프로필, 프리앰블, 재시도)
- `tools/discord_bot.py` — REST 클라이언트(executor.py 전용, relay_bot.py는 안 씀)
- `tools/video_judge.py` / `tools/youtube_upload.py` / `tools/youtube_auth_setup.py` — 영상 검증 파이프라인
- `tools/.secrets/discord_config.json` — 로컬 시크릿(gitignored), 배포 환경은 env var로 주입
- `tools/.secrets/executor_state.json` — 큐 커서/heartbeat 메시지ID 영속화

# 루프 엔지니어링 — 디스코드 원격 제어 시스템 (작업 현황)

> task.md에서 분리(2026-07-22). 게임 개발 진행상황은 task.md, **이 시스템 자체의 구조·상태·이슈**는 여기.
> 최종 업데이트: 2026-07-26 · 배포 기준 origin/main `271aa6a` / origin/bot-deploy `7ca9ef1`

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
- **부팅 자동 시작**(2026-07-26): 작업 스케줄러 태스크 `RemnentsOfLight-Executor`. 로그온 **30초 후**(부팅 직후 네트워크 스택 대기) `pythonw.exe -u tools\executor.py` 실행, 작업 디렉터리는 프로젝트 루트. 기본값으로 두면 안 되는 설정 4개를 명시함: `MultipleInstances=IgnoreNew`(두 프로세스가 같은 큐를 먹는 사고 차단) · 배터리에서도 시작·유지(기본값은 둘 다 반대 — 노트북이라 그냥 두면 안 뜸) · 실행시간 제한 해제(기본 3일 제한이 상주 프로세스를 잘라냄) · 실패 시 1분 간격 3회 재시작. 수동 기동은 `Start-ScheduledTask -TaskName RemnentsOfLight-Executor`
  - **`python.exe`+cmd 래퍼가 아니라 `pythonw.exe`인 이유는 아래 버그 #11** — 콘솔 창을 만들지 않기 위해서다. 그 대가로 작업 스케줄러 Exec 액션은 리다이렉트(`>>`)를 못 쓰므로, `executor.py`가 `__main__`에서 `sys.stdout`/`sys.stderr`를 직접 `tools/.secrets/executor.log`로 돌린다(append + 라인 버퍼링). 잡히지 않은 트레이스백도 여기 남는 것까지 확인함
  - 태스크의 `Hidden` 설정은 **끄는 게 맞다**. 이건 창을 숨기는 옵션이 아니라 작업 스케줄러 라이브러리 목록에서 항목을 숨기는 옵션이라, 켜두면 나중에 이 태스크를 찾지 못한다

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
| 9 | 몇 시간 만에 월 사용한도(spend limit) 소진 + 그 이후 매 재시도가 완전히 새 세션이라 맥락이 계속 날아감 | (a) `_run_claude_once`에 `--model`을 안 넘겨서 이 머신 전역설정(`~/.claude/settings.json`: `opus`+`xhigh`)을 그대로 물려받음 - 원격 루프 전용으로는 과함. (b) `DONE:` 마커 없이 끝난 결과(스펜드 한도 에러 등 실패)는 CLI가 세션ID를 정상 반환했는데도 무조건 `pending:false`로 처리 - 재시도가 매번 새 세션으로 시작됨 | 헤드리스 호출에 `--model claude-sonnet-5` 명시(전역 설정은 안 건드림). `done`이 없어도 `session_id`가 있으면 승인 대기와 동일하게 `pending:true` - 답장하면 그 세션 그대로 이어감 |
| 10 | 저녁에 디스코드에서 명령을 보내니 계속 "노트북 오프라인 / 하트비트가 없어" | **코드 문제 아님.** PC가 비정상 종료(시스템 이벤트 41 + 6008, 2026-07-26 15:48경)되면서 executor.py도 같이 사망 → 20:29에 재부팅됐지만 **자동 시작이 없어서 그대로 정지 상태로 방치**. `executor.log`는 0바이트라 단서도 없었는데, 이건 stdout을 파일로 리다이렉트하면 파이썬이 버퍼링을 하기 때문 — 전원이 나가며 flush를 못 해서 유언조차 안 남음 | 작업 스케줄러 `RemnentsOfLight-Executor` 등록(위 "완료된 기능" 참고). 실행을 `python -u`(버퍼링 제거)로 바꾸고 로그는 `>>` append로 변경 — 다음엔 죽은 이유가 파일에 남음. 검증은 수동 프로세스를 내린 뒤 `Start-ScheduledTask`만으로 다시 올려서 heartbeat 신선도 재확인(age 17.8s < 180s). 진단 순서: ① `Get-CimInstance Win32_Process`로 프로세스 존재 확인 ② `executor_state.json` 타임스탬프로 마지막 생존 시각 추정 ③ 시스템 이벤트 41/6008로 크래시 여부 확인 |
| 11 | 위 #10을 고치려고 등록한 자동 시작 태스크가 **4분 만에 죽음**. 이후에도 "갑자기 아무 이유 없이" 반복 | **화면에 뜬 빈 검은 콘솔 창을 사용자가 껐다.** 태스크 액션이 `cmd.exe`였고, 대화형 세션에 콘솔 창이 그대로 떴는데 `executor.py`는 에러가 없으면 아무것도 출력하지 않으므로 **정체불명의 빈 터미널**로 보였다. 콘솔 창을 닫으면 `CTRL_CLOSE_EVENT` → 파이썬은 `0xC000013A(STATUS_CONTROL_C_EXIT)`로 종료. 태스크의 `Hidden=true`를 창 숨김으로 오해한 것이 원인 — 그 옵션은 **라이브러리 목록에서 항목을 숨길 뿐 창과 무관**하다. 진단이 오래 걸린 이유: `poll_loop`/`heartbeat_loop`가 `except Exception`으로 모든 예외를 삼켜서 "일반 예외로는 죽을 수 없는 코드"였고, `KeyboardInterrupt`가 `BaseException`이라 그 그물을 빠져나간다는 점까지는 좁혔지만 **누가 보냈는지는 사용자가 "이상한 터미널 껐다"고 말해줄 때까지 알 수 없었다**(작업 스케줄러 운영 로그가 Windows 기본값으로 꺼져 있어 증거가 없었음) | 태스크 액션을 `pythonw.exe` 직접 실행으로 교체 — 콘솔을 아예 할당하지 않으므로 닫을 창이 존재하지 않는다. 로그는 `executor.py`가 직접 파일로 씀(위 "완료된 기능" 참고). 검증: 태스크로 기동 후 conhost/cmd 신규 생성 **0개**, heartbeat PASS. 교훈 — **상주 프로세스를 대화형 세션에 콘솔과 함께 띄우지 마라. 사용자에게 보이는 창은 언젠가 닫힌다** |

## ⚠️ 알려진 한계 / 열린 항목
- **agy 폴백 체인 (미착수)**: 사용량/한도 초과 시 코딩은 opus→gemini 3.1 pro→gemini 3.5 flash, 검증은 gemini 3.1 pro→gemini 3.5 flash 순으로 자동 전환하는 일반 정책. **agy의 권한 스코핑이 `--dangerously-skip-permissions`(전체 허용) 아니면 전부 거부만 있어서, `--allowedTools`급 세밀 제어가 안 됨** — 이 문제 어떻게 풀지 먼저 사용자와 상의 필요. 착수 순서: "지금 되는 것부터 안정화 → 그 다음 폴백 체인" (사용자 지시).
- **dishost.kr 자동 재배포 안 됨**(2회 확인) → `bot-deploy` 바뀔 때마다 수동 재배포 필수
- dishost 무료 티어는 7일마다 수동 연장 필요
- 버튼 클릭 → 승인 응답 흐름을 실제 왕복으로 아직 라이브 검증 못함(코드상으론 완료)
- **자동 시작은 "로그온" 트리거라 잠금화면에서는 안 뜬다**: 크래시 후 재부팅돼도 아무도 로그인하지 않으면 executor는 여전히 죽은 상태 = 외출 중 원격 제어의 마지막 구멍. 해결하려면 S4U(`로그온 여부와 무관하게 실행`, 암호 저장 불필요)로 바꿔야 하는데 세션 0에서 돌게 되어 헤드리스 `claude`·Unity MCP 연결이 지금과 다르게 동작할 위험이 있음 → **실제 왕복 테스트 후에만 전환할 것**(현재는 수동 실행과 100% 동일한 대화형 세션 조건을 택함)
- 태스크가 떠 있는 상태에서 `executor.py`를 수동으로 또 띄우면 **두 프로세스가 같은 큐를 먹는다**(`IgnoreNew`는 태스크 자신의 중복 실행만 막음)
- `executor.log`는 append라 무한정 자람 — 커지면 수동으로 잘라낼 것
- **배터리 + 뚜껑 닫힘 = 하트비트 끊김 (미해결, 아직 실제 사고는 없음)**: 이 노트북은 S1/S2/S3를 펌웨어가 지원하지 않아 **Modern Standby(S0)만** 가능하다. 덮개 동작이 AC=0(아무것도 안 함)/**DC=1(절전)** 이라, 배터리 상태로 뚜껑을 닫으면 Modern Standby에 들어가고 Windows가 정책적으로 네트워크를 끊는다(2026-07-26 20:43:55 `Reason: Lid` 진입 → 20:44:01 `Adaptive Connected Standby` 연결 해제 → 20:57:08 `Policy Setting` 해제 → 21:13:49 뚜껑 열며 복귀, **약 30분간 네트워크 사망**). 하필 **원격 제어를 쓰는 상황 = 뚜껑 닫고 나간 상황**이라, 집에서 뚜껑 열고 테스트하면 절대 재현되지 않는다. 당장의 회피책은 **외출 시 충전기를 꽂아두는 것**(AC에선 덮개 동작이 이미 0이라 안 잠듦). 근본 해결은 DC 덮개 동작도 0으로 바꾸는 것인데 가방 속 발열·배터리 소모를 감수해야 해서 보류 — 사용자 판단 대기
- **하트비트 여유가 두 박자뿐 (구조적 취약점)**: `HEARTBEAT_INTERVAL_S=60` vs relay의 `HEARTBEAT_STALE_SECONDS=180`. 그런데 `executor.py`의 모든 `discord_bot.*` 호출이 **동기 urllib를 async 이벤트 루프에서 직접 호출**하는 구조라(버그 #6과 같은 계열, 그때는 타임아웃만 붙이고 구조는 그대로 뒀음), 특히 `_request`가 429를 만나면 `time.sleep()`으로 루프 전체를 블록한다. 명령 처리 중엔 타이핑 인디케이터(8초)+진행 embed+결과 전송이 겹쳐 호출이 몰리므로, **프로세스가 멀쩡히 일하는 중에도 하트비트가 180초를 넘겨 "오프라인"으로 보일 수 있다.** 고치려면 blocking 호출을 `asyncio.to_thread`로 감싸야 함 — 미착수

## 🗂️ 관련 파일
- `tools/relay_bot.py` — dishost.kr 배포 대상(게이트웨이, 슬래시커맨드, 버튼 인터랙션)
- `tools/executor.py` — 노트북 상주(폴링, 커맨드 실행, heartbeat)
- `tools/claude_bridge.py` — 헤드리스 `claude -p` 공통 호출 로직(allowedTools 프로필, 프리앰블, 재시도)
- `tools/discord_bot.py` — REST 클라이언트(executor.py 전용, relay_bot.py는 안 씀)
- `tools/video_judge.py` / `tools/youtube_upload.py` / `tools/youtube_auth_setup.py` — 영상 검증 파이프라인
- `tools/.secrets/discord_config.json` — 로컬 시크릿(gitignored), 배포 환경은 env var로 주입
- `tools/.secrets/executor_state.json` — 큐 커서/heartbeat 메시지ID 영속화
- `tools/.secrets/executor.log` — executor stdout/stderr(작업 스케줄러가 append). 파일이 아니라 OS에 사는 항목: 작업 스케줄러 태스크 `RemnentsOfLight-Executor`(`Get-ScheduledTask`/`Get-ScheduledTaskInfo`로 확인)

# Remnents of Light — 미니멀 코어 세팅 계획 (Claude Code 실행용)

> 이 문서는 Claude Code가 직접 읽고 순서대로 실행하기 위한 세팅 계획서다.
> 목표는 **자율 무인 루프가 아니라**, 노트북 1대에서 대화형으로 도는 Claude Code의
> 개발 품질을 올리는 "미니멀 코어" + 폰 비동기 관제 + 자기 성장(스킬·조회) 습관을 까는 것이다.
> 각 단계는 **STOP 지점**에서 사용자 확인을 받고 넘어간다.
>
> 구성: **① CLAUDE.md 모드 분리(+웹/docs 조회 강화) · ② hooks · ③ ASSERT 로그 규약 ·
> ④ 입력 주입 테스트 · ⑤ 디스코드 비동기 관제 · ⑥ 스킬 자가생성 규칙.**

---

## 0. 컨텍스트 (읽고 시작할 것)

### 프로젝트 사실
- 엔진: **Unity 6000.3.10f1**, **URP 17.3**, **New Input System 1.19**, **Test Framework 1.6**
- 장르: **산나비 스타일 2D 액션 플랫포머** (벽점프·코요테타임·방 단위 고정 카메라). 탑다운 아님.
- 그라운딩: `com.coplaydev.unity-mcp` 이미 manifest 등록됨.
- 타일맵: `.tmx` (Tiled) + SuperTiled2Unity 2.3.0. → **텍스트(XML)라 비전 없이 직접 읽고 편집 가능.**
- 씬: `Assets/Scenes/SampleScene.unity` (단일)
- 입력 액션: `Assets/PlayerActions.inputactions` — 맵 `Player`, 액션 `Move / Jump / Dash / Attack / Parry`
- 플레이어: `Assets/Scripts/PlayerController.cs` — Rigidbody2D 기반, `moveSpeed / jumpForce / coyoteTime(0.1) / wallJumpForce` 등 public 필드.
- 다음 개발 작업(task.md 기준): **무적 대시(I-Frame) → 일섬 → 3타 콤보/패링 → 타격 피드백 UI**

### 하드웨어 제약 (중요)
- 가용 기기: **노트북 1대(유일한 작업대) + 아이패드 + 갤럭시 폰**.
- 별도 GPU 머신 없음 → 로컬 LLM 추론 티어(CLID) **범위 밖**.
- 노트북이 개발 머신이자 유일한 머신 → **무인 while 루프 범위 밖.**
- 폰/패드는 Unity·에이전트 실행 불가 → **디스코드로 진행 보고 수신만.**

### 이 세팅이 만드는 것 / 안 만드는 것
| 만든다 (미니멀 코어) | 지금 안 만든다 (게임이 필요를 증명하면 그때) |
|---|---|
| ① CLAUDE.md 모드 분리 + 웹/docs 조회 강화 | 무인 오케스트레이터(while 루프) |
| ② hooks 가드레일 | 상주 Hermes 봇(실시간 원격 조작) |
| ③ `[ASSERT]` 로그 규약 | L4 플레이 영상 판정 |
| ④ 입력 주입 플레이 테스트 | 리서치→스킬 증류 **자동** 파이프라인 |
| ⑤ 디스코드 비동기 관제(보고 push + 답변 pull) | 로컬 추론 티어 |
| ⑥ 스킬 자가생성 규칙 (반복→스킬 증류) | 교차 벤더 라우팅 |

> ⑤·⑥ 주의: **상주 봇**과 **자동 증류 파이프라인**은 안 만든다. 대신 봇 없이 도는 비동기 관제(파일 pull)와,
> 사람이 개발하다 반복을 감지하면 그 자리에서 스킬로 굳히는 규칙만 넣는다. 상시 프로세스가 필요 없어 노트북 1대 제약과 맞는다.

---

## 1. 사용자가 미리 준비할 것 (Claude Code가 못 하는 것)

Claude Code는 아래 2개를 만들 수 없다. 시작 전 사용자에게 요청하고, 없으면 해당 단계를 건너뛴다.

1. **디스코드 웹훅 URL** — 디스코드 서버 → 채널 설정 → 연동 → 웹훅 → URL 복사.
   (5단계 push에서 필요. 없으면 5단계 스킵하고 나머지는 진행.)
   - (선택) **디스코드 봇 토큰** — 폰 답변을 자동 pull하려면 필요. 없으면 수기 pull(채널 답 복사→answers.jsonl)로 대체.
2. **Unity 에디터에서 MCP 브릿지 ON 확인** — 에디터가 열려 있고 unity-mcp 서버가 연결된 상태.
   (4단계 플레이 검증에서 필요. 코드 생성 자체는 MCP 없이 가능.)
   - (선택) **`context7` 등 문서 MCP** — 붙이면 STEP 1의 Unity docs 조회가 더 정확. 없으면 내장 WebSearch로 대체.

> 비밀값은 코드/커밋에 넣지 말 것. 웹훅 URL은 `.gitignore`에 포함된 로컬 파일로만 다룬다.

---

## 2. 실행 순서 개요

```
STEP 1  CLAUDE.md 모드 분리 + 조회강화 → .claude/CLAUDE.md 개정      [STOP: 개정안 리뷰]
STEP 2  hooks 가드레일                → .claude/settings.json       [STOP: 차단 규칙 리뷰]
STEP 3  [ASSERT] 로그 규약            → Assets/Scripts/TestLog.cs    [STOP: 규약 리뷰]
STEP 4  입력 주입 플레이 테스트        → Assets/Scripts/Testing/*.cs  [STOP: MCP로 플레이 검증]
STEP 5  디스코드 비동기 관제 (선택)    → tools/report.py + inbox      [STOP: 왕복 테스트]
STEP 6  스킬 자가생성 규칙            → .claude/skills/ + CLAUDE.md   [STOP: 첫 스킬 예시 리뷰]
```

각 STEP 끝의 **[STOP]** 에서 멈추고 사용자 확인을 받은 뒤 다음으로 간다. (AGENTS.md 규칙 6 준수)

---

## STEP 1 — CLAUDE.md 모드 분리

### 문제
현재 `.claude/CLAUDE.md`와 `.agents/AGENTS.md`의 규칙 6("매 단계마다 사용자에게 허가를 받아라")은
대화형 세션엔 좋지만, 검증 기반 흐름을 막는다. 삭제가 아니라 **두 모드로 분리**한다.

### 할 일
`.claude/CLAUDE.md`를 편집해 상단에 **모드 선언** 섹션을 추가한다. 기존 규칙·코딩 규칙은 보존한다.

추가할 내용(요지):

- **대화 모드 (기본)**: 기존 규칙 6 그대로 — step-by-step, 매 단계 허가.
- **루프 모드 (사용자가 "루프 모드로"라고 명시할 때만)**:
  - 허가 대신 **검증 통과**를 게이트로 삼는다 (L1→L2 순).
  - 파괴적 작업(삭제·커밋·리팩터·씬/메타/tmx 편집)은 루프 모드에서도 **반드시 질문**.
  - 한 번에 **하나의 태스크**만. 검증 실패 3회 시 중단하고 상황 보고.
  - 작업 로그를 `task.md`에 갱신 (기존 형식 유지: 완료/버그/결정/순서).

기존 코딩 규칙 4(Goal-Driven Execution)를 루프 모드의 기본 동작으로 승격:
"성공 기준을 assertion 로그로 정의하고, 통과할 때까지 반복."

### 함께 넣을 것 — 웹/Unity docs 조회 강화 (조회 우선 규칙)
기존 규칙 8("애매할 때 웹서핑")은 너무 소극적이다. Unity 6은 최신이라 모델 지식이 낡았을 수 있으므로
**추측 대신 조회**를 기본값으로 격상한다. CLAUDE.md에 아래 규칙을 추가한다:

- **버전 고정**: Unity API는 **6.x(6000.x) 기준**으로만 작성한다. 검색·조회 시 2021/2022/2023 LTS 문서가 나오면
  무시하고 `docs.unity3d.com`의 최신(6000.x) 페이지를 우선한다. URP·Input System·Tilemap도 같은 원칙.
- **조회 트리거(추측 금지)**: 다음 상황에선 반드시 웹/docs를 조회한 뒤 코드를 쓴다 —
  ① API 시그니처·enum·기본값이 확실치 않을 때 ② 최근 바뀐 기능(Input System, URP 2D, STU)일 때
  ③ 서드파티 패키지(SuperTiled2Unity 등) 사용법 ④ 에러 메시지의 원인이 불명확할 때.
- **출처 남기기**: 조회로 확정한 비자명한 사실은 코드 주석이나 `task.md`에 출처 URL을 한 줄 남긴다
  (나중에 재검증·스킬 증류에 쓰인다 → STEP 6).
- **도구 우선순위**: 내장 WebSearch→WebFetch가 기본. `context7` 등 문서 MCP가 연결돼 있으면 API 레퍼런스는 그쪽을 우선.
- **조회해도 불확실하면 STOP**: 상충하는 정보뿐이면 추측해 진행하지 말고 사용자에게 선택지를 제시한다.

### 검증
- `.claude/CLAUDE.md`에 모드 선언 + 조회 강화 규칙이 추가되고 기존 내용이 보존됐는지 diff로 확인.
- **[STOP]** 사용자에게 개정안 diff를 보여주고 승인받는다. `.agents/AGENTS.md`에도 같은 변경을 반영할지 질문.
  (`context7` 등 문서 MCP를 붙일지도 이때 함께 확인.)

---

## STEP 2 — hooks 가드레일

### 목적
자율성이 늘수록 실수 비용이 커진다. Claude Code hooks로 **깨지기 쉬운 파일의 직접 텍스트 편집을 차단**한다.
(이 파일들은 Unity/STU가 생성·관리하므로 에디터나 MCP를 거쳐야 안전하다.)

### 할 일
`.claude/settings.json`에 `PreToolUse` 훅을 추가한다. Edit/Write 계열 도구가 아래 경로를 건드리면 차단하고
사용자에게 사유를 알린다.

**차단 대상 (직접 편집 금지):**
- `*.unity` (씬 파일)
- `*.meta` (메타 파일)
- `*.tmx`, `*.tsx` (Tiled 맵/타일셋 — STU가 임포트 관리)
- `*.inputactions` (입력 액션 에셋 — 에디터로 편집)
- `*.asset`, `*.prefab` (직렬화 에셋)

**허용:** `Assets/**/*.cs`, `*.md`, `.claude/*`, `tools/*` 등 일반 텍스트/코드.

구현 방식: 훅 스크립트(bash 또는 python)가 도구 입력의 파일 경로를 검사해 차단 패턴이면
비-제로 종료 + 사유 메시지. `settings.json`에서 이 스크립트를 PreToolUse에 등록.

> 주의: MCP 경유 작업은 파일시스템이 아니라 Unity API를 거치므로 이 훅을 우회한다.
> 따라서 "씬 조작은 MCP로만, 직접 파일 편집은 금지"라는 경계를 STEP 1의 CLAUDE.md 규칙으로도 이중 명시한다.

### 검증
- 훅 등록 후, 시험 삼아 `SampleScene.unity`를 Edit 시도 → 차단되는지 확인.
- `Assets/Scripts/PlayerController.cs` Edit 시도 → 정상 통과하는지 확인.
- **[STOP]** 차단/허용 목록을 사용자에게 보여주고 조정받는다.

---

## STEP 3 — `[ASSERT]` 로그 규약

### 목적
게임의 "검증 언어"를 만든다. `Debug.Log` 스팸이 아니라, 파싱 가능한 **구조화된 assertion 로그**로
수락 기준이 곧 기계 판정이 되게 한다. (L2 검증의 핵심 부품)

### 할 일
`Assets/Scripts/TestLog.cs` (정적 헬퍼) 생성:

- `TestLog.Event(string channel, string msg)` → 콘솔에 `[EVENT] channel: msg` 형식 출력.
- `TestLog.Assert(string name, bool pass, string detail="")` → `[ASSERT] name: PASS/FAIL detail`.
- `TestLog.Step(string scenario, string step)` → `[STEP] scenario: step` (테스트 봇용).
- 모든 출력에 프레임/타임코드 접두사 포함: `[T=00:03.20 f=192]` 형식.
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 가드로 릴리스 빌드에서 제외.

**규약 문서화:** `docs/dev/ASSERT_CONVENTION.md`를 만들어 채널명·네이밍 규칙을 정의한다.
예시 채널(다음 작업 대응):
- `dash_iframe` — 대시 무적 프레임 동안 피격 0회
- `combo_window` — 3타 콤보 입력 윈도우 내 연결
- `parry_timing` — 패링 유효 프레임 판정
- `room_transition` — 방 전환 시 세이브 트리거

### 검증
- `TestLog.cs` 컴파일 클린 확인 (에디터 콘솔 에러 0).
- **[STOP]** 규약 문서와 채널 네이밍을 사용자에게 확인. 다음 작업(대시)에 쓸 채널명 합의.

---

## STEP 4 — 입력 주입 플레이 테스트

### 목적
LLM은 실시간 조작을 못 한다. 대신 **의미 단위 시나리오를 봇이 프레임 입력으로 변환**해
재현 가능한 플레이 테스트를 만든다. 산나비형 플랫포머는 점프 아크·타이밍 검증이 핵심이라 이게 필수다.

### 할 일 (2개 파일)

**A. `Assets/Scripts/Testing/InputInjector.cs`**
- New Input System의 `InputTestFixture` 또는 런타임 가상 디바이스로 가상 Keyboard/Gamepad 생성.
- `Player` 맵의 `Move / Jump / Dash / Attack / Parry`에 이벤트 주입하는 메서드 제공.
- OS 레벨 키 에뮬레이션(SendInput) 쓰지 말 것 — 포커스/타이밍으로 깨진다.

**B. `Assets/Scripts/Testing/PlayTestRunner.cs`**
- 시나리오(스텝 리스트)를 코루틴으로 실행. 각 스텝마다 `TestLog.Step(...)` 기록.
- 첫 시나리오 `DashIFrameTest` (다음 개발 작업과 직결):
  1. 플레이어 스폰 위치 확인 → `[STEP] dash_iframe: spawned`
  2. 대시 입력 주입 → 대시 지속(0.1s 등 실제 값) 동안 데미지 소스에 노출
  3. 무적 프레임 동안 피격 0회 검증 → `TestLog.Assert("dash_iframe", hits==0)`
  4. 대시 종료 후 정상 피격 복귀 검증
- `[MenuItem("Tools/PlayTest/Dash I-Frame")]`로 에디터에서 트리거 가능하게.

> 대시 시스템이 아직 없다면: 이 STEP은 **테스트 하네스(InputInjector + Runner + MenuItem 골격)만** 깔고,
> `DashIFrameTest`는 대시 구현 시 채우도록 TODO로 남긴다. 하네스 자체가 이후 모든 전투 기능의 검증 토대가 된다.

### 검증
- 두 파일 컴파일 클린.
- **[STOP]** 에디터에서 MCP로 Play 진입 → `Tools/PlayTest` 메뉴 실행 → 콘솔에 `[STEP]`/`[ASSERT]` 로그가
  의도대로 찍히는지 사용자와 함께 확인. (MCP 미연결 시: 사용자가 수동으로 메뉴 실행 후 로그 캡처.)

---

## STEP 6 — 스킬 자가생성 규칙

### 목적
스킬을 **미리 다 만들지 않는다.** 대신 개발하다 같은 종류의 작업이 반복되면, 그 절차를 그 자리에서
스킬로 굳혀 다음부터 참조하게 한다. 반복이 스킬을 낳는 구조라 잼처럼 시간 없을 때도 부담이 없고,
루프를 돌릴수록 라이브러리가 스스로 자란다. (자동 증류 파이프라인이 아니라 **사람이 개발 중 트리거하는 규칙**.)

### 할 일

**A. 스킬 디렉터리 + 첫 예시**
- `.claude/skills/` 생성. 각 스킬은 `.claude/skills/<name>/SKILL.md` 한 파일로 시작(과설계 금지).
- 형식(짧게): `## 언제 쓰나(트리거)` / `## 절차(단계)` / `## 검증(ASSERT 채널)` / `## 출처(있으면)`.
- 첫 예시 하나를 다음 개발 작업 기준으로 만든다 — `.claude/skills/add-combat-move/SKILL.md`:
  "새 전투 동작(대시·일섬·콤보·패링류) 추가 절차": 입력 액션 확인 → PlayerController 상태 훅 →
  `[ASSERT]` 채널 정의 → PlayTestRunner 시나리오 추가 → docs 조회 규칙 준수. (STEP 3·4와 연결)

**B. CLAUDE.md에 자가생성 규칙 추가 (STEP 1 문서에 편입)**
- **트리거**: "같은 종류의 절차를 **세 번째** 수행하고 있다고 느끼면, 진행을 멈추고
  `.claude/skills/<name>/SKILL.md`로 그 절차를 증류할지 사용자에게 제안하라."
- **증류 내용**: 뜬구름 금지. 실제 파일 경로·명령·ASSERT 채널·주의점 등 **실행 가능한 단계**로.
- **웹 조사 연동(STEP 1)**: 폴리싱/콘텐츠 지식이 필요하면 먼저 웹/docs를 조회해 규칙·수치로 요약한 뒤
  스킬에 넣는다. 조회 출처 URL을 스킬 `## 출처`에 남긴다. (예: "타격 피드백 = 히트스톱 0.04~0.08s + 2프레임 플래시…")
- **소비**: 이후 같은 유형 작업은 조사·즉흥 대신 해당 스킬을 먼저 읽고 따른다.
- **정리**: 스킬이 중복·모순되면 통합·삭제를 제안(라이브러리 비대화 방지).

**C. 취향 앵커 (선택, 1회)**
- 폴리싱 방향이 필요하면 `docs/dev/PILLARS.md`에 레퍼런스 게임 2~3개만 적어둔다
  (예: 타격감=산나비, 이동감=…). 스킬 증류 시 이 방향과 정합하는지 기준으로 삼는다.
  지식은 조회가 공급하고, 취향만 사람이 한 번 공급하는 분업.

### 검증
- `.claude/skills/add-combat-move/SKILL.md` 초안이 실행 가능한 단계로 쓰였는지 확인.
- CLAUDE.md에 트리거 규칙이 들어갔는지 diff 확인.
- **[STOP]** 첫 스킬 예시를 사용자에게 보여주고, 트리거 임계값(3회가 적절한지)·PILLARS 작성 여부를 확인.

---

## STEP 5 — 디스코드 비동기 관제 (선택)

### 목적
폰/패드로 **비동기 양방향** 관제를 한다. 상주 봇(Hermes)이 아니라, 노트북이 켜져 있을 때만 도는
경량 스크립트로 — 진행을 **push**하고, 폰에서 남긴 답을 다음 세션에 **pull**한다. 실시간은 아니지만
"이동 중에 결정 남겨두면 노트북 앞에 앉을 때 Claude Code가 읽어감"이 된다. (웹훅 URL 없으면 이 STEP 스킵.)

> 왜 상주 봇이 아닌가: 노트북이 유일한 작업대라 24시간 봇 프로세스를 못 띄운다. 대신 파일 기반 pull로
> 봇 없이 양방향의 8할을 얻는다. 진짜 Hermes(실시간)는 상시 서버/GPU 머신이 생기면 그때 승격.

### 할 일

**A. `tools/report.py` — 보고 push (노트북 → 폰)**
- 요약 텍스트 + (선택)스크린샷/영상을 디스코드 웹훅으로 POST.
- 질문을 보낼 때는 **선택지를 번호로** 붙여 보낸다(폰에서 답하기 쉽게):
  예) `"[Q1] 대시 무적 0.1s vs 0.15s? 1) 0.1  2) 0.15  (추천: 2)"`
- 웹훅 URL은 하드코딩 금지 → `tools/.secrets/webhook` 로컬 파일 또는 환경변수.

**B. `tools/inbox.py` + `tools/inbox/answers.jsonl` — 답변 pull (폰 → 노트북)**
- 폰에서 답을 주는 경로는 두 가지 중 택1(사용자 편의대로):
  - **간단**: 디스코드 채널에 답을 적어두고, 나중에 사용자가 그 텍스트를 복사해
    `tools/inbox/answers.jsonl`에 붙여넣기(수기). 스크립트는 이 파일을 파싱.
  - **자동**: 디스코드 봇 토큰이 있으면 `inbox.py`가 채널 최근 메시지를 읽어 answers.jsonl로 저장.
    (봇 토큰도 비밀값 → `.secrets/`.) 상주 아님 — 세션 시작 시 1회 실행.
- 형식: `{"id":"Q1","answer":"2","ts":"..."}` 한 줄씩.

**C. CLAUDE.md 연동 — 세션 시작 규칙**
- STEP 1의 CLAUDE.md에 한 줄 추가: **"세션 시작 시, 그리고 blocked 상태 해소가 필요할 때
  `tools/inbox/answers.jsonl`을 읽어 미처리 답변을 반영하라."**
- 이게 "폰에서 남긴 답을 다음 세션에 읽어감"을 성립시키는 연결고리다.

**D. `.gitignore` 갱신**
- `tools/.secrets/` 와 `tools/inbox/` 추가 (비밀값·개인 답변 커밋 방지).

### 검증
- push: `python tools/report.py "test [Q1] 1) A 2) B"` → 디스코드에 도착 확인.
- pull: answers.jsonl에 `{"id":"Q1","answer":"2"}` 넣고 → Claude Code가 세션에서 읽어 반영하는지 확인.
- `.secrets/`·`inbox/`가 git status에 안 뜨는지 확인.
- **[STOP]** 왕복(push→답변→pull 반영) 한 번 성공하는지 사용자와 확인.

---

## 3. 완료 후 상태

세팅이 끝나면 이렇게 된다:

- 대화 모드로 평소처럼 개발하되, `.claude/CLAUDE.md`가 검증 기반 흐름을 알고 있음.
- Unity 6.x API를 추측 대신 **조회**로 확정하고, 출처를 남김.
- 씬/메타/tmx 등 위험 파일은 hooks가 실수로부터 보호.
- 새로 만드는 전투 기능마다 `[ASSERT]` 로그를 심어 L2 검증이 게임과 함께 성장.
- 대시·콤보·패링을 `PlayTestRunner` 시나리오로 재현 가능하게 테스트.
- 작업 진행을 폰으로 push하고, 폰에서 남긴 답을 다음 세션이 pull해 반영(비동기 관제).
- 반복되는 절차가 그때그때 `.claude/skills/`로 굳어 라이브러리가 스스로 자람.

**안 한 것**(의도적): 무인 루프, 상주 봇, 자동 증류 파이프라인, 영상 판정. 이것들은 GPU 머신이 생기거나
잼 이후 프로젝트가 계속될 때, task.md → tasks.json 기계화부터 얹으면 된다.

---

## 4. 실행 지시 (사용자가 Claude Code에 줄 프롬프트)

> 이 문서를 읽고, STEP 1부터 6까지 순서대로 세팅해줘. 각 STEP의 [STOP]에서 멈추고 나한테 확인받아.
> 시작 전에 디스코드 웹훅 URL(+선택: 봇 토큰), Unity 에디터 MCP 연결 상태, 문서 MCP 연결 여부부터 물어봐줘.
> 파괴적 작업(삭제·커밋)은 하지 말고, 새 파일 생성과 CLAUDE.md/settings.json/gitignore/skills 편집만 해.
> Unity API는 6.x 기준으로만, 시그니처가 애매하면 추측 말고 docs를 조회해서 출처를 남겨줘.

# 새 전투 동작 추가 (대시 · 일섬 · 콤보 · 패링류)

## 언제 쓰나(트리거)
`PlayerController.cs`에 새 전투/이동 동작을 하나 추가할 때 (예: 대시, 일섬, 3타 콤보, 패링).
같은 절차를 세 번째 반복하고 있다고 느끼면 이 스킬을 갱신할지 검토한다.

## 절차(단계)

1. **입력 액션 확인** — `Assets/PlayerActions.inputactions`의 `Player` 맵에 해당 액션이 이미 있는지 확인한다.
   현재 정의된 액션: `Move / Jump / Dash / Attack / Parry`. 지금은 `Move`, `Jump`만 코드에 연결돼 있다
   (`PlayerController.cs`의 `OnMove(InputValue)`, `OnJump(InputValue)` — `PlayerInput` 컴포넌트의
   SendMessages 방식). 새 액션은 같은 패턴으로 `On<ActionName>(InputValue value)` 퍼블릭 메서드를 추가해 연결한다.
   `.inputactions` 파일 자체는 텍스트 편집 금지(hooks가 차단) — 액션 자체를 추가/변경해야 하면 Unity 에디터로.

2. **PlayerController 상태 훅** — 기존 상태 변수 패턴을 따른다 (`isJumping`, `wallJumpLockCounter`처럼
   bool 플래그 + float 타이머 조합). 새 동작 전용 상태는:
   - `bool is<Move>ing` (예: `isDashing`)
   - 지속시간·쿨다운은 `float <move>Timer` + public 튜닝 필드 (`[Header("...")]`로 그룹화, 기존 헤더 스타일 유지)
   - `Update()`/`FixedUpdate()`에 훅을 추가할 때 기존 `HandleMovement()`, `HandleJump()`처럼
     `Handle<Move>()` 메서드로 분리한다. 기존 로직(벽점프 락 등)과의 상호작용을 반드시 확인
     (예: 대시 중엔 `HandleMovement()`의 속도 덮어쓰기를 막아야 함).

3. **`[ASSERT]` 채널 정의** — `docs/dev/ASSERT_CONVENTION.md`의 "현재 채널 목록" 표에 먼저 채널을 추가한다.
   네이밍: 소문자 스네이크케이스, 동작 단위로 하나 (`dash_iframe`, `combo_window`, `parry_timing`).
   구현 코드에 `TestLog.Event/Step/Assert(channel, ...)`를 심는다 (`Assets/Scripts/TestLog.cs`).

4. **`PlayTestRunner` 시나리오 추가** — `Assets/Scripts/Testing/PlayTestRunner.cs`에 코루틴 메서드
   (`<Move>Test`) 를 추가하고 `[MenuItem("Tools/PlayTest/<Move Name>")]`로 노출한다.
   입력 주입은 `Assets/Scripts/Testing/InputInjector.cs`의 `Press<Action>/Release<Action>` 사용
   (없으면 같은 패턴으로 추가). `DashIFrameTest()`가 참고 골격 — 대시 구현 시 그 TODO를 채운다.
   검증은 MCP로: `manage_editor(action="play")` → `execute_menu_item("Tools/PlayTest/...")` →
   `read_console`으로 `[STEP]`/`[ASSERT]` 로그 확인 → `manage_editor(action="stop")`.

5. **docs 조회 규칙 준수** — API 시그니처·수치가 불확실하면 (예: `InputSystem` 신규 API,
   물리 값) 추측하지 말고 `docs.unity3d.com`의 6000.x 페이지를 조회하고, 코드 주석 또는
   `task.md`에 출처 URL 한 줄을 남긴다.

## 검증(ASSERT 채널)
- 새로 정의한 채널이 `docs/dev/ASSERT_CONVENTION.md` 표와 `PlayTestRunner` 시나리오 양쪽에
  동일한 이름으로 등장하는지 확인.
- 컴파일 클린(에디터 콘솔 에러 0) → 플레이 모드에서 메뉴 실행 → `[ASSERT] <channel>: PASS` 확인.

## 출처(있으면)
- Input System `SendMessages` 방식(`On<ActionName>(InputValue)`): 현재 `PlayerController.cs` 기존 코드에서 확인된 패턴.
- 이후 이 절차에서 조회한 API 출처는 여기 목록에 이어서 추가한다.

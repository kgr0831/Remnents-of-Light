# 전투 학습(튜토리얼) — 구현 계획 · 진행 현황

> 작성 2026-08-12 · 대상 씬 `Assets/Scenes/TutorialScene.unity` · 브랜치 `feature/mapSystem`

---

## 0. 한눈에 보기

| 영역 | 상태 |
|---|---|
| 능력 게이트 · 연출 · 진행자 **코드** | ✅ 작성 완료 (컴파일 에러 0) |
| `PlayerController` · `SectionCamera` **연동 수정** | ✅ 완료 (컴파일 에러 0) |
| **씬 구축**(맵 12구역 · UI · 카메라 · 조명) | ✅ 완료 · 저장됨 |
| **스텝 데이터 배선**(인스펙터 값 채우기) | ✅ 12스텝 전부 배선 (누락 0) |
| **플레이 검증** | 🟡 자동 검증 6건 통과 · 손으로 봐야 할 항목 잔여 (§8) |

`TutorialScene.unity`는 **저장 완료**입니다(미저장 Player 복사본 문제 해소).
디스크의 `IntroScene_2.unity` 변경분(git `M`)은 **제 작업이 아니라 기존 사용자 편집**
(카메라에 BGM AudioSource 추가 등)입니다.

---

## 1. 확정 사양

### 1.1 사용자 확답으로 확정된 것

| 항목 | 결정 |
|---|---|
| 시간가속 스텝 모순(성공=적 처치 ↔ 공격 불가) | **공격 허용**. 성공 = 가속 사용 + 처치 / 실패 = 가속 없이 처치 |
| 초월 · 폭주 | 튜토리얼 내내 **봉인**, 처형 이후 **전용 스텝 2개를 신규 추가** |
| 맵 비주얼 | **미니멀 플랫폼**(어두운 배경 + 단색 도형). 타일 드레싱은 나중에 |
| 마무리 이후 | **암전 상태로 정지**(씬 전환 없음) |

### 1.2 추가된 스텝 2개 (사용자 지시 원문 반영)

- **초월** — 대시-카운터와 동일 상황 + 광원 100%
  - 대사: `광원이 100%일 때 발생하는 초월 상태에서는 지속적으로 광원을 소모하여 적의 공격이 예측되며, 빨라집니다.`
  - 성공: 적 처치 (성공 후 광원 50%로 → 초월 해제) / 실패: 없음
  - 봉인: 광원방출, 시간가속
- **폭주** — 대시-카운터와 동일 상황 + 광원 0%
  - 대사: `광원이 0%일 때 발생하는 폭주 상태에서는 빨라지지만 시아가 제한되며, 적을 때리지 않으면 자아가 소멸하여 사망합니다.`
  - 성공: 적 처치 (성공 후 광원 50%로 → 폭주 해제) / 실패: **자아 게이지 0% 3초 이상 유지**
  - 봉인: 광원방출, 시간가속, 일섬

### 1.3 스펙을 그대로 못 쓴 곳 (판단 근거 포함)

| 항목 | 스펙 | 실제 구현 | 이유 |
|---|---|---|---|
| 처형 스텝 적 HP | "HP 1" | **maxHp 5 / 현재 1** | 처형 조건이 `HP비율 ≤ 20%`. max 1이면 비율이 100%라 **영원히 처형 불가** |
| 인트로 검은 패널 | "a값 0으로 이미 떠있음" | 씬 시작 시 `Set(1f)` 로 **검은 채로 시작** | `ScreenBlackout`이 Awake에서 알파 0 패널을 미리 만드는 그 컴포넌트가 맞음. 다만 직전 씬(검 획득 컷신)이 **암전 상태로 넘겨주므로** 검게 시작해야 이음매가 안 보이고, "이후 검은 패널 페이드 아웃"도 성립함 |

---

## 2. 완료된 작업 (코드)

### 2.1 신규 파일 3개

#### `Assets/Scripts/Tutorial/TutorialGate.cs`
전역 능력 게이트. `[Flags] TutorialAbility` 13종 + `Invulnerable` / `NoCrit`.
- **기본값이 `All`** → 튜토리얼이 아닌 씬은 **동작 변화 0**.
- `[RuntimeInitializeOnLoadMethod]` 로 매 Play 시작에 초기화 —
  "도메인 리로드 없이 Play" 설정에서 잠금이 다음 세션으로 새는 것을 구조적으로 차단.

```
None / Move / Jump / WallClimb / Attack / Dash / DodgeCounter / Parry
/ TimeAccel / Ilseom / LightSpend / Execution / Transcend / Rampage / All
```

#### `Assets/Scripts/Tutorial/TutorialPanelUI.cs`
SwordPanel(설명 패널) 연출 — `Open(line1, line2)` / `Close()` / `HideImmediate()`.
- 패널 스프라이트 배열 정방향·역방향 훑기 + 타이핑/역타이핑 + 효과음.
- `SwordPickupSequence`의 검증된 방식을 그대로 따름
  (Animator `speed=-1` 역재생은 스프라이트 커브에서 안 걸림 — 2026-08-12 실측 기록).
- **모든 대기가 unscaled** (시간정지 중에 뜨는 패널이므로).
- **`Open()`이 매번 `SetAsLastSibling()`을 한다**(씬 구축 중 추가) —
  `ScreenBlackout`이 자기 Awake에서 검은 패널을 맨 마지막 형제로 밀어 넣기 때문에,
  이게 없으면 **인트로·마무리 문구가 암전 뒤에 가려 안 보인다**(둘 다 "검은 화면 위 문구"가 스펙).

#### `Assets/Scripts/Tutorial/TutorialDirector.cs`
진행자 본체. `TutorialGoal`(12종) · `TutorialStepData`(직렬화 스텝 정의) 포함.

한 스텝의 흐름 (사용자 스펙 그대로):
```
노이즈+검정 페이드 인 → 0.5초 → 순간이동 → 검정 페이드 아웃
→ SwordPanel 열림 + 타이핑 → 1초 → 자동 닫힘
→ [시간이 흐르고 그 동작만 입력 개방]
   ├ 실패: 페이드 인 → 0.5초 → 상황 초기화 → 페이드 아웃 → 재개
   └ 성공: 입력 제한 → (진행 중 연출 종료 대기) → 시간 정지 → 0.5초 → 페이드 인 → 다음 스텝
```

시간정지 관련 핵심 3가지(전부 구현됨):
1. 모든 대기가 `WaitForSecondsRealtime`.
2. 순간이동 후 `SectionCamera.SnapToTarget()` — 카메라 슬라이드는 `Time.deltaTime` 기반이라
   멈춘 시간에는 한 프레임도 안 움직임.
3. `LateUpdate`가 매 프레임 `timeScale = 0`을 **다시 눌러줌** —
   회피-카운터·히트스톱 코루틴의 `finally`가 자기 기준값(1)으로 되돌리는 것을 막음.
   추가로 성공 시 `IsDodgeCountering / IsExecuting / IsIlseomActive`가 풀릴 때까지 기다렸다 정지
   (그 연출들은 unscaled로 돌아서 timeScale=0으로는 안 멈춤).

적은 **매번 프리팹에서 새로 생성하고 스텝 종료 시 파괴**한다 —
`DummyEnemy`는 죽으면 `dead` 플래그가 남아 다시 켜도 안 되살아나기 때문.

### 2.2 `PlayerController.cs` 수정 — 게이트 삽입 지점 16곳

각 동작이 **시작되는 지점 한 곳**에만 걸었다. 잠긴 동작은 연출·판정·입력 수신까지 전부 안 일어난다.

| 줄 | 대상 | 효과 |
|---|---|---|
| 829 | `Update()` | Move 잠김 → `moveInput` 자체를 0으로 (이동·벽타기 상하 동시 차단) |
| 1246 | `HandleWallSlide` | 벽에 붙지도, 붙어 있지도 못함 |
| 1413 | `HandleJump` | 점프 입력 폐기 |
| 1471 | `HandleDash` | 대시 버퍼 소비 안 함 |
| 1584 | `HandleIlseom` | **홀드가 차지로 넘어가기 직전에 끊음** → 연출·광원소모·발동 전부 없음 (탭 패링은 유지) |
| 1671 | `CanStartCharge` | 패링·일섬 **둘 다** 잠겼을 때만 차지 시작 자체를 차단 |
| 1783 | `TryParry` | 패링 모션조차 재생 안 함 |
| 2125 | `HandleExecution` | 타겟팅까지 정리 → **적 아웃라인·프롬프트도 안 뜸** |
| 2167 / 2195 | `HandleRampage` / `HandleTranscend` | 자동 진입 차단 + 이미 켜졌으면 해제 |
| 2328 | `CanSustainTimeAccel` | 진입도 유지도 불가 |
| 2732 | `HandleLightSpend` | E 홀드 시작 불가 + 도중 잠기면 종료 |
| 3070 | `TryConsumeDodge` | 회피 판정 자체가 안 섬 |
| 3571 | `CheckAttackHit` | `NoCrit` → 크리티컬 봉인 |
| 3656 | `TakeDamage` | `Invulnerable` → 모든 피해 경로 무효 |
| 4120 | `OnAttack` | 공격 입력이 버퍼에도 안 쌓임 |

**성공 판정용 공개 판독구 추가**(≈2258행 [ASSERT] 블록):
`IsDashing` · `IsIlseomActive` · `IsExecuting` · `IsSpendingLight` ·
`ParrySuccessCount` · `DodgeCounterSuccessCount` · `ExecutionCount`

> `TestLog`는 `UNITY_EDITOR || DEVELOPMENT_BUILD`에서만 컴파일되므로 **판정 근거로 쓸 수 없어**
> 명시적 카운터를 넣었다.

### 2.3 `SectionCamera.cs` 수정
- 구간 중심 계산을 `ComputeTargetPos()`로 추출(중복 제거, 동작 변화 0).
- **`SnapToTarget()` 신규** — 보간 없이 즉시 정착. 시간정지 중 순간이동 전용.

### 2.4 검증 완료
- `refresh_unity` + `read_console` → **컴파일 에러 0**.
- 남은 경고 2건은 **기존 코드의 것**(`RampageVisionFx` 도달 불가 코드, `rampageDrainAccum` 미사용).

---

## 3. 스텝 정의표 (인스펙터에 그대로 넣을 값)

`systemLine`(text-action-1) = `SYSTEM MESSAGE` 고정, `message`(text-action-2)가 아래 대사.

| # | id | 대사 | allowed | 적 | 플레이어 | 성공 / 실패 |
|---|---|---|---|---|---|---|
| — | intro | 전투 학습에 오신것을 환영합니다. | None | — | — | 대사 종료 |
| 1 | jump | Space키를 눌러 점프하세요. | Move·Jump | — | 기본 | ReachZone(우측 플랫폼) / **y < 3 낙하** |
| 2 | wallclimb | 벽에 붙어 W키로 벽을 오르세요. | +WallClimb | — | 기본 | ReachZone(상단 플랫폼) / 없음 |
| 3 | attack | 좌클릭으로 적을 공격하여 처치하세요. | +Attack | HP **3** | 무적 · **NoCrit** | KillEnemies / 없음 |
| 4 | dash | Shift키로 대시하세요. | +Dash | — | 무적 | Dash 1회 / 없음 |
| 5 | dodgecounter | 대시로 적의 공격을 회피 후, 카운터 공격을 날리세요. | Move·Jump·WallClimb·Dash·**DodgeCounter** | HP 1 | 무적 | DodgeCounter / 없음 |
| 6 | parry | 우클릭으로 적의 공격을 타이밍에 맞게 방어하세요. | Move·Jump·WallClimb·**Parry** | HP 1 | 무적 | Parry / 없음 |
| 7 | timeaccel | alt키로 속도를 높혀 적을 처치하세요. | Move·Jump·WallClimb·**TimeAccel·Attack** | HP 1 | 무적 · 광원 **100%** | TimeAccelKill / **가속 없이 처치** |
| 8 | ilseom | 우클릭을 2초간 홀드하여 강력한 일격을 통해 적을 처치하세요. | Move·Jump·WallClimb·**Ilseom** | HP 1 | 무적 · 광원 100% | IlseomKill / 일섬 없이 처치 |
| 9 | lightspend | E키를 홀드하여 체력을 회복하세요. | Move·Jump·WallClimb·**LightSpend** | — | **체력 1** · 광원 100% | LightSpendRelease / 없음 |
| 10 | execution | 커서를 적에게 이동하여, 적을 처형하세요. | Move·Jump·WallClimb·**Execution** | **maxHp 5 / 현재 1** | 무적 · 광원 100% | Execution / 없음 |
| 11 | transcend | 광원이 100%일 때 발생하는 초월 상태에서는 지속적으로 광원을 소모하여 적의 공격이 예측되며, 빨라집니다. | Move·Jump·WallClimb·Attack·Dash·DodgeCounter·Parry·Ilseom·Execution·**Transcend** | HP **20** | 무적 · 광원 100% | TranscendKill (성공 시 광원 50%) / 없음 |
| 12 | rampage | 광원이 0%일 때 발생하는 폭주 상태에서는 빨라지지만 시아가 제한되며, 적을 때리지 않으면 자아가 소멸하여 사망합니다. | Move·Jump·WallClimb·Attack·Dash·DodgeCounter·Parry·Execution·**Rampage** | HP **20** | 무적 · 광원 **0%** | RampageKill (성공 시 광원 50%) / **자아 0% 3초** |
| — | outro | 전투 학습이 종료되었습니다. | None | — | — | 암전 정지 |

> 폭주 중에는 `PlayerController`가 이미 패링·처형·회피카운터·일섬·E홀드를 자체 봉인한다 —
> 게이트는 그 위에 더 얹지 않는다(실질적으로 이동·점프·대시·공격만 동작).

---

## 4. 작업 진행 (실행 순서대로)

### A. 씬 골격 (`TutorialScene`) — ✅ 완료
1. ✅ **Player** — Map-test 원본 복사본을 그대로 저장. 시작 위치 = 점프 구역 스폰 `(-10, 8)`
2. ✅ **Main Camera** — `SectionCamera` 추가, `orthographicSize 9`,
   `sectionSize (32,18)`, `gridOrigin (-16,0)`, `slideSpeed 10`, 배경 `#0B0D12`.
   기존 BGM `AudioSource`는 **건드리지 않았다**
3. ✅ **Global Light 2D** — `LightType.Global` · 흰색 · `intensity 1.0`
   (Map-test는 0.09지만 그건 LightObject로 밝히는 맵이다. 광원 오브젝트가 없는 미니멀 맵이라
   전역광만으로 보여야 해서 1.0으로 올렸다) + `Global Volume` 프리팹 인스턴스
4. ✅ **EventSystem** + `InputSystemUIInputModule`(기본 액션 자동 할당)
5. ✅ **캔버스 2개** — 레이어 순서가 중요해서 나눴다
   - `PlayerHudCanvas` (Overlay, **sortingOrder 0**) — `PlayerHudUI`가 "가장 낮은 sortingOrder의
     Overlay 캔버스"를 찾아 그 밑에 HUD를 만든다. 튜토리얼 UI와 같은 캔버스에 두면 HUD가
     `ScreenBlackout` 패널보다 **나중 형제**로 붙어 암전 위에 떠 버린다
   - `TutorialUI` (Overlay, **sortingOrder 100**) — `ScreenBlackout` · `TutorialPanelUI` ·
     효과음 `AudioSource` · `SwordPanel` 프리팹 인스턴스(꺼진 채로 저장)
6. ✅ **PlayerHudUI** — 별도 배치 불필요. `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로
   스스로 생성된다(Play 실측 확인)
7. ✅ **TutorialDirector** 오브젝트 — blackout/panel/player/sectionCamera 전부 배선

### B. 맵 12구역 (미니멀 플랫폼) — ✅ 완료 (`Map` 루트 아래)
8. ✅ 공통 평지 구역 10개 — 바닥 `x∈[cx-12,cx+12]` 윗면 y=4 · 좌우 투명벽(**Ground 레이어**)
9. ✅ 점프 구역 — 좌 `x∈[-14,-4]` / 우 `x∈[-1,9]` 윗면 y=8, 간격 **3.0u**, `failBelowY = 3`
10. ✅ 벽타기 구역 — 바닥 → **Wall 레이어** 벽 `x∈[66,68] y∈[4,12]` → 상단 플랫폼
    `x∈[68,78]` 윗면 y=12(벽 꼭대기와 **같은 높이**라 렛지 등반 후 그대로 걸어 올라간다)
11. ✅ 구역별 `Spawn` / `EnemySpawn` / `SuccessZone`(트리거) 배치
    - **`EnemySpawn`의 y는 바닥 윗면 + 0.60** — `DummyEnemy`의 원점이 콜라이더 바닥에서
      0.60 위(실측)라, 바닥 높이에 그대로 두면 지형에 묻혔다가 튀어나온다

### C. 배선 — ✅ 완료
12. ✅ `TutorialPanelUI` — 패널 프레임 6장(IntroScene_2와 **같은 시트·같은 순서**:
    `Assets/UI/Animated Tablets/blue/tablet_animation_spritesheet.png`) · text-action-1/2 · SFX 2종
13. ✅ `TutorialDirector.steps` 12개 (§3 표 그대로, 누락 0)
14. ✅ `enemyPrefab` = `Assets/Scenes/DummyEnemy (3).prefab`

### E. 2026-08-12 후속 요청 6건 — ✅ 반영 완료

| 요청 | 반영 |
|---|---|
| **적 `moveSpeed` 1로** | `DummyEnemy (3)` 프리팹 0 → 1. 공유 프리팹이라 Map-test·Map1-test·SampleScene도 같이 바뀜(사용자 승인) |
| **긴 텍스트가 잘림** | `text-action-2` 폰트 86 → **44**, `VerticalOverflow`를 **Overflow**로. 14개 문구 전부 실측 → 최대 147 / 200px |
| **좌우 여백 · 위로 금지 · 아래로만** | 폭 786.9 → **640**(여백 +73px씩), 정렬 `MiddleCenter` → **`UpperCenter`**, 윗변 y=12.5 **고정** · 높이 200(밑변 -187.5). 짧은 문구는 원래 자리 그대로, 긴 문구만 아래로 늘어남 |
| **달성 후 1초 대기** | `successHold` 0.5 → **1초**, 그리고 **`Freeze()` 앞으로 옮김** — 뒤에 두면 `timeScale=0`이라 적 사망 연출이 얼어붙은 채로 암전된다 |
| **이전 상태 전부 초기화** | `PlayerController.ResetTransientCombatState()` 신규 → `SetupStep` 첫 줄에서 호출. 대상: 대시·차지·시간가속·광원방출 / **폭주·초월**(각자 시야 이펙트·블룸까지) / 패링 실드 / 플레이어 블룸 4종 / `RampageVisionFx`·`TranscendVisionFx`·글리치(Ego·Heartbeat) / **카메라 지속 쉐이크·지속 줌인** / 입력 버퍼·엣지 / 쿨타임 4종 / 회피 인정 창 |
| **글리치 간헐 표시** | `ScreenGlitchFx.Source`에 `Tutorial` 플래그 추가 + `AmbientGlitchRoutine`. 플레이 가능한 동안에만 4~9초 간격으로 0.08~0.2초 번쩍(연출 구간은 이미 `Cutscene` 글리치가 돌아서 제외) |
| **텍스트·적·벽·플랫폼 블룸** | 아래 §9 |

### D. 검증 — 🟡 자동 검증분 통과
15. ✅ **점프 거리 실측** — 에디터 실값(`gravityScale 10` · `jumpForce 35` · `fallMultiplier 2.5`
    · `moveSpeed 6`)으로 체공 0.582s × 6 = **3.49u**. 플랫폼 콜라이더 반폭 0.375를 더하면
    "너무 일찍 뛴" 여유가 ≈0.87u, 코요테 타임(0.1s = 0.6u)까지 합쳐 유효 창 ≈1.5u.
    **Play 실측에서 실제로 건넜다**(`[ASSERT] tutorial_jump: PASS`) → 간격 **3.0u 확정**
16. 🟡 스텝별 Play 검증 — 자동으로 확인한 것:
    - ✅ 부팅 → 인트로 패널 → 점프 스텝 진입, 게이트 `Move|Jump`
    - ✅ 점프 성공 → 성공존 판정 → wallclimb 스텝으로 순간이동
    - ✅ 벽타기: 벽 부착 → 렛지 등반 → 상단 플랫폼 착지 → 성공존 안, 외곽벽에 막힘
    - ✅ **패링 스텝 게이트에서 우클릭 2.5초 홀드 → 아무 일도 안 일어남**
      (`IsIlseomActive=False` · `chargeVisualsStarted=False` · 광원 50 그대로 = 소모 0)
    - ✅ **처형 잠김 상태에서 HP 20%인 적 위에 커서 → `executionTarget=null`** (아웃라인·프롬프트 없음)
    - ✅ 실패 → 초기화 사이클(`[ASSERT] tutorial_jump: FAIL failed - reset` → 스폰 복귀 → 재개)
    - ✅ 무적: 적 `hit_player dmg=1` 로그가 떠도 체력 8/8 유지
    - ⬜ 남은 항목은 §8
17. ✅ `Build Settings` — TutorialScene `buildIndex 6`, enabled
18. ⬜ `task.md` 갱신 + 커밋(**승인 후**)

---

## 5. 맵 레이아웃 수치 (설계값)

### 카메라 구간
`orthographicSize 9` → 화면 18u 높이 × 32u 폭(16:9). `sectionSize (32,18)`, `gridOrigin (-16,0)`
→ 구간 k의 중심 = `(32k, 9)`. **한 칸 건너 배치**해 이웃 구역이 절대 안 보이게 한다.

| 구역 | 중심 X | 구역 | 중심 X |
|---|---|---|---|
| 점프 | 0 | 일섬 | 448 |
| 벽타기 | 64 | 광원방출 | 512 |
| 공격 | 128 | 처형 | 576 |
| 대시 | 192 | 초월 | 640 |
| 대시-카운터 | 256 | 폭주 | 704 |
| 패링 | 320 | | |
| 시간가속 | 384 | | |

### 공통 평지 구역 (중심 `cx`)
- 바닥: `x ∈ [cx-12, cx+12]`, 윗면 `y = 4`, 두께 2 — **Ground(레이어 9)**
- 투명 벽: `x = cx±12`, `y ∈ [4, 14]` — **Ground 레이어**
  (Wall 레이어로 두면 **벽타기가 가능해져 버린다** — 반드시 Ground)
- 플레이어 스폰 `(cx-5, 4)` / 적 스폰 `(cx+3, 4)` — 8u 간격

### 점프 구역 (cx = 0)
- 좌 플랫폼 `x ∈ [cx-14, cx-4]`, 윗면 `y = 8` / 우 플랫폼 `x ∈ [cx-1, cx+9]`, 윗면 `y = 8`
- **간격 3.0u**(아래 계산 근거) · 스폰 `(cx-10, 8)` · 성공존 = 우 플랫폼 위 · `failBelowY = 3`
- 점프 거리 계산: `gravityScale 10 → 98.1u/s²`, `jumpForce 35` → 상승 0.357s / 정점 6.24u,
  하강은 `fallMultiplier 2.5`(245u/s²) → 0.226s. 체공 ≈ **0.583s** × `moveSpeed 6` ≈ **3.5u**.
  → 안전 마진을 두고 3.0u에서 시작, **Play 실측으로 확정**할 것.

### 벽타기 구역 (cx = 64)
- 바닥 `x ∈ [cx-12, cx+2]`, 윗면 `y = 4` — Ground
- 벽 `x ∈ [cx+2, cx+4]`, `y ∈ [4, 12]` — **Wall(레이어 10)** ← `climbWallLayer`
- 상단 플랫폼 `x ∈ [cx+4, cx+14]`, `y ∈ [10, 12]`(윗면 12) — Ground
- 외곽 투명벽: `x = cx-12`(아래), `x = cx+14`(위) — Ground
- 성공존 = 상단 플랫폼 위 / 스폰 `(cx-8, 4)`

---

## 6. 알려진 함정 (구현·검증 시 반드시 확인)

1. **투명 벽은 Ground 레이어** — Wall이면 플레이어가 타고 올라간다.
2. **낙사 시스템 간섭** — `PlayerController`의 자체 낙사는 "20u 낙하 + 아래에 지형 없음"에서 발동.
   튜토리얼 실패선(`failBelowY`)이 훨씬 먼저 잡히도록 유지할 것.
3. **처형 임계값** — `HpRatio ≤ 0.2`. 적 HP를 바꿀 때 max/current 비율을 같이 봐야 함.
4. **초월 스텝 광원 드레인** — 2.4/s라 100→70까지 약 12.5초, 그 뒤 초월이 자동 해제된다.
   해제 후에도 적을 잡으면 성공이므로 진행이 막히지는 않음(의도된 동작).
5. **폭주 스텝** — 자아 6/s 소모 → 0까지 약 16.7초, 거기서 3초 더 버티면 실패.
   무적이라 자아 붕괴 피해로는 안 죽는다(튜토리얼 실패 규칙이 대신 처리).
6. **씬 편집 전 활성 씬·Play 모드 확인** — `execute_code` 첫 줄에 가드를 둘 것.
7. **`execute_code`는 메서드 본문**이라 `using` 선언 불가 · `Object.Instantiate`는
   `UnityEngine.Object.Instantiate`로 명시해야 함(`object`와 모호).

---

## 7. 확인이 필요한 잔여 항목

- [x] ~~미저장 Player 복사본~~ → **저장 완료**
- [x] ~~점프 구역 플랫폼 간격~~ → **3.0u 확정**(§4-D-15 실측)
- [x] 색값 — 지정이 없어 임의 결정했다. **원하면 바꿔 주세요**
  - 배경 `#0B0D12` · 지형 `#3A4252` · **벽타기 벽만 `#4E6B8C`(푸른색)** — "여긴 오를 수 있다"는
    신호를 색으로 주려고 지형과 구분했다
  - 전역광 `intensity 1.0`
- [ ] BGM을 튜토리얼에도 깔지 여부(Main Camera의 기존 `AudioSource`는 손대지 않았다)

---

## 8. 손으로 봐야 할 검증 (자동으로 확인 못 한 것)

아래는 전부 **실제 조작 감각·타이밍**이 걸려 있어 주입 입력으로는 의미 있게 못 본 항목이다.
Play 후 순서대로 해 보고 어긋나는 곳만 알려 주면 된다.

1. **대시-카운터**(5) · **패링**(6) — 적의 찌르기 타이밍에 맞춰 실제로 성공 판정이 뜨는지.
   `DummyEnemy (3)` 프리팹의 `moveSpeed`를 **0 → 1로 변경**(사용자 지시 2026-08-12)해
   이제 쫓아온다. 공유 프리팹이라 **Map-test · Map1-test · SampleScene의 적도 같이 움직인다**
   (세 씬 모두 이 값을 오버라이드하지 않아 프리팹 값을 그대로 상속 — 확인 완료).
   - ⚠️ **속도가 느리다**: 스폰 간격이 8u(플레이어 `cx-5` / 적 `cx+3`)이고 `attackRange`가 2.4라,
     플레이어가 가만히 있으면 첫 공격까지 **약 5.6초**가 걸린다. 답답하면 스폰 간격을 줄이거나
     (`Map/Zone_*/EnemySpawn`의 x) `moveSpeed`를 더 올리면 된다.
2. **시간가속**(7) — 실패 조건("가속 없이 처치")이 실제로 잡히는지.
3. **일섬**(8) — 2초 홀드가 잠금 해제 상태에서는 정상 발동하는지(잠금 쪽은 확인 완료).
4. **광원방출**(9) — 체력 1에서 E 홀드 → 회복 → 뗐을 때 성공 처리.
5. **처형**(10) — maxHp 5 / 현재 1(비율 정확히 0.2)에서 처형이 실제로 걸리는지.
   `executionHpThreshold`가 `≤ 0.2`라 **경계값**이다 — 안 걸리면 시작 HP를 1 그대로 두고
   maxHp만 6으로 올리면 된다.
6. **초월**(11) · **폭주**(12) — 상태 진입/해제와 성공 시 광원 50% 복귀.
   폭주 실패(자아 0% 3초)는 §6-5대로 진입 후 약 20초 걸린다.
7. 각 구역이 카메라 한 칸에 정확히 담기는지(구역 간격 64u = 한 칸 건너 배치).

> ⚠️ 에디터 전용 주의: **Play 도중 스크립트가 재컴파일되면 도메인 리로드가 일어나
> `TutorialDirector`의 코루틴이 죽고 `TutorialGate`가 `All`로 초기화된다**(2026-08-12 실측).
> 그러면 잠겨 있어야 할 동작이 전부 풀린 채 멈춘 것처럼 보인다 — 튜토리얼 버그가 아니라
> 리로드 흔적이니, Play를 멈췄다 다시 시작하면 된다. 빌드에는 없는 현상.

---

## 9. 블룸 · 네온 룩 (2026-08-12)

### 9.1 왜 지금까지 블룸이 하나도 안 걸렸나
`TutorialScene` Main Camera의 **`renderPostProcessing`이 꺼져 있었다**. `Global Volume`(=`IlseomBloomProfile`,
threshold 1.15 / intensity 2.2 / scatter 0.7)은 붙어 있었지만 카메라가 포스트프로세싱을 안 돌려 무의미했다.
→ `true`로 변경(IntroScene_2 · Map-test와 같은 값).

### 9.2 대상별 방식

| 대상 | 방식 |
|---|---|
| **플랫폼 · 벽** | 몸통 뒤에 `EDGE 0.13`만큼 큰 HDR 사각형(`Glow` 자식)을 깔아 **테두리만** 빛나게 한다. 몸통 색을 HDR로 올리면 도형이 통째로 하얗게 떠서 미니멀 톤이 사라진다 |
| **적 몬스터** | 같은 기법 — 스폰 시 `AttachEnemyGlow`가 같은 스프라이트를 1.15배로 뒤에 깐다. **스프라이트 색을 직접 못 올린다** — `DummyEnemy`가 피격 점멸용으로 원래 색을 캐시했다가 되돌려 놓기 때문 |
| **텍스트 · 패널** | 프리팹에 이미 `SwordTextBloom`(`Custom/UIBloomBoost`, `_Boost 6`)이 붙어 있었다. 다만 **Screen Space - Overlay 캔버스는 URP 포스트프로세싱을 아예 안 받는다** → `TutorialUI` 캔버스를 **Screen Space - Camera**로 전환(IntroScene_2의 `SwordBloomCanvas`와 같은 방식) |

### 9.3 캔버스를 Camera 모드로 바꾸면서 생긴 순서 문제
HUD(`PlayerHudCanvas`)는 Overlay라 **항상** Camera 모드 캔버스 위에 그려진다 → 암전 중에 검은 화면 위로
HUD만 떠 있게 된다. `TutorialDirector`가 직접 껐다 켠다: `FadeToBlack` 끝(완전히 검어진 뒤) · `Start`에서 끄고,
`Resume` · `OnDisable`에서 켠다.

### 9.4 확정 색값 (절제된 네온 — 사용자 지시 "과한 건 원치 않음")

| 항목 | 값 |
|---|---|
| 배경 | `#070A0E` |
| 지형 몸통 / 테두리 | `#1C2230` / HDR `(0.15, 1.90, 2.10)` 시안 |
| 벽 몸통 / 테두리 | `#232041` / HDR `(1.15, 0.85, 2.30)` **보라** ← 시안과 갈라 "오를 수 있다"를 색으로 표시 |
| 적 테두리 | HDR `(2.10, 0.30, 0.45)` 붉은 네온 · 실루엣 1.15배 |
| 테두리 두께 | 0.13u |

> 더 세게/약하게는 **HDR 값만** 올리고 내리면 된다(임계값 1.15가 기준선 — 1.15 아래면 아예 안 빛난다).

### 9.5 시뮬레이션 룩 (사용자 지시 "수치 조절 말고 다른 방식으로")

색·세기를 더 만지는 대신 **연출 요소를 추가**했다. 전부 `SimGrid` 루트와 `TutorialSimFx`(Main Camera)에 있다.

| 요소 | 내용 |
|---|---|
| **배경 격자** | `Assets/Textures/TutorialGrid.png`(32×32px, 좌·하 1px 선 · PPU 16 → **2u 한 칸**)를 `SpriteDrawMode.Tiled`로 구간마다 32×22 크기로 깐다. 알파 0.10 — **블룸 임계값 아래**라 배경은 번지지 않는다 |
| **구간 경계 기둥** | 구간 좌우 끝(`cx ± 15.7`)에 세로 발광선 — 시뮬레이션 챔버의 벽 |
| **스캔라인 스윕** | `TutorialScanline.png`(64×64px 세로 그라데이션 · PPU 64 → 1u×1u). **카메라의 자식**이라 어느 구역으로 순간이동해도 따라온다. 상세는 아래 9.5-a |
| **그리드 호흡 · 리프레시 깜빡임** | 알파가 5초 주기로 0.75~1.15배 오르내리고, 평균 7초마다 0.06초 동안 2.2배로 튄다. 간격은 매번 0.5~1.5배로 다시 뽑아 규칙적으로 안 보이게 한다 |

> 전부 **unscaled 시간**으로 돈다 — 연출 구간(timeScale=0)에도 배경만 계속 살아 있어야
> "시스템은 돌고 있고 세계만 멈췄다"로 읽힌다.

#### 9.5-a 스캔라인 상세 (사용자 지시 "여러 곳 · 여러 속도 · 여러 방향", "지형·캐릭터 뒤에")
씬에는 **원본 1개만** 두고 `TutorialSimFx`가 `scanlineCount`만큼 복제해 줄마다 다르게 굴린다.
손으로 배치하면 "서로 안 맞물리는 무작위"를 유지할 수 없다.

| 값 | 설정 |
|---|---|
| 줄 수 | 8 |
| 축 | `verticalChance 0.35` — 35%는 **세로 줄**이 되어 좌우로 훑는다(스크립트가 90도 회전) |
| 방향 | `reverseChance 0.4` — 40%는 진행 방향이 뒤집힌다(위→아래, 오른→왼) |
| 속도 | 한 줄이 훑는 시간 `1.4 ~ 5.5초` 사이에서 각자 뽑는다 |
| 쉼 | `0.2 ~ 3.5초` |
| 위상 | 균등 분할이 아니라 `0 ~ cycle` 통째 무작위 — 균등 분할은 줄 수가 적을 때 규칙적으로 보인다 |
| 지나는 자리 | 진행축과 직교하는 좌표도 ±2~3u 흔든다(전부 정중앙을 지나면 한 덩어리로 보인다) |
| 두께·진하기 | `scanlineVariance 0.45` → ±45% |
| 그리는 순서 | `sortingOrder -40` — 격자(-60)보다 앞, **지형(-11/-10)·플레이어보다 뒤** |
| 톤 | 두께 0.8u · 알파 0.16 · HDR `(0.30, 1.20, 1.35)` — 8줄이 동시에 도니 개별 선은 얇고 옅게 |

> ⚠️ **실측으로 잡은 버그**: 처음 만든 텍스처는 1×64px + PPU 64라 스프라이트 폭이 **1/64u**였다.
> `localScale.x = 34`를 줘도 실제 렌더 폭이 0.53u라 화면(32u)을 가로지르지 못했다.
> 64×64px로 만들어 1u×1u로 맞춰야 `localScale`이 곧 월드 크기가 된다.

#### 9.5-b 연출 스킵 (F · 좌클릭) — **효과 하나씩**
사용자 확정: *"스킵이라는 게 해당 효과만 스킵인 겁니다."*
`TutorialDirector.skipPending`을 각 효과가 **한 번 소비**(`ConsumeSkip`)한다 — 한 번 누르면
지금 도는 효과 하나만 끝나고, 뒤이어 오는 효과는 다시 정상 재생된다.

| 지금 도는 효과 | 한 번 누르면 |
|---|---|
| 암전 페이드 인/아웃 | 목표 알파로 즉시 확정 |
| 암전 대기 · 성공 대기 | 그 대기만 종료 |
| 패널 열림/닫힘 애니메이션 | 마지막 프레임으로 확정 |
| **대사 타이핑 중** | **두 줄 다 통째로 띄운다** |
| 패널 유지(panelHold) | 그 유지만 종료 |
| **대사 삭제(역타이핑) 중** | **두 줄 한 번에 전부 지운다** |

> 타이핑만 "두 줄 동시"인 이유: 첫 줄만 완성되고 둘째 줄이 다시 한 글자씩 타이핑되기 시작하면
> "대사를 다 띄웠다"로 안 보인다(`typingSkipped` 플래그가 그 Open() 동안 유지된다).

- 대기(`Wait`·`WaitOrSkip`)는 `WaitForSecondsRealtime` 대신 직접 세는 루프 — 그래야 중간에 끊을 수 있다.
- 페이드는 `ScreenBlackout.FadeTo` 코루틴을 직접 `MoveNext()`로 굴리다가 스킵이 오면 즉시 확정한다.
- 좌클릭은 공격 버튼이기도 하지만 연출 구간에는 게이트가 공격을 막아 둔다 —
  Play 실측으로 스킵 직후 `isAttacking=false` 확인(입력이 새어 나가지 않는다).
- **Play 실측**: 타이핑 중 F 1회 → `t2`가 `'벽에 붙'` → `'벽에 붙어 W키로 벽을 오르세요.'`로 완성되고,
  패널은 그대로 떠 있으며 `timeScale=0` 유지(다음 효과인 panelHold로 넘어가지 않음) 확인.

#### 9.5-c 글리치 효과음 · 시간 독립성

| 언제 | 소리 |
|---|---|
| 간헐 글리치(`Source.Tutorial`) | `Assets/SFX/GlitchSFX3.mp3` |
| 전환·컷신 글리치(`Source.Cutscene` — 암전 페이드 인) | `Assets/SFX/SwitchSFX.mp3` |

`TutorialDirector`에 **전용 `AudioSource`를 따로** 붙였다 — 패널 타이핑 소스를 같이 쓰면
`EndTypingSfx`의 `Stop()`이 글리치 효과음까지 잘라 버린다. `spatialBlend = 0`(2D)이라
카메라가 x=704까지 가도 거리 감쇠가 없다.

**시간 독립성**: Unity 오디오는 원래 `Time.timeScale`의 영향을 받지 않는다(DSP 클럭으로 돈다).
Play 실측으로 확인 — `Time.timeScale = 0`에서 `PlayOneShot` → `isPlaying = true`, 음정도 그대로.
다만 그 독립성이 깨질 수 있는 두 경로에 못을 박아 뒀다(`EnsureTimeIndependentAudio`):
`pitch = 1` 고정, `ignoreListenerPause = true`(다른 시스템이 `AudioListener.pause`를 켜도 계속 난다).
이 프로젝트에는 현재 오디오를 시간에 묶는 코드가 없다(`pitch` 대입·`AudioMixer`·`AudioListener.pause` 전부 없음 — grep 확인).

#### 9.5-d 대사 효과음 · 지워지는 속도
효과음은 **이미 배선돼 있었다**(`textSfx` 루프 + 패널 여닫이 `SwordPanelSfx`) — Play에서
`sfxSource.isPlaying=true`로 확인. 지워질 때만 느렸던 것이라
`untypeSpeedMultiplier = 3`을 추가해 타이핑의 **3배 속도**로 지워지게 했다.

### 9.6 구도 — 지형을 화면 아래쪽으로 (사용자 지시)
지형 y를 내리지 않고 **카메라 구간을 위로 올렸다** — 지형을 만지면 점프 높이·성공존·실패선을
전부 다시 잡아야 하고 카메라 그리드와도 어긋난다.
`SectionCamera.gridOrigin.y` **0 → 2** (구간 중심 y 9 → **11**, 보이는 범위 `[0,18]` → **`[2,20]`**).
평지 바닥 윗면 y=4가 화면 아래에서 **22% → 11%** 지점으로 내려간다.
`SimGrid`의 격자·기둥도 같은 중심(y=11)으로 옮기고 세로를 22로 늘려 새 화면을 덮게 했다.
Play 실측으로 점프 성공 → 벽타기 구역 순간이동까지 그대로 동작함을 확인.

### 9.7 ⚠️ 색을 눈으로 판단할 때
**Play 모드 스크린샷은 감마가 밝게 틀어져 나온다**(실측: 배경 `#0B0D12` → 캡처에선 `#3E4657`).
색·톤 판단은 **에디트 모드 캡처**로 해야 한다. 반대로 **에디트 모드 캡처는 Overlay 캔버스를 안 그리므로**
UI가 보이는지 확인할 때는 Play 캡처를 써야 한다 — 용도가 정반대다.
또한 Play에서는 `PlayerHudUI`가 스택에 얹는 `HpBloomCamera` 때문에 블룸이 한 번 더 걸려
에디트 캡처보다 조금 더 밝게 나온다.

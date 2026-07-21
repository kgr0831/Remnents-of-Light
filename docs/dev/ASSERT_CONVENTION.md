# [ASSERT] 로그 규약

게임의 "검증 언어" 규약. `Debug.Log` 스팸이 아니라, 파싱 가능한 구조화된 로그로
플레이 테스트(STEP4의 `PlayTestRunner`) 및 사람이 함께 읽는다.

## 헬퍼

`Assets/Scripts/TestLog.cs` (정적 클래스, `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 가드).

| 메서드 | 출력 형식 |
|---|---|
| `TestLog.Event(channel, msg)` | `[T=MM:SS.ff f=FRAME] [EVENT] channel: msg` |
| `TestLog.Assert(name, pass, detail="")` | `[T=MM:SS.ff f=FRAME] [ASSERT] name: PASS\|FAIL detail` |
| `TestLog.Step(scenario, step)` | `[T=MM:SS.ff f=FRAME] [STEP] scenario: step` |

타임코드 `[T=MM:SS.ff f=FRAME]`는 `Time.time`(분:초.센티초)과 `Time.frameCount`를 사용한다.

예:
```
[T=00:03.20 f=192] [EVENT] dash_iframe: dash_started
[T=00:03.30 f=198] [ASSERT] dash_iframe: PASS hits=0
[T=00:00.00 f=1] [STEP] dash_iframe: spawned
```

## 채널 네이밍 규칙

- 소문자 + 스네이크케이스 (`dash_iframe`, `combo_window`).
- 기능 단위로 채널을 나눈다 (하나의 전투 동작 = 하나의 채널).
- `Assert`의 `name`은 채널명과 동일하게 써서, 같은 채널의 `[EVENT]`/`[STEP]`/`[ASSERT]`를 로그에서 함께 필터링할 수 있게 한다.

## 현재 채널 목록 (다음 개발 작업 대응)

| 채널 | 검증 대상 |
|---|---|
| `dash_iframe` | 대시 무적 프레임 동안 피격 0회 |
| `combo_window` | 3타 콤보 입력 윈도우 내 연결 |
| `parry_timing` | 패링 유효 프레임 판정 |
| `room_transition` | 방 전환 시 세이브 트리거 |

새 전투 동작을 추가할 때는 이 표에 채널을 먼저 추가하고, `PlayTestRunner` 시나리오와 짝을 맞춘다.
(`.claude/skills/add-combat-move/SKILL.md` 참고 — STEP6)

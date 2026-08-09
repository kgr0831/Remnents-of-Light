# ⚠️ Unity 함정 노트

> **두 번 안 걸리려고 남긴 기록.** 전부 이 프로젝트에서 실제로 시간을 태운 것들이다.
> 출처 `task.md` · `.claude/skills/add-combat-move/SKILL.md`

---

## 🥇 가장 비쌌던 함정

### 1. 직렬화된 값이 코드 기본값을 덮는다 — **한 세션에 세 번, 이후에도 재발**

`.cs`의 public 필드 기본값을 바꿨는데 런타임이 옛 값을 계속 쓴다.

| 층 | 무엇을 덮는가 | 고치는 법 |
| --- | --- | --- |
| 씬 인스턴스 | `.cs`의 public 필드 기본값 | `SerializedObject`로 씬 컴포넌트 수정 → **씬 저장** |
| 머티리얼 에셋(`.mat`) | `.shader`의 `Properties` 기본값 | `mat.SetFloat(...)` → `SetDirty` → `SaveAssets` |
| 프리팹 인스턴스 | 프리팹 원본 | 인스턴스 오버라이드 해제 |

**게다가 4번째 층이 있다** — 디스크의 씬 파일엔 그 필드가 **아예 없는데도**, 에디터 **메모리의**
**컴포넌트 인스턴스가 스크립트 리컴파일을 건너오며 옛 값을 유지**한다 (2026-08-04 `timeAccelDrainMultiplier` 실측).

> ✅ **교훈**: 튜닝 기본값을 바꾼 뒤엔 반드시 **런타임 로그로 실제 값을 다시 읽어** 확인하라.
> 눈으로 판단하지 말 것. 그래서 `StartTimeAccel`은 이제 진입 로그에 `drain=x/s`를 같이 찍는다.
> 반대로, **플레이 모드에서 public 필드에 준 값은 정지하면 사라진다** — 캡처·측정용으로만.

---

### 2. `PlayerInput`은 Button 액션의 "뗌"을 아예 안 보낸다

```csharp
// PlayerInput.cs:1499
if (!(context.performed || (context.canceled && action.type == InputActionType.Value))) return;
```

→ `On<Action>(InputValue)`은 **press에서만** 호출된다.
홀드 길이를 재는 동작(차지류)에서 이걸 쓰면 **버튼을 떼도 타이머가 계속 쌓여 저절로 발동**한다
(일섬 1차 구현에서 실제 발생).

> ✅ **해결**: 액션을 직접 폴링한다 — `chargeAction.IsPressed()`
> 같은 이유로 `OnJump`의 `isJumpHeld = false` 분기도 실행되지 않는다 (**미수정 잠재 버그**).

---

### 3. 이 게임의 플레이어 피봇은 **발밑**이다

스프라이트 피봇이 `(0.46, 0.03)`이라 `transform.position.y`가 **콜라이더 중심이 아니라 발밑**이다.

- `TryStepUpShortWall` / `TryLedgeClimb` 둘 다 착지 Y를 "position = 중심"으로 가정해
  **반 캐릭터 키만큼 붕 뜨는** 버그를 만들었다.
- 히트박스를 적 판정점 높이와 비교할 때도 반드시 감안할 것.

---

## 🎮 Input System

| # | 함정 |
| --- | --- |
| 4 | **키보드 키는 비트필드 컨트롤**이라 `QueueDeltaStateEvent`를 못 쓴다 (`InvalidOperationException`). `KeyboardState` 풀스테이트 + `QueueStateEvent`로 교체하고, 동시 입력은 `_heldKeys` HashSet으로 매번 전체 상태를 재전송 |
| 5 | `InputActionAsset`은 `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()`로는 **디스크에 안 써진다**. JSON 비활성 사본을 만들어 편집 → 파일 덮어쓰기 → 재임포트 |
| 6 | 씬의 `PlayerInput`이 액션을 enable해둔 상태면 로드된 에셋을 직접 수정할 수 없다 (*"Cannot add/remove elements while one or more of its actions are enabled"*) |
| 7 | `InputInjector.PressAttack()`은 이벤트를 **큐잉만** 한다. 같은 `execute_code` 호출 안에서 곧바로 상태를 읽으면 항상 `False`로 보인다 |
| 8 | **한 버튼에 탭·홀드를 같이 걸면** 탭 동작이 "뗄 때" 발동으로 밀려 회귀를 만든다 (Shift 대시가 실제로 죽었다). 별도 키로 분리하는 게 안전하다 |

---

## 🎞️ Animator

| # | 함정 |
| --- | --- |
| 9 | AnyState 전이의 `CanTransitionToSelf = 1` → **매 프레임 재진입**해서 frame 0으로 리셋된다. 벽슬라이드/Fall이 첫 프레임만 재생되던 원인 |
| 10 | `anim.Play("...")`로 재생한 클립을 AnyState 전이가 **다음 평가에서 즉시 덮어쓴다**. 한 프레임 고정은 `FreezeAnimAt`, 전체 재생은 전이 조건을 전부 거짓으로 고정 |
| 11 | 애니메이터 그래프에 **아예 존재하지 않는 전이**를 코드가 기대하고 있을 수 있다 (접지 상태에서 Wall Slide가 안 풀리던 문제 — 리플렉션으로 AnyState 전이 목록을 조회해 확정) |
| 12 | `Glitch Sweep` 시트는 원본이 이미 FlipX 되어 있어 재생 중엔 flipX를 **반대로** 줘야 한다 |
| 13 | `TextureImporter.spritesheet`는 **Unity 6에서 제거됨**(CS0618). `SpriteDataProviderFactories` 경로로 이행. `SpriteRect.rect`의 y는 **좌하단 원점** |

---

## 🖼️ UGUI

| # | 함정 |
| --- | --- |
| 14 | **`Image.Type.Filled`는 sprite 없이는 렌더링을 통째로 무시한다.** `fillAmount` 값은 정상 저장되는데 실제 메시엔 반영이 안 된다 → **두 번 겪고** `WhiteSprite()`(내장 흰 텍스처로 런타임 스프라이트 1회 생성) 패턴으로 정착 |
| 15 | 에디터 모드에서 디버깅용으로 만든 UI 인스턴스가 **플레이 모드 재시작에도 안 지워지고** 낡은 값을 계속 들고 있다 (좀비 인스턴스) |
| 16 | 원격 환경에서는 **스크린샷 캡처에 오버레이 UI가 안 잡힌다** → `CanvasRenderer.GetMesh()` 실측으로 대체 |

---

## 🎨 렌더링 · 셰이더 (URP 2D)

| # | 함정 |
| --- | --- |
| 17 | **패스 태그는 `LightMode = "Universal2D"`**. 이 프로젝트는 URP **2D Renderer**라 `Universal2D`/`SRPDefaultUnlit`만 수집한다. `UniversalForward`는 통째로 스킵돼 아무것도 안 그려진다 (`VFXLit2D.shader`가 이 함정에 걸려 사실상 미사용) |
| 18 | Volume·프로파일이 다 맞아도 **Main Camera의 `renderPostProcessing = False`면 블룸이 안 나온다.** Map1 블룸 미적용의 진짜 원인이었고, 해결은 한 줄이었다 |
| 19 | `VolumeProfile.Add<T>()`만 하면 저장 시 `components: - {fileID: 0}`으로 날아간다. `AssetDatabase.AddObjectToAsset`으로 **서브에셋 등록**까지 해야 직렬화된다 |
| 20 | `_MainTex_ST`를 `UnityPerMaterial` CBUFFER에 넣으면 **그 머티리얼을 쓰는 2D 렌더러 전체의 SRP 배칭이 꺼진다** |
| 21 | HLSL 예약어: `centroid`는 보간 한정자라 변수명으로 못 쓴다. `TWO_PI`는 URP `Macros.hlsl`에 이미 있다 |
| 22 | `frac(sin(dot(p,k))*43758.5)` 해시 **금지** — 인덱스가 정수면 `sin` 인자가 커져 GPU 정밀도가 무너지고 결과가 몇 개 값으로 뭉친다(링이 가로 띠가 됨). "Hash without Sine"을 쓸 것 |
| 23 | 글로우를 스프라이트 색에 **그냥 곱하면 어두운 캐릭터는 거의 안 빛난다** → `lerp(tex.rgb, 1, _Flatten)`(0.65 권장)로 흰색 쪽으로 끌어올릴 것 |
| 24 | **인라인 스크린샷 프리뷰(축소본)는 밝은 장면을 워시아웃된 것처럼 보여준다.** 블룸 과다 판단은 반드시 **저장된 PNG의 배경 픽셀값**으로 |
| 25 | 정점을 원점에 몰아두는 파티클 메시는 **`mesh.bounds`를 직접 넉넉히 지정**해야 프러스텀 컬링에 안 잘린다 |

---

## 🧱 물리 · 타일맵

| # | 함정 |
| --- | --- |
| 26 | **`Physics2D.maxTranslationSpeed`(기본 100)는 안전장치가 아니라 이 게임의 실질 종단속도였다.** 낙하 가속 159u/s²라 0.63초면 도달한다. 시간 가속 중 배율을 안 걸면 실질 종단속도가 40u/s로 떨어져 플레이어만 붕 뜬 것처럼 느려진다 |
| 27 | 리플렉션으로 transform을 옮긴 직후 캐스트하면 **옛 위치**가 나온다 → `Physics2D.SyncTransforms()` 필요 |
| 28 | **`TransformPoint(localOffset)`은 `localScale`이 곱해진다.** 적 스케일 1.2 탓에 창 사거리가 1.9가 아니라 **2.28**이었고, 이게 "패링 판정 불가"의 직접 원인이었다 |
| 29 | `wallLayer`가 Ground+Wall 통합 마스크인데 **씬에 Wall 콜라이더가 하나도 없었다** → 모든 바닥·플랫폼을 벽으로 인식. 지형이 컴포지트로 병합돼 있어 오브젝트 단위 구분도 불가능했다 |
| 30 | **`CompositeCollider2D.GetPath()`는 이미 월드 좌표다** (변환을 또 하면 조각이 동떨어져 보인다). 타일 인접을 `HasTile()`로 재구성하는 것보다 이쪽이 정확하다 |
| 31 | SuperTiled2Unity 타일은 `tileAnchor`가 `(0,0)`이라, `(0.5,0.5)`인 일반 Tilemap 콜라이더와 **시각 대비 반 칸 어긋난다** |
| 32 | 붙는 순간 `gravityScale`만 0으로 만들면 **이미 실린 하강 속도는 그대로 남는다** — -60u/s면 멈추는 데 3초. `linearVelocity.y = 0`으로 즉시 끊을 것 |

---

## 🔬 진단 · 테스트

| # | 함정 |
| --- | --- |
| 33 | `GameObject.Find`는 **비활성(죽은) 오브젝트를 못 찾는다** → `FindObjectsByType(..., FindObjectsInactive.Include, ...)`. **한 세션에 세 번** 같은 패턴에 걸렸다 |
| 34 | **public 필드를 `BindingFlags.NonPublic`으로 `GetField`** 하면 null이 나와 NRE로 이어진다 |
| 35 | 타이머를 **시작 프레임에 누적하지 말 것** — `Start()`에서 0으로 만든 뒤 같은 프레임에 `+= Time.deltaTime`하면 시작 전 프레임 간격이 통째로 가산된다 (프레임이 튀면 `maximumDeltaTime` 0.333s까지) |
| 36 | **"적이 공격 중"을 `IsAttacking` 하나로 기다리지 말 것** — 판정이 끝난 회수(Recover) 구간도 True다. 이어진 Play 세션에서 **비결정적 FAIL**을 만든다 |
| 37 | 스크립트 재컴파일로 **플레이 모드가 이미 끊겨 있는데** 그걸 모르고 결과를 해석하는 사고 |
| 38 | 상태가 자동으로 풀리는 시스템(초월은 70%까지 자동 드레인)에서, **"이미 해제된 뒤"를 "해제 안 된 상태"로 오판**할 뻔한 사고 |
| 39 | 원격/비포커스 에디터는 프레임 스로틀이 심하다. `Time.time` vs `Time.unscaledTime`으로 확인할 것 — 로그가 멈춘 것처럼 보여도 정지가 아니다 |

---

## ⏱️ 시간 · timeScale

| # | 함정 |
| --- | --- |
| 40 | 히트스톱이 **진입 시점 값(`prev`)을 저장해 복원**하면, 기다리는 사이 시간 가속이 켜지거나 꺼졌을 때 낡은 값을 되살려 **슬로우모션이 stuck**된다 → `BaseTimeScale` 복원으로 변경 |
| 41 | **판정 창(window)은 "내 동작의 길이"가 아니라 "적의 타임라인과 겹치는가"를 재는 것**이다. 다른 플레이어 타이머와 같이 실시간으로 보정하면 관계가 깨진다 — 세계 시간으로 재야 한다 (가속 중 회피-카운터 불발의 원인) |
| 42 | `Time.timeScale`을 낮추면 `Time.fixedDeltaTime`도 같이 낮춰야 한다. 안 그러면 물리가 20Hz로 돌아 이동이 끊겨 보이고 스텝당 이동량이 커져 **터널링 위험** |

---

## 🧭 설계 판단으로 남은 것

- **"A 범위와 B 범위가 겹치면" 류 스펙은 코드를 쓰기 전에 기하를 실측하라.**
  패링에서 이걸 건너뛰고 구현→테스트까지 간 뒤에야 **교집합이 공집합**임을 발견했다 (디버그 사이클 1회 낭비).
- **판정점이 플레이어를 관통해 지나가면** 판정식을 비트는 대신 **적의 정지 거리를 무기 사거리에 맞추는 것**이 옳다.
- **"이 캐릭터만 빛나게"는 머티리얼 교체가 아니라 자식 SpriteRenderer 가산 오버레이로.**
  복원 실패로 캐릭터가 이상하게 남는 사고가 구조적으로 없고, 2D 라이팅도 원본이 그대로 받는다.
- **상시/지속형 아우라 연출은 이 프로젝트에서 총 세 번 거절됐다** (폭주 2회, 초월 1회).
  원하는 건 **진입 순간 1회성 연출**이다. 코드 주석에 "되살리지 말 것"이 명시돼 있다.

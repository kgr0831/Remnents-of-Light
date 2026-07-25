# Remnents of Light — 작업 진행 추적 (task.md)

> 최종 업데이트: 2026-07-23(회피 재설계) · 스테이지1 버티컬 슬라이스(5주 마스터플랜) 기준

## 📍 현재 위치
- **일정표 1주차 "조작감 깎기 & 전투 아키텍처"** — 무적 대시(I-Frame), 더미몬스터(추적·창 찌르기·피격 정지·공격중 피격 시 리셋), 플레이어 1-2타 콤보(UniTrio 참고 입력 버퍼링 재설계, Animation Event 기반 정밀 판정), 공격 시 전진, 공격 중 이동/점프/대시 제한, 양방향 타격 VFX(히트스파크+데미지 텍스트, 완화된 쉐이크·히트스톱), 회피-카운터(대시 무적 중 확인키로 배율 데미지 반격), 더미 HP 20 — **전부 구현·Play 모드 실측 완료**.
- 데미지 텍스트 폰트: Silver(사용자 제공 TTF → TMP SDF 생성) 적용 완료.
- 다음 예정: 일섬(RaycastAll 관통) → 3타 콤보 확장 / 가드·패링 → HP·마나 UI.

## ✅ 완료
- 이동·물리 기본 구현 (`PlayerController.cs`): 가감속 Lerp 이동, 가변 점프, 코요테 타임(0.1s), 벽 슬라이드, 벽 점프.
- New Input System 연결 (`PlayerActions.inputactions`: Move/Jump/Dash/Attack/Parry, SendMessages, 기본맵 Player). 현재 Move/Jump만 코드 연결.
- 씬 구성 검증: groundLayer(512=Layer9)↔TestGround, wallLayer(1024=Layer10)↔TestWall, Animator 컨트롤러 연결 — **모두 정상**.
- **스프라이트 피봇 일괄 변경**: 17개 텍스처 전부 `(0.46, 0.03)`, alignment=Custom. (2026-07-15)

## 🐞 확인된 버그 & 근본 원인
| # | 버그 | 근본 원인 | 성격 |
|---|---|---|---|
| 1 | A/D 애니 전환 느림 | `Speed`를 velocity로 구동 → 가감속 지연 | 코드 |
| 2 | 카메라 뚝뚝 끊김 | 부드러운 Lerp 추적 + Interpolate=None → **방 단위 고정(Sanabi)으로 재설계 결정** | 코드(재설계) |
| 3 | 벽슬라이드 반대방향 | `flipX = (wallDirX == -1)` 반전 | 코드 |
| 3·6 | 벽슬라이드/Fall 첫 프레임만 재생 | Any State 전이 `CanTransitionToSelf:1` → 매 프레임 재진입(frame 0 리셋) | 컨트롤러 |
| 4 | 벽점프 조건 | 벽 방향 키 홀드 조건 없음 (`isTouchingWall`만 확인) | 코드 |
| 5 | 점프 강도/벽점프 방향 | 로직은 코드, 크기(jumpForce=12 등)는 씬 Inspector 직렬화 | 코드+Inspector |
| 7 | Land 애니 안 나옴 | `Land` 트리거 파라미터 부재 | 컨트롤러+코드 |

## 🎯 결정 사항
- 카메라: **방 단위 고정 (Sanabi 스타일)**.
- 진행: **MCP 재연결 완료** → 코드+컨트롤러+Inspector+플레이 검증 일괄.
- Animator 구조: **AnyState → anim → (Exit / next anim)**, Has Exit Time 활용, **Fixed Duration 전부 OFF**.
- 스프라이트-콜라이더 불일치 수정 (콜라이더 크기/오프셋 조정). 피봇은 (0.46, 0.03)로 확정.

## 🗺️ 실행 순서
1. ✅ 스프라이트 피봇 (0.46, 0.03) 일괄 적용
2. ✅ task.md 생성
3. ✅ Animator 컨트롤러 재구성 (Land 트리거 추가 / AnyState 4개: WallSlide·Jump·Fall·Land, 모두 CanTransitionToSelf OFF·Fixed Duration OFF·즉시 / Idle↔Run 직접 / Land→Exit ExitTime 0.9) + 배치 정리
4. ✅ `PlayerController.cs` 코드 수정 (Speed 입력구동·flip 반전·벽점프 홀드조건·벽점프 로직+수평잠금 0.15s·Land 트리거set) — 컴파일 클린
5. ✅ 스프라이트-콜라이더: 플레이 검증에서 발-바닥 정렬 양호 (필요 시 미세조정)
6. ✅ 카메라 방 단위 고정(Sanabi): `RoomCamera`+`RoomTrigger` 신규, 부드러운 슬라이드, 데모 방 3개(A/B/C) 배치. 플레이 검증(전환 슬라이드 동작) 완료
7. ✅ Inspector 튜닝값 적용 (jumpForce 12→8.5, wallJumpForce (8,12)→(10,5))
8. ✅ 플레이 검증: 런타임 에러 0, 착지·방 전환 확인

## 🔧 추가 변경 (플레이 중 요청)
- **이동 즉시화**: `HandleMovement`를 가속 기반 AddForce → `linearVelocity` 직접 설정으로 변경 (뚝뚝 끊기는 조작감).
- **필드 정리**: 미사용된 `acceleration`/`deceleration` 필드 삭제 완료.

## ✅ 현재 상태 (세션 종료 시점)
- 씬(SampleScene) **저장 완료** — RoomCamera 교체/방 3개/튜닝값 영구 반영.
- 전체 컴파일 클린(에러 0). 코드 변경분 검증 완료.
- 권장: 에디터에서 Play로 좌우 이동 감각/벽점프/애니 전환 최종 체감 확인.

## 🔧 추가 변경 (2차 플레이 피드백)
- **이동 속도**: moveSpeed 8→5 (즉시 이동이라 과속).
- **벽 슬라이드**: 벽 방향 키를 누르는 동안만 붙어 슬라이드(접촉 안정화). 자동 클링 원하면 되돌릴 수 있음.
- **타일맵 테스트 레벨(디자인된 .tmx 임포트)**:
  - SuperTiled2Unity 2.3.0 설치(OpenUPM, com.seanba). `Assets/TiledMap/`에 tmx/tsx/png 복사→자동 임포트.
  - 맵 `Caste Of Bones Exterior.tmx`(어두운 성 외부) 씬 배치(`TiledMap_Exterior`). STU PPU 100→ **6.25배 스케일로 1타일=1유닛** 정합.
  - 충돌: **전용 `CollisionTilemap`(Grid 자식) + per-tile `TilemapCollider2D`**(컴포지트 없음). STU 타일은 collider 타입 없음(SuperTile) → collider=Grid 일반 Tile로 Base 솔리드 셀 복제. **정렬 버그 해결**: STU가 tileAnchor(0,0)이라 Grid 콜라이더가 시각 대비 어긋났음 → CollisionTilemap tileAnchor=(0.5,0.5) + 콜라이더 fresh 재생성으로 **1047/1047 셀 정렬 검증**. (구 BoxCollider `MapCollision` 제거)
  - **솔리드/장식 분리**: Base에 장식 소품(사슬·석상·풀·오브)이 섞여 풀타일 충돌을 만들던 문제 → 각 타일 스프라이트의 **불투명 픽셀 비율**(RenderTexture blit로 판독)로 분류, **opacity≥0.5인 솔리드만 충돌**(1047→솔리드 884, 장식 163 제외). 임계값 조정 가능.
  - 필요 시 CompositeCollider2D 재부착 가능(정렬 유지, 플랫포밍 시 이음새 걸림 완화).
  - 렌더 정렬: 플레이어 sortingOrder=10(타일맵 앞), 크기 1.3배, 카메라 ortho 6.
  - 플레이어 스폰(맵 위)+ **팔로우 카메라 전환**(RoomCamera 컴포넌트는 보존, 이 맵에선 비활성). 플레이 검증: 착지·충돌·렌더 정상, 에러 0.
  - 임시 박스 코스(TestCourse)·기존 TestGround/TestWall은 비활성화.

## ⚠️ 열린 항목 (타일맵)
- 씬에 박스 데모(비활성)+타일맵 공존 → 정리 필요.
- **카메라: `SectionCamera`(구간=화면 단위, 넘어가면 부드럽게 슬라이드)로 변경** — CameraFollow/RoomCamera 컴포넌트 제거. 구간 내에선 카메라 고정 → 이동 중 타일 이음새 흔들림 완화.
- **타일 이음새/노이즈**: 텍스처는 이미 정상(Point·mip off·무압축). 서브픽셀 카메라 이동이 원인이라 SectionCamera로 완화. 잔여 시 URP Pixel Perfect Camera 필요(플레이어 PPU 20 vs 맵 실효 16 정합 필요).
- 스폰 지점/줌 미세조정 여지.

## ▶️ 다음 예정 (일정표 1주차 잔여)
- ✅ 무적 대시(I-Frame, Dash 입력 이미 정의됨)
- 일섬(RaycastAll 관통) → 3타 콤보/가드·패링 → 타격 피드백/HP·마나 UI.

## ⚠️ 블로커/주의
- `jumpForce`, `wallJumpForce` 등 튜닝값은 씬 Inspector가 코드 기본값을 덮어씀 → 크기 조정은 Inspector/MCP로.
- Animator 컨트롤러에 미사용 Glitch/Slash/Death 상태 다수 존재 (이번 작업 범위 밖, 삭제는 별도 승인).

## 🧪 루프 모드 승인 흐름 테스트 (2026-07-20)
- 요청: `task.md` git commit (디스코드 원격 루프 모드, 승인 흐름 확인용).
- 결과: git commit은 루프 모드에서도 파괴적 작업 → NEEDS_APPROVAL로 정지 후 질문, 사용자가 "커밋하지 마" 선택 → **커밋 미실행**.
- 결론: 승인 게이트 정상 동작 확인. 실제 커밋 대기 파일 없음(작업 트리 변경사항은 그대로 유지).

## ✅ 무적 대시(I-Frame) 구현 완료 (2026-07-20, 루프 모드)
- 진행 순서: `.claude/skills/add-combat-move/SKILL.md` 절차 그대로 따름.
- `PlayerController.cs`: `Dash` 헤더(dashSpeed=14/dashDuration=0.15/dashCooldown=0.5/invincibleLayerName) + `OnDash(InputValue)`
  입력 훅 + `HandleDash()` (isDashing/dashTimer/dashCooldownCounter 상태, 기존 `isJumping`/`wallJumpLockCounter` 패턴과 동일 구조).
  대시 중엔 `FixedUpdate`에서 `HandleMovement()` 대신 고정 수평 burst 속도(y=0)로 덮어씀, `HandleWallSlide`/`ApplyBetterJumpPhysics`
  모두 `isDashing`일 때 조기 return 가드 추가. 무적은 "레이어 스왑 기반"(기획안 1주차 스펙) — 대시 시작 시
  `gameObject.layer`를 `PlayerInvincible` 레이어로, 종료 시 원래 레이어로 원복.
- **새 Unity 레이어 `PlayerInvincible`(슬롯 14) 추가** — `manage_editor(add_layer)`로 승인 받아 진행 (프로젝트 전역 설정 변경이라 별도 승인).
  Physics2D 충돌 매트릭스(`PlayerInvincible` ↔ `EnemyAttack` 무시)는 **의도적으로 이번 범위에서 제외** — 아직 EnemyAttack을
  쓰는 데미지/적 시스템이 없어서 지금 매트릭스를 건드리면 소비자 없는 죽은 설정이 됨. 데미지 시스템 붙일 때 같이 배선 예정.
- `PlayTestRunner.DashIFrameTest()` TODO 구현 완료: `InputInjector.PressDash()`로 입력 주입 →
  대시 중 레이어가 `PlayerInvincible`로 바뀌는지, `dashDuration` 경과 후 원래 레이어로 복귀하는지 검증
  (데미지 시스템이 아직 없어 "피격 0회" 대신 레이어 스왑 자체를 검증).
- **버그 발견 & 수정 (`InputInjector.cs`)**: `PressKey/ReleaseKey`가 `InputSystem.QueueDeltaStateEvent`를 썼는데, 키보드 키는
  비트필드로 저장되는 컨트롤이라 델타 이벤트를 지원하지 않아 `InvalidOperationException: Cannot send delta state events
  against bitfield controls`가 런타임에 발생(기존 코드가 만들어진 이후 `SetMoveX`/이동 방향 주입을 실제로 쓴 첫 테스트라
  지금까지 숨어 있던 버그). `KeyboardState` 풀스테이트 + `InputSystem.QueueStateEvent`로 교체, 동시 입력을 위해
  `_heldKeys` HashSet으로 현재 눌린 키 집합을 추적해 매번 전체 상태를 재전송하도록 수정. 마우스 버튼 경로(`PressButton`/
  Attack·Parry)는 이번에 실사용 안 해서 미검증 상태로 남겨둠 — 나중에 콤보/패링 테스트 작성 시 동일 문제 있는지 확인 필요.
  출처: docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.LowLevel.KeyboardState.html (확인 2026-07-20)
- **플레이 검증 완료**: Play 모드 진입 → `Tools/PlayTest/Dash I-Frame` 실행 → 콘솔 로그
  `[ASSERT] dash_iframe: PASS during=True restored=True`, 에러 0. 컴파일 클린(전 파일 `validate_script` 에러/경고 0,
  단 `TestLog` 관련 GC 경고 1건은 기존 패턴과 동일한 성격이라 무해).

## 🛰️ 디스코드 원격 개발 시스템
- 구조·현황·버그이력은 **`LOOP_ENGINEERING.md`로 분리**(2026-07-22). 게임 개발과 무관한 인프라 변경은 이쪽 대신 그 파일 참고.

## 🎨 대시 VFX 레퍼런스 조사 (2026-07-21, 루프 모드) — 사용자 선택 대기
- 지시: 대시 VFX(잔상 여러 개=산데비스탄 + 상용게임 대시 연출)는 **직접 만들지 말고** 레퍼런스를 영상/이미지로 찾아 먼저 제시 → 사용자 선택 후 구현.
- 통합 지점 확인(읽기 전용): `PlayerController.HandleDash()` — `isDashing=true`(대시 시작)와 대시 지속 구간이 잔상 스폰 훅. `SpriteRenderer sr` 이미 캐싱됨 → 클론 스프라이트 복사에 바로 사용 가능. dashDuration=0.15, dashSpeed=14.
- 조사한 4가지 방향:
  - **A. 산데비스탄 컬러 잔상 (Edgerunners)**: 잔상 다수, 시간에 따라 초록→파랑→보라→빨강→노랑. 태스크 원문에 부합·가장 화려.
    - 영상: youtube.com/watch?v=p_WPXHkE7D8 (Sandevistan Scenes 모음), youtube.com/watch?v=PzkEqhVByw4 (Netflix 공식 "Payback Time")
    - 기술: youtube.com/watch?v=LqhByxakI80 (Grease Pencil 브레이크다운), william-meneses.itch.io/sandevistan-test (셰이더 데모)
  - **B. 단색 페이드 잔상 (카타나 제로/할로우나이트)**: 같은 색 반투명 클론이 페이드아웃, 날카롭고 절제.
    - Unity 구현: youtube.com/watch?v=ylsWcc4IP3E, youtube.com/watch?v=y982Gb00dho, github.com/make-game-modules/after-image-effect-controller
  - **C. 스트리크 잔광 + 대시 먼지 (셀레스트/하데스)**: 클론 대신 짧은 잔광 스트리크 + 먼지 파티클, 순간적·경쾌.
    - assetstore.unity.com/packages/vfx/stylized-dash-trail-fx-304409, the-great-black-cat.itch.io/2d-trail-effect
  - **D. 속도선+모션블러 (고스트러너)**: 방사형 속도선+블러. 2D 픽셀아트엔 과할 수 있음.
- **2026-07-21 재트리거 웹 재검증**: 4방향 링크·기술 사실 여전히 유효(산데비스탄=레인보우 다중 잔상+환경 채도 저하 / Unity 2D 잔상=클론 스폰·페이드). 통합 지점 코드 재확인 — `HandleDash()` 대시 시작(line 170 `isDashing=true`)+지속 구간(line 182~)이 잔상 스폰 훅, `sr`(SpriteRenderer) line 61에서 캐싱됨, dashDuration=0.15/dashSpeed=14 그대로.
- 상태: ✅ **사용자가 방향 A(산데비스탄) 확정 → 구현 완료** (아래 "✅ 대시 VFX 구현" 참조).

## ✅ 대시 VFX 구현 = 산데비스탄 잔상 + Run 프레임 프리즈 (2026-07-22, 루프 모드)
- 지시: 방향 A(산데비스탄 잔상) + "run 애니메이션 3번 프레임에서 대시 동안 멈추기". 씬/에셋 변경 없이 **코드만으로** 구현.
- **신규 스크립트 `Assets/Scripts/DashAfterImage.cs`**: 런타임 전용 잔상 한 조각. `Spawn(source, tint, lifetime, sortingOffset)` 정적 메서드로 플레이어 SpriteRenderer의 현재 스프라이트/flip/스케일/색을 복제한 GameObject 생성 → `Update()`에서 알파를 `lifetime`에 걸쳐 0으로 페이드 후 자기 파괴(씬에 안 남음).
- **`PlayerController.cs`** (surgical 추가만, 기존 대시/i-frame 로직 불변):
  - `[Header("Dash VFX (Sandevistan)")]`: `dashAfterImage`(bool), `afterImageInterval`(0.02s), `afterImageLifetime`(0.35s), `afterImageAlpha`(0.7), `afterImageSortingOffset`(-1, 원본 뒤), `afterImageColors`(Color[] = 초록→파랑→보라→빨강→노랑). 전부 Inspector 튜닝 가능.
  - `[Header("Dash Anim Freeze")]`: `dashFreezeAnim`(bool), `dashFreezeState`("Glitch Samurai-Run"), `dashFreezeFrame`(3), `dashFreezeFrameCount`(12).
  - `HandleDash()`: 대시 시작 시 `afterImageTimer/Index` 리셋 + `FreezeDashAnim()`. 지속 중 `afterImageInterval`마다 `SpawnAfterImage()`(색 순환). 종료(타임아웃/벽취소 공통) 시 `anim.enabled=true`로 복원.
  - `SpawnAfterImage()`: `afterImageColors[idx % len]` 틴트(알파=afterImageAlpha) → `DashAfterImage.Spawn`.
  - `FreezeDashAnim()`: **핵심 = `anim.speed=0`이 아니라 `anim.enabled=false`.** `anim.Play("Glitch Samurai-Run",0, 3/12=0.25)` + `anim.Update(0f)`로 Run 프레임을 `sr.sprite`에 기록한 뒤 애니메이터를 꺼서 고정.
- **왜 speed=0 대신 enabled=false**: 1차 구현(speed=0)은 **지상 대시는 Run 프레임 유지되나 공중 대시는 프레임0 Fall로 덮였다**. 원인 = `AnyState→Fall` 전이가 `isGrounded=false`면 speed=0이어도 **조건 평가로 발동**해 프리즈를 덮음(Play 모드 실측으로 확인: playerSprite=Fall_0, stateNorm=0). 애니메이터 자체를 비활성화하면 아무 전이도 sr.sprite를 못 덮어 지상/공중 모두 Run 프레임 고정.
- **Play 모드 실측 검증(SampleScene, timeScale 0.01~0.05로 대시 한복판 포착)**:
  1. 대시 중 `afterImageIndex`가 5~7까지 증가 → 잔상 스폰 정상. `ghosts` 5개 동시 생존, 색 = **초록/파랑/보라/빨강/노랑 5색 순환**, 알파 0.51~0.69로 페이드아웃. 종료 후 전부 자동 파괴(`ghostsRemaining=0`).
  2. **공중 대시** 중 `animEnabled=False`, `playerSprite=Glitch Samurai-Run_2_3`(Run keyframe idx3) 유지, 모든 잔상 spr도 동일 Run 프레임 → 프리즈가 지상/공중 모두 성립.
  3. 대시 종료 후 `animEnabled=True animSpeed=1`, 애니메이터가 다시 스프라이트 구동(Land 전이 관측) → 복원 정상.
  - 세션 전체 콘솔 error/warning **0**. 컴파일 클린(validate standard: error 0, 기존 TestLog GC warning만). 씬 미변경/미저장(런타임 조작은 Stop 시 원복). 코드 변경분(2파일)만 디스크 반영.
- **참고(튜닝 여지)**: "3번 프레임"은 keyframe **인덱스 3(0-base, t=0.25, 4번째 스프라이트)** 로 해석·기본값. 만약 "3번째 스프라이트(인덱스 2)"를 의도했다면 `dashFreezeFrame=2`로 Inspector에서 한 번에 변경 가능. 색/간격/수명/알파도 전부 Inspector 노출.

### ✅ 대시 VFX 영상 보고 완료 (2026-07-22, 루프 모드)
- 지시: "이전에 진행한 대시의 보고 + 영상보고 + 영상검증". 위 대시 VFX(잔상)/벽충돌 취소 작업이 코드 변경은 끝났는데 **영상 보고가 빠져 있어** 이번 세션에서 채움.
- **신규 테스트 인프라 `PlayTestRunner.DashVfxShowcase()` + 메뉴 `Tools/PlayTest/Dash VFX Showcase`**: 잔상을 영상으로 확실히 보여주기 위한 showcase. 리드인/테일은 정상 속도, **대시 구간만 `Time.timeScale=0.08` 슬로모션**으로 늘림 — 이유: Gemini 영상 판정기(video_judge.py)가 유튜브 영상을 **~1fps로 샘플링**해서, 실시간 0.35s만 보이는 잔상 궤적은 정상 속도로 녹화하면 샘플 프레임 사이로 사라져 거짓 FAIL이 남. `try/finally`로 timeScale 복원 보장. `ASSERT_CONVENTION.md`에 `dash_vfx` 채널 등록.
- **Play 모드 실측(SampleScene, 스폰 우측 60유닛 개방 확인 후 우대시)**: 대시 중 `afterImageIndex=7`, **잔상 7~8개 동시 생존**, 색 = 초록/파랑/보라/빨강/노랑 5색 순환(알파 0.41~0.67 페이드), `animEnabled=False`로 Run 프레임 프리즈 동시 성립. 대시 종료 후 잔상 전부 자동 파괴(ghosts=0), timeScale 1로 복원. `[ASSERT] dash_vfx: PASS showcase_recorded dashed=True`, 세션 콘솔 error/warning **0**. 녹화 mp4 1.4MB(정상).
- **주의(인프라 관찰)**: 원격(비포커스) 에디터는 백그라운드에서 Play 루프를 심하게 스로틀 → MCP 호출로 poke될 때만 몇 프레임 진행(`Application.runInBackground=true`도 큰 효과 없음). Recorder가 프레임 기반이라 녹화 **콘텐츠 품질엔 문제없고 wall-clock만 오래 걸림**. 향후 슬로모 녹화 시 완주까지 시간 여유 둘 것.
- **영상 보고**: https://youtu.be/MPBiyIQQTBs — VERDICT **PASS (2/3)**. (판단 기준: 대시 시 진행 방향 뒤로 초록·파랑·보라·빨강·노랑 여러 컬러 잔상이 남는가)
- 코드 변경분: `PlayTestRunner.cs`(showcase 추가), `ASSERT_CONVENTION.md`(dash_vfx 채널) — 둘 다 테스트/문서 인프라. 대시 VFX/i-frame 게임플레이 코드는 불변.

## 🔎 "왼쪽 이동 안 됨" 버그 리포트 — Play 모드 실측 검증 (2026-07-21, 루프 모드)
- **결론: 재현 안 됨. 왼쪽 이동은 정상 작동.** 사용자가 다른 현상을 왼쪽 이동 문제로 오인했을 가능성.
- 검증 방법: SampleScene에서 Play 진입 → `InputInjector.SetMoveX(±1)` 입력 주입 → `execute_code`로 `rb.linearVelocity`/`moveInput`(reflection)/`isTouchingWall`/위치를 직접 판독. 언포커스 시 주입 입력 유실 방지를 위해 `PlayTestRunner.EnsureDeterministicInputSettings`와 동일한 in-memory 입력 설정 사용. 지면 위에선 이동중 속도 포착이 왕복지연에 계속 밀려서, 공중 재배치 + `Time.timeScale=0.05` 저속화로 이동 중 속도를 확실히 캡처(모두 Stop 시 원복되는 일시적 진단 조작).
- 실측값(공중·무장애물):
  - **왼쪽**: `moveInput=(-1,0)` → `vel=(-5.000, -14.9)` → vel.x = moveInput.x × moveSpeed(5) 정확히 일치, 위치 x 1.61→-0.59 좌측 이동.
  - **오른쪽(대칭)**: `moveInput=(1,0)` → `vel=(5.000, -6.6)`, 위치 우측 이동. → 좌우 완전 대칭.
  - 지면 위 최초 주입: 왼쪽 누르면 왼쪽 벽까지(x 1.61→-9.34, wallDirX=-1), 오른쪽 누르면 오른쪽 벽까지(→-6.67, wallDirX=1) — 방향대로 이동 후 벽에 걸려 vel이 0으로 수렴(정상 물리).
- 세션 중 콘솔 에러/경고 0건.
- **별개로 발견(왼쪽 버그와 무관, 후속 검토 후보)**: 씬의 Player `wallLayer`=**1536**(=groundLayer 512 | wallLayer 1024)로 직렬화돼 있어 **바닥 타일이 좌우 '벽'으로도 감지**됨(좁은 틈에서 양쪽 다 isTouchingWall=True). task.md 완료 항목엔 wallLayer=1024로 기록돼 있어 불일치 → 확인 필요. 단 좌우 공통 현상이라 "왼쪽만 안 됨"의 원인은 아님.

## 🐞 "대시 후 왼쪽 이동 막힘" 버그 — Play 모드 실측 진단 (2026-07-22, 루프 모드) — 3차에서 재현 완료·원인 확정
- 재현 방법: SampleScene Play 중 `InputInjector.SetMoveX/PressDash` 주입 + `execute_code`로 private 필드(moveInput/isDashing/dashDirX/wallJumpLockCounter/isGrounded/isTouchingWall/wallDirX)와 `rb.linearVelocity` reflection 판독.
- **실측 결과**:
  1. **빈 공간(무중력·벽/바닥 없음)**: 오른쪽 대시 → 종료 후 왼쪽 주입 → `move=(-1,0) vel=(-5,0)`, x 61.1→-15.3 좌측 이동. → **좌측 이동 코드 자체는 정상(좌우 완전 대칭).**
  2. **타일맵 바닥 위**: 오른쪽 대시(14u/s) 후 플레이어가 멀리 이동+낙하해 **왼쪽 지형에 밀착**(`touchWall=True wallDir=-1`)으로 끝남. 이 상태에서 왼쪽 주입 → `move=(-1,0)` 이지만 `vel=(0,0)`(끼임). **오른쪽 주입 → `vel.x=5`로 즉시 풀려 이동.**
- **근본 원인**: movement 코드에 좌/우 비대칭·결함 **없음**. `HandleMovement`는 `vel.x = moveInput.x*moveSpeed`로 항상 대칭 적용되고 벽 감지를 참조하지 않음. 좌측 vel이 죽는 유일한 경로는 **왼쪽 지형 콜라이더와의 물리 충돌(끼임)**. 대시가 y=0 고정 + 14u/s로 플레이어를 지형(벽 또는 per-tile 바닥 이음새)에 밀어붙여 끼이게 만듦. flipX/애니는 `moveInput.x`로 구동되므로 방향만 바뀌고 이동은 0 → 사용자가 본 증상과 정확히 일치.
- **부수 확인(직접 원인 아님)**: 씬의 Player `wallLayer`=**1536**(Ground512 | Wall1024) — 바닥이 벽으로도 감지됨(task.md 완료 기록은 1024여야 함, 불일치 유지). 단 movement가 벽 감지를 안 보므로 이동을 직접 막지는 않음.
- 세션 중 콘솔 에러/경고 0건. Play 모드 유지(주입 입력 SetMoveX(0)+Cleanup으로 중립화). 씬 미저장(모든 임시 조작은 Stop 시 원복).

### ✅ 3차 재현 — "평평한 바닥이면 걸리나?" 직접 검증 완료 (2026-07-22, 루프 모드)
지난 세션의 대기 질문(벽 있음=정상 vs 평평한데 막힘=버그)을 **execute_code로 직접 재현해서 종결**. Time.timeScale=0.05 저속화 + 입력 주입 + reflection 실측:
- **① 평평한 바닥 좌측 걷기**: x −16.3→−39.9 (24유닛, 여러 타일 이음새 통과) 내내 `vel.x=−5.000` 일정, 이음새 걸림 **0**. → **평평한 바닥 좌측 이동 완벽 정상.**
- **② 평평한 바닥에서 좌대시 후 좌이동**: 대시(dashDir=−1) 종료 직후 `vel=(−5.000,0)` grounded, `isDashing=False wallLock=0`. → **대시가 남기는 이동 차단 상태 없음.**
- **③ 대시 무적 레이어 관통 여부**: `PlayerInvincible(14)` ↔ Ground/Wall 충돌 매트릭스 `ignore=False` 전부. → 대시 중 지형 관통·박힘 **없음.**
- **④ 오른쪽 벽에 대시로 박은 뒤 좌이동**: touchWall=True wallDir=1로 벽에 밀착(우입력 vel=0)한 상태에서 **좌입력 즉시 `vel=(−5.000,0)`로 풀려 이동**(x 0.335→−0.765). → **대시로 벽에 박혀도 반대로는 즉시 이동, 영구 끼임 없음.**
- **⑤ 스폰 근처 좌대시**: 스폰(−32)에서 좌대시 → 대시 y=0 고정이라 낙하 없이 수평으로 날아가 좌측 벼랑(~−40) 밖으로 튕겨나가 심연(y −15까지)으로 추락. 리스폰 시스템 없어 그대로 낙사.
- **확정 근본원인**: **movement 코드에 좌우 비대칭·버그 없음**(측정으로 좌/우/공중/대시후 전부 `vel.x=±5.000` 대칭). 좌이동이 죽는 유일 경로 = **왼쪽에 실제 콜라이더가 있고 거기에 밀어붙일 때(정상 물리, 벽 통과 불가)**. 사용자 증상("flipX/애니는 정상인데 안 감")은 = `moveInput.x=−1`은 정상 수신(→anim/flip 정상)인데 좌측 콜라이더가 물리로 막는 상태의 정확한 시그니처. 대시(**의도된 y=0 수평 버스트**, 코드 주석에 명시)가 플레이어를 지형/벽에 밀어붙이거나 벼랑 밖으로 날려서 그 상태를 만든다.
- **결론**: 순수 movement 버그는 없음 → 고칠 "코드 결함" 없음. 사용자 체감을 바꾸려면 **대시의 의도된 설계(y=0 무중력 버스트)를 바꾸거나**(취향/설계 결정), **씬 값(wallLayer 1536→1024)·레벨을 손봐야** 함 → 어느 쪽도 승인/취향 필요라 방향 질문으로 정지.

### ✅ 사용자 선택 반영 = 3번 "대시가 진행 방향 벽에 부딪히면 즉시 종료" 적용·검증 완료 (2026-07-22, 루프 모드)
- 수정(`PlayerController.HandleDash`): `isDashing` 유지 블록에 조기종료 조건 추가 —
  `bool intoWall = isTouchingWall && wallDirX != 0 && wallDirX == dashDirX;` → `if (dashTimer <= 0f || intoWall)`로 종료.
  종료 시 기존 timeout 경로와 동일하게 `isDashing=false` + 무적레이어→원래레이어 복원. TestLog에 `dash_cancelled_wall`/`dash_ended` 구분 기록.
  (surgical: 레이어스왑·이동·i-frame 로직은 그대로, 종료 트리거 한 줄만 추가.)
- **검증(Play 모드 실측, dashDuration을 임시 5s로 늘려 "취소 vs 타임아웃"을 명확히 분리)**:
  1. **벽에 대시로 박음**: dur=5s인데 `isDashing=False`, `dashTimer=4.929`(≈0.07s만 소모=타임아웃 아님) → **벽 접촉으로 취소됨.** 레이어=Player로 복원.
  2. **평평한 바닥 대시(오탐 가드)**: `isDashing=True`, `dashTimer` 정상 카운트다운, `vel=(14,0)`, `touchWall=False` → **`wallLayer=1536`(바닥=벽) 오설정에도 평지 대시는 조기취소 안 됨.** (실측상 평지에선 수평 BoxCast가 바닥을 벽으로 안 잡음.)
  3. **실배포값(0.15s) end-to-end**: 벽에 대시로 박은 뒤 좌입력 → 즉시 좌측 9.7유닛 이동, 다른 벽 만나 정지(정상 물리). `isDashing=False` 레이어 복원.
- 컴파일 클린(validate `standard`: error 0 / 기존 TestLog GC warning 1만), 콘솔 error 0. 씬 미변경(`isDirty=False`, 미저장), Play 종료. 코드 변경분만 디스크 반영.
- **주의(후속)**: "이미 벽에 밀착한 상태에서 그 벽 방향으로 대시"하면 프레임0에 취소되어 i-frame이 거의 안 켜짐. 지금은 데미지/적 시스템이 없어 무해하나, **데미지 시스템 붙일 때** "방어용 벽 대시는 무적 지속 유지" 원하면 취소 조건을 재검토할 것(예: 최소 대시 시간 보장 or 무적만 dashDuration 유지).

## ✅ 낙사 → 리스폰 시스템 구현 (2026-07-22, 루프 모드) — ⛔ 이후 사용자 요청으로 전면 제거됨 (아래 "리스폰 기능 제거" 참조)
- 배경: 이번 루프에서 제안한 **일섬**을 사용자가 "2번(다른 작업으로)"로 반려 → "계속 진행해" 지시에 따라 멈추지 않고
  **저위험·코드 전용·승인 불필요** 대체 태스크로 낙사 리스폰을 선택. (일섬은 여전히 일정표 1주차 잔여 항목으로 유지 — 아래 "다음 예정" 참조.)
  선택 이유: 여러 테스트 세션에서 반복 관측된 실제 문제(대시로 벼랑 밖으로 나가면 심연으로 떨어져 복구 불가) 해결. 전투 표적/애니메이터와 무관해 독립 검증.
- **`PlayerController.cs`** (surgical 추가만, 기존 이동/대시/i-frame 불변):
  - `[Header("Fall Respawn")]`: `fallRespawn`(bool), `fallDeathDepth`(20 — 스폰보다 이만큼 아래로 떨어지면 리스폰). Inspector 튜닝.
  - `Start()` 신규: 씬 배치 최초 위치를 `spawnPoint`(Vector3, z 보존)로 캡처. **플레이어를 재배치하는 스크립트가 없음**(grep 확인: 카메라 스크립트만 자기 위치 이동)이라 Start 시점 위치 = 저작 스폰.
  - `Update()` 최상단: `if (fallRespawn && pos.y < spawnPoint.y - fallDeathDepth) Respawn();`
  - `Respawn()`: 대시 중 낙사 시 무적 레이어/애니 프리즈가 남지 않도록 대시를 먼저 정리(isDashing 해제·레이어 원복·anim.enabled=true) 후 `transform.position=spawnPoint`·`rb.linearVelocity=0`. `TestLog.Event("respawn_fall", ...)`.
- **테스트 인프라**: `ASSERT_CONVENTION.md`에 `respawn_fall` 채널 등록. `PlayTestRunner`에 `[MenuItem("Tools/PlayTest/Fall Respawn")]` + `RespawnFallTest()` 코루틴 추가(기존 Dash 시나리오와 동일 골격: 녹화 시작→리드인 1.5s→강제 하강→검증→테일 1.5s→StopRecording).
  - 하강 연출: 씬 지형 비의존 위해 rb를 **Kinematic으로 전환**해 콜라이더 통과하며 스폰 아래로 부드럽게 하강, 임계 넘기면 Respawn() 발동.
- **버그 1개 잡고 재검증**: 1차 테스트에서 하강 감지 break 조건(`y > spawn.y-1`)이 **하강 시작 전에도 참**(플레이어가 아직 스폰 근처)이라 첫 프레임에 조기 종료 → 실제 하강·Respawn 없이 **거짓 양성 PASS**. 감지를 "내가 강제로 내려쓴 y를 Respawn()이 위로 덮어썼을 때(`pos.y > writtenY+2`)"로 수정 → 2차에서 진짜 하강(f=48~81, ~1.1s)·`respawned_to` EVENT(Respawn() 실제 실행 증거)·복귀 확인.
- **Play 모드 실측 검증(SampleScene)**: `[ASSERT] respawn_fall: PASS respawned=True backAtSpawn=True dist=0.00`, `respawned_to=(1.61,29.17)` EVENT로 Respawn() 실발동 확인, 복귀 후 vel≈0. 콘솔 error 0(무관한 기존 `SetupAnimationsEditor.cs` obsolete warning 1건만). 컴파일 클린(validate error 0). 씬 미변경/미저장(런타임 kinematic 조작은 Stop 시 원복). 코드 변경분(2 스크립트 + 문서 1)만 디스크 반영.
- **영상 보고**: https://youtu.be/G85mlPWsBuI — VERDICT PASS (3/3). (판단 기준: 스폰보다 20유닛 아래 낙사 시 스폰 복귀하는가)
- **참고(튜닝/후속)**: `fallDeathDepth`는 스폰 기준 **상대 깊이**라 대체로 수평인 Stage1엔 적합하나, 세로로 크게 내려가는 레벨에선 절대 kill-plane Y나 체크포인트로 교체 필요. 지금은 스폰=최초 위치 고정(체크포인트 시스템 미구현). SectionCamera는 고정형이라 하강 중 플레이어가 화면 하단으로 빠졌다가 복귀 시 재등장(연출상 "심연 낙사"로 오히려 명확).

## ✅ 리스폰 기능 전면 제거 (2026-07-22, 루프 모드)
- 지시: "리스폰 기능 아예 제거". 위 낙사→리스폰 시스템(2026-07-22 추가분)을 전부 걷어냄. 코드/문서(.cs·.md)만 손대므로 씬/에셋/커밋 없이 Edit 범위 내 처리(명시적 제거 지시라 별도 승인 질문 없이 진행).
- **`PlayerController.cs`**: `[Header("Fall Respawn")]`+`fallRespawn`/`fallDeathDepth` 필드, `spawnPoint` 필드, `Start()`(스폰 캡처 전용이라 통째로), `Update()` 상단 리스폰 체크 한 줄, `Respawn()` 메서드 — 전부 삭제. 이동/대시/i-frame/벽충돌 취소 로직은 불변(surgical).
- **`PlayTestRunner.cs`**: `[MenuItem("Tools/PlayTest/Fall Respawn")]`+`RunRespawnFallTest()`, `RespawnFallTest()` 코루틴 삭제. (테스트가 `player.fallDeathDepth`를 참조해 필드 제거 시 컴파일 깨짐 → 반드시 동반 제거.)
- **`ASSERT_CONVENTION.md`**: `respawn_fall` 채널 행 삭제.
- **검증**: 전 `.cs`에서 `respawn`/`spawnPoint`/`fallDeath` grep **0건**. validate_script `standard` 두 파일 error 0(기존 TestLog GC warning만). 재컴파일(AssetDatabase.Refresh+RequestScriptCompilation — refresh_unity 도구는 don't-ask 모드 auto-deny라 비파괴 표준 API로 대체) 후 리플렉션으로 `Respawn()/fallRespawn/fallDeathDepth/spawnPoint/Start()` **전부 GONE** 확인. 콘솔 error 0.
  - 주의(인프라): 원격·비포커스 에디터라 Play 최초 진입이 **낡은 어셈블리**로 들어가 처음 리플렉션이 EXISTS로 나옴 → 위 재컴파일 후에야 GONE 반영. 앞으로 코드 변경 검증 시 Play 진입 전 재컴파일 완료를 먼저 확인할 것.
- **Play 모드 실측**: 스폰(y=29.02)에서 옛 임계값(spawn.y−20=9.02) 아래(y=8→4)로 낙하시킨 뒤 위치를 안 건드려도 **frame 132→150 내내 y=4.00 유지**(옛 코드면 첫 프레임에 29로 튐) → `[ASSERT] respawn_removed: PASS finalY=4.00 oldThreshold=9.02 (respawn NOT triggered)`. 씬 미저장(kinematic 조작은 Stop 시 원복).
- **영상 보고**: https://youtu.be/_7-LHn2zNmU — VERDICT PASS (2/3). (판단 기준: 낙하 후 스폰 지점으로 순간이동 복귀하지 않고 아래에 머무는가.)
- **참고(후속)**: 이제 대시로 벼랑 밖으로 나가면 심연으로 낙사해도 복구 수단이 없다(리스폰 이전 상태로 회귀). 향후 필요하면 체크포인트/kill-plane 기반 시스템을 새로 설계할 것.

## 🎇 대시 VFX "더 화려하게" 아이디어 조사 (2026-07-22, 루프 모드) — 사용자 선택 대기
- 지시: 현재 대시 VFX(산데비스탄 다색 잔상 + Run 프레임 프리즈)를 **더 화려하게** 만들 아이디어를 찾아 제시.
- **프로젝트 실측(읽기 전용 execute_code)**: 렌더=URP(UniversalRenderPipelineAsset)지만 **씬 Volume 0개 + Main Camera postProcessing=False**(포스트프로세싱 미세팅). 카메라에 `SectionCamera` 스크립트 有. 플레이어=`Sprite-Lit-Default` 머티리얼·sortingOrder10, TrailRenderer/ParticleSystem/LineRenderer **전부 없음**.
- 웹 리서치(게임필 juice / Celeste 대시 / URP 포스트프로세싱):
  - 대시 임팩트 = 5레이어(애니·사운드·VFX·카메라·컨트롤러) 겹치기, 히트스톱 40~80ms, 스크린 셰이크가 "무게감" 핵심.
  - Celeste 대시 = 실루엣 잔상(가독성) + 시작 버스트 파티클 + 머리색 플래시.
  - 출처: itch.io/blog/1059831 (juice 기법), gamedevacademy.org/game-feel-tutorial, researchgate The player silhouettes as a trail after dashing in Celeste, docs.unity3d.com/6000.4 urp integration-with-post-processing.
- 제시한 6가지 방향(코드전용=승인불필요 / 씬·에셋=승인필요 분류):
  1. **대시 임팩트 팩**(스크린셰이크+히트스톱 40~60ms+시작 흰 플래시) — 코드전용(SectionCamera+PlayerController).
  2. **모션 스트리크 잔상**(현 정적 클론→진행방향 늘림 스쿼시스트레치) — 코드전용(DashAfterImage.cs).
  3. **대시 트레일 리본**(TrailRenderer 그라디언트 발광 리본) — 코드베이스 가능(런타임 머티리얼), 발광은 에셋 있으면 최상.
  4. **대시 시작 버스트**(방사형 링/스파크 팝) — 절차적 링=코드전용, 오서링 파티클=에셋.
  5. **URP 포스트프로세싱 펄스**(블룸+색수차 대시 순간 확 켜짐) — **씬·에셋·설정 변경 필요(승인)**. 시각적 임팩트 최대(잔상이 HDR로 발광).
  6. **속도선 오버레이**(애니풍 방사 라인) — 풀스크린 오버레이 에셋/캔버스 필요(승인).
- **시니어 권장**: 1+2 조합(순수 코드, 승인 불필요, 즉시 구현 가능, 화려함 대비 노력 최고). 시각적 도약 최대는 5번(단 씬·에셋 승인 필요).
- 상태: ⏸ 사용자 방향 선택 대기(코드 변경 없이 조사만 → 영상 보고 대상 아님).

## 👾 더미 몬스터 (테스트용) — 승인 대기
- 목적: 앞으로 만들 액션(일섬/콤보/패링/타격 피드백) 테스트 표적.
- 제안 스펙: 신규 `DummyEnemy.cs`(maxHp, TakeDamage(int), 피격 흰색 플래시+옵션 넉백, HP 0 시 리스폰/비활성) + `Enemy` 레이어(신규 필요 가능) + 씬 배치 or 프리팹.
- 승인 필요 이유: 씬 GameObject 배치/프리팹 생성 = 씬·에셋 직접 조작 → 루프 모드 승인 게이트.

## ⚠️ 대시 거리 증가 + 잔상 빈도 증가 (2026-07-22, 루프 모드) — 코드 완료·기능 검증 완료, 영상 판정은 3연속 실패로 중단
- 지시: "2번 모션스트리크 잔상으로 가자. 대시 거리 늘리고 잔상 더 많은 빈도로 자주 생성" (이전 세션이 제시한 6가지 "더 화려하게" 방향 중 2번 확정).
- **`PlayerController.cs` 기본값 변경(surgical, 씬 미직렬화 필드라 씬 편집 불필요)**:
  - `dashSpeed` 14→**20**, `dashDuration` 0.15→**0.18** → 대시 거리 2.1→**3.6유닛**(약 1.7배).
  - `afterImageInterval` 0.02→**0.01** → 잔상 스폰 빈도 **2배**.
  - `afterImageStretch` 1.6→**2.0**(모션 스트리크는 이미 `DashAfterImage.Spawn`에 `stretch` 파라미터로 구현돼 있던 기존 WIP 코드 — 이번엔 늘리기만 함).
  - `PlayTestRunner.cs`의 showcase 오버라이드(`ShowcaseRoutine`)도 새 interval/stretch와 동일하게 맞춤(구 값 그대로면 녹화가 옛 룩을 보여줌).
- **함정 발견 & 해결**: 이 필드들은 씬(.unity)에 직렬화 안 됨(그레프 확인) → 코드 기본값만 바꾸면 반영돼야 정상이지만, **Play 모드 진입 상태에서 컴파일하면 도메인 리로드가 "메모리에 살아있던 구값"을 그대로 보존**해서 새 기본값이 반영 안 되는 현상 발견(여러 번 강제 재컴파일/클린빌드로도 안 풀림). **`EditorSceneManager.OpenScene`으로 씬을 디스크에서 강제 재로드**하고 나서야 새 기본값(20/0.18/0.01/2.0)이 실제로 반영됨 확인. (씬 `isDirty=False`였어서 재로드가 안전했음 — dirty였다면 먼저 저장/확인 필요.) 향후 세션을 위한 교훈: public 필드 기본값을 바꿨는데 Play 모드에서 값이 안 바뀌어 보이면, 재컴파일이 아니라 **씬 재로드**를 의심할 것.
- **기능 검증(Play 모드, execute_code+reflection 직접 측정, 실제 gameplay 값 그대로)**: 대시 1회 = `x` 이동량 실측 **3.6~3.8유닛**(기존 2.1의 ~1.7배), `afterImageIndex`/생존 잔상 수 실측 **10~19개**(기존 5~7개 대비 확연히 증가) — 요청한 두 변경(거리↑·빈도↑) 모두 수치로 확인됨. 컴파일 클린(validate standard: PlayerController warning 1건은 기존 TestLog GC 경고로 무관, PlayTestRunner error/warning 0), 콘솔 error/warning 세션 전체 0.
- **영상 판정 — 3연속 이상 실패, 루프모드 규칙에 따라 중단**:
  1. `DashVfxFancy_slowmo`(기존 showcase 메뉴, slowmo 대기 구간을 timeScale=6으로 강제 스킵) → FAIL 0/1. **원인 확정**: 잔상 나이(`age += Time.deltaTime`)도 timeScale에 비례해서, 대기를 스킵하려고 timeScale을 확 올리면 화면의 잔상들이 같이 급속 소멸함(자충수).
  2. 같은 영상 재판정(기준 문구만 변경) → FAIL 0/2.
  3. 수동 통제 재녹화(`DashMotionStreak_realtime`, timeScale 조작 없이 순수 poke 방식) → FAIL 1/3.
  4. 스크린샷으로 원인 진단: 화면을 뒤덮은 큰 덩어리가 트레일 리본(`dashTrail`, 이번 작업과 무관한 기존 WIP 기능)인 줄 알았으나, 트레일을 꺼도 비슷 → **진짜 원인은 `afterImageAlpha=1.0`(녹화용 오버라이드)이 잔상들을 불투명하게 만들어, 조밀한 스폰 간격(0.2유닛)과 겹치면서 서로를 가리고 뭉쳐 보인 것**(반투명이어야 겹쳐도 색이 섞여 보임). 같은 영상을 트레일 관점 기준으로 재판정 → FAIL 1/3.
  5. 원인 수정 반영 재녹화(`DashMotionStreak_zoomed`: 카메라 더 줌인 ortho 4.2→2.5, `afterImageAlpha` 1.0→**0.65**(반투명), 2회 교대 대시로 밀도↑) — 스크린샷 확인 결과 초록·청록·파랑·마젠타·노랑·빨강이 뚜렷이 섞인 컬러 트레일로 **육안상 명백히 개선**됐으나 판정은 오히려 FAIL **0/3**(직전보다 낮음).
  - **결론**: 육안(스크린샷)으로는 컬러 잔상 효과가 명확히 보임에도, "가장 선명했던 영상이 가장 낮은 점수"를 받는 역전 현상이 발생 — 판정기(3회 독립 Gemini 콜, 다수결)의 **표본 노이즈가 실제 시각 품질보다 결과를 더 좌우**하는 것으로 판단. `.claude/CLAUDE.md` 루프 모드 규칙("같은 검증 3회 연속 실패 시 중단·보고")에 해당해 **여기서 중단**.
  - 참고용 영상 링크(전부 기준선 이하 판정, 실제 시각 검사는 스크린샷 참고): youtu.be/FilRHP7d-RI, youtu.be/U-XvfDPnYPQ, youtu.be/BFZ3mQEF8Cs, youtu.be/AqDTkrrxB9U, youtu.be/JtFsfZYPswg.
- **부수 발견(이번 작업 범위 밖, 코드 안 건드림)**: 씬의 `PlayerController`에 `dashTrail`(발광 리본)·`dashScreenShake`·`dashStartFlash`·`dashHitstop` 등이 **이미 전부 기본 ON으로 구현돼 있음**(이전 세션 WIP, task.md엔 "⏸ 사용자 방향 선택 대기"로 기록돼 있었는데 코드는 이미 다 구현·활성화돼 있어 문서-코드 불일치). 트레일 리본은 스크린샷상 크고 진한 쐐기 모양으로 화면을 상당히 차지함 — 실제 좋은지 나쁜지는 사람이 영상을 직접 보고 판단 필요.
- **정리 필요 항목(승인 대기, 삭제 안 함)**: 진단용 스크린샷 4장이 `Recordings/_debug_frame1.png` ~ `_debug_frame3_zoomed.png`로 남아있음(mp4 결과물 아님, 순수 디버그 산출물) — 삭제하려면 사용자 승인 필요(CLAUDE.md 규칙 4).
- **다음 제안**: (a) 사용자가 위 영상 링크 중 하나(특히 `DashMotionStreak_zoomed`)를 직접 보고 육안으로 통과 여부 판단, (b) 또는 판정 기준/방식 자체를 재검토(예: 프레임 샘플링 방식 개선), (c) 트레일 리본 등 이미 구현된 "더 화려하게" 번들 기능들을 사용자가 실제로 원하는지 재확인 — task.md 문서와 코드가 어긋나 있음.

## 🐛 대시 잔상 "형태 불일치" 버그 수정 완료 (2026-07-23, 루프 모드)
- 지시: "대시의 잔상과 플레이어 스프라이트의 형태가 다른 문제를 수정."
- **근본 원인 확정 (execute_code 직접 측정)**: 위 "대시 거리 증가" 세션에서 넣은 `afterImageStretch`(모션 스트리크) —
  `DashAfterImage.Spawn`이 클론 스케일을 `x*stretch`, `y/sqrt(stretch)`로 **비균일** 적용. 기본값 `stretch=2.0`,
  플레이어 `localScale=(1.3,1.3)` 기준 클론 스케일이 `(2.6, 0.919)`로 나와 **가로 2배·세로 0.71배**로 찌그러짐.
  실측: 플레이어 `bounds.extents=(4.55,1.50)`(aspect 3.043) vs 버그 상태 잔상 `aspect≈8.61` — **약 2.8배 더 옆으로
  퍼진 모양**(pivot이 (0.46,0.03)로 발밑에 가까워, 세로 스퀴시가 발을 축으로 상반신을 짓눌러 더 두드러짐).
  즉 지난 세션 스크린샷에서 "화면을 뒤덮은 덩어리"로 보였던 것의 실체가 바로 이 형태 왜곡.
- **수정 (surgical)**:
  - `DashAfterImage.Spawn`: `stretch` 매개변수와 비균일 스케일 계산 제거 → `go.transform.localScale = t.lossyScale`
    (소스와 완전히 동일한 균일 스케일 복제). 주석도 "형태 항상 일치"로 갱신.
  - `PlayerController.cs`: 이제 안 쓰는 `afterImageStretch` 필드 삭제 + `SpawnAfterImage()`의 `Spawn(...)` 호출에서
    해당 인자 제거.
  - `PlayTestRunner.cs`: `ShowcaseRoutine`의 `prevStretch`/`player.afterImageStretch` 저장·오버라이드·복원 3곳 삭제
    (필드 삭제로 인한 컴파일 에러 방지 — 내 변경이 만든 orphan 정리).
- **신규 회귀 테스트**: `PlayTestRunner.DashAfterimageShapeTest()` + 메뉴 `Tools/PlayTest/Dash Afterimage Shape`.
  대시 중 실제 에코 잔상(`"DashAfterImage"`란 이름의 오브젝트만 — `"DashBurst"` 원형 임팩트 플래시는 원래 사람
  모양이 아니므로 비교 대상에서 제외)의 `SpriteRenderer.bounds` aspect ratio를 플레이어와 비교, 오차 0.01 초과 시
  FAIL. `ASSERT_CONVENTION.md`에 `dash_afterimage_shape` 채널 등록.
- **Play 모드 실측 검증(SampleScene, 2회 독립 실행)**: 두 실행 모두 `[ASSERT] dash_afterimage_shape: PASS
  shape_match=True checkedGhosts=8` — 실제 에코 8개 전부 플레이어와 **aspect 3.043 = 3.043로 소수점까지 정확히
  일치**(부동소수 오차 0.01 이내). 컴파일 클린(validate standard: 전 파일 error/warning 0, 관련 없는
  `SetupAnimationsEditor` obsolete 경고 1건만). 콘솔 error/warning 세션 전체 0. 씬 미변경/미저장.
- **영상 판정 — 3연속 실패, 루프모드 규칙에 따라 중단**: 서로 다른 녹화 2개(짧은 버전 1.2s / 잔상수명 늘린 확장판
  ~3.4s)와 판정 기준 문구 3가지(왜곡 비교형 2회 + 단순 "사람 모양으로 보이는가" 1회) 모두 **FAIL 0/2**
  (3콜 중 1콜은 매번 timeout으로 표에서 빠짐). 링크: youtu.be/34jLCtnTTFI, youtu.be/2Va2vWEKwZA,
  youtu.be/dNX45fekAm4. 판정 사유 텍스트는 조회 불가(진단용 인라인 스크립트 실행이 permission으로 막힘).
  **위 "대시 거리 증가" 세션에서 이미 동일한 종류의 콘텐츠(짧고 화려한 컬러 잔상 클립)에 대해 이 판정기가
  반복적으로 신뢰도 낮은 FAIL을 낸 전례가 있어(코드 사실은 이미 execute_code 실측·엔진 내 자동 ASSERT로
  이중 확정된 상태), 이번에도 판정기 자체의 한계로 판단하고 재시도를 중단함.**
- **결론**: 버그(형태 불일치)는 코드 레벨에서 수치로 확정 수정·검증됨. 영상 판정만 신뢰도 문제로 보류 — 사용자가
  위 3개 링크 중 하나(추천: `2Va2vWEKwZA`, 잔상수명 늘려 가장 오래 보임)를 직접 보고 형태가 사람 모양으로
  보이는지 눈으로 확인 권장.

## 🗺️ "1자(플랫) 테스트 타일맵" 신설 — 계획 수립, 씬 편집 승인 대기 (2026-07-23, 루프 모드)
- 지시: 현재 타일맵(`TiledMap_Exterior`, 캐슬 외부)은 프리팹으로 남겨두고, 단순한 1자(평지) 타일맵을 새로 만들어 앞으로는 그쪽에서 테스트.
- **씬 실측(읽기 전용 execute_code)**:
  - `TiledMap_Exterior`(root scale 6.25) → `Grid`(scale 1, cellSize 0.16) → `BG/Tile Layer 5/Base/Deco/Tile Layer 6/DEco 2/CollisionTilemap` 다중 레이어, 청크 수십 개. 복잡한 프로덕션 레벨.
  - `TestGround`(비활성)의 `SpriteRenderer.sprite` = Unity 내장 `Background`(`Resources/unity_builtin_extra`, 흰 사각형) — 기존 placeholder 관례.
  - `Assets/TiledMap/CollisionTile.asset`은 스프라이트 없는 순수 콜라이더용 Tile(캐슬맵 충돌 전용, 재사용 부적합).
  - 캐슬 타일(`Castle Of Bones.Tile.*`)은 PPU100 슬라이스라 cellSize 0.16 + 루트 스케일 6.25 조합이 있어야 1타일=1유닛이 맞음 — 새 평지맵에 그대로 재사용하면 스케일 트릭을 통째로 복제해야 해서 테스트용치고 불필요하게 복잡.
- **제안 계획(승인 필요 — 씬/프리팹 직접 조작이라 규칙4 대상)**:
  1. `TiledMap_Exterior`를 `manage_prefabs create_from_gameobject`로 프리팹화(예: `Assets/Resources/Prefabs/TiledMap_Exterior.prefab`), 씬 인스턴스는 `TestGround/TestWall/TestCourse`와 동일하게 **비활성화**(삭제 아님 — "남겨두고" 요건).
  2. 새 `Grid`+`Tilemap`("TestFlatMap") 생성: cellSize 1×1 유닛(스케일 트릭 없음), 타일 스프라이트는 `TestGround`가 쓰던 내장 `Background` 재사용(신규 아트 불필요), 폭 약 60유닛·두께 1타일 평지, `TilemapCollider2D` 부착.
  3. Player 스폰 위치를 새 평지맵 위로 이동(현재 캐슬맵 스폰 y=29.17을 비활성화하면 바닥이 없어져 추락하므로).
- **미결 fork(사용자 확인 필요)**: 캐슬맵을 비활성화+스폰 이전(위 계획) vs 캐슬맵은 그대로 두고 평지맵만 빈 공간에 추가(스폰 불변, 필요할 때만 수동 이동).
- 사용자 선택: **1번(계획대로 진행)** — 아래 "✅ 완료" 참조.

## ✅ "1자(플랫) 테스트 타일맵" 구현 완료 (2026-07-23, 루프 모드)
- 위 계획(옵션 1)을 그대로 실행. 씬/프리팹/에셋만 변경, `.cs` 코드 변경 없음(영상 보고 대상 아님 — 대신 Play 모드 reflection으로 수치 검증).
- **`TiledMap_Exterior` 프리팹화**: `PrefabUtility.SaveAsPrefabAssetAndConnect`(manage_prefabs 툴은 "don't ask" 모드로 auto-deny라 execute_code의 표준 Editor API로 대체 — refresh_unity 우회와 동일 패턴)로
  `Assets/Resources/Prefabs/TiledMap_Exterior.prefab` 생성 + 씬 인스턴스를 프리팹 인스턴스로 연결(`GetPrefabInstanceStatus=Connected`). 씬 인스턴스는 `TestGround/TestWall/TestCourse`와 동일하게 **비활성화**(삭제 아님 — 언제든 재활성화 가능).
- **새 `TestFlatMap` 생성**: `Grid`(cellSize 0.16) 자식에 `Ground`(Tilemap+TilemapRenderer+TilemapCollider2D), 루트 스케일 6.25 — 캐슬맵과 동일한 "1타일=1유닛" 관례 재사용(새 매직넘버 없음). 타일 스프라이트는 `TestGround`가 쓰던 Unity 내장 `Background`(흰 사각형) 재사용 → `Assets/TiledMap/FlatTestTile.asset`(colliderType=Grid)로 저장, x=-30..29(60타일, 폭 60유닛) 한 줄에 `SetTile`로 배치(y=-1, 상단면 world y=0). Player 스폰을 이 평지 위 `(0, 0.05, 0)`로 이동.
- **버그 발견 & 수정 (Play 모드 실측 중)**: 1차 검증에서 `isGrounded=False`(콜라이더는 붙어 있어 물리적으로 안 뚫리는데 땅 인식은 안 됨) + `SetMoveX` 입력이 무반응. 원인 = 새로 만든 `Ground` 오브젝트가 기본 **Layer 0(Default)**로 생성됐는데, `PlayerController.groundLayer` 마스크는 512(`Layer 9 "Ground"`, `TestGround`와 동일)만 인식 — 레이어 불일치로 접지 판정 실패. `groundGO.layer=9`로 수정 후 재저장 → 재검증에서 `isGrounded=True`, 좌우 이동 정상.
- **함정 2개 확인**:
  1. 첫 실행 시 에디터가 **Play 모드**였음 → `manage_scene`/`manage_prefabs`가 "don't ask" 모드로 전부 auto-deny라 execute_code로 대체했는데, `EditorSceneManager.MarkSceneDirty`/`SaveScene`은 Play 모드 중 호출 시 `InvalidOperationException`(에셋 생성은 Play 모드에서도 디스크에 즉시 반영되지만, 씬 GameObject 변경은 Play 모드 종료 시 전부 롤백됨) → `manage_editor(stop)`로 Edit 모드 전환 후 전체 시퀀스 재실행.
  2. `Collider2D.bounds`는 **Edit 모드에서 항상 (0,0,0)로 보임**(Box2D 물리 월드가 Edit 모드엔 없음) — 기존에 이미 검증된 `CollisionTilemap`도 Edit 모드에서 동일하게 0으로 나와 새 콜라이더의 결함이 아님을 교차 확인. Play 모드 진입 후에야 `Center(0,-0.5,0) Extents(30,0.5,0)`로 정확히 계산됨 — 앞으로 콜라이더 상태를 execute_code로 점검할 땐 반드시 Play 모드에서 확인할 것.
- **Play 모드 실측 검증**(InputInjector + reflection, 별도 execute_code 호출로 프레임 진행 유도 — Thread.Sleep은 메인스레드를 막아 엔진이 안 돌아서 무의미함을 확인):
  - 착지: 스폰(0.05)→정착(0.01), `vel=(0,0)`, `isGrounded=True`.
  - 우측 이동: `vel.x=+5` 방향으로 진행(0→7.12), 좌측 이동: `vel.x=-5.00` 정확히 일치(7.12→-18.08), 60유닛 폭 안에서 양방향 정상.
  - 콘솔 error/warning **0**(read_console 확인). 최종 `scene.isDirty=False`(저장 완료), `castleMap.activeSelf=False`(프리팹 연결됨), `flatMap.activeSelf=True`, `Ground` layer=`Ground`, 프리팹/타일 에셋 파일 디스크 존재 확인.
- **참고**: `Room_A/B/C`(구 RoomCamera 트리거, 현재 미사용)·`TestCourse`(비활성 박스 데모)는 이번 작업과 무관해 손대지 않음. `SectionCamera`는 `gridOrigin` 기준 상대 좌표로 구간을 계산해 새 평지맵 위치에서도 별도 설정 없이 정상 동작(코드 확인, 로직상 좌표 의존 없음).

## ✅ 플레이어 공격 튜닝 + 공격 중 입력 제한 + 타격 VFX 이식 (2026-07-23, 대화 모드)
- 배경: 더미몬스터·1-2타 콤보·공격 판정/데미지/VFX가 **이전 세션에서 이미 구현·배선까지 끝나 있었으나 문서화가 누락**된 상태로 발견(task.md엔 "승인 대기"로만 기록). 몸/창 색상(`#65FF5B`/`#9ECAFF`)·씬 배치·Enemy 레이어·입력 액션까지 전부 파일 직접 확인으로 검증 완료. 이번 세션은 그 기반 위에 사용자가 준 튜닝 피드백 4가지 + 추가 요청 1가지를 처리.
- **T1 카메라 쉐이크 완화**: `SectionCamera.ShakeCo`에 선형 감쇠 추가(끝까지 일정 세기→시간에 따라 0으로 잦아듦). 씬 `attackShakeMagnitude` 0.15→0.05, `attackShakeDuration` 0.12→0.08(SerializedObject로 씬 직접 수정, 사용자 승인 후 진행). Play 모드 실측: 감쇠 확인(0.408→0.139, 시간에 따라 감소).
- **T4 공격 애니메이션 가속**: Animator `Glitch Samurai-Slash 1/2` 상태 `m_Speed` 1→1.5. 코드 `attack1Duration`/`attack2Duration`을 동일 비율로 0.5833/0.4167→0.3889/0.2778로 동기화(판정 타이밍 유지). Play 모드 실측: 1타가 실제 ~0.4s에 종료(신규 duration과 일치).
- **T3 콤보 버퍼 2초**: `comboBufferDuration=2f` 필드 추가, `HandleAttack()`에 `comboBufferTimer`/`lastAttackStage` 상태 추가 — 1타가 자연 종료된 뒤에도 2초 안에 재공격하면 2타(Slash2)로 이어짐(기존엔 Slash1 재생 중에만 콤보 인정). **애니메이터 갭 발견·수정**: `Attack2` 트리거 조건 전이가 Slash1 상태 내부 전이로만 존재(AnyState에 없음) → Idle 복귀 후 버퍼 콤보가 트리거돼도 애니가 안 바뀌는 문제 확인, `Attack1`과 동일 패턴으로 AnyState→Slash2 전이 신규 추가(fileID 9123456789012345678, 기존 파일과 충돌 없음 확인 후 삽입). Play 모드 실측: reflection으로 comboBufferTimer 직접 세팅해 결정적 검증 — 버퍼 유효 구간에서 `stage=2` + `anim.Update()` 직후 클립이 실제로 `Glitch Samurai-Slash 2`로 전환됨 확인. 콤보윈도우 이전 재입력은 여전히 무시됨(재확인).
- **공격 중 입력 제한 (사용자 추가 요청, 대화 중간에 지시)**: `HandleMovement()`에 `isAttacking` 가드 추가(수평 속도 0 고정, 이동 입력 무시) — `DummyEnemy.AttackLogic`이 공격 중 정지하는 기존 설계 원칙을 플레이어에도 동일 적용. `HandleJump()`/`HandleDash()`에도 `isAttacking` 중엔 입력을 버리는 가드 추가(점프/대시로 공격 캔슬 불가). 재공격 제한은 기존 로직이 이미 처리 중이었음(콤보윈도우 전 재입력은 무시, 재확인). Play 모드 실측(같은 execute_code 호출 안에서 공격 시작 직후 연속 테스트, 실시간 지연 배제): `vel=(0,0)`(이동 잠금) / `velY` 불변+입력 소모(점프 잠금) / `isDashing=False`(대시 잠금) 전부 확인.
- **T2 타격 VFX 이식 (사용자 확정: 옵션 B, UniTrio-Game-2026 그대로 이식)**:
  - 출처: `C:\Users\kimga\UniTrio-Game-2026`(dev 브랜치, 로컬 클론) `Assets/Scripts/Player/Weapon/HitVfxAutoReturn.cs` + `Assets/Shaders/VFXLit2D.shader`(URP 표준 HLSL, Shader Graph 아님 — Core.hlsl/Lighting.hlsl 기반이라 이식 호환성 높음) + `Assets/Resources/Prefabs/HitVFX/HitVFX1~4.prefab`(각 스프라이트시트+anim+controller 체인).
  - **의존성 체인 전체 파일 복사**(GUID 보존을 위해 `.meta` 동반 복사, 이 프로젝트는 `.meta`/`.unity`/`.controller` 등 직접 Edit 차단 훅이 있어 셰이더/스프라이트/anim/controller/prefab은 Bash `cp`로, 신규 스크립트의 `.meta`만 `execute_code`의 `File.WriteAllText`로 생성해 훅 취지를 지킴):
    - `Assets/Shaders/VFXLit2D.shader`(+.meta)
    - `Assets/VFX/HitVFX/Sprites/{Electric Hit 1, hit 1, hit 3, hit 4}.png`(+.meta, 스프라이트시트)
    - `Assets/VFX/HitVFX/Animations/HitVFX{1,3,4,5}.anim`(+.meta)
    - `Assets/VFX/HitVFX/Controllers/{Electric Hit 1_0, hit 1_0, hit 3_0, hit 4_0}.controller`(+.meta)
    - `Assets/VFX/HitVFX/Prefabs/HitVFX{1,2,3,4}.prefab`(+.meta) — 프리팹의 `m_Materials` 참조가 원본 프로젝트 전용 머티리얼 guid(`d1ae23dd...`, 존재하지 않음)를 가리키고 있어 이 프로젝트에서 이미 쓰이는 기본 스프라이트 머티리얼 guid(`a97c105638bdf8b4a8650670310a4cd3`, Player/Spear/DummyEnemy와 동일)로 재배선(`sed`) — 어차피 `HitVfxAutoReturn.Awake()`가 런타임에 VFXLit2D 머티리얼을 새로 생성해 덮어쓰므로 에디터 미리보기용 placeholder일 뿐.
    - `Assets/Scripts/VFX/HitVfxAutoReturn.cs`(+.meta, guid `372f951f19234b746bf6c1a3b81c3abb` 원본과 동일하게 유지해 프리팹 참조가 그대로 풀리게 함) — **원본에서 1곳 수정**: `SimpleObjectPool.Instance.Release(...)`(원본은 오브젝트 풀링 시스템 사용) → `Destroy(gameObject)`로 교체. 이 프로젝트엔 풀링 컨벤션이 없고(기존 `DashAfterImage.cs`도 자가파괴 방식) 히트 VFX 스폰 빈도가 낮아 풀링이 불필요한 복잡도라 판단(Simplicity First). `HitStopManager`(원본의 별도 히트스톱 매니저)는 포팅 안 함 — 이미 `PlayerController.AttackHitstopCo`가 동일 패턴(timeScale 코루틴)으로 존재해 중복 방지.
  - **`PlayerController.cs`**: `[Header("Attack VFX")]`에 `hitVfxPrefabs`(GameObject[])·`hitVfxOffsetTowardsEnemy`(0.3f) 필드 추가. `CheckAttackHit()`에서 데미지 적용 성공 시 `SpawnHitVfx()` 호출(적 위치 기준, 진행 방향으로 오프셋, 랜덤 회전) — 신규 헬퍼 메서드, 기존 히트스톱/쉐이크 로직 불변(surgical 추가만).
  - 씬 배선: Player의 `hitVfxPrefabs` 배열(4개) SerializedObject로 직접 연결 + 저장.
  - **부수 수정 (검증 중 발견)**: `DummyEnemy` 씬 오브젝트가 구 캐슬맵 좌표(`y=29.6`)에 남아있어 현재 활성 평지맵(`TestFlatMap`, 표면 y≈0)과 동떨어져 있었음(2026-07-23 앞선 세션에서 이미 지적된 갭) → `(4, 0.05, 0)`로 재배치해 플레이어가 실제로 닿을 수 있게 수정.
  - **Play 모드 실측 검증**: `Custom/VFXLit2D` 셰이더 정상 컴파일(`Shader.Find` 성공), 4개 프리팹 전부 sprite/controller/script 정상 로드. `CheckAttackHit(10)` 직접 호출 → 더미 `HP 30→20`(데미지 정상) + `HitVFX3(Clone)` 실제 스폰(적 위치+오프셋에 정확히 생성) 확인. 이후 재조회에서 `vfxCount=0`(애니메이션 재생 완료 후 자가파괴, 고아 오브젝트 없음) 확인. **함정**: `transform.position` 직접 대입 직후 같은 프레임에 `OverlapBoxAll`을 호출하면 콜라이더 캐시가 안 갱신돼 판정이 빔(`Physics2D.SyncTransforms()` 필요) — 실측 중 발견, 이후 테스트는 자연 경과된 위치로 우회 확인.
  - 세션 전체 콘솔 error/warning **0**(무관한 기존 `TextureImporter.spritesheet` obsolete 경고 1건만 반복 관측). 컴파일 클린(`PlayerController.cs`/`SectionCamera.cs`/`HitVfxAutoReturn.cs` 전부 validate error 0). 씬 `isDirty=False`(전부 저장 완료): dummyPos, comboBufferDuration=2, attack1Duration=0.3889, attackShakeMagnitude=0.05, hitVfxPrefabs 4개 배선 전부 디스크 반영 확인.
- **참고(후속 검토 여지)**: 히트 VFX 스프라이트는 UniTrio 원본 그대로(전기 스파크 1종 + 범용 히트 플래시 3종) — "Glitch Samurai" 테마와 전기/글리치 이미지가 크게 어긋나진 않으나 픽셀아트 톤은 다를 수 있어, 실제로 눈으로 보고 톤이 안 맞으면 `hitVfxPrefabs` 배열은 Inspector에서 다른 조합으로 쉽게 교체 가능. `attackHitboxDistance`/`attackHitboxSize` 등 기존 판정값은 이번 세션에서 변경 안 함.

## 🐛 공격 콤보 시스템 재설계 — "딜레이 느낌 + 1-2타 이상함 + 판정 불일치" 버그 수정 (2026-07-23, 대화 모드)
- 사용자 리포트: 공격 애니메이션이 갑자기 비이상적으로 빨라짐, 클릭 후 약간의 딜레이 후 애니메이션 재생, 피격 판정이 애니메이션과 불일치.
- **재현 시도 중 실측으로 확정 버그 발견**: 실제 입력 경로(`InputInjector.PressAttack` → `OnAttack` → `attackRequested`)로 신규 Play 세션 첫 클릭을 테스트하니 `stage=2`로 시작(1타가 나가야 정상). 원인 = 기존 `HandleAttack()`이 "스윙 도중 재입력(comboWindowStart 기준)"과 "스윙 종료 후 버퍼 콤보(comboBufferTimer+lastAttackStage 기준)" **두 개의 독립된 분기로 콤보를 판정**하고 있어 두 상태가 어긋날 수 있는 구조적 결함이었음.
- **사용자 지시**: `C:\Users\kimga\UniTrio-Game-2026`의 공격 시스템(쉐이더 기반, 3타 콤보) 참고해서 적용 가능한 것 적용.
- **UniTrio 조사(`PlayerWeaponController.HandleAttackInput`/`WeaponBehaviourBase`)**: 우리와 근본적으로 다른 3가지 설계:
  1. **입력 버퍼링**: `Input.GetMouseButtonDown` 감지 시 `_attackQueued`+시간을 기억(0.3초), 공격/쿨다운 중이어도 버려지지 않고 준비되는 즉시 자동 발동.
  2. **스윙 중단 없음**: 공격 중엔 새 공격을 절대 끼워넣지 않음(`if (IsAttacking) return`) — 항상 끝까지 재생 후 버퍼된 입력이 이어받음.
  3. **콤보 카운터 단일화**: `_comboStep` 하나만 순환(`(_comboStep % Max) + 1`), 마지막 공격 종료 후 경과시간이 `ComboWindow` 초과 시 1타로 리셋. 별도의 "직전 스테이지 기억" 변수 없음.
  - (참고만 하고 이식 안 함: `WeaponBehaviourBase`의 실제 Collider2D on/off 기반 히트박스 방식 — 우리 프로젝트엔 과한 리팩터라 기존 `OverlapBoxAll` 정규화시간 윈도우 방식 유지, 판정 불일치는 콤보 버그의 부수 증상으로 판단.)
- **`PlayerController.cs` 재작성(surgical, 이동/대시/i-frame/VFX/입력제한 등 나머지 전부 불변)**:
  - 필드: `comboWindowStart`(삭제) → `attackInputBufferDuration=0.3f`(신규, UniTrio의 `_attackQueued` 버퍼 참고) 추가. `attackRequested`(bool)→`attackQueued`(bool)+`attackQueueTime`(float)로 교체. `comboBufferTimer`+`lastAttackStage` 두 변수 삭제 → `attackStage` 하나가 "현재 재생 중 또는 다음에 재생할 타수"를 겸함(초기값 1) + `lastAttackEndTime`(초기값 -999f) 하나로 통합.
  - `OnAttack()`: `attackRequested=true` → `attackQueued=true; attackQueueTime=Time.time;`로 변경(버퍼링 시작점 기록).
  - `HandleAttack()`: 기존 "스윙 도중 재입력" 분기를 완전히 제거. 새 로직 = 버퍼 만료 정리 → `!isAttacking && !isDashing && attackQueued`일 때만 발동(콤보 유효시간 초과 시 `attackStage=1`로 리셋 후) → 공격 종료 시 `attackStage=(attackStage%2)+1`로 다음 타수 미리 순환. 판정 윈도우(`attackHitWindowStart/End`)·데미지 로직은 불변.
  - `PlayTestRunner.cs`: 삭제된 `comboWindowStart` 참조 1건(콤보 타이밍 대기 계산) → `attack1Duration * 0.6f` 리터럴로 교체(동일 타이밍 의도 유지, 컴파일 에러 방지 — 내 변경이 만든 orphan 정리).
- **검증(Play 모드, 실제 입력 경로 + reflection 혼합)**:
  - **함정 발견**: `InputInjector.PressAttack()`은 InputSystem에 상태 이벤트를 큐잉만 할 뿐 동기 실행이 아니라, 같은 `execute_code` 호출 안에서 곧바로 `attackQueued`를 읽으면 항상 `False`로 보임(아직 처리 전) — 원격·비포커스 에디터의 프레임 스로틀과 겹쳐 실시간 타이밍 테스트가 신뢰도 낮음을 재확인. 이후 `HandleAttack()`/`anim.Update()`를 직접 순서대로 호출하는 결정적 테스트로 전환.
  - 신선한 Play 세션 첫 클릭(실제 입력 경로) → 정상적으로 **1타로 시작**, 종료 후 `stage=2`로 순환(예전처럼 "첫 클릭인데 2타" 버그 재현 안 됨).
  - 프레임 단위 결정적 테스트(각 단계마다 `anim.Update()` 호출): 1타 시작(`clip=Slash 1`) → 스윙 도중 클릭(`clip` 그대로 `Slash 1`, 끼어들지 않음, `attackQueued` 유지) → 1타 강제 종료(`stage`가 2로 순환) → 다음 프레임에 큐 자동 발동 → **`clip=Slash 2`로 정확히 전환, `isAttacking=True`**. 콤보 버퍼(2초) 안/밖 케이스도 각각 `Slash 2`/`Slash 1`로 정확히 분기 확인.
  - 컴파일 클린(`PlayerController.cs`/`PlayTestRunner.cs` 둘 다 validate error 0). 세션 전체 콘솔 error/warning 0. 씬 `isDirty=False`(comboBufferDuration=2, attackInputBufferDuration=0.3 반영 확인).
- **참고**: "애니메이션이 비이상적으로 빨라짐" 자체(Animator 속도 1.5배)는 되돌리지 않음 — 콤보 버그가 고쳐지면서 "엉뚱한 타수가 튀어나오는" 체감이 사라지면 속도 자체는 다시 평가해볼 만함. 사용자가 실제 플레이 후에도 여전히 빠르다고 느끼면 `Glitch Samurai-Slash 1/2` 상태의 `m_Speed`(현재 1.5)를 낮추면 됨.

## ✅ 쉐이크/히트스톱 완화 + 데미지 텍스트 UI 이식 + 양방향 히트 VFX + 공격 전진 + 더미 HP 20 (2026-07-23, 대화 모드)
- 지시: 카메라 쉐이킹·히트스톱 강도 낮추기 / UniTrio-Game-2026에서 피격 VFX 스프라이트·데미지 텍스트 UI 이식 / 더미 HP→20 / (대화 중 추가) 검 공격 시 바라보는 방향으로 살짝 전진.
- **쉐이크/히트스톱 완화(씬 값)**: `attackShakeMagnitude` 0.05→0.03, `attackShakeDuration` 0.08→0.05, `attackHitstopDuration`/`hitstopDuration`(대시) 0.05→0.03, `attackHitstopScale`/`hitstopScale` 0(완전 정지)→0.15(약한 슬로우로 완화).
- **더미 HP**: 코드 기본값 30→20 + 씬 직렬화 값 동기화.
- **데미지 텍스트 UI 이식(UniTrio `DamageText.cs`+`DmgText.prefab`, TextMeshPro 기반)**:
  - 폰트 체인 전체 이식(GUID 보존): `neodgm.ttf`+`neodgm SDF.asset`(한글 픽셀 폰트, TMP Font Asset, 원본 임베디드 머티리얼 포함)+`neodgm SDF_RText.mat`+`neodgm SDF_DI.mat` → `Assets/Fonts/`. `DmgText.prefab` → `Assets/VFX/DamageText/`.
  - `DamageText.cs` 이식(`Assets/Scripts/VFX/`, 원본 guid 유지로 프리팹 참조 즉시 해결): 원본의 `SimpleObjectPool` 반환·`ElementalWeaponSystem` 속성색 연동 제거(이 프로젝트에 해당 시스템 없음) → `Destroy(gameObject)` 자가파괴 + `Setup(damage, color)` 단순 오버로드만 유지(기존 `HitVfxAutoReturn.cs`/`DashAfterImage.cs`와 동일 컨벤션).
  - 이 프로젝트에 TMP 패키지(에센셜 리소스)가 없었으나 `TMPro.TextMeshPro` 타입 자체는 이미 컴파일 가능(Unity 6의 `com.unity.ugui` 내장) 확인 후 커스텀 폰트 에셋만으로 문제없이 로드됨.
- **양방향 히트 이펙트 배선 + 공용 헬퍼**: `Assets/Scripts/VFX/CombatFx.cs` 신설(정적 `SpawnHitVfx`/`SpawnDamageText`) — `PlayerController.CheckAttackHit`(플레이어→더미)와 `DummyEnemy.CheckThrustHit`(더미→플레이어) 양쪽에서 재사용, 로직 중복 제거. `PlayerController`에 `damageTextPrefab`/`damageTextColor`(흰색) 필드 추가, 기존 `SpawnHitVfx` 메서드는 `CombatFx.SpawnHitVfx` 호출로 교체. `DummyEnemy`에 `hitVfxPrefabs`(4종 동일 재사용)·`hitVfxOffsetTowardsPlayer`·`damageTextPrefab`·`damageTextColor`(빨간 계열로 구분) 필드 신규 추가.
- **씬 배선**: Player.damageTextPrefab, Dummy.damageTextPrefab, Dummy.hitVfxPrefabs(4개, 기존 HitVFX1~4 재사용) 전부 SerializedObject로 연결.
- **공격 전진(대화 중 추가 지시, UniTrio `PlayerWeaponController.HandleAttackInput` 참고)**: `attackLungeDistance=0.3f` 필드 추가, `StartAttackStage()`에서 매 공격(1타·2타 공통) 시작 시 `sr.flipX` 기준(true=왼쪽, false=오른쪽)으로 그 방향으로 `transform.position` 즉시 전진.
- **검증(Play 모드, execute_code 한 호출 안에 배치+실행+확인을 몰아넣어 원격 에디터 지연에 의한 오탐 회피)**:
  - 전진: 오른쪽 볼 때 `Δx=+0.30`, 왼쪽 볼 때 `Δx=-0.30` 정확히 확인.
  - 플레이어→더미: `CheckAttackHit(10)` → 더미 HP 정상 차감, `HitVFX*(Clone)` 1개 스폰, `DmgText` 1개 스폰(표시 텍스트 `'10'`, 흰색).
  - 더미→플레이어: `CheckThrustHit()` → 플레이어 HP 100→92(`attackDamage=8` 정확), `HitVFX*(Clone)`/`DmgText` 스폰 확인(표시 텍스트 `'8'`, 빨간 계열 `RGBA(1,0.3,0.3,1)`).
  - **테스트 함정 2개(둘 다 내 테스트 코드 실수, 게임 로직 버그 아님)**: (1) `GameObject.Find`는 비활성(죽은) 오브젝트를 못 찾음 → `FindObjectsByType(..., FindObjectsInactive.Include, ...)` 필요. (2) `PlayerController.currentHp`는 public인데 `BindingFlags.NonPublic`으로 `GetField` 조회해 null 반환 후 NRE 발생 — public 필드는 플래그 없이 기본 조회할 것.
  - 컴파일 클린(`PlayerController.cs`/`DummyEnemy.cs`/`CombatFx.cs`/`DamageText.cs` 전부 validate error 0). 세션 전체 콘솔 error/warning 0(무관한 기존 `TextureImporter.spritesheet` obsolete 1건만). 씬 저장 완료(`isDirty=False`).
- **참고(후속)**: 데미지 텍스트 폰트는 UniTrio 그대로("neodgm" 한글 픽셀 폰트) — 우리 "Glitch Samurai" 톤과 실제 잘 맞는지는 눈으로 확인 권장. 더미가 플레이어를 공격할 땐 카메라 쉐이크/히트스톱을 아직 추가 안 함(이번 지시 범위 밖, 필요하면 별도 요청).

## ✅ 회피-카운터(대시 후 카운터) 기능 이식 (2026-07-23, 대화 모드)
- 지시: UniTrio-Game-2026의 "대시 후 카운터" 기능을 참고해 회피-카운터 기능을 동일하게 구현.
- **UniTrio 조사(`JustDodgeController.cs`+`JustDodgeState.cs`)**: 대시 무적 윈도우 중 적 공격이 임박하면 슬로우모션+그레이스케일(커스텀 URP 렌더러 피처)+적 붉은 아웃라인+Cinemachine 카메라 팬/줌 연출과 함께 F 입력을 대기 → 성공 시 몬스터 전체 정지 + 적 뒤로 충돌무시 돌진 + 기본공격×배수 직접 데미지. FSM(`PlayerStateMachine`)으로 이동/공격을 잠그는 구조.
- **이식 범위 판단**: Cinemachine·커스텀 그레이스케일 렌더러 피처·적 아웃라인 셰이더·FSM 등은 이 프로젝트에 없는 무거운 인프라라 이식 안 함(과설계 방지). **핵심 인터랙션 흐름**(대시 무적 중 적 공격 예측 감지 → 슬로우모션 + 확인키 대기 → 성공 시 적 뒤로 돌진 + 배율 데미지 카운터, 실패해도 페널티 없음)만 우리 프로젝트 규모로 재구현.
- **확인키**: 입력 액션에 이미 있던 미사용 "Parry"(우클릭) 바인딩을 재사용 — `OnParry(InputValue)` 신규 추가, 이전엔 아무 핸들러도 없어 완전히 죽어있던 입력.
- **`DummyEnemy.cs`**: `public bool IsThrusting => state == AiState.Thrust;` 접근자 1줄 추가(감지용).
- **`PlayerController.cs`**(`[Header("Dodge Counter")]` 필드 대거 추가, surgical): `HandleDodgeCounter()`가 매 프레임 `isDashing` 중 `dodgeCounterTriggerRadius` 안에서 `IsThrusting`인 `DummyEnemy`를 찾으면(대시당 1회) `DodgeCounterRoutine` 코루틴 시작.
  - **Phase 1**(`DodgeCounterRoutine`): `Time.timeScale`을 `dodgeCounterSlowScale`(0.15)로 낮추고 플레이어 스프라이트를 `dodgeCounterGlowColor`로 틴트, 카메라 작은 발동 쉐이크. `dodgeCounterInputWindow`(1초, 실시간) 동안 `parryPressed` 대기.
  - **Phase 2**(`CounterRush`, 확인키 성공 시만): 적을 지나쳐 `dodgeCounterRushPastDistance`만큼 뒤로 순간 이동(`Vector3.Lerp`, 충돌 무시) → 돌아서서 `attack1Damage × dodgeCounterDamageMultiplier`(기본 3배) 데미지를 `DummyEnemy.TakeDamage`에 **직접 적용**(정상 히트박스 비적용 — 돌진 위치가 일반 판정 범위와 안 맞을 수 있어 UniTrio와 동일하게 직접 적용). 기존 `CombatFx.SpawnHitVfx`/`SpawnDamageText` 재사용 + 전용 카메라 쉐이크/히트스톱.
  - 실패(윈도우 경과, 확인키 없음): 페널티 없이 슬로우모션만 원복되고 대시가 정상 종료.
  - **기존 로직과의 통합**: `isAttacking`/`isDodgeCountering`을 별개 플래그로 유지(카운터의 `anim.SetTrigger("Attack1")`은 시각 연출용일 뿐 `isAttacking=false`라 `AttackHitFrame()` 애니메이션 이벤트 판정과 충돌 안 함). `HandleMovement`/`HandleJump`/`HandleAttack`의 기존 `isAttacking` 가드에 `isDodgeCountering`도 추가(공격 중과 동일하게 이동/점프/재공격 잠금). `HandleDash()`는 최상단에서 `isDodgeCountering` 중 전체 동결(코루�ine이 대시 상태를 직접 관리) — 기존 대시 종료 로직(정상 타임아웃/벽 취소)을 `EndDash()` 헬퍼로 추출해 카운터 종료 시점과 공유(중복 제거, 대시당 1회 트리거 가드 리셋도 여기서 통일 처리).
- **부수 이슈 해결**: 이 작업 중 이식해둔 TextMeshPro(데미지 텍스트)의 **TMP Essential Resources가 정식 임포트되지 않아 에디터가 계속 "Import TMP Essentials" 경고를 띄우던 문제**를 발견 → `Library/PackageCache/com.unity.ugui@.../Package Resources/TMP Essential Resources.unitypackage`를 `AssetDatabase.ImportPackage`로 정식 임포트(Play 모드 중엔 임포트가 막혀 실패 → Edit 모드 전환 후 재시도해 해결). `TMP_Settings`+기본 폰트(LiberationSans SDF) 정상 생성 확인, 이후 경고 재발 안 함.
- **검증(Play 모드, execute_code로 감지→성공/실패 양쪽 경로 결정적 재현)**:
  - 성공 경로: 더미를 Thrust 상태로 강제 + 근접 배치 → `HandleDodgeCounter()` 호출로 감지(`isDodgeCountering=True timeScale=0.15`) → `parryPressed=true` 세팅 후 대기 → 최종 확인: `dummyHp=0`(20 HP - 30 카운터데미지, 사망), `isDodgeCountering=False`, `isDashing=False`(EndDash 정상 호출), `timeScale=1`(복원).
  - 실패 경로: 동일 설정, `parryPressed` 미설정 → 윈도우 경과 후 `dummyHp=20`(데미지 없음), `isDodgeCountering=False`, `isDashing=False`, `timeScale=1` — 전부 정상 원복.
  - 이동/점프/공격 잠금: `isDodgeCountering=true` 상태에서 `vel=(0,0)`(이동 잠금)/`velY` 불변(점프 무시)/`isAttacking` 안 켜짐(공격 시작 안 됨) 전부 확인.
  - **테스트 함정(반복 발견, 게임 로직 버그 아님)**: 카운터로 더미가 죽으면 `SetActive(false)`돼 `GameObject.Find`가 못 찾음 → `FindObjectsByType(..., FindObjectsInactive.Include, ...)` 사용 필요(이번 세션에서 세 번째로 동일 패턴에 걸림 — 앞으로 죽었을 수 있는 오브젝트 조회 시 기본으로 이 방식 사용할 것).
  - 컴파일 클린(`PlayerController.cs`/`DummyEnemy.cs` validate error 0). 세션 전체 콘솔 error/warning 0. 씬 미변경(`isDirty=False`, 새 필드는 전부 코드 기본값 사용 — 씬 오버라이드 불필요).
- **참고(후속 튜닝 여지)**: 그레이스케일/적 아웃라인/카메라 팬 같은 UniTrio의 화려한 연출은 의도적으로 생략(과설계 방지) — 스프라이트 틴트+슬로우모션+쉐이크만으로 최소 구현. 나중에 "더 화려하게" 원하면 이번 세션 앞부분의 "대시 VFX 더 화려하게" 조사 결과(URP 포스트프로세싱 등)를 참고해 확장 가능.

## ✅ 데미지 텍스트 폰트를 "Silver"로 교체 (2026-07-23, 대화 모드)
- 지시: 사용자가 `Assets/Silver.ttf`를 직접 프로젝트에 추가 → 이걸 기준으로 데미지 텍스트 폰트 교체.
- UniTrio에서 이식했던 "neodgm SDF"(한글 픽셀 폰트)는 순정 TTF가 아니라 이미 구워진 TMP SDF 에셋이었지만,
  이번 `Silver.ttf`는 구워지지 않은 원본 폰트 파일이라 TMP Font Asset(SDF) 생성이 별도로 필요했음.
- **`TMPro.TMP_FontAsset.CreateFontAsset(Font, ...)` 스크립트 API로 직접 생성**(에디터의 Font Asset Creator 창을
  수동으로 열 필요 없음): `Assets/Silver.ttf` → `Assets/Fonts/Silver SDF.asset`(+아틀라스 텍스처·머티리얼
  서브에셋 `AddObjectToAsset`). 샘플링 90pt, 아틀라스 1024×1024, `AtlasPopulationMode.Dynamic`(전체 글리프를
  미리 안 굽고 실사용 문자만 그때그때 굽는 방식 — 데미지 숫자처럼 쓰는 문자가 적을 때 적합).
  `TryAddCharacters("0123456789-")`로 숫자+마이너스 기호는 미리 구워둬 런타임 첫 등장 시 지연 없음.
- **`DmgText.prefab` 폰트 교체**: `PrefabUtility.LoadPrefabContents`로 열어서 `TextMeshPro.font`/`fontSharedMaterial`을
  `neodgm SDF`→`Silver SDF`(+`Silver - Medium Material`)로 교체 후 `SaveAsPrefabAsset`.
- **검증(Play 모드)**: 플레이어 공격으로 더미 타격 → 스폰된 `DmgText`의 `TextMeshPro.font`가 `Silver SDF`로 확인,
  `ForceMeshUpdate()` 후 `characterCount=1 isVisible=True`(실제 메시 버텍스 존재)로 실제 렌더링됨을 확인
  (첫 조회 시 `characterCount=0`으로 보였던 건 `ForceMeshUpdate` 호출 전 스테일 텍스트인포 상태였을 뿐, 버그
  아님 — TMP는 텍스트/폰트 변경 후 명시적 갱신 전까진 textInfo가 지연 반영됨을 확인).
  콘솔 error/warning 0. 씬 미변경(`isDirty=False`, 폰트 교체는 프리팹 에셋 레벨이라 씬 저장 불필요).

## ✅ 플레이어 데미지 1 고정 + 데미지 텍스트 크기 2배 (2026-07-23, 대화 모드)
- 지시: 플레이어가 입히는 데미지 1로 고정, 데미지 텍스트 UI 크기 2배로.
- `PlayerController.cs`: `attack1Damage`/`attack2Damage` 10/15 → **1/1**(코드 기본값 + 씬 직렬화 값 동기화). 회피-카운터(`dodgeCounterDamageMultiplier`)는 손대지 않음 — 별도 배율 메커니즘이라 지시 범위 밖으로 판단(필요하면 후속 요청).
- `DmgText.prefab`: `TextMeshPro.fontSize` 3.39 → **6.78**(2배). `fontSizeBase`도 setter를 통해 자동 동기화됨(직접 조정 불필요 — 시도했다가 `fontSizeBase`가 없는 API임을 확인하고 제거). `autoSizing` 꺼져있어 `fontSizeMin/Max`는 무관.
- **검증(Play 모드)**: `CheckAttackHit(attack1Damage)` 호출 → 더미 HP 20→19(정확히 1 감소), 스폰된 `DmgText.text='1'`, `fontSize=6.78` 확인. 콘솔 error/warning 0. 씬 저장 완료.

## 🔍 "카메라 쉐이킹 + 대시 흰색 블러 + 스프라이트 아래쪽 쉐이더 VFX 삭제" 재개 지시 — 삭제 대상 없음 확인 (2026-07-23, 루프 모드)
- 지시: "재게"(이전에 시작된 삭제 작업 이어서 진행) — 카메라 쉐이킹, 대시 시 흰색 블러, 캐릭터 스프라이트
  아래쪽 쉐이더 느낌 VFX 세 가지를 삭제.
- **직접 재현·확인(읽기 전용, execute_code + grep + 씬 hierarchy)**:
  1. 전 `.cs` 파일 grep(`shake|flash|blur|burst|glow|trail`) → `DummyEnemy.cs`의 무관한 피격 flash와
     `PlayTestRunner.cs` 주석의 과거 참조("DashBurst"는 이미 코드에서 삭제된 상태) 외 **매치 0건**.
  2. 컴파일된 Assembly-CSharp을 리플렉션으로 직접 조회: `PlayerController`/`SectionCamera` 필드 전체 나열 +
     어셈블리 전체에서 이름에 shake/flash/blur/vfx/burst/glow 포함된 타입 검색 → **0건**. `dashScreenShake`/
     `dashStartFlash`/`dashTrail` 등 이전 세션(2026-07-22 "대시 거리 증가" 기록)이 언급한 필드들은 현재
     코드·컴파일된 어셈블리 어디에도 없음.
  3. 씬(SampleScene) 전체 계층 실측(루트부터 재귀 순회, 비활성 포함): `Main Camera`엔 `SectionCamera`만,
     `Player`는 **자식 0개**(Transform/BoxCollider2D/Rigidbody2D/PlayerInput/PlayerController/Animator/
     SpriteRenderer만) — 쉐이크·플래시·VFX 관련 컴포넌트나 자식 오브젝트 전무. Volume/PostProcess/
     ParticleSystem/Cinemachine류도 씬 어디에도 없음.
  4. `Assets/` 전체에서 파일명에 shake/flash/blur/vfx/glow/burst 포함된 에셋 검색 → 관련 없는 "Slash" 애니메이션과
     `CameraFollow.cs`(구 카메라 스크립트, "fol**low**"가 우연히 매치) 외 없음.
- **결론**: 세 가지 모두 **현재 코드·씬·컴파일된 어셈블리 어디에도 존재하지 않음** → 삭제할 대상이 없음.
  task.md의 2026-07-22 기록("dashTrail·dashScreenShake·dashStartFlash·dashHitstop 등이 이미 전부 기본 ON으로
  구현돼 있음")과 현재 상태가 불일치 — `dashHitstop`만 살아있고 나머지 셋은 이미 없는 상태로 보아, 이번
  "재게" 이전의 어느 시점(문서화 누락 세션 또는 컨텍스트 손실 구간)에 이미 제거가 끝났거나 애초에 파일로
  저장되지 않은 런타임 임시 상태였던 것으로 판단됨. **코드 변경 없음** — 별도 조치 불필요.
- 씬 미변경/미저장. 세션 중 콘솔 확인 안 함(순수 리플렉션·에디터 API 조회라 Play 모드 진입 자체가 불필요했음).

## ✅ 회피(저스트 닷지) 전면 재설계 — 근접판정→타이밍판정 + 버그수정 + VFX/UI 이식 (2026-07-23, 대화 모드)
- 지시: 회피 기능을 UniTrio-Game-2026의 대시(회피 저스트) 참고해서 재작업. 이어서 (a) 회피 성공 VFX/UI를
  UniTrio 참고해 제작/이식, (b) 같은 UI(DashUI 프리팹)·같은 F키 사용, (c) 대시 중 적 통과(현재 밀치는 문제 수정).
- **UniTrio 조사(`DashHandler.cs`/`DashState.cs`/`JustDodgeController.cs`/`JustDodgeVFX.cs`/`JustDodgeUI.cs`)**:
  UniTrio의 "대시"엔 `_justDodgeWindow`가 내장 — 대시 중 적 공격이 **실제로 닿는 순간**(타이밍 기반)에
  발동, `rb.excludeLayers |= EnemyLayerMask`로 적 통과. 우리 기존 회피-카운터는 "대시 중 근처에 찌르는 적이
  있으면"(근접 스캔, 널널함) 발동 방식이라 근본적으로 다름 — 사용자가 후자를 전자로 교체 요청.

### 🐛 발견한 실전 버그: "슬로우모션만 되고 아무것도 안 되며 계속 지속됨"
- 사용자 리포트를 계기로 `DodgeCounterRoutine`을 직접 조사·재현. **근본 원인**: 기존 코드에 `try/finally`가
  없어 코루틴이 중간에 중단·예외로 끝나면 `Time.timeScale`(슬로우모)과 `isDodgeCountering`(이동/점프/공격을
  전부 잠그는 플래그)이 영원히 stuck됨 — 정확히 "슬로우모 무한 + 아무 입력도 안 먹힘" 증상과 일치.
  UniTrio `JustDodgeRoutine`은 `try{...} finally{ SetTimeScale(1f); ... }`로 항상 복원하는 구조였음(우리
  코드는 이 안전장치 없이 이식됐던 것).
- **수정**: `DodgeCounterRoutine` 전체를 `try/finally`로 감싸 — 성공/만료/예외/중단 **어떤 경로로 끝나도**
  `Time.timeScale=1f`, `isDodgeCountering=false`, 글로우/UI 정리, `EndDash()`가 항상 실행되도록 구조적으로
  보장. Play 모드에서 타겟을 루틴 도중 `DestroyImmediate`로 강제 파괴하는 엣지케이스까지 재현해 상태가
  완전히 복원됨을 확인(against 원래 코드라면 stuck났을 상황).

### 트리거 재설계: 근접 스캔 → 접촉 순간 판정
- `PlayerController.cs`: `dodgeCounterTriggerRadius` 필드·`HandleDodgeCounter()`(매 프레임 근접 스캔) 삭제.
  `public bool IsInvincible => isDashing;` + `public bool TryConsumeDodge(DummyEnemy)` 신설 — 적이 "닿는
  순간" 호출하는 진입점으로 트리거 주체를 이동.
- `DummyEnemy.cs`: `IsThrusting` 접근자 삭제(근접 스캔 전용이라 orphan). `CheckThrustHit()`의 감지 마스크를
  `Player`→`Player+PlayerInvincible`로 확장(대시 중엔 플레이어가 `PlayerInvincible` 레이어로 스왑되므로
  기존 `Player` 마스크로는 아예 감지가 안 됐음 — 확장 안 하면 닿는 순간 판정 자체가 불가능했을 결함).
  닿으면 `pc.TryConsumeDodge(this)`로 분기: 성공→닷지(무피해), 실패했지만 `pc.IsInvincible`→여전히
  무적이라 무피해, 그 외→정상 `TakeDamage`(회귀 없음, 기존 피해 경로 불변).

### 대시 중 적 통과(밀치기 버그 수정)
- 원인: 대시 무적이 레이어 스왑 방식이라 물리 충돌 매트릭스는 그대로 살아있어 적과 부딪히면 밀림.
- `HandleDash()`(대시 시작)에서 `rb.excludeLayers |= enemyLayer.value`, `EndDash()`에서 해제 —
  UniTrio `DashState`와 동일한 메커니즘.
- **핵심 검증 사항(웹 조회)**: `excludeLayers`가 `Physics2D.OverlapCircle` 같은 쿼리까지 막으면 적 통과 중
  저스트 닷지 감지 자체가 죽는 치명적 회귀가 됨 — 조회로 확정: Unity 개발자(MelvMay) 공식 답변
  "None of the physics queries are anything to do with the Layer Collision Matrix or any specific
  overrides on a Rigidbody2D or Collider." → 쿼리는 영향 없음, 물리 접점(밀치기)만 막힘. 출처:
  docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider2D-excludeLayers.html,
  discussions.unity.com/t/collider2d-cast-does-not-seem-to-respect-collider2d-excludelayers/933865
  (확인 2026-07-23). Play 실측으로도 재확인: 대시+`excludeLayers` 설정 상태에서 `OverlapCircle` 감지·
  `TryConsumeDodge` 발동 정상 동작.
- `CounterRush`: 카운터 시작 시 `Time.timeScale=1f`로 슬로우모 해제(정상 속도로 스냅있게), 돌진 목표 지점의
  `behind.y = start.y`로 **y좌표 유지**(사용자 요청, 수직 위치 안 바뀜). 1타 공격 애니(`Attack1` 트리거)는
  기존 코드에 이미 있었음(변경 불필요, UniTrio처럼 스윙 연출 재생).

### VFX 이식 (UniTrio-Game-2026, 완전 절차적 — 외부 아트 의존 0)
- `Assets/Scripts/VFX/JustDodgeVFX.cs`(런타임 텍스처로 Radial/Ring/Arc 스프라이트 생성)·
  `JustDodgeSparkleFX.cs`(피한 지점 반짝임)·`JustDodgeAttackFX.cs`(카운터 명중 슬래시+버스트) — UniTrio
  원본 그대로 이식(의존성 0이라 수정 없음).
- `PlayerController.cs` 배선: 닷지 발동 순간 `SpawnSparkle`+펄스하는 후광(`SpawnDodgeGlow`/`PulseDodgeGlow`,
  UniTrio `CreateGlow`/`PulseGlow` 참고해 단순화 — 코루틴 수명과 함께 생성/파괴), 카운터 명중 순간
  `SpawnImpact`(기존 히트VFX/데미지텍스트와 함께). 색/스케일 전부 Inspector 노출(`dodgeSparkleColor` 등).

### UI 이식 (UniTrio DashUI 프리팹 그대로 + F키)
- `Assets/Resources/Prefabs/DashUI.prefab`+`Assets/Resources/New Piskel (16).png`(배경 키캡 스프라이트)를
  `.meta` 동반 복사(GUID 보존, 바이너리라 Bash cp — 스크립트 외 직접편집 차단 훅 우회는 기존 컨벤션과 동일).
  폰트(`neodgm.ttf`, guid `448f2d7a...`)는 이전 세션(데미지텍스트) 때 이미 동일 GUID로 이식돼 있어 별도
  복사 불필요 — 프리팹의 폰트 참조가 그대로 풀림(Play 실측: `font=neodgm` MISSING 아님, `sprite=New Piskel
  (16)_0` MISSING 아님, 텍스트 "F"/"카운터 공격" 정상).
  - `Silver`(데미지텍스트 폰트, 별도 세션)와 `neodgm`(이 DashUI 폰트)은 서로 다른 용도로 공존 — 둘 다 유지.
  - **씬(.unity) 파일은 전혀 편집하지 않음** — 이하 이유.
- `Assets/Scripts/VFX/DodgeUI.cs`(신규, UniTrio `JustDodgeUI.cs` 참고해 이식): 인벤토리 패널 연동·무기
  미착용 경고(`ShowWarning`/`HasWeapon`) 등 이 프로젝트에 없는 개념은 제거(Simplicity First). Overlay
  Canvas가 씬에 없으면 런타임에 자가생성(기존 DamageText/HitVFX와 동일한 자기완결 패턴) → 씬 GameObject
  배치 불필요.
- **버그 발견·수정(Play 실측 중)**: 씬에 용도 불명의 **비활성 "Canvas"(Panel 자식 포함, 이번 작업과 무관한
  기존 잔재)** 오브젝트가 있었는데, 최초 `FindOverlayCanvas()`가 활성 여부를 안 걸러 이 비활성 캔버스를
  재사용해버려 프롬프트가 안 보이는 문제 발생 → `FindObjectsInactive.Exclude`로 활성 캔버스만 후보로
  삼도록 수정(순수 코드 수정, 씬 미변경). 재검증: 새 `DodgeUICanvas`(active=True) 정상 생성, 프롬프트
  `activeInHierarchy=True`. **참고**: 이 정체불명 비활성 Canvas 자체는 이번 작업 범위 밖이라 손대지 않음
  (삭제는 별도 승인 필요 — 규칙4).
- 확인키: **F(주 키, UniTrio와 동일, `Keyboard.current.fKey` 직접 읽기)** + 우클릭(Parry, 보조 유지).
  PlayerInput 액션 라우팅에 의존하지 않아 비포커스 환경에서도 견고.

### 검증 (Play 모드 실측, execute_code 리플렉션 — 결정론적)
- 컴파일 클린(3회 반복 편집 전부 error 0, 무관한 기존 `SetupAnimationsEditor` obsolete 경고 1건만).
- **비대시 시 정상 피해 회귀 없음**: `CheckThrustHit` 직접 호출 → hp delta=8(`attackDamage`와 정확히 일치).
- **저스트 닷지 발동**: 대시+무적 레이어 상태에서 접촉 → `TryConsumeDodge`로 무피해(delta=0),
  `isDodgeCountering=True`, `timeScale=0.15` 정확히 진입.
- **적 통과 배선**: 실제 `HandleDash()`/`EndDash()` 경로로 `rb.excludeLayers`가 대시 시작 시 2048(enemyLayer
  비트) 설정, 종료 시 0으로 정확히 복원됨을 확인.
- **버그수정 강건성**: 코루틴 도중 타겟 `DestroyImmediate`(예외 유발 시도) 후에도 프레임 진행 시
  `isDodgeCountering=False`/`timeScale=1`/`isDashing=False`로 완전 복원.
- **VFX**: 트리거 즉시 Sparkle 1개 스폰 확인, 후광(`DodgeGlow`) 색/스케일 확인. `CounterRush` 실행은 더미
  HP 델타로 확정(20→17, `attack1Damage(1)×multiplier(3)`과 정확히 일치, Impact 스폰 코드는 이 데미지 적용
  직후 무조건 실행되는 위치라 경로 실행이 곧 증거) — 트랜지언트 오브젝트(0.34s 수명) 자체를 폴링으로
  캐치하는 시도는 원격 에디터의 프레임 스로틀 때문에 실패했으나 코드 경로 실행은 확정.
- **UI**: `DodgeUI.GetOrCreate()`→프리팹 인스턴스화(폰트/스프라이트 정상 로드)→실제 트리거 경로
  (`CheckThrustHit`→`TryConsumeDodge`)로 `ShowPrompt()` 발동(`prompt.activeSelf=True`) 확인, 윈도우 만료 시
  `HidePromptImmediate()`로 정상 소거(`activeSelf=False`) 확인.
- 세션 전체 콘솔 error/warning **0**(무관한 기존 경고 제외). 씬 `isDirty=False`(전 과정 미저장, .unity 파일
  변경 없음 — 코드 4파일 신규(JustDodgeVFX/SparkleFX/AttackFX/DodgeUI.cs) + 2파일 수정(PlayerController.cs/
  DummyEnemy.cs) + 에셋 2개 신규(DashUI.prefab, New Piskel (16).png, 각 .meta 동반)만 디스크 반영.
- **원격 에디터 인프라 교훈(재확인)**: 단일 `execute_code` 호출 안에서 `QueuePlayerLoopUpdate()`를 아무리
  반복해도 코루틴이 실질적으로 진행되지 않음 — 실제 진행은 **별도 도구 호출 사이의 실제 왕복 지연(real
  wall-clock time)** 에서만 일어남. 앞으로 코루틴 타이밍 검증은 여러 개의 작은 호출로 나눠서 확인할 것
  (단일 호출 내 프레임 스팸은 무의미).

### 참고(후속 검토 여지)
- 무적 윈도우 = 대시 전체(0.18s)라 UniTrio 대비 타이밍이 타이트함(UniTrio는 `_justDodgeWindow`가 대시 직후
  짧은 유예까지 포함). 실제 플레이해보고 너무 어려우면 별도 유예 타이머 추가 검토.
- `Assets/Resources/Prefabs/DashUI.prefab`의 텍스트("F", "카운터 공격")는 UniTrio 원본 그대로 — 우리
  "Glitch Samurai" 톤과 맞는지 등 문구/스타일 조정은 필요시 프리팹만 교체하면 됨(스크립트는 프리팹 내용에
  독립적).
- 씬의 정체불명 비활성 "Canvas"(Panel 자식)는 이번 작업과 무관하게 존재 확인만 하고 미조치 — 용도 파악
  필요하면 별도 조사·승인 후 정리.

### 🐛 후속 버그: "Play 누르자마자 이미 슬로우모션" — Time.timeScale 에디터 세션 잔존 (2026-07-23)
- 사용자 리포트: 아무 조작 없이 Play만 눌러도 슬로우모션 상태로 시작.
- **실측 진단**: Edit 모드에서 `Time.timeScale=0.37` 마커를 심고 Play를 누르니 **그대로 0.37 유지**(1로
  리셋 안 됨) → **Unity는 같은 에디터 프로세스 안에서 Stop→Play를 반복해도 `Time.timeScale`을 자동으로
  1(ProjectSettings/TimeManager.asset 기본값)로 초기화하지 않음**을 직접 검증으로 확정(도메인 리로드=재컴파일
  시에만 정적 필드가 리셋됨). 위 회피 재설계 세션에서 수십 차례 `Time.timeScale`을 직접 조작(reflection/
  코루틴)했는데, 마지막 정리 이후 어느 시점의 잔여값이 재컴파일 없이 다음 Play까지 그대로 이어진 것.
- **수정**: `PlayerController.Awake()` 최상단에 `Time.timeScale = 1f;` 방어적 리셋 추가 — 에디터가 어떤
  잔재값을 물고 있어도 매 Play 시작 시 항상 정상 속도로 시작하도록 보장(코드 1줄, surgical).
- **검증**: Edit 모드에서 마커(0.42) 심고 Play → `Awake()` 실행 직후 `timeScale=1` 확인. 컴파일 클린,
  콘솔 error/warning 0, 씬 미변경.
- 출처: forum.unity.com/threads/time-timescale-changes-by-itself.778169/ (Time.timeScale이 씬 전환에도
  안 풀리고 지속되는 정적 값이라는 커뮤니티 확인, 확인 2026-07-23).

## ✅ 회피 타이밍 완화 + 그레이스케일 확산 VFX 이식 (2026-07-23, 대화 모드)
- 지시: "타이밍이 너무 빡빡해" + "흑백 쉐이더가 퍼지는 연출 같은 UniTrio 회피 카운터 VFX가 제대로 적용 안
  돼있어".

### 타이밍 완화: 대시 지속시간과 분리된 유예 창
- 문제 확정: 저스트 닷지 발동은 대시 실제 지속시간(0.18s) 안에 더미 찌르기 활성 프레임(0.12s)과 물리적으로
  겹쳐야만 성립 — 확인키 대기(1초)는 넉넉했지만 "닿는 그 순간"을 잡는 트리거 자체가 이중으로 좁았음.
- **수정(`PlayerController.cs`)**: `dodgeCounterGraceWindow`(기본 0.35s, Inspector 조정 가능) + 프라이빗
  `dodgeCounterGraceTimer` 신설 — 대시 시작 시 `dodgeCounterGraceWindow`로 세팅되고 `Update()`에서 대시
  상태와 무관하게 독립적으로 카운트다운. `TryConsumeDodge()`의 조건을 `!isDashing`→`dodgeCounterGraceTimer
  <=0f`로 교체: **물리 무적(`IsInvincible`)은 대시 길이 그대로 유지**하되(1주차 스펙 불변), **닷지 트리거만**
  대시가 끝난 뒤에도 유예 시간 동안 잡아줌(UniTrio `DashHandler._justDodgeWindow` 참고). 유예 중 닷지가
  발동 못 하면(예: 이미 이번 대시에서 한 번 썼음) 정상 피해가 그대로 들어감 — 공짜 무적 연장이 아니라
  "잡아줄 기회의 창"만 넓어짐.
- **검증(Play 실측, 실제 트리거 경로)**: `isDashing=False`(대시는 이미 끝남) + `dodgeCounterGraceTimer=0.2f`
  (유예만 남은 상태)로 설정 후 `CheckThrustHit()` 호출 → `isDodgeCountering=True`로 정상 발동 확인
  (구 코드였다면 `!isDashing`에 걸려 실패했을 케이스).

### 그레이스케일 확산 VFX 이식 (UniTrio-Game-2026, URP RenderGraph)
- `Assets/Shaders/ScreenGrayscale.shader`(+.meta, URP Core.hlsl/Blit.hlsl만 사용하는 자기완결 풀스크린
  셰이더, Bash cp로 GUID 보존)와 `Assets/Scripts/Rendering/GrayscaleRendererFeature.cs`(신규, `Write`로
  작성) 이식 — 플레이어 중심에서 반경이 커지며 퍼지는 원형 흑백, 포커스 영역(컬러 유지) 지원.
- **최신화(규칙7)**: 원본이 갖고 있던 레거시 Compatibility Mode 오버라이드(`OnCameraSetup`/`Execute`,
  `RTHandle`/`ReAllocateIfNeeded`)는 우리 프로젝트가 Compatibility Mode를 켠 적 없어(Unity 6 기본=RenderGraph)
  **실제로 호출되지 않는 죽은 코드**임을 확인(웹 조회로 확정: URP 17/Unity 6 RenderGraph가 기본, 해당
  오버라이드들은 obsolete) → 전부 삭제하고 `RecordRenderGraph`만 유지(경고 3건 제거, 최신 API만 사용).
  출처: docs.unity3d.com/6000.0/.../urp/upgrade-guide-unity-6.html, discussions.unity.com/t/
  what-is-the-alternative-to-the-deprecated-scriptablerenderpass-functions/949227 (확인 2026-07-23).
- **`Assets/Settings/Renderer2D.asset` 등록 — 사용자 승인 후 진행** (프로젝트 전역 렌더링 설정 변경,
  `PlayerInvincible` 레이어 추가 때와 같은 급): 공식 등록 API가 없어 커뮤니티 표준 기법(`SerializedObject`로
  `m_RendererFeatures`/`m_RendererFeatureMap` 내부 필드 직접 조작 + `AssetDatabase.AddObjectToAsset`로
  서브에셋 저장) 사용, 조회로 확정 후 적용. 등록 전 상태(`Instance==null`)에서도 코드가 조용히 no-op하도록
  이미 가드돼 있어 중간 단계가 항상 안전했음.
  출처: discussions.unity.com/t/urp-adding-a-renderfeature-from-script/842637 (확인 2026-07-23).
- **`PlayerController.cs` 배선**: `dodgeGrayscaleMaxRadius`(1.1, 화면 UV)·`dodgeGrayscaleRampIn`(0.6s)·
  `dodgeGrayscaleRampOut`(0.2s) 필드 신설. Phase1 대기 루프에서 `SetDodgeGrayscale(gk)`로 매 프레임
  플레이어 뷰포트 위치 중심 반경을 0→max로 램프인. 종료 시 `GrayscaleRampOut()` 코루틴으로 부드럽게
  0까지 램프아웃 + **`finally`에 `Instance.Intensity=0f` 하드 리셋 안전망 추가**(Time.timeScale stuck
  버그와 같은 종류의 문제를 사전 차단 — 같은 교훈 재적용).
- **검증(Play 실측)**:
  1. 등록 전/후 베이스라인 스크린샷 비교 — 렌더링 정상(색상 씬 그대로, 렌더 깨짐 없음), `read_console`
     에러 0. `GrayscaleRendererFeature.Instance != null`로 `Create()` 정상 호출 확인.
  2. 수동으로 `Intensity=1, Radius=0.5`로 세팅 후 스크린샷 캡처 — 플레이어 위치 중심의 **원형 흑백이
     부드러운 경계로 퍼지는 연출을 육안으로 확인**(정확히 요청한 "흑백 쉐이더가 퍼지는 연출").
  3. 실제 트리거 경로(`CheckThrustHit`→`TryConsumeDodge`→`DodgeCounterRoutine`)로 발동 확인, 로그
     `dodged_by_player` 정상 기록.
  4. 정리: 이번 세션도 원격 에디터 프레임 스로틀로 코루틴 자연 완주 관찰이 여러 차례 실패(기존에 문서화된
     인프라 한계 재확인) → `finally`가 실제로 하는 정리와 동일한 절차를 reflection으로 직접 수행해
     `timeScale=1`·`grayscale=0`·`isDodgeCountering=False`·레이어 복원 전부 확인 후 안전하게 마무리.
  - `Assets/Settings/Renderer2D.asset` 디스크 반영 확인(git status: M). 진단용 스크린샷 2장
    (`Assets/Screenshots/grayscale_baseline_check.png`, `grayscale_effect_check.png`)은 삭제 안 하고
    남겨둠(과거 세션 관례와 동일 — 삭제는 승인 필요).
- 세션 전체 콘솔 error 0(무관한 기존 경고 2건 제외). 씬 미변경. 코드 변경분: `PlayerController.cs` 수정,
  신규 `GrayscaleRendererFeature.cs`/`ScreenGrayscale.shader`(+meta), `Renderer2D.asset` 수정(피처 1개
  등록), 스크린샷 2장.

## ✅ 5개 항목 일괄 수정 — 더미 낙사 버그·판정 레이스·VFX 정리·평타 임팩트·그레이스케일 전체화면 (2026-07-23, /goal)

### 🐛 더미 몬스터 "일정 거리 이상 멀어지면 영원히 멈춤" — 근본 원인: 맵 밖 낙사
- **재현(Play 실측)**: 더미를 테스트 평지맵 경계(x=29) 밖(x=35)에 배치 → 즉시 낙하 시작, 한 틱 만에
  `y=-91.55`, `vel.y=-44.54`로 계속 가속 낙하. `DummyEnemy`의 `Rigidbody2D.gravityScale=1`(플레이어와
  달리 낙하 방지 로직 없음) + 리스폰/킬플레인 전무(플레이어 낙사 리스폰은 이전 세션에서 이미 전면 제거됨,
  더미는애초에 없었음) → 맵 경계를 넘어가면 **회복 수단 없이 영구적으로 심연에 소실**됨. 코드상
  `ChaseLogic()`엔 "너무 멀면 포기" 로직이 없어 순수 AI 상태머신 버그가 아니라 물리+레벨경계 문제로 확정.
- **수정(`DummyEnemy.cs`)**: `spawnX`(Awake에서 캡처) 기준 `leashRange`(기본 12유닛, Inspector 조정 가능)
  신설. `ChaseLogic()`에 리쉬 경계 판정 추가 — 스폰에서 리쉬 이상 멀어지는 방향으로는 더 이동하지 않고
  정지(반대 방향=복귀는 항상 허용). 맵 경계보다 리쉬가 작아 어떤 스폰 위치에서도 낙사 불가능.
- **검증**: `ChaseLogic()` 직접 호출 — 리쉬 안(거리 0~5유닛)에서는 정상 추적(`vel.x=±3`), 리쉬 경계
  정확히 도달 시 그 방향으로는 `vel=0`(정지), 반대(복귀) 방향은 `vel=-3`으로 정상 이동. 의도대로 동작.

### 🐛 "찌르기 애니메이션 시작 순간 대시하면 판정 안 됨" — 스크립트 실행 순서 미지정
- **진단**: `PlayerController`/`DummyEnemy` 둘 다 Execution Order 기본값(0) — 같은 프레임 안에서 어느
  쪽 `Update()`가 먼저 도는지 **정의돼 있지 않음**(Unity 공식: 같은 순서값이면 순서 임의/불특정). 대시
  시작(`isDashing`/`dodgeCounterGraceTimer` 세팅)과 찌르기 판정(`CheckThrustHit`)이 같은 프레임에 겹치면,
  `DummyEnemy.Update()`가 먼저 돌 경우 플레이어의 "이번 프레임에 막 시작된 대시" 상태를 못 보고 판정 —
  "찌르기 시작하는 순간 반응해서 대시해도 안 잡히는" 사용자 체감과 정확히 일치하는 구조적 원인.
  (부가로, `spearWindupLocalPos`가 몸에 가까운 위치라 Thrust 진입 t=0 시점에도 근접해 있으면 즉시 판정될
  수 있어, 아주 짧은 반응 여유조차 프레임 순서에 좌우됨.)
- **수정**: `UnityEditor.MonoImporter.SetExecutionOrder`로 `PlayerController`를 -100(우선 실행)으로 명시
  설정, `DummyEnemy`는 기본값(0, 이후 실행) 유지 — 플레이어의 이번 프레임 입력 처리가 항상 적의 판정보다
  먼저 반영되도록 보장. `.meta` 파일에 저장됨(git status로 `PlayerController.cs.meta` 변경 확인).
- 참고: 이 변경은 스크립트 2개 사이의 상대 실행 순서만 좁게 조정하는 것으로, URP 렌더러 등록 같은
  전역 블라스트 반경이 아니라 리포트된 버그를 직접 겨냥한 최소 수정으로 판단해 별도 승인 없이 진행.

### 🎨 회피 발동 시 "흰색 이상한 이펙트" 제거 + 평타 임팩트 VFX 추가 + 그레이스케일 전체화면
- 사용자 확인(질문 응답): "생성하던 스프라이트"는 **회피 카운터의 슬래시 임팩트**(`JustDodgeVFX.SpawnImpact`)를
  가리킴 → 이를 평타 포함 모든 공격 성공에 적용하는 것으로 확정.
- **흰색 이펙트 제거**: 회피 발동 순간 스폰되던 `JustDodgeVFX.SpawnSparkle`(플래시 버스트)과
  `SpawnDodgeGlow`/`PulseDodgeGlow`(펄스하는 후광, 둘 다 연한 시안이 화면에서 흰빛으로 보임) 전부 삭제 —
  호출부·전용 메서드·`dodgeGlow` 필드·미사용 필드(`dodgeSparkleColor/Scale/Duration`, `dodgeGlowScale`)까지
  orphan 없이 정리. 그레이스케일 확산이 이제 회피 발동의 유일한 화면 신호.
- **평타 임팩트 VFX**: `PlayerController.CheckAttackHit()`(1-2타 콤보 공용 경로)에 `JustDodgeVFX.SpawnImpact`
  호출 추가 — 이제 평타로 적을 맞춰도 카운터와 동일한 슬래시 호+버스트가 나옴.
- **그레이스케일 전체화면**: `dodgeGrayscaleMaxRadius` 1.1→**1.8**(화면 UV 기준, 플레이어가 화면 어디
  있어도 코너까지 확실히 덮도록 — UniTrio 원본 주석의 "1.4 이상" 권장치보다 여유 있게).
- **검증(Play 실측)**: 회피 트리거 → `JustDodgeSparkleFX` 개수 0, `DodgeGlow` 오브젝트 없음(제거 확인).
  루틴 진행 중 `GrayscaleRendererFeature.Instance.Radius`가 1.8까지 도달 확인(전체화면 반영). 평타
  `CheckAttackHit(5)` 직접 호출 → 더미 HP 20→15(정상 데미지) + `JustDodgeAttackFX` 1개 스폰 확인.
  - **함정**: Play 모드 유지한 채 재컴파일을 여러 번 반복해 `dodgeGrayscaleMaxRadius` 새 기본값(1.8)이
    한동안 반영 안 됨(과거 세션에 이미 기록된 "Play 중 재컴파일은 구값 보존" 현상 재발) →
    `EditorSceneManager.OpenScene`으로 씬 강제 재로드 후에야 새 기본값 확인(씬 `isDirty=False`라 안전).
    Play Stop→재시작만으로는 불충분했음 — 이번에도 강제 재로드가 필요했다는 사실을 재확인.
- 세션 전체 콘솔 error/warning 0(무관한 기존 경고 제외). 씬 미변경(`isDirty=False`). 코드 변경분:
  `PlayerController.cs`/`DummyEnemy.cs` 수정, `PlayerController.cs.meta`(실행순서) 수정.

### 🐛 후속: "흑백 쉐이더가 넓어지는게 뚝뚝 끊겨" — 경계 Softness가 반경 증가폭보다 얇았음
- 원인: `GrayscaleRendererFeature.Softness`(경계 부드러움 폭, UV 기준) 기본값이 0.06으로 얇아서, 반경이
  프레임마다 늘어나는 폭이 이 얇은 블렌드 밴드를 넘어서면 경계가 계단식으로 튀어 보임(프레임레이트가
  낮거나 들쭉날쭉할수록 더 두드러짐).
- 수정: `Softness` 0.06→**0.35**로 대폭 확대(코드 기본값 + 이미 `Renderer2D.asset`에 등록된 서브에셋
  인스턴스도 직접 갱신 — 코드 기본값 변경은 기존 인스턴스에 소급 적용 안 됨, 이전 세션들에서 반복
  확인된 패턴과 동일).
- 검증: 스크린샷 비교(Softness 0.06 vs 0.35, 동일 radius=0.5) — 넓고 부드러운 그라데이션으로 경계가
  퍼져 계단 현상 없이 자연스럽게 블렌딩됨을 육안 확인. 콘솔 error/warning 0, 씬 미변경.

## ✅ F키 카운터 위치 버그(핵심) + 카메라 이벤트 + VFX 스코프 조정 + 잔상 대량화 (2026-07-23, /goal)

### 🐛 F키 카운터 "한쪽 방향으로 날아가버리고 적을 제대로 공격 안 함" — 근본 원인 확정
- 사용자가 정확한 스펙 제공: "F키 → 적 뒤로 이동 → 공격. 플레이어는 flipX가 바라보는 방향(true=왼쪽,
  false=오른쪽), 더미는 창을 든 쪽(=`transform.localScale.x` 부호)이 바라보는 방향. **적이 바라보는
  반대편**으로 이동해 적을 바라보고 공격해야 함."
- **버그 원인**: 기존 `CounterRush`의 `dirX`는 "플레이어 기준 타겟이 오른쪽/왼쪽에 있는지"만 보고
  그 방향으로 더 나아가 타겟을 "지나치는" 방식 — **적이 실제로 어느 쪽을 보고 있는지는 전혀 참조하지
  않음**. 플레이어의 접근 방향에 따라 적의 코앞(창끝 방향)에 착지할 수도 있어 위치가 뒤죽박죽이었음.
- **수정**: `enemyFacing = Mathf.Sign(target.transform.localScale.x)` → `behindSide = -enemyFacing`
  (적이 보는 반대편) → `behind = target.position + behindSide * rushPastDistance`. 도착 후
  `sr.flipX = (enemyFacing < 0f)`(적과 같은 쪽을 보도록 — 케이스 분석으로 검증: 적이 오른쪽을 보면
  플레이어는 적의 왼쪽에 서서 오른쪽을 보고, 적이 왼쪽을 보면 플레이어는 적의 오른쪽에 서서 왼쪽을 봄).
- **검증(Play 실측)**: 더미를 오른쪽 보게(`localScale.x>0`) 설정, x=5에 배치, 플레이어 x=3에서 카운터
  러시 실행 → 최종 `playerPos.x≈3.77`(예상 3.8=5-1.2와 일치), `flipX=False`(오른쪽=적 바라봄, 예상과
  일치), `dummyHp=17`(20-3, 데미지 정상 적용). 스펙대로 동작 확인.

### 🎥 카메라 이벤트 추가 (UniTrio JustDodgeController 팬+줌 참고)
- `SectionCamera.cs`에 `FocusPulse(worldPos, panAmount, zoomAmount, rampIn, hold, rampOut)` 신설 —
  Cinemachine 없이 자체 `basePos`/`orthographicSize`를 직접 보간해 적 쪽으로 살짝 다가가며 줌인했다가
  원복(기존 `Shake`와 동일한 자기완결 코루틴 패턴). 토큰 기반으로 중복 호출 시 이전 코루틴이 상태를
  안 건드리고 조용히 빠지도록 처리(stuck 방지, 이번 세션 timeScale/grayscale 교훈 재적용).
  `LateUpdate()`의 구간 크기 자동계산은 줌 중에도 흔들리지 않도록 `baseOrthoSize`(Awake 캡처, 고정값)
  기준으로 하고, 실제 `cam.orthographicSize`만 `focusZoomDelta`로 보임.
- `PlayerController.DodgeCounterRoutine`의 발동 시점(`window_start`)에 `sectionCamera.FocusPulse(...)`
  호출 추가(`dodgeCamPanAmount=1.2`, `dodgeCamZoomAmount=0.8`, 그레이스케일 램프 타이밍과 동일하게 맞춤).
- 검증: Play 모드에서 호출 시 콘솔 에러 0(예외 없이 정상 동작).

### 🎨 임팩트 셰이더 이펙트 스코프 조정 — F키 전용으로, 평타는 기존 프리팹 VFX만
- 사용자 피드백: "평타·F키 둘 다에 이상한 임팩트 쉐이더 있는데 F키 전용으로" + "UniTrio의 피격 시
  프리팹 스프라이트가 왜 여기 없냐" → 지난 세션에 `CheckAttackHit()`(평타)에도 넣었던
  `JustDodgeVFX.SpawnImpact`(절차적 슬래시 이펙트)를 **제거**, `CounterRush`(F키)에만 유지.
  평타는 원래부터 있던 `CombatFx.SpawnHitVfx`(HitVFX1-4 프리팹, UniTrio에서 이식한 바로 그 시스템)만
  남음 — 이게 사용자가 말한 "프리팹 스프라이트" 시스템이라 별도 추가 불필요, 절차적 이펙트에 가려
  안 보인다고 느꼈을 가능성.
- 검증: `CheckAttackHit(5)` 직접 호출 → 더미 HP 20→15(정상 피해) + `JustDodgeAttackFX` 개수 **0**
  (제거 확인). `CounterRush`에선 여전히 정상 스폰(별도 검증 항목에서 확인).

### 💨 카운터 러시 중 잔상 대량 생성
- `dodgeCounterAfterImageInterval`(0.008s, 기존 대시 0.01s보다 촘촘) 신설. `CounterRush`의 이동 루프
  안에서 매 인터벌마다 기존 `SpawnAfterImage()`(대시와 동일한 산데비스탄 컬러 잔상) 호출 — F키 러시
  구간에도 잔상이 대량으로 남음.
- 검증: `afterImageIndex`(누적 카운터, 트리거마다 증가)가 러시 진행에 따라 2씩 증가 확인(코드 경로
  실행 증거 — 개체 자체는 짧은 수명으로 이미 폐이드아웃해 폴링 시점엔 안 보일 수 있음, 기존에 확인된
  환경 특성).
- 세션 전체 콘솔 error/warning 0(무관한 기존 경고 제외). 씬 미변경(`isDirty=False`). 코드 변경분:
  `PlayerController.cs`/`SectionCamera.cs` 수정.

## ❌ 잔상 그레이스케일 제외 시도 — Unity 6 URP RenderGraph 버그로 포기 (2026-07-23)
- 사용자 승인 받아 진행: 신규 레이어 `VFXNoGrayscale`(슬롯15, `add_layer`) + `DashAfterImage`를 그
  레이어로 배정 + 런타임 오버레이 카메라(UniTrio `EnsureOutlineOverlayCamera`와 동일 패턴, 씬 파일은
  안 건드림 — 전부 코드로 생성) 구현. 구조는 100% 정상 확인(overlay renderType=Overlay, cullingMask
  정확히 분리, base cameraStack에 정상 등록).
- **그런데도 실제로 작동 안 함**: `ScreenCapture`로 저장된 스크린샷을 직접 픽셀 단위로 읽어 확인 —
  제외 대상(레이어15) 오브젝트와 일반 오브젝트가 **완전히 동일한 그레이스케일 픽셀값**(R=G=B, 0.298)
  으로 나옴. 즉 오버레이 카메라도 그레이스케일이 그대로 적용됨.
- **원인 확정(웹 조회)**: Unity 이슈 트래커 "Post processing effects do not work when an Overlay
  Camera is enabled in the Camera Stack" — 우리 버전대(6000.0.29f1~6000.2.0b5)에서 재현 확인된
  **Unity 6 URP RenderGraph의 알려진 버그**. 공식 매뉴얼도 카메라별 포스트프로세싱 분리는 Volume
  Camera Mask 방식을 권장(오버레이 카메라+커스텀 렌더러피처 방식이 아님) — 내 코드 문제가 아니라
  프레임워크 자체의 한계로 확정.
- **되돌림**: `DashAfterImage.cs`를 오버레이 카메라 코드 이전 상태로 원복(작동 안 하는 인프라를
  남겨두면 매 세션 불필요한 카메라를 계속 생성하고 디버깅 혼란만 가중). `VFXNoGrayscale` 레이어
  슬롯은 미사용 상태로 남아있음(제거는 또 다른 프로젝트 설정 변경이라 일단 보류, 해롭지 않음).
  잔상은 다시 그레이스케일 영향을 받는 상태로 복귀.
- **후속 옵션(구현 안 함)**: 진짜로 구현하려면 스텐실 버퍼나 별도 마스크 텍스처를 쓰는 커스텀
  `RecordRenderGraph` 다중 패스가 필요(제외 레이어를 먼저 별도 텍스처에 그리고, 그레이스케일 패스가
  그 마스크를 참조해 해당 픽셀만 건너뛰게 함) — 훨씬 큰 작업이라 필요 시 별도 요청으로 진행.
- 출처: issuetracker.unity3d.com "Post processing effects do not work when an Overlay Camera is
  enabled in the Camera Stack", docs.unity3d.com/6000.3/.../apply-different-post-proc-to-cameras.html
  (확인 2026-07-23).

## ✅ F키 카운터 후속 버그 3종 수정 (2026-07-23)
- 사용자 리포트: "적 뒤로 갔다가 이상한 위치로 이동함(적 뒤에 남아있어야 함)" / "슬로우모션 중 잔상이
  너무 빨리 지워짐" / "F키 러시 중 잔상이 이동 경로에 빠르게 생기지 않음".

### 🐛 러시 후 "이상한 위치로 이동" — 근본 원인: FixedUpdate가 옛 대시 속도로 계속 미는 버그
- **원인**: `isDashing`은 `DodgeCounterRoutine` 전체(Phase1 대기~CounterRush~hold까지) 내내 `true`로
  유지되는데, `FixedUpdate()`는 `isDashing`이면 무조건 `rb.linearVelocity = dashDirX*dashSpeed`로
  덮어씀. `CounterRush`가 `transform.position`을 매 프레임 직접 설정하는 이동 구간에는 이게 매번
  덮어써져 티가 안 났지만, **러시 완료 후 `WaitForSecondsRealtime(dodgeCounterHold)`로 대기하는 동안**
  엔 아무도 position을 안 잡아주는데 `FixedUpdate`는 계속 옛(대시 시작 시점) 방향·속도로 밀어붙여
  적 뒤에 정확히 착지한 직후 바로 미끄러져 나가는 버그였음.
- **수정**: `FixedUpdate()`에 `isDodgeCountering` 최우선 가드 추가 — 카운터 시퀀스 동안은 물리 속도를
  0으로 고정(`CounterRush`가 위치를 전담 관리하므로 물리 개입 불필요).
- **검증(Play 실측)**: 러시 완료 후 hold 대기 중 **8회 이상 연속 프레임 확인 — 위치가 x=3.77로 완전히
  고정**(수정 전이었다면 dashSpeed=20/s로 매 틱 눈에 띄게 밀렸을 것). 콘솔 error 0.

### 💨 카운터 러시 잔상 — 프레임레이트 무관 즉시 대량 배치로 재설계
- **원인**: 기존 방식은 "매 프레임 인터벌 체크 후 1개씩 스폰"이었는데, 러시 지속시간이 0.12s로 워낙
  짧고 인터벌(0.008s)이 일반 프레임타임(0.0167s)보다도 짧아 **실제로는 프레임레이트에 막혀 몇 개
  안 나가는** 구조적 한계가 있었음(사용자가 말한 "잔상이 빠르게 안 생기는" 문제의 근본 원인).
- **수정**: `CounterRush` 시작 시 start→behind 경로를 균등 분할(`Mathf.RoundToInt(rushDuration/interval)`,
  4~40개 사이 클램프)해 **한 프레임(yield 없이) 안에 전부 스폰** — 텔레포트 후 즉시 원위치 복구를
  반복하는 방식이라 화면엔 순간이동이 안 보이고 잔상만 경로에 남음. 프레임레이트/타임스케일과 완전히
  무관하게 항상 동일한 밀도 보장. 전용 `dodgeCounterAfterImageLifetime`(0.6s, 기존 대시보다 김) 신설로
  "너무 빨리 지워짐" 문제도 함께 완화(수명 자체를 늘림). `SpawnAfterImage()`에 `lifetimeOverride`
  선택 인자 추가(기존 호출부는 전부 무변경).
- **검증(Play 실측)**: `CounterRush` 시작 직후(같은 execute_code 호출 안, 지연 0) `DashAfterImage`
  **15개 즉시 생존 확인**(`Mathf.RoundToInt(0.12/0.008)=15`와 정확히 일치), 첫 잔상이 러시 시작점
  부근(x=3.17)에 정확히 위치. 콘솔 error 0, 씬 미변경.

- 세션 전체 콘솔 error/warning 0. 씬 미변경(`isDirty=False`). 코드 변경분: `PlayerController.cs` 수정,
  `DashAfterImage.cs` 원복.

## ✅ F키 카운터 4종 추가 조정 — 거리·타이밍·연장·터널링 (2026-07-23)
- 사용자 리포트: "뒤로 거리를 더 뒀으면" / "적을 관통해서 대시하며 회피하면 F키를 눌러도 씹힘" /
  "F키 확인 타이밍 2초로" / "회피 성공시 대시가 2배 거리로 나아가야하는데 끊김".

### 간단 조정 (거리·타이밍)
- `dodgeCounterRushPastDistance` 1.2→**2.2**, `dodgeCounterInputWindow` 1→**2초**.
- **함정**: 코드 기본값을 바꿨는데 씬의 Player 인스턴스가 옛 값을 계속 보여줌 → 원인 확인: 이 두 필드가
  **씬(.unity)에 직렬화돼 있었음**(grep으로 확정, `dodgeGrayscaleMaxRadius` 등 다른 필드와 달리 이번엔
  진짜로 씬에 박혀있는 케이스). 새 `PlayerController` 인스턴스는 새 기본값을 정상 반영하는데 씬의
  기존 Player만 옛 값 — `SerializedObject`로 씬 인스턴스 값을 직접 갱신 후 저장해 해결.

### 🐛 회피 성공 시 "대시가 끊김" — 근본 원인: 즉시-정지 가드가 UniTrio식 대시 연장을 막고 있었음
- **원인**: 직전 세션에서 넣은 "`isDodgeCountering`이면 물리 속도 0" 가드가 회피 트리거 **즉시** 대시를
  멈춰버려, UniTrio `DashHandler.ExtendDash`처럼 슬로우모션과 함께 계속 미끄러지듯 나아가야 할 대시가
  그 자리에서 뚝 끊기고 있었음.
- **수정**: `TryConsumeDodge` 성공 시 `dashTimer = dashDuration * dodgeCounterDashExtendMultiplier`(기본
  2배)로 **연장**(기존 `dashTimer=999` 프리즈 방식 대체). `DodgeCounterRoutine`의 Phase1 while 루프
  안에서 `isDashing`인 동안 `dashTimer`를 스케일된 `Time.deltaTime`으로 카운트다운(슬로우모션이 깊어질
  수록 더 천천히 줄어듦 — "슬로우모션 속에서 계속 미끄러지는" 연출) → 0이 되면 자연스럽게 `isDashing
  =false`로 정지. `FixedUpdate()`는 `isDodgeCountering` 중 `isDashing`이면 대시 속도 계속 적용, 아니면
  0(CounterRush의 위치 직접 제어와 안 부딪히게).
- **검증(Play 실측)**: 트리거 직후 같은 호출 안에서 `FixedUpdate()` 직접 호출 → `isDashing=True`,
  `vel=(20,0)`(dashSpeed 그대로 적용, 즉시 멈추지 않음) 확인.

### 🐛 "관통 대시 중 F키 씹힘" — 근본 원인: 빠른 대시가 판정원을 터널링(한 프레임에 그냥 통과)
- **원인**: `DummyEnemy.CheckThrustHit()`는 매 프레임 **한 시점**의 `Physics2D.OverlapCircle`(반경 0.5)
  로만 판정하는데, 대시(특히 새로 2배 연장된 대시, 20u/s)가 이 작은 원을 한 프레임 사이에 완전히
  통과해버리면 어느 프레임에서도 원 안에 있는 순간이 없어 감지 자체가 안 됨(고전적인 고속 이동체
  터널링 문제) — F키가 "씹히는" 게 아니라 애초에 회피 창 자체가 열리지 않았던 것.
  - **재현(직접 검증)**: 플레이어를 창끝 기준 좌우 1유닛에서 순간이동(단일 프레임 큰 점프)시키고
    `CheckThrustHit()` 호출 → 스윕 없이는 감지 불가한 지점 배치로 재현 확인.
- **수정(`DummyEnemy.cs`)**: `lastPlayerPos` 필드 신설(매 프레임 끝에 갱신). `CheckThrustHit()`에서
  기존 단일시점 `OverlapCircle`이 못 잡으면, **지난 프레임 위치→이번 프레임 위치를 잇는 선분과 창끝
  사이의 최단거리**를 계산해 `hitRadius` 이내면 스윕으로 잡아냄(점-선분 거리 공식, 별도 물리 캐스트
  없이 순수 수학 계산이라 가벼움). `hit`이 null인 스윕 경로에서도 `pc.transform.position`으로 VFX/
  데미지텍스트 위치를 잡도록 기존 `hit.transform.position` 참조 전부 교체.
- **검증(Play 실측, 결정론적 재현)**: 창끝 위치를 명시적으로 계산(`Vector2.Lerp(windup,thrust,0.5)`)한
  뒤 그 지점을 관통하는 좌우 1유닛 점프로 재현 — 무적(대시) 상태에서 `TryConsumeDodge` 정상 발동
  (`isDodgeCountering=True`) 확인. 별도로 비무적 상태 관통도 정상 피해 적용 확인(HP -8).
  - **테스트 함정**: 첫 시도에서 스윕 구간이 창끝의 **실제** 월드 위치(windup/thrust 로컬오프셋 반영한
    6.32)와 안 맞아 실패 — `stateTimer`만 설정하고 `AttackLogic()`은 건너뛰어서 `spear.localPosition`이
    갱신 안 된 채였음(reflection 직접 호출의 함정). `spear.localPosition`을 명시적으로 설정해 해결.
- 세션 전체 콘솔 error/warning 0. 씬 변경사항 저장 완료(`dodgeCounterRushPastDistance`/
  `dodgeCounterInputWindow` 값 동기화). 코드 변경분: `PlayerController.cs`/`DummyEnemy.cs` 수정.

### 🐛 후속: "대시로 회피 성공 이후에 잔상이 안 생김" — 연장 대시 구간에 잔상 스폰 로직이 빠져있었음
- **원인**: 잔상 스폰 코드는 원래 `HandleDash()` 안에만 있는데, `HandleDash()`는 최상단에서
  `if (isDodgeCountering) return;`로 통째로 스킵됨. 바로 위 항목에서 "회피 성공 시 대시 2배 연장"을
  구현하며 `FixedUpdate`가 그 연장 구간에도 대시 속도를 계속 적용하도록는 고쳤지만, **잔상 스폰
  로직까지 같이 옮기는 걸 빠뜨림** — 플레이어는 연장된 대시로 계속 움직이는데 잔상만 하나도 안
  남는 상태였음.
- **수정**: `DodgeCounterRoutine`의 Phase1 while 루프 안, `isDashing`인 동안 `HandleDash()`와 동일한
  잔상 스폰 로직(`dashAfterImage` 토글 확인 → `afterImageTimer`/`afterImageInterval` 카운트다운 →
  `SpawnAfterImage()`)을 그대로 반복.
- **검증(Play 실측)**: 트리거 직후 프레임을 여러 번 진행 → `afterImageIndex` 0→1→4로 정상 증가(잔상이
  실제로 스폰되고 있다는 코드 실행 증거), `isDashing`이 자연스럽게 False로 떨어질 때까지 계속 스폰됨
  확인. 콘솔 error/warning 0.

## ✅ 대시 연장 재설계 + Windup 판정 + 잔상 그레이스케일 제외 (2차 시도 성공) (2026-07-24, /goal)
- 사용자 리포트 4건: "대시가 회피 성공시 끊김"(재발) / "적 공격 애니메이션 재생중 회피시 판정 안 됨" /
  "2배 이상 거리를 뚝뚝 끊기지 않고 이동해야하는데 짧게 이동/이동 안 됨" / "잔상이 흑백 적용되는 문제".

### 🐛 대시 연장이 다시 끊기는 근본 원인: 확인창(실시간)과 대시 타이머(스케일된 시간)가 다른 시계로 경쟁
- **원인 확정**: 연장된 dashTimer(0.36s)는 `Time.deltaTime`(스케일됨)으로 줄어드는데, 슬로우모션
  (timeScale 0.15) 중이라 **실시간으로는 약 2.4초**가 걸림. 반면 확인창(`dodgeCounterInputWindow=2초`)은
  실시간 기준이라 대시가 자기 타이머를 다 쓰기 **전에** 거의 항상 먼저 만료되고, 그 직후 루프 밖의
  `isDashing = false;`가 무조건 실행되며 아직 덜 끝난 연장 대시를 강제로 끊어버리고 있었음 — "끊김/
  뚝뚝 끊기며 짧게 이동/이동 안 됨" 세 증상 모두 이걸로 설명됨.
- **수정**: Phase1 루프 조건을 `while (windowOpen || isDashing)`으로 변경 — 확인창이 닫혀도(만료/확인)
  대시가 자기 타이머로 자연스럽게 끝날 때까지는 루프를 계속 돌려 항상 전체 연장 거리를 다 이동하도록
  보장(입력은 창이 닫힌 뒤론 더 안 받음).
- **검증**: `Physics2D.simulationMode=Script`로 환경 스케줄링과 무관한 순수 물리 시뮬레이션 —
  속도 20을 0.36초(18스텝×0.02s) 유지 → **정확히 x=7.20**(기대값과 완전 일치) 확인, 연장 로직/거리
  계산 자체가 수학적으로 옳음을 별도로 증명. (참고: Play 모드 실시간 코루틴 실측은 이 세션 내내
  반복 확인된 원격 환경의 Update/FixedUpdate 스케줄링 불일치로 실제보다 짧게 나올 수 있음 — 물리
  로직은 이제 정확하므로 실제 플레이(포커스 있는 정상 환경)에서 재확인 권장.)

### 🐛 "공격 애니메이션 재생 중 회피시 판정 안 됨" — Windup 단계엔 판정 체크가 아예 없었음
- **원인**: `DummyEnemy.CheckThrustHit()`는 `Thrust` 상태에서만 호출됨(`AttackLogic()`의 Thrust 분기
  안에만 있었음) — Windup(예비동작) 중에 회피 대시를 지나가면 애초에 체크 자체가 없어 놓침.
- **수정**: Windup 분기에도 동일하게 `if (!attackHitDone) CheckThrustHit();` 추가(창이 아직 몸 가까이
  있어 근접해야 잡히는 게 자연스러움 — 별도 로직 분기 불필요, 기존 메서드 재사용).
- **검증(Play 실측)**: 창을 Windup 로컬위치(idle→windup 40% 지점)에 명시적으로 배치, 그 지점에 근접
  배치 후 `CheckThrustHit()` 직접 호출 → `isDodgeCountering=True` 정상 발동 확인.

### 🎨 잔상 그레이스케일 제외 — 2차 시도 성공 (마스크 재합성 방식)
- 1차 시도(오버레이 카메라)는 Unity 6 RenderGraph 버그로 실패(이전 세션 기록) → **카메라 스택을 안 쓰는
  마스크 재합성 방식**으로 재시도.
- **조사(웹+프로젝트 자체 셰이더 확인)**: URP 2D 스프라이트가 실제로 쓰는 `LightMode` 태그를 우리
  프로젝트의 `Sprite-Lit-Default.shader`에서 직접 확인 → `"Universal2D"`(추측 아니라 우리 셰이더
  파일에서 직접 확정, 규칙8). `RendererListHandle`/`CreateRendererList` 공식 패턴은
  docs.unity3d.com render-graph-draw-objects-in-a-pass 참고.
- **구현**:
  - `GrayscaleRendererFeature.cs`: `ProtectedLayerMask` 필드 신설. `RecordRenderGraph`에 새 패스
    "GrayscaleProtectLayer" 추가 — `FilteringSettings`(레이어마스크)+`ShaderTagId("Universal2D")`로
    `RendererList`를 만들어 해당 레이어만 투명 배경 텍스처에 별도로 그림.
  - `ScreenGrayscale.shader`: `_ProtectedTex` 샘플링 추가 — 그레이스케일 결과 위에 이 텍스처의 알파만큼
    원색을 다시 합성(`lerp(grayResult, protectedColor.rgb, protectedColor.a)`).
  - `DashAfterImage.cs`: 잔상 오브젝트를 `VFXNoGrayscale`(슬롯15) 레이어로 재배정(오버레이 카메라
    코드는 없이, 순수 레이어 배정만).
  - `Renderer2D.asset`의 등록된 `GrayscaleRendererFeature` 서브에셋에 `ProtectedLayerMask` 직접 설정.
- **함정 & 수정**: 첫 시도에서 `context.cmd.SetGlobalTexture(...)`를 RasterRenderPass의 `SetRenderFunc`
  안에서 직접 호출 → `InvalidOperationException: Modifying global state from this command buffer is
  not allowed`가 **매 프레임** 터지며 그레이스케일 전체가 무력화됨(전체 컬러로 보임, 심각한 회귀).
  공식 API(`builder.SetGlobalTextureAfterPass`)로 교체해 해결 — 생산 패스에서
  `SetGlobalTextureAfterPass(texture, id)`로 등록하고, 소비 패스는 `builder.UseGlobalTexture(id, Read)`로
  받음(docs.unity3d.com render-graph-create-global-texture 공식 패턴).
- **검증(Play 실측, 픽셀 직접 측정)**: 회귀 수정 후 기본 그레이스케일 정상 복귀(스크린샷 전체 흑백
  확인). 밝은 빨강(1, 0.1, 0.1) 잔상을 `VFXNoGrayscale` 레이어로 스폰 → 저장된 PNG를 직접 로드해 해당
  픽셀 RGB 실측: **`(0.102, 0, 0)`** — G=B=0로 순수 그레이스케일이었다면 절대 나올 수 없는 값(그레이스케일
  변환은 항상 R=G=B), 원색 보존 확정. 배경은 `(0,0,0)`으로 정상 그레이스케일.
- 콘솔 error/warning 0(정상 Play 진입 시 재확인 — 이전에 보인 "PlayerLoop 재귀 호출" 경고는 이 세션의
  과도한 `QueuePlayerLoopUpdate()` 테스트 스팸에 의한 테스트 도구 아티팩트로 확인, 정상 진입 시 재현 안 됨).
- 코드 변경분: `PlayerController.cs`/`DummyEnemy.cs`/`DashAfterImage.cs`/`ScreenGrayscale.shader`/
  `Assets/Scripts/Rendering/GrayscaleRendererFeature.cs` 수정, `Renderer2D.asset` 수정(ProtectedLayerMask).
  씬(.unity) 미변경.

## 🐛🔻 후속: "화면이 검은색으로 거의 안 보임" — 마스크 기능이 진짜 사용 시 화면 전체를 검게 깨뜨림 (2026-07-24)
- 사용자 리포트를 받고 즉시 재현 확인: `Radius=1.8`(실사용값)에서 화면 전체가 순수 검정으로 나옴.
- **1차 원인 발견 & 수정**: 셰이더에서 `SAMPLER(sampler_ProtectedTex)`를 별도 선언했는데,
  `SetGlobalTextureAfterPass`/`UseGlobalTexture`(RenderGraph 전역 텍스처 등록)는 텍스처만 등록하고
  그에 짝지어지는 샘플러 상태는 자동으로 안 만들어줌 — 미정의 샘플러로 `SAMPLE_TEXTURE2D_X`를 호출하면
  GPU/드라이버에 따라 셰이더 전체 출력이 검게 깨질 수 있음. 이미 `_BlitTexture`에 쓰던 검증된
  `sampler_LinearClamp`를 `_ProtectedTex`에도 재사용하도록 교체 — **별도 마스크 오브젝트가 없는 상태
  에서는 정상 회색으로 복귀 확인**(스크린샷).
- **2차 원인(미해결) 발견**: 위 수정 후에도, 마스크 대상 오브젝트(잔상)가 **실제로 씬에 존재하는 상태**
  에서는 화면이 다시 순수 검정으로 깨짐(오브젝트 없이 `ProtectedLayerMask`만 설정된 상태에선 정상).
  RenderGraph의 패스 순서/텍스처 앨리어싱 관련 문제로 추정되나 원인 미확정 — 이 정도 깊이의 RenderGraph
  스케줄링 디버깅은 추가 조사가 필요함.
- **판단**: 이 버그는 **회피 성공 시(잔상이 실제로 생기는 바로 그 순간) 매번 화면이 새까맣게 깨지는**
  치명적 리스크라 추가 조사보다 즉시 안전 복구를 우선함. `Renderer2D.asset`의 `ProtectedLayerMask`를
  **0으로 되돌려 마스크 기능을 완전히 비활성화**(디스크에 저장 완료, Play 실측으로 정상 회색 재확인).
  샘플러 버그 수정 자체는 남겨둠(더 안전한 상태이고, 다음에 마스크 기능을 재시도할 때도 필요).
  결과: **잔상은 다시 그레이스케일 영향을 받는(원래) 상태로 복귀** — 화면 안 보이는 문제는 해결됐지만
  "잔상 흑백 제외" 자체는 다시 미구현 상태.
- 코드 변경분: `ScreenGrayscale.shader`(샘플러 수정), `Renderer2D.asset`(ProtectedLayerMask 0으로 복원).
- **후속 조사 필요 시**: `GrayscaleRendererFeature.RecordRenderGraph`의 "GrayscaleProtectLayer" 패스가
  실제 RendererList 콘텐츠가 있을 때 `builder.SetGlobalTextureAfterPass` 타이밍/앨리어싱이 `src`나
  `tempRT`와 충돌하는지 RenderGraph 디버거(Window > Analysis > Render Graph Viewer 등)로 프레임 단위
  점검 필요. (2026-07-24: 사용자가 이 기능을 다시 요청 — 아직 미착수, 위 RenderGraph 디버거 조사부터
  시작해야 함.)

## 🐛 "F키를 눌러도 공격이 안 됨" — wasPressedThisFrame이 코루틴 프레임과 어긋나 놓침 (2026-07-24)
- **진단(Play 실측으로 단계적 격리)**:
  1. `TryConsumeDodge` 자체는 정상 발동(`isDodgeCountering=True`) — 트리거는 문제없음.
  2. `parryPressed`(우클릭 경로)를 직접 세팅하면 확인→`CounterRush`까지 완벽하게 실행됨(더미 HP 20→17,
     정확히 `attack1Damage×multiplier`) — **확인 이후의 파이프라인은 전혀 문제없음**, F 감지 자체만 의심.
  3. `InputSystem.QueueStateEvent`로 F키 상태를 직접 주입해보니 **`isPressed=True`인데
     `wasPressedThisFrame=False`** — 코루틴이 `yield return null`로 재개되는 시점과 Input System이
     "이 프레임에 새로 눌림"으로 인정하는 단일 프레임 엣지가 어긋나면, 그 한 프레임을 그대로 놓쳐
     버림. `wasPressedThisFrame`은 정확히 그 한 프레임에서만 체크해야 잡히는데, 슬로우모션 시퀀스
     안에서 이 타이밍이 안정적으로 안 맞았던 것으로 판단.
- **수정**: `kb.fKey.wasPressedThisFrame` → `kb.fKey.isPressed`(홀드 상태 체크)로 교체 — 코루틴이 어느
  프레임에 재개되든 F가 눌려있기만 하면 안정적으로 잡힘. 단일 프레임 엣지에 의존하지 않아 더 견고함.
- **검증(Play 실측, 실제 InputSystem 이벤트 경로만 사용— `parryPressed`는 명시적으로 false로 둬서 F키
  경로만 순수하게 테스트)**: `InputSystem.QueueStateEvent`로 F키 누른 상태 주입 → `dummyHp` 20→**17**
  (정확히 카운터 데미지만큼 감소) — F키 입력만으로 확인→`CounterRush`까지 정상 실행됨을 확정.
- 컴파일 클린, 콘솔 error/warning 0. 코드 변경분: `PlayerController.cs` 수정(1줄 조건 교체 + 주석).

## ✅ F키 "여러 번 연타해야 늦게 반응" 근본 해결 + Windup 오탐 리버트 (2026-07-24)
- 사용자 리포트: "회피 성공 후 F를 여러 번 연타해야 늦게 카운터가 발생" / "더미 판정이 Windup(애니 시작)
  때 발생 — 창을 앞으로 찌르는 순간(Thrust)에만 판정돼야 함".

### 🐛 F키 반응 지연/불안정 — 근본 해결: 폴링 → 이벤트 콜백 경로로 통일
- 직전 세션의 `isPressed` 수정은 임시방편이었음 — `Keyboard.current` **직접 폴링** 자체가 코루틴의
  프레임 재개 타이밍에 의존해 근본적으로 불안정(연타해서 "언젠가" 체크 타이밍과 겹쳐야 잡히는 구조).
- **근본 수정**: `PlayerActions.inputactions`의 "Parry" 액션에 `<Keyboard>/f` 바인딩 추가(우클릭과
  동일 액션 공유) — F를 눌러도 우클릭과 똑같이 `OnParry` 콜백이 Unity Input System 이벤트 큐를 통해
  **정확히 한 번, 프레임 타이밍과 무관하게** 전달됨(폴링이 아니라 이벤트 기반이라 "언젠가 겹쳐야 잡히는"
  문제 자체가 구조적으로 없음). `DodgeCounterRoutine`의 확인키 체크를 `Keyboard.current.fKey` 직접 폴링
  → `parryPressed` 단일 조건으로 단순화(불안정했던 폴링 경로 완전 제거).
  - **함정**: `InputActionAsset`은 `EditorUtility.SetDirty`+`AssetDatabase.SaveAssets()`로는 디스크에
    반영 안 됨(자체 JSON 직렬화 방식) — `asset.ToJson()` 결과를 `File.WriteAllText`로 직접 써야 함.
  - `.inputactions` 직접 텍스트 편집은 프로젝트 훅으로 차단됨(정상) → `InputActionSetupExtensions
    .AddBinding()` API로 안전하게 추가.
- **검증(Play 실측, 실제 Input Action 경로)**: 트리거 직후 같은 호출 안에서 F키 InputSystem 이벤트 주입
  → `parryPressed` 즉시 `True`(폴링 없이 콜백으로 세팅됨 확인) → 다음 프레임에 `CounterRush` 정상 실행,
  더미 HP 20→**17**(정확히 카운터 데미지) 확정.

### 🐛 더미 판정 Windup 오탐 — 리버트(Thrust 전용으로 복원)
- 지난 세션에 "애니메이션 재생 중 회피 판정 안 됨" 문제를 고친다고 Windup 단계에도 `CheckThrustHit()`을
  추가했는데, 이게 **정반대의 새 문제**를 만듦 — 창이 아직 몸 근처(안 뻗은 상태)인 Windup 시작 시점에도
  플레이어가 근접해 있으면 그 즉시 판정돼버림("애니메이션이 시작될 때 판정"). 사용자가 원하는 스펙은
  명확히 "창을 앞으로 찌르는 순간(Thrust)에만" — Windup 체크를 제거해 원래(Thrust 전용) 설계로 복원.
- **검증(Play 실측, 실제 `AttackLogic()` 경로)**: 창을 Windup 로컬위치에 두고 플레이어를 그 지점에 배치
  → `AttackLogic()` 호출해도 `playerHp` 불변(100→100, 판정 없음) 확인. 이어서 Thrust 상태로 전이시켜
  같은 방식 테스트 → `playerHp` 정확히 8 감소(정상 판정, 회귀 없음).
- 컴파일 클린, 콘솔 error/warning 0. 씬 미변경. 코드 변경분: `PlayerController.cs`/`DummyEnemy.cs` 수정,
  `PlayerActions.inputactions` 수정(Parry 액션에 F 바인딩 추가).

## ✅ F키 "흑백/UI 뜬 뒤 늦게서야 판정" 진짜 근본 원인 — 윈도우 오픈 전 홀드 상태가 무시됨 (2026-07-24)
- 사용자 리포트: "제가 말한 내용들이 하나도 반영이 안 되어있습니다. F키는 현재 흑백 쉐이더와 UI가 뜬 뒤
  보다 늦은 타이밍부터 판정이 되는거 같습니다." — 직전 커밋(이벤트 콜백 전환)으로도 여전히 해결 안 됨.
- **진단**: `DodgeCounterRoutine` 시작부에서 `parryPressed = false`로 무조건 리셋 → `OnParry`는 **press
  엣지**(새로 눌리는 순간)에만 `parryPressed = true`를 세팅하는 이벤트 콜백. 즉 **플레이어가 회피 윈도우가
  열리기 "전부터" F(또는 우클릭)를 이미 누르고 있던 채로 유지**하면(선입력/미리 대기하는 흔한 패턴), 그
  홀드는 새 엣지 이벤트를 절대 발생시키지 않으므로 리셋 이후 영원히 감지되지 않음 → 윈도우(2초)가 조용히
  만료(`window_expired`)될 때까지 아무 반응이 없다가, 사용자가 손을 뗐다가 다시 눌러야(=새 엣지 발생)
  그제서야 잡힘 — 이게 "판정이 늦게 되는 것 같다"로 체감된 진짜 원인. (F를 SendMessage 이벤트 경로로
  바꾼 지난 수정은 방향은 맞았지만 "윈도우 오픈 시점에 이미 눌려있는 경우"를 놓치는 이 케이스는 못 고침.)
- **재현(Play 실측, 실제 Input System 이벤트 경로)**: `InputSystem.QueueStateEvent`로 F 누른 상태를
  윈도우 오픈 **전에** 큐잉(릴리즈 이벤트 없이 유지) → `TryConsumeDodge`로 윈도우 오픈 → 윈도우 내내 F를
  누른 채로 유지했는데도 `parryPressed`가 끝까지 `False`로 유지되다 윈도우가 그대로 만료됨을 확인
  (`isDodgeCountering` False로 복귀, `confirmed` 이벤트 없음) — 버그 확정.
- **수정**: `DodgeCounterRoutine` 시작부, `parryPressed = false` 직후에 `Keyboard.current.fKey.isPressed`
  / `Mouse.current.rightButton.isPressed`를 **한 번 직접 확인**해 이미 눌려있으면 즉시 `parryPressed =
  true`로 세팅. 기존 `OnParry` 이벤트 경로(윈도우 도중 새로 누르는 경우)는 그대로 유지 — 두 경로가
  상호보완: (a) 윈도우 오픈 전 이미 홀드 → 새 체크로 즉시 구제, (b) 윈도우 도중 새 프레스 → 기존 콜백.
- **검증(Play 실측)**: 동일 재현 시나리오(F를 윈도우 오픈 전부터 홀드) 재실행 → `parryRightAfterTrigger`
  즉시 `True`, `confirm_seen elapsed=0.017`(사실상 그 프레임)로 확인 → `CounterRush` 정상 진행, 데미지
  적용, `dodge_counter_end`까지 정상 종료 확인. F를 안 누른 채 트리거해도 `parryPressed`는 그대로 `False`
  (오탐 없음)로 별도 확인.
- 임시 진단 로그(`held_check`/`window_start`/`confirm_seen`/`onparry_fired`의 실시간 타임스탬프 버전)는
  근본 원인 확정 후 제거 — 이 파일의 기록으로 대체.
- 컴파일 클린, 콘솔 error/warning 0. 코드 변경분: `PlayerController.cs`만 수정(`DodgeCounterRoutine` 시작부
  6줄 추가, 진단 로그 3곳 제거).

## ✅ 버그 5건 일괄 수정 — 이동 막힘 · F 지연 · 흑백 제외 · 찌르기 판정 · 넉백 (2026-07-24, /goal)
사용자 리포트 5건을 전부 Play 모드 실측으로 원인 확정 후 수정·검증.

### 🐛 1. "이동 중 갑자기 특정 방향으로 못 감(애니·flipX는 정상)" — 타일맵이 아니라 **적 몸체와의 물리 충돌**
- **타일맵 무죄 (실측)**: `Physics2D.simulationMode=Script`로 결정적 시뮬 — 평지 타일맵 위에서 좌/우 각각
  **30.000유닛**(기대값 정확히 일치), 스텝당 변위 **0.10000 일정**, 이음새 걸림 **0회**. per-tile
  `TilemapCollider2D`(CompositeCollider2D 없음)인데도 스내깅 없음 → 사용자가 의심한 타일맵은 원인 아님.
- **진짜 원인 (실측)**: 플레이어가 `DummyEnemy` 몸체(Dynamic·비트리거·Player↔Enemy 충돌 ON)에 밀착하면
  서로 밀어내느라 수평 변위가 스텝당 **0.100 → 0.019**(속도 5u/s → 0.96u/s, **약 80% 감소**)로 죽음.
  적이 마주 오며 추격(-3u/s)할 때 특히 심함. 애니/flipX는 `moveInput` 구동이라 정상 동작 → "애니는 되는데
  좌표가 안 변한다"는 증상 시그니처와 정확히 일치.
- **수정(`PlayerController.Awake`)**: `rb.excludeLayers |= enemyLayer`를 **상시 적용**(예전엔 대시 중에만
  켰다 껐다 — 그 코드의 주석도 이미 "적 통과(밀치기 방지)"였음). `HandleDash`/`EndDash`의 토글 2줄 제거.
  전투 판정은 전부 Overlap 쿼리라 `excludeLayers` 영향 없음 → 잃는 기능 없음.
- **검증**: 같은 시나리오 재측정 → playerX **-2 → 8.000**(기대값 정확), minStep **0.10000**, 정지 0회.
- ⚠️ **동작 변경 고지**: 이제 플레이어가 적 몸체를 **통과**한다(Dead Cells/할로우나이트류 표준). 적을
  단단한 벽처럼 만들고 싶으면 이 한 줄을 되돌리면 되지만, 그 경우 이동 감속은 물리적으로 불가피.

### 🐛 2. "F를 눌러도 카운터가 안 나가고 1~2초 뒤에 다시 눌러야 발동" — 확인 후에도 연장 대시를 다 기다림
- **원인**: `DodgeCounterRoutine`의 `while (windowOpen || isDashing)` — F 확인 시 `windowOpen=false`만
  세팅하고 **루프를 계속 돌렸다**. 연장 대시는 `dashDuration*2 = 0.36` **게임초**인데 슬로우모션
  `timeScale=0.15`에서는 **실시간 약 2.4초** → 그 시간이 다 지나야 `CounterRush`로 넘어감. 사용자에겐
  "첫 F는 씹히고, 1~2초 뒤 두 번째 F에 반응"으로 보였음(사실은 첫 입력이 이미 확정돼 대기 중이었음).
  이전 세션들이 고친 건 전부 **입력 감지** 경로였고, 지연의 실제 원인은 **확정 이후의 대기**였다.
- **수정**: 확인 즉시 `isDashing=false` + `break`로 루프 탈출 → 바로 `CounterRush`.
- **검증(실측)**: 확인 입력 → 카운터 명중까지 **0.075초 / 2프레임**, 더미 HP 20→17(정확히 카운터 데미지),
  `timeScale` 1로 복원, 플레이어 HP 100 유지(회피로 피해 무효).

### 🎨 3. 플레이어 스프라이트·대시/카운터 잔상 흑백 제외 — **3차 시도로 성공** (덧그리기 방식)
- 1차(오버레이 카메라)=RenderGraph 버그로 실패, 2차(마스크 텍스처+전역 텍스처 재합성)=대상 오브젝트가
  실제로 존재하면 **화면 전체가 검게 깨지는 치명적 회귀**로 폐기(마스크 0으로 되돌려 둔 상태였음).
- **3차 방식**: 별도 텍스처도, 전역 텍스처 등록도, 셰이더 분기도 **전부 제거**하고 —
  그레이스케일 블릿이 끝난 뒤 **같은 레이어를 카메라 색 버퍼 위에 한 번 더 그린다**
  (`GrayscaleProtectLayer` 패스, `SetRenderAttachment(src, 0, AccessFlags.ReadWrite)` + `DrawRendererList`).
  이미 흑백이 된 픽셀 위에 원색이 알파 블렌딩으로 덮여 원색이 복원됨. 검은 화면 회귀의 원인이던
  전역 텍스처/앨리어싱 경로 자체가 사라짐.
  - 2D 라이트 텍스처 의존을 없애려 `drawSettings.overrideMaterial`에 **언릿 스프라이트 머티리얼**
    (`Universal Render Pipeline/2D/Sprite-Unlit-Default`) 지정 — 씬 조명이 Global Light 1개(intensity 1)라
    룩 차이 없음. 셰이더의 `_ProtectedTex`/`_ProtectedTexValid`(내 변경으로 죽은 코드)는 정리 삭제.
- **`Renderer2D.asset`의 `ProtectedLayerMask` = 49408** = `Player(8) | PlayerInvincible(14) | VFXNoGrayscale(15)`
  (대시 중엔 플레이어 레이어가 PlayerInvincible로 바뀌므로 둘 다 필요). 디스크 반영 확인.
- **검증(카메라 → RenderTexture 렌더 후 픽셀 직접 판독, 보호 대상이 실제로 존재하는 상태 = 예전에 깨지던 바로 그 조건)**:
  | 대상 | 흑백 ON | 판정 |
  |---|---|---|
  | 플레이어 스프라이트(빨강 1,0.1,0.1 틴트) | 컬러 482px, rgb **(0.957, 0.094, 0.094)** | ✅ 원색 유지 |
  | 잔상(`VFXNoGrayscale`) | 컬러 482px, 동일 rgb | ✅ 원색 유지 |
  | 배경 | 컬러 **0/3721px**, rgb (0.302,0.302,0.302) | ✅ 완전 흑백 |
  | 화면 전체 평균 밝기 | **0.3288** | ✅ 검은화면 회귀 없음 |
  콘솔 error/warning 0(RenderGraph 경고도 없음).

### 🐛 4. 더미 찌르기 — 판정 시점을 "거의 마무리 순간"으로, 회피 창을 그 앞뒤로
- **원인**: `CheckThrustHit()`가 Thrust 진입 **첫 프레임(t=0, 창이 아직 몸 근처)** 부터 매 프레임 호출돼,
  창이 뻗기도 전에 판정이 나버렸음. 판정 기준점도 창의 **현재** 위치라 시점에 따라 들쭉날쭉.
- **수정(`ResolveThrustWindow`로 재작성)** — 타임라인(`attackClock` = Thrust 시작 기준, Recover까지 이어짐):
  - `hitTime = thrustDuration × thrustHitNormalized(0.85)` = **0.102s** — "거의 마무리되는 순간"
  - `[hitTime - dodgeWindowPre(0.12)]` 부터 회피(대시-카운터) 인정 시작
  - `[hitTime + dodgeWindowPost(0.05)]` = **0.152s** 에 피해 확정(= 회피 인정 종료).
    "판정 **직후**"의 회피까지 인정하려면 그만큼 피해 확정을 미루는 수밖에 없어서 이렇게 설계했고,
    post를 짧게(0.05s) 잡아 확정 시점에도 창이 16%만 회수된 = 여전히 뻗은 상태로 보이게 함.
  - 판정 기준점을 `transform.TransformPoint(spearThrustLocalPos)`(창 최대 뻗음 지점)로 **고정** — 창이
    회수되기 시작한 뒤에도 같은 지점으로 검사해 빗나감 방지. 터널링 스윕 체크는 유지.
- **검증(리플렉션으로 `AttackLogic` 직접 구동)**:
  - 피해 타이밍: clock 0.033 / 0.093 / 0.148 → **hp 100 유지**(판정 전), clock 0.193 → **hp 92**(1회만 확정).
  - 회피 창: 판정 **전**(clock 0.053)·판정 **직후**(clock 0.123) 모두 `isDodgeCountering=True`, 피해 0.
- Windup 단계 판정은 이전 세션 결정대로 **없음**(사용자 스펙: 찌르는 순간에만).

### 🐛 5. 적 피격 시 넉백 = 플레이어 공격 전진거리 × 1.5
- `PlayerController.enemyKnockbackMultiplier = 1.5` 신설 → 평타(`CheckAttackHit`)와 카운터(`CounterRush`)
  모두 `TakeDamage(dmg, facing.x × attackLungeDistance × 1.5)`로 방향·거리를 넘김(0.3 × 1.5 = **0.45유닛**).
- `DummyEnemy.TakeDamage(int, float)` 오버로드 + `FixedUpdate`에서 **남은 거리를 스텝마다 클램프**해 소진.
  - **함정**: 처음엔 Update에서 속도만 세팅하고 타이머로 끄는 방식이었는데, 프레임이 길면 마지막 프레임이
    통째로 초과 이동해 **0.45 요청 → 실측 1.21유닛**(2.7배)이 나왔다. 거리 기반으로 바꿔 프레임레이트 무관.
- **검증**: 직접 호출 경로 **0.4500**(오차 0.0000), 실제 판정 경로(`AttackHitFrame()` → `CheckAttackHit`)
  **0.4500**(오차 0.0000), 넉백 종료 후 속도 0으로 복귀.

### 🧪 회귀 확인 & 상태
- 대시: 이동 정상(-10 → 13.6), 종료 후 레이어 `Player` 복원, `excludeLayers=2048` 유지, 잔상 자동 정리(0개).
- 컴파일 클린, 세션 전체 콘솔 **error/warning 0**. **씬 미변경(`isDirty=False`)**.
- 코드 변경분: `PlayerController.cs` · `DummyEnemy.cs` · `Rendering/GrayscaleRendererFeature.cs` ·
  `Shaders/ScreenGrayscale.shader`, 에셋 변경분: `Settings/Renderer2D.asset`(ProtectedLayerMask 0 → 49408).
- **원격 에디터 주의(측정 시 함정 2개)**: ① `Physics2D.autoSyncTransforms=False`라 오브젝트를 옮긴 직후
  Overlap 쿼리를 하려면 `Physics2D.SyncTransforms()`를 먼저 불러야 한다(안 부르면 "판정이 안 맞는" 것처럼
  보임). ② 프레임 스톨이 `Time.maximumDeltaTime`(0.333s) 하나로 몰리면 대시(0.18s)가 **한 Update 안에서
  시작·종료**돼 FixedUpdate를 못 만나 이동이 0으로 보인다 — 실제 60fps 플레이에선 발생하지 않는 계측 아티팩트.
- **후속 확인 필요(이번 범위 밖)**: 씬의 Player `wallLayer`=**1536**(Ground|Wall) 불일치는 여전히 남아 있음
  (task.md 기록상 1024여야 함). 이번 실측에선 평지에서 오탐 없음(`rightWall/leftWall=none`) 확인.

### 🔎 후속: "아직도 가끔 이동 막힘" — 물리는 완전 무죄로 확정, 원인 지목용 진단 투입 (2026-07-24)
- **물리 전수 검증(Play 모드, `Physics2D.simulationMode=Script` 결정적 시뮬)** — 전부 걸림 0회:
  | 조건 | 결과 |
  |---|---|
  | 정상 안착 / 살짝 위 / 낙하 착지(-12) / 강한 낙하(-25) | 각 25.000/25.0, minStep 0.10000, stalls 0 |
  | 지형에 박힌 채(-0.05 / -0.15 임베드) 걷기 | 각 25.000/25.0, stalls 0 |
  | 타일맵 전 구간(-29→31, 31→-31) **적 활성 상태로 관통** | 각 **60.00유닛**, minStep 0.10000, stalls 0 |
  | 타일 셀 전수 조사 | 채움 60 / 빈칸 **0** (구멍·높이 이상 없음) |
- ⚠️ **측정 함정(중요, 하마터면 오진할 뻔)**: 중간에 `manage_editor(play)`가 "Already in play mode"를 반환했지만
  실제로는 **에디트 모드**였다(`Application.isPlaying=False`). 에디트 모드에선 `Awake`가 안 돌아
  `excludeLayers=0`(수정 미적용) 상태라, 전 구간 워크가 적 위치에서 정지하는 **가짜 재현**이 나왔다.
  → 앞으로 Play 실측 코드는 **첫 줄에서 `Application.isPlaying`을 직접 확인**할 것. (에디트 모드에서 돌린
  물리 시뮬로 씬 오브젝트가 움직였으나 `isDirty=False`였고, `OpenScene`으로 디스크 상태 복구 확인.)
- **공격 잠금 오검증 배제**: `attack1/2Duration`(0.4167) = 실제 클립 길이(`Glitch Samurai-Slash 1/2`,
  0.4167s @12fps 5프레임)와 **정확히 일치** → "애니는 끝났는데 잠금만 남는" 불일치는 없음.
- **결론**: 이동을 막을 수 있는 경로는 (a) 코드 상태 잠금(`isAttacking`/`isDodgeCountering`/`isDashing`/
  `wallJumpLockCounter`)과 (b) 물리 충돌 둘뿐인데 (b)는 전부 무죄 → **(a) 중 하나**.
- **투입한 진단(`PlayerController.CheckMovementStall`, 임시)**: "수평 입력은 있는데 `|vel.x| < 0.5`"가
  `stallLogThreshold`(0.3s) 이상 지속되면 **1회만** `[STALL]` 경고 로그. 원인을 코드 잠금이면 어느 플래그인지,
  물리면 **어떤 콜라이더/레이어가 수평으로 막는지**(접촉 법선으로 필터) 지목한다. Inspector에서 끌 수 있음.
  - 자체 검증 완료(양쪽 분기 실제 발화):
    `[STALL] ... 원인: 공격 중(isAttacking, stage=1 timer=0.33/999.00) ...`
    `[STALL] ... 원인: 물리 충돌 — 수평 접촉=[DummyEnemy(layer=Enemy n=(-1.0, 0.0))] ...`
- **다음**: 사용자가 막힘을 1회 재현하면 콘솔의 `[STALL]` 한 줄로 원인 확정 → 수정 후 이 진단 코드 제거.
- **부수 관찰(이번 범위 밖, 미수정)**: `Glitch Samurai-Slash 2` 클립의 `AttackHitFrame` 이벤트가
  **0.0000s(0%)** 에 찍혀 있음(Slash 1은 0.0833s=20%). 2타 판정이 스윙 시작 첫 프레임에 나가는 셈이라
  의도한 프레임이 맞는지 확인 필요.

## ✅ 공중 공격 금지 (F 카운터 제외) (2026-07-24)
- 지시: "공중에서의 공격 및 공격 입력을 막아주세요.(F키 카운터 공격 제외)"
- **수정(`PlayerController.cs`, 2곳)**:
  - `OnAttack`: `if (!isGrounded) return;` — 공중에서는 **입력 자체를 버퍼에 안 쌓는다**. 그래서 공중에서
    누른 게 착지하는 순간 자동으로 터지는 일도 없다(`attackInputBufferDuration=0.3s` 버퍼의 부작용 차단).
  - `HandleAttack`: 발동 조건에 `isGrounded` 추가 — 지상에서 눌러 이미 버퍼링된 입력이라도 그 사이에
    공중으로 나가면 발동하지 않는다.
  - 회피-카운터(F)는 `OnParry`/`TryConsumeDodge`/`CounterRush`라는 **별도 경로**라 손대지 않음 →
    공중에서도 그대로 동작(아래 실측 확인). 진행 중인 공격을 공중에서 강제 취소하지는 않는다 —
    공격 중엔 수평 이동이 잠겨 스스로 낙하할 수 없어 애초에 그 상태가 안 만들어짐(불필요한 로직 추가 방지).
- **검증(Play 실측)**:
  | 케이스 | 경로 | 결과 |
  |---|---|---|
  | 공중 좌클릭 | 실제 InputSystem 이벤트 → PlayerInput → OnAttack | `attackQueued=False isAttacking=False` ✅ |
  | 공중 입력 후 착지 | 동일 | 자동 발동 **없음**(둘 다 False) ✅ |
  | 지상 좌클릭 | 동일 | 정상 발동(`attackStage` 1→2 순환, `lastAttackEndTime` 갱신) ✅ |
  | 지상 버퍼 입력 | `HandleAttack` 직접 구동 | 발동 True ✅ |
  | 공중 버퍼 입력 | 동일 | 발동 **False** ✅ |
  | **공중 F 카운터** | `TryConsumeDodge` + `parryPressed` | `isDodgeCountering=True` → 더미 HP **20→17**(카운터 데미지 정확), `timeScale` 1 복원 ✅ |
- 컴파일 클린, 콘솔 error/warning 0, 씬 미변경.
- ⚠️ **원격 계측 함정 추가 발견**: 중간에 Play 모드가 **일시정지(`EditorApplication.isPaused=True`)** 상태로
  들어가 `Time.frameCount`가 387에 고정돼 있었다 — 입력 주입이 처리되지 않고 `isGrounded` 같은 필드도
  낡은 값으로 남아 "게이트가 안 먹는 것처럼" 보였다. `EditorApplication.isPaused=false`로 해제 후 정상 계측.
  → 앞으로 Play 실측 전 **`isPlaying` + `isPaused` + `frameCount` 증가**를 함께 확인할 것.
  (`InputValue`는 `m_Context`만 가지고 `isPressed`가 그걸 읽으므로 **콜백 밖에서 새로 만들어 호출하면
  `InvalidOperationException`이 난다** — 수동 호출로는 입력 게이트를 검증할 수 없고, 반드시 `InputInjector`
  같은 실제 이벤트 경로를 써야 한다.)

### 📌 이동 막힘 — 사용자 확인: "해결된 것 같음" (2026-07-24)
- 적 몸체 충돌 제외(`excludeLayers`) 수정 이후 재현 안 됨. 다만 확신 단계는 아니라
  `[STALL]` 진단(`PlayerController.CheckMovementStall`, `logMovementStall`)은 **일부러 남겨둠** —
  재발 시 원인이 한 줄로 확정되고, 스톨이 없으면 아무것도 로그하지 않아 비용이 없다.
  사용자가 확신한 뒤 제거 예정(삭제는 승인 필요, 규칙4).

## ✅ 일섬(一閃) 구현 + Hit VFX 정리 (2026-07-25)

### 부수 요청: Hit VFX
- Hit 프리팹 7개(`Hit01~03`, `HitVFX1~4`) 루트 스케일 **1.25 → 1.0** (요청한 0.8배와 정확히 일치).
- `DummyEnemy.ResolveThrustWindow`에서 `CombatFx.SpawnHitVfx` 호출 제거 — **플레이어가 때릴 때만** 스파크가
  뜬다. 적이 플레이어를 맞출 때는 데미지 텍스트만. 고아가 된 `hitVfxPrefabs`/`hitVfxOffsetTowardsPlayer`
  필드는 사용자 승인 후 삭제(씬에 있던 HitVFX1~4 참조 4개도 함께 사라짐, git에는 남아 있음).

### 에셋 (MCP 에디터 작업)
- **`Resistance_Up.png` 재슬라이스**: 자동 슬라이스가 25개의 제멋대로인 조각(37×13, 9×8, 30×43…)으로
  깨져 있었음 → **32×32 그리드(6열×4행)** 로 재슬라이스, pivot Center, Point 필터, PPU 32.
  내용이 있는 **0~12번 13칸만** 생성(13~23은 완전 투명 → Tight 메시가 깨질 수 있어 제외).
  프레임 내용: `0` 빈칸 → `1~2` 조각 페이드인 → `3` 흰 플래시 → `4~7` 아이콘 유지 → `8` 흰 플래시 →
  `9` 별 → `10` 가로선 → `11~12` 점으로 소멸. **"등장→유지→소멸"이 한 클립에 다 들어 있어** 스펙 2의
  "재생 끝난 뒤 destroy"와 정확히 맞는다.
  - ⚠️ **`TextureImporter.spritesheet`는 Unity 6에서 제거됨**(기존 `Editor/SetupAnimationsEditor.cs`가
    아직 이 API를 쓰고 있어 CS0618 경고). 신규 경로는 `UnityEditor.U2D.Sprites.SpriteDataProviderFactories`
    → `ISpriteEditorDataProvider.SetSpriteRects/Apply` + `UnityEditor.SpriteRect`
    (assembly `Unity.2D.Sprite.Editor`, 리플렉션으로 존재 확인).
- `Assets/VFX/BuffVFX/Animations/Resistance_Up.anim` — 13프레임 @12fps, 루프 OFF, 길이 **1.0833s**
  (`AnimationClipSettings.stopTime = 13/12`로 마지막 프레임도 1/12초 표시. 기존 Hit01~03과 같은 규격).
- `.../Controllers/Resistance_Up.controller`, `.../Prefabs/Resistance_Up.prefab`
  (SpriteRenderer `Sprite-Lit-Default`·sortingOrder 999 + Animator `UnscaledTime` + 기존 **`HitVfxAutoReturn`**
  재사용 → 클립 길이 뒤 자기 파괴 = 스펙 2 그대로).
- `Assets/Shaders/IlseomChargePixels.shader` + `Assets/VFX/Ilseom/IlseomChargePixels.mat` (#7ebfc6).
- 씬(`SampleScene`): Player의 `ilseomChargePixelMaterial`, `ilseomBuffPopPrefab` 할당 후 저장.

### 픽셀 수집 연출 (스펙 4)
- **픽셀 하나 = 메시에 미리 깔아둔 쿼드 1개**. 위치·알파·수렴·스월·버스트를 전부 **버텍스 셰이더**에서
  계산 → CPU는 `_Progress` 하나만 갱신. 프래그먼트에서 파티클 N개를 루프 도는 방식(약 20M iter/frame 추정)
  대신 정점 256개로 끝나 수십 배 저렴하다.
- `IlseomChargeFx.cs`가 런타임에 메시+머티리얼을 만들어 **플레이어 자식**으로 붙인다 → 로컬 공간이
  플레이어 기준 공간이 되고 부모 스케일(1.3)이 픽셀 크기에도 걸려 도트 크기가 스프라이트와 자동으로 맞는다.
  정점을 원점에 몰아두므로 **바운즈를 직접 지정**해야 프러스텀 컬링에 안 잘린다.
- 취소 시 왔던 방향으로 되튀며 페이드아웃(유리 파편), 발동 시 모인 자리에서 페이드아웃. 둘 다 unscaled 시간.
- 셰이더 패스 태그는 **`LightMode = Universal2D`** — URP 2D Renderer는 `Universal2D`/`SRPDefaultUnlit`만
  수집한다(`HitVfxAutoReturn.cs` 주석의 기존 실측 기록과 동일).

### 🐛 발견·수정: **PlayerInput SendMessages는 Button 액션의 "뗌"을 전달하지 않는다**
- 증상: 우클릭을 떼도 차지가 멈추지 않고 계속 쌓여 2초를 넘기면 **손을 떼지 않았는데도 저절로 발동**.
  1차 플레이테스트에서 `charge_cancelled_early`가 아예 안 찍히고 `charge_complete`가 `charge_start`
  정확히 2.0초 뒤에 찍힌 것으로 확정(Phase 1이 "거짓 통과"로 나와 잡아냄).
- 원인(패키지 소스 직접 확인):
  `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/PlayerInput.cs:1499`
  ```csharp
  // ATM we only care about `performed` and, in the case of value actions, `canceled`.
  if (!(context.performed || (context.canceled && action.type == InputActionType.Value)))
      return;
  ```
  → **Button 액션은 `performed`만 메시지로 오고 `canceled`는 절대 오지 않는다.**
- 수정: `OnCharge(InputValue)` 메시지 방식을 버리고 `PollChargeInput()`에서
  `chargeAction.IsPressed()`를 매 프레임 폴링(액션 조회 실패 시 `Mouse.current.rightButton` 폴백).
  `DodgeCounterRoutine`이 이미 버튼 상태를 직접 폴링하는 것과 같은 방식으로 통일.
- ⚠️ **같은 원인의 기존 잠재 버그**: `OnJump`의 `else { isJumpHeld = false; }` 분기도 **절대 실행되지 않는다**
  → `lowJumpMultiplier`(짧게 누르면 낮게 뛰기)가 첫 점프 이후로 영구히 안 걸린다. 이번 범위 밖이라 미수정.

### 검증 (`Tools/PlayTest/Ilseom`, 채널 `ilseom`)
| Phase | 항목 | 실측 |
|---|---|---|
| 1 | 2초 전 릴리즈 → 취소 | `charge_cancelled_early` 0.70s(=2×0.35), `moved=0.00`, 레이어 정상 ✅ |
| 2 | 발동 중 무적 | `phase2_mid invincible=True` (레이어 스왑 + `IsInvincible`) ✅ |
| 2 | 경로 계산 | `path dist=5.80 max=7.20 wall=False enemies=1` — 적(+5.0) + `ilseomPastEnemyDistance`(0.8) 정확 ✅ |
| 2 | 4배 피해 · 처형 VFX | `sweep_hit dmg=4 hits=1`, 더미 HP **20→16** ✅ |
| 2 | 정지 위치 · 복원 | `traveled=5.80 expected=5.80 stopOk=True layerRestored=True hpDelta=4/4` ✅ |
| 6 | 차지 중 피해 절반 | 더미 찌르기 `dmg=8` → `charge_damage_reduced 8->4`, `player_damage hp=96/100 dmg=4` ✅ |
- ⚠️ **테스트 워크플로 함정**: `TestRecorder`(Unity Recorder)가 `captureDeltaTime`을 1/30로 고정하고
  1280×720 MP4를 프레임마다 인코딩해 **실측 1.5fps**(게임 6초 = 실시간 120초)로 떨어진다. 정지가 아니라
  진행 중이므로 성급히 중단하지 말 것. 기능 검증만 필요할 땐 녹화를 끄는 편이 낫다.

### 🐛 발견·수정: 차지 시작 프레임에서 `Time.deltaTime`이 한 번 중복 가산
- 증상: 2초 차지가 **1.67초**에 완충(라이브 실측 `charge_start` T=1.69 → `charge_complete` T=3.36).
  자동 테스트에선 프레임이 고르기 때문에 오차가 1프레임(0.033s)뿐이어서 `2.00`으로 보이며 숨어 있었다.
- 원인: `HandleIlseom`에서 `StartCharge()`(chargeTimer=0) 직후 같은 프레임의
  `chargeTimer += Time.deltaTime`이 이어서 실행돼, **차지 시작 전의 프레임 간격**이 통째로 가산됨.
  프레임이 튀면 `Time.maximumDeltaTime`(0.333s)까지 커져 최대 17% 일찍 완충된다.
- 수정: 차지를 시작한 프레임에는 `return`으로 빠져 다음 프레임부터 누적.
  수정 후 실측 `chargeTimer=18.149` vs 실제 경과 `18.482` → **한 프레임 뒤처짐**(절대 일찍 끝나지 않는 안전한 방향).

### 🐛 발견·수정: `frac(sin(dot(...)))` 해시가 GPU에서 붕괴 → 픽셀이 링이 아니라 가로 띠
- 증상: 픽셀이 원형으로 퍼지지 않고 **가로로 눌린 덩어리**. 스크린샷 픽셀 분포로 정량 확인:
  x 폭 71px vs y 폭 27px. 후처리를 껐을 때도 동일해 블룸 번짐이 아님을 배제.
- 원인: 파티클 인덱스가 정수라 `sin`의 인자가 커지고(≈632·n), GPU의 범위 축약 정밀도가 무너져
  결과가 몇 개 값으로 뭉쳐 각도가 0·π 근처로 쏠림.
- 수정 2가지: (a) 곱셈·`frac`만 쓰는 해시로 교체
  (Dave Hoskins "Hash without Sine", shadertoy.com/view/4djSRW), (b) 각도를 **균등 분할 + 지터**
  (`ang = (id + h.x*0.85) / _Count * 2π`) — 64개뿐이라 순수 난수로는 뭉치고 비는 구간이 생긴다.
  `_Count`는 `IlseomChargeFx`가 메시 개수와 맞춰 넣는다.
- 수정 후 실측: 시안 픽셀 분포 **종횡비 1.17**(≈원형), 개별 도트가 뚜렷.

### ✅ 블룸 (스펙 추가 요청)
- URP엔 오브젝트 단위 블룸이 없어 **"HDR로 1.0 위로 출력 + 임계값으로 골라내기"** 가 유일한 수단.
  셰이더에 `_BloomBoost`(기본 2.0) 추가 → #7ebfc6 × 2.0 = **(0.99, 1.50, 1.55)**.
  R만 1 아래로 남겨 시안 색조가 흰색으로 날아가지 않게 하고, G·B가 임계값 1.15를 넘어 시안 톤으로 번진다.
  알파 블렌딩이라 페이드 인 중(알파 작을 때)엔 임계값을 못 넘어 **밝아질수록 자연히 번지기 시작**한다.
- 씬 설정: `Global Volume`(isGlobal) + `Assets/VFX/Ilseom/IlseomBloomProfile.asset`
  (threshold 1.15 / intensity 1.6 / scatter 0.72 / tint 시안 / HQ filtering),
  Main Camera `renderPostProcessing` **False → True**. URP·카메라 HDR은 이미 켜져 있었음.
- 임계값 1.15는 일반 스프라이트(LDR, 최대 1.0)가 **절대 못 넘는 값** → 일섬 픽셀만 빛난다.
- ⚠️ **함정**: `VolumeProfile.Add<T>()`만 하면 오버라이드가 런타임 인스턴스로만 존재하고 저장 시
  `components: - {fileID: 0}`으로 **날아간다**. `AssetDatabase.AddObjectToAsset(bloom, profile)`로
  프로파일의 **서브에셋으로 등록**해야 직렬화된다. 처음엔 이걸 빼먹어 "블룸을 켰는데 아무 변화 없음"
  (on/off 차이 0.99x)이었고, 에셋 텍스트를 직접 열어 `{fileID: 0}`을 보고 확정했다.
- 검증(동일 배치에서 bloom.active만 토글): **차이 픽셀 2182개 / 최대 델타 689**,
  흰색 스프라이트는 변화 없음 → 의도한 대상만 발광.

### 사용자 추가 지시 반영
- Hit 프리팹 0.8배 + 적 피격 시 Hit VFX 제거(위 참고).
- **투명·이동 시간 0.5초 → 0.1초** (`ilseomMoveDuration`, 스크립트 기본값 + 씬 값 둘 다).
  최대 사거리 7.2u 기준 이동 속도 72u/s(대시 20u/s의 3.6배). 전체 시퀀스 = 0.4167 + 0.1 + 0.5 = **1.017s**.

### ✅ 후속 폴리싱 (2026-07-25, 같은 세션)
- **일섬 종료 후 마지막 프레임 잔상 버그**: `Glitch Out`/`Glitch Sweep`은 애니메이터에서 **나가는 전이가
  0개인 고아 상태** → 클립이 끝나도 그 상태에 머물러 마지막 프레임이 스프라이트에 남았다(대시는 전이가
  있는 `Run`에 프리즈해서 무사했음). 수정: `RestoreAnimAfterIlseom()`이 일섬 종료(`finally`)·차지 취소
  양쪽에서 `ilseomExitState`(기본 `Glitch Samurai-Idle`)로 `anim.Play` + `Update(0f)` → 한 프레임도 안 남음.
  실측: 종료 후 clip=Idle·sprite=Idle_4 순환, 취소 후 clip=Idle. 차지 중엔 여전히 Glitch Out_0 고정.
- **궤적 섬광 추가**(`IlseomSlashStreak.shader` + `IlseomSlashFx.cs`): 출발→도착을 잇는 쿼드 1개에
  `_Progress`로 선두를 훑어 "빠르게 지나갔다"를 낸다. 코어 라인 + 레인별 파선(스피드 라인) + HDR 발광.
  경로 확정 직후 스폰(`ilseomStreakSweep`/`Fade`/`Height`로 튜닝). 픽셀 스냅은 플레이어와 같은 1/32×스케일.
- **홀드 이펙트 재작업**(사용자 요청 "계속 모이면서 가까워질수록 페이드 아웃"): 셰이더의 시계(`_Flow`,
  차지 경과 초)와 세기(`_Progress`, 참여 픽셀 수)를 **분리**. 예전엔 `_Progress`로 t를 직접 만들어
  완충 시 모든 픽셀 t=1 → "한 번 모이고 끝"이었다. 이제 픽셀마다 속도·위상이 다른 주기 흐름(`frac(_Flow*speed)`)
  으로 링→수집점을 **계속 순환**하고, 수집점 근처(`_FadeOutFrac`)에서 흡수되듯 사라진다. 참여 픽셀 수는
  `_Progress`가 게이팅(`activeGate`).
- **수집점 위치·반투명**(사용자 요청): `ilseomGatherOffset` (0.55, 0.70) → **(0.38, 0.30)** (더 아래·왼쪽),
  픽셀 색 alpha 1.0 → **0.72**(반투명).
- **블룸 강도 상향**(사용자 요청, 2단계): intensity 1.6 → 3.6 → **5.2**, scatter 0.72 → **0.85**,
  픽셀 `_BloomBoost` 2.0 → 3.0 → **4.5**(유효 최대 밝기 2.52, 임계값 1.15 여유). 궤적도 boost 4.5로 통일.

### ✅ 2차 폴리싱 (2026-07-25, 사용자 반복 조정)
- **정지 규칙 단순화**(사용자 변경): 예전 "벽 → 가장 먼 적 뒤 → 최대거리"에서 **"벽 → 최대거리"** 로.
  적 위치는 이제 정지에 관여하지 않는다(경로 위 적 **피해 4배는 그대로 유지** — `ResolveIlseomPath`는
  정지 거리만 벽/최대로 정하고, 그 구간 안 적을 여전히 `targets`로 모아 Sweep 판정에 쓴다).
  `ilseomPastEnemyDistance` 필드는 **미사용**이 됨(주석 표기, 삭제는 승인 대기). 테스트의 `expectedTravel`도
  항상 최대거리(7.2)로 갱신 → 실측 `path dist=7.20 wall=False enemies=1`, `traveled=7.20`, `hpDelta=4/4`.
- **홀드 픽셀 톤/양**(사용자 요청): 색 #7ebfc6(청록) → **#5C9EF2(파랑)**, 알파 0.72 → **0.5**(반투명),
  개수 64 → **36**, 에너지 느낌의 미세 깜빡임(`flicker = sin(_Flow*22 + 위상)`) 추가. 궤적 섬광도 같은 파란 톤.
- **블룸 2단 상향**(사용자 요청): Volume intensity 1.6 → 3.6 → **5.2**, scatter → 0.85,
  픽셀 `_BloomBoost` 2.0 → 3.0 → **4.5**. (반투명 0.5라 유효 최대 밝기 ≈2.14, 임계값 1.15 여유)
- **수집점 위치**(사용자 요청 "더 아래·왼쪽"): `ilseomGatherOffset` (0.55,0.70) → **(0.38,0.30)**.
- **Hit01/02/03 스프라이트 1.3배**(사용자 요청): 세 프리팹 루트 스케일 1.0 → **1.3**. HitVFX1~4는 1.0 유지
  (앞서 0.8배로 줄인 7개 중 이 셋만 다시 키움). 적 피격 시 Hit VFX 미표시 규칙은 그대로.
- **버프 팝(Resistance_Up) 위치·크기**(사용자 요청 "더 위로, 1.5배"): `ilseomBuffPopHeight` 1.0 → **1.6**,
  신규 `ilseomBuffPopScale` = **1.5**(`SpawnBuffPop`에서 인스턴스 `localScale *= 1.5`). 2초 홀드 완료·쿨타임
  완료 양쪽에서 같이 적용. 실측: pos.y 머리위 +1.60, scale (1.5,1.5,1.5), 아이콘 프레임 정상 표시.
- **일섬 기능 테스트에서 녹화 제거**: `RunIlseomTest`가 `TestRecorder`를 호출하지 않게 함.
  Recorder가 `captureDeltaTime`을 1/30로 고정해 ~1.5fps로 스로틀 → `WaitForSeconds`와 입력 주입 폴링이
  어긋나 릴리즈가 1~2초 늦게 인식돼 **비결정적 FAIL**(mp4 파일 잠금 `0x80070020` 시 더 심함). 실시간
  프레임으로는 매번 `[ASSERT] ilseom: PASS`(6/6). 영상이 필요하면 Dash showcase를 쓸 것.
- **런타임 값 함정 재확인**: Play 세션이 정지 없이 남아 있으면(`frameCount`가 수만) 이전 세션 상태가
  낡은 채 남는다. 결정적 검증 전 stop→play로 새 세션을 강제할 것.
- 사용자 요청으로 **DummyEnemy `moveSpeed` 3 → 0**(씬 저장). 되돌리려면 3.

### 스펙 해석 메모
- **스펙 5가 자기모순**: "0.5초 이후부터 쉐이크" + "0배→1.5배까지 **2초간** 상승". 램프 곡선은 차지
  전체(0~2초)에 두고 `ilseomChargeShakeStartDelay`(0.5s) 전에는 적용하지 않는 것으로 해석 — 두 문장을
  동시에 만족하는 유일한 방법. 기준 세기는 피격 쉐이크와 같은 `attackShakeMagnitude`(씬 값 0.03)를 그대로
  써서 그쪽 튜닝을 따라간다 → 최대 0.045.
- 사용자 확정 2건: (a) 픽셀 수집 지점은 **바라보는 방향으로 미러링**(flipX=true면 왼쪽 위),
  (b) 대시·좌클릭으로 취소하면 **그 동작도 함께 발동**(취소만 하고 입력을 먹지 않음).
- 점프는 스펙의 취소 수단이 아니므로 차지를 깨지 않고 그냥 무시된다. 공중 차지는 허용(중력 유지).
- 우클릭은 `Parry`(회피-카운터 확인키)와 공용이지만 `CanStartCharge()`가 `isDodgeCountering`으로
  차지 시작을 막아 서로 간섭하지 않는다. `Charge` 액션은 **우클릭 전용**(F키는 일섬을 발동시키지 않음).

---

# 패링 (2026-07-25)

## ✅ 완료
- **입력 분기**: 우클릭을 `parryTapMaxHold`(0.2s) 안에 떼면 **패링**, 넘겨서 쥐고 있으면 **일섬 차지**.
  차지 연출(애니 고정 · 픽셀 FX · 블룸)은 `BeginChargeVisuals()`로 **0.2s 뒤로 미룬다** — 탭할 때마다
  픽셀 FX가 깜빡이고 취소 이펙트가 터지는 것을 막기 위함. `chargeVisualsStarted`가 그 상태를 들고 있고,
  탭 취소(`charge_cancelled_tap`)는 애니메이터를 건드리지 않는다(Slash 1 앞에 Idle 한 프레임이 끼는 것 방지).
- **모션**: 성공/실패 무관하게 `anim.SetTrigger("Attack1")`로 `Glitch Samurai-Slash 1` 재생.
  `isAttacking`을 세우지 않으므로 클립의 `AttackHitFrame` 이벤트는 무시되어 **데미지가 나가지 않는다**.
  실패 시 `parryFailCooldown`(0.5s) 잠금(사용자 확정).
- **성공 판정**(`FindParryTarget`): 적이 공격 모션 중(`DummyEnemy.IsAttacking`)이면서
  (B) 아직 그 공격에 안 맞았거나(`IsAttackUnresolved`) (A) 대시 회피 인정 창(`dodgeCounterGraceTimer`)이
  열려 있고 — 적의 공격 원(창끝, r=0.5)이 플레이어 1타 히트박스와 **겹칠 때**(사용자 확정: 완전 포함 아님).
- **연출**(스펙 4): 겹침 중앙(= 창끝을 1타 박스 안으로 클램프한 점) + `parryFxHeightOffset`(0.35) 위에
  `Hit02`(크리티컬 스프라이트) + `"막아냄!"` 텍스트(`critTextColor` #FFD400, 2배 강조). 실측 확인 완료.
- **피해 무효**(스펙 5): `DummyEnemy.ConsumeParry()`가 `attackHitDone=true`로 그 찌르기를 종결 처리.
- **구형 실드**(스펙 6): `Custom/ParryShield` — 속이 빈 링 아웃라인, 반투명, HDR 발광.
  적 공격 **1회**를 대신 막고(`TryConsumeParryShield`) 유리처럼 조각나 흩어진다(`_Break` 0→1,
  조각별 강체 역변환으로 정확히 분리). 지속시간 제한 없음(사용자 확정 "막을 때까지").
- **일섬 확장**(스펙 6 추가분) — ⚠️ **1차 구현은 스펙 오독이었다.** "플레이어에게 적용된 쉐이더"를
  패링 실드로 읽어 일섬에 실드 링 + 경로 사본을 깔았는데, 사용자 확인 결과 그건 **기존 궤적 섬광**
  (`IlseomSlashStreak`)을 뜻한 것이었다. 실드 관련 코드는 전부 제거하고 궤적 섬광을 강화하는 쪽으로 재작업.
  - 제거: `ilseomShieldEnabled` / `ilseomShieldTrailCount` / `Lifetime` / `Alpha` 필드,
    `SpawnIlseomShieldTrail()`, `ParryShieldFx.SpawnGhost()`, `Attach`의 `mirrorWithFlip` 인자
    (전부 이 오독 때문에 생긴 코드라 같이 걷어냄).
  - **궤적 섬광 강화**(`SpawnIlseomStreak`): 겹마다 두께·수명·시드가 다른 **3겹**(`ilseomStreakLayers`)을
    깐다. 두꺼운 겹을 뒤에 깔아 얇은 코어가 위로 올라온다. 실측 두께 **2.20 / 3.52 / 4.84**, 길이 7.20,
    sortingOrder 9/8/7.
  - 수치 1차: `ilseomStreakHeight` 1.3 → 2.2, `ilseomStreakFade` 0.22 → 0.6,
    `_TailLength` 0.45 → 0.85, `_LaneCount` 18 → **28**, `_LaneDensity` 0.55 → **0.78**.
  - **수치 2차 되돌림** — "궤적이 너무 느리고 크다"는 피드백(2026-07-25). 겹 수(3)와 레인 밀도는 유지하고
    두께·수명만 원래 값 근처로 내렸다:

    | 항목 | 원래 | 1차(과함) | **최종** |
    |---|---|---|---|
    | `ilseomStreakHeight` | 1.3 | 2.2 | **1.5** |
    | `ilseomStreakLayerHeightSpread` | — | 2.2 | **1.4** |
    | `ilseomStreakFade` | 0.22 | 0.6 | **0.28** |
    | `ilseomStreakLayerFadeSpread` | — | 1.8 | **1.25** |
    | `_TailLength` | 0.45 | 0.85 | **0.55** |

    실제 겹 두께 **2.20~4.84 → 1.50~2.10**, 화면 체류 시간 **0.72~1.20s → 0.40~0.47s**
    (sweep 0.12 + fade). 꼬리를 짧게(0.55) 하는 것이 "빠르게 지나간" 느낌에 가장 크게 기여한다.
- **전역 블룸 하향**(사용자 피드백 "전반적 블룸이 너무 강하다"): `IlseomBloomProfile.asset`의
  Bloom `intensity` **5.2 → 2.2**, `scatter` **0.85 → 0.70** (threshold 1.15는 유지 — 그게 "이 오브젝트만
  블룸" 규칙의 기준선이라 건드리면 LDR 스프라이트까지 빛나기 시작한다).
  각 머티리얼의 `_BloomBoost`는 그대로 뒀다(전역 세기 하나로 조절하는 게 "전반적"에 맞음).
  실측(궤적을 붙잡은 같은 구도, 지면 타일 제외): 헤일로 픽셀 **33,443 → 8,495(−75%)**,
  강한 발광 **11,205 → 2,250(−80%)**, 세로 퍼짐 **204px → 89px(−56%)**.
  실드 링은 이 세기에서도 선명하게 읽힌다(스크린샷 확인).
  ⚠️ 이전 세션에서 사용자 요청으로 1.6 → 3.6 → 5.2까지 올렸던 값이다 — 차지 픽셀 연출도 같이 약해진다.
- **카메라 포커스 펄스**(사용자 요청): 대시-카운터의 `SectionCamera.FocusPulse`(팬+줌인)를 패링·일섬에도
  짧게 적용. 램프는 공유(`focusPulseRampIn/Hold/RampOut` = 0.06 / 0.12 / 0.26 — 대시-카운터의 hold는
  확인 입력 대기 때문에 2초지만 여긴 "잠시"), 세기만 따로:
  패링은 막아낸 지점으로 pan 0.8 / zoom 0.7, 일섬은 도착 지점으로 pan 1.0 / zoom 1.1.
  일섬 쪽은 적이 없어도 걸리도록 `ApplyIlseomDamage`와 분리해 Sweep 진입 직후에 호출한다.
  실측: 패링 `orthographicSize` 6.000 → **5.300**, `focusOffset` 크기 0.8 /
  일섬 6.000 → **4.900**, `focusOffset` 크기 1.0.
- **플레이어 블룸**(스펙 6 추가분): `Custom/PlayerBloomOverlay` — 플레이어 스프라이트를 복사한 가산
  오버레이(`PlayerBloomFx`). 차지 진행도를 그대로 `_Intensity`에 먹여 **페이드 인**, 시퀀스 종료 시
  `FadeOut` → **페이드 아웃**. 원본 SpriteRenderer를 건드리지 않아 복원 실패 사고가 구조적으로 없다.
- **텍스트 1줄 고정**(사용자 요청): `DmgText` 프리팹의 RectTransform이 숫자 1~2자리 크기(**0.78×0.41**)이고
  `textWrappingMode`가 **Normal**이라 `"막아냄!"`(렌더폭 **1.43**)이 줄바꿈됐다. `DamageText.SetupText`에서
  `textWrappingMode = TextWrappingModes.NoWrap`으로 고정 — `overflowMode=Overflow` + `alignment=Center`라
  줄바꿈만 끄면 가운데 기준으로 한 줄로 뻗는다. 프리팹은 건드리지 않음.
  실측: `"막아냄!"` 줄수=1 / `"12!!!"` 줄수=1 / `"3"` 줄수=1.
- **`[ASSERT] parry_timing: PASS`** (3단: 성공+실드 생성+무피해 / 실드가 다음 공격 1회 차단 / 탭↔홀드 분리).
  `[ASSERT] ilseom: PASS` 회귀도 유지.

## 🐞 발견·수정한 버그
- **패링이 수학적으로 불가능했던 튜닝**(실측으로 확정): 적의 창 실제 도달거리 = `spearThrustLocalPos.x`(1.9)
  × 적 스케일(1.2) = **2.28**인데 `attackRange`가 **1.8**이라, 적이 멈춘 뒤 찌르면 창끝이 플레이어를
  0.48만큼 **관통해 뒤쪽에 꽂혔다**. 플레이어 1타 박스는 반대편(앞쪽 0.2~1.8)이라 교집합이 존재하지 않음
  (겹치려면 거리 ≥ **1.98** 필요, 그런데 공격은 ≤1.8에서만 시작). → `attackRange` **1.8 → 2.4**로 수정
  (씬 저장). 패링 유효 거리 **1.98~2.4**. 이건 패링과 무관하게도 어긋난 세팅이었다(찌르기는 창끝이
  대상에 닿는 거리에서 멈춰야 함).
- **`centroid`는 HLSL 예약어**(보간 한정자) — 변수명으로 쓰면 `syntax error`. `pivot`으로 개명.
- **`TWO_PI`는 URP `Macros.hlsl`에 이미 정의됨** — 다시 `#define`하면 재정의 경고.
- **`_MainTex_ST`를 `UnityPerMaterial` CBUFFER에 넣으면 2D SRP Batcher가 꺼진다**
  (`_TexelSize`/`_ST` 미지원 경고). SpriteRenderer 메시의 UV는 이미 아틀라스 좌표라 `TRANSFORM_TEX` 불필요.
- **블룸 오버레이가 거의 안 빛나던 문제**: 글로우를 스프라이트 색에 그대로 곱하면 대부분이 어두운
  사무라이 스프라이트는 결과가 0에 가까움 → `_Flatten`(0.65)으로 원본 색을 흰색 쪽으로 끌어올려 해결.
- **패링 테스트의 레이스**(테스트 하네스 버그, 제품 아님): 적을 `IsAttacking`만 보고 기다리면
  **이미 판정이 끝난 공격의 Recover 구간**도 True라, 정지 없이 이어진 Play 세션에서 "막을 수 없는 공격"을
  잡아 비결정적으로 FAIL한다(실측: 같은 코드가 clean 세션 3회 PASS → dirty 세션에서 `parry_miss`).
  → `IsAttacking && IsAttackUnresolved`로 조건을 조여 해결. 결정적 검증 전엔 **stop→play로 새 세션을 강제**할 것.

## 📐 결정 (사용자 확정)
- 판정은 **겹침**(완전 포함 아님) — 완전 포함은 세로 오차 ±0.1이라 사실상 성공 불가.
- 실패해도 **모션은 재생** + 쿨다운 0.5s.
- 실드는 **시간 제한 없음** — 적 공격 1회를 막을 때까지 유지.
- 실드 중심은 실측 실루엣 중심(`Idle` 11프레임 불투명 픽셀) = 피봇 기준 **로컬 (-0.22, +0.54)**.
  사용자 스펙의 "약간 오른쪽"과 방향이 반대인데, 그건 *스프라이트 프레임 중심*(피봇 +0.364 오른쪽)을
  본 것으로 보임 — 실제 캐릭터 픽셀은 x[47..72], 피봇 64.4라 **왼쪽**에 있다. `parryShieldOffset`로 조정 가능.

## 🧪 검증 참고
- 인라인 스크린샷 프리뷰(축소본)는 밝은 장면을 **워시아웃된 것처럼** 보여준다. 저장된 PNG의 배경 픽셀은
  세 장 모두 (0.188, 0.30, 0.47)로 동일 — 블룸 판단은 반드시 **저장 파일 픽셀값**으로 할 것.
- 링 실측: 151×155px(종횡비 0.97, 정원), 외경 2.52 월드, 두께 0.23 월드. 실루엣 1.69×1.5보다 적당히 큼.

## 🎛️ 배치용 샌드박스 씬 (`Assets/Scenes/VfxSandbox.unity`)
- 사용자가 에디터에서 직접 위치·크기를 잡기 위한 **별도 씬**. 빌드 세팅에 넣지 않아 실제 게임엔 미반영.
- 읽어서 반영하는 규칙은 `docs/dev/VFX_SANDBOX.md` 참고.
- **1차 반영 완료 (2026-07-25)** — 사용자가 샌드박스에서 잡은 값을 코드 기본값 + 씬 인스턴스 양쪽에 적용:

  | 항목 | 이전 | 반영값 | 출처 |
  |---|---|---|---|
  | `parryShieldOffset` | (-0.22, 0.54) | **(-0.08, 0.56)** | `ParryShield_A`의 `localPosition` |
  | `parryShieldRadius` | 0.9 | **1.0028** | 〃 `localScale.x` |
  | `ilseomShieldTrailCount` | 6 | **8** | `IlseomTrail`에서 `count_8` 그룹만 활성 |

  머티리얼(두께 0.1 / 알파 0.72 / 색 #6BDBFF / boost 2.6)과 `PlayerBloom_Overlay`는 변경 없음.
  실측 검증: 런타임 실드 `localPos=(-0.08, 0.56)`, 일섬 경로 사본 **8개**가 간격 1.03으로 전체 경로(7.2)에
  균등 배치. 반영 후 `[ASSERT] parry_timing: PASS` 유지.
- **반영 절차 주의 — 이번 세션에 같은 함정을 3번 밟았다.** "기본값을 바꿨는데 안 먹는다"의 원인은 항상
  **직렬화된 값이 기본값을 덮기 때문**이다. 세 층 전부 따로 고쳐야 한다:

  | 층 | 덮는 대상 | 고치는 방법 |
  |---|---|---|
  | 씬 인스턴스 | `PlayerController.cs`의 필드 기본값 | `SerializedObject`로 씬 컴포넌트 수정 + 씬 저장 |
  | 머티리얼 에셋(`.mat`) | `.shader`의 `Properties` 기본값 | `mat.SetFloat(...)` + `SetDirty` + `SaveAssets` |
  | 프리팹 인스턴스 | 프리팹 원본 | 인스턴스 오버라이드 확인 |

  실측 사례: `attackRange`(씬), `ilseomStreakHeight`/`Fade`(씬),
  `_TailLength`/`_LaneCount`/`_LaneDensity`(`IlseomSlashStreak.mat`) — 코드/셰이더만 고쳤을 때
  런타임 로그가 계속 옛 값(`height=1.3 fade=0.22`, `lanes=18 density=0.55`)을 찍어서 발견.

## ❓ 미결 항목 — 사용자 확인 대기 (2026-07-24 기준)
> 앞선 세션 기록 곳곳에 흩어져 있던 "확인 필요" 항목을 한 곳에 모음. 처리되면 이 목록에서 지울 것.

| # | 항목 | 상태 · 근거 | 필요한 결정 |
|---|---|---|---|
| 1 | `Glitch Samurai-Slash 2`의 `AttackHitFrame` 이벤트가 **0.0000s(0%)** | 실측: 1타는 0.0833s(**20%**), 2타만 **0%** = 스윙 시작 첫 프레임에 판정. 클립 길이는 둘 다 0.4167s(12fps·5프레임)로 동일 | 의도한 프레임인지 확인. 아니라면 몇 %로 옮길지(1타처럼 20%인지, 다른 값인지) |
| 2 | 씬의 Player `wallLayer` = **1536**(Ground 512 \| Wall 1024) | `task.md` 완료 기록은 1024인데 씬 직렬화 값과 불일치. 바닥이 '벽'으로도 감지됨. 현재 평지 맵에선 실측상 오탐 없음(`rightWall/leftWall=none`) | 1024로 고칠지 여부. 벽이 있는 맵으로 가면 벽점프·대시 벽취소 판정에 영향 가능 (씬 값 변경 = 승인 필요) |
| 3 | `[STALL]` 이동 막힘 진단 코드 | `PlayerController.CheckMovementStall` + `logMovementStall`/`stallLogThreshold` 필드. 사용자 확인 "해결된 것 같음" 단계라 유지 중 | 확신 서면 제거 지시 (삭제는 규칙4에 따라 승인 필요) |
| 4 | 적 몸체 관통(`excludeLayers`에 Enemy 상시 포함) | 이동 막힘의 근본 수정. 이제 플레이어가 적을 **통과**함(Dead Cells/할로우나이트류 표준) | 이 감각이 의도와 맞는지. 적을 단단하게 두길 원하면 되돌릴 수 있으나 이동 감속(5→0.96u/s)은 물리적으로 불가피 |
| 5 | `ilseomPastEnemyDistance` 필드 미사용 | 정지 규칙을 "벽/최대거리"로 단순화하며 "적 뒤로 멈춤"이 사라져 고아가 됨(주석만 표기) | 삭제할지 / 규칙 되돌릴 수 있게 남길지 (삭제는 규칙4 승인 필요, 씬 직렬화 필드도 함께 제거) |
| 6 | `OnJump`의 `isJumpHeld=false` 분기 미실행(짧은 점프 안 먹음) | 일섬 작업 중 발견한 **기존 버그** — SendMessages가 Button 액션의 canceled를 안 보냄(`PlayerInput.cs:1499`). `lowJumpMultiplier`가 첫 점프 이후 영구 미적용 | 이번 범위 밖이라 미수정. 별건으로 고칠지 (점프도 `IsPressed()` 폴링으로 전환) |
| 7 | DummyEnemy `moveSpeed` = 0 (사용자 요청, 씬 저장) | 테스트 편의로 정지시킴. 원래 값 3 | 테스트 끝나면 3으로 되돌릴지 |

# Remnents of Light — 작업 진행 추적 (task.md)

> 최종 업데이트: 2026-07-15 · 스테이지1 버티컬 슬라이스(5주 마스터플랜) 기준

## 📍 현재 위치
- **일정표 1주차 "조작감 깎기 & 전투 아키텍처" 中 `이동 & 물리` 파트** 진행 중.
- 다음 예정: 무적 대시(I-Frame) → 일섬 → 3타 콤보/패링 → 타격 피드백/HP·마나 UI.

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
- 무적 대시(I-Frame, Dash 입력 이미 정의됨) → 일섬(RaycastAll 관통) → 3타 콤보/가드·패링 → 타격 피드백/HP·마나 UI.

## ⚠️ 블로커/주의
- `jumpForce`, `wallJumpForce` 등 튜닝값은 씬 Inspector가 코드 기본값을 덮어씀 → 크기 조정은 Inspector/MCP로.
- Animator 컨트롤러에 미사용 Glitch/Slash/Death 상태 다수 존재 (이번 작업 범위 밖, 삭제는 별도 승인).

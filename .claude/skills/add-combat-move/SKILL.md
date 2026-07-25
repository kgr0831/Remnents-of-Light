# 새 전투 동작 추가 (대시 · 일섬 · 콤보 · 패링류)

## 언제 쓰나(트리거)
`PlayerController.cs`에 새 전투/이동 동작을 하나 추가할 때 (예: 대시, 일섬, 3타 콤보, 패링).
같은 절차를 세 번째 반복하고 있다고 느끼면 이 스킬을 갱신할지 검토한다.

## 절차(단계)

1. **입력 액션 확인** — `Assets/PlayerActions.inputactions`의 `Player` 맵에 해당 액션이 이미 있는지 확인한다.
   현재 정의된 액션: `Move / Jump / Dash / Attack / Parry / Charge`.
   `.inputactions` 파일 자체는 텍스트 편집 금지(hooks가 차단).

   **액션을 새로 추가하는 방법** (MCP `execute_code`):
   씬의 `PlayerInput`이 액션을 enable해둔 상태라 로드된 에셋은 직접 수정할 수 없다
   ("Cannot add/remove elements while one or more of its actions are enabled"). JSON에서 **비활성 사본**을
   만들어 편집한 뒤 파일을 덮어쓰고 재임포트한다 — GUID는 JSON에 실려 있어 참조가 안 깨진다.
   ```csharp
   var tmp = InputActionAsset.FromJson(File.ReadAllText(path));
   InputActionSetupExtensions.AddAction(tmp.FindActionMap("Player"), "Charge",
       InputActionType.Button, "<Mouse>/rightButton", null, null, null, null);
   File.WriteAllText(path, tmp.ToJson());
   Object.DestroyImmediate(tmp);
   AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
   ```
   `Assets/Editor/SetupInputActions.cs`(메뉴 `Tools/Generate Player Actions`)에도 같은 줄을 추가해 재생성 시
   빠지지 않게 한다. ⚠️ 이 생성기는 현재 `Parry`의 F키 보조 바인딩이 빠져 있어 실행하면 그게 사라진다.

2. **⚠️ 홀드(눌림 유지) 입력은 `On<Action>(InputValue)`로 못 잡는다** — 반드시 폴링할 것.
   `PlayerInput`의 SendMessages 경로는 **Button 액션의 `canceled`(뗌)를 아예 전달하지 않는다**:
   ```csharp
   // PlayerInput.cs:1499
   // ATM we only care about `performed` and, in the case of value actions, `canceled`.
   if (!(context.performed || (context.canceled && action.type == InputActionType.Value))) return;
   ```
   → `On<Action>`은 press에서만 호출된다. 홀드 길이를 재는 동작(차지류)에서 이걸 쓰면 **버튼을 떼도
   타이머가 계속 쌓여 저절로 발동**한다(일섬 1차 구현에서 실제로 발생).
   해결: 액션을 직접 폴링한다(`PlayerController.PollChargeInput` 참고).
   ```csharp
   chargeAction = GetComponent<PlayerInput>().actions.FindAction("Charge"); // Awake
   bool held = chargeAction.IsPressed();                                    // Update
   ```
   같은 이유로 `OnJump`의 `isJumpHeld = false` 분기도 실행되지 않는다(기존 잠재 버그, 미수정).

3. **타이머는 시작 프레임에 누적하지 말 것** — `Start<Move>()`로 타이머를 0으로 만든 뒤 같은 프레임에서
   `timer += Time.deltaTime`을 이어서 실행하면 **동작 시작 전의 프레임 간격이 통째로 가산**된다.
   프레임이 튀면 `Time.maximumDeltaTime`(0.333s)까지 커져 2초 차지가 1.67초에 끝난다(실측).
   시작한 프레임에는 `return`으로 빠져 다음 프레임부터 누적할 것.

4. **PlayerController 상태 훅** — 기존 상태 변수 패턴을 따른다 (`isJumping`, `wallJumpLockCounter`처럼
   bool 플래그 + float 타이머 조합). 새 동작 전용 상태는:
   - `bool is<Move>ing` (예: `isDashing`)
   - 지속시간·쿨다운은 `float <move>Timer` + public 튜닝 필드 (`[Header("...")]`로 그룹화, 기존 헤더 스타일 유지)
   - `Update()`/`FixedUpdate()`에 훅을 추가할 때 기존 `HandleMovement()`, `HandleJump()`처럼
     `Handle<Move>()` 메서드로 분리한다.
   - **잠금 상태를 추가하면 아래 6곳을 전부 훑을 것** (일섬에서 실제로 다 필요했다):
     `HandleMovement` / `HandleJump` / `HandleWallSlide` / `HandleDash` / `HandleAttack` /
     `UpdateAnimations` / `FixedUpdate`의 분기 / `CheckMovementStall`(의도된 정지는 스톨이 아님).
   - **취소 입력은 "새 press 엣지"로만 판정** — `attackQueued` 같은 버퍼 플래그를 그대로 보면
     동작 시작 직전에 눌린 묵은 입력이 첫 프레임에 취소를 발동시킨다. `OnDash`/`OnAttack`에서
     `if (isCharging) cancelChargeRequested = true;`처럼 별도 플래그를 세우고 그걸 본다.

5. **애니메이션을 코드로 직접 재생할 때 AnyState 전이를 막을 것** — `PlayerAnimator.controller`에는
   AnyState → `Fall`(!isGrounded && !isWallSliding && yVelocity<0) / `Jump`(yVelocity>0) /
   `Wall Slide`(isWallSliding) / `Land`(트리거) / `Attack1` / `Attack2` 전이가 있어
   `anim.Play("...")`로 재생한 클립을 **다음 평가에서 즉시 덮어쓴다**.
   - 한 프레임 고정: `FreezeAnimAt(state, frame, frameCount)` — `Play` → `Update(0f)` → `anim.enabled = false`.
   - 클립 전체 재생: `LockAnimForIlseom()`처럼 조건을 전부 거짓으로 고정(`isGrounded=true`,
     `yVelocity=0`, `isWallSliding=false`, 트리거 `ResetTrigger`)하고, 그 구간엔
     `UpdateAnimations()`가 파라미터를 다시 안 건드리게 early-return 시킨다.
   - `Glitch Out`/`Glitch Sweep`/`Glitch Slices`는 전이 없는 **고아 상태**라 `anim.Play(이름)`으로 바로 쓸 수 있다.
     `Glitch Sweep` 시트는 원본이 이미 FlipX 되어 있어 재생 중엔 flipX를 **반대로** 줘야 한다.

6. **⚠️ "A 범위와 B 범위가 겹치면" 류 스펙은 코드를 쓰기 전에 기하를 실측하라.**
   두 범위가 현재 튜닝으로 **애초에 만날 수 있는지**를 숫자로 먼저 확인한다. 패링에서 이걸 건너뛰고
   구현→테스트까지 간 뒤에야 교집합이 공집합임을 발견했다(디버그 사이클 1회 통째로 낭비).
   ```csharp
   // execute_code로 한 번에 뽑는다 — 스케일이 붙은 실제 도달거리를 반드시 포함할 것
   float reach = e.spearThrustLocalPos.x * Mathf.Abs(e.transform.localScale.x); // 1.9×1.2 = 2.28 (!)
   float boxMinX = pc.attackHitboxDistance - pc.attackHitboxSize.x * 0.5f;
   float needed  = reach + boxMinX - e.hitRadius;      // 겹치려면 필요한 최소 거리
   bool possible = needed <= e.attackRange;            // 공격이 시작되는 최대 거리
   ```
   - **`localScale`을 빼먹지 말 것** — `TransformPoint(localOffset)`은 스케일이 곱해진다.
     적 스케일 1.2 탓에 창 사거리가 1.9가 아니라 2.28이었고, 이게 판정 불가의 직접 원인이었다.
   - **플레이어 `transform.position.y`는 발밑**이다(피봇 하단). 1타 히트박스는 그 y를 중심으로
     ±0.6이라 실제로는 지면 근처를 덮는다 — 적의 판정점 높이와 비교할 때 반드시 감안할 것.
   - **적의 공격 판정점은 플레이어 "뒤"에 꽂힐 수 있다.** 적이 멈추는 거리(`attackRange`)보다
     무기 사거리가 길면 판정점이 플레이어를 관통해 지나가고, 플레이어의 공격 박스는 반대편(앞쪽)이라
     영원히 안 만난다. 이럴 땐 판정식을 비트는 대신 **적의 정지 거리를 무기 사거리에 맞추는 것**이 옳다.
   - 테스트에도 `gap`/`overlaps`를 로그로 남겨, 실패했을 때 "겹쳤는데 로직이 틀림"인지
     "애초에 안 겹침"인지 한 줄로 구분되게 한다.

7. **한 버튼에 탭·홀드를 같이 걸 때는 홀드 연출을 탭 임계치만큼 미뤄라.**
   누른 순간엔 탭인지 홀드인지 알 수 없다 → 상태(`isCharging`)만 먼저 열고, 연출(애니 고정 · FX · 블룸)은
   `chargeVisualsStarted` 플래그로 임계치 이후에 시작한다. 안 그러면 탭할 때마다 FX가 깜빡이고
   취소 이펙트까지 터진다. 탭으로 끝난 취소는 **애니메이터를 건드리지 않아야** 다음 모션 앞에
   Idle 한 프레임이 끼지 않는다.

8. **`[ASSERT]` 채널 정의** — `docs/dev/ASSERT_CONVENTION.md`의 "현재 채널 목록" 표에 먼저 채널을 추가한다.
   네이밍: 소문자 스네이크케이스, 동작 단위로 하나 (`dash_iframe`, `ilseom`, `combo_window`).
   구현 코드에 `TestLog.Event/Step/Assert(channel, ...)`를 심는다 (`Assets/Scripts/TestLog.cs`).

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

7. **`PlayTestRunner` 시나리오 추가** — `Assets/Scripts/Testing/PlayTestRunner.cs`에 코루틴 메서드
   (`<Move>Test`) 를 추가하고 `[MenuItem("Tools/PlayTest/<Move Name>")]`로 노출한다.
   입력 주입은 `Assets/Scripts/Testing/InputInjector.cs`의 `Press<Action>/Release<Action>` 사용
   (없으면 같은 패턴으로 추가). `IlseomTest()`가 3단(취소/발동/쿨타임) 참고 골격.
   검증은 MCP로: `manage_editor(action="play")` → `execute_menu_item("Tools/PlayTest/...")` →
   `read_console`으로 `[STEP]`/`[ASSERT]` 로그 확인 → `manage_editor(action="stop")`.
   - **판정값은 하드코딩하지 말고 `player.<필드>`에서 읽어라** — 튜닝값이 바뀌어도 테스트가 따라간다.
   - **적을 쓰는 시나리오는 `enemy.moveSpeed = 0` + 알려진 좌표로 세워둘 것**(런타임 전용, 끝나면 원복).
     추적하게 두면 발동 시점의 거리가 매번 달라져 멈출 위치를 예측할 수 없다.
   - ⚠️ **`TestRecorder`가 켜져 있으면 실측 1.5fps로 떨어진다**(Unity Recorder가 `captureDeltaTime`을
     1/30로 고정하고 1280×720 MP4를 프레임마다 인코딩). 게임 9초 테스트가 **실시간 3분**이 된다.
     로그가 멈춘 것처럼 보여도 정지가 아니다 — `Time.time` vs `Time.unscaledTime`으로 확인하고 기다릴 것.

   - **"적이 공격 중"을 `IsAttacking` 하나로 기다리지 말 것** — 이미 판정이 끝난 공격의 회수(Recover)
     구간도 True다. 정지 없이 이어진 Play 세션에서는 "막을 수 없는 공격"을 잡아 **비결정적으로 FAIL**한다
     (패링에서 실측: clean 세션 3회 PASS → dirty 세션에서 `parry_miss`). 판정이 아직 남아 있는지
     (`IsAttackUnresolved` 류)를 **함께** 기다릴 것. 그리고 결정적 검증 전엔 stop→play로 새 세션을 강제한다.

8. **거짓 통과를 경계할 것** — "이동 안 함 = 통과" 같은 **음성 판정**은 기능이 아예 안 걸려도 통과한다.
   일섬 1차 테스트에서 Phase 1(취소)이 PASS였지만 실제로는 릴리즈가 전달되지 않아 차지가 계속되던
   상태였고, `charge_cancelled_early` **이벤트 로그가 없다는 것**으로 잡았다.
   → 상태 판정과 함께 **기대하는 EVENT가 실제로 찍혔는지** 로그로 확인할 것.

9. **⚠️ "기본값을 바꿨는데 안 먹는다"는 거의 항상 직렬화된 값이 덮고 있는 것이다.**
   한 세션에서 이 함정을 세 번 밟았다(패링). 값을 바꿀 때 **아래 층을 전부** 확인할 것.

   | 층 | 무엇을 덮는가 | 고치는 방법 |
   |---|---|---|
   | 씬 인스턴스 | `.cs`의 public 필드 기본값 | `SerializedObject`로 씬 컴포넌트 수정 → **씬 저장** |
   | 머티리얼 에셋(`.mat`) | `.shader`의 `Properties` 기본값 | `mat.SetFloat(...)` → `SetDirty` → `SaveAssets` |
   | 프리팹 인스턴스 | 프리팹 원본 | 인스턴스 오버라이드 해제 |

   실측 시그니처: 코드/셰이더를 고쳤는데 **런타임 로그가 옛 값을 계속 찍는다**
   (`height=1.3 fade=0.22`, `lanes=18 density=0.55`). 그러니 튜닝을 바꾼 뒤엔 반드시
   런타임에서 그 값을 **다시 읽어 로그로 확인**하고, 눈으로 판단하지 말 것.
   반대로 **플레이 모드에서 public 필드에 준 값은 정지하면 사라진다** — 캡처·측정용으로만 쓸 것.

10. **docs 조회 규칙 준수** — API 시그니처·수치가 불확실하면 추측하지 말고 조회한다.
   `unity_reflect`(get_type/get_member)가 문서보다 확실하다 — 프로젝트에 로드된 실제 어셈블리를 본다.
   확정한 비자명한 사실은 코드 주석 또는 `task.md`에 출처를 한 줄 남긴다.

## 검증(ASSERT 채널)
- 새로 정의한 채널이 `docs/dev/ASSERT_CONVENTION.md` 표와 `PlayTestRunner` 시나리오 양쪽에
  동일한 이름으로 등장하는지 확인.
- 컴파일 클린(에디터 콘솔 에러 0) → 플레이 모드에서 메뉴 실행 → `[ASSERT] <channel>: PASS` 확인.

## 셰이더 VFX를 곁들일 때 (일섬 픽셀 연출에서 확정)
- **HLSL 예약어·중복 정의 함정**(패링 실드에서 실측): `centroid`는 보간 한정자라 **변수명으로 못 쓴다**
  (`syntax error: unexpected token 'centroid'`). `TWO_PI`는 URP `Macros.hlsl`에 **이미 있어서**
  다시 `#define`하면 재정의 경고가 난다.
- **`_MainTex_ST`를 `UnityPerMaterial` CBUFFER에 넣지 말 것** — 2D SRP Batcher가 `_TexelSize`/`_ST`
  텍스처 프로퍼티를 지원하지 않아 **그 머티리얼을 쓰는 2D 렌더러 전체의 SRP 배칭이 꺼진다**(에디터 경고).
  SpriteRenderer가 만드는 메시의 UV는 이미 아틀라스 좌표라 `TRANSFORM_TEX` 자체가 불필요하다.
- **"이 캐릭터만 빛나게"는 가산 오버레이로**(`PlayerBloomFx`/`PlayerBloomOverlay.shader`):
  원본 SpriteRenderer의 머티리얼을 갈아끼우지 말고, `sprite`/`flipX`를 `LateUpdate`에 복사한
  **자식 SpriteRenderer**를 `Blend One One`으로 얹는다. 복원 실패로 캐릭터가 이상하게 남는 사고가
  구조적으로 없고, 2D 라이팅도 원본이 그대로 받는다. `_Intensity` 하나로 페이드 인/아웃.
  ⚠️ 글로우를 스프라이트 색에 **그냥 곱하면 어두운 캐릭터는 거의 안 빛난다** — `lerp(tex.rgb, 1, _Flatten)`
  (0.65 권장)로 흰색 쪽으로 끌어올릴 것. 실루엣 모양은 `tex.a`가 만든다.
- **조각나 흩어지는 파괴 연출**은 프래그먼트에서 **조각별 강체 변환을 역으로 풀어** 판정한다:
  픽셀 p를 조각 i의 (이동+회전) 역변환으로 되돌린 뒤 그 점이 조각의 원래 각도 구간 안인지 본다.
  링처럼 극좌표로 표현되는 도형이면 정확히 분리되고, 조각 수가 적으면(≤16) `[loop]`로 충분히 싸다.
  `_Break=0`일 때 변환이 항등이 되게 짜두면 성한 상태와 코드 경로를 하나로 유지할 수 있다.
- ⚠️ **인라인 스크린샷 프리뷰(축소본)는 밝은 장면을 워시아웃된 것처럼 보여준다.** 블룸이 과한지 판단할 땐
  반드시 **저장된 PNG의 배경 픽셀값**을 읽어 비교할 것(실측: 프리뷰는 하얗게 떴지만 저장 파일 배경은
  세 장 모두 (0.188, 0.30, 0.47)로 동일 = 전역 워시아웃 없음).
- **패스 태그는 `LightMode = "Universal2D"`** — 이 프로젝트는 URP **2D Renderer**(`Settings/Renderer2D.asset`)라
  `Universal2D`/`SRPDefaultUnlit`만 수집한다. `UniversalForward`는 통째로 스킵돼 아무것도 안 그려진다
  (`Assets/Shaders/VFXLit2D.shader`가 이 함정에 걸려 사실상 미사용 상태).
- **파티클류는 프래그먼트 루프 대신 "파티클 1개 = 쿼드 1개" 메시 + 버텍스 셰이더**로 움직여라.
  corner를 `mesh.uv`(TEXCOORD0), 인덱스를 `mesh.uv2`(TEXCOORD1)에 실어 보낸다.
  정점을 원점에 몰아두므로 **`mesh.bounds`를 직접 넉넉하게 지정**해야 프러스텀 컬링에 안 잘린다.
- **`frac(sin(dot(p,k))*43758.5) 해시 금지`** — 인덱스가 정수면 `sin` 인자가 커져 GPU 정밀도가 무너지고
  결과가 몇 개 값으로 뭉친다(각도가 0·π로 쏠려 링이 가로 띠가 됨, 실측 x71px vs y27px).
  곱셈·`frac`만 쓰는 해시(Dave Hoskins "Hash without Sine")를 쓰고, 개수가 적으면(≤64)
  **각도는 인덱스로 균등 분할 + 지터**해 커버리지를 보장한다.
- FX 오브젝트를 **플레이어 자식**으로 붙이면 로컬 공간 = 플레이어 기준 공간이 되고, 부모 스케일(1.3)이
  픽셀 크기에도 걸려 도트 크기가 스프라이트와 자동으로 맞는다. `Renderer.sortingLayerID/sortingOrder`는
  MeshRenderer에도 있으니 그걸로 정렬한다.
- **특정 오브젝트만 블룸시키려면**: URP엔 오브젝트별 블룸이 없다 →
  ① 셰이더가 1.0 초과 HDR로 출력(`_BloomBoost`), ② Bloom 임계값을 1.0보다 위(예: 1.15)로 둬서
  LDR 스프라이트는 절대 못 넘게 한다. 색조를 지키려면 R 채널은 1 아래로 남길 것.
  전제: URP `supportsHDR` + 카메라 `allowHDR` + 카메라 `renderPostProcessing = true` + 씬에 Global Volume.
- ⚠️ **`VolumeProfile.Add<T>()`만 하면 저장 시 `components: - {fileID: 0}`으로 날아간다.**
  `AssetDatabase.AddObjectToAsset(override, profile)`로 **서브에셋 등록**까지 해야 직렬화된다.
  적용 여부는 `bloom.active`만 토글해 같은 배치의 스크린샷 2장을 픽셀 diff로 비교해 확인할 것
  (눈으로는 "적용된 것 같다"에 속기 쉽다).
- 픽셀 연출 검증은 **스크린샷 픽셀 통계**로 한다: 색 조건으로 카운트 → 중심·x/y 분포폭·종횡비를 뽑으면
  "링인지 띠인지", "수집점이 맞는지"가 수치로 나온다. 눈대중보다 훨씬 빠르고 확실하다.
- **"계속 흐르는" 연출은 시계와 세기를 분리하라** — 진행도(`_Progress`) 하나로 애니 위상 t를 만들면
  완충 시 모든 파티클 t=1로 같아져 "한 번 모이고 끝"이 된다. 별도 시계(`_Flow` = 경과 초)로
  `frac(_Flow*speed)` 주기 흐름을 돌리고, `_Progress`는 참여 파티클 수 게이팅에만 쓴다(일섬 홀드 이펙트).
- **궤적 섬광**(`IlseomSlashStreak.shader`/`IlseomSlashFx.cs`): 출발→도착을 잇는 쿼드 1개 + `_Progress`로
  선두를 훑는 방식. uv.x=진행축, uv.y=두께축. 쿼드가 가로로 길어 UV당 월드 크기가 축마다 다르므로
  도트 스냅용 `_PixelStep`을 x/y 따로 넘긴다(`pixelSize/length`, `pixelSize/height`). 방향은 메시를
  `Quaternion.FromToRotation(Vector3.right, dir)`로 회전해 대응.

## 스프라이트 시트를 새로 자를 때 (Unity 6)
- **`TextureImporter.spritesheet`는 제거됨**(CS0618). `Assets/Editor/SetupAnimationsEditor.cs`가 아직 이걸 쓴다.
- 신규 경로 (assembly `Unity.2D.Sprite.Editor`):
  `SpriteDataProviderFactories.Init()` → `GetSpriteEditorDataProviderFromObject(importer)` →
  `InitSpriteEditorDataProvider()` → `UnityEditor.SpriteRect[]` 채워 `SetSpriteRects()` → `Apply()` → `SaveAndReimport()`.
  `SpriteRect.rect`의 y는 **좌하단 원점**이라 시트 위에서부터 세려면 `texH - (row+1)*cell`로 뒤집는다.
- 완전 투명한 칸은 슬라이스에서 빼라(`spriteMeshType: Tight`에서 퇴화 메시가 될 수 있다).
- 클립은 `AnimationUtility.SetObjectReferenceCurve` + `EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite")`,
  마지막 프레임도 보이게 `AnimationClipSettings.stopTime = frameCount / frameRate`(Unity 시트 드래그와 동일 규격).
- 재생 후 스스로 사라지는 원샷 VFX는 기존 **`HitVfxAutoReturn`**(클립 길이 뒤 자기 파괴)을 재사용한다.
  Animator `updateMode = UnscaledTime`으로 두면 히트스톱 중에도 정상 재생·소멸한다.

## 출처(있으면)
- `PlayerInput`이 Button 액션의 `canceled`를 안 보내는 코드:
  `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/PlayerInput.cs:1499` (확인 2026-07-25)
- URP 2D Renderer의 셰이더 태그 수집 범위: `Assets/Scripts/VFX/HitVfxAutoReturn.cs` 주석의 실측 기록.
- "Hash without Sine" (Dave Hoskins): shadertoy.com/view/4djSRW
- 스프라이트 슬라이스 신규 API: `unity_reflect`로 `SpriteDataProviderFactories` /
  `ISpriteEditorDataProvider` / `UnityEditor.SpriteRect` 존재·시그니처 확인 (확인 2026-07-25)
- 이후 이 절차에서 조회한 API 출처는 여기 목록에 이어서 추가한다.

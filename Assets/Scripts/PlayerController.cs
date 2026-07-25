using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    
    [Header("Jump")]
    public float jumpForce = 9f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float coyoteTime = 0.1f;
    
    [Header("Wall Slide & Jump")]
    public float wallSlideSpeed = 2f;
    public float wallSlideAccel = 20f;
    public Vector2 wallJumpForce = new Vector2(5f, 9f);
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.5f;
    public string invincibleLayerName = "PlayerInvincible";

    [Header("Dash VFX (Sandevistan)")]
    public bool dashAfterImage = true;
    public float afterImageInterval = 0.01f;   // 대시 중 잔상 스폰 간격 (촘촘할수록 잔상↑)
    public float afterImageLifetime = 0.35f;   // 잔상 하나가 사라지기까지 시간
    [Range(0f, 1f)] public float afterImageAlpha = 0.7f;
    public int afterImageSortingOffset = -1;   // 원본 뒤에 깔리도록
    // 산데비스탄: 생성 순서에 따라 초록→파랑→보라→빨강→노랑으로 에코 색이 바뀐다
    public Color[] afterImageColors = new Color[] {
        new Color(0.35f, 1f, 0.6f),
        new Color(0.35f, 0.65f, 1f),
        new Color(0.7f, 0.4f, 1f),
        new Color(1f, 0.35f, 0.45f),
        new Color(1f, 0.9f, 0.4f),
    };

    [Header("Dash Anim Freeze")]
    public bool dashFreezeAnim = true;
    public string dashFreezeState = "Glitch Samurai-Run"; // 대시 중 고정할 상태
    public int dashFreezeFrame = 3;                        // 고정할 프레임 인덱스
    public int dashFreezeFrameCount = 12;                  // Run 클립 총 프레임 수

    [Header("Dash Hitstop")]
    public bool dashHitstop = true;                 // 대시 시작 순간 짧은 시간정지(임팩트)
    public float hitstopDuration = 0.05f;           // 정지 길이(실시간 초, 40~80ms 권장)
    [Range(0f, 1f)] public float hitstopScale = 0f; // 정지 중 timeScale(0=완전 정지)

    [Header("Health")]
    public int maxHp = 100;
    public int currentHp;

    [Header("Attack (1-2 Combo)")]
    public int attack1Damage = 1;
    public int attack2Damage = 1;
    public float attack1Duration = 0.4167f; // Glitch Samurai-Slash 1 클립 길이(사용자가 5프레임으로 재편집), 애니메이터 재생속도 1
    public float attack2Duration = 0.4167f; // Glitch Samurai-Slash 2 클립 길이(5프레임), 애니메이터 재생속도 1
    public float comboBufferDuration = 2f;                     // 마지막 공격이 끝난 뒤 이 시간 안에 다시 공격하면 콤보로 이어짐, 지나면 1타로 리셋
    public float attackInputBufferDuration = 0.3f;              // 공격 중/쿨다운 중에 눌러도 이 시간 안이면 버퍼링돼 자동 발동(UniTrio-Game-2026 PlayerWeaponController._attackQueued 참고)
    public Vector2 attackHitboxSize = new Vector2(1.6f, 1.2f);
    public float attackHitboxDistance = 1f;
    public float attackLungeDistance = 0.3f; // 공격 시작 시 바라보는 방향으로 전진하는 거리(UniTrio 참고)
    // 적이 플레이어 공격에 맞으면 "플레이어가 공격 시 전진하는 거리 × 이 배율"만큼 밀려난다(사용자 스펙).
    public float enemyKnockbackMultiplier = 1.5f;
    public LayerMask enemyLayer;

    [Header("Attack VFX")]
    public bool attackHitstop = true;
    public float attackHitstopDuration = 0.05f;
    [Range(0f, 1f)] public float attackHitstopScale = 0f;
    public bool attackScreenShake = true;
    public float attackShakeDuration = 0.12f;
    public float attackShakeMagnitude = 0.15f;
    // UniTrio-Game-2026(SwordHitbox.SpawnHitVFX/SpawnDamageText)에서 참고: 타격 지점에 스파크+데미지 숫자 스폰
    public GameObject[] hitVfxPrefabs;
    public float hitVfxOffsetTowardsEnemy = 0.3f;
    public GameObject damageTextPrefab;
    public Color damageTextColor = Color.white;

    // 크리티컬(Hit02) · 처형(Hit03). 두 경우엔 일반 hitVfxPrefabs 대신 전용 프리팹이 반드시 뜨고,
    // 데미지 텍스트가 2배 크기 + "숫자!!!"로 강조되며, 쉐이크·히트스톱이 배율만큼 세진다.
    // 텍스트 색은 각 스프라이트에서 뽑은 대표색(Hit02 #FFD400 금색 / Hit03 #FF1922 적색).
    [Header("Critical / Execution")]
    [Range(0f, 1f)] public float critChance = 0.3f;      // 좌클릭 일반 공격에만 적용(카운터는 항상 크리티컬)
    public float critDamageMultiplierMin = 2f;
    public float critDamageMultiplierMax = 3f;
    public GameObject critHitVfxPrefab;                  // Hit02
    public GameObject executionHitVfxPrefab;             // Hit03 (처형 — 발동 조건은 추후 구현)
    public Color critTextColor = new Color(1f, 0.831f, 0f, 1f);        // #FFD400
    public Color executionTextColor = new Color(1f, 0.098f, 0.133f, 1f); // #FF1922
    public float critShakeMultiplier = 2f;
    public float critHitstopMultiplier = 2f;

    // UniTrio-Game-2026(JustDodge) 참고 — 타이밍 기반 저스트 닷지. 대시 무적 윈도우 중 적 찌르기가
    // 실제로 닿는 순간(DummyEnemy.CheckThrustHit → TryConsumeDodge) 발동 → 슬로우모션 +
    // 확인키(F, 보조로 우클릭) 대기 → 성공 시 적 쪽으로 돌진해 배율 데미지 카운터(y좌표는 유지).
    [Header("Dodge Counter")]
    public bool dodgeCounterEnabled = true;
    // 대시 실제 지속시간(0.18s)만으로는 적 찌르기 활성 프레임과 겹치는 타이밍이 너무 빡빡함(사용자 피드백) —
    // UniTrio DashHandler._justDodgeWindow 참고: 무적(i-frame) 자체는 대시 길이 그대로 두고, "닷지로 잡아줄
    // 창"만 대시 시작 기준으로 더 길게 열어둠(대시가 끝난 직후 살짝의 유예 동안도 닷지 인정).
    public float dodgeCounterGraceWindow = 0.35f;    // 대시 시작 후 이 시간 동안 접촉하면 닷지 인정(대시 길이보다 길게)
    public float dodgeCounterInputWindow = 2f;       // 확인키 대기 시간(실시간 초, 사용자 요청으로 1→2초)
    [Range(0.01f, 1f)] public float dodgeCounterSlowScale = 0.15f; // 대기 중 timeScale
    public Color dodgeCounterGlowColor = new Color(0.6f, 0.95f, 1f, 1f); // 대기 중 플레이어 스프라이트 틴트
    // 회피 성공 순간 대시를 이 배율만큼 연장해서 슬로우모션과 함께 계속 미끄러지듯 나아가게 함
    // (UniTrio DashHandler.ExtendDash 참고). 연장 중엔 물리 무적(IsInvincible=isDashing)도 함께 유지됨.
    public float dodgeCounterDashExtendMultiplier = 2f;
    public float dodgeCounterRushDuration = 0.12f;   // 적 뒤로 돌진하는 시간(실시간 초)
    public float dodgeCounterRushPastDistance = 2.2f; // 적 중심 기준 얼마나 지나쳐서 멈출지(사용자 요청으로 거리 확대)
    public float dodgeCounterDamageMultiplier = 3f;  // attack1Damage 대비 배율
    public float dodgeCounterHold = 0.3f;            // 타격 후 여운(실시간 초)
    public float dodgeCounterActivationShakeDuration = 0.08f;
    public float dodgeCounterActivationShakeMagnitude = 0.04f;
    public float dodgeCounterHitShakeDuration = 0.1f;
    public float dodgeCounterHitShakeMagnitude = 0.07f;
    public float dodgeCounterHitstopDuration = 0.05f;
    [Range(0f, 1f)] public float dodgeCounterHitstopScale = 0.1f;
    // 카운터 러시(적 뒤로 돌진) 중 잔상을 대량 생성(사용자 요청) — 평소 대시(0.01s)보다도 촘촘하게.
    // 러시가 0.12s로 매우 짧아 프레임당 1회 스폰 방식은 프레임레이트에 막혀 실제로는 몇 개 안 나감 →
    // 경로를 미리 계산해 한 번에 까는 방식으로 변경(간격은 "몇 개를 깔지" 계산용으로만 사용).
    public float dodgeCounterAfterImageInterval = 0.008f;
    public float dodgeCounterAfterImageLifetime = 0.6f; // 평소 대시 잔상보다 오래 남아 궤적이 잘 보이게
    // 카운터 명중 임팩트 VFX(UniTrio-Game-2026 JustDodgeVFX 절차적 슬래시 — 외부 아트 의존 없음).
    // 회피 발동 순간의 Sparkle/Glow(흰빛 이펙트)는 사용자 피드백으로 제거 — 그레이스케일 확산만으로 신호.
    public Color dodgeImpactColor = new Color(0.85f, 0.97f, 1f, 1f);
    public float dodgeImpactScale = 3f;
    // 흑백 화면 확산(UniTrio JustDodgeController 참고, GrayscaleRendererFeature 필요 — URP 렌더러에 미등록
    // 상태면 Instance가 null이라 조용히 무시됨, 등록 시 자동 활성화)
    public float dodgeGrayscaleMaxRadius = 1.8f; // 화면 UV 기준 — 플레이어가 화면 어디에 있어도 전체를 덮도록 넉넉하게
    public float dodgeGrayscaleRampIn = 0.6f;    // 흑백이 플레이어 중심에서 퍼지는 시간(실시간 초)
    public float dodgeGrayscaleRampOut = 0.2f;   // 종료 시 원복 시간(실시간 초)
    // 카메라 포커스 이벤트(UniTrio JustDodgeController의 팬+줌 참고) — 회피 발동 시 적 쪽으로 살짝 다가가며 줌인.
    public float dodgeCamPanAmount = 1.2f;
    public float dodgeCamZoomAmount = 0.8f;

    // ── 일섬(一閃) ──────────────────────────────────────────────────────────────────────────
    // 우클릭을 ilseomChargeTime 이상 모았다 떼면 발동하는 장거리 필살기.
    // 차지 중: 이동 잠금(A/D로 방향만 전환) · Glitch Out 0프레임 고정 · 픽셀 수집 연출 · 카메라 쉐이크 상승 · 받는 피해 절반.
    // 발동: 무적 → Glitch Out 재생 → 투명 이동(벽 1순위, 없으면 최원거리 적 살짝 지나서, 둘 다 없으면 최대 사거리)
    //       → Glitch Sweep 0프레임에 경로 위 모든 적에게 처형 피격 → Sweep 종료 시 무적 해제 + 쿨타임.
    [Header("Ilseom (일섬)")]
    public bool ilseomEnabled = true;
    public float ilseomChargeTime = 2f;
    public float ilseomCooldown = 20f;
    public float ilseomDistanceMultiplier = 2f;   // 대시 이동거리(dashSpeed×dashDuration) 대비 최대 사거리 배율
    public float ilseomDamageMultiplier = 4f;     // attack1Damage 대비 배율
    [Range(0f, 1f)] public float ilseomChargeDamageTakenMultiplier = 0.5f; // 차지 중 받는 피해(0.5 = 50% 감소)
    public float ilseomPastEnemyDistance = 0.8f;  // (미사용) 예전 "적 뒤로 멈춤" 규칙용 — 현재 정지는 벽/최대거리만
    public float ilseomWallMargin = 0.25f;        // 경로에 벽이 있으면 이만큼 띄우고 그 앞에서 멈춤
    public float ilseomMoveDuration = 0.1f;       // 투명해지며 이동하는 시간
    [Range(0f, 1f)] public float ilseomMoveAlpha = 0.15f; // 이동 중 스프라이트 알파
    public float ilseomPathHeight = 1.4f;         // 경로 위 적을 훑는 판정 박스 높이

    [Header("Ilseom Anim")]
    // Glitch Out은 차지 중 0프레임 고정용 + 발동 시 통째로 재생용으로 같이 쓰인다(5프레임 @12fps = 0.4167s).
    public string ilseomChargeState = "Glitch Samurai-Glitch Out";
    public int ilseomChargeFreezeFrame = 0;
    public int ilseomChargeFrameCount = 5;
    public float ilseomGlitchOutDuration = 0.4167f;
    // Glitch Sweep 시트는 원본이 이미 FlipX 되어 있어(사용자 확인) 재생 중엔 flipX를 반대로 준다. 6프레임 @12fps = 0.5s.
    public string ilseomSweepState = "Glitch Samurai-Glitch Sweep";
    public float ilseomSweepDuration = 0.5f;
    // Glitch Out/Sweep은 애니메이터에서 나가는 전이가 0개인 고아 상태다 — 클립이 끝나도 그 상태에
    // 머물러 마지막 프레임이 스프라이트에 그대로 남는다(사용자 리포트). 일섬이 끝나거나 차지가
    // 취소되면 이 상태로 직접 되돌려 Idle↔Run·AnyState 전이가 다시 정상 동작하게 한다.
    // (대시는 전이가 있는 Run에 프리즈하기 때문에 이 문제가 없었다)
    public string ilseomExitState = "Glitch Samurai-Idle";

    [Header("Ilseom VFX")]
    public Material ilseomChargePixelMaterial;    // Custom/IlseomChargePixels (Assets/VFX/Ilseom/IlseomChargePixels.mat)
    public int ilseomChargePixelCount = 36;   // 사용자 요청으로 양을 줄임(64→36)
    // 픽셀이 모이는 지점(플레이어 피봇 기준). x는 바라보는 방향으로 미러링된다(사용자 확정).
    public Vector2 ilseomGatherOffset = new Vector2(0.55f, 0.7f);
    public float ilseomChargeShakeStartDelay = 0.5f;  // 이 시간 전에는 쉐이크를 적용하지 않음
    public float ilseomChargeShakeMultiplier = 1.5f;  // attackShakeMagnitude 대비 최대 배율(차지 100%일 때)
    public float ilseomCancelFxDuration = 0.25f;      // 취소 시 픽셀이 터져나가며 사라지는 시간
    public float ilseomFinishFxDuration = 0.18f;      // 발동 시 모인 픽셀이 페이드아웃 되는 시간
    public GameObject ilseomBuffPopPrefab;            // Resistance_Up (충전 완료 · 쿨타임 완료 시 머리 위 표시)
    public float ilseomBuffPopHeight = 1.6f;          // 머리 위 표시 높이(사용자 요청으로 더 위로)
    public float ilseomBuffPopScale = 1.5f;           // 표시 크기 배율(사용자 요청 1.5배)

    // 스펙 6 확장("일섬 이동 경로 내에 더 길게 그리고 더 많이 표현") — 이 궤적 섬광을 강화한 것.
    [Header("Ilseom Trail Streak")]
    public Material ilseomStreakMaterial;   // Custom/IlseomSlashStreak (Assets/VFX/Ilseom/IlseomSlashStreak.mat)
    // 1차로 2.2 / fade 0.6까지 올렸다가 "너무 느리고 크다"는 피드백으로 되돌림(사용자 확인 2026-07-25).
    // 원래 값(1.3 / 0.22)보다 살짝만 위에 둬서 "더 길게·더 많이"는 겹 수로 표현한다.
    public float ilseomStreakHeight = 1.5f; // 궤적 두께(월드 단위)
    public float ilseomStreakSweep = 0.12f; // 선두가 궤적을 훑는 시간 — 이동 시간과 비슷하게 두면 몸과 같이 나간다
    public float ilseomStreakFade = 0.28f;  // 훑은 뒤 사라지는 시간
    public int ilseomStreakSortingOffset = -1; // 플레이어보다 뒤에 깔아 실루엣을 가리지 않게
    // 궤적을 몇 겹으로 깔지("더 많이"). 겹마다 두께·수명·시드가 달라 한 장짜리보다 두껍고 오래 남는다.
    public int ilseomStreakLayers = 3;
    public float ilseomStreakLayerHeightSpread = 1.4f; // 마지막 겹의 두께 배율(첫 겹 1배 → 이 값까지)
    public float ilseomStreakLayerFadeSpread = 1.25f;  // 마지막 겹의 수명 배율(바깥 겹이 조금 더 오래 남아 번지듯)

    // ── 패링 ────────────────────────────────────────────────────────────────────────────────
    // 우클릭을 "톡" 눌렀다 떼면(parryTapMaxHold 이내) 패링, 그보다 오래 쥐고 있으면 일섬 차지로 넘어간다.
    // 성공 조건(스펙 3): 적이 공격 모션 중이면서 —
    //   (B) 아직 그 공격에 맞지 않았거나, (A) 대시 회피 인정 창(dodgeCounterGraceTimer)이 열려 있고
    //   — 그 적의 공격 범위(창끝 원)가 플레이어 1타 히트박스와 겹칠 때.
    // 성공/실패와 무관하게 Slash 1 모션은 나가고, 실패하면 parryFailCooldown만큼 재입력이 잠긴다(사용자 확정).
    [Header("Parry (패링)")]
    public bool parryEnabled = true;
    public float parryTapMaxHold = 0.2f;        // 이 시간 안에 떼면 패링(넘기면 일섬 차지 연출이 시작됨)
    public float parryMotionDuration = 0.4167f; // Glitch Samurai-Slash 1 클립 길이(attack1Duration과 동일)
    public float parryFailCooldown = 0.5f;      // 판정 실패 시 재입력 잠금
    public float parrySearchRadius = 5f;        // 후보 적 검색 반경 — 적 몸통은 창 길이(1.9)만큼 떨어져 있어 넉넉히
    public float parryFxHeightOffset = 0.35f;   // 겹침 중앙에서 텍스트/VFX를 띄울 높이(스펙 4 "약간 위쪽")
    public string parryText = "막아냄!";

    // 실드는 플레이어 자식으로 붙어 부모 스케일(1.3)을 그대로 받으므로 값은 전부 "플레이어 로컬 단위".
    // 피봇이 스프라이트 중앙이 아니라 오프셋이 필요하다. 실측 실루엣 중심은 (-0.22, +0.54)였고
    // 사용자가 VfxSandbox 씬에서 (-0.08, +0.56) / 반지름 1.0028로 직접 잡았다(2026-07-25).
    // x는 바라보는 방향에 따라 미러링된다.
    [Header("Parry Shield (구형 실드)")]
    public Material parryShieldMaterial;        // Custom/ParryShield (Assets/VFX/Parry/ParryShield.mat)
    public Vector2 parryShieldOffset = new Vector2(-0.08f, 0.56f);
    public float parryShieldRadius = 1.0028f;   // 실루엣(로컬 1.3×1.2)보다 조금 크게
    public float parryShieldBreakDuration = 0.45f; // 유리처럼 조각나 사라지는 시간
    public int parryShieldSortingOffset = 2;    // 플레이어보다 앞에 그려 감싸는 것처럼 보이게

    // 대시-카운터가 쓰는 카메라 팬+줌인(SectionCamera.FocusPulse)을 일섬·패링에도 짧게 적용(사용자 요청).
    // 대시-카운터는 확인 입력을 기다려야 해서 hold가 2초(dodgeCounterInputWindow)지만, 이쪽은 "잠시"라
    // 훨씬 짧다. 램프 타이밍은 두 동작이 공유하고 팬·줌 세기만 따로 둔다.
    [Header("Focus Pulse (일섬 · 패링 카메라 줌)")]
    public float focusPulseRampIn = 0.06f;
    public float focusPulseHold = 0.12f;
    public float focusPulseRampOut = 0.26f;
    public float parryCamPanAmount = 0.8f;   // 막아낸 지점 쪽으로 다가가는 거리
    public float parryCamZoomAmount = 0.7f;  // orthographicSize 감소량(줌인)
    public float ilseomCamPanAmount = 1f;
    public float ilseomCamZoomAmount = 1.1f;

    // 차지~발동 구간에는 플레이어 자체에 블룸이 페이드 인 → 아웃으로 걸린다(스펙 6 확장).
    // (실드 링을 일섬에 두르던 1차 구현은 스펙 오독이라 제거됨 — "플레이어에게 적용된 쉐이더"는
    //  실드가 아니라 아래 궤적 섬광을 뜻했다. 사용자 확인 2026-07-25.)
    [Header("Ilseom Bloom")]
    public Material playerBloomMaterial;          // Custom/PlayerBloomOverlay (Assets/VFX/Parry/PlayerBloom.mat)
    public float ilseomBloomFadeOut = 0.35f;      // 시퀀스가 끝난 뒤 빛이 빠지는 시간
    public float ilseomBloomCancelFadeOut = 0.18f; // 차지가 취소됐을 때 더 빠르게 빠짐
    public int playerBloomSortingOffset = 1;

    // ── 처형(Execution) ─────────────────────────────────────────────────────────────────────
    // 적 체력이 executionHpThreshold(20%) 이하일 때 커서를 올리면 붉은 글로우 + UI 프롬프트가 뜨고,
    // R키를 누르면 Glitch Out → Glitch Slices(적 위치) → 이동 → Glitch Sweep(첫 프레임에 즉사 데미지)
    // 시퀀스가 발동한다. 대시-카운터·일섬과 동일한 try/finally 구조로 무적·상태를 항상 복원.
    [Header("Execution (처형)")]
    public bool executionEnabled = true;
    [Range(0f, 1f)] public float executionHpThreshold = 0.2f;  // 적 HP가 이 비율 이하면 처형 가능
    public Material enemyExecutionGlowMaterial;                 // Custom/EnemyExecutionGlow (Assets/VFX/Execution/EnemyExecutionGlow.mat)
    public int enemyGlowSortingOffset = 1;
    public float executionGlowFadeIn = 0.25f;                   // 커서를 댔을 때 글로우가 켜지는 시간
    public float executionGlowFadeOut = 0.2f;                   // 커서를 뗐을 때 글로우가 꺼지는 시간
    public float executionRushDuration = 0.12f;                 // 적 위치로 이동하는 시간(실시간 초)
    public float executionHold = 0.3f;                          // Sweep 후 여운(실시간 초)
    public string executionText = "처형됨!!";                   // 적에게 뜨는 텍스트

    // "가끔 이동이 막힌다"는 리포트의 원인을 현장에서 지목하기 위한 임시 진단(원인 확정 후 제거).
    [Header("Diagnostics (임시)")]
    public bool logMovementStall = true;
    public float stallLogThreshold = 0.3f; // 이 시간 이상 "입력은 있는데 안 움직임"이면 1회 로그

    Rigidbody2D rb;
    BoxCollider2D coll;
    Animator anim;
    SpriteRenderer sr;

    Vector2 moveInput;
    bool isJumping;
    bool isJumpHeld;
    float coyoteTimeCounter;

    bool isWallSliding;
    bool isTouchingWall;
    bool isGrounded;
    int wallDirX;

    float wallJumpLockCounter;
    bool wasGrounded;

    bool dashRequested;
    bool isDashing;
    float dashTimer;
    float dashCooldownCounter;
    int dashDirX = 1;
    int normalLayer;
    int invincibleLayer;

    float afterImageTimer;
    int afterImageIndex;

    bool attackQueued;
    float attackQueueTime;
    bool isAttacking;
    int attackStage = 1; // 현재(공격 중) 또는 다음(대기 중) 발동될 콤보 타수 — 공격 종료 시 다음 타수로 순환됨
    float attackTimer;
    bool attackHitDone;
    float lastAttackEndTime = -999f;
    SectionCamera sectionCamera;

    float stallTimer;
    bool stallLogged;

    bool isDodgeCountering;
    bool dodgeCounterTriggeredThisDash;
    bool parryPressed;
    float dodgeCounterGraceTimer; // 대시 시작 시 dodgeCounterGraceWindow로 세팅, 대시 지속시간과 무관하게 독립 카운트다운

    bool chargeHeld;               // 우클릭 홀드 상태(OnCharge가 press/release로 갱신)
    bool chargeStartRequested;     // 홀드 시작 엣지 — Update에서 한 번 소비
    bool cancelChargeRequested;    // 차지 중 들어온 대시/좌클릭 "새 입력"만 취소로 인정(묵은 버퍼로 즉시 취소되는 것 방지)
    bool isCharging;
    float chargeTimer;
    bool chargeCompletePopped;     // 2초 도달 시 Resistance_Up을 이미 띄웠는지
    bool chargeVisualsStarted;     // 차지 연출(애니 고정 · 픽셀 FX · 블룸)이 켜졌는지 — 패링 탭 구간엔 안 켠다
    bool ilseomActive;             // 발동 시퀀스 진행 중(무적 + 충돌 무시)
    float ilseomCooldownCounter;
    IlseomChargeFx chargeFx;
    PlayerBloomFx bloomFx;         // 차지~발동 구간 동안 플레이어에 걸리는 블룸 오버레이

    bool isParrying;               // Slash 1 패링 모션 재생 중(이동·점프·대시·공격 잠금)
    float parryTimer;
    float parryCooldownCounter;
    bool parryShieldActive;        // 실드가 적 공격 1회를 막아줄 수 있는 상태인지(연출과 분리된 판정용 상태)
    ParryShieldFx parryShieldFx;
    InputAction chargeAction;      // PlayerActions "Charge" — 홀드 상태를 직접 폴링(PollChargeInput 주석 참고)

    bool isExecuting;              // 처형 시퀀스 진행 중(무적 + 이동/공격 잠금)
    DummyEnemy executionTarget;    // 현재 커서로 타겟팅 중인 적 (null이면 타겟 없음)
    EnemyExecutionGlowFx executionGlowFx; // 현재 적에게 붙어 있는 글로우 FX

    void Awake()
    {
        // 방어적 리셋: Time.timeScale은 에디터에서 Stop→Play를 반복해도 자동으로 1로
        // 초기화되지 않는 정적 값(도메인 리로드 전까지 유지)이라, 이전 Play 세션이 슬로우모션
        // 코루틴 도중 비정상 종료됐다면 다음 Play가 그 값을 그대로 물려받는다. 항상 정상 속도로 시작.
        Time.timeScale = 1f;

        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<BoxCollider2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        // Rigidbody2D 기본 셋팅
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        normalLayer = gameObject.layer;
        invincibleLayer = LayerMask.NameToLayer(invincibleLayerName);

        currentHp = maxHp;
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
        // 적 몸체와의 물리 충돌을 항상 제외한다. 예전엔 대시 중에만 제외했는데, 평상시 이동에서 적에게
        // 밀착하면 서로 밀어내느라 수평 속도가 죽어(실측: 5u/s → 0.96u/s, 약 80% 감소) 사용자가 본
        // "이동 중 갑자기 특정 방향으로 못 감(애니·flipX는 정상)" 증상이 발생했다 — moveInput은 정상
        // 수신되니 애니/flip은 그대로 돌고 좌표만 거의 안 변하는 정확한 시그니처. 전투 판정은 전부
        // Overlap 쿼리(excludeLayers 영향 없음)라 이 제외로 잃는 기능이 없다.
        rb.excludeLayers = rb.excludeLayers.value | enemyLayer.value;
        if (Camera.main != null) sectionCamera = Camera.main.GetComponent<SectionCamera>();

        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
            chargeAction = playerInput.actions.FindAction("Charge");
        if (chargeAction == null)
            Debug.LogWarning("[Ilseom] PlayerActions에 \"Charge\" 액션이 없어 우클릭을 직접 폴링합니다.");
    }

    void Update()
    {
        if (wallJumpLockCounter > 0f) wallJumpLockCounter -= Time.deltaTime;
        if (dashCooldownCounter > 0f) dashCooldownCounter -= Time.deltaTime;
        if (dodgeCounterGraceTimer > 0f) dodgeCounterGraceTimer -= Time.deltaTime;

        CheckEnvironment();
        // 패링 타이머는 일섬보다 먼저 굴린다 — HandleIlseom이 패링을 시작하는 그 프레임에 타이머가
        // 한 번 가산돼 모션이 그만큼 짧아지는 것을 막는다(일섬 차지에서 겪었던 것과 같은 함정).
        HandleParry();
        // 일섬은 대시/공격보다 먼저 본다 — 차지를 취소한 그 입력이 같은 프레임에 정상 발동돼야 하기 때문(사용자 확정).
        HandleIlseom();
        HandleJump();
        HandleWallSlide();
        HandleDash();
        HandleAttack();
        HandleExecution();
        UpdateAnimations();
        CheckMovementStall();
    }

    // ── 임시 진단: "이동 입력은 있는데 실제로 안 움직임"을 잡아 원인을 콘솔에 지목한다 ──────────
    // 이동을 막을 수 있는 경로는 (a) 코드 상태 잠금(공격/회피카운터/대시/벽점프 잠금)과
    // (b) 물리 충돌 둘뿐이다. 어느 쪽인지 매번 추측하지 않으려고 스톨이 감지되면 그 순간의
    // 상태와 수평 접촉 콜라이더를 한 번만 로그로 남긴다. 원인 확정 후 제거할 코드.
    void CheckMovementStall()
    {
        if (!logMovementStall) return;
        // 일섬 차지/발동 중 정지는 스펙대로 의도된 잠금이라 스톨이 아니다(A/D를 눌러도 방향만 바뀜).
        if (isCharging || ilseomActive || isExecuting) { stallTimer = 0f; stallLogged = false; return; }

        bool wantsMove = Mathf.Abs(moveInput.x) > 0.01f;
        bool moving = Mathf.Abs(rb.linearVelocity.x) > 0.5f;
        if (!wantsMove || moving) { stallTimer = 0f; stallLogged = false; return; }

        stallTimer += Time.unscaledDeltaTime;
        if (stallTimer < stallLogThreshold || stallLogged) return;
        stallLogged = true;

        string cause;
        if (isParrying) cause = "패링 모션 중(isParrying, timer=" + parryTimer.ToString("F2") + "/" + parryMotionDuration.ToString("F2") + ")";
        else if (isDodgeCountering) cause = "회피-카운터 시퀀스 중(isDodgeCountering)";
        else if (isAttacking) cause = "공격 중(isAttacking, stage=" + attackStage + " timer=" + attackTimer.ToString("F2") + "/" + (attackStage == 1 ? attack1Duration : attack2Duration).ToString("F2") + ")";
        else if (isDashing) cause = "대시 중(isDashing, dir=" + dashDirX + ")";
        else if (wallJumpLockCounter > 0f) cause = "벽점프 수평잠금(wallJumpLockCounter=" + wallJumpLockCounter.ToString("F2") + ")";
        else
        {
            // 코드 잠금이 아니면 물리 — 무엇이 수평으로 막고 있는지 접촉점에서 직접 조회
            string blockers = "";
            ContactPoint2D[] contacts = new ContactPoint2D[16];
            int n = rb.GetContacts(contacts);
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(contacts[i].normal.x) < 0.5f) continue; // 수평으로 밀어내는 접촉만
                blockers += contacts[i].collider.gameObject.name
                    + "(layer=" + LayerMask.LayerToName(contacts[i].collider.gameObject.layer)
                    + " n=" + contacts[i].normal.ToString("F1") + ") ";
            }
            cause = "물리 충돌 — 수평 접촉=[" + (blockers.Length > 0 ? blockers : "없음(원인 불명)") + "]";
        }

        Debug.LogWarning("[STALL] 이동 막힘 " + stallTimer.ToString("F2") + "s | 원인: " + cause
            + " | moveInput.x=" + moveInput.x.ToString("F2") + " vel=" + rb.linearVelocity.ToString("F2")
            + " pos=" + transform.position.ToString("F2")
            + " grounded=" + isGrounded + " touchWall=" + isTouchingWall + " wallDirX=" + wallDirX
            + " timeScale=" + Time.timeScale.ToString("F2"));
    }

    void FixedUpdate()
    {
        if (isDodgeCountering)
        {
            // 회피 성공 직후엔 연장된 대시가 계속 진행 중이어야 함(끊기지 않고 슬로우모션과 함께
            // 미끄러지듯 나아감, UniTrio ExtendDash 참고) — DodgeCounterRoutine이 연장 시간을 다 쓰면
            // isDashing=false로 내려주므로, 그 이후(CounterRush의 Lerp 이동/명중 후 대기)엔 물리 속도를
            // 0으로 고정해 CounterRush의 transform.position 직접 제어와 충돌하지 않게 한다.
            rb.linearVelocity = isDashing ? new Vector2(dashDirX * dashSpeed, 0f) : Vector2.zero;
        }
        else if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirX * dashSpeed, 0f);
        }
        else if (ilseomActive)
        {
            // 발동 시퀀스는 IlseomRoutine이 transform.position을 직접 보간해 이동시킨다 —
            // 물리 속도를 0으로 완전히 묶어 중력·잔여 속도가 그 보간과 싸우지 않게 한다.
            rb.linearVelocity = Vector2.zero;
        }
        else if (isExecuting)
        {
            // 처형 시퀀스는 ExecutionRoutine이 transform.position을 직접 보간해 이동시킨다 —
            // 물리 속도를 0으로 완전히 묶어 중력·잔여 속도가 그 보간과 싸우지 않게 한다.
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            HandleMovement();
        }
        ApplyBetterJumpPhysics();
    }

    void CheckEnvironment()
    {
        Bounds bounds = coll.bounds;
        isGrounded = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.down, 0.1f, groundLayer);
        
        bool rightWall = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.right, 0.1f, wallLayer);
        bool leftWall = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.left, 0.1f, wallLayer);
        isTouchingWall = rightWall || leftWall;
        wallDirX = rightWall ? 1 : (leftWall ? -1 : 0);

        if (isGrounded) 
        {
            coyoteTimeCounter = coyoteTime;
        } 
        else 
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    void HandleMovement()
    {
        // 벽 점프 직후에는 수평 입력을 잠시 잠가 벽 반대 방향으로 확실히 밀어냄
        if (wallJumpLockCounter > 0f) return;

        // 공격 중 · 회피-카운터 중 · 일섬 차지 중 · 패링 모션 중엔 제자리에 멈춤 (이동 입력 무시, 수평
        // 속도만 고정 — 차지 중에도 중력은 그대로 살아 있어 공중에서 모으면 떨어진다)
        if (isAttacking || isDodgeCountering || isCharging || isParrying || isExecuting)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 가속 없이 즉시 목표 속도로 (뚝뚝 끊기는 조작감)
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
    }

    void HandleWallSlide()
    {
        if (isDashing || isCharging || ilseomActive || isParrying || isExecuting) { isWallSliding = false; return; }

        // 벽 방향 키를 누르고 있는 동안만 벽에 붙어 슬라이드 (즉시 이동과 궁합: 접촉 유지 안정화)
        bool pushingIntoWall = isTouchingWall && wallDirX != 0
            && Mathf.Abs(moveInput.x) > 0.01f && Mathf.Sign(moveInput.x) == wallDirX;

        if (pushingIntoWall && !isGrounded)
        {
            isWallSliding = true; // 벽 접촉 + 키 감지 순간 즉시 슬라이드
            // 하강 시 가속하며 최대 슬라이드 속도로 수렴 (상승 중이면 점프 유지)
            if (rb.linearVelocity.y < 0f)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                    Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideSpeed, wallSlideAccel * Time.deltaTime));
        }
        else
        {
            isWallSliding = false;
        }
    }

    void HandleJump()
    {
        if (isJumping) {
            // 공격/회피-카운터/일섬 중엔 점프로 캔슬할 수 없음 — 입력은 버림.
            // (스펙 3의 취소 수단은 대시·좌클릭뿐이므로 점프는 차지를 깨지 않고 그냥 무시된다)
            if (isAttacking || isDodgeCountering || isCharging || ilseomActive || isParrying || isExecuting) { isJumping = false; return; }
            if (coyoteTimeCounter > 0f) {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                coyoteTimeCounter = 0f;
            }
            else if (isTouchingWall && Mathf.Abs(moveInput.x) > 0.01f && Mathf.Sign(moveInput.x) == wallDirX) {
                // 벽 방향 키를 누르고 있을 때만 벽 점프 (벽 반대 방향 + 약간 위)
                rb.linearVelocity = new Vector2(-wallDirX * wallJumpForce.x, wallJumpForce.y);
                wallJumpLockCounter = 0.15f;
            }
            isJumping = false;
        }
    }

    void HandleDash()
    {
        // 회피-카운터 시퀀스 동안은 대시 타이머를 동결(코루틴이 직접 관리, EndDash로 종료)
        if (isDodgeCountering) return;

        if (dashRequested)
        {
            dashRequested = false;
            // 공격 중 · 일섬 발동 중 · 패링 모션 중엔 대시로 캔슬할 수 없음(입력은 여기서 버려진다)
            if (!isDashing && !isAttacking && !ilseomActive && !isParrying && !isExecuting && dashCooldownCounter <= 0f)
            {
                isDashing = true;
                dashTimer = dashDuration;
                dashCooldownCounter = dashCooldown;
                dashDirX = Mathf.Abs(moveInput.x) > 0.01f
                    ? (int)Mathf.Sign(moveInput.x)
                    : ((sr != null && sr.flipX) ? -1 : 1);

                if (invincibleLayer != -1) gameObject.layer = invincibleLayer;
                // (적 통과는 이제 Awake에서 상시 적용 — 대시에서만 켜고 끄지 않는다)
                afterImageTimer = 0f;
                afterImageIndex = 0;
                dodgeCounterGraceTimer = dodgeCounterGraceWindow; // 닷지 트리거 창(대시 길이보다 길게 유예)
                if (dashFreezeAnim) FreezeDashAnim();

                // 히트스톱: 대시 시작 순간 짧게 시간정지 → 무게감
                if (dashHitstop) StartCoroutine(DashHitstop());

                TestLog.Event("dash_iframe", $"dash_started dir={dashDirX}");
            }
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            if (dashAfterImage)
            {
                afterImageTimer -= Time.deltaTime;
                if (afterImageTimer <= 0f)
                {
                    SpawnAfterImage();
                    afterImageTimer = afterImageInterval;
                }
            }

            // 진행 방향 벽에 부딪히면 남은 시간 무시하고 즉시 종료.
            // (벽에 0.15s 내내 처박는 낭비 제거 → 벽 붙은 뒤 반대/낙하로 즉시 복귀)
            bool intoWall = isTouchingWall && wallDirX != 0 && wallDirX == dashDirX;

            if (dashTimer <= 0f || intoWall)
            {
                EndDash(intoWall ? "dash_cancelled_wall" : "dash_ended");
            }
        }
    }

    // ── 일섬: 차지 판정 ─────────────────────────────────────────────────────────────────────
    void HandleIlseom()
    {
        PollChargeInput();

        if (ilseomCooldownCounter > 0f)
        {
            ilseomCooldownCounter -= Time.deltaTime;
            if (ilseomCooldownCounter <= 0f)
            {
                ilseomCooldownCounter = 0f;
                SpawnBuffPop(); // 스펙 8: 쿨타임이 가득 차면 충전 완료와 똑같이 머리 위에 Resistance_Up
                TestLog.Event("ilseom", "cooldown_ready");
            }
        }

        if (ilseomActive) { chargeStartRequested = false; cancelChargeRequested = false; return; }

        if (chargeStartRequested)
        {
            chargeStartRequested = false;
            // 시작한 프레임에는 타이머를 더하지 않고 그냥 빠진다. 아래 chargeTimer += Time.deltaTime을
            // 같은 프레임에 이어서 실행하면 "차지 시작 전"의 프레임 간격이 통째로 한 번 가산돼
            // 그만큼 일찍 완충된다 — 프레임이 튀면 Time.maximumDeltaTime(0.333s)까지 커져서
            // 2초 차지가 1.67초에 끝나는 것을 라이브 실측으로 확인(2026-07-25).
            if (CanStartCharge()) { StartCharge(); return; }
        }

        if (!isCharging) { cancelChargeRequested = false; return; }

        // 취소(대시·좌클릭). 그 입력 자체는 소비하지 않으므로 같은 프레임의 HandleDash/HandleAttack이 정상 발동시킨다.
        if (cancelChargeRequested)
        {
            cancelChargeRequested = false;
            CancelCharge("charge_cancelled_input");
            return;
        }

        chargeTimer += Time.deltaTime;

        // 누른 직후 parryTapMaxHold 동안은 "패링일 수도 있는" 구간이라 차지 연출을 켜지 않는다.
        // (탭할 때마다 픽셀 FX가 깜빡이고 취소 이펙트까지 터지는 것을 막는다.)
        if (!chargeVisualsStarted && chargeTimer >= parryTapMaxHold) BeginChargeVisuals();

        // A/D는 flipX만 바꾼다 — 이 flipX가 일섬 방향(true=왼쪽, false=오른쪽)을 결정한다.
        if (moveInput.x > 0.01f) sr.flipX = false;
        else if (moveInput.x < -0.01f) sr.flipX = true;

        if (!chargeCompletePopped && chargeTimer >= ilseomChargeTime)
        {
            chargeCompletePopped = true;
            SpawnBuffPop();
            TestLog.Event("ilseom", "charge_complete");
        }

        UpdateChargeFx();

        if (!chargeHeld)
        {
            if (chargeTimer >= ilseomChargeTime)
            {
                isCharging = false;
                StartCoroutine(IlseomRoutine());
            }
            // 탭(짧게 눌렀다 뗌) = 패링. 아직 차지 연출이 시작되기 전이라 조용히 정리하고 넘긴다.
            else if (chargeTimer <= parryTapMaxHold)
            {
                CancelCharge("charge_cancelled_tap");
                TryParry();
            }
            else CancelCharge("charge_cancelled_early");
        }
    }

    bool CanStartCharge()
    {
        return ilseomEnabled && !isCharging && !ilseomActive && !isDashing && !isAttacking
            && !isDodgeCountering && !isParrying && !isExecuting && ilseomCooldownCounter <= 0f;
    }

    // 누르는 순간엔 아직 패링(탭)인지 일섬(홀드)인지 알 수 없다 — 상태만 열어두고 연출은 뒤로 미룬다.
    void StartCharge()
    {
        isCharging = true;
        chargeTimer = 0f;
        chargeCompletePopped = false;
        chargeVisualsStarted = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        TestLog.Event("ilseom", "charge_start");
    }

    // parryTapMaxHold를 넘겨 계속 쥐고 있으면 그때부터 일섬 차지 연출을 시작한다.
    void BeginChargeVisuals()
    {
        chargeVisualsStarted = true;

        // 홀드 중에는 Glitch Out의 0프레임에 고정(대시 프리즈와 동일한 방식 — 애니메이터를 꺼서
        // AnyState 전이가 프레임을 덮어쓰지 못하게 한다).
        FreezeAnimAt(ilseomChargeState, ilseomChargeFreezeFrame, ilseomChargeFrameCount);

        chargeFx = IlseomChargeFx.Attach(transform, ilseomChargePixelMaterial, ilseomChargePixelCount,
            GatherOffset(), sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 1);

        // 스펙 6 확장: 차지~발동 구간에 플레이어 자체가 빛난다. 세기는 UpdateChargeFx가 차지 진행도로
        // 매 프레임 먹이므로 여기서는 0에서 시작만 시켜두면 그대로 페이드 인이 된다.
        bloomFx = PlayerBloomFx.Attach(transform, playerBloomMaterial, playerBloomSortingOffset);

        TestLog.Event("ilseom", "charge_visuals_start");
    }

    void UpdateChargeFx()
    {
        float p = Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, ilseomChargeTime));

        if (chargeFx != null)
        {
            chargeFx.SetGatherOffset(GatherOffset());
            // 경과 시간(흐름)과 진행도(세기)를 따로 넘긴다 — 완충 후에도 픽셀이 계속 모여들어야 하므로.
            chargeFx.SetCharge(chargeTimer, p);
        }

        // 블룸은 차지 진행도를 그대로 따라간다 → 0에서 시작해 완충에서 최대(페이드 인).
        if (bloomFx != null) bloomFx.SetIntensity(p);

        // 쉐이크 램프는 차지 전체(0→ilseomChargeTime)에 걸쳐 0배→ilseomChargeShakeMultiplier배로 오르고,
        // ilseomChargeShakeStartDelay 이전에는 적용하지 않는다(스펙 5의 두 문장을 동시에 만족시키는 해석).
        // 기준 세기는 피격 쉐이크와 같은 attackShakeMagnitude라 그쪽 튜닝을 그대로 따라간다.
        if (sectionCamera != null)
        {
            float mag = chargeTimer >= ilseomChargeShakeStartDelay
                ? attackShakeMagnitude * ilseomChargeShakeMultiplier * p
                : 0f;
            sectionCamera.SetSustainedShake(mag);
        }
    }

    void CancelCharge(string reason)
    {
        isCharging = false;
        chargeTimer = 0f;
        chargeCompletePopped = false;

        // 연출이 시작되기 전(패링 탭 구간)에 취소되면 애니메이터를 건드리지 않는다 — Idle로 강제
        // 복귀시키면 곧바로 재생할 패링 모션(Slash 1) 앞에 한 프레임짜리 Idle이 끼어든다.
        if (chargeVisualsStarted)
        {
            RestoreAnimAfterIlseom();
            if (chargeFx != null) { chargeFx.PlayCancel(ilseomCancelFxDuration); chargeFx = null; }
            if (bloomFx != null) { bloomFx.FadeOut(ilseomBloomCancelFadeOut); bloomFx = null; }
        }
        chargeVisualsStarted = false;

        if (sectionCamera != null) sectionCamera.SetSustainedShake(0f);
        TestLog.Event("ilseom", reason);
    }

    // 픽셀이 모이는 지점 — 바라보는 방향의 살짝 위쪽(사용자 확정: flipX에 따라 미러링).
    Vector2 GatherOffset()
    {
        float dirX = (sr != null && sr.flipX) ? -1f : 1f;
        return new Vector2(ilseomGatherOffset.x * dirX, ilseomGatherOffset.y);
    }

    void SpawnBuffPop()
    {
        if (ilseomBuffPopPrefab == null) return;
        var pop = Instantiate(ilseomBuffPopPrefab, transform.position + Vector3.up * ilseomBuffPopHeight, Quaternion.identity);
        pop.transform.localScale *= ilseomBuffPopScale;
    }

    // ── 패링 ────────────────────────────────────────────────────────────────────────────────
    void HandleParry()
    {
        if (parryCooldownCounter > 0f) parryCooldownCounter -= Time.deltaTime;

        if (!isParrying) return;
        parryTimer += Time.deltaTime;
        if (parryTimer >= parryMotionDuration) isParrying = false;
    }

    // 우클릭 탭으로 진입. 모션(Slash 1)은 성공/실패와 무관하게 항상 재생되고, 판정이 성립하면
    // 그 공격을 무효화(스펙 5) + 겹침 지점에 연출(스펙 4) + 구형 실드(스펙 6)까지 이어진다.
    // 데미지는 주지 않는다 — isAttacking을 세우지 않으므로 클립의 AttackHitFrame 이벤트는 무시된다.
    void TryParry()
    {
        if (!parryEnabled || parryCooldownCounter > 0f) return;

        isParrying = true;
        parryTimer = 0f;
        if (anim != null) { anim.enabled = true; anim.SetTrigger("Attack1"); }

        Vector2 contact;
        DummyEnemy target = FindParryTarget(out contact);
        if (target == null)
        {
            parryCooldownCounter = parryFailCooldown;
            TestLog.Event("parry_timing", "parry_miss");
            return;
        }

        target.ConsumeParry();

        Vector3 fxPos = (Vector3)contact + Vector3.up * parryFxHeightOffset;
        Vector2 facing = (contact - (Vector2)transform.position).normalized;
        if (facing.sqrMagnitude < 0.01f) facing = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;
        // 크리티컬과 같은 프리팹·색을 그대로 쓴다(사용자 스펙) — 위치는 이미 정확하므로 추가 오프셋 0.
        CombatFx.SpawnHitVfx(critHitVfxPrefab, fxPos, facing, 0f);
        CombatFx.SpawnDamageText(damageTextPrefab, fxPos, parryText, critTextColor, true);

        // 막아낸 지점으로 카메라가 잠깐 파고든다(대시-카운터와 같은 FocusPulse, 훨씬 짧게).
        if (sectionCamera != null)
            sectionCamera.FocusPulse(fxPos, parryCamPanAmount, parryCamZoomAmount,
                focusPulseRampIn, focusPulseHold, focusPulseRampOut);

        SpawnParryShield();
        TestLog.Event("parry_timing", $"parry_success enemy={target.name} at={contact.ToString("F2")}");
    }

    // 스펙 3의 성공 조건을 만족하는 적을 찾는다.
    // contact = 적 공격 원의 중심을 1타 히트박스 안으로 클램프한 점 — 원이 박스 밖이면 박스 경계의
    // 최근접점, 안이면 원 중심 그 자체가 되어 "두 범위가 겹치는 영역의 중앙"이 된다(스펙 4).
    DummyEnemy FindParryTarget(out Vector2 contact)
    {
        contact = Vector2.zero;

        Vector2 facing = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;
        Vector2 boxCenter = (Vector2)transform.position + facing * attackHitboxDistance;
        Vector2 half = attackHitboxSize * 0.5f;

        // 적 몸통은 창 길이(1.9)만큼 떨어져 있어 1타 히트박스로 직접 훑으면 못 잡는다 → 후보만 넓게
        // 모으고, 실제 판정은 "적의 공격 범위(창끝 원) vs 1타 히트박스"로 한다.
        Collider2D[] found = Physics2D.OverlapCircleAll(transform.position, parrySearchRadius, enemyLayer);
        for (int i = 0; i < found.Length; i++)
        {
            DummyEnemy e = found[i].GetComponent<DummyEnemy>();
            if (e == null || !e.IsAttacking) continue;
            // (B) 아직 그 공격에 맞지 않았거나, (A) 대시 회피 인정 창이 열려 있음
            //     — (A)만 성립하는 경우 = 대시 무적으로 이미 흘려낸 공격을 유예 중에 되받아치는 상황.
            if (!e.IsAttackUnresolved && dodgeCounterGraceTimer <= 0f) continue;

            Vector2 c = e.AttackHitPoint;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(c.x, boxCenter.x - half.x, boxCenter.x + half.x),
                Mathf.Clamp(c.y, boxCenter.y - half.y, boxCenter.y + half.y));
            if ((c - clamped).sqrMagnitude > e.AttackHitRadius * e.AttackHitRadius) continue;

            contact = clamped;
            return e;
        }
        return null;
    }

    void SpawnParryShield()
    {
        parryShieldActive = true;
        // 이미 실드가 있으면 새 것으로 갈아끼운다(중첩 방어가 아니라 갱신 — 스펙은 항상 "1회"다).
        if (parryShieldFx != null) Destroy(parryShieldFx.gameObject);
        parryShieldFx = ParryShieldFx.Attach(transform, parryShieldMaterial, parryShieldRadius,
            parryShieldOffset, parryShieldBreakDuration,
            sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + parryShieldSortingOffset);
        TestLog.Event("parry_timing", "shield_up");
    }

    // 실드가 살아 있으면 적 공격 1회를 대신 막고 유리처럼 깨진다(스펙 6).
    // DummyEnemy가 피해 확정 직전에 호출한다 — true면 그 공격은 데미지도 데미지 텍스트도 없다.
    // 판정 상태(parryShieldActive)를 연출 오브젝트와 분리해 둔 이유: 셰이더/머티리얼을 못 찾아
    // 연출이 생성되지 않아도 "1회 막아준다"는 기능 자체는 그대로 살아 있어야 하기 때문.
    public bool TryConsumeParryShield()
    {
        if (!parryShieldActive) return false;
        parryShieldActive = false;
        if (parryShieldFx != null) { parryShieldFx.Break(); parryShieldFx = null; }
        TestLog.Event("parry_timing", "shield_blocked");
        return true;
    }

    public bool HasParryShield => parryShieldActive;

    // ── 일섬: 발동 시퀀스 ───────────────────────────────────────────────────────────────────
    // Glitch Out → 투명 이동 → Glitch Sweep(0프레임에 처형 피격) → 무적 해제.
    // ★ try/finally로 무적·레이어·알파·flipX·쉐이크를 항상 복원(DodgeCounterRoutine과 같은 구조적 안전망).
    System.Collections.IEnumerator IlseomRoutine()
    {
        ilseomActive = true;
        ilseomCooldownCounter = ilseomCooldown; // 발동이 확정된 순간 쿨타임 시작

        int dirX = (sr != null && sr.flipX) ? -1 : 1;
        Color baseColor = sr != null ? sr.color : Color.white;

        if (sectionCamera != null) sectionCamera.SetSustainedShake(0f);
        if (chargeFx != null) { chargeFx.PlayFinish(ilseomFinishFxDuration); chargeFx = null; } // 모인 픽셀은 그 자리에서 페이드아웃
        if (invincibleLayer != -1) gameObject.layer = invincibleLayer;
        rb.linearVelocity = Vector2.zero;
        LockAnimForIlseom();

        if (bloomFx != null) bloomFx.SetIntensity(1f); // 완충 세기 유지 — 시퀀스가 끝날 때 페이드 아웃

        TestLog.Event("ilseom", "fire dir=" + dirX);

        try
        {
            // 1) Glitch Out 전체 재생
            PlayIlseomState(ilseomChargeState, dirX, false);
            yield return new WaitForSeconds(ilseomGlitchOutDuration);

            // 2) 목표 지점과 경로 위의 적을 먼저 확정한다(이동 중 적이 움직여도 판정이 흔들리지 않게).
            Vector3 start = transform.position;
            float maxDist = dashSpeed * dashDuration * ilseomDistanceMultiplier;
            var targets = new System.Collections.Generic.List<DummyEnemy>();
            Vector3 end = ResolveIlseomPath(start, dirX, maxDist, targets);

            // 3) 궤적 섬광을 먼저 깔고(경로가 확정된 직후) 투명해지며 이동한다.
            // 픽셀 스냅 단위는 플레이어와 같은 1/32 × 현재 스케일로 맞춰 도트 크기를 통일한다.
            float pixelSize = transform.lossyScale.x / 32f;
            SpawnIlseomStreak(start, end, pixelSize);

            float t = 0f;
            while (t < ilseomMoveDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / ilseomMoveDuration);
                transform.position = Vector3.Lerp(start, end, k);
                if (sr != null)
                {
                    Color c = baseColor;
                    c.a = baseColor.a * Mathf.Lerp(1f, ilseomMoveAlpha, Mathf.Sin(k * Mathf.PI * 0.5f));
                    sr.color = c;
                }
                yield return null;
            }
            transform.position = end;
            if (sr != null) sr.color = baseColor; // 투명화 해제

            // 4) Glitch Sweep — 첫 프레임에 경로 위 모든 적에게 처형 피격
            PlayIlseomState(ilseomSweepState, dirX, true);
            // 베는 순간 카메라가 도착 지점으로 잠깐 파고든다(적이 없어도 걸리도록 피해 처리와 분리).
            if (sectionCamera != null)
                sectionCamera.FocusPulse(end, ilseomCamPanAmount, ilseomCamZoomAmount,
                    focusPulseRampIn, focusPulseHold, focusPulseRampOut);
            ApplyIlseomDamage(targets, dirX);
            yield return new WaitForSeconds(ilseomSweepDuration);
        }
        finally
        {
            ilseomActive = false;
            if (invincibleLayer != -1) gameObject.layer = normalLayer;
            if (sr != null) { sr.color = baseColor; sr.flipX = (dirX < 0); } // Sweep의 반전 flipX를 원상복구
            RestoreAnimAfterIlseom();
            if (sectionCamera != null) sectionCamera.SetSustainedShake(0f);
            // 어떤 경로(정상 종료/중단/예외)로 끝나도 연출이 남지 않게 항상 정리한다.
            if (bloomFx != null) { bloomFx.FadeOut(ilseomBloomFadeOut); bloomFx = null; }
            TestLog.Event("ilseom", "end");
        }
    }

    // 궤적 섬광을 이동 경로에 여러 겹으로 깐다(스펙 6 확장 "더 길게 그리고 더 많이, 이동 경로 내에 더 표현").
    // 겹마다 두께와 수명이 달라 바깥 겹이 더 두껍고 더 오래 남아 번지는 잔광처럼 보인다.
    // IlseomSlashFx가 겹마다 새 머티리얼 인스턴스에 무작위 시드를 넣으므로 스피드 라인 패턴도 겹마다 다르다.
    void SpawnIlseomStreak(Vector3 start, Vector3 end, float pixelSize)
    {
        int layers = Mathf.Max(1, ilseomStreakLayers);
        int layerID = sr != null ? sr.sortingLayerID : 0;
        int baseOrder = (sr != null ? sr.sortingOrder : 0) + ilseomStreakSortingOffset;

        for (int i = 0; i < layers; i++)
        {
            float k = layers == 1 ? 0f : (float)i / (layers - 1);
            float height = ilseomStreakHeight * Mathf.Lerp(1f, ilseomStreakLayerHeightSpread, k);
            float fade = ilseomStreakFade * Mathf.Lerp(1f, ilseomStreakLayerFadeSpread, k);
            // 두꺼운(바깥) 겹을 더 뒤에 깔아 얇은 코어가 위로 올라오게 한다.
            IlseomSlashFx.Spawn(start, end, height, pixelSize, ilseomStreakMaterial,
                ilseomStreakSweep, fade, layerID, baseOrder - i);
        }
        TestLog.Event("ilseom", "streak layers=" + layers);
    }

    // 멈출 지점을 정한다. 경로에 벽이 있으면 그 앞, 없으면 최대 사거리(사용자 변경: 적 위치는 정지에
    // 관여하지 않음 — 예전의 "가장 먼 적 뒤로" 규칙 제거). 지나가는 경로 안의 적은 여전히 targets에
    // 모아 Sweep 판정에 쓴다(피해는 유지, 정지 위치만 적과 무관).
    Vector3 ResolveIlseomPath(Vector3 start, int dirX, float maxDist,
        System.Collections.Generic.List<DummyEnemy> targets)
    {
        Vector2 castDir = dirX > 0 ? Vector2.right : Vector2.left;
        Bounds b = coll.bounds;
        float dist = maxDist;

        // 벽 검사: 몸 크기를 살짝 줄여 캐스트해 지형에 스치는 오검출을 줄인다.
        // (wallLayer에는 Ground(9)+Wall(10)이 모두 들어 있어 지형 벽 전반이 잡힌다)
        Vector2 castSize = new Vector2(b.size.x * 0.9f, b.size.y * 0.8f);
        RaycastHit2D wall = Physics2D.BoxCast(b.center, castSize, 0f, castDir, maxDist, wallLayer);
        bool wallBlocked = wall.collider != null;
        if (wallBlocked) dist = Mathf.Max(0f, wall.distance - ilseomWallMargin);

        // 실제 이동 구간(0~dist) 안의 적을 전부 피해 대상으로 수집(정지 위치 계산에는 쓰지 않음).
        if (dist > 0.01f)
        {
            Vector2 boxCenter = (Vector2)b.center + castDir * (dist * 0.5f);
            Vector2 boxSize = new Vector2(dist + b.size.x, Mathf.Max(ilseomPathHeight, b.size.y));
            Collider2D[] found = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f, enemyLayer);
            for (int i = 0; i < found.Length; i++)
            {
                DummyEnemy e = found[i].GetComponent<DummyEnemy>();
                if (e == null || targets.Contains(e)) continue;
                targets.Add(e);
            }
        }

        TestLog.Event("ilseom", "path dist=" + dist.ToString("F2") + " max=" + maxDist.ToString("F2")
            + " wall=" + wallBlocked + " enemies=" + targets.Count);

        return start + (Vector3)(castDir * dist);
    }

    void ApplyIlseomDamage(System.Collections.Generic.List<DummyEnemy> targets, int dirX)
    {
        int dmg = Mathf.RoundToInt(attack1Damage * ilseomDamageMultiplier);
        Vector2 facing = dirX > 0 ? Vector2.right : Vector2.left;
        int hits = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            DummyEnemy e = targets[i];
            if (e == null) continue;
            e.TakeDamage(dmg, facing.x * attackLungeDistance * enemyKnockbackMultiplier);
            SpawnHitFeedback(e.transform.position, facing, dmg, HitTier.Execution); // 처형 VFX(Hit03) + 강조 텍스트
            hits++;
        }

        if (hits > 0)
        {
            if (attackHitstop) StartCoroutine(AttackHitstopCo(critHitstopMultiplier));
            if (attackScreenShake && sectionCamera != null)
                sectionCamera.Shake(attackShakeDuration, attackShakeMagnitude * critShakeMultiplier);
        }
        TestLog.Event("ilseom", "sweep_hit dmg=" + dmg + " hits=" + hits);
    }

    // 일섬 클립을 재생한다. invertFlip=true면 flipX를 반대로 준다 —
    // Glitch Sweep 시트가 원본부터 FlipX 되어 있기 때문(사용자 확인).
    void PlayIlseomState(string state, int dirX, bool invertFlip)
    {
        bool faceLeft = dirX < 0;
        if (sr != null) sr.flipX = invertFlip ? !faceLeft : faceLeft;
        if (anim == null) return;
        anim.enabled = true;
        anim.Play(state, 0, 0f);
        anim.Update(0f);
    }

    // 일섬/차지가 끝나면 애니메이터를 다시 켜고 기본 상태로 되돌린다.
    // Glitch Out/Sweep은 나가는 전이가 없는 고아 상태라 그냥 enabled만 되돌리면 그 상태에 계속 머물러
    // 마지막 프레임이 스프라이트에 남는다 — Idle로 직접 복귀시켜야 Idle↔Run 순환과 AnyState 전이가 살아난다.
    // 공중이었다면 다음 프레임에 UpdateAnimations가 파라미터를 다시 먹여 AnyState→Fall/Jump가 바로 받아간다.
    void RestoreAnimAfterIlseom()
    {
        if (anim == null) return;
        anim.enabled = true;
        anim.Play(ilseomExitState, 0, 0f);
        anim.Update(0f); // Idle 0프레임을 즉시 sr.sprite에 기록해 한 프레임도 남지 않게
    }

    // AnyState 전이(Fall/Jump/Wall Slide/Land/Attack)가 일섬 클립을 즉시 덮어쓰는 것을 막는다.
    // 조건을 전부 거짓으로 고정해두고, 이 구간엔 UpdateAnimations가 파라미터를 다시 안 건드린다.
    void LockAnimForIlseom()
    {
        if (anim == null) return;
        anim.SetFloat("Speed", 0f);
        anim.SetFloat("yVelocity", 0f);
        anim.SetBool("isGrounded", true);
        anim.SetBool("isWallSliding", false);
        anim.ResetTrigger("Land");
        anim.ResetTrigger("Attack1");
        anim.ResetTrigger("Attack2");
    }

    // ── 처형(Execution): 커서 감지 + R키 발동 ────────────────────────────────────────────────
    // 매 프레임 마우스 위치에서 적을 감지하고, 체력 조건을 확인해 글로우/UI를 제어한다.
    // R키 입력 시 ExecutionRoutine 코루틴을 시작한다.
    void HandleExecution()
    {
        if (!executionEnabled || isExecuting || isDashing || isDodgeCountering
            || ilseomActive || isCharging || isParrying || isAttacking) return;

        // 커서 아래 적 감지 — 2D 물리 레이캐스트
        DummyEnemy hoveredEnemy = null;
        if (Camera.main != null && Mouse.current != null)
        {
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
            Collider2D hit = Physics2D.OverlapPoint(worldPos, enemyLayer);
            if (hit != null) hoveredEnemy = hit.GetComponent<DummyEnemy>();
        }

        // 처형 가능한 적인지 확인
        bool validTarget = hoveredEnemy != null && hoveredEnemy.IsExecutable
            && (float)hoveredEnemy.currentHp / hoveredEnemy.maxHp <= executionHpThreshold;

        if (validTarget)
        {
            // 새 타겟이거나 타겟이 바뀌었으면 글로우를 교체
            if (executionTarget != hoveredEnemy)
            {
                ClearExecutionTargeting();
                executionTarget = hoveredEnemy;
                // 글로우 FX 붙이기
                executionGlowFx = EnemyExecutionGlowFx.Attach(
                    executionTarget.transform, enemyExecutionGlowMaterial, enemyGlowSortingOffset);
                if (executionGlowFx != null) executionGlowFx.FadeIn(1f, executionGlowFadeIn);
                // UI 페이드 인
                ExecutionUI.GetOrCreate().ShowPrompt();
            }

            // R키 입력 확인
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ExecutionUI.GetOrCreate().FlashHidePrompt();
                StartCoroutine(ExecutionRoutine(executionTarget));
            }
        }
        else if (executionTarget != null)
        {
            // 타겟이 무효해짐 — 글로우/UI 정리
            ClearExecutionTargeting();
        }
    }

    void ClearExecutionTargeting()
    {
        if (executionGlowFx != null) { executionGlowFx.FadeOut(executionGlowFadeOut); executionGlowFx = null; }
        ExecutionUI.GetOrCreate().HidePrompt();
        executionTarget = null;
    }

    // ── 처형: 발동 시퀀스 ────────────────────────────────────────────────────────────────────
    // Glitch Out → 적 위치에 Glitch Slices 스폰 + 적 쪽으로 이동 → Glitch Sweep(첫 프레임에 즉사 데미지).
    // ★ try/finally로 무적·레이어·상태를 항상 복원(DodgeCounterRoutine/IlseomRoutine과 같은 구조적 안전망).
    System.Collections.IEnumerator ExecutionRoutine(DummyEnemy target)
    {
        isExecuting = true;
        executionTarget = null; // 타겟팅 UI 정리(시퀀스 중엔 불필요)
        if (executionGlowFx != null) { executionGlowFx.FadeOut(0.1f); executionGlowFx = null; }

        int dirX = (sr != null && sr.flipX) ? -1 : 1;
        Color baseColor = sr != null ? sr.color : Color.white;

        // 무적 + 충돌 무시
        if (invincibleLayer != -1) gameObject.layer = invincibleLayer;
        rb.linearVelocity = Vector2.zero;
        LockAnimForIlseom(); // 일섬과 동일하게 AnyState 전이를 막는다

        TestLog.Event("execution", "start dir=" + dirX);

        try
        {
            // ── Phase 1: Glitch Out 재생 + Glitch Slices 스폰 + 적 위치로 이동 ──
            // 플레이어가 적을 바라보도록 방향 전환
            float dx = target.transform.position.x - transform.position.x;
            if (!Mathf.Approximately(dx, 0f))
            {
                dirX = dx > 0f ? 1 : -1;
                if (sr != null) sr.flipX = dirX < 0;
            }

            // Glitch Out 재생 (일섬과 동일)
            PlayIlseomState(ilseomChargeState, dirX, false);

            // Glitch Slices를 적 위치에 스폰 — Animator가 필요하므로 프리팹 대신 일섬과 같이 처리한다.
            // Glitch Slices는 고아 상태라 anim.Play로 바로 재생 가능.
            // 적의 SpriteRenderer에 잠시 Glitch Slices를 재생할 방법이 없으므로(적은 별도 애니메이터),
            // 플레이어의 position을 적에게 옮기는 것으로 자연스러운 연출을 만든다.
            // → 실제로는 Glitch Out이 재생되는 동안 이동이 진행된다.

            // 이동 목표: 적 위치 (y좌표는 대시-카운터와 동일 로직 — 차이가 작으면 유지)
            Vector3 start = transform.position;
            Vector3 targetPos = target.transform.position;
            // y좌표 보정: 대시-카운터와 동일하게 y 차이가 작으면 현재 y를 유지
            float yDiff = Mathf.Abs(targetPos.y - start.y);
            if (yDiff < 1.5f) targetPos.y = start.y; // 1.5 유닛 이내면 y 이동 불필요
            targetPos.z = start.z;

            // Glitch Out 재생 중에 이동
            float moveDur = ilseomGlitchOutDuration;
            float t = 0f;
            while (t < moveDur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / moveDur);
                transform.position = Vector3.Lerp(start, targetPos, k);
                yield return null;
            }
            transform.position = targetPos;

            // ── Phase 2: Glitch Sweep 재생 + 첫 프레임에 즉사 데미지 ──
            PlayIlseomState(ilseomSweepState, dirX, true);

            // 첫 프레임 즉시: 즉사 데미지 + "처형됨!!" 텍스트 + HitVFX03
            if (target != null && target.gameObject.activeInHierarchy)
            {
                // 무조건 즉사 데미지: 현재 HP + 여유분
                int lethalDamage = target.currentHp + 999;
                Vector2 facing = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;

                target.TakeDamage(lethalDamage, facing.x * attackLungeDistance * enemyKnockbackMultiplier);

                // HitVFX03 스폰
                CombatFx.SpawnHitVfx(executionHitVfxPrefab, target.transform.position, facing, hitVfxOffsetTowardsEnemy);

                // "처형됨!!" 붉은 텍스트 (숫자 표시 X — 문구만)
                CombatFx.SpawnDamageText(damageTextPrefab, target.transform.position,
                    executionText, executionTextColor, true);

                // 카메라 쉐이크 + 히트스톱 (크리티컬 배율 적용)
                if (sectionCamera != null)
                    sectionCamera.Shake(attackShakeDuration * critShakeMultiplier,
                        attackShakeMagnitude * critShakeMultiplier);
                if (attackHitstop) StartCoroutine(AttackHitstopCo(critHitstopMultiplier));

                TestLog.Event("execution", $"hit dmg={lethalDamage}");
            }

            // Sweep 재생 대기
            yield return new WaitForSeconds(ilseomSweepDuration);

            // 여운 (처형 후 잠시 멈춤)
            yield return new WaitForSecondsRealtime(executionHold);
        }
        finally
        {
            isExecuting = false;
            if (invincibleLayer != -1) gameObject.layer = normalLayer;
            if (sr != null) { sr.color = baseColor; sr.flipX = (dirX < 0); }
            RestoreAnimAfterIlseom(); // 일섬과 동일하게 애니메이터 복원
            ExecutionUI.GetOrCreate().HidePromptImmediate();
            TestLog.Event("execution", "end");
        }
    }

    // 대시 종료 공통 처리(정상 타임아웃/벽 취소/회피-카운터 종료 모두 여기로 모음).
    void EndDash(string reason)
    {
        isDashing = false;
        if (invincibleLayer != -1) gameObject.layer = normalLayer;
        if (dashFreezeAnim && anim != null) anim.enabled = true; // 애니메이터 재가동
        dodgeCounterTriggeredThisDash = false;
        TestLog.Event("dash_iframe", reason);
    }

    // 물리 무적(i-frame): 대시는 실제 지속시간 그대로(1주차 스펙 불변), 일섬/처형은 발동 시퀀스 전체.
    public bool IsInvincible => isDashing || ilseomActive || isExecuting;

    // DummyEnemy.CheckThrustHit가 찌르기가 실제로 닿는 순간 호출한다. 닷지 트리거는 dodgeCounterGraceTimer로
    // 판정 — 대시가 물리적으로 끝난 뒤에도 유예 시간 동안은 여전히 닷지로 잡아준다(타이밍 완화, 사용자 피드백).
    // 유예 중에는 대시가 끝나 무적이 풀린 상태일 수 있으므로, 닷지가 발동 못 하면 정상 피해가 그대로 들어간다
    // (공짜 무적 연장이 아니라 "잡아줄 기회의 창"만 넓어짐).
    public bool TryConsumeDodge(DummyEnemy attacker)
    {
        if (!dodgeCounterEnabled) return false;
        if (dodgeCounterGraceTimer <= 0f || dodgeCounterTriggeredThisDash || isDodgeCountering) return false;
        dodgeCounterTriggeredThisDash = true;
        StartCoroutine(DodgeCounterRoutine(attacker));
        return true;
    }

    // Phase 1: 슬로우모션 + 확인키(F, 보조 우클릭) 대기. Phase 2: 성공 시 CounterRush로 이어짐.
    // 실패(윈도우 경과)해도 페널티 없이 대시가 그대로 이어져 끝난다.
    // ★ try/finally로 timeScale·상태 복원을 항상 보장 — 중단/예외로 슬로우모션·입력잠금이 stuck되던 버그의 구조적 수정.
    System.Collections.IEnumerator DodgeCounterRoutine(DummyEnemy target)
    {
        isDodgeCountering = true;
        parryPressed = false;
        // 윈도우가 열리기 "전부터" F/우클릭을 이미 누르고 있던 경우(선입력) 구제: OnParry는 press 엣지
        // 이벤트라서 이미 눌려있는 버튼은 새 이벤트를 발생시키지 않아 위 리셋 이후 감지가 안 됨 — 그 결과
        // 윈도우가 조용히 만료될 때까지 반응이 없다가, 사용자가 떼었다 다시 눌러야 그제서야 잡히는 것처럼
        // 보였음("판정이 늦게 되는 것 같다" 버그의 실제 원인, 홀드 재현으로 확인). 윈도우가 열리는 시점의
        // 현재 홀드 상태를 한 번 직접 확인해 즉시 확인 처리한다.
        bool heldAtWindowOpen = (Keyboard.current != null && Keyboard.current.fKey.isPressed) ||
            (Mouse.current != null && Mouse.current.rightButton.isPressed);
        if (heldAtWindowOpen) parryPressed = true;
        // 회피 성공 순간 대시를 연장(기본 2배) — 즉시 멈추지 않고 슬로우모션과 함께 계속 미끄러지듯
        // 나아감(UniTrio ExtendDash 참고). isDashing은 이 연장 구간 동안만 true로 유지되고, 아래
        // while 루프에서 시간이 다 되면 자동으로 false로 내려간다(FixedUpdate가 그 시점부터 속도를 0으로).
        dashTimer = dashDuration * dodgeCounterDashExtendMultiplier;
        Color baseColor = sr != null ? sr.color : Color.white;

        try
        {
            if (sr != null) sr.color = dodgeCounterGlowColor;
            if (sectionCamera != null)
            {
                sectionCamera.Shake(dodgeCounterActivationShakeDuration, dodgeCounterActivationShakeMagnitude);
                if (target != null)
                    sectionCamera.FocusPulse(target.transform.position, dodgeCamPanAmount, dodgeCamZoomAmount,
                        dodgeGrayscaleRampIn, dodgeCounterInputWindow, dodgeGrayscaleRampOut);
            }
            DodgeUI.GetOrCreate().ShowPrompt();

            // 확인 대기창(실시간)과 연장된 대시(슬로우모션 스케일 시간)는 서로 다른 시계라 별개로 다뤄야 함.
            // 예: dashDuration*2=0.36s를 timeScale 0.15로 나누면 실시간 약 2.4초가 걸리는데, 확인창이
            // 2초(실시간)면 대시가 자기 타이머를 다 못 쓰고 창이 먼저 닫혀버림 — 예전엔 이 시점에 무조건
            // isDashing=false로 끊어버려서 "대시가 뚝뚝 끊기며 짧게 이동" 버그가 났었음. 이제 창이 닫혀도
            // 대시가 자기 타이머로 자연스럽게 끝날 때까지는 루프를 계속 돌려(입력만 더 안 받음) 항상
            // 의도한 전체 거리(2배)를 다 이동하도록 보장한다.
            bool confirmed = false;
            bool windowOpen = true;
            float elapsed = 0f;
            while (windowOpen || isDashing)
            {
                Time.timeScale = dodgeCounterSlowScale;

                if (windowOpen) elapsed += Time.unscaledDeltaTime;

                // 연장된 대시 진행(스케일된 시간이라 슬로우모션이 깊어질수록 더 천천히 줄어듦 —
                // "슬로우모션 속에서 계속 미끄러지는" 느낌). 다 되면 그 자리에서 자연스럽게 정지.
                // HandleDash()는 최상단에서 isDodgeCountering이면 통째로 return하므로(이 구간엔 안 들어옴)
                // 잔상 스폰 로직을 여기서 동일하게 반복해줘야 함 — 이게 빠져서 "회피 성공 후 잔상이
                // 안 생김" 버그였음(연장된 대시는 움직이는데 잔상 스폰 코드만 실행될 기회가 없었음).
                if (isDashing)
                {
                    dashTimer -= Time.deltaTime;
                    if (dashTimer <= 0f) { dashTimer = 0f; isDashing = false; }

                    if (dashAfterImage)
                    {
                        afterImageTimer -= Time.deltaTime;
                        if (afterImageTimer <= 0f)
                        {
                            SpawnAfterImage();
                            afterImageTimer = afterImageInterval;
                        }
                    }
                }

                SetDodgeGrayscale(Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, dodgeGrayscaleRampIn)));

                if (windowOpen)
                {
                    // 확인키: PlayerActions의 "Parry" 액션(F키+보조로 우클릭 둘 다 바인딩됨, OnParry 콜백이
                    // parryPressed를 세팅) — Unity Input System 이벤트 큐 기반이라 프레임 타이밍과 무관하게
                    // 정확히 한 번 전달됨. 예전엔 Keyboard.current.fKey를 직접 폴링했는데
                    // wasPressedThisFrame이 이 코루틴의 프레임 재개 시점과 어긋나 그 한 프레임을 놓쳐
                    // "F를 눌러도 반응 없음/여러 번 눌러야 늦게 반응" 버그가 있었음 — 이벤트 콜백 경로로
                    // 통일해 해결.
                    if (parryPressed)
                    {
                        // ★ 확인 즉시 카운터로 넘어간다(루프 탈출). 예전엔 창만 닫고 `isDashing`이 false가
                        // 될 때까지 루프를 계속 돌렸는데, 연장된 대시(dashDuration*2 = 0.36 게임초)는
                        // 슬로우모션(timeScale 0.15)에서 실시간 약 2.4초가 걸린다 → F를 눌러도 그 시간이
                        // 다 지나갈 때까지 카운터가 안 터지고, 사용자에겐 "F가 씹혔다가 1~2초 뒤에 다시
                        // 누르니 발동"으로 보였다(그 사이 첫 입력이 이미 확정돼 있었을 뿐). 확인이 들어온
                        // 순간엔 연장 대시를 더 끌 이유가 없으므로 대시를 종료하고 즉시 CounterRush로 간다.
                        confirmed = true; windowOpen = false;
                        isDashing = false;
                        break;
                    }
                    if (elapsed >= dodgeCounterInputWindow) { windowOpen = false; }
                }

                yield return null;
            }

            bool countered = confirmed && target != null;
            TestLog.Event("dodge_counter", countered ? "confirmed" : "window_expired");
            if (confirmed) DodgeUI.GetOrCreate().FlashHidePrompt();
            else DodgeUI.GetOrCreate().HidePromptImmediate();
            if (countered)
                yield return CounterRush(target);

            yield return GrayscaleRampOut();
        }
        finally
        {
            // 안전망: 램프아웃이 중단/예외로 못 끝나도 흑백이 화면에 stuck되지 않도록 즉시 하드 리셋
            // (Time.timeScale stuck 버그와 같은 종류의 문제를 사전 차단).
            if (GrayscaleRendererFeature.Instance != null) GrayscaleRendererFeature.Instance.Intensity = 0f;
            // 어떤 경로(성공/만료/예외/중단)로 끝나도 항상 복원 — 슬로우모션/입력잠금 stuck 방지
            Time.timeScale = 1f;
            if (sr != null) sr.color = baseColor;
            DodgeUI.GetOrCreate().HidePromptImmediate();
            EndDash("dodge_counter_end");
            isDodgeCountering = false;
        }
    }

    // 화면 흑백을 플레이어 위치 기준으로 gk(0~1)만큼 퍼뜨린다. GrayscaleRendererFeature가 URP 렌더러에
    // 등록 안 돼 있으면 Instance가 null이라 조용히 무시(에러 없음) — 등록 시 자동으로 살아남.
    void SetDodgeGrayscale(float gk)
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f == null) return;
        f.Intensity = gk;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up * 0.6f);
            f.Center = new Vector2(vp.x, vp.y);
        }
        f.Radius = Mathf.Lerp(0f, dodgeGrayscaleMaxRadius, gk);
    }

    System.Collections.IEnumerator GrayscaleRampOut()
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f == null) yield break;
        float from = f.Intensity;
        float dur = Mathf.Max(0.0001f, dodgeGrayscaleRampOut);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            f.Intensity = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / dur));
            yield return null;
        }
        f.Intensity = 0f;
    }

    // 적을 지나쳐 뒤로 돌진한 뒤 돌아서서 배율 데미지를 직접 적용한다(정상 히트박스를 거치지 않음 —
    // 돌진 거리 특성상 일반 판정 범위와 안 맞을 수 있어 UniTrio JustDodgeController와 동일하게 직접 적용).
    System.Collections.IEnumerator CounterRush(DummyEnemy target)
    {
        TestLog.Event("dodge_counter", "rush_start");
        Time.timeScale = 1f; // 카운터는 정상 속도로(슬로우모 해제) — 스냅있게

        Vector3 start = transform.position;

        // 더미는 창을 든 쪽(=transform.localScale.x 부호)이 바라보는 방향. "적 뒤"는 적이 바라보는
        // 반대편이므로 behindSide = -enemyFacing. 도착 후엔 그 자리에서 적을 바라봐야 하므로
        // 플레이어의 최종 방향은 enemyFacing과 같아진다(반대편에 서서 다시 적 쪽을 보게 되므로).
        float enemyFacing = Mathf.Sign(target.transform.localScale.x);
        if (Mathf.Approximately(enemyFacing, 0f)) enemyFacing = 1f;
        float behindSide = -enemyFacing;

        Vector3 behind = target.transform.position + new Vector3(behindSide * dodgeCounterRushPastDistance, 0f, 0f);
        behind.y = start.y; // y좌표 유지(수직 위치 안 바꿈) — 사용자 요청
        behind.z = start.z;

        if (anim != null) anim.enabled = true; // 대시 프리즈 해제(돌진 모션이 보이도록)
        rb.linearVelocity = Vector2.zero;

        // 카운터 러시 중 잔상 다량 생성(사용자 요청) — 러시가 0.12s로 워낙 짧아 "프레임당 1회 스폰"
        // 방식은 프레임레이트에 막혀 실제로는 몇 개 안 나가는 문제가 있었음. 경로를 미리 계산해
        // start→behind 사이를 균등 분할한 지점에 한 번에(같은 프레임 안, yield 없이) 깔아둔다 —
        // 프레임레이트/타임스케일과 무관하게 항상 동일한 밀도가 보장됨. 실제 이동은 텔레포트 후 즉시
        // 원위치 복구라 화면에는 순간이동이 안 보이고(같은 프레임 안이라 렌더 안 됨) 잔상만 경로에 남는다.
        int burstCount = Mathf.Max(4, Mathf.RoundToInt(dodgeCounterRushDuration / Mathf.Max(0.001f, dodgeCounterAfterImageInterval)));
        burstCount = Mathf.Min(burstCount, 40); // 과도한 스폰 방지
        for (int i = 0; i < burstCount; i++)
        {
            float bt = (float)i / (burstCount - 1);
            transform.position = Vector3.Lerp(start, behind, bt);
            SpawnAfterImage(dodgeCounterAfterImageLifetime);
        }
        transform.position = start; // 실제 이동은 아래 Lerp 루프가 다시 처음부터 담당

        float t = 0f;
        while (t < dodgeCounterRushDuration)
        {
            t += Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(start, behind, Mathf.Clamp01(t / dodgeCounterRushDuration));
            yield return null;
        }
        transform.position = behind;

        // 적을 바라봄 (적이 바라보는 방향과 같은 쪽)
        if (sr != null) sr.flipX = (enemyFacing < 0f);
        Vector2 facingBack = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;

        if (anim != null) anim.SetTrigger("Attack1"); // 시각적 스윙만(isAttacking=false라 AttackHitFrame 판정은 무시됨)

        // 닷지 카운터는 항상 크리티컬 취급(사용자 스펙) — 배율은 기존 dodgeCounterDamageMultiplier(3배) 그대로 쓰고,
        // 연출만 크리티컬과 동일하게(Hit02 VFX + 금색 2배 "숫자!!!" 텍스트 + 쉐이크/히트스톱 2배) 맞춘다.
        int dmg = Mathf.RoundToInt(attack1Damage * dodgeCounterDamageMultiplier);
        target.TakeDamage(dmg, facingBack.x * attackLungeDistance * enemyKnockbackMultiplier);
        SpawnHitFeedback(target.transform.position, facingBack, dmg, HitTier.Critical);
        float impactAngle = Mathf.Atan2(facingBack.y, facingBack.x) * Mathf.Rad2Deg;
        JustDodgeVFX.SpawnImpact(target.transform.position, impactAngle, dodgeImpactColor, dodgeImpactScale);

        if (sectionCamera != null) sectionCamera.Shake(dodgeCounterHitShakeDuration, dodgeCounterHitShakeMagnitude * critShakeMultiplier);
        StartCoroutine(DodgeCounterHitstopCo());
        TestLog.Event("dodge_counter", $"hit dmg={dmg} crit=True");

        yield return new WaitForSecondsRealtime(dodgeCounterHold);
    }

    System.Collections.IEnumerator DodgeCounterHitstopCo()
    {
        float prev = Time.timeScale;
        Time.timeScale = Mathf.Clamp01(dodgeCounterHitstopScale);
        // 카운터는 항상 크리티컬이므로 히트스톱도 2배(critHitstopMultiplier)
        yield return new WaitForSecondsRealtime(dodgeCounterHitstopDuration * critHitstopMultiplier);
        Time.timeScale = prev;
    }

    // 대시 중 Run 애니를 지정 프레임에 고정한다 (산데비스탄 잔상이 같은 실루엣을 남기도록).
    void FreezeDashAnim()
    {
        FreezeAnimAt(dashFreezeState, dashFreezeFrame, dashFreezeFrameCount);
    }

    // 지정 상태의 지정 프레임에 스프라이트를 고정한다(대시 프리즈 · 일섬 차지 홀드 공용).
    // speed=0은 AnyState→Fall 전이(공중)가 조건 평가로 프리즈를 덮으므로,
    // 그 프레임을 sr.sprite에 기록한 뒤 애니메이터 자체를 꺼서 지상/공중 모두 고정한다.
    void FreezeAnimAt(string state, int frame, int frameCount)
    {
        if (anim == null) return;
        int count = Mathf.Max(1, frameCount);
        float nt = (float)frame / count;
        anim.enabled = true;                // 평가되도록 보장
        anim.Play(state, 0, nt);
        anim.Update(0f);                    // 해당 프레임을 sr.sprite에 즉시 기록
        anim.enabled = false;               // 애니메이터 정지 → sr.sprite 고정
    }

    void SpawnAfterImage(float? lifetimeOverride = null)
    {
        if (sr == null || sr.sprite == null) return;
        Color tint = (afterImageColors != null && afterImageColors.Length > 0)
            ? afterImageColors[afterImageIndex % afterImageColors.Length]
            : Color.cyan;
        tint.a = afterImageAlpha;
        afterImageIndex++;
        DashAfterImage.Spawn(sr, tint, lifetimeOverride ?? afterImageLifetime, afterImageSortingOffset);
    }

    // 대시 시작 순간의 히트스톱: 짧게 timeScale을 떨궈 임팩트를 준 뒤 원래 값으로 복원.
    // 실시간 대기라 timeScale=0 중에도 정상 종료하고, 진입 시점의 timeScale을 되돌려
    // 슬로모션 showcase(0.08 등)와도 충돌하지 않는다.
    System.Collections.IEnumerator DashHitstop()
    {
        float prev = Time.timeScale;
        Time.timeScale = Mathf.Clamp01(hitstopScale);
        yield return new WaitForSecondsRealtime(hitstopDuration);
        Time.timeScale = prev;
    }

    // UniTrio-Game-2026(PlayerWeaponController.HandleAttackInput) 참고 재설계:
    // 스윙 도중엔 새 공격을 끼워넣지 않고(항상 끝까지 재생), 대신 입력을 attackInputBufferDuration만큼
    // 버퍼링해 공격이 끝나는 즉시 자동 발동시킨다. 콤보 타수는 attackStage 하나만 순환시켜 관리
    // (별도의 "마지막 타수 기억" 변수 없이 종료 시 다음 타수로 미리 넘겨둠).
    void HandleAttack()
    {
        if (attackQueued && Time.time - attackQueueTime > attackInputBufferDuration)
            attackQueued = false;

        // 공중 공격 금지(사용자 스펙): 지상에서만 스윙이 시작된다. 지상에서 눌러 버퍼링된 입력도
        // 그 사이에 공중으로 나가면 발동하지 않는다(아래 isGrounded 조건). 회피-카운터(F)는 이
        // 공격 시스템을 거치지 않는 별도 경로라 공중에서도 그대로 동작한다.
        if (!isAttacking && !isDashing && !isDodgeCountering && !ilseomActive && !isParrying && !isExecuting && isGrounded && attackQueued)
        {
            attackQueued = false;

            // 마지막 공격이 끝난 뒤 콤보 유효시간이 지났으면 1타로 리셋
            if (Time.time - lastAttackEndTime > comboBufferDuration)
                attackStage = 1;

            StartAttackStage(attackStage);
        }

        if (!isAttacking) return;

        attackTimer += Time.deltaTime;
        float duration = attackStage == 1 ? attack1Duration : attack2Duration;

        if (attackTimer >= duration)
        {
            lastAttackEndTime = Time.time;
            isAttacking = false;
            attackStage = (attackStage % 2) + 1; // 다음 공격을 위해 미리 순환(1->2, 2->1)
        }
    }

    void StartAttackStage(int stage)
    {
        isAttacking = true;
        attackStage = stage;
        attackTimer = 0f;
        attackHitDone = false;
        if (anim != null) anim.SetTrigger(stage == 1 ? "Attack1" : "Attack2");

        // UniTrio-Game-2026(PlayerWeaponController.HandleAttackInput) 참고: 검 공격 시작 시
        // 바라보는 방향으로 살짝 전진해 타격감 연출(flipX: true=왼쪽, false=오른쪽).
        float lungeDirX = (sr != null && sr.flipX) ? -1f : 1f;
        transform.position += new Vector3(lungeDirX * attackLungeDistance, 0f, 0f);

        TestLog.Event("player_attack", $"stage={stage}_start");
    }

    // Slash 1/2 애니메이션 클립에 찍힌 Animation Event(사용자가 직접 표시한 판정 프레임)에서 호출됨.
    // 정규화시간 윈도우 폴링 대신 애니메이션이 그 프레임에 도달하는 정확한 순간에 판정 — 프레임 스킵에도 안전.
    public void AttackHitFrame()
    {
        if (!isAttacking || attackHitDone) return;
        CheckAttackHit(attackStage == 1 ? attack1Damage : attack2Damage);
    }

    // 타격 등급. Critical/Execution은 전용 VFX + 강조 텍스트 + 배율 쉐이크/히트스톱을 공유한다.
    public enum HitTier { Normal, Critical, Execution }

    // 등급별 타격 연출(VFX 프리팹 + 데미지 텍스트)을 한곳에서 처리 — 일반 공격/카운터/처형이 같은 규칙을 따르게.
    void SpawnHitFeedback(Vector3 targetPos, Vector2 facing, int damage, HitTier tier)
    {
        if (tier == HitTier.Normal)
        {
            CombatFx.SpawnHitVfx(hitVfxPrefabs, targetPos, facing, hitVfxOffsetTowardsEnemy);
            CombatFx.SpawnDamageText(damageTextPrefab, targetPos, damage, damageTextColor);
            return;
        }

        GameObject vfx = (tier == HitTier.Critical) ? critHitVfxPrefab : executionHitVfxPrefab;
        Color textColor = (tier == HitTier.Critical) ? critTextColor : executionTextColor;
        CombatFx.SpawnHitVfx(vfx, targetPos, facing, hitVfxOffsetTowardsEnemy);
        CombatFx.SpawnDamageText(damageTextPrefab, targetPos, damage, textColor, true);
    }

    void CheckAttackHit(int damage)
    {
        attackHitDone = true;
        // 크리티컬 판정은 스윙 1회당 한 번(맞은 적마다 따로 굴리지 않음).
        bool crit = Random.value < critChance;
        if (crit) damage = Mathf.RoundToInt(damage * Random.Range(critDamageMultiplierMin, critDamageMultiplierMax));

        Vector2 facing = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing * attackHitboxDistance;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, attackHitboxSize, 0f, enemyLayer);
        int hitCount = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            DummyEnemy enemy = hits[i].GetComponent<DummyEnemy>();
            if (enemy != null)
            {
                // 넉백: 플레이어가 바라보는 방향으로 attackLungeDistance × 배율(기본 1.5)만큼 밀어냄
                enemy.TakeDamage(damage, facing.x * attackLungeDistance * enemyKnockbackMultiplier);
                hitCount++;
                SpawnHitFeedback(hits[i].transform.position, facing, damage, crit ? HitTier.Critical : HitTier.Normal);
            }
        }

        if (hitCount > 0)
        {
            TestLog.Event("player_attack", $"stage={attackStage} dmg={damage} hits={hitCount} crit={crit}");
            float mul = crit ? critShakeMultiplier : 1f;
            if (attackHitstop) StartCoroutine(AttackHitstopCo(crit ? critHitstopMultiplier : 1f));
            if (attackScreenShake && sectionCamera != null) sectionCamera.Shake(attackShakeDuration, attackShakeMagnitude * mul);
        }
    }

    // 대시 히트스톱(DashHitstop)과 동일 패턴: 타격 확정 순간 짧게 시간정지.
    // durationMultiplier: 크리티컬/처형이면 2배(사용자 스펙 "히트 스톱도 2배").
    System.Collections.IEnumerator AttackHitstopCo(float durationMultiplier)
    {
        float prev = Time.timeScale;
        Time.timeScale = Mathf.Clamp01(attackHitstopScale);
        yield return new WaitForSecondsRealtime(attackHitstopDuration * durationMultiplier);
        Time.timeScale = prev;
    }

    // 적 공격에 맞았을 때 호출됨(예: DummyEnemy 창 찌르기). HP UI는 별도 과제라 아직 없음 — 수치만 관리.
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        // 일섬 발동 중엔 아예 피격되지 않는다(스펙 7). DummyEnemy는 IsInvincible로 이미 걸러내지만,
        // 다른 피해 경로가 생겨도 새지 않도록 여기서도 막는다.
        if (ilseomActive)
        {
            TestLog.Event("ilseom", "damage_blocked");
            return;
        }

        // 차지 홀드 중엔 받는 피해가 절반(스펙 6). 1 미만으로 깎여 무피해가 되지 않도록 최소 1은 남긴다.
        if (isCharging)
        {
            int reduced = Mathf.Max(1, Mathf.RoundToInt(damage * ilseomChargeDamageTakenMultiplier));
            TestLog.Event("ilseom", $"charge_damage_reduced {damage}->{reduced}");
            damage = reduced;
        }

        currentHp -= damage;
        TestLog.Event("player_damage", $"hp={currentHp}/{maxHp} dmg={damage}");
    }

    void ApplyBetterJumpPhysics()
    {
        if (isWallSliding || isDashing) return; // 벽 슬라이드/대시 중엔 각자 y를 제어
        if (rb.linearVelocity.y < 0) {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        } else if (rb.linearVelocity.y > 0 && !isJumpHeld) {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void UpdateAnimations()
    {
        // 일섬 차지/발동 중엔 애니메이터를 직접 제어한다 — 여기서 파라미터를 갱신하면
        // AnyState 전이(Fall/Jump/Land/Wall Slide)가 Glitch Out/Sweep을 즉시 덮어써버린다.
        // flipX도 이 구간엔 HandleIlseom/PlayIlseomState가 관리한다.
        if (isCharging || ilseomActive || isExecuting) return;

        if (anim != null) {
            anim.SetFloat("Speed", Mathf.Abs(moveInput.x));
            anim.SetFloat("yVelocity", rb.linearVelocity.y);
            anim.SetBool("isGrounded", isGrounded);
            anim.SetBool("isWallSliding", isWallSliding);

            if (isGrounded && !wasGrounded) anim.SetTrigger("Land");
            wasGrounded = isGrounded;
        }

        if (sr != null) {
            if (isWallSliding) {
                sr.flipX = (wallDirX == 1);
            } else {
                Vector3 currentScale = transform.localScale;
                if (moveInput.x > 0.01f){
                    sr.flipX = false;
                }
                else if (moveInput.x < -0.01f)
                {
                    sr.flipX = true;
                }
            }
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed) {
            isJumping = true;
            isJumpHeld = true;
        } else {
            isJumpHeld = false;
        }
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed)
        {
            dashRequested = true;
            if (isCharging) cancelChargeRequested = true; // 일섬 차지 취소(대시는 그대로 발동됨)
        }
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
        {
            // 일섬 차지는 지상/공중 무관하게 좌클릭으로 취소된다(스펙 3). 공격 자체는 아래 지상 조건을 그대로 따른다.
            if (isCharging) cancelChargeRequested = true;

            // 공중에서는 공격 "입력" 자체를 받지 않는다(사용자 스펙) — 버퍼에도 안 쌓이므로
            // 착지하는 순간 밀린 입력이 자동으로 터지는 일도 없다.
            if (!isGrounded) return;

            attackQueued = true;
            attackQueueTime = Time.time;
        }
    }

    // 일섬 차지(우클릭 홀드)의 홀드 상태를 매 프레임 직접 조회한다.
    //
    // ★ OnCharge(InputValue) 메시지 방식을 쓰지 않는 이유(실측으로 확인):
    //   PlayerInput의 SendMessages 경로는 Button 액션의 "뗌"을 아예 전달하지 않는다 —
    //   PlayerInput.cs:1499 "ATM we only care about `performed` and, in the case of value actions, `canceled`."
    //   → if (!(context.performed || (context.canceled && action.type == InputActionType.Value))) return;
    //   그래서 On<Action>은 press에서만 호출되고, release 콜백은 영원히 오지 않는다(홀드를 떼도 차지가
    //   계속 쌓여 2초를 넘겨 저절로 발동돼버렸다). DodgeCounterRoutine이 이미 버튼 상태를 직접 폴링하는
    //   것과 같은 방식으로 통일한다.
    //   Source: Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/PlayerInput.cs:1499 (확인 2026-07-25)
    //
    // 우클릭은 "Parry"(회피-카운터 확인키)에도 걸려 있지만, 회피-카운터 대기 중에는 CanStartCharge()가
    // isDodgeCountering으로 차지 시작을 막아 서로 간섭하지 않는다.
    void PollChargeInput()
    {
        bool held;
        if (chargeAction != null) held = chargeAction.IsPressed();
        else if (Mouse.current != null) held = Mouse.current.rightButton.isPressed; // 액션 조회 실패 시 폴백
        else held = false;

        if (held && !chargeHeld) chargeStartRequested = true;
        chargeHeld = held;
    }

    // 회피-카운터 확인키. PlayerActions "Parry" 액션에 F키+우클릭 둘 다 바인딩됨(둘 중 아무거나로 확인
    // 가능). 다른 상황에선 아직 쓰임새 없음 — DodgeCounterRoutine이 대기 중일 때만 의미 있음.
    public void OnParry(InputValue value)
    {
        if (value.isPressed)
        {
            parryPressed = true;
        }
    }
}

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
    
    [Header("Wall Climb & Jump")]
    // 벽타기(사용자 지시 2026-08-03) — 기존 "닿으면 자동으로 미끄러져 내려가는" Wall Slide를 대체.
    // 벽 쪽으로 이동 입력을 누르고 있는 동안 벽에 붙고, W/S(=moveInput.y, PlayerActions Dpad 합성이라
    // 이미 W=Up/S=Down으로 매핑돼 있어 새 입력 배선 불필요)로 상하 이동, 입력이 없으면 제자리 고정.
    // 스프라이트가 아직 없어 애니메이터의 기존 Wall Slide 상태·"isWallSliding" 파라미터를 그대로
    // 재사용한다(이 필드들은 그 상태의 수직 속도만 재정의).
    public float wallClimbSpeed = 3f;
    public float wallClimbAccel = 20f;
    public Vector2 wallJumpForce = new Vector2(5f, 9f);
    public float wallClimbShakeDuration = 0.08f; // 벽에 붙는 순간 카메라 쉐이크(사용자 지시)
    public float wallClimbShakeMagnitude = 0.06f;
    // 벽 꼭대기 자동 오르기(사용자 지시 2026-08-03) — "벽을 다 올라가서 위로 갈 수 있는 상황이면
    // 자연스럽게 그 자리로 이동". 머리 위로(원래 벽 감지와 같은 짧은 거리) 아직 뭔가 있으면 꼭대기가
    // 아니고, 없으면 그 지점 위쪽에서 아래로 디딜 곳(바닥)을 찾아 그 위로 옮긴다. 착지면 탐색도
    // wallLayer(벽+바닥 통합 마스크)로 봐서 벽 자체의 꼭대기든 별도로 얹힌 발판이든 다 잡는다 —
    // groundLayer로 좁히면 순수 Wall 레이어인 벽 꼭대기 자체는 못 찾는다. 순간 이동 1프레임
    // (이 프로젝트의 "즉시 이동" 컨벤션과 일치, 코루틴으로 부드럽게 깎지 않음).
    public float ledgeWallCheckDist = 0.15f;  // 머리 위 확인 거리(원래 벽 감지 여유 0.1f과 비슷하게)
    public float ledgeProbeUpOffset = 0.5f;   // 바닥 탐색을 시작할 머리 위 높이
    public float ledgeProbeDownDist = 0.8f;   // 그 지점에서 바닥을 찾는 아래쪽 거리
    // 꼭대기에 올라섰을 때 모서리에서 얼마나 더 안쪽에 놓을지(플레이어 반폭에 더해지는 여유).
    // 0이면 뒤꿈치가 모서리에 딱 걸려 조금만 움직여도 다시 떨어진다.
    public float ledgeLandingMargin = 0.15f;
    // 벽 꼭대기 자동 오르기의 이동 시간(사용자 지시 2026-08-04 "순간이동 느낌"). 0이면 예전처럼 즉시 이동.
    public float ledgeClimbDuration = 0.12f;
    public LayerMask groundLayer;
    // 지형 통합 마스크(Ground+Wall) — "올라설 자리가 있는가"를 찾는 탐색용으로만 쓴다.
    public LayerMask wallLayer;
    // ★ 벽타기가 붙을 수 있는 면(사용자 지시 2026-08-04 "벽은 따로 콜라이더로 지정하자").
    // 예전엔 wallLayer(=Ground 포함)로 벽을 감지해서 바닥·플랫폼·타일 이음매가 전부 벽으로 잡혔고,
    // 높이로 추정해 걸러내려 해도 얇은 플랫폼 같은 예외가 계속 나왔다. 이제 추정하지 않는다 —
    // **Wall 레이어 콜라이더로 명시된 면에만** 붙는다. 비어 있으면 Awake에서 "Wall" 레이어로 채운다.
    public LayerMask climbWallLayer;
    // 벽 감지 여유 거리. 예전엔 0.1로 하드코딩돼 있었는데, 벽 트리거를 지형 표면에 "딱 맞춰" 놓으면
    // (실측: 지형 표면 30.87 / 트리거 면 30.97 = 간격 0.10) 경계에 걸쳐 감지가 실패했다 — 플레이어는
    // 콜라이더 접촉 오프셋(0.01) 때문에 지형에 완전히 밀착하지도 못한다. Wall은 이제 레벨에서 명시적으로
    // 지정하는 면이라 여유를 넉넉히 줘도 오검출이 없다(지형은 애초에 이 마스크에 없다).
    // 0.25 → 0.15(2026-08-04, "너무 붙어있습니다"). 벽 트리거를 지형 표면에 맞춰 놓으면 실측 간격이
    // 0.10 정도라 0.15면 충분히 잡히면서, 멀찍이 스쳐도 붙어버리는 느낌은 줄어든다.
    public float wallCheckDistance = 0.15f;
    // 벽으로 인정할 면의 "수직에 가까운 정도"(법선의 x성분 최소값). 1에 가까울수록 완전한 수직면만 인정.
    // 폴리곤 콜라이더로 벽 실루엣을 통째로 감싸면 윗면·경사면까지 같은 콜라이더에 들어가서, 벽 위에
    // 서 있어도 벽타기가 붙어버린다(사용자 리포트 2026-08-04, 스크린샷). 콜라이더를 다시 그리게 하는
    // 대신 코드에서 **면의 방향**을 보고 거른다 — 실루엣을 통째로 감싸도 수직면에서만 붙는다.
    // 0.7 ≈ 수직에서 45° 이내.
    [Range(0.1f, 1f)] public float wallFaceMinNormalX = 0.7f;
    // 지형에 막혀 더 붙을 수 없을 때만 허용하는 추가 도달거리. 벽 콜라이더를 지형 표면보다 **안쪽에**
    // 그리면(실측: Wall (4)의 y17~22 구간이 지형보다 0.32~0.47 안쪽) 플레이어가 지형에 막혀서
    // wallCheckDistance로는 벽면에 손이 닿지 않는다 — 그렇다고 감지 거리 자체를 늘리면 지나가기만 해도
    // 붙어버린다. 앞이 트여 있으면 기존 거리(짧게), **지형이 막고 있으면**(=이미 최대한 붙은 상태)
    // 이 거리까지 봐준다.
    public float wallBlockedReach = 0.7f;

    // ── 오르막·내리막(경사) ──────────────────────────────────────────────────────────────────
    // 지형이 타일 컴포지트라 경사면이 실제로 많다(실측: 지형 변 702개 중 259개가 5~85°).
    // 수평 속도만 주면 오르막에선 벽처럼 걸리고 내리막에선 붕 떠서 통통 튄다 — 접지 중엔 표면
    // 접선 방향으로 움직여 자연스럽게 오르내리게 한다(사용자 지시 2026-08-04).
    [Header("Slope (오르막·내리막)")]
    public float maxSlopeAngle = 50f;  // 이보다 가파르면 "오를 수 있는 경사"로 보지 않는다(기존 이동 그대로)
    public float slopeMinAngle = 5f;   // 이보다 완만하면 평지로 취급(타일 이음매의 미세한 각도 무시)
    // 지면 스냅 — 경사가 꺾이는 지점(평지→경사, 경사→평지)에서 살짝 떠버리는 프레임에, 바로 아래
    // 이 거리 안에 지면이 있으면 도로 붙여준다. 없으면 안 붙이므로 절벽에서 걸어 나갈 땐 정상 낙하한다.
    // 실측(30° 경사 왕복): 스냅 전 공중 프레임 오르막 15% · 내리막 34% → "통통 튀는" 체감의 원인이었다.
    public float slopeSnapDistance = 0.35f;

    [Header("Dash")]
    public float dashSpeed = 40f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.5f;
    // 입력 버퍼(사용자 지시 2026-08-04 "대시가 더 잘 눌러지게") — 누른 순간 발동 조건이 안 되면
    // (쿨타임이 몇 프레임 남았거나 공격 모션 끝자락) 예전엔 그 입력을 그냥 버렸다. 이제 이 시간
    // 동안 요청을 들고 있다가 조건이 열리는 첫 프레임에 발동한다(점프 코요테 타임과 같은 성격).
    public float dashInputBuffer = 0.12f;
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

    // 체력은 수치가 아니라 "갯수"(칸)다 — HUD에 칸이 그대로 그려져서 "몇 대 더 맞으면 죽는가"가
    // 숫자를 읽지 않고도 보인다. 그래서 피해도 칸 단위(정수 1 = 한 칸)로만 들어온다.
    // ★ 필드명을 maxHp/currentHp에서 바꾼 이유는 의미가 달라졌기 때문이고, 덤으로 씬에 직렬화돼
    //   있던 옛 100 스케일 값(maxHp=100)이 버려지고 아래 기본값이 실제로 적용된다.
    [Header("Health (갯수)")]
    public int maxHealth = 5;
    public int currentHealth;

    // 사용자 지시(2026-08-09): 허공으로 많이 떨어지면 체력 한 칸을 잃고 마지막으로 서 있던 발판으로
    // 돌아온다. 연출 순서도 지정됐다 — 게임 멈춤 + 암전(페이드 인) → 복귀 → 페이드 아웃 후 재개.
    //
    // ⚠️ "많이 떨어짐"만으로는 부족하다는 조건이 붙었다("아래에 플랫폼 or 땅바닥 없어야 함").
    //    긴 낙하가 정상 루트인 구간(높은 곳에서 아래층으로 내려가는 설계)에서 오발동하면 안 되므로,
    //    낙하 거리와 **발밑 탐색** 둘 다 만족해야 발동한다.
    [Header("Fall Death (허공으로 오래 떨어지면 마지막 발판으로 복귀)")]
    [Tooltip("마지막으로 서 있던 지점보다 이만큼 아래로 내려가면 낙사 후보")]
    public float fallDeathDistance = 20f;
    [Tooltip("그 시점에 발밑으로 이만큼 훑어서 아무 지형도 없어야 진짜 '허공'으로 본다")]
    public float fallDeathGroundProbe = 30f;
    public int fallDeathDamage = 1;
    [Tooltip("암전(페이드 인) 시간 — 이 동안 게임은 멈춰 있다")]
    public float fallFadeInDuration = 0.22f;
    [Tooltip("완전 암전 상태로 머무는 시간(이 사이에 복귀·피해가 처리된다)")]
    public float fallBlackHoldDuration = 0.18f;
    public float fallFadeOutDuration = 0.32f;

    // 기능_구현_명세서: 일섬은 빛 에너지를 소모하고, 패링 성공 시 크게 충전되며, 처형 성공 시 체력/에너지를
    // 회복한다. ★ 지금은 에너지가 부족해도 일섬을 막지 않는다 — "쓰려면 얼마가 필요한가"는 밸런스 결정이라
    // 현재 플레이 감각을 바꾸지 않는 선에서 수치·게이지만 먼저 세운다(게이팅은 별도 지시 후).
    [Header("Light Energy (빛 에너지)")]
    public int maxEnergy = 100;
    public int currentEnergy;
    [Range(0f, 1f)] public float startEnergyRatio = 0.5f; // 충전(패링)과 소모(일섬)가 둘 다 보이도록 절반에서 시작
    public int parryEnergyGain = 25;
    public int executionEnergyGain = 30;
    public int executionHealCount = 1;   // 처형 성공 시 회복되는 체력 "칸" 수
    public int ilseomEnergyCost = 40;
    // 사용자 확정(2026-08-01): 일섬은 발동 시 목돈을 떼는 게 아니라, 홀드(차지) 진행도에 비례해
    // ilseomEnergyCost를 완충까지 점진적으로 다 쓴다. 그 과정에서 에너지가 이 비율(=maxEnergy 기준) 아래로
    // 떨어지면 홀드 자체가 취소된다(이미 쓴 만큼은 안 돌려줌 — 채널링 실패의 대가).
    [Range(0f, 1f)] public float ilseomCancelEnergyPercent = 0.1f;

    // ── 폭주(Rampage) ───────────────────────────────────────────────────────────────────────
    // 세계관: 빛을 강제로 흡수해 이성은 잃지만 파괴력·맷집이 극도로 오르는 상태(세계관_및_고유명사_설정.md:60~70).
    // ★ 규칙(2026-08-01 사용자 확정): **광원이 0이 되면 자동 진입**하고, 광원을
    //   rampageExitEnergyPercent(25%) 이상 되찾아야 풀린다. 발동 키는 없다.
    //   진입(0)과 해제(25%)를 다르게 둔 이유 = 이력(hysteresis). 같게 두면 폭주 중 한 대만 때려도
    //   광원이 1 들어와 즉시 풀려서 전투 내내 깜빡인다(실측으로 확인한 문제).
    [Header("Rampage (폭주)")]
    public bool rampageEnabled = true;
    public int rampageExitEnergyPercent = 25;       // 이 % 이상 회복해야 폭주가 풀린다(진입은 0)
    // 폭주 중 광원 획득 75% 감소(25%만 회복) — 사용자 지시 2026-08-02로 기존 50% 감소(0.5)에서 강화.
    public float rampageEnergyGainMultiplier = 0.25f;
    public int rampageMinEnergy = 50;               // ⚠️ 고아 필드(옛 Q 토글 게이트) — 삭제는 별도 승인
    public float rampageDrainPerSecond = 20f;       // ⚠️ 고아 필드(옛 지속 드레인) — 삭제는 별도 승인
    // 폭주 버프 4종. 기획안은 "원초적인 파괴력과 맷집이 극도로 상승"이라고만 쓰고 수치는 없어서
    // (세계관_및_고유명사_설정.md:64) 아래 값은 이번에 정한 초안이다 — 전부 인스펙터에서 조정 가능.
    public float rampageDamageMultiplier = 2f;      // 공격력
    public float rampageJumpMultiplier = 1.25f;     // 점프력
    public float rampageMoveSpeedMultiplier = 1.2f; // 이동속도 (예전엔 0.9로 "느려짐"이었는데 사용자 지시로 버프로 반전)
    public float rampageAttackSpeedMultiplier = 1.4f; // 공격속도 — 애니메이터 재생속도도 같이 올라간다
    public float rampageKnockbackMultiplier = 1.5f; // 적이 밀려나는 거리 배율
    public float rampageHitstopMultiplier = 1.8f;   // 묵직함(히트스톱)
    public float rampageShakeMultiplier = 1.6f;     // 묵직함(카메라 쉐이크)
    public int rampageHitEnergyLoss = 20;           // ⚠️ 고아 필드(옛 피격 시 에너지 손실) — 삭제는 별도 승인
    // 진입 순간 스프라이트가 잠깐 튀는 글리치 프레임(사용자 지시 2026-08-02) — 일섬(Glitch Out/Sweep)과
    // 별개의, 여태 아무 데도 안 쓰인 애니메이터 상태를 재사용한다.
    public string rampageGlitchState = "Glitch Samurai-Idle Gltich";
    public float rampageGlitchDuration = 0.09f;

    // 폭주 중에만 존재하는 두 번째 게이지. 가만히 있으면 계속 닳고 공격을 맞혀야 회복된다 —
    // "이성이 붕괴한다"를 자원으로 옮긴 것이라, 폭주 중엔 멈춰 있을 수 없게 만드는 압박 장치다.
    [Header("Ego (자아 게이지 — 폭주 중에만)")]
    public int maxEgo = 100;
    public float egoDrainPerSecond = 6f;  // 아무것도 안 하면 약 16초에 바닥(사용자 지시로 12 → 절반)
    public int egoGainPerHit = 15;        // 공격이 적중할 때마다 회복(스윙이 아니라 적중 기준)
    // 자아가 바닥나면 몸이 스스로 무너진다 — 5초에 체력 1칸씩(사용자 지시 2026-08-01로 8→5). 자아가
    // 다시 차면 즉시 멈춘다(디버프 해제). 실드는 이 피해를 막지 않는다(실드 소모는 DummyEnemy의 적 공격
    // 경로에만 있다) — 안에서 무너지는 피해라 막히면 오히려 이상하다.
    public float egoDepletedDamageInterval = 5f;
    public int egoDepletedDamage = 1;
    // 자아가 바닥난 동안엔 광원도 서서히 깎인다(사용자 지시 2026-08-02) — 가만히 버텨도 폭주 탈출에
    // 필요한 회복치가 도로 줄어드는 벌칙이다. HP 붕괴(5초 간격 틱)와 달리 이쪽은 매 프레임 연속으로 깎인다.
    public float egoDepletedEnergyDrainPerSecond = 5f;

    // 캐스팅류 — E를 누르는 동안 제자리에 고정돼 초당 25(=25%)만큼 에너지를 체력·실드로 바꾼다.
    // 폭주(Q)와 자원을 공유하는 두 번째 "지속" 소비처지만, 이쪽은 회복 방향이라 위험 대신 정지가 대가다.
    [Header("Light Spend (광원 소모 — E 홀드)")]
    public float lightSpendDrainPerSecond = 25f;    // 초당 소모량(=% 포인트, maxEnergy 100 기준)
    public float lightSpendHealThreshold = 25f;     // 이만큼 모일 때마다 체력 1칸(또는 만체력이면 실드)
    [Range(0f, 1f)] public float lightSpendLowWarnPercent = 0.1f; // 이 비율 이하로 내려가면 1회 강제 중지
    public float lightSpendPixelRate = 16f;         // 방출 픽셀 스폰 빈도(초당 개수)
    public float lightSpendZoomTarget = 1.30f;      // 홀드 지속 시 도달하는 카메라 배율
    public float lightSpendZoomRampIn = 1.5f;       // 목표 배율까지 걸리는 시간(더 오래 눌러도 이 이상 안 들어감)
    public float lightSpendZoomRampOut = 0.15f;     // 해제 시 빠르게 원복
    public float lightSpendCamPan = 1f;             // 일섬·처형과 같은 팬 세기
    // 사용자 지시(2026-08-02): "좀 더 아래로(바닥 보여도 되는데, 바닥 아래는 보이면 안 됨)".
    // X는 완전 센터링(pan=1)을 유지하되 Y는 이만큼만 부분적으로 내려가고, 카메라 하단이 실제
    // 바닥 라인 아래로 내려가지 않도록 매 프레임 클램프한다(SustainedFocusRampCo).
    public float lightSpendCamPanDownMax = 1.2f;
    public float lightSpendSustainedShake = 0.025f; // 지속 쉐이크(사용자 요청으로 완화 — 기존 0.06 → 0.025)
    // 광원이 모이고(흡수) 흩어지는(방출) 기준점 — 스프라이트 중심에서 이만큼 치우친 지점(사용자 요청:
    // 왼쪽 아래로). x는 flipX로 미러링된다(ilseomGatherOffset과 같은 패턴, LightPixelFx.ComputePivot 참고).
    public Vector2 lightPixelPivotOffset = new Vector2(-0.18f, -0.22f);

    // ── 초월(Transcendence) ─────────────────────────────────────────────────────────────────
    // 세계관: 체내의 빛을 고압력으로 뿜어낼 때 일어나는 폭주의 반대 극단(세계관_및_고유명사_설정.md:66).
    // 사용자 지시(2026-08-02): 광원 100%에서 자동 진입, 70%까지 내려가면 해제. 폭주(0↔25)와 정확히
    // 대칭인 이력(hysteresis) 구조라 자원 하나가 세 구간(폭주/평상/초월)을 만든다.
    [Header("Transcendence (초월)")]
    public bool transcendEnabled = true;
    public int transcendEnterEnergyPercent = 100; // 이 % 이상이면 자동 진입
    public int transcendExitEnergyPercent = 70;   // 이 % 이하로 내려가면 해제
    // 유지 비용. 100→70 = 12.5초(사용자 지시 2026-08-02: 기존 5.0초의 2.5배로 연장 — 자아 드레인(6/s)과
    // 같은 값이던 것을 여기서 분리했다).
    public float transcendDrainPerSecond = 2.4f;
    // 진입 순간 1회 재생되는 빛 흡수 연출(사용자 지시 2026-08-02) — LightPixelFx.SpawnAbsorb 재사용.
    public int transcendAbsorbPixelCount = 30;      // 클수록 튀어나오는 픽셀 개수가 늘어난다(3~10개로 클램프됨)
    public float transcendAbsorbSourceRadius = 2.5f; // "주변"의 범위 — 플레이어 중심에서 이 반경 안에서 튀어나온다
    // 이동·판정 버프 4종(사용자 지시 2026-08-02) — 세계관의 "초인적인 가속력·정교하고 빠른 속도전"을
    // 수치로 옮긴 것(폭주의 7종 배율과 대칭, PLAN §11(b)에서 "밸런스 실측이 필요해 이번엔 제외"라고
    // 미뤘던 항목을 사용자가 직접 요청해 이번에 넣는다).
    public float transcendMoveSpeedMultiplier = 1.2f;    // 이동속도 — 폭주(rampageMoveSpeedMultiplier)와 같은 배율
    public float transcendJumpMultiplier = 1.25f;         // 점프력 — 폭주(rampageJumpMultiplier)와 같은 배율
    public float transcendDashSpeedMultiplier = 1.3f;     // 대시 거리 — dashSpeed에 곱함(지속시간은 그대로라 거리가 그만큼 늘어남)
    // 대시 판정 완화 — 대시 시작 후 이 시간 안에 적 공격과 접촉하면 회피-카운터로 인정되는 창
    // (기본 dodgeCounterGraceWindow=0.35초보다 넉넉하게).
    public float transcendDodgeCounterGraceWindow = 0.5f;
    // 패링 판정 완화 — "적 공격 범위 원 ↔ 1타 히트박스" 겹침 판정에 월드 단위 여유(패딩)를 더한다.
    // 0이면 기존과 동일(정확히 겹쳐야 성공), 이 값만큼 원이 커진 것처럼 판정해 여유를 준다.
    public float transcendParryHitboxPadding = 0.3f;
    // 초월 유지 중 상시 연출(사용자 지시 2026-08-02) — 위로 천천히 떠올라 사라지는 픽셀
    // (LightPixelFx.SpawnRiseOne 재사용, 색은 CurrentPixelTint — 초월 중이라 흰색, 폭주 중 픽셀은
    // 붉은색으로 갈린다). 같이 있던 cyan 아우라는 2026-08-04 지시로 제거(StartTranscend 주석 참고).
    // 사용자 피드백으로 밀도 상향(4→8) + 몸통 둘레 원형 스캐터 반경 신설(여러 방향에서 나오도록).
    public float transcendPixelRiseRate = 8f;          // 초당 스폰 개수
    public float transcendPixelRiseScatterRadius = 0.6f; // 몸통 둘레 스캐터 반경(월드 유닛)
    // 사용자 지시(2026-08-02): "좀 더 아래에서부터" — 발밑(transform.position, 피봇 위치) 기준
    // 이 값만큼만 띄운다(0에 가까울수록 땅에 붙어 보임).
    public float transcendPixelRiseYOffset = 0.1f;

    [Header("Attack (1-2 Combo)")]
    public int attack1Damage = 1;
    public int attack2Damage = 1;
    public float attack1Duration = 0.4167f; // Glitch Samurai-Slash 1 클립 길이(사용자가 5프레임으로 재편집), 애니메이터 재생속도 1
    public float attack2Duration = 0.4167f; // Glitch Samurai-Slash 2 클립 길이(5프레임), 애니메이터 재생속도 1
    public float comboBufferDuration = 2f;                     // 마지막 공격이 끝난 뒤 이 시간 안에 다시 공격하면 콤보로 이어짐, 지나면 1타로 리셋
    public float attackInputBufferDuration = 0.3f;              // 공격 중/쿨다운 중에 눌러도 이 시간 안이면 버퍼링돼 자동 발동(UniTrio-Game-2026 PlayerWeaponController._attackQueued 참고)

    [Header("Attack (공중 전용, Jump Attack)")]
    // 공중 공격은 지상 1타/2타 콤보를 쓰지 않고 이 전용 애니메이션만 사용한다(사용자 지시 2026-08-05).
    // 착지 전까지 횟수를 세던 이전 방식(최대 2타) 대신 쿨타임으로 스팸을 막는다.
    public int jumpAttackDamage = 1;
    public float jumpAttackDuration = 0.5833f; // Glitch Samurai-Jump Attack 클립 길이(7프레임 @ 12fps)
    public float jumpAttackCooldown = 0.5f;    // 공중 공격 사이 최소 간격
    // 공중에 뜬 채로 미는 것이 아니라 판정 프레임 순간에만 살짝 정지시킨다(사용자 지시 2026-08-05 —
    // 예전엔 isAttacking 동안 내내 y를 묶어서 애니메이션이 끊기거나 공격이 끝난 뒤에도 잠시 떠
    // 있는 것처럼 보였다). AttackHitFrame()에서 이 시간만큼 카운트다운을 시작하고, 그동안만
    // ApplyGravityScale/HandleMovement/ApplyBetterJumpPhysics가 y를 고정한다 — 그 전후(윈드업·회수)엔
    // 평소처럼 낙하한다.
    public float jumpAttackHangDuration = 0.08f;
    // 위 클립의 애니메이터 상태·클립 이름(dashFreezeState·ilseomChargeState와 같은 컨벤션). 실제 클립
    // 길이를 Awake에서 이 이름으로 찾아 jumpAttackAnimLength에 캐시한다.
    public string jumpAttackClipName = "Glitch Samurai-Jump Attack";
    // 점프 공격으로 무언가(적·LightObject)를 맞히면 공중에서 점프를 1회 더 쓸 수 있다(사용자 지시
    // 2026-08-05, 저글링 리셋). CheckAttackHit에서 세팅, HandleJump에서 소비, 착지 시 리셋.
    public bool jumpAttackBonusJumpEnabled = true;
    public Vector2 attackHitboxSize = new Vector2(1.6f, 1.2f); // 아래 자식 히트박스를 못 찾았을 때만 쓰는 폴백
    public float attackHitboxDistance = 1f;                    // (동일 — 폴백 전용)
    // 1타/2타 × 좌우 히트박스(2026-08-03, 사용자가 씬에서 Player 자식 "1_R"/"1_L"/"2_R"/"2_L"로 직접
    // 배치) — 각각 비활성 GameObject의 BoxCollider2D로, 콜라이더 자체는 물리에 참여하지 않고
    // offset·size만 GetAttackHitbox()가 데이터로 읽어 OverlapBox에 쓴다. Awake에서 찾는다.
    BoxCollider2D attackBox1R, attackBox1L, attackBox2R, attackBox2L;
    public float attackLungeDistance = 0.3f; // 공격 시작 시 바라보는 방향으로 전진하는 거리(UniTrio 참고)
    // 적이 플레이어 공격에 맞으면 "플레이어가 공격 시 전진하는 거리 × 이 배율"만큼 밀려난다(사용자 스펙).
    public float enemyKnockbackMultiplier = 1.5f;
    public LayerMask enemyLayer;
    // 문/스위치 기믹의 DoorSwitch 전용 레이어("Switch") — enemyLayer와 분리해 자아 게이지 회복(적 타격
    // 전용) 로직에 안 걸리게 한다. 물리 통과는 enemyLayer와 동일 원리(rb.excludeLayers)로 처리.
    public LayerMask switchLayer;

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

    // ── 시간 가속(Time Accel) ───────────────────────────────────────────────────────────────
    // Left Alt "토글"로 켜고 끈다(2026-08-04 사용자 지시 — 처음엔 Shift 홀드였지만 대시와 같은 키를
    // 나눠 쓰느라 대시가 뗄 때 나가게 돼 조작감이 깨졌다. 키를 분리하고 홀드 대신 토글로 확정).
    // 세계는 Time.timeScale로 통째로 느려지고(적·함정·파티클·VFX가 전부 자동으로 따라온다),
    // 플레이어 쪽만 TimeAccelMul(=1/timeAccelTimeScale)로 되돌려 "플레이어는 변화하지 않는다"를 만든다.
    // 연출(진입·유지)은 회피-카운터(dodgeCounter*/dodgeGrayscale*)의 것 + 초월 블룸을 재사용한다.
    [Header("Time Accel (시간 가속)")]
    public bool timeAccelEnabled = true;
    // 가속 중 세계의 timeScale. 사용자 지정 0.4(요구 스펙의 "회피-카운터 0.15의 2/3 = 0.1"보다 완만하게).
    // 값을 더 내려도 보정은 따라간다 — 속도 클램프(Physics2D.maxTranslationSpeed)까지 같은 배율로
    // 올리기 때문(StartTimeAccel 참고). 다만 배율이 커질수록 스텝당 계산 오차도 같이 커진다.
    [Range(0.05f, 1f)] public float timeAccelTimeScale = 0.4f;
    // 초월 드레인(transcendDrainPerSecond=2.4) 대비 배율 — 최초 지시 "초월의 1.5배"에 후속 지시
    // "소모속도 2배"가 곱해져 1.5 × 2 = 3배 = **초당 7.2**. 초월·폭주 드레인과 달리 **실시간 기준**이라
    // 느려진 세계 시간과 무관하게 실제 1초당 7.2씩 닳는다(광원 100이면 약 13.9초).
    public float timeAccelDrainMultiplier = 3f;
    // 이 % 이하로 내려가면 강제 해제되고, 다시 이 위로 회복할 때까지 재진입도 막힌다.
    public int timeAccelMinEnergyPercent = 10;
    // 가속 중 잔상 스폰 간격 — 대시(afterImageInterval=0.01s)는 0.18초짜리라 촘촘해도 되지만,
    // 가속은 수 초간 이어져서 그 값을 그대로 쓰면 초당 100개가 쌓인다.
    public float timeAccelAfterImageInterval = 0.05f;
    // 시간 가속·회피-카운터의 잔상만 이 배율만큼 빨리 사라진다(사용자 지시). 일반 대시 잔상은 그대로.
    public float fastAfterImageFadeMultiplier = 2f;

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
    [Header("Focus Pulse (일섬 · 패링 · 처형 카메라 줌)")]
    public float focusPulseRampIn = 0.06f;
    public float focusPulseHold = 0.12f;
    public float focusPulseRampOut = 0.26f;
    public float parryCamPanAmount = 0.8f;   // 막아낸 지점 쪽으로 다가가는 거리
    public float parryCamZoomAmount = 0.7f;  // orthographicSize 감소량(줌인)
    public float ilseomCamPanAmount = 1f;
    public float ilseomCamZoomAmount = 1.1f;
    public float executionCamPanAmount = 1f;
    public float executionCamZoomAmount = 1.1f;

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
    public Material enemyExecutionGlowMaterial;                 // Custom/EnemyExecutionGlow (Assets/Shaders/EnemyExecutionGlow.mat)
    public int enemyGlowSortingOffset = 1;
    public float executionGlowFadeIn = 0.25f;                   // 커서를 댔을 때 글로우가 켜지는 시간
    public float executionGlowFadeOut = 0.2f;                   // 커서를 뗐을 때 글로우가 꺼지는 시간
    public float executionRushDuration = 0.12f;                 // 적 위치로 파고드는 시간(Glitch Out 재생 구간 안에서 소진)
    // 대시-카운터(CounterRush)는 `behind.y = start.y`로 y를 아예 안 옮긴다. 처형도 같은 규칙을 따르되,
    // 적이 이 값보다 더 높거나 낮은 곳에 있으면 "옮길 필요가 있는 경우"로 보고 적의 발밑 높이로 맞춘다(스펙 2).
    public float executionYSnapThreshold = 1.5f;
    public string executionSlicesState = "Glitch Samurai-Glitch Slices"; // 적 자리에 재생할 클립(11프레임 @12fps)
    public float executionSlicesDuration = 0.9167f;             // 위 클립 길이 — 이 시간 뒤 FX가 스스로 사라진다
    public Vector2 executionSlicesOffset = Vector2.zero;        // 적 발밑 기준 미세 보정
    public int executionSlicesSortingOffset = 2;
    public float executionHold = 0.08f;                         // Sweep 후 여운(실시간 초)
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
    float defaultGravityScale; // 벽타기 중 중력을 0으로 껐다가 뗄 때 되돌릴 원래 값(Awake에서 캐시)
    bool isTouchingWall;
    bool wasGroundedLastFrame;         // 지면 스냅 판정용(직전 프레임에 땅에 있었는가)
    float jumpSuppressTimer;           // 점프 직후 이 시간 동안은 "슬로프 런치 억제"를 끈다
    Vector2 groundNormal = Vector2.up; // 접지 중인 표면의 법선(경사 이동용)
    float groundAngle;                 // 그 표면의 기울기(도)
    bool isGrounded;
    int wallDirX;

    Vector3 lastGroundedPosition;  // 낙사 복귀 지점 — 접지 중에는 매 프레임 갱신된다
    bool isFallRespawning;         // 낙사 연출(멈춤+암전) 진행 중 — 입력·물리·시간 조작을 전부 막는다

    // [ASSERT] 판독구
    public Vector3 LastGroundedPosition => lastGroundedPosition;
    public bool IsFallRespawning => isFallRespawning;

    float wallJumpLockCounter;
    bool wasGrounded;

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
    bool isJumpAttacking; // 지금 재생 중인 공격이 지상 콤보(Slash)가 아니라 공중 전용(Jump Attack)인지
    float jumpAttackCooldownCounter;
    float jumpAttackHangTimer;   // 판정 프레임 순간의 짧은 정지 — 0보다 큰 동안만 y를 고정
    bool hasJumpAttackBonusJump; // 점프 공격이 무언가를 맞혀서 생긴 여분의 공중 점프 1회
    // Jump Attack 클립의 실제 길이(Awake에서 애니메이터 컨트롤러에서 읽음). jumpAttackDuration(공격
    // 잠금 시간)은 이보다 의도적으로 길게 잡혀 있어서(사용자 확인 2026-08-05), 애니메이터 파라미터를
    // 가리는 창은 "잠금 시간"이 아니라 "클립이 실제로 재생되는 시간"이어야 한다(UpdateAnimations 참고).
    float jumpAttackAnimLength = 0.3333f;
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
    int ilseomChargeDrained;       // 이번 홀드에서 지금까지 깎은 누적량 — chargeTimer 진행도에 맞춰 목표치를 따라간다
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

    bool isRampaging;              // 폭주 상태 — 잠금이 아니라 순수 버프라 대시/차지/패링/처형과 공존한다
    float rampageDrainAccum;       // 에너지가 정수라 1 미만의 소모분을 여기 모았다가 1 이상이 되면 깎는다
    public int currentEgo;         // 자아 게이지(폭주 중에만 0 초과). HUD·테스트가 직접 읽는다
    float egoDrainAccum;           // 자아도 정수 자원이라 같은 누적 패턴을 쓴다
    float egoDepletedTimer;        // 자아 0인 동안만 도는 붕괴 피해 타이머
    float egoDepletedEnergyDrainAccum; // 자아 0인 동안 광원을 연속으로 깎는 누적치

    bool isTranscending;           // 초월 상태 — 폭주와 구조적으로 동시 성립 불가(광원 0 vs 100)
    float transcendDrainAccum;     // rampageDrainAccum과 동일 패턴(정수 미만 소모분을 모았다가 깎는다)
    float transcendPixelRiseAccum; // lightSpendPixelAccum과 동일 패턴(초당 개수 누적)
    PlayerBloomFx transcendBloomFx; // 초월 중 상시 cyan 블룸(마스크 기준)

    bool isExecuting;              // 처형 시퀀스 진행 중(무적 + 이동/공격 잠금)
    DummyEnemy executionTarget;    // 현재 커서로 타겟팅 중인 적 (null이면 타겟 없음)
    EnemyExecutionGlowFx executionGlowFx; // 현재 적에게 붙어 있는 글로우 FX

    bool isSpendingLight;          // 광원 소모(E 홀드) 중 — 캐스팅(이동·점프·대시 잠금 + Idle 프리즈)
    float lightSpendDrainAccum;    // rampageDrainAccum과 동일 패턴(정수 미만 소모분을 모았다가 깎는다)
    float lightSpendHealAccum;     // 25 모일 때마다 체력 1칸(만체력이면 실드 1개)으로 전환
    bool lightSpendLowWarned;      // 10% 진입 경고를 그 순간에만 1회 발동시키는 플래그(10% 위로 회복되면 리셋)
    float lightSpendPixelAccum;    // 방출 픽셀 스폰 간격 누적(초당 lightSpendPixelRate개)
    PlayerBloomFx lightSpendBloomFx;
    PlayerBloomFx rampageBloomFx;  // 폭주 중 상시 붉은 블룸(마스크 기준)
    PlayerBloomFx actionBloomFx;   // 대시 · 회피카운터 · 처형 구간 블룸(같은 슬롯 재사용)

    bool isLedgeClimbing;          // 벽 꼭대기로 올라타는 보간 이동 중(LedgeClimbRoutine이 위치를 직접 몬다)
    bool isTimeAccelActive;        // 시간 가속 유지 중(Left Alt 토글)
    float timeAccelDrainAccum;     // transcendDrainAccum과 동일 패턴(정수 미만 소모분을 모았다가 깎는다)
    float timeAccelVfxTimer;       // 흑백 확산 램프인 진행도(실시간)
    float timeAccelAfterImageTimer;
    Color timeAccelBaseColor = Color.white; // 진입 전 스프라이트 색(해제 시 복원)
    float defaultFixedDeltaTime = 0.02f;    // Awake에서 프로젝트 설정값(TimeManager)을 캐시
    float defaultMaxTranslationSpeed = 100f; // Physics2D의 속도 클램프(=실질 종단속도) 원래값
    float dashBufferTimer;         // 남은 대시 입력 버퍼(dashInputBuffer에서 카운트다운)

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
        defaultGravityScale = rb.gravityScale;
        // 시간 가속은 물리 스텝 간격도 같은 배율로 줄인다(플레이어의 스텝당 이동량과 물리 실시간
        // 주파수를 평소와 같게 유지 — 터널링·저주파 끊김 방지). 프로젝트 설정값을 여기서 캐시해
        // 두고 해제 때 정확히 되돌린다. 복원은 EndTimeAccel 한 곳과 OnDisable이 보장한다.
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        // Physics2D의 Max Translation Speed(기본 100)는 이 게임에서 단순한 안전장치가 아니라 실제
        // 종단속도다 — 낙하 가속이 159u/s²라 0.63초면 여기 걸린다(실측). 보정된 속도는 그 값의
        // mul배로 표현되므로, 가속 중엔 클램프도 같이 mul배로 올려야 "실시간 낙하 속도"가 평소와
        // 같아진다(안 올리면 종단속도가 실질 1/mul로 떨어져 플레이어가 붕 뜬 것처럼 느려진다).
        defaultMaxTranslationSpeed = Physics2D.maxTranslationSpeed;

        // Jump Attack 클립의 실제 길이를 읽어 둔다 — 애니메이터 표시값을 가릴 창의 기준(위 필드 주석 참고).
        // 못 찾으면 기본값(0.3333)을 그대로 쓴다.
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var clip in anim.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == jumpAttackClipName) { jumpAttackAnimLength = clip.length; break; }
        }

        attackBox1R = transform.Find("1_R")?.GetComponent<BoxCollider2D>();
        attackBox1L = transform.Find("1_L")?.GetComponent<BoxCollider2D>();
        attackBox2R = transform.Find("2_R")?.GetComponent<BoxCollider2D>();
        attackBox2L = transform.Find("2_L")?.GetComponent<BoxCollider2D>();
        if (attackBox1R == null || attackBox1L == null || attackBox2R == null || attackBox2L == null)
            Debug.LogWarning("[PlayerController] 1_R/1_L/2_R/2_L 히트박스 자식을 못 찾아 attackHitboxSize/Distance로 폴백합니다.");

        // Rigidbody2D 기본 셋팅
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        normalLayer = gameObject.layer;
        invincibleLayer = LayerMask.NameToLayer(invincibleLayerName);

        currentHealth = maxHealth;
        currentEnergy = Mathf.Clamp(Mathf.RoundToInt(maxEnergy * startEnergyRatio), 0, maxEnergy);
        lastGroundedPosition = transform.position; // 시작 지점 = 첫 복귀 지점(아직 착지한 적이 없을 때 대비)
        // 이 시점의 값(씬/인스펙터 기준)을 세이브 데이터의 출발점으로 심는다. 옛 세이브를 되돌리는 건
        // GameDataManager.LoadGame()을 부른 쪽만 — Play할 때마다 자동 복원되면 매 판 상태가 달라진다.
        GameDataManager.Bind(this);
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
        if (switchLayer.value == 0) switchLayer = LayerMask.GetMask("Switch");
        if (climbWallLayer.value == 0) climbWallLayer = LayerMask.GetMask("Wall");
        // 적 몸체와의 물리 충돌을 항상 제외한다. 예전엔 대시 중에만 제외했는데, 평상시 이동에서 적에게
        // 밀착하면 서로 밀어내느라 수평 속도가 죽어(실측: 5u/s → 0.96u/s, 약 80% 감소) 사용자가 본
        // "이동 중 갑자기 특정 방향으로 못 감(애니·flipX는 정상)" 증상이 발생했다 — moveInput은 정상
        // 수신되니 애니/flip은 그대로 돌고 좌표만 거의 안 변하는 정확한 시그니처. 전투 판정은 전부
        // Overlap 쿼리(excludeLayers 영향 없음)라 이 제외로 잃는 기능이 없다.
        // 스위치(DoorSwitch)도 같은 이유로 통과 — LightObject/DummyEnemy와 동일 원리.
        rb.excludeLayers = rb.excludeLayers.value | enemyLayer.value | switchLayer.value;
        if (Camera.main != null) sectionCamera = Camera.main.GetComponent<SectionCamera>();

        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
            chargeAction = playerInput.actions.FindAction("Charge");
        if (chargeAction == null)
            Debug.LogWarning("[Ilseom] PlayerActions에 \"Charge\" 액션이 없어 우클릭을 직접 폴링합니다.");
    }

    // 플레이 종료·비활성 시 전역 시간 상태를 반드시 되돌린다. Time.timeScale/fixedDeltaTime은 정적
    // 값이라 여기서 안 되돌리면 다음 Play 세션이 느려진 채로 시작한다(Awake의 방어적 리셋과 같은 이유).
    void OnDisable()
    {
        EndTimeAccel("disabled");
        // 낙사 연출 도중에 멈추면 화면이 검은 채로, 게임이 멈춘 채로 남는다 — 둘 다 되돌린다.
        if (isFallRespawning)
        {
            isFallRespawning = false;
            Time.timeScale = 1f;
            ScreenFadeUI.ClearImmediate();
        }
    }

    void Update()
    {
        // 낙사 연출 중엔 아무것도 굴리지 않는다. ⚠️ 특히 HandleTimeAccel보다 먼저 빠져야 한다 —
        // 그쪽이 Time.timeScale을 자기 값으로 덮어써서 "게임 멈춤"이 풀려 버린다.
        if (isFallRespawning) return;

        // 시간 가속을 가장 먼저 굴린다 — 이 프레임의 TimeAccelMul(플레이어 보정 배율)이 아래 모든
        // 타이머·속도 계산의 전제이기 때문이다(Left Alt 토글 입력도 여기서 본다).
        HandleTimeAccel();

        // 플레이어 자신의 타이머는 전부 PDelta(=가속 중에도 실시간과 같은 간격)로 센다 — 세계만
        // 느려지고 플레이어는 평소대로 움직여야 하므로 쿨다운·모션 길이도 평소 속도여야 한다.
        if (wallJumpLockCounter > 0f) wallJumpLockCounter -= PDelta;
        if (dashCooldownCounter > 0f) dashCooldownCounter -= PDelta;
        // ⚠️ 이 유예만은 PDelta가 아니라 **세계 시간**(Time.deltaTime)으로 센다 — 다른 플레이어 타이머와
        // 성격이 다르기 때문이다. 이건 "내 동작의 길이"가 아니라 "적의 공격 타임라인과 겹치는가"를 재는
        // 판정 창이라, 적이 느려지면 같이 늘어나야 관계가 유지된다. 실시간으로 세면(1차 구현) 가속 중엔
        // 예비동작이 실시간 0.625s(0.25/0.4)인데 유예는 0.35s라 창이 열리기도 전에 만료돼 예비동작을
        // 보고 대시하는 정상 플레이가 통째로 막혔다(사용자 리포트 "이 시간 동안은 대시 카운터가 안터져",
        // 실측: 평상시 0.35>0.25 통과 / 가속 중 0.35<0.625 실패 / 수정 후 0.875>0.625 통과).
        if (dodgeCounterGraceTimer > 0f) dodgeCounterGraceTimer -= Time.deltaTime;
        if (dashBufferTimer > 0f) dashBufferTimer -= PDelta;
        if (jumpSuppressTimer > 0f) jumpSuppressTimer -= PDelta;

        CheckEnvironment();
        CheckFallDeath();
        // 패링 타이머는 일섬보다 먼저 굴린다 — HandleIlseom이 패링을 시작하는 그 프레임에 타이머가
        // 한 번 가산돼 모션이 그만큼 짧아지는 것을 막는다(일섬 차지에서 겪었던 것과 같은 함정).
        HandleParry();
        // 일섬은 대시/공격보다 먼저 본다 — 차지를 취소한 그 입력이 같은 프레임에 정상 발동돼야 하기 때문(사용자 확정).
        HandleIlseom();
        HandleJump();
        HandleWallSlide();
        TryLedgeClimb();
        HandleDash();
        HandleAttack();
        HandleExecution();
        HandleRampage();
        HandleTranscend();
        HandleLightSpend();
        UpdateAnimations();
        CheckMovementStall();
    }

    // ── 키 직접 폴링 헬퍼 ────────────────────────────────────────────────────────────────────
    // ⚠️ Keyboard.current를 보면 안 된다. current는 "가장 최근에 입력이 들어온 키보드"라, 테스트용
    //    가상 키보드(InputInjector.AddDevice)가 붙어 있으면 그쪽을 가리켜 실제 키보드의 Q/R/E/F가
    //    통째로 무시된다. 실제로 PlayTest가 남긴 가상 키보드 3개가 장치 목록에 살아 있었다(2026-08-01).
    //    연결된 모든 키보드를 훑으면 어느 장치에서 왔든 입력이 잡힌다.
    //    (Keyboard.all 대신 InputSystem.devices를 쓰는 이유: 버전에 관계없이 확실히 존재하는 API다.)
    static bool KeyPressedThisFrame(Key key)
    {
        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
            if (devices[i] is Keyboard kb && kb[key].wasPressedThisFrame) return true;
        return false;
    }

    static bool KeyHeld(Key key)
    {
        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
            if (devices[i] is Keyboard kb && kb[key].isPressed) return true;
        return false;
    }

    // ── 임시 진단: "이동 입력은 있는데 실제로 안 움직임"을 잡아 원인을 콘솔에 지목한다 ──────────
    // 이동을 막을 수 있는 경로는 (a) 코드 상태 잠금(공격/회피카운터/대시/벽점프 잠금)과
    // (b) 물리 충돌 둘뿐이다. 어느 쪽인지 매번 추측하지 않으려고 스톨이 감지되면 그 순간의
    // 상태와 수평 접촉 콜라이더를 한 번만 로그로 남긴다. 원인 확정 후 제거할 코드.
    void CheckMovementStall()
    {
        if (!logMovementStall) return;
        // 일섬 차지/발동 중 정지는 스펙대로 의도된 잠금이라 스톨이 아니다(A/D를 눌러도 방향만 바뀜).
        if (isCharging || ilseomActive || isExecuting || isSpendingLight || isLedgeClimbing) { stallTimer = 0f; stallLogged = false; return; }

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
        // ── 슬로프 런치 억제 ──
        // 경사면에서는 콜라이더가 살짝 파고든 것을 물리 엔진이 밀어내면서 플레이어가 위로 튀어오른다
        // (실측: 내리막 진입 순간 속도 (-4.00, +6.92) — 점프도 안 했는데 6.92로 솟았다). 그러면 접지가
        // 끊겨 붕 떴다가 떨어지는 "통통 튀는" 움직임이 된다. 점프한 직후가 아니라면 이 상승분을 깎는다.
        if (isGrounded && rb.linearVelocity.y > 0.1f && jumpSuppressTimer <= 0f
            && !isWallSliding && !isLedgeClimbing && !ilseomActive && !isExecuting && !isDodgeCountering)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }

        if (isDodgeCountering)
        {
            // 회피 성공 직후엔 연장된 대시가 계속 진행 중이어야 함(끊기지 않고 슬로우모션과 함께
            // 미끄러지듯 나아감, UniTrio ExtendDash 참고) — DodgeCounterRoutine이 연장 시간을 다 쓰면
            // isDashing=false로 내려주므로, 그 이후(CounterRush의 Lerp 이동/명중 후 대기)엔 물리 속도를
            // 0으로 고정해 CounterRush의 transform.position 직접 제어와 충돌하지 않게 한다.
            rb.linearVelocity = isDashing ? new Vector2(dashDirX * EffectiveDashSpeed * TimeAccelMul, 0f) : Vector2.zero;
        }
        else if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirX * EffectiveDashSpeed * TimeAccelMul, 0f);
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
        else if (isSpendingLight)
        {
            // 캐스팅류 제자리 고정 — ilseomActive와 같은 패턴(물리 속도를 0으로 묶어 중력이 파고들지 못하게).
            rb.linearVelocity = Vector2.zero;
        }
        else if (isLedgeClimbing)
        {
            // 벽 꼭대기 올라타기는 LedgeClimbRoutine이 transform.position을 직접 보간한다 — 같은 이유로 고정.
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
        // 접지 판정과 함께 표면 법선까지 같은 캐스트에서 받아둔다(경사 이동이 이 값을 쓴다).
        RaycastHit2D groundHit = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.down, 0.1f, groundLayer);
        isGrounded = groundHit.collider != null;
        // 캐스트 시작 지점이 이미 겹쳐 있으면 normal이 0으로 돌아올 수 있어 방어적으로 위쪽으로 폴백한다.
        groundNormal = (isGrounded && groundHit.normal.y > 0.1f) ? groundHit.normal : Vector2.up;

        // 지면 스냅(경사 전환부에서 붕 뜨는 것 방지, slopeSnapDistance 주석 참고).
        // 조건: 직전 프레임에 땅에 있었고 · 위로 솟는 중이 아니고(점프 아님) · 바로 아래에 걸을 수 있는
        // 지면이 있을 때만. 벽타기·대시·연출 구간처럼 위치를 직접 모는 상태에서는 건드리지 않는다.
        if (!isGrounded && wasGroundedLastFrame && rb.linearVelocity.y <= 0.1f
            && !isWallSliding && !isDashing && !ilseomActive && !isExecuting && !isLedgeClimbing)
        {
            RaycastHit2D snap = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.down,
                slopeSnapDistance, groundLayer);
            if (snap.collider != null && snap.normal.y > 0.5f)
            {
                transform.position += Vector3.down * snap.distance;
                isGrounded = true;
                groundNormal = snap.normal;
            }
        }
        wasGroundedLastFrame = isGrounded;

        groundAngle = Vector2.Angle(groundNormal, Vector2.up);
        
        // 벽 감지는 climbWallLayer(Wall 전용)만 본다 — 바닥·플랫폼은 아무리 가까이 붙어도 벽이 아니다.
        bool rightWall = DetectWallFace(1, bounds);
        bool leftWall = DetectWallFace(-1, bounds);
        isTouchingWall = rightWall || leftWall;
        wallDirX = rightWall ? 1 : (leftWall ? -1 : 0);

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            hasJumpAttackBonusJump = false; // 착지하면 코요테 타임으로 다시 점프할 수 있으니 여분은 정리
            // 낙사 복귀 지점 — "마지막으로 있었던 플랫폼"이 곧 지금 서 있는 자리다.
            lastGroundedPosition = transform.position;
        }
        else
        {
            coyoteTimeCounter -= PDelta;
        }
    }

    // ── 낙사(허공으로 오래 떨어짐) ────────────────────────────────────────────────────────────
    // 조건 두 가지를 모두 만족해야 한다(사용자 지시 2026-08-09).
    //  ① 마지막으로 서 있던 지점보다 fallDeathDistance 이상 아래로 내려왔다.
    //  ② 그 시점에 발밑 fallDeathGroundProbe 안에 지형이 하나도 없다(= 받아 줄 바닥이 없는 허공).
    // ②가 없으면 "높은 곳에서 아래층으로 내려가는" 정상 루트가 통째로 낙사로 처리된다.
    void CheckFallDeath()
    {
        if (isFallRespawning || isGrounded) return;
        if (rb.linearVelocity.y > 0f) return; // 아직 솟는 중이면 낙하가 아니다

        if (lastGroundedPosition.y - transform.position.y < fallDeathDistance) return;

        Bounds b = coll.bounds;
        RaycastHit2D below = Physics2D.BoxCast(b.center, b.size, 0f, Vector2.down, fallDeathGroundProbe, groundLayer);
        if (below.collider != null) return; // 아래에 발판·땅이 있다 → 그냥 긴 낙하다

        StartCoroutine(FallRespawnRoutine());
    }

    // 게임 멈춤 → 암전(페이드 인) → 복귀 + 체력 한 칸 → 페이드 아웃 → 재개.
    // 전부 unscaled로 돈다 — 멈춘(timeScale=0) 상태에서 진행되는 연출이라 스케일 시간으로 재면 영원히 안 끝난다.
    System.Collections.IEnumerator FallRespawnRoutine()
    {
        isFallRespawning = true;
        float fallen = lastGroundedPosition.y - transform.position.y;
        TestLog.Event("fall_death", $"triggered fall={fallen:F1} from={transform.position} to={lastGroundedPosition}");

        Time.timeScale = 0f; // 게임 멈춤(적·함정·VFX까지 통째로)
        yield return ScreenFadeUI.FadeTo(1f, fallFadeInDuration);

        // 완전 암전 상태에서 복귀시킨다 — 순간이동이 화면에 보이지 않게.
        transform.position = lastGroundedPosition;
        rb.linearVelocity = Vector2.zero;
        TakeDamage(fallDeathDamage);
        TestLog.Event("fall_death", $"respawned hp={currentHealth}/{maxHealth}");

        yield return new WaitForSecondsRealtime(fallBlackHoldDuration);

        // 화면이 아직 검을 때 세계를 먼저 되살린다 — 카메라·애니메이션이 한 박자 정리된 뒤 밝아진다.
        Time.timeScale = BaseTimeScale;
        isFallRespawning = false;

        yield return ScreenFadeUI.FadeTo(0f, fallFadeOutDuration);
    }

    /// <summary>그 방향에 "붙을 수 있는 벽면"이 있는지. 단순히 Wall 콜라이더에 닿았는지가 아니라
    /// **맞은 면이 수직에 가까운지**(법선 x성분)까지 본다 — 폴리곤으로 벽 실루엣을 통째로 감싸면
    /// 윗면·경사면도 같은 콜라이더라, 그것만으로는 벽 위에 서 있을 때도 벽타기가 붙는다.
    ///
    /// 박스캐스트 대신 허리·어깨 두 높이의 레이를 쓴다: 박스캐스트는 이미 겹쳐 있으면 법선이 0으로
    /// 나와 방향을 알 수 없고, 발끝 높이는 바닥 모서리를 긁어 오탐이 난다.</summary>
    bool DetectWallFace(int dirX, Bounds b)
    {
        Vector2 dir = new Vector2(dirX, 0f);
        // ⚠️ 레이는 플레이어 **반대쪽 바깥**에서 출발한다. 몸 중심에서 쏘면, 벽 트리거와 몸이 조금이라도
        // 겹친 순간 "콜라이더 안에서 시작한 레이"가 되어 거리 0·법선 (0,0)으로 돌아오고, 법선 검사에서
        // 탈락해 벽이 아닌 것으로 판정된다(공중에서 벽에 파고들 때 실제로 이 상태가 된다 —
        // 사용자 리포트 2026-08-05 "점프중/공중에 떠있을 때 벽타기가 안 발동"). 몸 뒤에서 쏘면 출발점이
        // 항상 벽 바깥이라 법선이 제대로 나온다.
        float back = b.extents.x + 0.05f;
        float dist = back + b.extents.x + wallCheckDistance;
        // 발목~머리까지 네 높이를 훑는다 — 낮은 벽(자동 생성분은 높이 1.7~2.4)이나 공중에서 몸의 일부만
        // 벽 옆에 걸치는 상황에서도 잡히게. Wall 레이어에만 쏘므로 발목 높이도 지형 모서리에 안 걸린다.
        float[] heightRatios = { 0.15f, 0.4f, 0.65f, 0.9f };
        for (int i = 0; i < heightRatios.Length; i++)
        {
            Vector2 from = new Vector2(b.center.x - dirX * back, b.min.y + b.size.y * heightRatios[i]);

            RaycastHit2D hit = Physics2D.Raycast(from, dir, dist, climbWallLayer);
            if (hit.collider != null && Mathf.Abs(hit.normal.x) >= wallFaceMinNormalX) return true;

            // 2차 시도: 앞을 지형이 막고 있으면 이미 최대한 붙은 상태다 — 벽 콜라이더가 지형보다 안쪽에
            // 그려져 있어도 인정한다(wallBlockedReach 주석 참고). 앞이 트여 있으면 그냥 멀리 있는 벽이므로
            // 여기서 끝낸다 — 이래야 지나가기만 해도 붙는 일이 안 생긴다.
            if (Physics2D.Raycast(from, dir, dist, groundLayer).collider == null) continue;
            hit = Physics2D.Raycast(from, dir, back + b.extents.x + wallBlockedReach, climbWallLayer);
            if (hit.collider != null && Mathf.Abs(hit.normal.x) >= wallFaceMinNormalX) return true;
        }
        return false;
    }

    void HandleMovement()
    {
        // 아래 경사 분기에서 중력을 끄므로, 그 상태에서 빠져나오는 모든 경로에서 반드시 되살려야 한다.
        // (ApplyGravityScale은 벽타기 중이면 0을 유지하고, 시간 가속 보정도 함께 반영한다)
        ApplyGravityScale();

        // 벽 점프 직후에는 수평 입력을 잠시 잠가 벽 반대 방향으로 확실히 밀어냄
        if (wallJumpLockCounter > 0f) return;

        // 공격 중 · 회피-카운터 중 · 일섬 차지 중 · 패링 모션 중엔 제자리에 멈춤 (이동 입력 무시, 수평
        // 속도만 고정 — 차지 중에도 중력은 그대로 살아 있어 공중에서 모으면 떨어진다)
        // 지상 콤보는 스윙 내내 y도 고정("공격 동안은 낙하하지 않습니다"), 점프 공격은 AttackFreezesY
        // 참고 — 판정 프레임 순간만 고정하고 그 전후엔 정상적으로 낙하/상승한다(사용자 지시 2026-08-05).
        if (isAttacking || isDodgeCountering || isCharging || isParrying || isExecuting)
        {
            rb.linearVelocity = new Vector2(0f, AttackFreezesY ? 0f : rb.linearVelocity.y);
            return;
        }

        // 가속 없이 즉시 목표 속도로 (뚝뚝 끊기는 조작감)
        // 폭주·초월 중엔 버프로 더 빨라진다(MoveSpeedMultiplier — UpdateAnimations의 재생속도와
        // 같은 값을 공유해 실제 이동속도와 애니메이션이 항상 같이 움직인다).
        // 시간 가속 중엔 세계가 timeScale로 느려진 만큼 속도를 되돌려 곱해야(TimeAccelMul) 실시간
        // 이동속도가 평소와 같아진다 — 비활성 시엔 정확히 1이라 평상시 계산은 전혀 바뀌지 않는다.
        float speed = moveSpeed * MoveSpeedMultiplier * TimeAccelMul;

        // ── 경사면 처리 ──
        // 점프로 올라가는 중(y속도 +)이면 손대지 않는다 — 접선 속도가 y를 덮어써 점프가 그 자리에서 죽는다.
        bool rising = rb.linearVelocity.y > 0.1f;
        if (isGrounded && !rising && groundAngle > slopeMinAngle && groundAngle <= maxSlopeAngle)
        {
            // ⚠️ 속도를 0으로 만드는 것만으로는 안 멈춘다 — 매 물리 스텝마다 중력이 다시 실리고, 그게
            // 경사면 충돌 해소를 거쳐 아래로 미끄러지는 이동으로 바뀐다(실측: 45° 경사에서 등속
            // -0.78/-0.78로 계속 밀려남, 마찰이 0이라 멈추지도 않는다). 경사에 붙어 있는 동안엔
            // 벽타기와 같은 방식으로 중력 자체를 끈다 — 이동은 아래 접선 속도가 전부 담당한다.
            rb.gravityScale = 0f;

            if (Mathf.Abs(moveInput.x) > 0.01f)
            {
                // 표면 접선 방향으로 이동한다. 오르막은 위로, 내리막은 아래로 같이 나아가므로
                // 벽처럼 걸리지도, 붕 떠서 통통 튀지도 않는다(속도 크기는 평지와 동일).
                Vector2 tangent = new Vector2(groundNormal.y, -groundNormal.x);
                if (moveInput.x < 0f) tangent = -tangent;
                rb.linearVelocity = tangent * speed;
            }
            else
            {
                // 이 프로젝트의 PlayerPhysicsMaterial은 friction=0이라(지형과의 실효 마찰도 0)
                // 가만히 두면 경사를 따라 계속 미끄러진다 — 입력이 없으면 그 자리에 고정한다.
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        rb.linearVelocity = new Vector2(moveInput.x * speed, rb.linearVelocity.y);
    }

    void HandleWallSlide()
    {
        bool wasWallSliding = isWallSliding;

        if (isDashing || isCharging || ilseomActive || isParrying || isExecuting || isSpendingLight || isLedgeClimbing)
        {
            isWallSliding = false;
        }
        else if (isWallSliding)
        {
            // 이미 붙어있는 상태(사용자 지시 2026-08-03) — 방향키를 계속 누르고 있지 않아도 유지된다.
            // 벽에서 아예 떨어지거나, 반대쪽 키를 누르거나, Space(벽점프, HandleJump가 이번 프레임에
            // 먼저 처리하며 wallJumpLockCounter를 세팅함)를 누르면 해제.
            bool pressingAway = wallDirX != 0 && Mathf.Abs(moveInput.x) > 0.01f && Mathf.Sign(moveInput.x) == -wallDirX;
            if (!isTouchingWall || wallDirX == 0 || pressingAway || wallJumpLockCounter > 0f)
            {
                isWallSliding = false;
            }
            else
            {
                // 폭주·초월 버프 중엔 벽타기 속도도 같이 빨라진다(사용자 지시 2026-08-03).
                // 목표 속도는 ×mul, 가속도(maxDelta)는 ×mul² — 속도가 mul배로 스케일된 세계에서
                // "같은 실시간 가속"을 내려면 초당 변화량도 그만큼 더 커야 한다(중력과 같은 규칙).
                // 즉시 이동(이 프로젝트의 조작감 컨벤션 — HandleMovement의 수평 이동과 같은 방식).
                // 예전엔 wallClimbAccel(20)로 가속·감속했는데, 붙는 순간과 떼는 순간이 뭉개져
                // "움직임이 어색하다"는 지적을 받았다(사용자 2026-08-04). wallClimbAccel은 이제 미사용.
                float targetY = moveInput.y * wallClimbSpeed * MoveSpeedMultiplier * TimeAccelMul;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, targetY);
            }
        }
        else
        {
            // 아직 안 붙어있음 — 처음 붙으려면 벽 쪽으로 눌러야 한다(진입 조건은 기존과 동일,
            // 붙은 뒤부터는 위 분기가 이어받아 방향키 없이도 유지).
            bool pushingIntoWall = isTouchingWall && wallDirX != 0
                && Mathf.Abs(moveInput.x) > 0.01f && Mathf.Sign(moveInput.x) == wallDirX;

            // 접지 상태에서 낮은 턱을 걸어 올라가던 TryStepUpShortWall은 2026-08-04 사용자 지시로 제거됐다
            // ("자꾸 플랫폼에 순간이동으로 올라간다"). 자동으로 올라가는 건 **벽타기 중에만**(TryLedgeClimb),
            // 그 외의 턱은 전부 점프로 넘는다. 벽 여부는 이제 추정하지 않고 climbWallLayer가 결정한다.
            if (pushingIntoWall)
            {
                isWallSliding = true; // 벽 접촉 + 키 감지 순간 즉시 붙음(애니메이터는 기존 Wall Slide 상태 재사용)
                if (sectionCamera != null)
                    sectionCamera.Shake(wallClimbShakeDuration, wallClimbShakeMagnitude);

                // W/S(moveInput.y)로 상하 이동, 안 누르면 그 자리에 고정(자동으로 미끄러지지 않음 — 사용자 지시
                // "Wall Slide 대신 벽타기": 입력 없을 때 정지가 곧 "벽에 붙어있다"는 뜻).
                // 목표 속도는 ×mul, 가속도(maxDelta)는 ×mul² — 속도가 mul배로 스케일된 세계에서
                // "같은 실시간 가속"을 내려면 초당 변화량도 그만큼 더 커야 한다(중력과 같은 규칙).
                // 즉시 이동(이 프로젝트의 조작감 컨벤션 — HandleMovement의 수평 이동과 같은 방식).
                // 예전엔 wallClimbAccel(20)로 가속·감속했는데, 붙는 순간과 떼는 순간이 뭉개져
                // "움직임이 어색하다"는 지적을 받았다(사용자 2026-08-04). wallClimbAccel은 이제 미사용.
                float targetY = moveInput.y * wallClimbSpeed * MoveSpeedMultiplier * TimeAccelMul;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, targetY);
            }
        }

        // 붙어있는 동안 중력을 완전히 꺼서(사용자 지시) MoveTowards 보정과 FixedUpdate 중력 적분이
        // 서로 못 이기고 미세하게 흘러내리는 문제를 근본적으로 없앤다. 어느 경로로 isWallSliding이
        // 꺼지든(위 여러 분기) 여기서 한 번에 복구되도록 전이 시점만 본다.
        if (isWallSliding && !wasWallSliding)
        {
            rb.gravityScale = 0f;
            // ⚠️ 붙는 순간 남아 있던 낙하 속도를 끊는다(사용자 리포트 2026-08-04 "떨어지면서 벽타기 하면
            // 쭉 떨어진다"). 중력만 0으로 만들면 "더 빨라지지 않을" 뿐, 이미 실린 하강 속도는 아래
            // MoveTowards가 wallClimbAccel(20/s)로만 깎아서 빠르게 떨어지던 상태면 멈추는 데 몇 초가
            // 걸렸다(예: -60u/s면 3초). 벽을 잡으면 낙하가 즉시 멎는 게 이 게임의 "즉시" 조작감과도 맞다.
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
        else if (!isWallSliding && wasWallSliding)
        {
            ApplyGravityScale();
            // 애니메이터 그래프엔 "Wall Slide → Idle/Run" 전이가 없다 — 원래 벽타기는 공중에서만
            // 일어나서 뗄 때 항상 Jump/Fall(각각 isWallSliding IfNot 조건 있음)이나 착지 Land 트리거를
            // 거쳐 자연스럽게 빠져나갔는데, 이제 접지 상태에서도 벽타기가 되므로(사용자 지시
            // 2026-08-03) 접지 상태 그대로 떨어지면 그 경로를 하나도 안 거쳐 Wall Slide에 그대로
            // 멈춰 있는다(사용자가 실제로 겪은 버그). 애니메이터에 새 전이를 추가하는 대신
            // UpdateAlteredStateAnim과 같은 패턴으로 코드에서 직접 되돌린다 — UpdateAnimations가
            // 같은 프레임 뒤에 돌며 폭주/초월 글리치 변형이 필요하면 그쪽에서 마저 처리한다.
            if (isGrounded && anim != null)
                anim.Play(Mathf.Abs(moveInput.x) > 0.01f ? "Glitch Samurai-Run" : "Glitch Samurai-Idle", 0, 0f);
        }
    }

    /// <summary>벽을 끝까지 올라 위로 갈 수 있는 상황이면 그 자리로 옮겨준다(사용자 지시 2026-08-03,
    /// "벽을 다 올라가서 위쪽으로 올라갈 수 있는 상황이 나오면 자연스럽게 해당 위치로 이동"). 머리 위로
    /// 벽이 이어져 있으면(아직 꼭대기 아님) 대기, 벽이 끝난 바로 그 지점 앞쪽 아래에 바닥이 있으면
    /// 순간 그 위로 옮긴다 — 이 프로젝트의 "즉시 이동" 컨벤션과 일치, 별도 코루틴/부드러운 보간 없음.</summary>
    void TryLedgeClimb()
    {
        if (isLedgeClimbing) return; // 이미 올라타는 중 — 코루틴이 위치를 직접 몬다
        if (!isWallSliding || wallDirX == 0) return;

        Bounds b = coll.bounds;
        Vector2 aboveHead = new Vector2(b.center.x, b.max.y + 0.05f);
        // 머리 위 그 방향으로 벽이 계속 있으면 아직 꼭대기가 아니다 — 대기.
        // "벽이 계속 있는가"는 벽 판정과 같은 기준(climbWallLayer)으로 봐야 한다 — 통합 마스크로 보면
        // 벽 위에 얹힌 바닥 타일 때문에 꼭대기인데도 아직 벽이라고 오판한다.
        // 부착 판정과 같은 여유(wallCheckDistance)를 써야 한다 — 여기만 좁으면 아직 벽에 붙어 있는데도
        // "꼭대기에 도달했다"고 오판해 엉뚱한 지점에서 올라타 버린다.
        bool wallStillAbove = Physics2D.Raycast(aboveHead, new Vector2(wallDirX, 0f), b.extents.x + wallCheckDistance, climbWallLayer);
        if (wallStillAbove) return;

        // 벽이 끝난 그 앞쪽 위에서 아래로 디딜 곳을 찾는다 — 벽 자체의 꼭대기든 별도 발판이든
        // wallLayer로 잡는다(있으면 올라설 자리가 있다는 뜻).
        Vector2 probeStart = new Vector2(b.center.x + wallDirX * (b.extents.x + ledgeWallCheckDist), b.max.y + ledgeProbeUpOffset);
        RaycastHit2D ledgeHit = Physics2D.Raycast(probeStart, Vector2.down, ledgeProbeDownDist, wallLayer);
        if (ledgeHit.collider == null) return;

        // transform.position.y == 발밑(피봇이 발, TryStepUpShortWall 주석 참고) — b.extents.y를 더하면
        // 중심 기준으로 착각해 반 캐릭터 키만큼 붕 뜬다(실측으로 잡은 버그).
        float feetOffset = transform.position.y - b.min.y;
        // ⚠️ 예전엔 probeStart.x(벽면에서 0.15만 지난 곳)를 그대로 도착 지점으로 썼는데, 그러면 플레이어
        // **중심**이 모서리 바로 위라 몸의 절반이 허공에 걸친다 — 올라서자마자 다시 떨어지고, 떨어질
        // 때마다 착지 애니메이션이 다시 재생됐다(사용자 리포트 2026-08-04). 몸 하나를 더 들여보내
        // 뒤꿈치까지 확실히 모서리 안쪽에 놓는다. 그 자리에 디딜 곳이 없으면(좁은 기둥 꼭대기 등)
        // 원래 지점으로 되돌린다.
        Bounds pb = coll.bounds;
        float inwardX = probeStart.x + wallDirX * (pb.extents.x + ledgeLandingMargin);
        float landY = ledgeHit.point.y;
        RaycastHit2D inwardHit = Physics2D.Raycast(new Vector2(inwardX, ledgeHit.point.y + ledgeProbeUpOffset),
            Vector2.down, ledgeProbeUpOffset + 0.3f, wallLayer);
        float targetX = probeStart.x;
        if (inwardHit.collider != null) { targetX = inwardX; landY = inwardHit.point.y; }

        Vector3 target = new Vector3(targetX, landY + feetOffset + 0.02f, transform.position.z);
        // 예전엔 여기서 바로 transform.position에 대입했는데 "순간이동 느낌"이라는 사용자 지적을 받았다
        // (2026-08-04) — 같은 목표 지점으로 ledgeClimbDuration 동안 보간해 "올라탄다"는 느낌을 준다.
        StartCoroutine(LedgeClimbRoutine(target));
    }

    /// <summary>벽 꼭대기로 올라타는 짧은 보간 이동. 이 구간엔 입력·물리를 잠그고(일섬·처형의 위치
    /// 보간과 같은 패턴) 끝나면 중력을 되살린다. try/finally로 어떤 경로로 끝나도 중력·잠금이 stuck되지
    /// 않게 한다(회피-카운터에서 배운 구조).</summary>
    System.Collections.IEnumerator LedgeClimbRoutine(Vector3 target)
    {
        isLedgeClimbing = true;
        isWallSliding = false;
        Vector3 start = transform.position;
        try
        {
            rb.gravityScale = 0f;               // 보간 중엔 중력이 끼어들지 않게(벽타기와 같은 처리)
            rb.linearVelocity = Vector2.zero;
            float dur = Mathf.Max(0f, ledgeClimbDuration);
            float t = 0f;
            while (t < dur)
            {
                t += PDelta;                    // 시간 가속 중에도 실시간으로 같은 길이가 되도록
                transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(t / dur));
                yield return null;
            }
            transform.position = target;
        }
        finally
        {
            isLedgeClimbing = false;
            ApplyGravityScale();
            // 올라선 직후 그 순간의 입력을 그대로 이어받는다(기존 동작과 동일 — 멈춰 서지 않는다).
            rb.linearVelocity = new Vector2(moveInput.x * moveSpeed * TimeAccelMul, 0f);
        }
    }

    void HandleJump()
    {
        if (isJumping) {
            // 회피-카운터/일섬 중엔 점프로 캔슬할 수 없음 — 입력은 버림.
            // 공격 중엔 이제 캔슬하고 점프로 넘어간다(사용자 지시 2026-08-05: "공격 도중에 애니메이션을
            // 캔슬하고 점프 가능"). CancelAttack()이 isAttacking을 끄므로 아래로 그대로 진행된다.
            if (isDodgeCountering || isCharging || ilseomActive || isParrying || isExecuting || isSpendingLight || isLedgeClimbing) { isJumping = false; return; }
            CancelAttack();
            float jumpMul = isRampaging ? rampageJumpMultiplier
                : isTranscending ? transcendJumpMultiplier
                : 1f; // 폭주·초월 점프력 버프
            if (isWallSliding) {
                // 벽에 붙어있으면 일반 점프보다 우선(사용자 실측 버그 2026-08-03) — 땅에 붙은 채
                // 벽타기 중이면 coyoteTimeCounter가 항상 접지 상태로 가득 차 있어(0보다 큼) 아래
                // 일반 점프 분기가 먼저 걸려버렸다. 그러면 벽타기 특유의 "붙어있는 동안 중력 0"이
                // 안 풀린 채로 일반 점프의 큰 상승 속도만 얹혀서 중력 없이 끝없이 치솟는 것처럼
                // 보였다(HandleWallSlide가 이 분기를 못 보고 그대로 "유지" 취급). Space로 벽에서
                // 확실히 떼어내려면(방향키 유무와 무관하게, 그 순간의 입력이 아니라 "붙어있는가"만
                // 본다) 벽점프가 항상 먼저 처리돼야 한다 — wallJumpLockCounter를 세팅해 바로 뒤
                // HandleWallSlide가 같은 프레임에 떼어내고 중력을 복구한다.
                rb.linearVelocity = new Vector2(-wallDirX * wallJumpForce.x * TimeAccelMul, wallJumpForce.y * jumpMul * TimeAccelMul);
                wallJumpLockCounter = 0.15f;
            }
            else if (coyoteTimeCounter > 0f) {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * jumpMul * TimeAccelMul);
                coyoteTimeCounter = 0f;
            }
            else if (hasJumpAttackBonusJump) {
                // 점프 공격으로 무언가를 맞혀서 생긴 여분의 공중 점프(사용자 지시 2026-08-05) — 코요테
                // 타임이 끝난 뒤에도 이 한 번만은 쓸 수 있다. 사용하면 소모, 착지하면 CheckEnvironment가 리셋.
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * jumpMul * TimeAccelMul);
                hasJumpAttackBonusJump = false;
            }
            // 폭주·초월 중엔 점프 순간 글리치 변형으로 덮어쓴다(사용자 지시 2026-08-02). Any State가
            // isGrounded/yVelocity로 매 프레임 "Glitch Samurai-Jump"를 다시 끌어올 수 있는 Fall과 달리
            // Jump는 발동 순간 한 번만 재생되는 클립이라 여기서 한 번 Play하면 그대로 끝까지 간다.
            if (anim != null && (isRampaging || isTranscending)) anim.Play("Glitch Samurai-Jump Glitch", 0, 0f);
            // 점프한 상승은 슬로프 런치 억제(FixedUpdate 상단)가 깎으면 안 되므로 잠깐 면제해 준다.
            jumpSuppressTimer = 0.2f;
            isJumping = false;
        }
    }

    /// <summary>초월 중이면 대시 속도(=거리, 지속시간은 그대로라 속도가 곧 거리다)가 늘어난다.
    /// 폭주는 대시를 막지 않지만 별도 배율은 없다(사용자 지시가 초월에만 해당).</summary>
    float EffectiveDashSpeed => isTranscending ? dashSpeed * transcendDashSpeedMultiplier : dashSpeed;

    void HandleDash()
    {
        // 회피-카운터 시퀀스 동안은 대시 타이머를 동결(코루틴이 직접 관리, EndDash로 종료)
        if (isDodgeCountering) return;

        // 버퍼가 살아 있는 동안 매 프레임 발동을 재시도한다 — 예전엔 누른 그 프레임에 조건이 안 맞으면
        // (쿨타임 잔여·공격 모션 끝자락) 입력을 그냥 버려서 "눌렀는데 안 나감"이 났다(사용자 지시로 개선).
        if (dashBufferTimer > 0f)
        {
            // 일섬 발동 중 · 패링 모션 중엔 대시로 캔슬할 수 없음(버퍼가 살아 다음 프레임에 재시도).
            // 공격 중엔 이제 캔슬하고 대시로 넘어간다(사용자 지시 2026-08-05: "공격 도중에 애니메이션을
            // 캔슬하고 대시 가능").
            if (!isDashing && !ilseomActive && !isParrying && !isExecuting && !isSpendingLight && !isLedgeClimbing && dashCooldownCounter <= 0f)
            {
                dashBufferTimer = 0f; // 소비
                CancelAttack();
                isDashing = true;
                BeginActionBloom(0.9f); // 대시 중 마스크 블룸(사용자 지시)
                dashTimer = dashDuration;
                dashCooldownCounter = dashCooldown;
                dashDirX = Mathf.Abs(moveInput.x) > 0.01f
                    ? (int)Mathf.Sign(moveInput.x)
                    : ((sr != null && sr.flipX) ? -1 : 1);

                if (invincibleLayer != -1) gameObject.layer = invincibleLayer;
                // (적 통과는 이제 Awake에서 상시 적용 — 대시에서만 켜고 끄지 않는다)
                afterImageTimer = 0f;
                afterImageIndex = 0;
                // 닷지 트리거 창(대시 길이보다 길게 유예) — 초월 중이면 판정 완화(사용자 지시)로 더 넉넉해진다.
                dodgeCounterGraceTimer = isTranscending ? transcendDodgeCounterGraceWindow : dodgeCounterGraceWindow;
                if (dashFreezeAnim) FreezeDashAnim();

                // 히트스톱: 대시 시작 순간 짧게 시간정지 → 무게감
                if (dashHitstop) StartCoroutine(DashHitstop());

                TestLog.Event("dash_iframe", $"dash_started dir={dashDirX}");
            }
        }

        if (isDashing)
        {
            dashTimer -= PDelta;

            if (dashAfterImage)
            {
                afterImageTimer -= PDelta;
                if (afterImageTimer <= 0f)
                {
                    SpawnAfterImage();
                    afterImageTimer = afterImageInterval;
                }
            }

            // 진행 방향 벽에 부딪히면 남은 시간 무시하고 즉시 종료.
            // (벽에 0.15s 내내 처박는 낭비 제거 → 벽 붙은 뒤 반대/낙하로 즉시 복귀)
            // ⚠️ isTouchingWall(=Wall 레이어 전용)이 아니라 **지형 전체**로 본다 — 이 판정의 목적은
            // "더 못 가는데 대시 시간을 낭비하지 않는다"이지 벽타기와 무관하다. 벽 감지를 Wall 레이어로
            // 좁힌 뒤(2026-08-04)에도 일반 지형에 처박히면 그대로 끊기게 유지하려면 여기만 따로 봐야 한다.
            Bounds db = coll.bounds;
            bool intoWall = Physics2D.BoxCast(db.center, db.size, 0f, new Vector2(dashDirX, 0f), 0.1f, wallLayer);

            if (dashTimer <= 0f || intoWall)
            {
                EndDash(intoWall ? "dash_cancelled_wall" : "dash_ended");
            }
        }
    }

    // ── 일섬: 차지 판정 ─────────────────────────────────────────────────────────────────────
    // ── 일섬: 차지 판정 ─────────────────────────────────────────────────────────────────────
    void HandleIlseom()
    {
        PollChargeInput();

        if (ilseomCooldownCounter > 0f)
        {
            ilseomCooldownCounter -= PDelta;
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

            // 공격 도중엔 홀드/탭(차지 vs 패링) 구분 없이 즉시 패링으로 처리하고 스윙을 캔슬한다
            // (사용자 지시 2026-08-05: "공격 도중에 애니메이션을 캔슬하고 패링 가능"). 일섬 차지(홀드)는
            // 요청 범위 밖이라 공격 중엔 여전히 시작하지 않는다 — CanStartCharge()의 !isAttacking
            // 게이트는 그대로 둔다. TryParry()가 스스로 확인하는 조건을 미리 봐서, 패링이 어차피 실패할
            // 상황(쿨타임 등)엔 공격을 헛되이 캔슬하지 않는다.
            if (isAttacking && parryEnabled && !isRampaging && parryCooldownCounter <= 0f)
            {
                CancelAttack();
                TryParry();
                return;
            }

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

        chargeTimer += PDelta;

        // 누른 직후 parryTapMaxHold 동안은 "패링일 수도 있는" 구간이라 차지 연출을 켜지 않는다.
        // (탭할 때마다 픽셀 FX가 깜빡이고 취소 이펙트까지 터지는 것을 막는다.)
        if (!chargeVisualsStarted && chargeTimer >= parryTapMaxHold) BeginChargeVisuals();

        // 광원 소모(사용자 확정): 홀드가 진짜 차지로 확정된 순간부터(탭=패링 구간 제외) 완충까지
        // ilseomEnergyCost를 진행도에 비례해 점진적으로 깎는다. 발동 시점에 목돈을 다시 떼지 않는다
        // (IlseomRoutine에서 제거 — 이미 홀드 중에 다 냈다).
        if (chargeVisualsStarted)
        {
            int targetDrained = Mathf.FloorToInt(ilseomEnergyCost * Mathf.Clamp01(chargeTimer / ilseomChargeTime));
            int delta = targetDrained - ilseomChargeDrained;
            if (delta > 0)
            {
                delta = Mathf.Min(delta, currentEnergy);
                currentEnergy -= delta;
                ilseomChargeDrained += delta;
            }

            // 10% 아래로 떨어지면 홀드 자체가 취소된다(사용자 확정) — 이미 쓴 만큼은 돌려주지 않는다.
            if (currentEnergy <= ilseomCancelEnergyPercent * maxEnergy)
            {
                PlayerHudUI.Instance?.FlashEnergyBarRed();
                CancelCharge("ilseom_blocked_low_energy");
                return;
            }
        }

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
                // 에너지 게이팅은 이제 위 홀드 중 점진 소모 + 10% 컷으로 이미 처리된다(여기 도달했다는
                // 건 완충까지 살아남았다는 뜻). 여기서는 쿨타임만 막는다 — CanStartCharge()가 쿨타임을
                // 더 이상 안 막으므로(패링이 이 차지 상태를 빌려 쓰기 때문), 홀드 발동 확정 지점에서 대신 막는다.
                if (ilseomCooldownCounter > 0f)
                {
                    CancelCharge("ilseom_blocked_cooldown");
                }
                else
                {
                    isCharging = false;
                    StartCoroutine(IlseomRoutine());
                }
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
        // ilseomCooldownCounter는 여기서 막지 않는다 — 패링(짧은 탭)이 이 차지 상태를 빌려 판정하므로,
        // 쿨타임 중에도 차지는 시작돼야 패링이 죽지 않는다. 쿨타임 자체는 완충 확정 지점에서 따로 막는다.
        // 폭주 중엔 홀드(차지) 자체를 시작하지 못하게 막는다 — 여기서 막으면 패링(탭)과 일섬(홀드)이
        // 같은 입력을 공유하므로 둘 다 한 번에 봉인된다(사용자 지시: "홀드 자체도 안 되도록").
        // 벽타기 중도 마찬가지로 막는다(사용자 지시 2026-08-03) — 일섬·패링이 같은 입력을 공유하므로
        // 여기 한 곳만 막으면 둘 다 한 번에 봉인된다(위 폭주와 같은 논리).
        return ilseomEnabled && !isRampaging && !isCharging && !ilseomActive && !isDashing && !isAttacking
            && !isDodgeCountering && !isParrying && !isExecuting && !isSpendingLight && !isWallSliding;
    }

    // 누르는 순간엔 아직 패링(탭)인지 일섬(홀드)인지 알 수 없다 — 상태만 열어두고 연출은 뒤로 미룬다.
    void StartCharge()
    {
        isCharging = true;
        chargeTimer = 0f;
        chargeCompletePopped = false;
        chargeVisualsStarted = false;
        ilseomChargeDrained = 0;
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
        if (parryCooldownCounter > 0f) parryCooldownCounter -= PDelta;

        if (!isParrying) return;
        parryTimer += PDelta;
        if (parryTimer >= parryMotionDuration) isParrying = false;
    }

    // 우클릭 탭으로 진입. 모션(Slash 1)은 성공/실패와 무관하게 항상 재생되고, 판정이 성립하면
    // 그 공격을 무효화(스펙 5) + 겹침 지점에 연출(스펙 4) + 구형 실드(스펙 6)까지 이어진다.
    // 데미지는 주지 않는다 — isAttacking을 세우지 않으므로 클립의 AttackHitFrame 이벤트는 무시된다.
    void TryParry()
    {
        // 폭주 중 봉인(사용자 지시). CanStartCharge에서 이미 막히지만, 다른 경로로 새지 않게 여기서도 막는다.
        if (isRampaging) { TestLog.Event("parry_timing", "blocked_rampage"); return; }
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
        AddEnergy(parryEnergyGain); // "성공 시 빛 에너지가 크게 충전됨"(기능_구현_명세서)
        TestLog.Event("parry_timing", $"parry_success enemy={target.name} at={contact.ToString("F2")}");
    }

    /// <summary>1타/2타 × 좌우 자식 히트박스(2026-08-03)에서 월드 공간 박스를 읽는다 — stage(1|2)와
    /// 현재 flipX로 4개 중 하나를 골라 offset·size를 TransformPoint/lossyScale로 월드 변환. 자식을
    /// 못 찾았으면(다른 씬 등) attackHitboxSize/Distance 폴백. 회전은 두 박스 다 항상 0이라 angle을
    /// 쓰는 OverlapBoxAll엔 문제없지만, FindParryTarget의 축 정렬 clamp 근사는 회전을 지원하지 않는다
    /// (기존에도 없던 기능이라 회귀 아님).</summary>
    void GetAttackHitbox(int stage, out Vector2 center, out Vector2 size, out float angle)
    {
        bool flipX = sr != null && sr.flipX;
        BoxCollider2D box = stage == 1 ? (flipX ? attackBox1L : attackBox1R) : (flipX ? attackBox2L : attackBox2R);
        if (box != null)
        {
            Transform bt = box.transform;
            center = bt.TransformPoint(box.offset);
            size = Vector2.Scale(box.size, bt.lossyScale);
            angle = bt.eulerAngles.z;
            return;
        }
        Vector2 facing = flipX ? Vector2.left : Vector2.right;
        center = (Vector2)transform.position + facing * attackHitboxDistance;
        size = attackHitboxSize;
        angle = 0f;
    }

    // 스펙 3의 성공 조건을 만족하는 적을 찾는다.
    // contact = 적 공격 캡슐(밑동~창끝) 위에서 1타 히트박스에 가장 가까운 점을 박스 안으로 클램프한
    // 점 — "두 범위가 겹치는 영역의 중앙"에 해당한다(스펙 4).
    // ⚠️ 사용자 지시(2026-08-02): "창 전체가 범위" — 창끝 한 점(원)이 아니라 밑동~창끝을 훑는
    // 캡슐 전체로 확장(DummyEnemy.FindPlayerAtHitPoint의 실제 피격 판정과 같은 기준).
    DummyEnemy FindParryTarget(out Vector2 contact)
    {
        contact = Vector2.zero;

        GetAttackHitbox(1, out Vector2 boxCenter, out Vector2 boxSize, out _);
        Vector2 half = boxSize * 0.5f;

        // 적 몸통은 창 길이(1.9)만큼 떨어져 있어 1타 히트박스로 직접 훑으면 못 잡는다 → 후보만 넓게
        // 모으고, 실제 판정은 "적의 공격 범위(밑동~창끝 캡슐) vs 1타 히트박스"로 한다.
        Collider2D[] found = Physics2D.OverlapCircleAll(transform.position, parrySearchRadius, enemyLayer);
        for (int i = 0; i < found.Length; i++)
        {
            DummyEnemy e = found[i].GetComponent<DummyEnemy>();
            if (e == null || !e.IsAttacking) continue;
            // (B) 아직 그 공격에 맞지 않았거나, (A) 대시 회피 인정 창이 열려 있음
            //     — (A)만 성립하는 경우 = 대시 무적으로 이미 흘려낸 공격을 유예 중에 되받아치는 상황.
            if (!e.IsAttackUnresolved && dodgeCounterGraceTimer <= 0f) continue;

            // 캡슐(밑동~창끝) vs 박스 — 원-vs-박스처럼 한 번의 클램프로 끝나는 정확한 공식은 없어
            // 세그먼트를 여러 지점으로 샘플해 박스에 가장 가까운 점을 찾는 방식으로 근사한다
            // (표본 9개, 실사용 정확도로는 충분 — 완벽한 최소값이 필요한 물리 시뮬레이션이 아니다).
            Vector2 baseP = e.AttackHitPointBase;
            Vector2 tipP = e.AttackHitPoint;
            Vector2 bestClamped = Vector2.zero;
            float bestDistSq = float.MaxValue;
            const int sampleCount = 8;
            for (int s = 0; s <= sampleCount; s++)
            {
                Vector2 p = Vector2.Lerp(baseP, tipP, s / (float)sampleCount);
                Vector2 cl = new Vector2(
                    Mathf.Clamp(p.x, boxCenter.x - half.x, boxCenter.x + half.x),
                    Mathf.Clamp(p.y, boxCenter.y - half.y, boxCenter.y + half.y));
                float dSq = (p - cl).sqrMagnitude;
                if (dSq < bestDistSq) { bestDistSq = dSq; bestClamped = cl; }
            }

            // 패링 판정 완화(사용자 지시) — 초월 중이면 겹침 판정에 월드 단위 여유(패딩)를 더한다.
            // 비초월 중엔 패딩 0이라 기존과 동일한 정확도(캡슐 확장 자체는 공통 적용, 회귀 없음).
            float pad = isTranscending ? transcendParryHitboxPadding : 0f;
            float effRadius = e.AttackHitRadius + pad;
            if (bestDistSq > effRadius * effRadius) continue;

            contact = bestClamped;
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
        // "빛 에너지를 소모하여" 발동(기능_구현_명세서) — 단, 목돈을 여기서 떼지 않는다. HandleIlseom의
        // 홀드 중 점진 소모가 완충까지 ilseomEnergyCost를 이미 다 썼다(사용자 확정: 홀드 자체가 소모).

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
        int dmg = RampageDamage(Mathf.RoundToInt(attack1Damage * ilseomDamageMultiplier));
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
    // 매 프레임 커서 아래의 적을 보고 글로우/UI를 갱신한 뒤, R키가 들어오면 시퀀스를 시작한다.
    void HandleExecution()
    {
        if (!executionEnabled) { ClearExecutionTargeting(); return; }
        // 폭주 중 봉인(사용자 지시). 타겟팅까지 지워야 붉은 글로우·프롬프트가 화면에 남지 않는다.
        if (isRampaging) { ClearExecutionTargeting(); return; }
        if (isExecuting) return; // 시퀀스 시작 시 이미 정리했다 — 진행 중엔 커서를 보지 않는다

        // ① 타겟팅은 다른 동작 중에도 항상 갱신한다. 예전엔 공격·대시·차지 중이면 여기서 통째로
        //    return해버려서, 그 사이에 커서를 떼거나 적이 죽어도 붉은 글로우와 프롬프트가 화면에
        //    그대로 남아 있었다(스펙 1의 "커서를 뗐을 때 페이드 아웃"이 깨지는 경로).
        UpdateExecutionTargeting(FindExecutableUnderCursor());
        if (executionTarget == null) return;

        // ② 발동만 다른 동작과 배타적이다.
        if (isDashing || isDodgeCountering || ilseomActive || isCharging || isParrying || isAttacking) return;

        // R키는 InputSystem 액션이 아니라 직접 폴링한다 — PlayerActions에 "Execute" 액션이 없기 때문
        // (액션 추가는 .inputactions 편집이라 MCP가 필요). Update에서 읽으므로 wasPressedThisFrame이
        // 프레임과 어긋나지 않는다(코루틴 안에서 폴링했다가 입력을 놓쳤던 대시-카운터 사례와 다름).
        if (!KeyPressedThisFrame(Key.R)) return;

        ExecutionUI.GetOrCreate().FlashHidePrompt();
        StartCoroutine(ExecutionRoutine(executionTarget));
    }

    // ── 폭주(Rampage): 광원이 0이 되면 자동 진입 ─────────────────────────────────────────────
    // ★ 사용자 확정(2026-08-01): **폭주는 광원이 0일 때 자동으로 된다.**
    //   이전 구현은 정반대였다 — "50 이상에서 Q로 발동 → 초당 20 소모 → 0이 되면 자동 종료"
    //   (기획안 기능_구현_명세서.md:78~79의 "모은 빛 에너지를 소모하여 폭주"를 그대로 옮긴 것).
    //   이 지시가 기획안보다 우선한다.
    // 이렇게 두면 세계관(세계관_및_고유명사_설정.md:64 "이성이 붕괴")과 시야 제한이 정확히 맞물린다 —
    // 몸 안의 빛이 없으니 앞이 안 보이고, 대신 원초적인 파괴력만 남는다.
    // 해제는 다시 빛을 얻는 것(타격 +3 · 처치 +10 · 패링 +25 · 처형 +30)뿐이다.
    // 상태 잠금이 아니라 수치 버프라 다른 동작과 배타 처리하지 않는다.
    /// <summary>지금 아무 동작·연출 중이 아닌가. 상태 진입(폭주·초월)을 미룰지 판단하는 단일 기준
    /// (CanStartLightSpend()가 쓰던 "자유로움" 목록과 동일 — 사용자 승인으로 이 프로퍼티를 공유한다).
    /// isGrounded는 넣지 않는다 — 넣으면 공중·낙하 중엔 진입이 무한 연기된다.</summary>
    bool IsActionIdle =>
        !isExecuting && !ilseomActive && !isCharging && !isDashing
        && !isAttacking && !isDodgeCountering && !isParrying && !isSpendingLight;

    void HandleRampage()
    {
        if (!rampageEnabled) { EndRampage("disabled"); return; }

        // 폭주는 "쓰는 능력"이 아니라 빛이 바닥난 상태 그 자체다 — 조건이 곧 상태라 토글이 없다.
        // ⚠️ 진입은 지연된다(사용자 지시 2026-08-02): 조건이 성립해도 IsActionIdle이 아니면 기다린다
        // (처형·일섬 연출 도중 폭주가 끼어들어 카메라·블룸이 한 연출 안에서 두 번 갈아타는 문제 방지).
        // 예약 플래그는 두지 않는다 — 매 프레임 다시 본다. 대기 중 광원이 회복되면 진입 자체가 취소된다.
        // 해제는 지연하지 않는다 — 폭주 해제(25% 회복)는 전투 중에만 성립해 IsActionIdle이 거의 안 열린다.
        if (!isRampaging)
        {
            if (currentEnergy <= 0 && IsActionIdle) StartRampage();
            return;
        }

        // 해제는 진입선(0)이 아니라 25%다 — 이력이 없으면 한 대 때릴 때마다 폭주가 깜빡인다.
        if (currentEnergy >= RampageExitEnergy) { EndRampage("energy_restored"); return; }

        DrainEgo();
    }

    // ── 초월(Transcendence): 광원이 100%가 되면 자동 진입, 70%로 내려가면 해제 ──────────────────
    // 세계관: 체내의 빛을 고압력으로 뿜어낼 때 초인적인 속도전을 구사하는 상태 — 폭주의 정반대 극단
    // (세계관_및_고유명사_설정.md:66). 폭주(0↔25%)와 정확히 대칭인 이력(hysteresis) 구조라, 자원 하나가
    // 폭주/평상/초월 세 구간을 만든다. 진입은 IsActionIdle로 지연되고(T-1a와 같은 기준을 공유),
    // 해제는 지연하지 않는다 — 일섬(-40)·광원 소모(-25/s)로 70 아래가 되는 건 대개 전투·캐스팅 중이라
    // 지연하면 사실상 공짜 연장이 된다(폭주 해제를 지연하지 않는 것과 같은 이유).
    void HandleTranscend()
    {
        if (!transcendEnabled) { EndTranscend("disabled"); return; }
        if (isRampaging) { EndTranscend("rampage"); return; } // 구조적으로 동시 성립 불가 — 순서 의존 제거용 방어 가드

        if (!isTranscending)
        {
            if (currentEnergy >= TranscendEnterEnergy && IsActionIdle) StartTranscend();
            return;
        }

        if (currentEnergy <= TranscendExitEnergy) { EndTranscend("energy_drained"); return; }

        DrainTranscend();
        TickTranscendPixelRise();
    }

    // 초월 유지 중 상시 연출(사용자 지시 2026-08-02) — 위로 천천히 떠올라 사라지는 cyan 픽셀.
    // lightSpendPixelAccum과 동일한 누적 패턴(HandleLightSpend 참고).
    // ⚠️ 사용자 피드백(2026-08-02): "여러 방향에서 좀 더 많이" — 한 지점(가슴 피봇)에서만 나오던 것을
    // 몸통 둘레 원형 스캐터(SpawnAbsorb의 "여러 방향에서 튀어나옴"과 같은 방식)로 바꾸고 스폰 빈도를 올렸다.
    void TickTranscendPixelRise()
    {
        transcendPixelRiseAccum += transcendPixelRiseRate * Time.deltaTime;
        // 사용자 지시(2026-08-02): "좀 더 아래에서부터" — 가슴 피봇(lightPixelPivotOffset, 흡수
        // 버스트가 쓰는 그 지점) 대신 발밑(transform.position, 피봇이 발에 있다)에서 살짝만 띄워
        // 시작한다. 흡수 버스트는 건드리지 않는다(그쪽은 이미 확정된 값).
        Vector3 center = transform.position + Vector3.up * transcendPixelRiseYOffset;
        // 발밑 앵커라 원형 스캐터(아래 방향 포함)가 바닥 아래로 파고들 수 있어 클램프한다
        // (E홀드 카메라와 같은 원칙: 바닥은 보여도 되지만 바닥 밑은 보이면 안 됨).
        float floorY = GetFloorY();
        while (transcendPixelRiseAccum >= 1f)
        {
            transcendPixelRiseAccum -= 1f;
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            Vector3 origin = center + (Vector3)(dir * Random.Range(transcendPixelRiseScatterRadius * 0.4f, transcendPixelRiseScatterRadius));
            origin.y = Mathf.Max(origin.y, floorY);
            LightPixelFx.SpawnRiseOne(origin, CurrentPixelTint);
        }
    }

    /// <summary>초월이 자동 진입하는 광원 수치(기본 100%).</summary>
    public int TranscendEnterEnergy =>
        Mathf.Clamp(Mathf.FloorToInt(maxEnergy * transcendEnterEnergyPercent / 100f), 1, maxEnergy);

    /// <summary>초월이 풀리는 광원 수치(기본 70%). 100으로 진입하고 여기까지 내려가야 빠져나온다.</summary>
    public int TranscendExitEnergy =>
        Mathf.Clamp(Mathf.CeilToInt(maxEnergy * transcendExitEnergyPercent / 100f), 1, maxEnergy);

    // 초월 유지 비용. 정수 자원이라 1 미만의 소모분은 모았다가 한 번에 깎는다(DrainEgo와 같은 패턴).
    void DrainTranscend()
    {
        if (isExecuting) return; // 처형은 조작이 막힌 연출 구간이라 그동안 자원이 닳으면 손해를 본다(자아 드레인과 같은 이유)

        // Time.deltaTime(scaled) — 히트스톱·저스트 닷지 슬로우 중에 초월만 정상 속도로 닳으면 슬로우가
        // 페널티가 된다. 자아·폭주 드레인과 같은 선택.
        transcendDrainAccum += transcendDrainPerSecond * Time.deltaTime;
        int spend = Mathf.FloorToInt(transcendDrainAccum);
        if (spend > 0)
        {
            transcendDrainAccum -= spend;
            currentEnergy = Mathf.Max(0, currentEnergy - spend);
        }
    }

    // ── 시간 가속(Time Accel) ───────────────────────────────────────────────────────────────
    // Shift 탭 = 기존 대시 / 홀드 = 시간 가속(누르는 동안). 세계만 느려지고 플레이어는 평소 그대로.

    /// <summary>가속 중 플레이어 쪽 계산에 곱하는 보정 배율(=1/timeAccelTimeScale). 비활성이면 정확히
    /// 1이라 평상시 코드 경로는 전혀 바뀌지 않는다. 속도엔 ×mul, 가속도엔 ×mul²을 쓴다.</summary>
    public float TimeAccelMul => isTimeAccelActive ? 1f / Mathf.Max(0.01f, timeAccelTimeScale) : 1f;

    /// <summary>플레이어 자신의 타이머용 델타 — 가속 중에도 실시간과 같은 간격이 된다(스케일된
    /// Time.deltaTime × 보정 배율). 히트스톱(timeScale=0) 중엔 그대로 0이라 같이 멈춘다.</summary>
    float PDelta => Time.deltaTime * TimeAccelMul;

    /// <summary>지금 "기본"이어야 할 timeScale. 히트스톱처럼 잠깐 시간을 눌렀다 되돌리는 코드는 진입
    /// 시점 값(prev)이 아니라 이 값으로 복원해야 한다 — 그 사이에 가속이 켜지거나 꺼졌으면 낡은 값을
    /// 되살려 슬로우모션이 stuck된다(예전 timeScale stuck 버그와 같은 종류의 문제).</summary>
    float BaseTimeScale => isTimeAccelActive ? timeAccelTimeScale : 1f;

    /// <summary>플레이어와 같은 실시간으로 돌아야 하는 외부 시스템(카메라 추적 등)이 곱해 쓰는 배율.
    /// 플레이어는 씬에 하나뿐이라 정적으로 노출한다(SectionCamera.LateUpdate가 쓴다).</summary>
    public static float PlayerTimeMultiplier { get; private set; } = 1f;

    /// <summary>가속이 강제 해제되는 광원 수치(기본 10%). 여기까지 떨어지면 풀리고, 이 위로 회복할
    /// 때까지 재진입도 막힌다(사용자 지시).</summary>
    public int TimeAccelMinEnergy =>
        Mathf.Clamp(Mathf.CeilToInt(maxEnergy * timeAccelMinEnergyPercent / 100f), 0, maxEnergy);

    public bool IsTimeAccelActive => isTimeAccelActive;
    public bool IsDodgeCountering => isDodgeCountering; // 회피-카운터 시퀀스 진행 중(테스트가 읽는다)
    public bool IsWallSliding => isWallSliding;         // 벽타기 부착 중(테스트가 읽는다)
    public bool IsLedgeClimbing => isLedgeClimbing;     // 벽 꼭대기 올라타는 보간 중(테스트가 읽는다)
    public bool IsGrounded => isGrounded;               // 접지 상태(테스트가 읽는다)
    public float GroundAngle => groundAngle;            // 발밑 경사 각도(도, 테스트가 읽는다)

    void HandleTimeAccel()
    {
        PollTimeAccelInput();

        if (!isTimeAccelActive) return;
        if (!timeAccelEnabled) { EndTimeAccel("disabled"); return; }
        if (!CanSustainTimeAccel()) { EndTimeAccel("state_lock"); return; }
        if (currentEnergy <= TimeAccelMinEnergy) { EndTimeAccel("energy_drained"); return; }

        DrainTimeAccel();
        TickTimeAccelVfx();
    }

    // Left Alt 토글(사용자 지시 2026-08-04) — 누를 때마다 켜고 끈다. PlayerActions에 액션이 없어
    // 처형(R)·폭주(Q)·광원소모(E)와 같은 방식으로 키를 직접 폴링한다(.inputactions는 hooks가 편집을
    // 막기도 하고, 이 프로젝트는 이미 그런 키가 셋이라 컨벤션이 확립돼 있다).
    void PollTimeAccelInput()
    {
        if (!KeyPressedThisFrame(Key.LeftAlt)) return;
        if (isTimeAccelActive) EndTimeAccel("toggled_off");
        else TryStartTimeAccel();
    }

    // 회피-카운터·일섬·처형·광원소모는 각자 자기 timeScale이나 연출 타이밍을 소유하는 구간이라
    // 시간 가속과 겹치면 서로의 시계를 덮어쓴다. 폭주는 광원이 바닥난 상태라 애초에 쓸 자원이 없다.
    bool CanSustainTimeAccel() =>
        !isRampaging && !isDodgeCountering && !ilseomActive && !isExecuting && !isSpendingLight;

    void TryStartTimeAccel()
    {
        if (!timeAccelEnabled || isTimeAccelActive) return;
        if (!CanSustainTimeAccel()) { TestLog.Event("time_accel", "blocked_state"); return; }
        if (currentEnergy <= TimeAccelMinEnergy)
        {
            TestLog.Event("time_accel", $"blocked_low_energy energy={currentEnergy}/{maxEnergy}");
            return;
        }
        StartTimeAccel();
    }

    void StartTimeAccel()
    {
        isTimeAccelActive = true;
        timeAccelDrainAccum = 0f;
        timeAccelVfxTimer = 0f;
        timeAccelAfterImageTimer = 0f;
        PlayerTimeMultiplier = 1f / Mathf.Max(0.01f, timeAccelTimeScale);

        Time.timeScale = timeAccelTimeScale;
        // 물리 스텝 간격도 같은 배율로 줄인다 → 실시간 스텝 주파수(50Hz)와 플레이어의 스텝당 이동량이
        // 평소와 정확히 같아진다. 안 줄이면 스텝이 timeScale배로만 돌아(0.4면 20Hz) 플레이어 이동이
        // 끊겨 보이고, 보정된 속도 탓에 스텝당 이동량이 2.5배로 커져 얇은 벽을 뚫을 위험이 생긴다.
        Time.fixedDeltaTime = defaultFixedDeltaTime * timeAccelTimeScale;
        // 속도 클램프도 같은 배율로(=실시간 종단속도 유지, Awake의 캐시 주석 참고).
        Physics2D.maxTranslationSpeed = defaultMaxTranslationSpeed * TimeAccelMul;
        ApplyGravityScale();

        // (진입 시 광원 20% 획득은 2026-08-04에 넣었다가 같은 날 사용자 지시로 뺐다 — 토글할 때마다
        //  보너스가 들어가 켰다 껐다 반복하면 광원을 무한히 벌 수 있는 구멍이었다. 소모는 그대로 7.2/초)

        // ── 연출: 회피-카운터와 동일(사용자 지시) ──
        // (초월 블룸은 2026-08-04에 넣었다가 같은 날 사용자 지시로 뺐다 — "걍 블룸 빼라")
        timeAccelBaseColor = sr != null ? sr.color : Color.white;
        if (sr != null) sr.color = dodgeCounterGlowColor;
        BeginActionBloom(1f);
        if (sectionCamera != null)
        {
            sectionCamera.Shake(dodgeCounterActivationShakeDuration, dodgeCounterActivationShakeMagnitude);
            // 회피-카운터는 "적" 쪽으로 팬+줌하지만 가속엔 대상이 없다 → 자기 자신 기준 1회성 펄스.
            // 지속형(SetSustainedFocus)이 아니라 단발인 이유: 유지 시간이 입력에 달려 가변이고 진입
            // 순간의 임팩트만 필요하기 때문. FocusPulseCo는 unscaled라 느려진 시간과 무관하게 돈다.
            sectionCamera.FocusPulse(transform.position, dodgeCamPanAmount, dodgeCamZoomAmount,
                dodgeGrayscaleRampIn, 0.3f, dodgeGrayscaleRampOut);
        }
        // 드레인 수치도 같이 남긴다 — 씬에 직렬화된 옛 값이 코드 기본값을 덮고 있으면(SKILL 9번)
        // 이 로그만 보고 바로 알 수 있다.
        TestLog.Event("time_accel",
            $"started scale={timeAccelTimeScale:F2} mul={TimeAccelMul:F2} " +
            $"drain={transcendDrainPerSecond * timeAccelDrainMultiplier:F1}/s energy={currentEnergy}/{maxEnergy}");
    }

    void EndTimeAccel(string reason)
    {
        if (!isTimeAccelActive) return;
        isTimeAccelActive = false;
        timeAccelDrainAccum = 0f;
        timeAccelVfxTimer = 0f;
        PlayerTimeMultiplier = 1f;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        Physics2D.maxTranslationSpeed = defaultMaxTranslationSpeed;
        ApplyGravityScale();

        if (sr != null) sr.color = timeAccelBaseColor;
        EndActionBloom(0.15f);
        // 흑백은 회피-카운터와 같은 램프아웃으로 되돌린다. OnDisable 경로에선 코루틴을 못 돌리므로
        // (비활성 오브젝트) 그 자리에서 하드 리셋 — 어느 경로로 끝나도 화면에 흑백이 남지 않는다.
        if (isActiveAndEnabled) StartCoroutine(GrayscaleRampOut());
        else if (GrayscaleRendererFeature.Instance != null) GrayscaleRendererFeature.Instance.Intensity = 0f;

        TestLog.Event("time_accel", $"ended reason={reason} energy={currentEnergy}/{maxEnergy}");
    }

    // 실시간 기준 드레인(사용자 지시) — 초월·폭주 드레인이 쓰는 Time.deltaTime(scaled)과 달리
    // unscaledDeltaTime을 쓴다. 느려진 세계 시간으로 재면 실제 소모가 timeScale배(0.4)로 느려져
    // 사실상 무제한이 되기 때문. 정수 자원이라 1 미만을 모았다가 깎는 패턴은 초월과 동일하다.
    void DrainTimeAccel()
    {
        timeAccelDrainAccum += transcendDrainPerSecond * timeAccelDrainMultiplier * Time.unscaledDeltaTime;
        int spend = Mathf.FloorToInt(timeAccelDrainAccum);
        if (spend > 0)
        {
            timeAccelDrainAccum -= spend;
            currentEnergy = Mathf.Max(0, currentEnergy - spend);
        }
    }

    // 유지 중 연출: 흑백이 플레이어 중심에서 퍼진 채로 유지되고, 움직이는 동안 잔상이 깔린다.
    void TickTimeAccelVfx()
    {
        timeAccelVfxTimer += Time.unscaledDeltaTime;
        SetDodgeGrayscale(Mathf.Clamp01(timeAccelVfxTimer / Mathf.Max(0.0001f, dodgeGrayscaleRampIn)));

        if (!dashAfterImage) return;
        if (isDashing) return; // 대시 중엔 HandleDash가 자기 간격으로 이미 깔고 있다(중복 스폰 방지)
        if (rb == null || rb.linearVelocity.sqrMagnitude < 0.25f) return; // 멈춰 있으면 같은 자리에 겹친다

        timeAccelAfterImageTimer -= PDelta;
        if (timeAccelAfterImageTimer > 0f) return;
        timeAccelAfterImageTimer = timeAccelAfterImageInterval;
        SpawnAfterImage(FastAfterImageLifetime(afterImageLifetime));
    }

    /// <summary>시간 가속·회피-카운터의 잔상은 평소보다 fastAfterImageFadeMultiplier배 빨리 사라진다
    /// (사용자 지시 2026-08-04) — 수명을 그 배율로 나눈다. 일반 대시 잔상은 이 함수를 거치지 않는다.</summary>
    float FastAfterImageLifetime(float lifetime) =>
        lifetime / Mathf.Max(0.01f, fastAfterImageFadeMultiplier);

    /// <summary>공격 중 y를 고정해야 하는가. 지상 콤보(Slash 1/2)는 원래 스펙대로 스윙 내내 고정한다
    /// (이미 접지 상태라 체감상 무해). 점프 공격은 사용자 지시(2026-08-05)로 범위를 좁혔다 — 예전엔
    /// isAttacking 전체 구간(윈드업~회수)을 고정해서 "애니메이션이 끊기거나 공격 후에도 잠시 떠 있는"
    /// 문제가 났다. 이제 AttackHitFrame()이 세팅하는 jumpAttackHangTimer가 0보다 큰 그 짧은 순간에만
    /// 고정하고, 그 전후엔 평소처럼 중력을 받아 낙하/상승한다.</summary>
    bool AttackFreezesY => isAttacking && (!isJumpAttacking || jumpAttackHangTimer > 0f);

    /// <summary>중력은 가속도라 보정 배율의 제곱을 곱한다 — 속도가 mul배로 표현되는 세계에서 같은
    /// 실시간 낙하를 만들려면 초당 속도 증가량도 mul배여야 하는데, 그 증가량 자체가 다시 느려진
    /// 시간으로 적분되기 때문이다. 벽타기 중·공격으로 y가 고정된 동안엔 중력을 완전히 끈다.</summary>
    void ApplyGravityScale()
    {
        if (rb == null) return;
        rb.gravityScale = (isWallSliding || AttackFreezesY) ? 0f : defaultGravityScale * TimeAccelMul * TimeAccelMul;
    }

    /// <summary>폭주 중이면 공격속도 배율(애니메이터 재생속도와 공격 모션 길이가 이 값을 공유한다).</summary>
    public float AttackSpeedMultiplier => isRampaging ? Mathf.Max(0.01f, rampageAttackSpeedMultiplier) : 1f;

    /// <summary>폭주·초월 중 이동속도 배율 — HandleMovement()의 실제 속도 계산과 UpdateAnimations()의
    /// 애니메이션 재생속도가 이 값 하나를 공유한다(사용자 지시 2026-08-03: "이동속도가 빨라지면
    /// 애니메이션 속도도 빨라지게"). 벽타기(HandleWallSlide)도 같은 배율을 그대로 곱해 쓴다("벽타기도
    /// 이동속도가 증가하면 똑같이 증가").</summary>
    float MoveSpeedMultiplier => isRampaging ? rampageMoveSpeedMultiplier : isTranscending ? transcendMoveSpeedMultiplier : 1f;

    /// <summary>폭주가 풀리는 광원 수치(기본 25%). 0으로 진입하고 여기까지 회복해야 빠져나온다.</summary>
    public int RampageExitEnergy =>
        Mathf.Clamp(Mathf.CeilToInt(maxEnergy * rampageExitEnergyPercent / 100f), 1, maxEnergy);

    // 자아는 폭주 중에만 닳는다. 정수 자원이라 1 미만의 소모분은 모았다가 한 번에 깎는다
    // (폭주 드레인·광원 소모가 쓰던 것과 같은 누적 패턴).
    void DrainEgo()
    {
        if (isExecuting) return; // 조작이 막힌 연출 시간엔 자아도 닳지 않는다(광원 드레인과 같은 이유)

        egoDrainAccum += egoDrainPerSecond * Time.deltaTime;
        int spend = Mathf.FloorToInt(egoDrainAccum);
        if (spend > 0)
        {
            egoDrainAccum -= spend;
            int before = currentEgo;
            currentEgo = Mathf.Max(0, currentEgo - spend);
            if (before > 0 && currentEgo == 0)
            {
                egoDepletedTimer = 0f; // 붕괴 시작 — 첫 피해는 한 주기(5초)를 채운 뒤에 들어간다
                ScreenGlitchFx.Begin(); // 자아 고갈 — 화면 전체 글리치(사용자 지시 2026-08-01)
                TestLog.Event("ego", "depleted");
            }
        }

        // 자아가 0인 동안에만 몸이 무너진다. 공격을 맞혀 자아가 다시 차면 타이머가 리셋되며
        // 디버프가 사라진다(사용자 확정: "자아가 다시 차면 HP감소 디버프 사라짐"). 글리치도 같이 끊는다.
        if (currentEgo > 0)
        {
            egoDepletedTimer = 0f;
            egoDepletedEnergyDrainAccum = 0f;
            ScreenGlitchFx.End();
            return;
        }

        // 자아가 바닥난 동안엔 광원도 서서히 깎인다(사용자 지시 2026-08-02). HP 붕괴 틱과 달리 매 프레임
        // 연속으로 깎이므로 별도 주기 없이 그냥 여기서 바로 처리한다.
        egoDepletedEnergyDrainAccum += egoDepletedEnergyDrainPerSecond * Time.deltaTime;
        int energySpend = Mathf.FloorToInt(egoDepletedEnergyDrainAccum);
        if (energySpend > 0)
        {
            egoDepletedEnergyDrainAccum -= energySpend;
            currentEnergy = Mathf.Max(0, currentEnergy - energySpend);
        }

        egoDepletedTimer += Time.deltaTime;
        if (egoDepletedTimer < egoDepletedDamageInterval) return;

        egoDepletedTimer -= egoDepletedDamageInterval;
        TakeDamage(egoDepletedDamage);
        TestLog.Event("ego", $"collapse_damage -{egoDepletedDamage} hp={currentHealth}/{maxHealth}");
    }

    // ── 마스크 기반 블룸 부착 헬퍼 ────────────────────────────────────────────────────────────
    // C-4 덕분에 PlayerBloomFx는 시트에 대응하는 발광 마스크를 자동으로 물린다(눈·글리치만 빛남).
    // 여기선 "어느 구간에 어떤 색으로 켜는가"만 정한다.
    static readonly Color RampageBloomTint = new Color(1f, 0.10f, 0.06f, 1f); // RampageCore 계열
    static readonly Color TranscendBloomTint = new Color(0.10f, 0.95f, 1.00f, 1f); // 폭주 붉은색의 색상환 반대편(cyan)

    /// <summary>광원 픽셀 VFX(흡수·방출·상승) 공통 색상 — 폭주 중엔 붉은색, 그 외(초월 포함 평상시)엔
    /// 흰색(사용자 지시 2026-08-02). 몸 마스크 블룸(RampageBloomTint/TranscendBloomTint)과는 별개 —
    /// 그쪽은 이번 지시 대상이 아니라 손대지 않는다.</summary>
    Color CurrentPixelTint => isRampaging ? RampageBloomTint : Color.white;

    // 폭주 중엔 화면이 완전 암전이고 실드 VFX는 보호 레이어라, 평소엔 은은하던 세로 스캔라인이
    // "플레이어가 여러 개로 보이는" 수준으로 튀어 보인다(사용자 스크린샷 2026-08-01).
    // 판정(parryShieldActive)은 그대로 두고 **그림만** 숨긴다 — 폭주 중엔 어차피 패링이 봉인이라
    // 실드가 새로 생기지도 않는다.
    void SetParryShieldVisible(bool visible)
    {
        var fx = GetComponentInChildren<ParryShieldFx>(true);
        if (fx == null) return;
        foreach (var r in fx.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }

    // 암전이 완전히 걷힌 뒤에 실드 그림을 되살린다(RampageVisionFx는 페이드아웃이 끝나면 스스로 파괴된다).
    System.Collections.IEnumerator RestoreParryShieldAfterVision()
    {
        while (RampageVisionFx.Instance != null) yield return null;
        if (!isRampaging) SetParryShieldVisible(true); // 기다리는 사이 다시 폭주했으면 숨긴 채로 둔다
    }

    /// <summary>대시·카운터·처형처럼 짧은 구간에 켜지는 블룸(같은 슬롯을 재사용해 중첩을 막는다).</summary>
    void BeginActionBloom(float k)
    {
        EndActionBloom(0.05f);
        actionBloomFx = PlayerBloomFx.Attach(transform, playerBloomMaterial, playerBloomSortingOffset);
        if (actionBloomFx == null) return;
        if (isRampaging) actionBloomFx.SetColor(RampageBloomTint); // 폭주 중엔 모든 빛이 붉다
        actionBloomFx.SetIntensity(k);
    }

    void EndActionBloom(float fade = 0.12f)
    {
        if (actionBloomFx == null) return;
        actionBloomFx.FadeOut(fade);
        actionBloomFx = null;
    }

    /// <summary>공격이 적중하면 자아가 회복된다(폭주 중에만 의미가 있다).</summary>
    void RestoreEgo()
    {
        if (!isRampaging || egoGainPerHit <= 0) return;
        currentEgo = Mathf.Min(maxEgo, currentEgo + egoGainPerHit);
    }

    void StartRampage()
    {
        isRampaging = true;
        rampageDrainAccum = 0f;
        currentEgo = maxEgo;   // 자아는 폭주와 함께 생겼다가 함께 사라진다
        egoDrainAccum = 0f;
        egoDepletedTimer = 0f;
        egoDepletedEnergyDrainAccum = 0f;
        RampageVisionFx.Begin(transform); // 시야 제한(B-2) — 이성의 붕괴를 게임플레이로 옮긴 것
        // 진입 순간에만 재생되는 하트비트 연출(화면 붉은 펄스 + 카메라 펀치, 사용자 지시 2026-08-02).
        // 폭주 지속 중엔 관여하지 않고 1회 재생 후 스스로 파괴된다.
        RampageHeartbeatFx.Begin(transform, sectionCamera, focusPulseRampIn, focusPulseHold, focusPulseRampOut);
        RampageGlitchFlicker(); // 같은 순간 스프라이트도 잠깐 글리치 프레임으로 튄다
        SetParryShieldVisible(false);     // 실드 스캔라인이 암전 위에서 과하게 튄다(판정은 유지)
        // ⚠️ 예전엔 여기서 스프라이트를 붉게 틴트했는데, 그러면 몸 전체가 붉어져 "블룸"이 아니라
        //    "빨간 캐릭터"가 됐다(사용자 피드백). 이제 덮어쓰기 셰이더가 마스크 부위만 처리하므로
        //    원본 스프라이트는 손대지 않는다.

        // 상시 붉은 블룸(사용자 지시). 화면이 완전 암전이라 이게 없으면 플레이어가 검은 덩어리로만 보인다 —
        // 마스크 덕분에 눈·글리치 같은 발광부만 붉게 타오른다.
        if (rampageBloomFx == null)
        {
            // ⚠️ 가산(PlayerBloomOverlay)이 아니라 **덮어쓰기**(PlayerMaskEmissive)를 쓴다.
            //    가산은 원본 청록과 섞여 분홍이 되고, 세기를 올리면 몸 전체가 물들어 "블룸"으로 안 읽혔다
            //    (사용자 피드백 2026-08-01). 덮어쓰기는 마스크 부위만 정확히 붉게 치환하고 그 부분만 빛난다.
            rampageBloomFx = PlayerBloomFx.AttachWithShader(transform, "Custom/PlayerMaskEmissive", playerBloomSortingOffset);
            if (rampageBloomFx != null)
            {
                rampageBloomFx.SetColor(RampageBloomTint);
                rampageBloomFx.SetBoost(5f);     // HDR — 임계값(1.15)을 크게 넘겨 그 부위가 확실히 빛나게
                rampageBloomFx.SetMaskFloor(0f); // 마스크 부위'만' (사용자 지시)
                // 칼날처럼 원래 흰 부위도 빛난다(사용자 요청). 색은 마스크와 **같은 붉은색**으로 맞추고
                // 세기만 약간 낮춘다 — 흰 계열로 줬더니 칼만 하얗게 튀어 톤이 깨졌다(사용자 피드백).
                // 칼도 마스크와 **같은 색·같은 세기**로 빛난다(사용자 확정). weight는 "덮는 정도"(알파)라
                // 1이어야 원본 흰색이 비쳐 분홍이 되지 않는다. 색이 _Color와 같으므로 HDR 출력도 동일하다.
                rampageBloomFx.SetBrightEmission(1f, RampageBloomTint);
                rampageBloomFx.SetIntensityRaw(1f);
            }
        }

        TestLog.Event("rampage", $"started energy={currentEnergy}/{maxEnergy} ego={currentEgo}/{maxEgo}");
    }

    void EndRampage(string reason)
    {
        if (!isRampaging) return;
        isRampaging = false;
        rampageDrainAccum = 0f;
        currentEgo = 0;        // 바가 사라진다(HUD는 폭주 중에만 그린다)
        egoDrainAccum = 0f;
        egoDepletedTimer = 0f; // 폭주가 끝나면 붕괴 디버프도 같이 끝난다
        egoDepletedEnergyDrainAccum = 0f;
        RampageVisionFx.End(); // 페이드아웃 후 스스로 파괴(화면·아웃라인 전부 원복)
        ScreenGlitchFx.End();  // 자아 고갈 글리치도 같이 끝난다(붕괴 중 폭주가 풀린 경우 대비)
        if (rampageBloomFx != null) { rampageBloomFx.FadeOut(0.25f); rampageBloomFx = null; }
        // ⚠️ 여기서 바로 되살리면 안 된다 — 시야 제한은 0.30s에 걸쳐 페이드아웃하므로, 그 동안 화면은
        //    아직 어둡고 실드는 보호 레이어라 스캔라인이 "스프라이트가 여러 개"처럼 번쩍인다(사용자 지적).
        //    암전이 완전히 걷힌 뒤에 되살린다.
        StartCoroutine(RestoreParryShieldAfterVision());
        TestLog.Event("rampage", $"ended reason={reason} energy={currentEnergy}/{maxEnergy}");
    }

    /// <summary>폭주 중이면 플레이어가 주는 피해를 배율만큼 올린다(0 이하로 깎이지 않게 최소 1 보장).</summary>
    int RampageDamage(int damage)
    {
        if (!isRampaging || damage <= 0) return damage;
        return Mathf.Max(1, Mathf.RoundToInt(damage * rampageDamageMultiplier));
    }

    public bool IsRampaging => isRampaging;

    void StartTranscend()
    {
        isTranscending = true;
        transcendDrainAccum = 0f;
        TranscendVisionFx.Begin(transform); // 적 미래 공격 범위 예고(T-3) — 폭주의 RampageVisionFx.Begin과 같은 자리

        // 진입 순간에만 재생되는 1회성 연출(사용자 지시 2026-08-02): 주변의 블룸된 픽셀 광원들이
        // 플레이어 중앙으로 흡수된다 — "빛을 고압력으로 뿜어내기 전 끌어모으는" 그림. 폭주의 하트비트
        // FX와 같은 자리(진입 1회). 기존 C-1 광원 획득 흡수(LightPixelFx.SpawnAbsorb)를 그대로
        // 재사용 — 신규 VFX 코드 0. 실제 광원 획득이 아니라 순수 장식이라 onArrivePixel 콜백은
        // 비워 둔다(게이지에 반영 안 함).
        LightPixelFx.SpawnAbsorb(transform.position, transform, transcendAbsorbPixelCount, null,
            sourceRadius: transcendAbsorbSourceRadius, pivotOffset: lightPixelPivotOffset, color: CurrentPixelTint);

        // "모아서(흡수) → 터뜨린다(방출)" 서사를 잇는 릴리즈 비트(PLAN §13 B안, 사용자 지시 2026-08-02).
        // 흡수 픽셀이 도착하는 타이밍에 맞춰 자체 딜레이 후 플래시+링+카메라 펀치 각 1회만 낸다 —
        // 폭주(RampageHeartbeatFx)의 2박동·글리치·슬로우모와 의도적으로 대칭이 아니라 대비된다.
        TranscendBurstFx.Begin(transform, sectionCamera, focusPulseRampIn, focusPulseHold, focusPulseRampOut);

        // (몸을 감싸던 cyan 상시 아우라(RampageAuraFx)는 2026-08-04 사용자 지시로 제거 — "아우라 느낌의
        //  이상한 원 형태". 폭주에서 두 번, 초월에서 한 번, 총 세 번 거절된 연출이라 되살리지 말 것.
        //  진입 1회성 연출(TranscendBurstFx의 플래시+링)과 유지 중 떠오르는 픽셀은 그대로 유지된다.)
        // 위로 떠오르는 픽셀은 HandleTranscend → TickTranscendPixelRise가 매 프레임 틱을 굴린다.

        // cyan 상시 블룸 — 폭주 블룸 블록(StartRampage)과 같은 셰이더·마스크, 색·세기만 다르다.
        // ⚠️ 덮어쓰기(PlayerMaskEmissive)를 쓴다 — 가산이 아니라 마스크 부위만 정확히 치환한다(폭주와 같은 이유).
        if (transcendBloomFx == null)
        {
            transcendBloomFx = PlayerBloomFx.AttachWithShader(transform, "Custom/PlayerMaskEmissive", playerBloomSortingOffset);
            if (transcendBloomFx != null)
            {
                transcendBloomFx.SetColor(TranscendBloomTint);
                // ⚠️ 폭주(5.0)보다 낮다 — 폭주는 화면이 완전 암전이라 5.0이어야 읽혔지만, 초월은 화면이
                //    평상시 밝기 그대로라 같은 값이면 과포화된다(초안값, 실측 후 조정 — PLAN §5).
                transcendBloomFx.SetBoost(3.5f);
                transcendBloomFx.SetMaskFloor(0f);   // 마스크 부위'만'
                transcendBloomFx.SetBrightEmission(1f, TranscendBloomTint); // 칼날 등 원래 밝은 부위도 같은 색
                transcendBloomFx.SetIntensityRaw(1f);
            }
        }

        TestLog.Event("transcend", $"started energy={currentEnergy}/{maxEnergy}");
    }

    void EndTranscend(string reason)
    {
        if (!isTranscending) return;
        isTranscending = false;
        transcendDrainAccum = 0f;
        transcendPixelRiseAccum = 0f;
        TranscendVisionFx.End(); // 페이드아웃 후 스스로 파괴(예고 원 전부 원복)
        if (transcendBloomFx != null) { transcendBloomFx.FadeOut(0.25f); transcendBloomFx = null; }
        TestLog.Event("transcend", $"ended reason={reason} energy={currentEnergy}/{maxEnergy}");
    }

    public bool IsTranscending => isTranscending;

    /// <summary>자아 붕괴 타이머의 다음 피해까지 진행률(0=방금 틱/1=다음 틱 직전, 자아가 있으면 0).
    /// PlayerHudUI가 마지막 HP 칸의 "위→아래로 줄어드는" 붕괴 연출에 그대로 쓴다(사용자 지시 2026-08-01).</summary>
    public float EgoDepletedProgress => (isRampaging && currentEgo <= 0)
        ? Mathf.Clamp01(egoDepletedTimer / Mathf.Max(0.0001f, egoDepletedDamageInterval)) : 0f;

    // ── 광원 소모(Light Spend): E 홀드 ────────────────────────────────────────────────────────
    // 캐스팅류 — 시작 시 제자리에 고정되고(이동·점프·대시 잠금 + Idle 프리즈), 초당 lightSpendDrainPerSecond
    // 만큼 에너지를 소모해 lightSpendHealThreshold가 모일 때마다 체력 1칸(만체력이면 실드 1개)으로 바꾼다.
    // 피격·폭주 진입·에너지 소진·10% 경고 어느 쪽으로든 즉시 중단되고, 재개하려면 E를 다시 눌러야 한다.
    void HandleLightSpend()
    {
        // 낮은 에너지 경고 플래그는 방출 여부와 무관하게 매 프레임 갱신한다 — 전투로 회복해도
        // 다음 방출에서 경고가 다시 작동하게 하려면 필요하다.
        if (currentEnergy > lightSpendLowWarnPercent * maxEnergy) lightSpendLowWarned = false;

        if (!isSpendingLight)
        {
            if (KeyPressedThisFrame(Key.E))
            {
                if (isRampaging) TestLog.Event("light_spend", "blocked_rampage");
                else if (currentEnergy <= 0) TestLog.Event("light_spend", "blocked_no_energy");
                else if (CanStartLightSpend()) StartLightSpend();
            }
            return;
        }

        if (isRampaging) { EndLightSpend("blocked_rampage"); return; }
        if (!KeyHeld(Key.E)) { EndLightSpend("released"); return; }

        lightSpendDrainAccum += lightSpendDrainPerSecond * Time.deltaTime;
        int spend = Mathf.FloorToInt(lightSpendDrainAccum);
        if (spend > 0)
        {
            spend = Mathf.Min(spend, currentEnergy);
            lightSpendDrainAccum -= spend;
            currentEnergy -= spend;
            lightSpendHealAccum += spend;

            while (lightSpendHealAccum >= lightSpendHealThreshold)
            {
                lightSpendHealAccum -= lightSpendHealThreshold;
                if (currentHealth < maxHealth) Heal(1);
                else if (!HasParryShield) SpawnParryShield();
            }
        }

        if (currentEnergy <= 0) { EndLightSpend("energy_empty"); return; }

        if (!lightSpendLowWarned && currentEnergy <= lightSpendLowWarnPercent * maxEnergy)
        {
            lightSpendLowWarned = true;
            EndLightSpend("low_energy");
            return;
        }

        // 방출 픽셀 — 초당 lightSpendPixelRate개, 장식용(에너지 콜백 없음).
        lightSpendPixelAccum += lightSpendPixelRate * Time.deltaTime;
        while (lightSpendPixelAccum >= 1f)
        {
            lightSpendPixelAccum -= 1f;
            LightPixelFx.SpawnEmitOne(sr != null ? LightPixelFx.ComputePivot(sr, lightPixelPivotOffset) : transform.position, CurrentPixelTint);
        }
    }

    bool CanStartLightSpend()
    {
        // IsActionIdle과 같은 플래그 목록을 공유한다(사용자 승인 리팩터, 2026-08-02) — 안 그러면
        // "지금 자유로운가"의 정의가 두 곳에 생겨 이중 진실이 된다(DummyEnemy의 처형 임계값 사고 선례).
        // isWallSliding은 IsActionIdle에 안 넣고 여기 직접 추가한다(사용자 지시 2026-08-03) — IsActionIdle에
        // 넣으면 폭주/초월 진입 지연 조건까지 덩달아 바뀌는데, 이번 지시는 공격·E홀드·일섬·패링 네
        // 가지로 한정됐다(범위 확대 방지).
        return currentEnergy > 0 && isGrounded && !isRampaging && IsActionIdle && !isWallSliding;
    }

    void StartLightSpend()
    {
        isSpendingLight = true;
        lightSpendDrainAccum = 0f;
        lightSpendHealAccum = 0f;
        lightSpendPixelAccum = 0f;

        FreezeAnimAt(ilseomExitState, 0, 1); // Idle 0프레임 고정(일섬 차지 홀드와 같은 헬퍼)

        lightSpendBloomFx = PlayerBloomFx.Attach(transform, playerBloomMaterial, playerBloomSortingOffset);
        lightSpendBloomFx?.SetIntensity(1f);

        if (sectionCamera != null)
        {
            sectionCamera.SetSustainedFocus(transform, lightSpendCamPan, lightSpendZoomTarget, lightSpendZoomRampIn,
                lightSpendCamPanDownMax);
            sectionCamera.SetSustainedShake(lightSpendSustainedShake);
        }

        TestLog.Event("light_spend", $"started energy={currentEnergy}/{maxEnergy}");
    }

    // 발밑 바닥의 월드 Y — E홀드 카메라가 그 아래로는 못 내려가게 클램프하는 기준(사용자 지시
    // 2026-08-02). CanStartLightSpend()가 isGrounded를 요구하므로 이 시점엔 반드시 바닥 위에 서
    // 있다 — CheckEnvironment()의 접지 판정(groundLayer, BoxCast)과 같은 레이어를 쓴다.
    float GetFloorY()
    {
        Bounds b = coll.bounds;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(b.center.x, b.center.y), Vector2.down, b.extents.y + 0.5f, groundLayer);
        return hit.collider != null ? hit.point.y : b.min.y;
    }

    void EndLightSpend(string reason)
    {
        if (!isSpendingLight) return;
        isSpendingLight = false;
        lightSpendDrainAccum = 0f;
        lightSpendHealAccum = 0f;

        RestoreAnimAfterIlseom(); // Idle 프리즈 해제(같은 헬퍼 — 내부에서 ilseomExitState로 복귀)

        if (lightSpendBloomFx != null) { lightSpendBloomFx.FadeOut(0.2f); lightSpendBloomFx = null; }

        if (sectionCamera != null)
        {
            sectionCamera.ClearSustainedFocus(lightSpendZoomRampOut);
            sectionCamera.SetSustainedShake(0f);
        }

        TestLog.Event("light_spend", $"ended reason={reason} energy={currentEnergy}/{maxEnergy}");
    }

    // 커서 아래에서 "지금 처형 가능한" 적을 찾는다. HP 비율 판정은 여기 한 곳에서만 한다.
    DummyEnemy FindExecutableUnderCursor()
    {
        Camera cam = Camera.main;
        if (cam == null || Mouse.current == null) return null;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        Collider2D hit = Physics2D.OverlapPoint(worldPos, enemyLayer);
        if (hit == null) return null;

        DummyEnemy e = hit.GetComponent<DummyEnemy>();
        if (e == null || !e.IsAlive || e.HpRatio > executionHpThreshold) return null;
        return e;
    }

    // 타겟이 실제로 바뀐 프레임에만 글로우/UI를 건드린다(매 프레임 재생성 방지).
    void UpdateExecutionTargeting(DummyEnemy hovered)
    {
        if (executionTarget == hovered) return;

        ClearExecutionTargeting();
        if (hovered == null) return;

        executionTarget = hovered;
        executionGlowFx = EnemyExecutionGlowFx.Attach(
            hovered.transform, enemyExecutionGlowMaterial, enemyGlowSortingOffset);
        if (executionGlowFx != null) executionGlowFx.FadeIn(1f, executionGlowFadeIn);
        ExecutionUI.GetOrCreate().ShowPrompt();
        TestLog.Event("execution", "target_on " + hovered.name + " hp=" + hovered.HpRatio.ToString("F2"));
    }

    void ClearExecutionTargeting()
    {
        if (executionTarget == null && executionGlowFx == null) return; // 매 프레임 호출돼도 싸게 빠진다
        if (executionGlowFx != null) { executionGlowFx.FadeOut(executionGlowFadeOut); executionGlowFx = null; }
        ExecutionUI.GetOrCreate().HidePrompt();
        executionTarget = null;
        TestLog.Event("execution", "target_off");
    }

    // ── 처형: 발동 시퀀스 ────────────────────────────────────────────────────────────────────
    // Glitch Out → 적 위치에 Glitch Slices 스폰 + 적 쪽으로 이동 → Glitch Sweep(첫 프레임에 즉사 데미지).
    // ★ try/finally로 무적·레이어·상태를 항상 복원(DodgeCounterRoutine/IlseomRoutine과 같은 구조적 안전망).
    System.Collections.IEnumerator ExecutionRoutine(DummyEnemy target)
    {
        isExecuting = true;
        executionTarget = null; // 타겟팅 UI 정리(시퀀스 중엔 불필요)
        if (executionGlowFx != null) { executionGlowFx.FadeOut(0.1f); executionGlowFx = null; }
        BeginActionBloom(1f); // 처형 구간 마스크 블룸(사용자 지시) — "빛을 강탈"하는 순간이라 강하게

        int dirX = (sr != null && sr.flipX) ? -1 : 1;
        Color baseColor = sr != null ? sr.color : Color.white;

        // 무적 + 충돌 무시
        if (invincibleLayer != -1) gameObject.layer = invincibleLayer;
        rb.linearVelocity = Vector2.zero;
        LockAnimForIlseom(); // 일섬과 동일하게 AnyState 전이를 막는다

        TestLog.Event("execution", "start dir=" + dirX);

        try
        {
            // ── Phase 1(스펙 2): Glitch Out 재생 + 적 자리에 Glitch Slices + 적 위치로 이동 ──
            float dx = target.transform.position.x - transform.position.x;
            if (!Mathf.Approximately(dx, 0f)) dirX = dx > 0f ? 1 : -1; // 적을 바라본다(flipX는 PlayIlseomState가 준다)

            PlayIlseomState(ilseomChargeState, dirX, false);

            Vector3 start = transform.position;
            Vector3 targetPos = ResolveExecutionDestination(target, start);
            SpawnGlitchSlices(target, dirX);

            // 파고드는 이동은 Glitch Out 재생 구간 안에서 끝난다 — 스펙 3의 "2번의 과정이 끝나면 Sweep"을
            // 지키려면 Sweep은 Glitch Out 클립이 다 돌아간 뒤에 시작해야 하기 때문.
            float rush = Mathf.Clamp(executionRushDuration, 0.01f, ilseomGlitchOutDuration);
            float t = 0f;
            while (t < rush)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, targetPos, Mathf.Clamp01(t / rush));
                yield return null;
            }
            transform.position = targetPos;

            float rest = ilseomGlitchOutDuration - rush;
            if (rest > 0f) yield return new WaitForSeconds(rest);

            // ── Phase 2(스펙 3): Glitch Sweep 재생 + 그 첫 프레임에 즉사 피격 ──
            PlayIlseomState(ilseomSweepState, dirX, true);
            // 베는 순간 카메라가 처형 지점으로 잠깐 파고든다(일섬과 같은 FocusPulse, 피해 처리와 분리).
            if (sectionCamera != null && target != null)
                sectionCamera.FocusPulse(target.transform.position, executionCamPanAmount, executionCamZoomAmount,
                    focusPulseRampIn, focusPulseHold, focusPulseRampOut);

            if (target != null && target.gameObject.activeInHierarchy)
            {
                // 남은 HP 전부를 그대로 준다 — 어떤 체력에서도 반드시 죽고, 넘치는 매직넘버가 없다.
                int lethalDamage = Mathf.Max(1, target.currentHp);
                Vector2 facing = dirX > 0 ? Vector2.right : Vector2.left;
                Vector3 hitPos = target.transform.position;

                target.TakeDamage(lethalDamage, facing.x * attackLungeDistance * enemyKnockbackMultiplier);
                CombatFx.SpawnHitVfx(executionHitVfxPrefab, hitPos, facing, hitVfxOffsetTowardsEnemy);
                // "처형됨!!" 붉은 텍스트 — 숫자는 표시하지 않는다(스펙 3).
                CombatFx.SpawnDamageText(damageTextPrefab, hitPos, executionText, executionTextColor, true);

                // 카메라 쉐이크 + 히트스톱 (크리티컬 배율 적용)
                if (sectionCamera != null)
                    sectionCamera.Shake(attackShakeDuration * critShakeMultiplier,
                        attackShakeMagnitude * critShakeMultiplier);
                if (attackHitstop) StartCoroutine(AttackHitstopCo(critHitstopMultiplier));

                // "성공 시 체력이나 에너지를 대폭 회복함"(기능_구현_명세서)
                Heal(executionHealCount);
                AddEnergy(executionEnergyGain);

                TestLog.Event("execution", $"hit dmg={lethalDamage}");
            }

            yield return new WaitForSeconds(ilseomSweepDuration);   // Sweep 재생 대기
            yield return new WaitForSecondsRealtime(executionHold); // 여운
        }
        finally
        {
            isExecuting = false;
            EndActionBloom(0.2f); // 처형 블룸도 어떤 경로로 끝나든 반드시 꺼진다
            if (invincibleLayer != -1) gameObject.layer = normalLayer;
            if (sr != null) { sr.color = baseColor; sr.flipX = (dirX < 0); }
            RestoreAnimAfterIlseom(); // 일섬과 동일하게 애니메이터 복원
            ExecutionUI.GetOrCreate().HidePromptImmediate();
            TestLog.Event("execution", "end");
        }
    }

    // 처형의 도착 지점. 대시-카운터(CounterRush)는 `behind.y = start.y`로 y를 아예 안 옮기는데,
    // 처형도 같은 규칙을 따른다(스펙 2 "y좌표를 이동할 필요가 없는 경우 이동하지 않는다").
    // 다만 적이 executionYSnapThreshold보다 멀리 위/아래에 있으면 그대로는 닿지 않으므로 그때만 y를 옮기고,
    // 이때 기준은 적의 콜라이더 밑면이다 — 플레이어 스프라이트의 피봇이 발밑(y=0.03)이라
    // 적의 중심(=transform.position, 정사각 스프라이트) 높이로 맞추면 공중에 뜬 것처럼 보인다.
    Vector3 ResolveExecutionDestination(DummyEnemy target, Vector3 start)
    {
        Vector3 dest = target.transform.position;
        dest.y = EnemyFootY(target);
        if (Mathf.Abs(dest.y - start.y) <= executionYSnapThreshold) dest.y = start.y;
        dest.z = start.z;
        return dest;
    }

    static float EnemyFootY(DummyEnemy target)
    {
        Collider2D c = target.GetComponent<Collider2D>();
        return c != null ? c.bounds.min.y : target.transform.position.y;
    }

    // 스펙 2: 적 위치에 Glitch Slices를 재생한다. 적은 자기 애니메이터가 따로 있어 이 클립을 얹을 수 없으므로,
    // 플레이어의 컨트롤러를 물린 1회용 SpriteRenderer를 적 자리에 세워 재생한다.
    // (Glitch Slices는 나가는 전이가 없는 고아 상태라 anim.Play로 바로 재생된다 — add-combat-move SKILL STEP5.
    //  새 Animator의 파라미터는 전부 기본값이라 AnyState→Fall/Jump/Wall Slide 조건도 성립하지 않는다.)
    void SpawnGlitchSlices(DummyEnemy target, int dirX)
    {
        if (anim == null || anim.runtimeAnimatorController == null || sr == null) return;

        Vector3 pos = target.transform.position;
        pos.y = EnemyFootY(target);
        pos += (Vector3)executionSlicesOffset;

        var go = new GameObject("GlitchSlicesFx");
        go.transform.position = pos;
        go.transform.localScale = transform.lossyScale; // 플레이어와 같은 도트 크기
        int noGrayscaleLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (noGrayscaleLayer >= 0) go.layer = noGrayscaleLayer; // 흑백 확산 중에도 원색 유지

        var r = go.AddComponent<SpriteRenderer>();
        r.sharedMaterial = sr.sharedMaterial;
        r.flipX = dirX < 0;
        r.sortingLayerID = sr.sortingLayerID;
        r.sortingOrder = sr.sortingOrder + executionSlicesSortingOffset;

        var a = go.AddComponent<Animator>();
        a.runtimeAnimatorController = anim.runtimeAnimatorController;
        // 바로 뒤 Sweep 첫 프레임에서 히트스톱(timeScale≈0)이 걸리므로 스케일된 시간으로 두면 재생이 멈춘다.
        a.updateMode = AnimatorUpdateMode.UnscaledTime;
        a.Play(executionSlicesState, 0, 0f);
        a.Update(0f); // 0프레임을 즉시 기록해 한 프레임도 기본 상태가 보이지 않게

        StartCoroutine(DestroyAfterRealtime(go, executionSlicesDuration));
        TestLog.Event("execution", "slices at=" + pos.ToString("F2"));
    }

    // Destroy(go, delay)는 스케일된 시간이라 히트스톱 중에 멈춘다 — Animator(UnscaledTime)와 시계를 맞춘다.
    System.Collections.IEnumerator DestroyAfterRealtime(GameObject go, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (go != null) Destroy(go);
    }

    // 대시 종료 공통 처리(정상 타임아웃/벽 취소/회피-카운터 종료 모두 여기로 모음).
    void EndDash(string reason)
    {
        isDashing = false;
        // 회피-카운터로 이어지는 중이면 그쪽이 자기 블룸을 다시 켜므로 여기선 끄기만 하면 된다.
        if (!isDodgeCountering) EndActionBloom(0.1f);
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
        if (isRampaging) { TestLog.Event("dodge_counter", "blocked_rampage"); return false; } // 폭주 중 봉인(사용자 지시)
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
        // 회피-카운터는 자기 timeScale(dodgeCounterSlowScale)을 직접 소유하는 구간이라 시간 가속과
        // 겹치면 서로의 시계를 덮어쓴다 — 여기서 먼저 확실히 끝내고 시작한다(스프라이트 색 복원도
        // 이 시점에 끝나야 아래 baseColor가 "가속 틴트"가 아닌 원래 색을 집는다).
        EndTimeAccel("dodge_counter");

        isDodgeCountering = true;
        BeginActionBloom(1f); // 회피-카운터 구간 마스크 블룸(사용자 지시)
        parryPressed = false;
        // 윈도우가 열리기 "전부터" F/우클릭을 이미 누르고 있던 경우(선입력) 구제: OnParry는 press 엣지
        // 이벤트라서 이미 눌려있는 버튼은 새 이벤트를 발생시키지 않아 위 리셋 이후 감지가 안 됨 — 그 결과
        // 윈도우가 조용히 만료될 때까지 반응이 없다가, 사용자가 떼었다 다시 눌러야 그제서야 잡히는 것처럼
        // 보였음("판정이 늦게 되는 것 같다" 버그의 실제 원인, 홀드 재현으로 확인). 윈도우가 열리는 시점의
        // 현재 홀드 상태를 한 번 직접 확인해 즉시 확인 처리한다.
        bool heldAtWindowOpen = KeyHeld(Key.F) ||
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
            // 슬로우모션 중 잔상 뭉침 방지용 — 직전 스폰 위치(초기값은 멀리 둬서 첫 스폰은 항상 찍힘).
            Vector3 lastAfterImageSpawnPos = transform.position - Vector3.right * 999f;
            const float minSlowMoAfterImageGap = 0.12f; // CounterRush의 최소 간격과 동일 기준
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
                            afterImageTimer = afterImageInterval;
                            // ⚠️ 이 루프는 Update 프레임마다 도는데, 실제 이동은 FixedUpdate(물리)가
                            // 담당한다 — 슬로우모션(Time.timeScale이 낮음) 중엔 FixedUpdate 빈도가
                            // 실시간 기준으로 뚝 떨어져서, 위치가 실제로 안 바뀐 채로 이 타이머만 여러 번
                            // 먼저 만료돼 잔상이 같은 자리에 겹겹이 쌓였다(사용자 스크린샷: "패링 실드
                            // 켜져있을 때 대시 성공 시 캐릭터 여러 개"). CounterRush의 최소 간격 트릭과
                            // 같은 방식으로, 직전 스폰 위치에서 실제로 벌어졌을 때만 찍는다.
                            if (Vector3.Distance(transform.position, lastAfterImageSpawnPos) >= minSlowMoAfterImageGap)
                            {
                                // 회피-카운터 잔상은 2배 빨리 사라진다(사용자 지시 2026-08-04)
                                SpawnAfterImage(FastAfterImageLifetime(afterImageLifetime));
                                lastAfterImageSpawnPos = transform.position;
                            }
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
            EndActionBloom(0.15f); // 카운터 블룸도 어떤 경로로 끝나든 반드시 꺼진다
            // 어떤 경로(성공/만료/예외/중단)로 끝나도 항상 복원 — 슬로우모션/입력잠금 stuck 방지.
            // 1f가 아니라 BaseTimeScale인 이유는 그 프로퍼티 주석 참고(가속 중이면 그쪽 값이 기본).
            Time.timeScale = BaseTimeScale;
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

        // 돌진 경로가 적의 x좌표를 지나치는 순간을 기준으로 방향을 바꾼다: 지나치기 전엔 진행
        // 방향(=적 쪽)을 보고, 지나친 뒤엔 반대로 돌아서서 적을 본다(사용자 지시 2026-08-05 —
        // "적을 관통해서 지나갈 때 그거에 맞춰 바라봐야 함"). start==behind.x인 초근접 케이스는
        // enemyFacing으로 폴백.
        float travelDir = !Mathf.Approximately(behind.x, start.x) ? Mathf.Sign(behind.x - start.x) : enemyFacing;
        float enemyX = target.transform.position.x;
        bool PassedEnemy(float x) => travelDir > 0f ? x >= enemyX : x <= enemyX;
        void UpdateRushFacing(float x)
        {
            if (sr == null) return;
            bool passed = PassedEnemy(x);
            sr.flipX = passed ? (travelDir > 0f) : (travelDir < 0f);
        }

        if (anim != null) anim.enabled = true; // 대시 프리즈 해제(돌진 모션이 보이도록)
        rb.linearVelocity = Vector2.zero;

        // 카운터 러시 중 잔상 다량 생성(사용자 요청) — 러시가 0.12s로 워낙 짧아 "프레임당 1회 스폰"
        // 방식은 프레임레이트에 막혀 실제로는 몇 개 안 나가는 문제가 있었음. 경로를 미리 계산해
        // start→behind 사이를 균등 분할한 지점에 한 번에(같은 프레임 안, yield 없이) 깔아둔다 —
        // 프레임레이트/타임스케일과 무관하게 항상 동일한 밀도가 보장됨. 실제 이동은 텔레포트 후 즉시
        // 원위치 복구라 화면에는 순간이동이 안 보이고(같은 프레임 안이라 렌더 안 됨) 잔상만 경로에 남는다.
        // ⚠️ start와 behind가 가까우면(예: 패링 직후처럼 이미 적과 거의 붙어 있던 경우) 균등분할
        // 지점들이 전부 한 자리에 겹쳐 찍혀 "잔상 여러 장이 한 곳에 쌓여 세로로 뭉쳐 보이는" 버그가
        // 있었다(사용자 스크린샷 확인). 직전 스폰 지점과 최소 간격 이상 떨어졌을 때만 실제로 찍는다.
        int burstCount = Mathf.Max(4, Mathf.RoundToInt(dodgeCounterRushDuration / Mathf.Max(0.001f, dodgeCounterAfterImageInterval)));
        burstCount = Mathf.Min(burstCount, 40); // 과도한 스폰 방지
        // start≈behind(적과 이미 거의 붙어 있던 경우 등 경로가 짧을 때)에도 최소 간격을 보장해야
        // 위 버그가 다시 재현되지 않는다 — 경로 길이에 비례하는 값과 고정 최솟값(0.12) 중 큰 쪽.
        float minSpawnGap = Mathf.Max(0.12f, Mathf.Abs(behind.x - start.x) / burstCount * 0.5f);
        float lastSpawnX = start.x - minSpawnGap - 1f; // 첫 스폰은 항상 찍히도록 충분히 멀리 초기화
        for (int i = 0; i < burstCount; i++)
        {
            float bt = (float)i / (burstCount - 1);
            Vector3 pos = Vector3.Lerp(start, behind, bt);
            transform.position = pos;
            UpdateRushFacing(pos.x);
            if (Mathf.Abs(pos.x - lastSpawnX) < minSpawnGap) continue;
            lastSpawnX = pos.x;
            SpawnAfterImage(FastAfterImageLifetime(dodgeCounterAfterImageLifetime));
        }
        transform.position = start; // 실제 이동은 아래 Lerp 루프가 다시 처음부터 담당
        UpdateRushFacing(start.x);

        float t = 0f;
        while (t < dodgeCounterRushDuration)
        {
            t += Time.unscaledDeltaTime;
            Vector3 pos = Vector3.Lerp(start, behind, Mathf.Clamp01(t / dodgeCounterRushDuration));
            transform.position = pos;
            UpdateRushFacing(pos.x);
            yield return null;
        }
        transform.position = behind;

        // 적을 바라봄 (적이 바라보는 방향과 같은 쪽) — 위 루프가 이미 이 값에 도달해 있지만
        // 안전망으로 한 번 더 명시적으로 확정한다.
        if (sr != null) sr.flipX = (enemyFacing < 0f);
        Vector2 facingBack = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;

        if (anim != null) anim.SetTrigger("Attack1"); // 시각적 스윙만(isAttacking=false라 AttackHitFrame 판정은 무시됨)

        // 닷지 카운터는 항상 크리티컬 취급(사용자 스펙) — 배율은 기존 dodgeCounterDamageMultiplier(3배) 그대로 쓰고,
        // 연출만 크리티컬과 동일하게(Hit02 VFX + 금색 2배 "숫자!!!" 텍스트 + 쉐이크/히트스톱 2배) 맞춘다.
        int dmg = RampageDamage(Mathf.RoundToInt(attack1Damage * dodgeCounterDamageMultiplier));
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
        Time.timeScale = Mathf.Clamp01(dodgeCounterHitstopScale);
        // 카운터는 항상 크리티컬이므로 히트스톱도 2배(critHitstopMultiplier)
        yield return new WaitForSecondsRealtime(dodgeCounterHitstopDuration * critHitstopMultiplier);
        // 진입 시점 값(prev)이 아니라 "지금의 기본값"으로 복원한다 — 기다리는 사이 시간 가속이
        // 켜지거나 꺼졌을 수 있고, 그 경우 낡은 값을 되살리면 슬로우모션이 stuck된다.
        Time.timeScale = BaseTimeScale;
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

    // 폭주 진입 순간 스프라이트를 잠깐 글리치 프레임에 고정했다가 되돌린다(사용자 지시 2026-08-02).
    // 물리 이동은 멈추지 않는다 — FreezeAnimAt은 anim.enabled만 끄므로 rb는 그대로 움직인다.
    // 다른 프리즈(대시·일섬)와 달리 특정 게임 이벤트로 안 풀리고 정해진 실시간이 지나면 스스로 풀린다.
    void RampageGlitchFlicker()
    {
        FreezeAnimAt(rampageGlitchState, 0, 1);
        StartCoroutine(UnfreezeAnimAfter(rampageGlitchDuration));
    }

    System.Collections.IEnumerator UnfreezeAnimAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (anim == null) yield break;
        anim.enabled = true;
        // ⚠️ 버그 수정(사용자 리포트 2026-08-02): "Glitch Samurai-Idle Gltich"는 프리즈 전용 상태라
        // Animator Controller에 자체 전이가 하나도 없다 — enabled만 켜면 Speed가 올라가도 Run으로
        // 못 나가고 그 자리에 멈춘 채로 남는다(공격·착지처럼 트리거가 있는 동작만 우연히 탈출 가능했다).
        // 실제 Idle 상태로 되돌려 다음 프레임부터 정상적으로 Run/Jump 전이가 먹히게 한다.
        anim.Play("Glitch Samurai-Idle", 0, 0f);
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
        Time.timeScale = Mathf.Clamp01(hitstopScale);
        yield return new WaitForSecondsRealtime(hitstopDuration);
        Time.timeScale = BaseTimeScale; // prev가 아닌 이유는 DodgeCounterHitstopCo 주석 참고

    }

    // UniTrio-Game-2026(PlayerWeaponController.HandleAttackInput) 참고 재설계:
    // 스윙 도중엔 새 공격을 끼워넣지 않고(항상 끝까지 재생), 대신 입력을 attackInputBufferDuration만큼
    // 버퍼링해 공격이 끝나는 즉시 자동 발동시킨다. 콤보 타수는 attackStage 하나만 순환시켜 관리
    // (별도의 "마지막 타수 기억" 변수 없이 종료 시 다음 타수로 미리 넘겨둠).
    void HandleAttack()
    {
        if (attackQueued && Time.time - attackQueueTime > attackInputBufferDuration)
            attackQueued = false;

        if (jumpAttackCooldownCounter > 0f) jumpAttackCooldownCounter -= PDelta;
        if (jumpAttackHangTimer > 0f) jumpAttackHangTimer -= PDelta;

        // 지상은 기존 1타/2타 콤보 그대로. 공중은 별도의 Jump Attack 전용 애니메이션만 쓰고(사용자
        // 지시 2026-08-05), 착지 전까지 2타로 세던 이전 방식 대신 쿨타임(jumpAttackCooldown)으로
        // 스팸을 막는다. 공격 중엔 낙하하지 않게 HandleMovement/ApplyGravityScale에서 isAttacking일
        // 때 y속도·중력을 함께 묶어둔다(공중 여부 무관, 지상 공격도 원래 이렇게 동작했음).
        // isWallSliding은 계속 막는다(사용자 지시 2026-08-03) — OnAttack()에서 이미 입력 자체를
        // 막지만(attackQueued가 안 세워짐), 벽에 붙기 직전에 버퍼링된 입력이 남아있는 경우까지 이중으로 막는다.
        bool canAttack = !isAttacking && !isDashing && !isDodgeCountering && !ilseomActive && !isParrying && !isExecuting && !isSpendingLight && !isWallSliding && attackQueued;

        if (canAttack && isGrounded)
        {
            attackQueued = false;

            // 마지막 공격이 끝난 뒤 콤보 유효시간이 지났으면 1타로 리셋
            if (Time.time - lastAttackEndTime > comboBufferDuration)
                attackStage = 1;

            StartAttackStage(attackStage);
        }
        else if (canAttack && !isGrounded && jumpAttackCooldownCounter <= 0f)
        {
            attackQueued = false;
            StartJumpAttack();
        }

        if (!isAttacking) return;

        attackTimer += PDelta;
        // 공격속도 버프는 모션 길이를 그대로 나눈다 — 애니메이터 재생속도(UpdateAnimations)와 같은
        // 배율을 쓰므로 "빨라진 애니메이션"과 "빨라진 판정 종료"가 어긋나지 않는다.
        float duration = isJumpAttacking
            ? jumpAttackDuration / AttackSpeedMultiplier
            : (attackStage == 1 ? attack1Duration : attack2Duration) / AttackSpeedMultiplier;

        if (attackTimer >= duration)
        {
            lastAttackEndTime = Time.time;
            isAttacking = false;
            if (isJumpAttacking)
            {
                isJumpAttacking = false;
                jumpAttackCooldownCounter = jumpAttackCooldown;
            }
            else
            {
                attackStage = (attackStage % 2) + 1; // 다음 공격을 위해 미리 순환(1->2, 2->1)
            }
        }
    }

    // 공격 스윙 도중 패링/대시/점프 입력이 들어오면 스윙을 즉시 취소하고 그 동작으로 넘어간다(사용자
    // 지시 2026-08-05: "공격 도중에 애니메이션을 캔슬하고 패링/대시/점프 가능"). 정상 종료(HandleAttack의
    // duration 만료)와 같은 마무리를 하되, attackStage는 순환시키지 않는다 — 스윙을 끝까지 못 쳤으니
    // 콤보를 공짜로 다음 타로 넘겨주지 않는다(다음 공격 입력은 같은 타수를 다시 시도한다).
    void CancelAttack()
    {
        if (!isAttacking) return;
        isAttacking = false;
        isJumpAttacking = false;
        attackQueued = false; // 캔슬한 스윙 뒤에 버퍼링돼 있던 다음 공격까지 그대로 이어 나가면 안 된다
        lastAttackEndTime = Time.time;
        TestLog.Event("player_attack", "cancelled_by_action");
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

    void StartJumpAttack()
    {
        isAttacking = true;
        isJumpAttacking = true;
        attackTimer = 0f;
        attackHitDone = false;
        // 지난 스윙에서 남았을 수 있는 정지 잔여값을 지운다 — 판정 프레임(AttackHitFrame)에서만 세워야
        // 하는 값이라, 스윙 시작부터 켜져 있으면 안 된다. 현재 쿨타임(0.5s)이면 실제로 남을 일이 없지만
        // 쿨타임을 0에 가깝게 조정하면 재현 가능한 상태 누수라 여기서 명시적으로 초기화한다.
        jumpAttackHangTimer = 0f;
        if (anim != null) anim.SetTrigger("JumpAttack");

        float lungeDirX = (sr != null && sr.flipX) ? -1f : 1f;
        transform.position += new Vector3(lungeDirX * attackLungeDistance, 0f, 0f);

        TestLog.Event("player_attack", "jump_attack_start");
    }

    // Slash 1/2, Jump Attack 애니메이션 클립에 찍힌 Animation Event(사용자가 직접 표시한 판정
    // 프레임)에서 호출됨. 정규화시간 윈도우 폴링 대신 애니메이션이 그 프레임에 도달하는 정확한
    // 순간에 판정 — 프레임 스킵에도 안전.
    public void AttackHitFrame()
    {
        if (!isAttacking || attackHitDone) return;
        // 점프 공격은 이 프레임(=판정 프레임)에 도달한 순간부터 jumpAttackHangDuration만큼만 y를
        // 고정한다(AttackFreezesY 참고) — CheckAttackHit보다 먼저 세팅해야 그 안에서 도는 히트스톱
        // 코루틴·VFX와 같은 프레임에 이미 정지가 걸린다.
        if (isJumpAttacking) jumpAttackHangTimer = jumpAttackHangDuration;
        CheckAttackHit(isJumpAttacking ? jumpAttackDamage : (attackStage == 1 ? attack1Damage : attack2Damage));
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
        damage = RampageDamage(damage); // 폭주 중이면 그 위에 다시 배율(명세서 "데미지 증폭")

        Vector2 facing = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;
        GetAttackHitbox(attackStage, out Vector2 center, out Vector2 size, out float angle);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle, enemyLayer);
        // 폭주 중엔 같은 스윙이 적을 더 멀리 밀어낸다("타격감 증폭"의 넉백 몫)
        float knockback = facing.x * attackLungeDistance * enemyKnockbackMultiplier
            * (isRampaging ? rampageKnockbackMultiplier : 1f);
        int hitCount = 0;
        // 적/LightObject 타격만 자아를 회복시킨다 — 스위치는 hitCount에는 잡히지만(히트스톱·쉐이크는
        // 그대로 느껴지게) 자아 게이지는 안 채운다(2026-08-10 사용자 지시).
        bool restoreEgo = false;
        for (int i = 0; i < hits.Length; i++)
        {
            DummyEnemy enemy = hits[i].GetComponent<DummyEnemy>();
            if (enemy != null)
            {
                // 넉백: 플레이어가 바라보는 방향으로 attackLungeDistance × 배율(기본 1.5)만큼 밀어냄
                bool killed = enemy.TakeDamage(damage, knockback);
                hitCount++;
                restoreEgo = true;
                SpawnHitFeedback(hits[i].transform.position, facing, damage, crit ? HitTier.Critical : HitTier.Normal);

                // C-1: 적 타격 +3 / 처치 +10(합산) — 처형·회피-카운터는 각자 보상(+30 등)이 있어
                // 여기서 중복 지급하지 않는다(일반 공격 경로에서만 killed를 본다). 에너지는 즉시가 아니라
                // 포물선 픽셀이 도착할 때마다 AddEnergy가 나눠서 불린다(게이지가 또르르 차오르게).
                LightPixelFx.SpawnAbsorb(hits[i].transform.position, transform, 3 + (killed ? 10 : 0), AddEnergy, hits[i].bounds.extents.magnitude, lightPixelPivotOffset, CurrentPixelTint);
                continue;
            }

            LightObject lightObj = hits[i].GetComponent<LightObject>();
            if (lightObj != null && lightObj.TryHit())
            {
                hitCount++;
                restoreEgo = true;
                int chargeAmount = Mathf.RoundToInt(maxEnergy * lightObj.energyChargePercent);
                LightPixelFx.SpawnAbsorb(hits[i].transform.position, transform, chargeAmount, AddEnergy, hits[i].bounds.extents.magnitude, lightPixelPivotOffset, CurrentPixelTint);
            }
        }

        // 스위치는 별도 레이어(enemyLayer와 분리) — 문/스위치 기믹, 자아 게이지 회복 대상이 아니다.
        Collider2D[] switchHits = Physics2D.OverlapBoxAll(center, size, angle, switchLayer);
        for (int i = 0; i < switchHits.Length; i++)
        {
            DoorSwitch doorSwitch = switchHits[i].GetComponent<DoorSwitch>();
            if (doorSwitch != null)
            {
                doorSwitch.Toggle();
                hitCount++;
            }
        }

        if (hitCount > 0)
        {
            if (restoreEgo) RestoreEgo(); // 폭주 중 자아 회복 — 적중 1회당 1번(여러 적을 동시에 맞혀도 중첩 없음)
            // 점프 공격이 무언가를 맞히면 공중 점프 1회 재충전(사용자 지시 2026-08-05, 저글링 리셋).
            if (isJumpAttacking && jumpAttackBonusJumpEnabled) hasJumpAttackBonusJump = true;
            TestLog.Event("player_attack", $"stage={attackStage} dmg={damage} hits={hitCount} crit={crit} rampage={isRampaging}");
            float mul = (crit ? critShakeMultiplier : 1f) * (isRampaging ? rampageShakeMultiplier : 1f);
            float hitstopMul = (crit ? critHitstopMultiplier : 1f) * (isRampaging ? rampageHitstopMultiplier : 1f);
            if (attackHitstop) StartCoroutine(AttackHitstopCo(hitstopMul));
            if (attackScreenShake && sectionCamera != null) sectionCamera.Shake(attackShakeDuration, attackShakeMagnitude * mul);
        }
    }

    // 대시 히트스톱(DashHitstop)과 동일 패턴: 타격 확정 순간 짧게 시간정지.
    // durationMultiplier: 크리티컬/처형이면 2배(사용자 스펙 "히트 스톱도 2배").
    System.Collections.IEnumerator AttackHitstopCo(float durationMultiplier)
    {
        Time.timeScale = Mathf.Clamp01(attackHitstopScale);
        yield return new WaitForSecondsRealtime(attackHitstopDuration * durationMultiplier);
        Time.timeScale = BaseTimeScale; // prev가 아닌 이유는 DodgeCounterHitstopCo 주석 참고

    }

    // 적 공격에 맞았을 때 호출됨(예: DummyEnemy 창 찌르기). damage 단위는 체력 "칸" 수다.
    // 화면 표시는 PlayerHudUI가 이 값을 읽어 칸으로 그린다.
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        // 광원 소모(E 홀드) 중 피격 시 즉시 중단(스펙 6) — 재개하려면 E를 다시 눌러야 한다.
        if (isSpendingLight) EndLightSpend("hit");

        // 일섬 발동 중엔 아예 피격되지 않는다(스펙 7). DummyEnemy는 IsInvincible로 이미 걸러내지만,
        // 다른 피해 경로가 생겨도 새지 않도록 여기서도 막는다.
        if (ilseomActive)
        {
            TestLog.Event("ilseom", "damage_blocked");
            return;
        }

        // 차지 홀드 중엔 받는 피해가 절반(스펙 6). 1 미만으로 깎여 무피해가 되지 않도록 최소 1은 남긴다.
        // ⚠️ 체력이 갯수가 되면서 한 대 = 1칸이라 "절반"이 사실상 무효가 됐다(max(1, 0.5)=1).
        //    되살리려면 "차지 중 N번째 피격만 무효" 같은 칸 단위 규칙이 필요 — 밸런스 결정이라 미수정.
        if (isCharging)
        {
            int reduced = Mathf.Max(1, Mathf.RoundToInt(damage * ilseomChargeDamageTakenMultiplier));
            TestLog.Event("ilseom", $"charge_damage_reduced {damage}->{reduced}");
            damage = reduced;
        }

        // ⚠️ 예전엔 여기서 "폭주 중 피격 시 에너지 -20 → 0이 되면 폭주 종료"를 했는데, 폭주가
        //    "광원 0인 상태"로 바뀌면서 둘 다 성립하지 않는다 — 폭주 중엔 이미 0이라 깎을 것이 없고,
        //    0에서 종료시키는 건 새 규칙과 정면으로 충돌한다(맞으면 폭주가 풀려 버린다). 그래서 뺐다.
        //    `rampageHitEnergyLoss` 필드는 씬에 직렬화돼 있어 남겨 뒀다(삭제는 별도 승인).

        // 체력이 칸이 된 뒤로 음수가 되면 HUD가 그릴 칸이 없다(예전엔 -3 같은 값이 그대로 남았다).
        currentHealth = Mathf.Max(0, currentHealth - damage);
        TestLog.Event("player_damage", $"hp={currentHealth}/{maxHealth} dmg={damage}");

        // 피격 연출(쉐이크 + 붉은 점멸) — 공격 쉐이크(0.12s/0.15)보다 크게(맞은 쪽이 더 아파야 한다).
        if (sectionCamera != null) sectionCamera.Shake(0.18f, 0.22f);
        PlayerDamageFlashUI.Flash();
    }

    /// <summary>함정 등 외부에서 플레이어를 밀어낸다. lockDuration 동안 수평 입력을 잠가
    /// HandleMovement가 다음 FixedUpdate에 밀린 속도를 바로 덮어쓰는 걸 막는다
    /// (벽점프가 쓰는 수평잠금과 같은 장치라 카운터를 공유한다).</summary>
    public void ApplyKnockback(Vector2 velocity, float lockDuration)
    {
        // 폭주 = 슈퍼아머. 세계관의 "맷집이 극도로 상승"을 경직/넉백 면역으로 표현한다(피해량은 그대로).
        if (isRampaging)
        {
            TestLog.Event("rampage", "knockback_ignored");
            return;
        }

        rb.linearVelocity = velocity;
        if (lockDuration > wallJumpLockCounter) wallJumpLockCounter = lockDuration;
    }

    /// <summary>빛 에너지 증감(양수=충전, 음수=소모). 0~maxEnergy로 클램프한다.</summary>
    public void AddEnergy(int delta)
    {
        if (delta == 0 || maxEnergy <= 0) return;
        // 폭주 중엔 빛이 잘 안 모인다(25%만 회복, 사용자 확정 2026-08-02로 기존 50%에서 강화). 획득
        // 경로가 전부 이 한 곳을 지나가므로(타격·처치·패링·처형·픽업) 여기서 한 번만 깎으면 된다.
        // 최소 1은 보장한다 — 0이 되면 소량 획득으로는 영영 폭주에서 못 빠져나온다.
        if (delta > 0 && isRampaging) delta = Mathf.Max(1, Mathf.RoundToInt(delta * rampageEnergyGainMultiplier));
        currentEnergy = Mathf.Clamp(currentEnergy + delta, 0, maxEnergy);
        TestLog.Event("player_hud", $"energy={currentEnergy}/{maxEnergy} delta={delta}");
    }

    /// <summary>체력 회복(처형 성공 등). 단위는 "칸"이며 최대 칸 수를 넘지 않는다.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        TestLog.Event("player_hud", $"heal hp={currentHealth}/{maxHealth} amount={amount}");
    }

    void ApplyBetterJumpPhysics()
    {
        // 벽 슬라이드/대시 중엔 각자 y를 제어. 공격은 AttackFreezesY 구간(지상 콤보 전체, 점프 공격은
        // 판정 프레임 순간만)에서만 스킵 — 점프 공격의 나머지 구간은 이 함수가 평소처럼 fall/low-jump
        // 배율을 적용해 정상적으로 낙하하게 둔다.
        if (isWallSliding || isDashing || AttackFreezesY) return;
        // 가속도라서 ×mul²(rb.gravityScale과 같은 규칙, ApplyGravityScale 주석 참고) — 비활성 시엔 1.
        float gravityMul = TimeAccelMul * TimeAccelMul;
        if (rb.linearVelocity.y < 0) {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime * gravityMul;
        } else if (rb.linearVelocity.y > 0 && !isJumpHeld) {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime * gravityMul;
        }
    }

    void UpdateAnimations()
    {
        // 일섬 차지/발동 중엔 애니메이터를 직접 제어한다 — 여기서 파라미터를 갱신하면
        // AnyState 전이(Fall/Jump/Land/Wall Slide)가 Glitch Out/Sweep을 즉시 덮어써버린다.
        // flipX도 이 구간엔 HandleIlseom/PlayIlseomState가 관리한다.
        // isLedgeClimbing 추가(2026-08-04): 꼭대기로 보간 이동하는 0.12초 동안 접지 판정이 오락가락하면
        // Land 트리거가 계속 들어가 "착지 애니메이션이 지속적으로 재생"된다(사용자 리포트). 이 구간엔
        // 파라미터를 아예 안 건드리고, 끝난 뒤 실제 착지에서 한 번만 Land가 나가게 한다.
        // isDodgeCountering 추가(2026-08-05): HandleMovement는 이미 이 상태에서 속도를 0으로 묶지만,
        // 이 함수의 flipX 갱신(moveInput.x 기준)은 별개 경로라 안 막혀 있었다 — 실제로는 제자리에
        // 묶여 있는데 A/D를 누르면 스프라이트만 방향을 바꾸는 버그(사용자 리포트). CounterRush가
        // 끝에서 enemyFacing 기준으로 flipX를 다시 확정하므로, 그 사이엔 아예 안 건드리는 게 맞다.
        if (isCharging || ilseomActive || isExecuting || isSpendingLight || isLedgeClimbing || isDodgeCountering) return;

        if (anim != null) {
            // 공격속도 버프에 맞춰 공격 애니메이션도 빨라진다(사용자 지시). 대시 프리즈는 anim.enabled=false로
            // 처리하므로 여기서 speed를 건드려도 프리즈가 깨지지 않는다.
            // 이동(Run)·벽타기는 이동속도 버프(MoveSpeedMultiplier)를 따로 공유한다(사용자 지시 2026-08-03:
            // "이동속도가 빨라지면 애니메이션 속도도 빨라지게, 벽타기도 동일"). 벽타기 중엔 W/S를 누르는
            // 동안만 재생되고, 안 누르면 그 자리에서 멈춰야 한다 — Wall Slide 클립 재생 속도를 W/S 입력
            // 여부로 덮어쓴다.
            // 애니메이터는 스케일된 시간으로 도므로, 시간 가속 중엔 재생속도도 TimeAccelMul을 곱해야
            // 실시간 기준으로 평소와 같은 속도로 재생된다(정지 상태의 0은 곱해도 0이라 그대로 멈춘다).
            anim.speed = (isAttacking
                ? AttackSpeedMultiplier
                : isWallSliding
                    ? (Mathf.Abs(moveInput.y) > 0.01f ? MoveSpeedMultiplier : 0f)
                    : MoveSpeedMultiplier) * TimeAccelMul;
            anim.SetFloat("Speed", Mathf.Abs(moveInput.x));
            // AnyState 전이는 Fall="yVelocity < 0", Jump="yVelocity > 0"이라 정확히 0(또는 그 근방)인
            // 순간은 어느 쪽도 못 잡는다(공격 종료 직후, 중력이 다시 붙기 전 몇 프레임 — 점프 정점에서도
            // 발생) — 공중인데 마지막으로 걸려있던 전이가 그대로 유지돼 Idle에 멈춰있는 버그가 났다
            // (사용자 리포트 2026-08-05, "공격 후 Idle로 잠시 떠있음"). 그 좁은 사각지대(±0.05)만 Fall
            // 쪽으로 밀어준다 — 진짜 상승 중인 점프(강한 양수)는 그대로 Jump를 탄다.
            // ⚠️ 공격 애니메이션이 재생되는 동안엔 실제 물리 속도가 아니라 0을 먹인다 — 점프 공격이
            // 판정 프레임 순간만 y를 고정하도록 바뀌면서(AttackFreezesY, 2026-08-05) 나머지 구간은 실제로
            // 낙하해 rb.linearVelocity.y가 진짜 음수가 된다. 그 값을 그대로 먹이면 AnyState→Fall
            // (hasExitTime=false, 즉시 끼어듦)이 걸려 점프 공격 애니메이션이 중간에 낙하로 끊긴다
            // (사용자 리포트 "점프 공격 애니메이션이 끊김") — 예전 전체 구간 고정 시절엔 y가 항상 정확히
            // 0이라 우연히 안전했을 뿐이다. 실제 물리는 건드리지 않고 애니메이터 표시값만 가린다.
            // ⚠️ 단 **isAttacking 전체**를 가리면 안 된다: jumpAttackDuration(0.5833)이 실제 클립 길이
            // (0.3333, 키프레임 4개)보다 의도적으로 길게 잡혀 있어서(사용자 확인 2026-08-05 "일부러
            // 그렇게 뒀다"), 클립이 끝난 뒤 남는 약 0.25초 동안 Animator는 이미 Idle로 빠져나온 상태인데
            // yVelocity가 0으로 가려져 Fall로 못 넘어가 **공중에서 Idle 포즈로 떠 있는 것처럼** 보였다
            // (사용자 리포트 "공격 후에도 잠시 떠있음"의 남은 절반). 클립이 실제로 재생되는 구간만 가려서,
            // 그 뒤 회수 구간은 정상적으로 Fall이 나오게 한다. 클립 길이는 Awake에서 컨트롤러에서 읽으므로
            // 나중에 클립을 재편집하면 자동으로 따라간다.
            bool attackAnimPlaying = isAttacking
                && (!isJumpAttacking || attackTimer < jumpAttackAnimLength / AttackSpeedMultiplier);
            float animYVelocity = attackAnimPlaying ? 0f : rb.linearVelocity.y;
            // 사각지대 보정(±0.05)은 공격 애니메이션이 끝난 뒤에만 — 재생 중엔 위에서 이미 0으로 가렸다.
            if (!isGrounded && !isWallSliding && !attackAnimPlaying && Mathf.Abs(animYVelocity) < 0.05f) animYVelocity = -0.05f;
            anim.SetFloat("yVelocity", animYVelocity);
            anim.SetBool("isGrounded", isGrounded);
            anim.SetBool("isWallSliding", isWallSliding);
            // 벽타기 중 폭주/초월 여부 — Animator Controller의 AnyState 조건(아래 주석 참고)이
            // 직접 갈아탄다. isWallSliding이 계속 true인 동안 AnyState→Wall Slide 전이가 매 프레임
            // 재평가돼, 코드에서 anim.Play()로 강제로 다른 상태(Glitch Climb Glitch)로 밀어넣어도
            // 바로 다음 프레임에 그 전이가 도로 Wall Slide로 되돌려 매 프레임 두 상태를 오가며
            // 격렬하게 깜빡였다(사용자 스크린샷으로 확인 — 마스크 없는 쪽이 잠깐 보일 때마다
            // 흰색 폴백 마스크로 실루엣 전체가 확 빛나 보임). 파라미터 하나로 그래프 자체가 배타적으로
            // 갈아타게 해 전이끼리 서로 안 싸우게 했다.
            anim.SetBool("isWallClimbGlitch", isRampaging || isTranscending);

            // 공격 애니메이션 재생 중 가드(2026-08-05): 점프 공격이 판정 프레임 순간만 y를 고정하도록
            // 바뀌면서(AttackFreezesY) 스윙 도중 실제로 착지하는 경우가 생겼다 — Land 트리거가 그 순간
            // 끼어들면 JumpAttack 애니메이션이 중간에 끊긴다. 위 animYVelocity와 **같은 창**을 쓴다:
            // 클립이 끝난 뒤 회수 구간에 착지하면 Land가 정상적으로 나가야 한다(그때 Animator는 이미
            // Idle이라 끊을 것도 없다). wasGrounded는 어느 경우든 갱신해 뒤늦게 튀어나오지 않게 한다.
            if (isGrounded && !wasGrounded && !attackAnimPlaying) anim.SetTrigger("Land");
            wasGrounded = isGrounded;

            UpdateAlteredStateAnim();
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

    // 폭주·초월 중엔 Idle/Run이 각각 글리치 변형("Idle Gltich"/"Run Gltich")으로 재생된다(사용자
    // 지시 2026-08-02). Idle Gltich↔Run Gltich는 실제 Idle↔Run과 완전히 같은 Speed 임계값 전이를
    // 갖도록 Animator Controller에 추가해 뒀으므로(승인 받음), 한 번 갈아타면 그 뒤로는 그래프가
    // 스스로 둘 사이를 오간다 — 이 함수는 "지금 있어야 할 쪽에 있는지"만 매 프레임 확인해 어긋나면
    // (예: 착지 직후 Land가 실제 Idle로 돌려놓은 경우) normalizedTime을 보존한 채 다시 갈아탄다.
    // Jump/Fall/Land/공격 등 다른 상태는 건드리지 않는다 — Idle/Run 두 상태만 본다.
    void UpdateAlteredStateAnim()
    {
        if (anim == null || !isGrounded) return;

        bool altered = isRampaging || isTranscending;
        var cur = anim.GetCurrentAnimatorStateInfo(0);
        float nt = cur.normalizedTime % 1f;

        if (altered)
        {
            if (cur.IsName("Glitch Samurai-Idle")) anim.Play("Glitch Samurai-Idle Gltich", 0, nt);
            else if (cur.IsName("Glitch Samurai-Run")) anim.Play("Glitch Samurai-Run Gltich", 0, nt);
        }
        else
        {
            if (cur.IsName("Glitch Samurai-Idle Gltich")) anim.Play("Glitch Samurai-Idle", 0, nt);
            else if (cur.IsName("Glitch Samurai-Run Gltich")) anim.Play("Glitch Samurai-Run", 0, nt);
        }
    }

    // 벽타기 전용 애니메이션 등록(사용자 지시 2026-08-05). 기존엔 전용 스프라이트가 없어 Wall Slide
    // 클립을 그대로 재사용했는데, "Glitch Samurai-Wall Slide" 애니메이터 상태의 Motion 자체를 신규
    // "Glitch Samurai-Glitch Climb" 클립으로 교체해 뒀다(전이 그래프는 그대로 — AnyState→isWallSliding
    // 조건이 이 상태를 그대로 가리키므로 코드 변경 없이 평소엔 자동으로 새 클립이 나온다).
    // 폭주·초월 중엔 "Glitch Samurai-Glitch Climb Glitch" 상태로 바뀌어야 하는데, 처음엔 Idle/Run
    // Gltich와 같은 코드-직접-Play() 패턴을 썼다가 실제로 미친듯이 깜빡이는 버그가 났다(사용자
    // 스크린샷) — Idle/Run과 달리 Wall Slide는 AnyState(isWallSliding==true, 매 프레임 계속 참)가
    // 그 상태를 계속 다시 잡아당겨서, 코드가 Glitch Climb Glitch로 밀어넣어도 바로 다음 프레임에
    // Animator가 도로 Wall Slide로 되돌리며 서로 계속 싸웠다. 그래서 대신 파라미터
    // (`isWallClimbGlitch`, 위 UpdateAnimations에서 세팅)로 AnyState 전이 자체를 배타적으로 나눴다
    // — AnyState→Wall Slide는 `isWallClimbGlitch==false` 조건을 추가로 걸고, AnyState→Glitch Climb
    // Glitch를 새로 만들어 `isWallSliding && isWallClimbGlitch`로 잡게 했다(Animator Controller 직접
    // 편집, MCP). 발광 마스크(Assets/Sprites/Player/Mask/Glitch Samurai-Glitch Climb Glitch.png)는
    // PlayerBloomFx가 텍스처 이름으로 자동 매칭해 붙이므로 여기서 별도로 지정할 게 없다(기존 규칙).

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
            // 누른 즉시 발동(기존 동작). 조건이 안 맞으면 dashInputBuffer 동안 HandleDash가 계속
            // 재시도한다 — 시간 가속은 이제 Left Alt로 분리돼 이 키는 온전히 대시 전용이다.
            dashBufferTimer = dashInputBuffer;
            if (isCharging) cancelChargeRequested = true; // 일섬 차지 취소(대시는 그대로 발동됨)
        }
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
        {
            // 일섬 차지는 지상/공중 무관하게 좌클릭으로 취소된다(스펙 3).
            if (isCharging) cancelChargeRequested = true;

            // 공중 공격 허용(사용자 지시 2026-08-05) — 예전엔 지상에서만 받았으나, 이제 공중에서도
            // 입력이 버퍼에 쌓인다(HandleAttack의 낙하 정지 처리와 함께 봐야 함). 벽타기 중엔 계속 막는다.
            if (isWallSliding) return;

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

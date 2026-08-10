using UnityEngine;
using UnityEngine.Rendering.Universal;

// 보스의 붉은 눈이 플레이어 쪽을 작게(좌우+상하 모두) 응시한다(제한된 회전, 눈이 통째로 홱
// 돌아가는 대신 각속도 기반으로 서서히 — 순간 스냅 금지). 위치 이동은 플레이어를 쫓지 않고,
// 카메라(SectionCamera)가 실제로 움직인 만큼만 1:1로 같이 움직인다 — 화면 안에서 플레이어가
// 어디 있든 보스는 안 움직이고, 카메라가 구간을 넘어갈 때만 따라간다.
// 그 눈 위치에서 플레이어 쪽으로 Light2D(Point 타입, inner/outer angle로 부채꼴)를 겨눈다.
//
// 임포트된 메시의 피벗이 눈 중심과 다르기 때문에(피벗은 바닥, 눈은 로컬 (0, eyeLocalOffset.y, 0)
// 부근) 그냥 transform을 회전시키면 눈이 피벗을 중심으로 궤도를 그리며 같이 움직여버린다.
// 그래서 회전 직후 눈의 월드 위치가 회전 전과 같아지도록 위치를 되돌려 보정한다.
public class BossEyeTracker : MonoBehaviour
{
    [Header("Target")]
    public Transform player; // 비워두면 "Player" 태그로 자동 탐색

    [Header("Eye Pivot (로컬 좌표계 기준 눈 중심 근사값)")]
    public Vector3 eyeLocalOffset = new Vector3(0f, 0.5f, 0f);
    // 임포트된 메시의 "정면"이 어느 로컬 축인지 불확실해 노출해둠 — 눈이 플레이어 반대쪽을
    // 보면 이 값을 뒤집거나 다른 축으로 바꾼다.
    public Vector3 localForwardAxis = Vector3.forward;
    // 사용자 지시(2026-08-08): "약간 비스듬하게 쳐다보면 좋겠어" — 완전 정면이 아니라 기본
    // 자세(restRotation) 자체를 이만큼 미리 틀어둔다. maxYawDegrees의 좌우 응시는 이 틀어진
    // 자세를 기준으로 계산된다(즉 항상 이만큼은 비스듬한 채로 유지됨).
    public Vector3 restTiltEuler = new Vector3(0f, 15f, 0f);

    // 사용자 지시(2026-08-08): "회전과 이동이 최소화되어야해, 무조건" — 기본값을 눈에 띄게 작게 잡는다.
    [Header("Look (정면 자세 기준 좌우+상하 회전 제한 — 최소한으로)")]
    [Tooltip("정면 자세에서 허용하는 최대 회전각(좌우+상하 합산) — 작을수록 거의 안 돌아간다")]
    public float maxYawDegrees = 5f;
    [Tooltip("초당 회전 속도(도) — 순간적으로 돌지 않고 이 속도로 목표각을 따라간다")]
    public float rotationSpeedDegPerSec = 45f;

    // 사용자 지시(2026-08-08): "무조건 보스는 카메라 이동이 될 때만 따라가게" — 플레이어 위치가
    // 아니라 카메라가 실제로 움직인 만큼만(1:1 델타) 같이 움직인다. 플레이어가 화면 안에서 어디
    // 있든 보스는 안 움직이고, 카메라(SectionCamera)가 구간을 넘어가며 실제로 이동할 때만 따라간다.
    [Header("Follow (플레이어가 아니라 카메라가 움직일 때만 같이 이동)")]
    public Transform trackedCamera; // 비워두면 Camera.main 자동 탐색
    // trackedCamera가 SectionCamera라면 transform.position이 아니라 이 컴포넌트를 통해 basePos만
    // 읽는다(사용자 리포트 2026-08-11 "E 홀드시 튜토리얼 보스가 움직이는 버그") — transform.position은
    // 쉐이크·FocusPulse·SetSustainedFocus(E홀드 줌인 팬 등)의 오프셋까지 전부 합산된 값이라, 카메라가
    // 실제로 구간을 넘어가지 않았는데도 이런 연출 흔들림만으로 보스가 화면 안에서 밀려 보였다.
    SectionCamera trackedSectionCamera;
    bool cameraTrackingInitialized; // lastCameraPosition의 첫 캡처를 첫 LateUpdate까지 미루는 플래그
    // 사용자 지시(2026-08-08): "너무 보스가 빨리 따라오는 문제 수정" — 카메라 델타를 즉시
    // 100% 반영하지 않고, 목표 지점(desiredPosition)을 향해 이 속도로 서서히 뒤쫓아간다.
    [Tooltip("카메라를 뒤쫓아가는 속도 — 작을수록 더 느긋하게(뒤늦게) 따라온다")]
    public float followSmoothSpeed = 2f;
    // 사용자 지시(2026-08-08): "보스가 약간 플레이어 왼쪽에 배치되어있어야해" — 시작 시 플레이어
    // 기준 이만큼(월드 X, 음수=왼쪽) 떨어진 곳을 홈 포지션으로 삼는다.
    public float startOffsetFromPlayerX = -6f;
    Vector3 lastCameraPosition;
    Vector3 desiredPosition; // 카메라 델타가 누적되는 목표 지점 — transform.position은 이걸 서서히 뒤쫓는다

    [Header("Beam Light (Light2D, Point/Spot)")]
    public Color beamColor = new Color(1f, 0.02f, 0.01f);
    public float beamIntensity = 3f;
    public float beamInnerAngle = 14f;
    // 45 → 25(사용자 지시 2026-08-11 "걍 좁게 해주세요 지금보다") — 부채꼴 자체를 좁혀서 살짝만
    // 옆으로 비켜도 각도상 밖으로 빠지기 쉽게 한다. Y축 추적 로직(chaseSpeed의 magnitude 기준)은
    // 그대로 유지한 채 회피 난이도만 낮춘 상태(사용자 확정 2026-08-11).
    public float beamOuterAngle = 25f;
    // 2026-08-10: 25는 실제 보스룸(48x27) 기준 눈~최원거리 코너 실측 약 49유닛의 절반밖에 안 돼,
    // 플레이어가 방 반대쪽에 서 있으면(가만히 있어도) 빛이 아예 안 닿고 노출 판정(IsPlayerInBeam도
    // 이 값을 그대로 씀)도 끊겨서 한 번 55로 올렸었다. 이후(2026-08-11) "충분히 멀어지면 확실히
    // 안전"을 우선해 25로 다시 낮췄었으나, 그러면 맵 끝(방 구석)이 빔이 아예 안 닿는 영구 안전지대가
    // 되어 "맵 끝에 있으면 레이저를 안 맞는다"는 사용자 리포트로 이어졌다. ⚠️ 재수정(2026-08-11) —
    // 그 사이 exposureReacquireDelay(재포착 유예, 시간 기반)를 추가해 "이동하면 확실히 안전"이라는
    // 요구는 이미 거리와 무관하게 충족되므로, beamRange를 낮출 이유가 사라졌다. 방 전체를 커버하도록
    // 55로 되돌린다 — 회피는 이동(재포착 유예)으로, 도달 범위는 방 전체 커버로 역할을 분리한다.
    public float beamRange = 55f;
    // 180 → 35 → 20(사용자 지시 2026-08-11) — 부채꼴을 다 쓸어버리는 데 걸리는 시간을 늘려 방향을
    // 꺾어 회전을 앞지르는(부채꼴 밖으로 빠지는) 여유를 준다.
    // ⚠️ 사용자 지적(2026-08-11): "1자 평지 이동은 이 값을 아무리 낮춰도 결국 걸린다" — 정확하다.
    // 각속도 dθ/dt = v·h/(h²+x²)로, 일정 속도로 직선 이동하면 거리가 늘수록 각속도가 0에
    // 수렴하므로 고정된 추적 속도는 "충분히 오래 걷다 보면 반드시 다시 따라잡는다"는 구조적 한계가
    // 있다. 그래서 이 숫자 하나로는 못 풀고, 아래 exposureReacquireDelay(재포착 유예)를 같이 둔다.
    [Tooltip("빔이 플레이어를 순간적으로 스냅하지 않고 쫓아가는 느낌을 주는 회전 속도(도/초) — 너무 크면 사실상 항상 명중")]
    public float beamTrackSpeedDegPerSec = 20f;
    // 회전(beamTrackSpeedDegPerSec)엔 이미 속도 제한이 있었지만, 빔 길이(사거리)는 매 프레임
    // 플레이어와의 실제 거리에 그대로 스냅돼(Mathf.Min) 늘어나는/줄어드는 데 아무 제한이 없었다
    // — "직선으로 쫓아오는(커지는) 게 너무 빠르다"는 사용자 지시(2026-08-11)로, 이 길이 변화도
    // 회전과 같은 방식(초당 최대 변화량)으로 속도를 제한한다.
    [Tooltip("빔 길이(사거리)가 플레이어와의 실제 거리를 따라가는 속도(유닛/초) — 순간적으로 늘어나거나 줄지 않는다")]
    public float beamLengthChangeSpeed = 20f;
    float beamCurrentLength;          // 스무딩된 현재 빔 길이 — pointLightOuterRadius에 매 프레임 반영
    bool beamLengthInitialized;       // 첫 프레임엔 스무딩 없이 실제 거리로 바로 맞춘다(0에서 안 자라나게)
    // 사용자 지시(2026-08-10): "거리가 멀수록 붉은 빔이 잘 안 보인다" — Light2D Point의 반경 감쇠
    // (pointLightOuterRadius=beamRange에 가까워질수록 자연히 어두워짐) 때문에 사거리 끝에서는
    // "지금 빔에 비춰지고 있다"는 게 잘 안 느껴졌다. 플레이어와의 실제 거리(0=바로 옆, beamRange=사거리
    // 끝)에 따라 세기를 보간해서, 멀어질수록 오히려 더 쨍하게 밝혀 항상 눈에 띄게 한다.
    [Header("Beam Distance Compensation (멀수록 더 밝게 — 감쇠 보정)")]
    [Tooltip("플레이어가 눈 바로 옆(거리 0)에 있을 때의 세기 배율 — 1이면 beamIntensity 그대로")]
    public float beamNearIntensityMultiplier = 1f;
    [Tooltip("플레이어가 사거리(beamRange) 끝에 있을 때의 세기 배율 — 1보다 크면 멀어질수록 더 밝아짐")]
    public float beamFarIntensityMultiplier = 2.5f;
    // Light2D의 Point(=인스펙터상 Spot) 타입 콘이 실제로 어느 로컬 축을 향해 뻗는지 불확실해
    // 노출해둠 — 빔이 눈 방향과 90도 어긋나 보이면 이 값을 Vector3.right 등으로 바꾼다.
    public Vector3 beamAimLocalAxis = Vector3.up;
    // 보스 본체는 화면에 보이면 안 되는 동안(배경 뒤) PixelBoss 레이어를 쓰지만, 빛은 실제로
    // 플레이어·주변 오브젝트를 비춰야 한다는 지시(2026-08-08)에 따라 기본(Default) 레이어에 둔다 —
    // 그래야 메인 카메라가 이 빛의 영향을 받는 실제 화면을 그린다. 다른 레이어를 쓰고 싶으면 여기서 바꾼다.
    // ⚠️ Light2DCullResult는 camera.cullingMask & (1 << light.gameObject.layer)로 라이트를 컬링한다 —
    //    PixelBoss(16)에 두면 메인 카메라가 이 빛을 아예 안 그린다.
    public string beamLightLayerName = "Default";

    // 사용자 지시(2026-08-09): "보스의 빛이 타일과 모든 요소를 비추게" — 눈에서 나가는 좁은 빔만으로는
    // 부채꼴이 지나가는 자리밖에 밝아지지 않는다(씬에 Global Light 2D가 하나도 없어서 나머지는 전부
    // 검게 남는다). 그래서 눈을 중심으로 방 전체를 덮는 넓은 원형 광원을 하나 더 둬서 타일·지형·적·
    // 플레이어가 실제로 보스 빛을 받게 한다. 이쪽은 볼류메트릭을 끈다 — 빔의 부채꼴 연출은 그대로
    // 두고 "조명"만 담당(안 그러면 화면 전체가 붉은 안개로 덮인다).
    // 사용자 지시(2026-08-09): "레이저 빔이 플레이어를 추격하는 형태(플레이어보다 약간 느림)".
    // ⚠️ 재설계 2회(사용자 지시 2026-08-11) — ① 조준점(beamAimPoint)이 플레이어를 고정/가변
    // 속도로 뒤쫓는 위치기반 방식이었는데, 플레이어보다 느린 추격점이 MoveTowards로 매 프레임
    // "현재 위치"만 보고 다가가도 수학적으로 플레이어의 이동 경로를 따라가는 곡선(pursuit curve)을
    // 그린다 — "추격이 아니라 경로를 따라오는 것 같다"는 정확한 지적을 받음. ② 그래서 위치기반
    // 조준점을 완전히 없애고, 빔이 **항상 플레이어의 실제 현재 위치**를 직접 겨누게 하며, 지연은
    // 오직 빔의 회전 속도(beamTrackSpeedDegPerSec, 아래 회전 로직 참고)만으로 만든다 — 원래
    // "각속도만 쓰면 멀리서는 순식간에 붙고 가까이서는 못 따라잡는다"는 이유로 조준점 방식을
    // 도입했었지만, 부채꼴 폭(beamOuterAngle)이 이미 여유를 주고 있어 이번엔 그 단점을 감수하고
    // "경로를 따라오는" 느낌을 없애는 쪽을 사용자가 선택했다.

    // 사용자 지시(2026-08-10): "fan activ 뒤에 있으면 빔이 통과 못하고 응시도 적용 안 됨" — 이 레이어의
    // 콜라이더(FanActiv.cs가 자기 자신을 이 레이어로 강제함)가 눈과 플레이어 사이를 가로막으면
    // 노출 판정을 끈다. 시각적으로 빔 자체가 안 보이는 건 별개 경로(ShadowCaster2D + beamLight의
    // shadowsEnabled)로 처리되므로, 이 마스크와 그 오브젝트들의 레이어가 항상 일치해야 한다.
    [Header("Beam Occlusion (이 레이어가 눈-플레이어 사이를 가리면 노출 무효)")]
    public string beamOccluderLayerName = "BossBeamOccluder";
    int beamOccluderMask;

    // 사용자 지시(2026-08-09): 빔에 3초 이상 계속 노출되면 자아 고갈과 같은 화면 노이즈가 걸리고
    // 5초에 한 칸씩 체력이 깎인다. 노출이 끊기면 카운트도 노이즈도 즉시 0으로 돌아간다.
    // ⚠️ 첫 피해 시점은 자아 붕괴(egoDepletedDamageInterval)와 같은 규칙을 따른다 — 디버프가
    //    걸린 뒤 한 주기를 꽉 채워야 들어온다(=노출 3초 + 5초 = 8초째). 더 빡세게 하려면
    //    exposureDamageInterval을 줄이면 된다.
    [Header("Beam Exposure (빔에 계속 노출되면 붕괴)")]
    public float exposureToBreakSeconds = 3f;
    public float exposureDamageInterval = 5f;
    public int exposureDamage = 1;
    // 순수 프레임 단위 기하 판정(부채꼴 안/밖)만 쓰면, 추적 속도를 아무리 낮춰도 "1자로 충분히
    // 오래 걸으면 각속도가 0에 수렴해 결국 다시 걸린다"는 구조적 한계가 있다(사용자 지적
    // 2026-08-11, beamTrackSpeedDegPerSec 주석 참고). 그래서 노출이 한 번이라도 끊기면(부채꼴을
    // 실제로 벗어나면) 그 뒤 이 시간 동안은 순수 각도 계산과 무관하게 무조건 노출 아님으로 친다 —
    // "계속 움직인다"는 선택 자체가 확실히 보상받게 하는 시간 기반 유예. 유예가 끝났는데도 여전히
    // 부채꼴 안이면(제자리에 서 있었다면) 그때부터 다시 정상적으로 노출 판정이 시작된다.
    [Tooltip("노출이 한 번 끊기면 최소 이만큼(초)은 각도와 무관하게 다시 노출되지 않는다")]
    public float exposureReacquireDelay = 1.5f;
    float reacquireCooldown; // 노출이 방금 끊긴 뒤 남은 유예 시간(0이면 평소대로 판정)

    // 사용자 지시(2026-08-09): "보스로 인해 HP 깎일 때도 기존 피해 연출". 화면 쉐이크와 붉은 점멸은
    // PlayerController.TakeDamage 안에 있어 이미 타지만, 적 공격이 추가로 띄우는 **데미지 텍스트**는
    // 호출한 쪽(DummyEnemy.cs:342)이 직접 스폰하는 구조라 보스 빔에는 빠져 있었다. 같은 헬퍼·같은
    // 색으로 맞춘다.
    [Header("Damage Text (비워두면 씬의 적에게서 같은 프리팹을 빌려 쓴다)")]
    public GameObject damageTextPrefab;
    public Color damageTextColor = new Color(1f, 0.3f, 0.3f, 1f); // DummyEnemy.damageTextColor와 같은 값

    [Header("Ambient Glow (주변 타일·오브젝트를 실제로 비추는 넓은 원형 광원)")]
    public Color glowColor = new Color(1f, 0.16f, 0.1f);
    public float glowIntensity = 1.5f;
    [Tooltip("이 반경 안쪽은 감쇠 없이 최대 밝기")]
    public float glowInnerRadius = 6f;
    [Tooltip("보스 룸(가장 큰 것이 48x27)을 덮을 만큼 넉넉히")]
    public float glowOuterRadius = 45f;

    Quaternion restRotation;
    Light2D beamLight;
    Light2D glowLight;

    PlayerController playerController;
    Collider2D playerCollider;    // occluder 겹침 판정용(발밑 피벗 보정 — 몸통 중심을 쓴다)
    float exposureTimer;       // 연속 노출 시간(끊기면 0)
    float exposureDamageTimer; // 디버프가 걸린 뒤 도는 피해 주기
    bool exposureBroken;       // 3초를 넘겨 노이즈·피해가 켜진 상태

    // [ASSERT] 판독구 — 플레이 테스트에서 로그로 확인한다
    public bool IsPlayerExposed { get; private set; }
    public float ExposureSeconds => exposureTimer;
    public bool ExposureBroken => exposureBroken;

    void Awake()
    {
        beamOccluderMask = LayerMask.GetMask(beamOccluderLayerName);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        // 월드 (0,0,0)을 기준으로 써봤으나, 임포트된 메시 자체가 이상한 각도를 "정면"으로 삼고
        // 있어서(0,0,0이 실제 정면이 아님) 렌즈가 위로 튀고 몸통이 잘려 보였다 — 그래서 시작
        // 시점의 실제 자세(=지금 인스펙터에 보이는 "정면"으로 맞춰둔 자세)를 기준점으로 삼되,
        // restTiltEuler만큼 미리 비스듬히 틀어서 완전 정면이 아니게 한다.
        restRotation = transform.rotation * Quaternion.Euler(restTiltEuler);

        if (player != null) transform.position += new Vector3(startOffsetFromPlayerX, 0f, 0f);

        if (trackedCamera == null)
        {
            Camera cam = Camera.main;
            if (cam != null) trackedCamera = cam.transform;
        }
        if (trackedCamera != null)
        {
            trackedSectionCamera = trackedCamera.GetComponent<SectionCamera>();
            // ⚠️ 여기서 바로 TrackedCameraPosition()을 읽으면 안 된다(사용자 리포트 2026-08-11
            // "보스가 겁나 위에 있음") — SectionCamera.BasePosition은 SectionCamera 자신의 Awake()가
            // 돌아야 실제 카메라 위치로 채워지는데, Unity는 서로 다른 오브젝트의 Awake() 순서를
            // 보장하지 않는다. 이 스크립트의 Awake()가 먼저 실행되면 아직 초기화 안 된 basePos(0,0,0)를
            // 읽고, 다음 프레임에 SectionCamera가 실제 위치(예: y≈36)로 초기화되면 그 차이 전체가
            // "카메라가 한 번에 움직인 델타"로 보스에 그대로 더해져 훅 튀어 오른다. 첫 LateUpdate까지
            // 초기화를 미룬다(LateUpdate는 씬의 모든 Awake가 끝난 뒤에만 도므로 항상 안전하다).
        }
        desiredPosition = transform.position;

        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }

        BuildBeamLight();
    }

    /// <summary>trackedCamera가 SectionCamera면 흔들림·포커스 오프셋이 안 섞인 basePos만,
    /// 아니면(다른 카메라·씬) 기존처럼 transform.position을 그대로 쓴다.</summary>
    Vector3 TrackedCameraPosition() =>
        trackedSectionCamera != null ? trackedSectionCamera.BasePosition : trackedCamera.position;

    void BuildBeamLight()
    {
        GameObject lightGO = new GameObject("BossEyeBeamLight");
        int layer = LayerMask.NameToLayer(beamLightLayerName);
        lightGO.layer = layer >= 0 ? layer : 0;
        beamLight = lightGO.AddComponent<Light2D>();
        beamLight.lightType = Light2D.LightType.Point;
        beamLight.color = beamColor;
        beamLight.intensity = beamIntensity;
        beamLight.pointLightInnerAngle = beamInnerAngle;
        beamLight.pointLightOuterAngle = beamOuterAngle;
        beamLight.pointLightInnerRadius = 0f;
        beamLight.pointLightOuterRadius = beamRange;
        beamLight.volumeIntensity = 1f;
        // 기본값이 false라 이게 없으면 라이트 자체가 화면에 전혀 안 보인다(볼류메트릭 시각화 스위치).
        beamLight.volumetricEnabled = true;
        // fan activ 같은 ShadowCaster2D 오브젝트가 실제로 빔을 가리게 한다(사용자 지시 2026-08-10).
        // shadowIntensity=1이면 그림자 영역을 완전히 차단(부분 투과 없음).
        beamLight.shadowsEnabled = true;
        beamLight.shadowIntensity = 1f;
        // ⚠️ shadowsEnabled는 라이트의 "표면 밝기" 기여만 가린다 — 실제 눈에 보이는 붉은 빔은
        // volumetricEnabled(안개형 렌더링)로 그려지는데 이쪽은 별도 스위치(volumetricShadowsEnabled)가
        // 있어야 그림자를 반영한다. 이걸 빠뜨리면 표면 조명은 막히는데 안개 빔은 그대로 뚫고 지나가
        // "시각적으로 여전히 통과한다"로 보인다(사용자 리포트 2026-08-10 스크린샷으로 확인).
        beamLight.volumetricShadowsEnabled = true;
        beamLight.shadowVolumeIntensity = 1f;
        ApplyToAllSortingLayers(beamLight);

        // 넓은 원형 조명 — 각도 360이면 부채꼴이 아니라 완전한 원이 된다.
        GameObject glowGO = new GameObject("BossEyeGlowLight");
        glowGO.layer = lightGO.layer;
        glowLight = glowGO.AddComponent<Light2D>();
        glowLight.lightType = Light2D.LightType.Point;
        glowLight.color = glowColor;
        glowLight.intensity = glowIntensity;
        glowLight.pointLightInnerAngle = 360f;
        glowLight.pointLightOuterAngle = 360f;
        glowLight.pointLightInnerRadius = glowInnerRadius;
        glowLight.pointLightOuterRadius = glowOuterRadius;
        glowLight.volumetricEnabled = false; // 조명만 — 안개(볼류메트릭)는 빔 쪽에만 남긴다
        // 사용자 리포트(2026-08-10): 빔만 가려도 방 전체를 채우는 이 원형 조명이 그대로 새어 들어와
        // fan 뒤가 여전히 붉게 보이고 "빛이 안 막힌 것"처럼 느껴졌다 — 같은 차폐물이 빔뿐 아니라
        // 보스의 빛 전체를 막아야 눈에 띄게 어두워진다. volumetricShadows는 이 라이트가
        // volumetricEnabled=false라 필요 없다.
        glowLight.shadowsEnabled = true;
        glowLight.shadowIntensity = 1f;
        ApplyToAllSortingLayers(glowLight);
    }

    // Light2D는 "타깃 소팅 레이어"에 속한 렌더러만 비춘다. AddComponent로 만들면 Awake가 전체
    // 레이어로 기본값을 채워 주지만(URP 17.3 Light2D.Awake), 나중에 소팅 레이어가 추가돼도
    // "모든 요소를 비춘다"는 요구가 깨지지 않도록 매번 현재 전체 레이어로 명시한다.
    static void ApplyToAllSortingLayers(Light2D light)
    {
        var layers = SortingLayer.layers;
        var ids = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
        light.targetSortingLayers = ids;
    }

    void LateUpdate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else return;
        }

        // ── 카메라가 움직인 만큼 목표 지점(desiredPosition)을 갱신하고, 실제 위치는 그걸 서서히
        // 뒤쫓는다(즉시 100% 반영 X) — 위/아래 방향에 편향 없이 대칭적으로 동작한다.
        if (trackedCamera != null)
        {
            // 첫 LateUpdate에서만 lastCameraPosition을 캡처한다(Awake 실행 순서 경합 회피 —
            // 위 Awake()의 주석 참고). LateUpdate는 씬의 모든 Awake가 끝난 뒤에만 돌므로 이 시점엔
            // SectionCamera.BasePosition이 항상 진짜 값으로 채워져 있다.
            if (!cameraTrackingInitialized)
            {
                lastCameraPosition = TrackedCameraPosition();
                cameraTrackingInitialized = true;
            }

            Vector3 currentTrackedPos = TrackedCameraPosition();
            Vector3 cameraDelta = currentTrackedPos - lastCameraPosition;
            cameraDelta.z = 0f;
            desiredPosition += cameraDelta;
            lastCameraPosition = currentTrackedPos;

            float followT = 1f - Mathf.Exp(-followSmoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followT);
        }

        // ── 제한된 회전(좌우뿐 아니라 위아래도 함께 — "눈으로 응시"하는 느낌) ──
        // 정면(restRotation)에서 플레이어 방향까지의 회전을 구하고, 그 전체 각도를 maxYawDegrees로
        // 캡(Quaternion.RotateTowards가 각도 기준으로 잘라줌) → 좌우뿐 아니라 위아래 성분도 함께
        // 작게 반영된다. 그 목표 자세를 향해 매 프레임 rotationSpeedDegPerSec만큼만 서서히 다가간다.
        Vector3 eyeWorldBefore = transform.TransformPoint(eyeLocalOffset);
        Vector3 toPlayer = player.position - eyeWorldBefore;
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            Vector3 restForward = restRotation * localForwardAxis.normalized;
            Quaternion fullLookRotation = Quaternion.FromToRotation(restForward, toPlayer.normalized) * restRotation;
            Quaternion clampedTarget = Quaternion.RotateTowards(restRotation, fullLookRotation, maxYawDegrees);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, clampedTarget, rotationSpeedDegPerSec * Time.deltaTime);

            // 회전 때문에 밀린 눈의 월드 위치를 원래 자리로 되돌린다(피벗 보정).
            Vector3 eyeWorldAfter = transform.TransformPoint(eyeLocalOffset);
            transform.position += (eyeWorldBefore - eyeWorldAfter);
        }

        // ── 빔 라이트를 눈 위치에 두고 플레이어를 정확히 겨눈다(부채꼴 자체는 눈 회전 제한과 무관) ──
        // Z(깊이) 성분은 버리고 화면 평면(XY)에 눕혀서 조준한다 — 2D 게임이라 눈과 플레이어의 Z가
        // 다르면(원근 없는 직교 카메라라도) 빛의 부채꼴이 화면 밖으로 기울어져 투영된 크기가 계속
        // 달라 보이는 문제가 있었다(사용자 리포트: "빛이 자꾸 크기가 바뀜").
        Vector3 eyeWorldNow = transform.TransformPoint(eyeLocalOffset);
        // ⚠️ 라이트는 반드시 z=0 평면에 둔다(2026-08-10 실측으로 잡은 버그). 보스 메시가 z≈18에
        // 있어 눈 위치를 그대로 쓰면 라이트도 z≈18로 가는데, URP 2D 그림자는 광원과
        // ShadowCaster2D(전부 z=0)가 같은 평면에 있어야 투영된다 — z가 어긋나면 그림자가 아예
        // 생성되지 않아 "차폐물을 빛이 그대로 통과"한다(셰이더·레이어를 다 맞춰도 안 됐던 진짜 원인).
        Vector3 lightPos = new Vector3(eyeWorldNow.x, eyeWorldNow.y, 0f);
        beamLight.transform.position = lightPos;

        // 넓은 조명은 눈 위치만 따라가면 된다(방향 없음). 플레이 모드에서 인스펙터로 세기·반경을
        // 바로 굴려볼 수 있도록 값도 매 프레임 반영한다.
        if (glowLight != null)
        {
            glowLight.transform.position = lightPos;
            glowLight.color = glowColor;
            glowLight.intensity = glowIntensity;
            glowLight.pointLightInnerRadius = glowInnerRadius;
            glowLight.pointLightOuterRadius = glowOuterRadius;
        }

        // ── 빔은 항상 플레이어의 "실제 현재 위치"를 직접 겨눈다(사용자 지시 2026-08-11) — 위치
        // 기반 조준점(beamAimPoint)을 없앴다. 지연은 오직 아래 회전 속도(beamTrackSpeedDegPerSec)
        // 하나로만 만든다 — 조준점을 플레이어 속도로 뒤쫓는 이전 방식은 플레이어보다 느리기만 해도
        // 수학적으로 플레이어의 이동 경로를 따라가는 곡선을 그려 "추격이 아니라 경로를 따라오는
        // 것 같다"는 문제가 있었다.
        Vector3 playerFlat = new Vector3(player.position.x, player.position.y, eyeWorldNow.z);

        // 실제 플레이어와의 거리로 감쇠를 보정한다.
        float distToPlayer = Vector3.Distance(eyeWorldNow, playerFlat);
        float distT = Mathf.Clamp01(distToPlayer / Mathf.Max(0.01f, beamRange));
        beamLight.intensity = beamIntensity * Mathf.Lerp(beamNearIntensityMultiplier, beamFarIntensityMultiplier, distT);
        // 사용자 지시(2026-08-10): "빔의 크기는 딱 플레이어와의 거리로" — 사거리(beamRange)를 항상
        // 꽉 채우는 대신, 플레이어와의 실제 거리만큼만 뻗는다(플레이어가 가까우면 짧게, 멀면 길게).
        // beamRange는 그 위에 거는 최대 한도로만 남긴다(위 세기 보정의 정규화 기준값과도 공유).
        // ⚠️ 재수정(2026-08-11, 사용자 지시 "직선으로 쫓아오는(커지는) 것도 속도를 줄여줘요") — 예전엔
        // 목표 길이로 매 프레임 순간 스냅(Mathf.Min)해서, 회전(beamTrackSpeedDegPerSec)엔 있던 속도
        // 제한이 길이 쪽엔 전혀 없었다. beamLengthChangeSpeed로 초당 변화량을 제한한다.
        float targetLength = Mathf.Min(distToPlayer, beamRange);
        if (!beamLengthInitialized) { beamCurrentLength = targetLength; beamLengthInitialized = true; }
        beamCurrentLength = Mathf.MoveTowards(beamCurrentLength, targetLength, beamLengthChangeSpeed * Time.deltaTime);
        beamLight.pointLightOuterRadius = beamCurrentLength;

        Vector3 aimDir = playerFlat - eyeWorldNow;
        aimDir.z = 0f;
        if (aimDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetAim = Quaternion.FromToRotation(beamAimLocalAxis.normalized, aimDir.normalized);
            beamLight.transform.rotation = Quaternion.RotateTowards(beamLight.transform.rotation, targetAim, beamTrackSpeedDegPerSec * Time.deltaTime);
        }

        UpdateExposure(eyeWorldNow);
    }

    /// <summary>플레이어가 빔 부채꼴 안에 있는지 — 사거리(beamRange)와 외곽각(beamOuterAngle, 전체각)으로
    /// 1차 판정한 뒤, 플레이어가 occluder(fan activ 등) **안에 들어가 있으면** 최종적으로 false로 뒤집는다.
    /// ⚠️ 시선 차단(눈→플레이어 라인캐스트)이 아니라 **겹침** 판정이다(사용자 지시 2026-08-10):
    /// "오브젝트 안에 들어가 있을 때만 안전, 밖에 있으면 노출". 라인캐스트로 하면 보스 눈이 높이
    /// 있어 차폐물의 그림자가 아래로 길게 깔리고, 플레이어가 오브젝트 옆(밖)에 서 있어도 그 그림자
    /// 선에 걸려 계속 안전해지는 문제가 있었다. 시각적 그림자와 판정이 일부러 다르다.</summary>
    bool IsPlayerInBeam(Vector3 eyeWorld)
    {
        Vector3 toPlayer = player.position - eyeWorld;
        toPlayer.z = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.0001f) return true;
        if (dist > beamRange) return false;

        Vector3 beamDir = beamLight.transform.rotation * beamAimLocalAxis.normalized;
        beamDir.z = 0f;
        if (beamDir.sqrMagnitude < 0.0001f) return false;

        // Light2D의 pointLightOuterAngle은 부채꼴 "전체" 각이라 반각과 비교한다.
        if (Vector3.Angle(beamDir, toPlayer) > beamOuterAngle * 0.5f) return false;

        if (IsPlayerInsideOccluder()) return false;

        return true;
    }

    /// <summary>플레이어 몸통 중심이 occluder 콜라이더 안에 들어가 있는지. 피벗이 발밑이라
    /// transform.position을 그대로 쓰면 발끝만 걸쳐도 숨은 것으로 쳐지므로 콜라이더 중심을 쓴다.</summary>
    bool IsPlayerInsideOccluder()
    {
        if (beamOccluderMask == 0) return false;

        if (playerCollider == null) playerCollider = player.GetComponentInChildren<Collider2D>();
        Vector2 probe = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;

        return Physics2D.OverlapPoint(probe, beamOccluderMask) != null;
    }

    // 3초 이상 연속 노출 → 자아 고갈과 같은 화면 노이즈 + 5초마다 체력 한 칸(사용자 지시 2026-08-09).
    // 노출이 한 프레임이라도 끊기면 카운트·노이즈·피해 주기가 전부 0으로 돌아간다.
    void UpdateExposure(Vector3 eyeWorld)
    {
        bool rawExposed = IsPlayerInBeam(eyeWorld);

        // 재포착 유예 중엔(reacquireCooldown>0) 실제로 부채꼴 안에 들어와 있어도 노출로 치지 않는다.
        bool exposed = rawExposed && reacquireCooldown <= 0f;

        // 노출이 "방금" 끊긴 순간(직전 프레임엔 노출 중이었는데 지금은 아님)에만 유예를 새로 건다 —
        // 유예 중에 rawExposed가 오락가락해도 매번 재설정되어 무한히 늘어지지 않게 한다.
        if (!exposed && IsPlayerExposed) reacquireCooldown = exposureReacquireDelay;
        if (reacquireCooldown > 0f) reacquireCooldown -= Time.deltaTime;

        IsPlayerExposed = exposed;

        if (!IsPlayerExposed)
        {
            ClearExposure();
            return;
        }

        exposureTimer += Time.deltaTime;

        if (!exposureBroken)
        {
            if (exposureTimer < exposureToBreakSeconds) return;
            exposureBroken = true;
            exposureDamageTimer = 0f; // 첫 피해는 한 주기를 꽉 채운 뒤(자아 붕괴와 같은 규칙)
            ScreenGlitchFx.Begin(ScreenGlitchFx.Source.BossBeam);
            TestLog.Event("boss_beam", $"exposure_break after={exposureTimer:F2}s");
        }

        exposureDamageTimer += Time.deltaTime;
        if (exposureDamageTimer < exposureDamageInterval) return;

        exposureDamageTimer -= exposureDamageInterval;
        if (playerController == null) playerController = player.GetComponent<PlayerController>();
        if (playerController == null) return;

        // 쉐이크 · 화면 붉은 점멸은 TakeDamage 안에서 처리된다. 여기선 적 공격과 같은 데미지 텍스트만 더한다.
        playerController.TakeDamage(exposureDamage);
        CombatFx.SpawnDamageText(ResolveDamageTextPrefab(), player.position, exposureDamage, damageTextColor);
        TestLog.Event("boss_beam", $"exposure_damage -{exposureDamage} hp={playerController.currentHealth}/{playerController.maxHealth}");
    }

    /// <summary>데미지 텍스트 프리팹을 인스펙터에서 안 넣어도 동작하게 한다 — 씬의 아무 적에게서
    /// 같은 프리팹을 한 번 빌려 캐시한다. 보스는 씬 오브젝트라 참조를 새로 꽂으려면 씬을 저장해야
    /// 하는데, 그러지 않고도 적과 완전히 같은 연출이 나오게 하기 위한 선택이다.</summary>
    GameObject ResolveDamageTextPrefab()
    {
        if (damageTextPrefab != null) return damageTextPrefab;

        var enemies = FindObjectsByType<DummyEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i].damageTextPrefab == null) continue;
            damageTextPrefab = enemies[i].damageTextPrefab;
            break;
        }
        return damageTextPrefab; // 끝내 못 찾으면 null — CombatFx가 조용히 무시한다
    }

    void ClearExposure()
    {
        exposureTimer = 0f;
        exposureDamageTimer = 0f;
        if (!exposureBroken) return;
        exposureBroken = false;
        ScreenGlitchFx.End(ScreenGlitchFx.Source.BossBeam);
        TestLog.Event("boss_beam", "exposure_clear");
    }

    // 보스가 사라져도 노이즈가 화면에 남지 않게 한다(씬 전환 · 오브젝트 파괴).
    void OnDisable()
    {
        ClearExposure();
    }
}

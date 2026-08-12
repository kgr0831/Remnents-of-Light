using System.Collections;
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

    // ⚠️ 추적 방식 전면 교체(2026-08-13, 사용자 지시 "카메라 이동시에 따라 말고 상시로 따라오게,
    //    일정 거리 이상 이동시마다"). 이전 방식은 카메라(SectionCamera.BasePosition)가 움직인 델타를
    //    1:1로 누적해 따라가는 것이었다(2026-08-08 지시 "무조건 카메라 이동이 될 때만"). 그래서 룸
    //    안에서는 카메라가 고정이라 보스가 아예 안 움직였고, 구간을 넘어갈 때만 한꺼번에 밀려왔다 —
    //    "덩치에 비해 갑자기 빠르게 따라온다"는 인상의 근본 원인이기도 하다.
    //
    //    지금은 플레이어를 **상시** 목표로 삼되, 매 프레임 야금야금 따라가지 않는다. 목표 지점이
    //    stepTriggerDistance만큼 벌어질 때까지 보스는 제자리에 버티고, 한계를 넘는 순간 목표를 한 번에
    //    갱신해 크게 한 걸음 내딛는다(+그 순간 카메라를 쿵 친다). "가만히 버틴다 → 훅 다가온다"의
    //    반복이라, 매끄럽게 따라붙는 것보다 질량이 훨씬 잘 느껴진다.
    [Header("Follow (플레이어를 상시 추적 — 일정 거리 벌어질 때마다 한 걸음)")]
    public Transform trackedCamera; // 쉐이크를 걸 SectionCamera + 화면 경계를 읽을 Camera. 비워두면 Camera.main
    SectionCamera trackedSectionCamera;
    Camera trackedCameraComponent;  // 화면 밖 재배치가 뷰 크기(orthographicSize·aspect)를 읽는다
    [Tooltip("목표 지점이 이만큼(유닛) 벌어지면 따라붙기 시작한다 — 그 전까지 보스는 완전히 정지")]
    public float stepTriggerDistance = 8f;
    // ⚠️ 재설계(2026-08-13, 사용자 피드백 "덩치에 비해 갑자기 + 빠르게 따라와서 중압감이 덜하다"):
    //    예전엔 지수 보간(Lerp, t = 1-exp(-k·dt))이라 **속도가 오차에 그대로 비례**했다 — 오차가
    //    생기는 순간 보스가 정지 상태에서 곧바로 최고 속도로 튀어나갔다(가속 구간이 아예 없음 =
    //    "갑자기"). SmoothDamp는 속도를 프레임 간 상태로 들고 있어 정지→가속→감속이 생기고(=관성),
    //    maxSpeed로 최고 속도에 천장을 씌워 "빠르게"도 같이 잡는다.
    // 사용자 지시(2026-08-13): "플레이어 이동속도보다 살짝 느리게" — followMaxSpeed를 플레이어
    // 이동속도 바로 아래로 잡는다. 달리면 조금씩 벌어지지만 압도적으로 뒤처지진 않는다.
    // ⚠️ 기준값은 PlayerController.moveSpeed의 **스크립트 기본값(5)이 아니라 씬 값**이다 — Map-test는
    //    6으로 덮어써져 있다(실측 2026-08-13). 5 기준으로 잡으면 "살짝"이 아니라 75%가 된다.
    // ⚠️ followSmoothTime을 같이 줄여야 이 값이 실제로 먹는다. SmoothDamp의 최고 속도는 대략
    //    거리/smoothTime × 0.46이라, 8유닛 걸음에 smoothTime이 2.4면 1.5유닛/초밖에 안 나와서
    //    maxSpeed를 아무리 올려도 천장에 닿질 않는다(예전 값이 그랬다). 0.8이면 약 4.6이 나와
    //    maxSpeed가 비로소 실제 상한으로 작동한다.
    [Tooltip("한 번 따라붙는 데 걸리는 대략적인 시간(초) — 너무 크면 maxSpeed에 아예 안 닿는다")]
    public float followSmoothTime = 0.8f;
    [Tooltip("최고 속도(유닛/초) — 플레이어 이동속도(Map-test 기준 6)보다 살짝 낮게")]
    public float followMaxSpeed = 5.5f;
    Vector3 followVelocity; // SmoothDamp가 프레임 간에 들고 가는 속도 — 관성의 실체
    // 사용자 지시(2026-08-08): "보스가 약간 플레이어 왼쪽에 배치되어있어야해" — 시작 시 플레이어
    // 기준 이만큼(월드 X, 음수=왼쪽) 떨어진 곳을 홈 포지션으로 삼는다.
    // ⚠️ 버그 수정(2026-08-13, 사용자 지시 "플레이어 앞쪽(왼쪽)에 위치하게. 애초에 등장부터 그렇게"):
    //    예전 코드는 Awake에서 `transform.position += (이 값, 0, 0)`이었다 — 즉 **씬에 찍어둔 위치를
    //    왼쪽으로 미는** 것이지 플레이어 기준이 아니었다. Map-test 실측으로 보스 x=-0.86, 플레이어
    //    x=-8.70이라 -6을 더해도 x=-6.86 → 플레이어보다 1.84유닛 **오른쪽**에 서 있었다. 이제 X를
    //    플레이어 기준으로 직접 계산한다(Y·Z는 씬에 잡아둔 구도를 그대로 존중한다).
    public float startOffsetFromPlayerX = -6f;
    // 사용자 지시(2026-08-13): "상시로 이동하지마" — 걸음은 완전한 **이산 동작**이다. 한 번 떼면
    // 그 순간의 목표를 붙들고(걷는 도중엔 플레이어를 다시 조준하지 않는다) 거기 도착하면 딱 멈춘다.
    // 도착 후 stepRestSeconds 동안은 아무리 멀어져도 안 움직인다 — 이 정지 구간이 없으면 플레이어가
    // 달리는 내내 다음 걸음이 곧바로 이어져 결국 "상시 이동"으로 되돌아간다.
    [Tooltip("한 걸음이 끝난 뒤 최소 이만큼(초)은 완전히 멈춰 있는다")]
    public float stepRestSeconds = 1.2f;
    Vector3 homeOffset;       // 첫 프레임에 잡은 플레이어→보스 상대 위치. 이걸 유지하며 X·Y 모두 따라간다
    bool followInitialized;   // homeOffset의 첫 캡처를 첫 LateUpdate까지 미루는 플래그
    Vector3 desiredPosition;  // 걸음을 뗄 때 한 번 정해지고, 그 걸음이 끝날 때까지 고정되는 목표 지점
    bool stepping;            // 걷는 중인가 — false면 SmoothDamp를 아예 안 돌려 완전히 정지한다
    float stepRestTimer;      // 도착 후 남은 강제 정지 시간
    const float StepArriveEpsilon = 0.15f; // 이 안에 들어오면 도착으로 치고 스냅(SmoothDamp는 점근이라 영영 안 닿는다)

    // 사용자 지시(2026-08-13): "카메라 밖에 존재할 때, 카메라 밖 위치 중 가장 가까운 위치로 순간이동
    // (단, 몸의 아주 일부분이라도 순간이동 시 카메라에 보이면 안됨)".
    // 보스는 플레이어보다 느리고 걸음 사이에 정지 구간까지 있어 한 방향으로 계속 달리면 구조적으로
    // 무한히 뒤처진다. 그래서 **화면 밖에 있는 동안에만** 화면 밖 최근접 지점으로 당겨 온다 —
    // 보이지 않는 동안에만 벌어지므로 순간이동 자체가 플레이어 눈에 띌 수 없다.
    //
    // ⚠️ 여백을 상수로 추측하면 안 된다(이전 구현의 실수) — 보스 몸집을 모르는 값이라 "일부분이라도
    //    보이면 안 된다"를 보장할 수 없다. 매번 실제 렌더러 월드 AABB를 재서 그 반폭만큼 밀어낸다.
    Renderer[] bossRenderers; // AABB 계산용 — 계층은 안 변하므로 한 번만 캐시
    const float OffscreenPadding = 0.25f; // 부동소수·RT 반올림 여유. 경계에 딱 붙이지 않는다

    // 사용자 피드백(2026-08-13): "보스가 움직일 때 카메라 쉐이킹 약간 주면 덩치가 느껴질듯".
    // 한 걸음 내딛는 순간에만 터지고 잦아드는 펄스다 — 이동 내내 깔리는 지속 진동이 아니다.
    // ⚠️ 되먹임 없음 — 보스는 이제 카메라를 아예 안 읽으므로(플레이어만 본다), 여기서 건 쉐이크가
    //    다시 보스를 밀어 진동이 자가증폭되는 경로가 구조적으로 없다.
    [Header("Step Shake (한 걸음 내딛는 순간의 쿵)")]
    [Tooltip("쿵의 세기 — 플레이어 공격 쉐이크가 0.15, 벽타기가 0.06이다")]
    public float stepShakeMagnitude = 0.08f;
    [Tooltip("쿵이 잦아드는 데 걸리는 시간(초)")]
    public float stepShakeDuration = 0.35f;
    float stepShakeTimer; // 남은 쿵 시간(0이면 조용)

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
    // 20 → 12(사용자 지시 2026-08-13 "빔도 느리게"). ⚠️ Map-test 씬에는 이 20조차 반영된 적이 없어
    // 실제로는 최초값 180으로 돌고 있었다 — 씬 값이 스크립트 기본값을 이긴다는 걸 놓치면 "고쳤는데
    // 왜 그대로냐"가 반복된다. 씬 쪽도 같이 맞춰야 한다.
    [Tooltip("빔이 플레이어를 순간적으로 스냅하지 않고 쫓아가는 느낌을 주는 회전 속도(도/초) — 너무 크면 사실상 항상 명중")]
    public float beamTrackSpeedDegPerSec = 12f;
    // 회전(beamTrackSpeedDegPerSec)엔 이미 속도 제한이 있었지만, 빔 길이(사거리)는 매 프레임
    // 플레이어와의 실제 거리에 그대로 스냅돼(Mathf.Min) 늘어나는/줄어드는 데 아무 제한이 없었다
    // — "직선으로 쫓아오는(커지는) 게 너무 빠르다"는 사용자 지시(2026-08-11)로, 이 길이 변화도
    // 회전과 같은 방식(초당 최대 변화량)으로 속도를 제한한다.
    // 20 → 10(사용자 지시 2026-08-13 "빔도 느리게") — 회전만 늦추고 길이는 그대로 두면 빔이 여전히
    // 쭉 뻗어 나오는 속도로 "빠르다"고 읽힌다. 두 축을 같은 비율로 같이 내린다.
    [Tooltip("빔 길이(사거리)가 플레이어와의 실제 거리를 따라가는 속도(유닛/초) — 순간적으로 늘어나거나 줄지 않는다")]
    public float beamLengthChangeSpeed = 10f;
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

    // 사용자 지시(2026-08-12): "layer2도 빔을 가리게" — 지형은 fan activ와 성격이 다르다. 팬은
    // "안에 들어가 숨는" 것이라 겹침 판정이지만, 지형은 "뒤에 서면 안전"이라 시선 차단이 맞다.
    // ⚠️ 이 마스크에 BossBeamOccluder를 절대 같이 넣지 말 것 — 눈이 높이 있어 차폐물의 그림자가
    // 아래로 길게 깔리는 탓에 팬 옆(밖)에 서 있어도 라인캐스트에 걸려 안전해지던 문제 때문에
    // 2026-08-10에 겹침으로 바꾼 이력이 있다. 그래서 레이어를 따로 판다.
    [Header("Terrain Occlusion (이 레이어가 눈-플레이어 시선을 끊으면 노출 무효)")]
    [Tooltip("지형 타일맵(layer2)의 콜라이더 레이어. 겹침이 아니라 라인캐스트로 판정하므로 뒤에 서 있기만 해도 안전하다")]
    public string terrainOccluderLayerName = "BossBeamTerrain";
    int terrainOccluderMask;

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

    // 사용자 지시(2026-08-12): BossEndTrigger에 닿으면 빔을 멈추고(페이드아웃 + 모든 빔 효과 해제)
    // 오른쪽 상단으로 이동하며 사라진다. BossPixelCamera는 보스가 아니라 메인 카메라를 따라가므로
    // (BossPixelResolutionController) 위치만 옮겨도 화면 밖으로 빠지며 자연스럽게 사라진다 —
    // 알파 페이드가 따로 필요 없다.
    // 사용자 지시(2026-08-13): Map-test는 보스가 처음부터 서 있는 게 아니라, BossTrigger를 밟은 뒤
    // 어둠 속 왼쪽 위에서 걸어 들어오며 눈 → 빔 순서로 켜진다. 그 연출 동안에는 추적·조준·노출 판정이
    // 전부 멈춰 있어야 해서(BossStageDirector가 위치와 라이트를 직접 몰고 간다) 휴면 스위치를 둔다.
    //
    // ⚠️ startDormant를 끄면 이 블록은 존재하지 않는 것과 같다 — 다른 씬(TutorialScene 등)에 배치된
    //    보스는 기본값 false라 예전 그대로 Awake 직후부터 추적을 시작한다(회귀 0).
    [Header("등장 연출 (BossStageDirector가 몰고 간다)")]
    [Tooltip("켜면 씬 시작부터 잠들어 있다 — 라이트 전부 꺼짐 + 추적·빔·노출 판정 정지")]
    public bool startDormant;
    [Tooltip("등장 시작 위치 — 도착 지점(홈) 기준 오프셋. 기본값은 화면 밖 왼쪽 위")]
    public Vector3 introSpawnOffset = new Vector3(-34f, 22f, 0f);

    [Header("퇴장 (BossEndTrigger가 호출)")]
    [Tooltip("빔이 꺼지는 데 걸리는 시간(초) — 이동보다 먼저 빠르게 끝난다")]
    public float beamFadeSeconds = 0.6f;
    [Tooltip("빔이 다 꺼진 뒤 보스가 천천히 물러나는 데 걸리는 시간(초)")]
    public float retreatSeconds = 4f;
    [Tooltip("퇴장 시 현재 위치에서 이동할 오프셋 — 기본값은 오른쪽 위 대각선")]
    public Vector3 retreatOffset = new Vector3(40f, 30f, 0f);
    [Tooltip("보스가 물러나는 동안 같이 페이드 아웃할 BGM(Main Camera의 AudioSource). 비워두면 BGM은 건드리지 않는다")]
    public AudioSource bgm;

    Quaternion restRotation;
    Light2D beamLight;
    Light2D glowLight;

    PlayerController playerController;
    Collider2D playerCollider;    // occluder 겹침 판정용(발밑 피벗 보정 — 몸통 중심을 쓴다)
    float exposureTimer;       // 연속 노출 시간(끊기면 0)
    float exposureDamageTimer; // 디버프가 걸린 뒤 도는 피해 주기
    bool exposureBroken;       // 3초를 넘겨 노이즈·피해가 켜진 상태
    bool retreating;           // 퇴장 시작 후 — 추적·조준·노출 판정을 전부 멈춘다
    bool dormant;              // 등장 연출 전/중 — retreating과 같은 이유로 LateUpdate를 통째로 건너뛴다
    Vector3 sceneStartPosition; // 씬에 찍어둔 원래 자리. 홈 좌표의 Y·Z 기준값이라 Awake에서만 잡는다

    // [ASSERT] 판독구 — 플레이 테스트에서 로그로 확인한다
    public bool IsPlayerExposed { get; private set; }
    public float ExposureSeconds => exposureTimer;
    public bool ExposureBroken => exposureBroken;

    void Awake()
    {
        beamOccluderMask = LayerMask.GetMask(beamOccluderLayerName);
        terrainOccluderMask = LayerMask.GetMask(terrainOccluderLayerName);

        // 등장 연출이 보스를 화면 밖으로 옮기기 **전에** 원래 자리를 잡아 둔다 — 홈 좌표의 Y·Z가 여기서 나온다.
        sceneStartPosition = transform.position;
        dormant = startDormant;

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

        // 위치 배치는 첫 LateUpdate(아래 followInitialized 블록)에서 한다 — player가 아직 null일 수
        // 있고, 어차피 거기서 homeOffset을 잡으므로 한 군데서 처리하는 게 어긋날 여지가 없다.

        if (trackedCamera == null)
        {
            Camera cam = Camera.main;
            if (cam != null) trackedCamera = cam.transform;
        }
        if (trackedCamera != null)
        {
            trackedSectionCamera = trackedCamera.GetComponent<SectionCamera>();
            trackedCameraComponent = trackedCamera.GetComponent<Camera>();
        }
        desiredPosition = transform.position;

        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }

        BuildBeamLight();
    }

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

        // 휴면으로 시작하면 "빔 꺼짐 + 모든 보스 라이트 꺼짐"이 첫 프레임부터 성립해야 한다(사용자 지시).
        // 세기만 0으로 두면 볼류메트릭 안개가 미세하게 남으므로 오브젝트째 끈다.
        if (dormant)
        {
            beamLight.gameObject.SetActive(false);
            glowLight.gameObject.SetActive(false);
        }
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
        if (retreating) return; // 퇴장 연출이 위치·라이트를 직접 몰고 있다 — 추적이 끼어들면 안 된다
        if (dormant) return;    // 등장 연출도 같은 이유(BossStageDirector가 위치·라이트를 직접 몬다)

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else return;
        }

        // ── 걷는 중 / 멈춰 있는 중, 둘 중 하나다. 멈춰 있을 땐 SmoothDamp를 아예 안 돌린다.
        // 첫 LateUpdate에서만 homeOffset을 캡처한다 — Awake 실행 순서에 의존하지 않기 위해서다.
        // (LateUpdate는 씬의 모든 Awake가 끝난 뒤에만 돈다.)
        if (!followInitialized)
        {
            // 등장 시점부터 반드시 플레이어 왼쪽(앞쪽)에 선다 — X만 플레이어 기준으로 다시 잡고,
            // Y·Z는 씬에 잡아둔 구도(눈높이 · 메시 깊이)를 그대로 존중한다.
            transform.position = new Vector3(player.position.x + startOffsetFromPlayerX,
                                             transform.position.y, transform.position.z);
            homeOffset = transform.position - player.position;
            desiredPosition = transform.position;
            followInitialized = true;
            TestLog.Event("boss_step", $"spawn x={transform.position.x:F1} offsetFromPlayer={homeOffset.x:F1}");
        }

        // z는 보스 메시 자체의 깊이(≈18)라 플레이어를 따라가면 안 된다 — X·Y만 추적한다.
        Vector3 anchor = new Vector3(player.position.x + homeOffset.x,
                                     player.position.y + homeOffset.y,
                                     desiredPosition.z);

        if (TryPullBackOffscreen(anchor))
        {
            // 재배치했으면 진행 중이던 걸음은 무효 — 새 자리에서 정지 구간부터 다시 시작한다.
            desiredPosition = transform.position;
            followVelocity = Vector3.zero;
            stepping = false;
            stepRestTimer = stepRestSeconds;
        }
        else if (stepping)
        {
            // ⚠️ 걷는 도중엔 desiredPosition을 절대 갱신하지 않는다 — 매 프레임 플레이어를 다시 조준하면
            //    그게 바로 "상시 이동"이다. 이번 걸음은 걸음을 뗀 순간의 좌표만 붙들고 간다.
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity,
                                                    Mathf.Max(0.01f, followSmoothTime),
                                                    Mathf.Max(0.01f, followMaxSpeed), Time.deltaTime);

            if ((transform.position - desiredPosition).sqrMagnitude <= StepArriveEpsilon * StepArriveEpsilon)
            {
                transform.position = desiredPosition;
                followVelocity = Vector3.zero;
                stepping = false;
                stepRestTimer = stepRestSeconds;
                TestLog.Event("boss_step", "step_end");
            }
        }
        else if (stepRestTimer > 0f)
        {
            stepRestTimer -= Time.deltaTime; // 강제 정지 구간 — 아무리 멀어져도 안 움직인다
        }
        else
        {
            if (Vector3.Distance(desiredPosition, anchor) > Mathf.Max(0.01f, stepTriggerDistance))
            {
                desiredPosition = anchor;
                stepping = true;
                stepShakeTimer = stepShakeDuration; // 걸음을 떼는 그 순간에 쿵
                TestLog.Event("boss_step", $"step_begin to={anchor.x:F1},{anchor.y:F1}");
            }
        }

        UpdateStepShake();

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

    /// <summary>보스가 **화면 밖에 완전히 나가 있는 동안에만**, 화면 밖 위치 중 목표에 가장 가까운
    /// 자리로 순간이동시킨다(사용자 지시 2026-08-13). 재배치했으면 true.
    ///
    /// 판정·배치 모두 보스의 실제 월드 AABB로 한다 — "몸의 아주 일부분이라도 보이면 안 된다"는
    /// 조건은 피벗이 아니라 렌더 경계로만 보장할 수 있다. 몸통 절반(extents)만큼 더 밀어내므로
    /// 순간이동 직후에도 AABB가 화면과 절대 겹치지 않는다.
    ///
    /// 후보는 두 개뿐이다: 지금 이미 벗어나 있는 축(들)의 **같은 쪽** 경계 바깥. 같은 쪽만 쓰므로
    /// 화면을 가로질러 반대편에서 튀어나오는 일이 없고, 그중 목표에 더 가까운 쪽을 고른다.
    ///
    /// ⚠️ 직교 카메라라 Z는 화면 위치에 영향이 없다(원근 없음) — 월드 X·Y만 뷰 크기와 비교한다.
    /// 카메라 중심은 SectionCamera.BasePosition을 쓴다: transform.position은 쉐이크·줌 오프셋이
    /// 전부 합산된 값이라 화면이 흔들리는 순간마다 경계가 미세하게 달라진다.</summary>
    bool TryPullBackOffscreen(Vector3 anchor)
    {
        if (trackedCameraComponent == null || !trackedCameraComponent.orthographic) return false;
        if (!TryGetBossWorldBounds(out Bounds b)) return false;

        Vector3 camCenter = trackedSectionCamera != null ? trackedSectionCamera.BasePosition : trackedCamera.position;
        float halfH = trackedCameraComponent.orthographicSize;
        float halfW = halfH * trackedCameraComponent.aspect;
        if (halfH <= 0.01f || halfW <= 0.01f) return false;

        // AABB가 화면과 겹치는지 — 겹치면(=조금이라도 보이면) 순간이동 자체를 하지 않는다.
        float limitX = halfW + b.extents.x + OffscreenPadding;
        float limitY = halfH + b.extents.y + OffscreenPadding;
        float offX = b.center.x - camCenter.x;
        float offY = b.center.y - camCenter.y;
        bool xOutside = Mathf.Abs(offX) >= limitX;
        bool yOutside = Mathf.Abs(offY) >= limitY;
        if (!xOutside && !yOutside) return false;

        // 피벗과 렌더 중심이 다르다(메시 피벗이 바닥 쪽) — 경계 계산은 중심으로 하고 결과는 피벗으로 되돌린다.
        Vector3 pivotFromCenter = transform.position - b.center;

        Vector3 best = Vector3.zero;
        float bestDist = float.MaxValue;

        if (xOutside)
        {
            Vector3 cand = new Vector3(camCenter.x + Mathf.Sign(offX) * limitX + pivotFromCenter.x,
                                       anchor.y, transform.position.z);
            float d = Vector2.Distance(cand, anchor);
            if (d < bestDist) { bestDist = d; best = cand; }
        }
        if (yOutside)
        {
            Vector3 cand = new Vector3(anchor.x,
                                       camCenter.y + Mathf.Sign(offY) * limitY + pivotFromCenter.y,
                                       transform.position.z);
            float d = Vector2.Distance(cand, anchor);
            if (d < bestDist) { bestDist = d; best = cand; }
        }

        // 이미 그만큼 가까우면 굳이 옮기지 않는다 — 매 프레임 무의미하게 걸음 상태를 리셋하지 않기 위해서다.
        if (bestDist >= Vector2.Distance(transform.position, anchor) - 0.5f) return false;

        transform.position = best;
        TestLog.Event("boss_step", $"pullback to={best.x:F1},{best.y:F1} dist={bestDist:F1}");
        return true;
    }

    /// <summary>보스의 현재 월드 AABB(자식 렌더러 전부 합산). 렌더러가 하나도 없으면 false.</summary>
    bool TryGetBossWorldBounds(out Bounds bounds)
    {
        if (bossRenderers == null) bossRenderers = GetComponentsInChildren<Renderer>();

        bounds = default;
        bool any = false;
        for (int i = 0; i < bossRenderers.Length; i++)
        {
            Renderer r = bossRenderers[i];
            if (r == null || !r.enabled) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    /// <summary>한 걸음의 쿵을 감쇠시킨다 — 덩치 연출(사용자 피드백 2026-08-13).
    ///
    /// SectionCamera의 전용 채널(SetAmbientShake)을 쓴다. 두 가지 이유다:
    /// ① SetSustainedShake는 플레이어 연출(일섬 차지·빛 소모)이 매 프레임 덮어쓰는 슬롯이라 같이
    ///    쓰면 서로를 지운다. ② Shake()는 매 프레임 난수(백색 잡음)에 shakeOffset을 공유해서
    ///    StopShake()에 같이 끊기는데, ambient 채널은 Perlin 저주파라 같은 세기에서도 훨씬 무겁게
    ///    읽힌다("지지직"이 아니라 "쿵").</summary>
    void UpdateStepShake()
    {
        if (trackedSectionCamera == null) return;

        if (stepShakeTimer <= 0f)
        {
            trackedSectionCamera.SetAmbientShake(0f);
            return;
        }

        stepShakeTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(stepShakeTimer / Mathf.Max(0.01f, stepShakeDuration));
        trackedSectionCamera.SetAmbientShake(stepShakeMagnitude * k * k); // 제곱 감쇠 — 초반이 세고 빨리 잦아든다
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
        if (IsSightBlockedByTerrain(eyeWorld)) return false;

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

    /// <summary>지형(layer2)이 눈→플레이어 시선을 끊는지. 겹침이 아니라 라인캐스트라 "뒤에 숨으면
    /// 안전"이 된다 — occluder(겹침)와는 독립된 경로고, 둘 중 하나만 걸려도 노출이 꺼진다.
    /// 지형 콜라이더는 isTrigger라 물리적으로 막지 않지만 Physics2D.queriesHitTriggers가 true라
    /// 라인캐스트에는 정상적으로 걸린다.</summary>
    bool IsSightBlockedByTerrain(Vector3 eyeWorld)
    {
        if (terrainOccluderMask == 0) return false;

        if (playerCollider == null) playerCollider = player.GetComponentInChildren<Collider2D>();
        Vector2 target = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;

        return Physics2D.Linecast(eyeWorld, target, terrainOccluderMask).collider != null;
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
            // 사용자 지시(2026-08-13): "빔에 의해 노이즈가 나타날 때 BossBeamSFX 재생".
            // 노이즈를 켜는 이 지점 하나에만 건다 — 노출이 끊겼다 다시 걸리면 다시 울린다.
            GameSfx.Play(Sfx.BossBeamNoise);
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

    // ── 등장 연출 (BossStageDirector 전용) ────────────────────────────────────────────────────
    // 여기 있는 이유: beamLight·glowLight는 런타임에 만들어지는 private 참조라 밖에서 못 만진다.
    // 순서(이동 → 눈 → 빔 위쪽 페이드 인 → 플레이어 조준)는 감독이 정하고, 각 동작의 구현만 가져간다.

    /// <summary>등장이 끝나고 보스가 서 있어야 할 자리 — X는 플레이어 기준
    /// <see cref="startOffsetFromPlayerX"/>, Y·Z는 씬에 잡아둔 구도를 그대로 쓴다.
    /// LateUpdate의 followInitialized 블록이 계산하는 것과 정확히 같은 값이라, 연출이 끝나고 추적이
    /// 켜지는 순간 보스가 튀지 않는다.</summary>
    public Vector3 ResolveHomePosition()
    {
        Transform p = ResolveIntroPlayer();
        if (p == null) return sceneStartPosition;
        return new Vector3(p.position.x + startOffsetFromPlayerX, sceneStartPosition.y, sceneStartPosition.z);
    }

    /// <summary>등장 시작 위치(홈 + <see cref="introSpawnOffset"/>)로 순간이동시킨다. 라이트는 전부 꺼진 채다.</summary>
    public void IntroPlaceAtSpawn()
    {
        dormant = true;
        if (beamLight != null) beamLight.gameObject.SetActive(false);
        if (glowLight != null) glowLight.gameObject.SetActive(false);
        transform.position = ResolveHomePosition() + introSpawnOffset;
        TestLog.Event("boss_intro", $"spawn at={transform.position.x:F1},{transform.position.y:F1}");
    }

    /// <summary>시작 위치에서 홈까지 걸어 들어온다(감속 도착 — 덩치가 멈춰 서는 느낌).</summary>
    public IEnumerator IntroMoveIn(float duration)
    {
        Vector3 from = transform.position;
        Vector3 to = ResolveHomePosition();
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, IntroMaxStep);
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d)));
            yield return null;
        }
        transform.position = to;
        TestLog.Event("boss_intro", "move_in_done");
    }

    /// <summary>눈(넓은 원형 광원)만 켠다 — 빔은 그대로 꺼진 채다. 밝기와 반경이 같이 자라난다.</summary>
    public IEnumerator IntroEyeLightUp(float duration)
    {
        if (glowLight == null) yield break;

        glowLight.gameObject.SetActive(true);
        glowLight.color = glowColor;
        PlaceLightsAtEye();

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, IntroMaxStep);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
            glowLight.intensity = glowIntensity * k;
            glowLight.pointLightInnerRadius = glowInnerRadius * k;
            glowLight.pointLightOuterRadius = glowOuterRadius * k;
            PlaceLightsAtEye();
            yield return null;
        }
        glowLight.intensity = glowIntensity;
        glowLight.pointLightInnerRadius = glowInnerRadius;
        glowLight.pointLightOuterRadius = glowOuterRadius;
        TestLog.Event("boss_intro", "eye_on");
    }

    /// <summary>빔을 **위쪽**을 향한 채로 켠다(밝기와 길이가 같이 자라난다). 아직 플레이어를 겨누지 않는다.</summary>
    public IEnumerator IntroBeamUp(float duration)
    {
        if (beamLight == null) yield break;

        beamLight.gameObject.SetActive(true);
        beamLight.color = beamColor;
        beamLight.pointLightInnerAngle = beamInnerAngle;
        beamLight.pointLightOuterAngle = beamOuterAngle;
        beamLight.transform.rotation = Quaternion.FromToRotation(beamAimLocalAxis.normalized, Vector3.up);
        PlaceLightsAtEye();

        float targetLength = ResolveBeamTargetLength();
        float targetIntensity = ResolveBeamTargetIntensity();

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, IntroMaxStep);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
            beamLight.intensity = targetIntensity * k;
            beamLight.volumeIntensity = k;
            beamLight.pointLightOuterRadius = targetLength * k;
            PlaceLightsAtEye();
            yield return null;
        }
        beamLight.intensity = targetIntensity;
        beamLight.volumeIntensity = 1f;
        beamLight.pointLightOuterRadius = targetLength;
        TestLog.Event("boss_intro", "beam_up");
    }

    /// <summary>위를 보던 빔을 플레이어 쪽으로 돌린다. 여기까지가 등장 연출의 마지막 동작이다.</summary>
    public IEnumerator IntroBeamAimToPlayer(float duration)
    {
        if (beamLight == null) yield break;

        Quaternion from = beamLight.transform.rotation;
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, IntroMaxStep);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
            // 목표는 매 프레임 다시 구한다 — 연출 중 플레이어는 안 움직이지만, 카메라 줌 아웃으로
            // 보스가 밀려나는 경우까지 감안해 항상 "지금의" 플레이어를 겨누게 한다.
            beamLight.transform.rotation = Quaternion.Slerp(from, ResolveBeamAimToPlayer(), k);
            beamLight.intensity = ResolveBeamTargetIntensity();
            beamLight.pointLightOuterRadius = ResolveBeamTargetLength();
            PlaceLightsAtEye();
            yield return null;
        }
        beamLight.transform.rotation = ResolveBeamAimToPlayer();
        TestLog.Event("boss_intro", "beam_aimed");
    }

    /// <summary>휴면 해제 — 이 시점부터 평소의 추적·조준·노출 판정이 돈다.</summary>
    public void IntroFinish()
    {
        if (!dormant) return;
        dormant = false;
        // 빔 길이를 지금 값에서 이어받게 한다. 안 하면 LateUpdate의 첫 프레임이 beamCurrentLength=0에서
        // 시작해 빔이 한 번 사그라들었다 다시 뻗는다.
        if (beamLight != null)
        {
            beamCurrentLength = beamLight.pointLightOuterRadius;
            beamLengthInitialized = true;
        }
        TestLog.Event("boss_intro", "finish");
    }

    // 연출 동안에는 LateUpdate가 통째로 빠져 있어 라이트를 아무도 안 옮긴다 — 매 프레임 직접 붙인다.
    // z=0 평면에 두는 이유는 LateUpdate 쪽과 같다(URP 2D 그림자는 광원과 캐스터가 같은 평면이어야 한다).
    void PlaceLightsAtEye()
    {
        Vector3 eye = transform.TransformPoint(eyeLocalOffset);
        Vector3 lightPos = new Vector3(eye.x, eye.y, 0f);
        if (beamLight != null) beamLight.transform.position = lightPos;
        if (glowLight != null) glowLight.transform.position = lightPos;
    }

    Transform ResolveIntroPlayer()
    {
        if (player != null) return player;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        return player;
    }

    // 연출이 끝나는 순간의 빔 길이·세기를 LateUpdate와 **같은 식**으로 구한다 — 두 곳의 값이 다르면
    // 휴면이 풀리는 프레임에 빔이 눈에 띄게 튄다.
    float ResolveBeamTargetLength()
    {
        Transform p = ResolveIntroPlayer();
        if (p == null) return beamRange;
        Vector3 eye = transform.TransformPoint(eyeLocalOffset);
        return Mathf.Min(Vector3.Distance(eye, new Vector3(p.position.x, p.position.y, eye.z)), beamRange);
    }

    float ResolveBeamTargetIntensity()
    {
        Transform p = ResolveIntroPlayer();
        if (p == null) return beamIntensity;
        Vector3 eye = transform.TransformPoint(eyeLocalOffset);
        float dist = Vector3.Distance(eye, new Vector3(p.position.x, p.position.y, eye.z));
        float distT = Mathf.Clamp01(dist / Mathf.Max(0.01f, beamRange));
        return beamIntensity * Mathf.Lerp(beamNearIntensityMultiplier, beamFarIntensityMultiplier, distT);
    }

    Quaternion ResolveBeamAimToPlayer()
    {
        Transform p = ResolveIntroPlayer();
        if (p == null) return beamLight.transform.rotation;
        Vector3 eye = transform.TransformPoint(eyeLocalOffset);
        Vector3 dir = new Vector3(p.position.x - eye.x, p.position.y - eye.y, 0f);
        if (dir.sqrMagnitude < 0.0001f) return beamLight.transform.rotation;
        return Quaternion.FromToRotation(beamAimLocalAxis.normalized, dir.normalized);
    }

    // 씬 로드 직후 첫 프레임의 unscaledDeltaTime은 1초를 넘길 수 있다(ScreenBlackout 주석 참고) —
    // 그대로 누적하면 등장 연출이 한 프레임에 끝나 버린다.
    const float IntroMaxStep = 0.05f;

    /// <summary>BossEndTrigger가 호출한다 — 빔을 멈추고(현재 걸려 있는 노출 효과 전부 즉시 해제),
    /// 두 라이트를 페이드아웃하면서 오른쪽 상단으로 물러나 사라진다. 두 번 불러도 한 번만 돈다.</summary>
    public void BeginRetreat()
    {
        if (retreating) return;
        retreating = true;

        // "현재 모든 빔 효과 해제" — 화면 노이즈(ScreenGlitchFx)·노출 타이머·피해 주기를 즉시 끈다.
        ClearExposure();
        IsPlayerExposed = false;
        reacquireCooldown = 0f;
        // 퇴장 중엔 LateUpdate가 통째로 빠져나가 쿵을 아무도 안 내린다 — 마지막 값이 그대로 카메라에
        // 남으므로 여기서 명시적으로 끈다(퇴장 이동은 자체 코루틴이 몰고 간다).
        stepShakeTimer = 0f;
        if (trackedSectionCamera != null) trackedSectionCamera.SetAmbientShake(0f);

        StartCoroutine(RetreatRoutine());
    }

    // 순서가 중요하다(사용자 지시 2026-08-12): 빔이 "먼저 빠르게" 꺼지고, 그 다음에야 "천천히"
    // 이동하며 떠난다. 둘을 동시에 돌리면 물러나는 보스에 빔이 끌려가 마무리가 흐려진다.
    IEnumerator RetreatRoutine()
    {
        // ── 1단계: 빔을 빠르게 끈다 ─────────────────────────────────────────────
        float beamIntensity0 = beamLight != null ? beamLight.intensity : 0f;
        float beamVolume0 = beamLight != null ? beamLight.volumeIntensity : 0f;
        float glowIntensity0 = glowLight != null ? glowLight.intensity : 0f;

        float fadeDuration = Mathf.Max(0.01f, beamFadeSeconds);
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);

            // volumeIntensity(안개 빔)를 같이 내리지 않으면 표면 조명만 어두워지고 붉은 안개는
            // 그대로 남는다(BuildBeamLight의 볼류메트릭 주석과 같은 이유).
            if (beamLight != null)
            {
                beamLight.intensity = Mathf.Lerp(beamIntensity0, 0f, k);
                beamLight.volumeIntensity = Mathf.Lerp(beamVolume0, 0f, k);
            }
            if (glowLight != null) glowLight.intensity = Mathf.Lerp(glowIntensity0, 0f, k);

            yield return null;
        }

        // 라이트는 BuildBeamLight에서 루트 오브젝트로 만들어져 보스의 자식이 아니다 —
        // 보스만 끄면 꺼진 채로 남으니 명시적으로 같이 끈다.
        if (beamLight != null) beamLight.gameObject.SetActive(false);
        if (glowLight != null) glowLight.gameObject.SetActive(false);
        TestLog.Event("boss_beam", "retreat_beam_off");

        // ── 2단계: 천천히 물러난다. BGM도 이 구간에 맞춰 같이 내려간다 ──────────
        Vector3 from = transform.position;
        Vector3 to = from + retreatOffset;
        float bgmVolume0 = bgm != null ? bgm.volume : 0f;

        float moveDuration = Mathf.Max(0.01f, retreatSeconds);
        t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / moveDuration);

            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
            if (bgm != null) bgm.volume = Mathf.Lerp(bgmVolume0, 0f, k);

            yield return null;
        }
        if (bgm != null) bgm.volume = 0f;

        TestLog.Event("boss_beam", "retreat_done");
        gameObject.SetActive(false);
    }

    // 보스가 사라져도 노이즈·진동이 화면에 남지 않게 한다(씬 전환 · 오브젝트 파괴).
    void OnDisable()
    {
        ClearExposure();
        stepShakeTimer = 0f;
        if (trackedSectionCamera != null) trackedSectionCamera.SetAmbientShake(0f);
    }
}

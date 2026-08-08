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
    public float beamOuterAngle = 45f;
    public float beamRange = 25f;
    [Tooltip("빔이 플레이어를 순간적으로 스냅하지 않고 쫓아가는 느낌을 주는 회전 속도(도/초)")]
    public float beamTrackSpeedDegPerSec = 180f;
    // Light2D의 Point(=인스펙터상 Spot) 타입 콘이 실제로 어느 로컬 축을 향해 뻗는지 불확실해
    // 노출해둠 — 빔이 눈 방향과 90도 어긋나 보이면 이 값을 Vector3.right 등으로 바꾼다.
    public Vector3 beamAimLocalAxis = Vector3.up;
    // 보스 본체는 화면에 보이면 안 되는 동안(배경 뒤) PixelBoss 레이어를 쓰지만, 빛은 실제로
    // 플레이어·주변 오브젝트를 비춰야 한다는 지시(2026-08-08)에 따라 기본(Default) 레이어에 둔다 —
    // 그래야 메인 카메라가 이 빛의 영향을 받는 실제 화면을 그린다. 다른 레이어를 쓰고 싶으면 여기서 바꾼다.
    public string beamLightLayerName = "Default";

    Quaternion restRotation;
    Light2D beamLight;

    void Awake()
    {
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
        if (trackedCamera != null) lastCameraPosition = trackedCamera.position;
        desiredPosition = transform.position;

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
            Vector3 cameraDelta = trackedCamera.position - lastCameraPosition;
            cameraDelta.z = 0f;
            desiredPosition += cameraDelta;
            lastCameraPosition = trackedCamera.position;

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
        beamLight.transform.position = eyeWorldNow;
        Vector3 aimDir = player.position - eyeWorldNow;
        aimDir.z = 0f;
        if (aimDir.sqrMagnitude > 0.0001f)
        {
            // 순간 스냅 대신 각속도 기반으로 따라가게 해서 "추적하는" 느낌을 준다.
            Quaternion targetAim = Quaternion.FromToRotation(beamAimLocalAxis.normalized, aimDir.normalized);
            beamLight.transform.rotation = Quaternion.RotateTowards(beamLight.transform.rotation, targetAim, beamTrackSpeedDegPerSec * Time.deltaTime);
        }
    }
}

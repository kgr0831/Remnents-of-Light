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
    // ⚠️ Light2DCullResult는 camera.cullingMask & (1 << light.gameObject.layer)로 라이트를 컬링한다 —
    //    PixelBoss(16)에 두면 메인 카메라가 이 빛을 아예 안 그린다.
    public string beamLightLayerName = "Default";

    // 사용자 지시(2026-08-09): "보스의 빛이 타일과 모든 요소를 비추게" — 눈에서 나가는 좁은 빔만으로는
    // 부채꼴이 지나가는 자리밖에 밝아지지 않는다(씬에 Global Light 2D가 하나도 없어서 나머지는 전부
    // 검게 남는다). 그래서 눈을 중심으로 방 전체를 덮는 넓은 원형 광원을 하나 더 둬서 타일·지형·적·
    // 플레이어가 실제로 보스 빛을 받게 한다. 이쪽은 볼류메트릭을 끈다 — 빔의 부채꼴 연출은 그대로
    // 두고 "조명"만 담당(안 그러면 화면 전체가 붉은 안개로 덮인다).
    // 사용자 지시(2026-08-09): "레이저 빔이 플레이어를 추격하는 형태(플레이어보다 약간 느림)".
    // 각속도만으로 쫓으면 멀리 있을 때는 순식간에 따라붙고 가까이 붙으면 절대 못 따라잡는 —
    // "추격"이 아니라 "조준"이 된다. 그래서 월드 공간의 조준점(beamAimPoint)이 플레이어를
    // 이동속도 기준으로 뒤쫓게 하고, 빔은 그 점을 겨눈다. 플레이어보다 느리니 달아나면 벗어난다.
    [Header("Beam Chase (조준점이 플레이어를 뒤쫓는 속도)")]
    [Tooltip("플레이어 이동속도 대비 배율 — 1보다 작아야 도망칠 수 있다")]
    public float beamChaseSpeedFactor = 0.85f;
    [Tooltip("플레이어(PlayerController)를 못 찾았을 때 쓸 초당 이동 속도")]
    public float beamChaseSpeedFallback = 4.5f;

    // 사용자 지시(2026-08-09): 빔에 3초 이상 계속 노출되면 자아 고갈과 같은 화면 노이즈가 걸리고
    // 5초에 한 칸씩 체력이 깎인다. 노출이 끊기면 카운트도 노이즈도 즉시 0으로 돌아간다.
    // ⚠️ 첫 피해 시점은 자아 붕괴(egoDepletedDamageInterval)와 같은 규칙을 따른다 — 디버프가
    //    걸린 뒤 한 주기를 꽉 채워야 들어온다(=노출 3초 + 5초 = 8초째). 더 빡세게 하려면
    //    exposureDamageInterval을 줄이면 된다.
    [Header("Beam Exposure (빔에 계속 노출되면 붕괴)")]
    public float exposureToBreakSeconds = 3f;
    public float exposureDamageInterval = 5f;
    public int exposureDamage = 1;

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
    Vector3 beamAimPoint;      // 플레이어를 뒤쫓는 월드 조준점 — 빔은 항상 이 점을 겨눈다
    float exposureTimer;       // 연속 노출 시간(끊기면 0)
    float exposureDamageTimer; // 디버프가 걸린 뒤 도는 피해 주기
    bool exposureBroken;       // 3초를 넘겨 노이즈·피해가 켜진 상태

    // [ASSERT] 판독구 — 플레이 테스트에서 로그로 확인한다
    public bool IsPlayerExposed { get; private set; }
    public float ExposureSeconds => exposureTimer;
    public bool ExposureBroken => exposureBroken;

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

        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            beamAimPoint = player.position; // 시작할 땐 플레이어를 정확히 겨눈 상태
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

        // 넓은 조명은 눈 위치만 따라가면 된다(방향 없음). 플레이 모드에서 인스펙터로 세기·반경을
        // 바로 굴려볼 수 있도록 값도 매 프레임 반영한다.
        if (glowLight != null)
        {
            glowLight.transform.position = eyeWorldNow;
            glowLight.color = glowColor;
            glowLight.intensity = glowIntensity;
            glowLight.pointLightInnerRadius = glowInnerRadius;
            glowLight.pointLightOuterRadius = glowOuterRadius;
        }

        // ── 조준점이 플레이어를 "약간 느리게" 뒤쫓는다(사용자 지시 2026-08-09) ──
        // 빔은 플레이어가 아니라 이 점을 겨눈다. 플레이어 이동속도보다 느리므로 계속 달리면
        // 빔이 뒤로 처지다가 부채꼴 밖으로 빠진다(=노출 카운트가 끊긴다).
        float chaseSpeed = (playerController != null ? playerController.moveSpeed : beamChaseSpeedFallback)
                           * Mathf.Max(0f, beamChaseSpeedFactor);
        Vector3 playerFlat = new Vector3(player.position.x, player.position.y, eyeWorldNow.z);
        beamAimPoint = Vector3.MoveTowards(beamAimPoint, playerFlat, chaseSpeed * Time.deltaTime);

        Vector3 aimDir = beamAimPoint - eyeWorldNow;
        aimDir.z = 0f;
        if (aimDir.sqrMagnitude > 0.0001f)
        {
            // 순간 스냅 대신 각속도로도 한 번 더 눌러 준다 — 조준점이 눈 바로 옆을 스칠 때
            // 각도가 순간적으로 튀는 것을 막는 안전장치다(평소엔 거의 걸리지 않는다).
            Quaternion targetAim = Quaternion.FromToRotation(beamAimLocalAxis.normalized, aimDir.normalized);
            beamLight.transform.rotation = Quaternion.RotateTowards(beamLight.transform.rotation, targetAim, beamTrackSpeedDegPerSec * Time.deltaTime);
        }

        UpdateExposure(eyeWorldNow);
    }

    /// <summary>플레이어가 빔 부채꼴 안에 있는지 — 사거리(beamRange)와 외곽각(beamOuterAngle, 전체각)으로만
    /// 판정한다. 지형 차폐는 보지 않는다(요구사항 밖 · 2D 부채꼴 빛 자체도 벽을 무시한다).</summary>
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
        return Vector3.Angle(beamDir, toPlayer) <= beamOuterAngle * 0.5f;
    }

    // 3초 이상 연속 노출 → 자아 고갈과 같은 화면 노이즈 + 5초마다 체력 한 칸(사용자 지시 2026-08-09).
    // 노출이 한 프레임이라도 끊기면 카운트·노이즈·피해 주기가 전부 0으로 돌아간다.
    void UpdateExposure(Vector3 eyeWorld)
    {
        IsPlayerExposed = IsPlayerInBeam(eyeWorld);

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
        if (playerController != null)
        {
            playerController.TakeDamage(exposureDamage);
            TestLog.Event("boss_beam", $"exposure_damage -{exposureDamage} hp={playerController.currentHealth}/{playerController.maxHealth}");
        }
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

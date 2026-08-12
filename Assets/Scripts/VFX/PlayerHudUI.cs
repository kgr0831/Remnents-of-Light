using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 HUD — 체력 아이콘(스프라이트 스왑) + 빛 에너지 게이지
/// (기능_구현_명세서 5장 "게임 UI (HUD): 플레이어 체력바, 빛 에너지 게이지").
///
/// 체력은 더 이상 칸을 늘어놓지 않는다(2026-08-10 사용자 지시) — Assets/Resources/UI/HpBar.png
/// 한 장(8프레임, HP1~8)과 Hp_Zero.png(HP0) 사이를 스프라이트째로 스왑하는 아이콘 하나가 전부다.
/// 프레임은 왼쪽부터 x좌표 순으로 HP1..HP8이며(실측: 프레임마다 밝은 빨강 픽셀 수가
/// 30,60,90,120,128,136,144,152로 단조 증가 — 이름이 아니라 rect.x로 정렬해야 안전하다),
/// 낮은 프레임일수록 어두운 적갈색(142,0,0) 칸이 많고 높은 프레임일수록 밝은 빨강(237,0,0) 칸이
/// 많다. 두 프레임 사이를 그냥 스냅하면 "칸이 갑자기 사라진다"만 보이므로, 옛 칩/예고 바 관습(에너지
/// 게이지의 EnergyLoss/EnergyGain)을 그대로 가져와 두 스프라이트를 겹쳐 알파로 크로스페이드한다
/// (ApplyHpIcon 참고) — 위 프레임 스프라이트가 서로 "빨간 칸의 초집합/부분집합" 관계라, 알파만
/// 바꿔도 자연히 "칸이 빨강→검정(또는 반대)으로 전환되는" 것처럼 보인다. 한 번에 여러 칸이 바뀌어도
/// (사용자 지시: "2,3개씩 줄거나 늘 수도 있다") 프레임을 하나씩 훑고 지나가는 연속값이라 그대로 대응된다.
///
/// 유닛별 스무딩 배열(_pipDisplay)은 예전 "칸 5개" 구조 그대로 남겨뒀다 — PlayTestRunner가
/// PipCount/LitPipCount/PipDisplay(i)로 화면에 그려지는 값을 직접 검증하기 때문에, 렌더링 방식만
/// 바꾸고 그 밑의 유닛별 지연·스무딩 로직(맞은 순간의 pipLossDelay 등)은 그대로 재사용한다.
///
/// 폭주 중 자아 고갈 회색 오버레이도 예전엔 칸마다 하나씩(5개) 채웠지만, 이제 아이콘이 하나뿐이라
/// 오버레이도 하나로 통합했다(사용자 지시: "회색 HP 셀 전환 효과는 UI 전체에 한번에 적용").
///
/// 빨간 칸만 블룸시키는 것(사용자 지시 2026-08-10)은 Screen Space Overlay 캔버스가 URP
/// 포스트프로세싱을 아예 안 받는다는 제약 때문에 별도 파이프라인이 필요하다 — 전용 레이어(HPBloom)
/// + 그 레이어만 컬링하는 Overlay 카메라(메인 카메라 스택에 추가) + 그 카메라의 Volume Mask에만
/// 걸리는 전용 Bloom 볼륨을 코드로 만든다(BuildBloomPipeline — 2026-08-11부터 광원 블룸과 공유).
/// 전역 DefaultVolumeProfile은
/// 건드리지 않는다 — 이미 threshold=0.9/intensity=0으로 다른 용도(플레이어 블룸)를 위해 대기 중인
/// 설정이라, 만졌다간 게임 전체 밝은 픽셀이 다 번진다. 아이콘 위 "빨간 칸"만 골라내는 것도 카메라
/// 컬링마스크가 아니라(그건 오브젝트 단위 필터일 뿐 픽셀 단위가 아니다) 마스크 텍스처
/// (HpBarGlowMask.png, PlayerMaskEmissive.shader와 같은 기법)로 한다 — 흰=빨간 칸, 검=그 외.
///
/// DodgeUI/ExecutionUI와 같은 자가완결 방식 — 씬에 배치하지 않고 런타임에 전부 코드로 만든다.
/// 블룸 카메라·볼륨·글로우 캔버스까지 전부 이 안에서 만들어지므로, 이 스크립트가 붙는 씬이라면
/// (map-test든 UISandbox든) 별도 씬 작업 없이 그대로 동작한다.
///
/// 광원(에너지) 게이지도 더 이상 채움 바가 아니다(2026-08-11 사용자 지시) — Resources/UI/EnergyBar.png
/// (8프레임, x좌표 순 정렬)와 EnergyBar-Zero.png(0) 사이를 스프라이트째로 스왑하는 아이콘 하나로
/// 바뀐다. HP와 달리 크로스페이드는 하지 않는다 — 사용자 지시가 "n/8 이상일 때 그 프레임으로
/// 스위치"라는 계단 함수를 명시했고, 어차피 매 프레임 스무딩되는 _energyDisplay를 그대로 문턱값에
/// 먹이므로 경계를 넘는 순간 자연스럽게 프레임이 바뀐다(ApplyEnergyIcon 참고).
///
/// EnergyBar.png는 HpBar.png와 같은 2색 픽셀아트지만 색상이 청록이라(실측: 꺼진 칸(9,88,95)/켜진
/// 칸(52,221,236) — 둘 다 빨강 채널이 거의 0) HP의 _LitTint 셰이더(빨강 채널 문턱)를 그대로 못
/// 쓴다. 전용 셰이더 Custom/UIEnergyIconBody가 대신 파랑 채널(0.37 대 0.93로 크게 갈림)로 켜진/꺼진
/// 칸을 나누고, 켜진 칸은 _LitTint로(저에너지/폭주 시 매 프레임 빨갛게), 꺼진 칸은 평소엔 원본
/// 그대로 두다가 폭주 중에만 _UnlitTint로 어둡게 눌러 대비를 살린다(사용자 지시 2026-08-11 "폭주
/// 상태에서 빈칸이 잘 안보이니까 대비 잘되게"). HP와 마찬가지로 흰색 아웃라인(Custom/
/// UISilhouetteOutline)도 두른다.
///
/// 블룸도 HP와 같은 파이프라인을 공유한다(BuildBloomPipeline, 색만 다름 — 사용자 지시 2026-08-11)
/// — 전용 글로우 마스크(Resources/UI/EnergyBarGlowMask.png, EnergyBar.png의 켜진 칸만 흰색으로
/// 뽑아 미리 구운 텍스처)로 Custom/UIHpGlow 셰이더를 그대로 재사용한다. 폭주 중엔 글로우가
/// RampageHeartbeatFx의 lub 박동 리듬을 참고한 빠른 삼각파로 페이드 인/아웃한다(RampagePulse 참고).
/// </summary>
[ExecuteAlways]
public class PlayerHudUI : MonoBehaviour
{
    [Header("Layout (CanvasScaler 1920x1080 기준 px, 좌상단 원점)")]
    public Vector2 origin = new Vector2(56f, -52f);
    public Vector2 hpIconSize = new Vector2(96f, 96f); // 광원 아이콘도 이 크기를 그대로 쓴다(사용자 지시: "크기는 HP UI와 동일")
    public float barGap = 14f;

    // 원본 아트의 "켜진 칸" 색(237,0,0)이 "꺼진 칸"(142,0,0)과 명도 차이만 있어 대비가 잘 안
    // 느껴졌다(사용자 지시 2026-08-11 "아직 차있는 HP를 좀 더 연하고 밝은 색으로") — 켜진 칸만
    // 이 색으로 다시 칠한다(Custom/UIHpIconBody._LitTint). 꺼진 칸은 원래 색 그대로 둔다.
    [Header("HP 칸 색 대비")]
    public Color hpLitTint = new Color(1f, 0.42f, 0.38f, 1f);

    [Header("Lerp 스무딩")]
    public float hpLerpSpeed = 8f;       // 유닛별 스무딩 배열이 목표로 접근하는 속도(기존 pipLerpSpeed)
    public float hpLossDelay = 0.18f;    // 맞은 직후 그 유닛이 잠깐 그대로 남아 있는 시간(기존 pipLossDelay)
    public float energyLerpSpeed = 10f;
    // 원격/비포커스 에디터는 프레임이 길게 튀는데(수백 ms), 그대로 쓰면 한 프레임에 목표까지
    // 도달해 "스무딩"이 사라진다. UI 보간에서 흔한 방어로 dt에 상한을 둔다.
    public float maxSmoothDelta = 0.05f;

    [Header("색")]
    // 광원바 색(2026-08-02 사용자 지시: "픽셀 이팩트 폭주=붉은, 초월=흰, 둘다 아니면 흰"과 통일):
    // 옛 채움 바 시절엔 이 색을 Image.color로 "곱했을" 뿐이라 흰색=무변화(원래 바 색 그대로)였다.
    // 지금은 Custom/UIEnergyIconBody._LitTint가 켜진 칸 색을 통째로 "대체"하므로, 흰색을 그대로
    // 넣으면 EnergyBar.png의 원래 청록이 지워지고 하얗게 빛나 보인다(사용자 리포트 2026-08-11
    // "과하게, 이상한 색상으로 빛납니다" — 블룸 세기가 아니라 이 흰색 대체가 원인이었다). 그래서
    // "평상시"의 기본값을 EnergyBar.png 실측 켜진 칸 색(52,221,236)으로 바꿔 사실상 무변화가 되게
    // 한다. 초월도 같은 이유로 흰색 대신 같은 청록을 쓴다(상태별로 독립 튜닝할 수 있게 필드는
    // 분리 — 폭주만 rampageColor로 붉게 갈린다).
    public Color energyColor = new Color(0.204f, 0.867f, 0.925f, 1f);
    public Color energyLowColor = new Color(0.95f, 0.25f, 0.22f); // A-2/C-3: 에너지 부족 경고색
    public float energyColorLerpSpeed = 20f; // 정상↔경고색 전환 속도(대략 0.15s)
    public Color transcendColor = new Color(0.204f, 0.867f, 0.925f, 1f); // 초월 중(평상시와 동일 — 위 주석 참고)
    public float transcendColorLerpSpeed = 20f; // 저에너지 경고와 같은 속도(대략 0.15s)
    public Color rampageColor = new Color(0.95f, 0.20f, 0.18f); // 폭주 중(2026-08-02 신규 지시)
    public float rampageColorLerpSpeed = 20f; // 위 두 전환과 같은 속도

    // 자아 바를 따로 그리지 않고(사용자 지시 2026-08-01: "더이상 자아 게이지가 바로 표시되지 않고"),
    // HP 아이콘 자체가 자아 상태를 대신 표시한다 — 전부 폭주 중에만 켜진다(자아는 폭주 중에만 의미 있는 값).
    //   · 자아가 줄어드는 만큼 아이콘 위에 회색이 위→아래로 차올라 자아 0에서 완전한 회색이 된다
    //     (아이콘 전체를 덮는 EgoGray 오버레이 하나, Image.Filled/Vertical/origin=Top의
    //     fillAmount = 1-자아비율 — 2026-08-10 사용자 지시로 칸마다 하나였던 걸 하나로 통합).
    //   · 자아가 0이 되면 화면 전체에 글리치(ScreenGlitchFx)가 걸린다
    //   · 자아 0 상태에서 도는 5초 붕괴 타이머(PlayerController.EgoDepletedProgress) 동안, 마지막
    //     유닛이 원래 "칸이 꺼질 때" 쓰는 스무딩(_pipDisplay 기반)을 그대로 5초에 걸쳐 재생한다 —
    //     아이콘 프레임이 그 5초 동안 서서히 낮아지는 것으로 자연히 드러난다.
    //   · 자아가 다시 차면(0이 아니게 되면) 두 효과 전부 즉시 사라진다
    [Header("자아 고갈 연출 (HP 아이콘에 표시 — 폭주 중에만)")]
    public Color pipDepletedColor = new Color(0.55f, 0.56f, 0.60f); // 자아가 줄어들며 차오르는 회색(오버레이 색)

    [Header("HP 블룸 (빨간 칸만, 2026-08-10 사용자 지시)")]
    public bool hpGlowEnabled = true;
    public Color hpGlowColor = new Color(1f, 0.16f, 0.08f, 1f);
    [Range(1f, 8f)] public float hpGlowBoost = 4f;
    [Range(0f, 1f)] public float hpGlowIntensity = 1f;

    // Outline 컴포넌트는 원본 텍스처 색(빨강/적갈색)을 그대로 복제해 effectColor를 곱하기 때문에
    // 흰색을 넣어도 살짝 물든 빨간 테두리가 된다(사용자 실측 2026-08-10) — 그래서 알파 경계만 보고
    // 순수 흰색을 칠하는 전용 셰이더(Custom/UISilhouetteOutline)를 쓴다. 프레임마다 실루엣이
    // 미묘하게 달라(HP1~8 실측 확인) 정적 마스크로는 못 만들고, Base와 같은 스프라이트를 매 프레임
    // 동기화해서 그 알파 경계를 실시간으로 검사한다.
    [Header("HP 아웃라인 (흰색, 2026-08-10 사용자 지시)")]
    public bool hpOutlineEnabled = true;
    public Color hpOutlineColor = Color.white;

    // 만피(찼을 때)를 안 찼을 때와 색으로 바로 구분되게 한다(사용자 지시 2026-08-11). 평소엔 빨간
    // 아이콘 그대로 두고, 8칸이 전부 찼을 때만 금빛 틴트 + 전용(더 강한) 글로우로 바뀐다.
    [Header("HP 만피 강조 (안 찼을 때와 색으로 구분, 2026-08-11 사용자 지시)")]
    public Color hpFullTint = new Color(1f, 0.92f, 0.55f);
    public Color hpFullGlowColor = new Color(1f, 0.82f, 0.25f, 1f);
    [Range(1f, 8f)] public float hpFullGlowBoost = 6f;

    // 칸이 찰 때/깎일 때 훨씬 잘 보이게(사용자 지시 2026-08-11) — 변화 순간 아이콘에 색이 번쩍
    // 스치고 살짝 커졌다 돌아온다. 방향(회복/피격)에 따라 색만 다르고 메커니즘은 같다.
    [Header("HP 변화 이펙트 (채움/깎임을 훨씬 잘 보이게, 2026-08-11 사용자 지시)")]
    public Color hpGainFlashColor = new Color(1f, 0.98f, 0.75f);
    public Color hpLossFlashColor = new Color(1f, 0.08f, 0.05f);
    public float hpChangeFlashDecay = 6f;
    public float hpChangePunchScale = 0.3f;
    public float hpChangePunchDecay = 10f;

    // HP와 같은 파이프라인(BuildBloomPipeline이 카메라·볼륨·글로우 캔버스를 공유)이지만 색은
    // 다르게(사용자 지시 2026-08-11) — 평상시엔 아이콘 원래 색(청록)에 가깝게, 폭주 중엔 HP 블룸과
    // 같은 계열의 빨강으로 갈린다.
    // ⚠️ 재수정(2026-08-11, 사용자 지적 "저게 정상으로 보여?") — EnergyBar 프레임이 8/8(만충전)일 땐
    // 마스크가 아이콘 전체를 덮는다(HpBar와 달리 "켜진 칸"이 아이콘 전부라 마스크 면적이 훨씬 큼).
    // 거기에 색까지 흰색에 가까워(0.2,0.92,1) boost=4/intensity=1이면 블룸 블러가 아이콘 전체를
    // 뭉개 톱니 모양 실루엣(4방향 갈퀴+내부 홈, EnergyBar 원본 실측)이 매끈한 원으로 뭉개져
    // 보였다 — 셰이더가 안 먹은 게 아니라 블룸 자체가 너무 강해 디테일을 삼킨 것. HP는 같은
    // boost/intensity를 써도 글로우 색이 채도 높은 빨강/금색이라 덜 뭉개지고, "켜진 칸"만 덮는
    // 경우가 많아 마스크 면적도 작다 — 광원 쪽만 세기를 크게 낮춘다.
    [Header("광원 블룸 (2026-08-11 사용자 지시 — HP와 같은 방식, 색은 다르게)")]
    public bool energyGlowEnabled = true;
    public Color energyGlowColor = new Color(0.20f, 0.92f, 1f, 1f);
    public Color energyRampageGlowColor = new Color(1f, 0.16f, 0.08f, 1f);
    [Range(1f, 8f)] public float energyGlowBoost = 1.5f;
    [Range(0f, 1f)] public float energyGlowIntensity = 0.4f;
    // 폭주 중엔 블룸이 하트비트처럼 빠르게 페이드 인/아웃을 반복한다(사용자 지시 2026-08-11,
    // RampageHeartbeatFx.cs의 lub 박동 리듬(상승 0.05s/하강 0.10s)을 참고한 빠른 삼각파 펄스 —
    // 화면 전체가 아니라 이 블룸 하나에만 건다).
    public float energyRampagePulseRise = 0.08f;
    public float energyRampagePulseFall = 0.18f;
    [Range(0f, 1f)] public float energyRampagePulseFloor = 0.35f; // 펄스 바닥에서도 완전히 안 꺼지게

    [Header("광원 아웃라인 (흰색, HP와 동일 — 2026-08-11 사용자 지시)")]
    public bool energyOutlineEnabled = true;
    public Color energyOutlineColor = Color.white;

    // 폭주 중엔 화면 전체가 붉게 물들어(스크린 비네트 등) 켜진 칸(밝은 경고색)만 두드러지고
    // 꺼진 칸(원래 어두운 청록)은 묻혀 안 보인다는 피드백(사용자 지시 2026-08-11) — 폭주 중에만
    // 꺼진 칸을 이 어두운 색으로 눌러 대비를 살린다(Custom/UIEnergyIconBody._UnlitTint).
    [Header("광원 대비 (폭주 중 빈칸 강조, 2026-08-11 사용자 지시)")]
    public Color energyUnlitRampageTint = new Color(0.08f, 0.02f, 0.02f);

    static PlayerHudUI _instance;
    public static PlayerHudUI Instance => _instance;

    PlayerController _player;
    GameObject _root;

    // HP 아이콘 — 낮은 프레임(Base, 항상 불투명)과 다음 프레임(Ghost, 알파=frac)을 겹쳐 크로스페이드한다.
    GameObject _hpIconGO;
    Image _hpIconBase, _hpIconGhost, _hpEgoOverlay, _hpIconOutline;
    Material _hpOutlineMat, _hpEgoMat, _hpBaseMat, _hpGhostMat;
    float[] _pipDisplay;
    float _lossHoldTimer, _energyDisplay, _findTimer;
    // HP 변화 이펙트(채움/깎임 번쩍임 + 펀치 스케일) 상태 — ApplyHpIcon이 읽어 그린다.
    float _hpChangeFlash, _hpChangePunch;
    Color _hpChangeFlashColor = Color.white;
    float _energyColorLerp, _energyFlashTimer, _transcendColorLerp, _rampageColorLerp;
    int _builtPipCount, _prevLit = -1;

    // 광원 아이콘 — 본체(현재 n/8 프레임)에 직접 켜진/꺼진 칸을 재색칠하는 전용 셰이더를 쓴다
    // (EnergyBar.png가 청록 고정색이라 HP처럼 플랫 오버레이로는 빨강 경고색을 못 낸다 — 대신
    // Custom/UIEnergyIconBody가 파랑 채널로 켜진/꺼진 칸을 갈라 각각 독립적으로 재색칠한다).
    GameObject _energyIconGO;
    Image _energyIcon, _energyIconOutline;
    Material _energyBaseMat, _energyOutlineMat;
    // 광원 블룸 — 전용 카메라·볼륨·글로우 캔버스는 HP와 공유한다(BuildBloomPipeline 참고).
    Image _energyGlowIcon;
    Material _energyGlowMat;
    float _energyGlowPulsePhase; // 폭주 중 하트비트 펄스 위상(0에서 시작, rampaging 아니면 리셋)

    // HP 블룸 파이프라인(전용 카메라·볼륨·글로우 캔버스)
    Camera _hpBloomCamera;
    Image _hpGlowIcon;
    Material _hpGlowMat;

    // 프로젝트 전역에서 한 번만 로드하면 되는 리소스 — Awake마다 다시 조회하지 않는다.
    static Sprite[] _hpSpriteCache;   // 0=HP1 .. 7=HP8 (x좌표 순 정렬)
    static Sprite _hpZeroSpriteCache; // HP0
    static Texture2D _hpGlowMaskCache;
    static Sprite[] _energySpriteCache;   // 0=Energy 1/8 .. 7=Energy 8/8 (x좌표 순 정렬)
    static Sprite _energyZeroSpriteCache; // Energy 0
    static Texture2D _energyGlowMaskCache;

    // 테스트(PlayTestRunner)에서 "실제 수치"가 아니라 "화면에 그려지는 값"을 검증하기 위한 판독구.
    // 아이콘이 하나뿐이어도 내부적으로는 예전과 같은 유닛별 스무딩 배열(_pipDisplay)을 그대로 쓰므로
    // 아래 판독구들의 의미(칸 수/켜진 칸 수/칸별 표시값)는 예전과 동일하다.
    public int PipCount => _pipDisplay != null ? _pipDisplay.Length : 0;
    public float PipDisplay(int index) =>
        _pipDisplay != null && index >= 0 && index < _pipDisplay.Length ? _pipDisplay[index] : 0f;
    /// <summary>화면에 켜져 있는 것으로 보이는 칸 수(꺼지는 중인 칸은 세지 않는다).</summary>
    public int LitPipCount
    {
        get
        {
            int n = 0;
            for (int i = 0; _pipDisplay != null && i < _pipDisplay.Length; i++)
                if (_pipDisplay[i] > 0.95f) n++;
            return n;
        }
    }
    /// <summary>칸 표시값의 합 / 칸 수 — 칸 단위 표시에도 "부드럽게 줄었는가"를 볼 수 있는 집계값.</summary>
    public float HpDisplayRatio
    {
        get
        {
            if (_pipDisplay == null || _pipDisplay.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _pipDisplay.Length; i++) sum += _pipDisplay[i];
            return sum / _pipDisplay.Length;
        }
    }
    public float EnergyDisplayRatio => _energyDisplay;
    public bool HasPlayer => _player != null;

    /// <summary>에너지 바를 잠깐 붉게 점멸시킨다(일섬 게이팅 실패 등 1회성 경고 피드백).</summary>
    public void FlashEnergyBarRed(float duration = 0.24f) => _energyFlashTimer = duration;

    bool _hidden; // SetVisible(false)로 강제 숨김 중(사망 연출) — Update의 자동 재활성화를 막는다

    /// <summary>사망 연출 등에서 HUD를 강제로 숨기거나 되돌린다(_player 존재 여부와 별개 스위치).</summary>
    public void SetVisible(bool visible)
    {
        _hidden = !visible;
        if (_root != null) _root.SetActive(visible && _player != null);
    }

    // 씬에 배치하지 않아도 항상 뜨게 한다(씬 편집 없이 HUD가 붙는 유일한 방법).
    // 플레이어가 없는 씬(VfxSandbox 등)에서는 아래 Update가 HUD를 숨긴다.
    //
    // ⚠️ RuntimeInitializeOnLoadMethod는 "런타임이 시작되며 **첫 씬**을 로드할 때" 딱 한 번만 불린다
    // (공식 문서: https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html).
    // 에디터에서 Map-test를 직접 Play하면 첫 씬이 곧 Map-test라 티가 안 나지만, 빌드는 TitleScene에서
    // 시작하므로 HUD가 TitleScene에 만들어졌다가 TitleEvent의 LoadScene("Map-test")에서 씬과 함께
    // 파괴되고 다시는 만들어지지 않았다(이 오브젝트는 DontDestroyOnLoad가 아니다) — 빌드에서만
    // HUD가 갱신되지 않던 원인(사용자 리포트 2026-08-11). 그래서 씬이 바뀔 때마다 다시 만든다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // 에디터 도메인 리로드 비활성 시 중복 구독 방지
        SceneManager.sceneLoaded += OnSceneLoaded;
        GetOrCreate();
    }

    // Additive 로드는 기존 씬(과 그 HUD)이 그대로 살아 있으므로 손대지 않는다 — Single 로드일 때만
    // 이전 HUD가 파괴된 상태라 다시 만들어야 한다(GetOrCreate가 _instance 생존 여부로 알아서 거른다).
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) GetOrCreate();
    }

    public static PlayerHudUI GetOrCreate()
    {
        if (_instance != null) return _instance;
        return new GameObject("PlayerHudUI").AddComponent<PlayerHudUI>(); // Awake가 _instance를 세운다
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return;
        }
        _instance = this;
        _player = FindFirstObjectByType<PlayerController>();
        Build();
        if (_player != null) SnapToPlayer();
        else if (!Application.isPlaying) SnapToFullPreview(); // 에디터 프리뷰(플레이어 없는 씬): 풀피/풀에너지로 보여준다
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

#if UNITY_EDITOR
    // Edit 모드 실시간 프리뷰 — Inspector에서 레이아웃 값(origin·hpIconSize 등)을 바꾸면 즉시 다시 그린다.
    void OnValidate()
    {
        if (Application.isPlaying || _root == null) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Build();
            if (_player != null) SnapToPlayer();
            else SnapToFullPreview();
        };
    }
#endif

    /// <summary>
    /// 에디트 모드 프리뷰로 만든 오브젝트가 씬 파일에 저장되지 않게 막는다.
    /// [ExecuteAlways]라 에디트 모드에서도 Build()가 도는데, 그 결과물(PlayerHud 하위 전체·
    /// HpGlowCanvas·런타임 생성 머티리얼)이 전부 진짜 씬 오브젝트라 씬을 저장하면 통째로 구워졌다.
    /// 그렇게 구워진 잔재는 스크립트가 안 붙어 있어 값이 변해도 갱신되지 않는 "박제된 HUD"가 되고,
    /// 빌드에서 그게 그대로 보였다(사용자 리포트 2026-08-11 — Map-test.unity에 8/11 수정 이전
    /// 머티리얼 값 그대로 저장돼 있었다: EnergyGlow _BloomBoost 4/_Intensity 1, _LitTint 흰색).
    /// 플레이 모드에서는 씬 저장 자체가 불가능하므로 에디트 모드에서만 건다.
    /// </summary>
    static void MarkDontSaveInEditor(GameObject go)
    {
        if (go == null || Application.isPlaying) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags |= HideFlags.DontSaveInEditor;
    }

#if UNITY_EDITOR
    // hideFlags는 "우리가 만든 오브젝트"만 저장에서 뺄 수 있다 — 메인 카메라(씬에 원래 있는 진짜
    // 오브젝트)의 cameraStack에 얹어둔 블룸 카메라 참조까지는 못 막아서, 그대로 저장하면 그 자리에
    // {fileID: 0} 즉 null 항목이 씬 파일에 박힌다(실측 2026-08-11 UISandbox.unity). URP는 스택의
    // null을 매 프레임 건너뛰어야 하고, 런타임 Build()가 청소하기 전까지 남는다.
    // 저장 직전에 빼고 저장 직후에 되돌려, 에디트 모드 블룸 프리뷰는 그대로 두고 파일만 깨끗하게 한다.
    [UnityEditor.InitializeOnLoadMethod]
    static void HookSceneSave()
    {
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
    }

    // _instance에 기대지 않고 씬을 훑는다 — 에디트 모드에서는 도메인 리로드 후 Awake가 돌지 않아
    // _instance가 null인 채로 프리뷰 오브젝트만 남아 있는 상태가 실제로 나온다(실측 2026-08-11).
    static readonly System.Collections.Generic.List<UniversalAdditionalCameraData> _stashedOwners = new System.Collections.Generic.List<UniversalAdditionalCameraData>();
    static readonly System.Collections.Generic.List<Camera> _stashedCams = new System.Collections.Generic.List<Camera>();

    static void OnSceneSaving(Scene scene, string path)
    {
        if (Application.isPlaying) return;
        _stashedOwners.Clear();
        _stashedCams.Clear();
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // GetUniversalAdditionalCameraData()는 없으면 컴포넌트를 붙여버린다 — 저장 훅에서 그런
            // 부수효과를 내면 안 되므로 이미 붙어 있는 것만 본다.
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) continue;
            for (int i = data.cameraStack.Count - 1; i >= 0; i--)
            {
                var c = data.cameraStack[i];
                bool isPreview = c != null && (c.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0;
                if (c != null && !isPreview) continue; // 사용자가 직접 얹은 카메라는 건드리지 않는다
                if (isPreview) { _stashedOwners.Add(data); _stashedCams.Add(c); }
                data.cameraStack.RemoveAt(i); // null 항목은 되돌리지 않고 그냥 버린다(스택에 null은 언제나 오류)
            }
        }
    }

    static void OnSceneSaved(Scene scene)
    {
        for (int i = 0; i < _stashedCams.Count; i++)
            if (_stashedCams[i] != null && _stashedOwners[i] != null && !_stashedOwners[i].cameraStack.Contains(_stashedCams[i]))
                _stashedOwners[i].cameraStack.Add(_stashedCams[i]);
        _stashedOwners.Clear();
        _stashedCams.Clear();
    }
#endif

    void Build()
    {
        Canvas canvas = FindOverlayCanvas();
        // 우리가 만든 캔버스만 "저장 금지"로 찍는다 — 씬에 원래 있던 캔버스를 재사용한 경우에
        // 그걸 찍으면 사용자가 배치한 진짜 UI가 씬에서 통째로 사라진다.
        bool createdCanvas = canvas == null;
        if (createdCanvas) canvas = CreateOverlayCanvas();

        // 이름으로 찾아서 지운다 — _root는 private 필드라 도메인 리로드(Play 진입·스크립트 컴파일)마다
        // null로 초기화되지만, 이미 만들어둔 자식 오브젝트는 씬에 그대로 남아있어 필드 체크만으론
        // 못 잡는다(ExecuteAlways 프리뷰에서 실측 — 리로드마다 중복 생성됨).
        var existing = canvas.transform.Find("PlayerHud");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
        }

        _root = new GameObject("PlayerHud", typeof(RectTransform));
        var rt = (RectTransform)_root.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = origin;
        rt.sizeDelta = Vector2.zero;
        // 회피/처형 프롬프트가 HUD 위에 그려지도록 맨 뒤로 보낸다(같은 캔버스를 공유할 수 있다).
        rt.SetAsFirstSibling();

        BuildHpIcon(_player != null ? _player.maxHealth : 5);
        BuildEnergyIcon();
        BuildBloomPipeline();

        // 자식이 전부 만들어진 뒤에 찍는다(하위를 훑어 내려가므로 순서가 중요하다).
        if (createdCanvas) MarkDontSaveInEditor(canvas.gameObject);
        MarkDontSaveInEditor(_root);
    }

    /// <summary>
    /// 체력 아이콘을 만든다. 최대 칸 수가 바뀌면(세이브 로드 등) 통째로 다시 만든다 — 시각적으로는
    /// 칸이 없어졌지만, "몇 유닛짜리 스무딩 배열을 쓸지"는 그대로 maxHealth를 따른다.
    /// </summary>
    void BuildHpIcon(int count)
    {
        count = Mathf.Max(1, count);
        if (_hpIconGO != null) { if (Application.isPlaying) Destroy(_hpIconGO); else DestroyImmediate(_hpIconGO); }
        if (_hpOutlineMat != null) { if (Application.isPlaying) Destroy(_hpOutlineMat); else DestroyImmediate(_hpOutlineMat); _hpOutlineMat = null; }
        if (_hpEgoMat != null) { if (Application.isPlaying) Destroy(_hpEgoMat); else DestroyImmediate(_hpEgoMat); _hpEgoMat = null; }
        if (_hpBaseMat != null) { if (Application.isPlaying) Destroy(_hpBaseMat); else DestroyImmediate(_hpBaseMat); _hpBaseMat = null; }
        if (_hpGhostMat != null) { if (Application.isPlaying) Destroy(_hpGhostMat); else DestroyImmediate(_hpGhostMat); _hpGhostMat = null; }

        LoadHpAssets();

        _hpIconGO = new GameObject("HpIcon", typeof(RectTransform));
        var iconRt = (RectTransform)_hpIconGO.transform;
        iconRt.SetParent(_root.transform, false);
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0f, 1f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = hpIconSize;

        // 실루엣 밖 1px 링에만 흰색을 칠하는 아웃라인 — 맨 뒤에 그려 넣는다(실루엣 안쪽은 셰이더가
        // 투명으로 비워서 Base/Ghost와 겹치지 않는다, 그리기 순서는 상관없지만 관례상 맨 뒤).
        var outlineImg = AddImage(_hpIconGO.transform, "Outline", hpIconSize, Vector2.zero, Color.white);
        _hpIconOutline = outlineImg.GetComponent<Image>();
        Shader outlineShader = Shader.Find("Custom/UISilhouetteOutline");
        if (outlineShader != null)
        {
            _hpOutlineMat = new Material(outlineShader);
            _hpOutlineMat.SetColor("_OutlineColor", hpOutlineColor);
            _hpIconOutline.material = _hpOutlineMat;
        }
        _hpIconOutline.enabled = false;

        // 세 레이어가 전부 같은 자리·크기로 완전히 겹친다(순서 = 그리기 순서). Base/Ghost는 전용
        // 셰이더(Custom/UIHpIconBody)를 쓴다 — 평소엔 UI/Default와 동일하게 그리지만, 자아 0으로
        // HP가 깎일 때만(_HideUnlit) 이미 깎인 칸은 투명, 아직 남은 칸은 회색으로 바뀐다(사용자
        // 지시 2026-08-11 — 처음엔 남은 칸도 원래 빨강 그대로 뒀다가, "아직 남아있는 HP 부분은
        // 회색으로" 피드백을 받아 _GrayTint를 추가했다).
        Shader bodyShader = Shader.Find("Custom/UIHpIconBody");

        var baseImg = AddImage(_hpIconGO.transform, "Base", hpIconSize, Vector2.zero, Color.white);
        _hpIconBase = baseImg.GetComponent<Image>();
        _hpIconBase.sprite = _hpZeroSpriteCache;
        if (bodyShader != null)
        {
            _hpBaseMat = new Material(bodyShader);
            _hpBaseMat.SetColor("_GrayTint", pipDepletedColor);
            _hpBaseMat.SetColor("_LitTint", hpLitTint);
            _hpIconBase.material = _hpBaseMat;
        }

        var ghostImg = AddImage(_hpIconGO.transform, "Ghost", hpIconSize, Vector2.zero, Color.white);
        _hpIconGhost = ghostImg.GetComponent<Image>();
        _hpIconGhost.enabled = false;
        if (bodyShader != null)
        {
            _hpGhostMat = new Material(bodyShader);
            _hpGhostMat.SetColor("_GrayTint", pipDepletedColor);
            _hpGhostMat.SetColor("_LitTint", hpLitTint);
            _hpIconGhost.material = _hpGhostMat;
        }

        // ⚠️ Image.Type.Filled + 흰색 사각형 스프라이트(WhiteSprite)를 썼던 예전 방식은 "칸 5개"
        // 시절엔 칸 자체가 정사각형이라 문제가 없었지만, 지금은 아이콘이 꽃 모양이라 사각형 Fill이
        // 실루엣을 무시하고 네모난 회색 막대로 덮어버렸다(사용자 스크린샷 2026-08-10). 아이콘의
        // 실제 알파를 실루엣 마스크로 쓰는 전용 셰이더(Custom/UISilhouetteFill)로 바꾼다 — Base와
        // 같은 스프라이트를 매 프레임 동기화하고, Fill 진행도는 셰이더의 _FillAmount로 넘긴다.
        var egoImg = AddImage(_hpIconGO.transform, "EgoGray", hpIconSize, Vector2.zero, Color.white);
        _hpEgoOverlay = egoImg.GetComponent<Image>();
        Shader egoShader = Shader.Find("Custom/UISilhouetteFill");
        if (egoShader != null)
        {
            _hpEgoMat = new Material(egoShader);
            _hpEgoMat.SetColor("_Color", pipDepletedColor);
            _hpEgoMat.SetFloat("_FillAmount", 0f);
            _hpEgoOverlay.material = _hpEgoMat;
        }
        _hpEgoOverlay.enabled = false;

        // 칸 수만 바뀌었을 땐 남아 있던 표시값을 이어받아 화면이 튀지 않게 한다.
        var previous = _pipDisplay;
        _pipDisplay = new float[count];
        for (int i = 0; i < count; i++)
            _pipDisplay[i] = previous != null && i < previous.Length ? previous[i] : 0f;

        _builtPipCount = count;
    }

    /// <summary>
    /// 빨간 칸/청록 칸만 블룸시키는 공유 파이프라인(HP+광원) — 전용 레이어(HPBloom)만 컬링하는
    /// Overlay 카메라를 메인 카메라 스택에 추가하고, 그 카메라의 Volume Mask에만 걸리는 전용 Bloom
    /// 볼륨을 단다. Screen Space Overlay 캔버스(HUD 본체)는 URP 포스트프로세싱을 안 받으므로, 글로우만
    /// 별도 Screen Space - Camera 캔버스(같은 좌표계·같은 origin)로 그 카메라에 붙인다. 카메라·볼륨·
    /// 캔버스는 HP·광원 아이콘 둘 다 공유하고(둘 다 같은 레이어일 뿐이라 하나로 충분), 아이콘별
    /// on/off(hpGlowEnabled/energyGlowEnabled)만 독립적이다.
    /// </summary>
    void BuildBloomPipeline()
    {
        _hpBloomCamera = null;
        _hpGlowIcon = null;
        _energyGlowIcon = null;
        if (_hpGlowMat != null) { if (Application.isPlaying) Destroy(_hpGlowMat); else DestroyImmediate(_hpGlowMat); _hpGlowMat = null; }
        if (_energyGlowMat != null) { if (Application.isPlaying) Destroy(_energyGlowMat); else DestroyImmediate(_energyGlowMat); _energyGlowMat = null; }
        if (!hpGlowEnabled && !energyGlowEnabled) return;

        int layer = LayerMask.NameToLayer("HPBloom");
        if (layer < 0) return; // 레이어가 없는 프로젝트에서도 HP·광원 아이콘 자체는 정상 동작해야 한다

        Camera baseCam = Camera.main;
        if (baseCam == null) return; // 붙일 메인 카메라가 없으면 블룸을 걸 대상이 없다

        var baseData = baseCam.GetUniversalAdditionalCameraData();
        // Build()가 반복 호출될 때마다(에디터 프리뷰 등) 이전 회차의 카메라가 파괴되며 남긴
        // null 항목을 정리한다 — 안 지우면 스택이 매 리빌드마다 하나씩 늘어난다.
        baseData.cameraStack.RemoveAll(c => c == null);

        var camGO = new GameObject("HpBloomCamera", typeof(Camera));
        camGO.transform.SetParent(_root.transform, false);
        camGO.layer = layer;
        _hpBloomCamera = camGO.GetComponent<Camera>();
        _hpBloomCamera.clearFlags = CameraClearFlags.Depth;
        _hpBloomCamera.cullingMask = 1 << layer;
        _hpBloomCamera.orthographic = true;
        _hpBloomCamera.nearClipPlane = 0.1f;
        _hpBloomCamera.farClipPlane = 500f;

        var hpData = _hpBloomCamera.GetUniversalAdditionalCameraData();
        hpData.renderType = CameraRenderType.Overlay;
        hpData.renderPostProcessing = true;
        // ⚠️ 여기에 HP 전용 마스크(1 << layer)를 걸면 **화면 전체 블룸이 HP바용 프로파일로 바뀐다.**
        //    URP는 카메라 스택의 포스트프로세싱을 "마지막 카메라"에서 한 번만 적용하는데, 이 카메라가
        //    바로 그 마지막이기 때문이다(URP 문서: "스택의 마지막 카메라에만 포스트를 적용하라").
        //    실측(빌드 로그 2026-08-12): 씬의 IlseomBloomProfile(threshold 1.15 / intensity 2.2 /
        //    scatter 0.7) 대신 HpBarBloomProfile(1.05 / 0.7 / 0.25)이 화면 전체에 걸려, 세계·UI·
        //    플레이어 블룸이 통째로 3배 약해져 있었다.
        //    베이스 카메라와 같은 마스크를 써서 씬이 의도한 블룸이 그대로 화면에 적용되게 한다
        //    (사용자 확정 2026-08-12: HP 전용 블룸 튜닝은 포기).
        hpData.volumeLayerMask = baseData.volumeLayerMask;
        baseData.cameraStack.Add(_hpBloomCamera);

        // ⚠️ 현재 이 볼륨은 **동작하지 않는다**(2026-08-12). 위에서 volumeLayerMask를 베이스 카메라와
        //    같게 바꾼 뒤로 HPBloom 레이어가 마스크에 없어서다. HP 전용 블룸 튜닝을 되살리려면 이 카메라를
        //    스택에서 빼고 별도 RenderTexture로 합성해야 한다 — 마스크만 되돌리면 화면 전체 블룸이
        //    다시 이 프로파일에 하이재킹된다. 정리(삭제)는 별도 승인 후에 한다.
        var profile = Resources.Load<VolumeProfile>("VFX/HpBarBloomProfile");
        if (profile != null)
        {
            var volGO = new GameObject("HpBloomVolume", typeof(Volume));
            volGO.transform.SetParent(_root.transform, false);
            volGO.layer = layer;
            var vol = volGO.GetComponent<Volume>();
            vol.isGlobal = true;
            vol.weight = 1f;
            vol.sharedProfile = profile;
        }

        // ⚠️ 씬 루트에 독립 캔버스로 만든다 — 절대 _root(HUD 본체가 붙어있는 기존 Overlay 캔버스)
        // 밑에 중첩시키면 안 된다. Unity의 Nested Canvas는 renderMode·worldCamera가 자식 것이 아니라
        // "루트 캔버스"에 적용된다(실측: 중첩시켰더니 씬의 기존 Overlay 캔버스 자체가 통째로
        // ScreenSpaceCamera로 바뀌어 worldCamera가 이 카메라를 가리키게 되면서 HUD 전체가 사라졌다).
        var existingGlow = GameObject.Find("HpGlowCanvas");
        if (existingGlow != null) { if (Application.isPlaying) Destroy(existingGlow); else DestroyImmediate(existingGlow); }
        var glowCanvasGO = new GameObject("HpGlowCanvas", typeof(RectTransform));
        glowCanvasGO.layer = layer;
        var glowCanvas = glowCanvasGO.AddComponent<Canvas>();
        glowCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        glowCanvas.worldCamera = _hpBloomCamera;
        glowCanvas.planeDistance = 100f;
        var glowScaler = glowCanvasGO.AddComponent<CanvasScaler>();
        glowScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        glowScaler.referenceResolution = new Vector2(1920, 1080);

        if (hpGlowEnabled)
        {
            var glowIconGO = new GameObject("HpGlowIcon", typeof(RectTransform), typeof(Image));
            glowIconGO.layer = layer;
            var glowRt = (RectTransform)glowIconGO.transform;
            glowRt.SetParent(glowCanvasGO.transform, false);
            glowRt.anchorMin = glowRt.anchorMax = glowRt.pivot = new Vector2(0f, 1f);
            glowRt.anchoredPosition = origin; // 본체 HUD와 같은 원점 → 화면상 같은 자리에 겹친다
            glowRt.sizeDelta = hpIconSize;
            _hpGlowIcon = glowIconGO.GetComponent<Image>();
            _hpGlowIcon.raycastTarget = false;

            Shader glowShader = Shader.Find("Custom/UIHpGlow");
            if (glowShader != null)
            {
                _hpGlowMat = new Material(glowShader);
                _hpGlowIcon.material = _hpGlowMat;
                if (_hpGlowMaskCache != null) _hpGlowMat.SetTexture("_EmissionMask", _hpGlowMaskCache);
                _hpGlowMat.SetColor("_Color", hpGlowColor);
                _hpGlowMat.SetFloat("_BloomBoost", hpGlowBoost);
                _hpGlowMat.SetFloat("_Intensity", hpGlowIntensity);
            }
        }

        if (energyGlowEnabled)
        {
            var glowIconGO = new GameObject("EnergyGlowIcon", typeof(RectTransform), typeof(Image));
            glowIconGO.layer = layer;
            var glowRt = (RectTransform)glowIconGO.transform;
            glowRt.SetParent(glowCanvasGO.transform, false);
            glowRt.anchorMin = glowRt.anchorMax = glowRt.pivot = new Vector2(0f, 1f);
            // 본체 광원 아이콘과 같은 오프셋(HP 아이콘 바로 아래)으로 화면상 같은 자리에 겹친다.
            glowRt.anchoredPosition = origin + new Vector2(0f, -(hpIconSize.y + barGap));
            glowRt.sizeDelta = hpIconSize;
            _energyGlowIcon = glowIconGO.GetComponent<Image>();
            _energyGlowIcon.raycastTarget = false;

            Shader glowShader = Shader.Find("Custom/UIHpGlow"); // 범용 셰이더 — 마스크·색만 다르게 재사용
            if (glowShader != null)
            {
                _energyGlowMat = new Material(glowShader);
                _energyGlowIcon.material = _energyGlowMat;
                if (_energyGlowMaskCache != null) _energyGlowMat.SetTexture("_EmissionMask", _energyGlowMaskCache);
                _energyGlowMat.SetColor("_Color", energyGlowColor);
                _energyGlowMat.SetFloat("_BloomBoost", energyGlowBoost);
                _energyGlowMat.SetFloat("_Intensity", energyGlowIntensity);
            }
        }

        MarkDontSaveInEditor(glowCanvasGO); // 씬 루트에 만든 캔버스라 그대로 두면 씬에 저장된다
    }

    /// <summary>
    /// 광원 아이콘을 만든다 — HP 아이콘과 같은 크기, 그 아래(barGap만큼 띄워서) 배치한다.
    /// HP와 같은 순서(Outline 맨 뒤 → Base)를 쓴다. 색 재계산은 전용 셰이더(Custom/UIEnergyIconBody)
    /// 안에서 켜진/꺼진 칸을 갈라 각각 처리하므로 별도 오버레이 레이어가 필요 없다.
    /// </summary>
    void BuildEnergyIcon()
    {
        if (_energyIconGO != null) { if (Application.isPlaying) Destroy(_energyIconGO); else DestroyImmediate(_energyIconGO); }
        if (_energyBaseMat != null) { if (Application.isPlaying) Destroy(_energyBaseMat); else DestroyImmediate(_energyBaseMat); _energyBaseMat = null; }
        if (_energyOutlineMat != null) { if (Application.isPlaying) Destroy(_energyOutlineMat); else DestroyImmediate(_energyOutlineMat); _energyOutlineMat = null; }

        LoadEnergyAssets();

        _energyIconGO = new GameObject("EnergyIcon", typeof(RectTransform));
        var iconRt = (RectTransform)_energyIconGO.transform;
        iconRt.SetParent(_root.transform, false);
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0f, 1f);
        iconRt.anchoredPosition = new Vector2(0f, -(hpIconSize.y + barGap));
        iconRt.sizeDelta = hpIconSize;

        var outlineImg = AddImage(_energyIconGO.transform, "Outline", hpIconSize, Vector2.zero, Color.white);
        _energyIconOutline = outlineImg.GetComponent<Image>();
        Shader outlineShader = Shader.Find("Custom/UISilhouetteOutline");
        if (outlineShader != null)
        {
            _energyOutlineMat = new Material(outlineShader);
            _energyOutlineMat.SetColor("_OutlineColor", energyOutlineColor);
            _energyIconOutline.material = _energyOutlineMat;
        }
        _energyIconOutline.enabled = false;

        var baseImg = AddImage(_energyIconGO.transform, "Base", hpIconSize, Vector2.zero, Color.white);
        _energyIcon = baseImg.GetComponent<Image>();
        _energyIcon.sprite = _energyZeroSpriteCache;
        Shader bodyShader = Shader.Find("Custom/UIEnergyIconBody");
        if (bodyShader != null)
        {
            _energyBaseMat = new Material(bodyShader);
            _energyIcon.material = _energyBaseMat;
        }
    }

    RectTransform AddImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        // 피봇을 좌상단에 두면 sizeDelta.x만 줄여도 바가 왼쪽 기준으로 줄어든다
        // (Image.type=Filled는 스프라이트가 필요해서, 스프라이트 없는 단색 바에는 폭 조절이 더 단순하다).
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    Canvas FindOverlayCanvas()
    {
        // 비활성 캔버스는 재사용하지 않는다(그 밑에 붙으면 같이 숨는다) — DodgeUI와 같은 규칙.
        Canvas best = null;
        foreach (var cv in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cv.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (best == null || cv.sortingOrder < best.sortingOrder) best = cv;
        }
        return best;
    }

    Canvas CreateOverlayCanvas()
    {
        var go = new GameObject("PlayerHudCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    void Update()
    {
        if (!Application.isPlaying) return; // 에디터 프리뷰는 정적 배치만 보여준다(애니메이션은 Play 모드 전용)

        // 히트스톱(timeScale=0)·회피 슬로우모션 중에도 게이지는 정상 속도로 움직여야 하므로 unscaled.
        float dt = Mathf.Min(Time.unscaledDeltaTime, maxSmoothDelta);

        if (_player == null)
        {
            _findTimer -= dt;
            if (_findTimer <= 0f)
            {
                _findTimer = 0.5f; // 매 프레임 씬 탐색은 비싸다
                _player = FindFirstObjectByType<PlayerController>();
                if (_player != null) SnapToPlayer();
            }
        }

        bool shouldShow = _player != null && !_hidden;
        if (_root != null && _root.activeSelf != shouldShow) _root.SetActive(shouldShow);
        if (_player == null) return;

        // 최대 칸 수는 세이브 불러오기로도 바뀔 수 있다 → 바뀌면 줄을 다시 만든다.
        if (_player.maxHealth != _builtPipCount) BuildHpIcon(_player.maxHealth);

        int lit = Mathf.Clamp(_player.currentHealth, 0, _pipDisplay.Length);

        // 칸을 잃은 "그 순간"에만 지연을 건다(꺼지는 중인 상태를 조건으로 삼으면 지연이 영원히 갱신된다).
        if (_prevLit >= 0 && lit < _prevLit) _lossHoldTimer = hpLossDelay;
        // 칸이 바뀐 "그 순간"에 번쩍임+펀치 스케일을 터뜨린다(회복·피격 공통, 색만 다르다).
        if (_prevLit >= 0 && lit != _prevLit)
        {
            _hpChangeFlash = 1f;
            _hpChangePunch = 1f;
            _hpChangeFlashColor = lit > _prevLit ? hpGainFlashColor : hpLossFlashColor;
        }
        _prevLit = lit;
        if (_lossHoldTimer > 0f) _lossHoldTimer -= dt;
        _hpChangeFlash = Mathf.MoveTowards(_hpChangeFlash, 0f, hpChangeFlashDecay * dt);
        _hpChangePunch = Mathf.MoveTowards(_hpChangePunch, 0f, hpChangePunchDecay * dt);

        for (int i = 0; i < _pipDisplay.Length; i++)
        {
            float target = i < lit ? 1f : 0f;
            // 회복(꺼진 칸이 켜지는 것)은 지연 없이 바로 차오른다.
            if (target < _pipDisplay[i] && _lossHoldTimer > 0f) continue;
            _pipDisplay[i] = Smooth(_pipDisplay[i], target, hpLerpSpeed, dt);
        }

        bool rampaging = _player.IsRampaging;
        bool transcending = _player.IsTranscending; // T-4: 폭주 재스케일 장치를 반대로 쓴다
        // 폭주 중엔 "회복치 100"을 만들어야 한다(사용자 지시 2026-08-02) — 실제로 필요한 회복량은
        // maxEnergy가 아니라 RampageExitEnergy(25%)뿐이라, 그 값을 분모로 써서 바가 처음부터 끝까지
        // 꽉 차는 것처럼 보이게 한다. 그대로 두면 폭주 내내 바가 최대 25%까지만 차 회복이 안 되는
        // 것처럼 보인다(사용자 스크린샷 2026-08-02).
        // 초월 중엔 반대로 "남은 초월 시간"이 100%에서 0%로 완전히 빠지는 것으로 재스케일한다
        // (TranscendExitEnergy=70을 바닥으로 삼아, 100→70 드레인이 바 전체를 쓴다).
        float energyTarget = rampaging
            ? Ratio(_player.currentEnergy, _player.RampageExitEnergy)
            : transcending
                ? Ratio(_player.currentEnergy - _player.TranscendExitEnergy, _player.maxEnergy - _player.TranscendExitEnergy)
                : Ratio(_player.currentEnergy, _player.maxEnergy);
        _energyDisplay = Smooth(_energyDisplay, energyTarget, energyLerpSpeed, dt);

        // 저에너지 경고(1/8 이하, 사용자 지시 2026-08-11로 10%→1/8) + 1회성 점멸(A-2 일섬 게이팅
        // 실패 피드백)이 같은 색 전환 장치를 공유한다. 폭주·초월 중엔 위 재스케일과 뜻이 달라지므로
        // (폭주는 항상 낮게, 초월은 항상 높게 나옴) 경고색 전환은 평상시에만 건다(폭주 중 끄던 것과
        // 같은 이유 — PlayerHudUI.cs:324 선례). 폭주 진입 문턱과 값을 공유(PlayerController.RampageEnterEnergy)
        // — 둘 다 "1/8" 하나의 개념이라는 사용자 지시를 코드에서도 한 값으로 묶는다.
        if (_energyFlashTimer > 0f) _energyFlashTimer -= dt;
        bool lowEnergy = !rampaging && !transcending && _player.maxEnergy > 0 && _player.currentEnergy <= _player.RampageEnterEnergy;
        _energyColorLerp = Smooth(_energyColorLerp, (lowEnergy || _energyFlashTimer > 0f) ? 1f : 0f, energyColorLerpSpeed, dt);
        _transcendColorLerp = Smooth(_transcendColorLerp, transcending ? 1f : 0f, transcendColorLerpSpeed, dt);
        _rampageColorLerp = Smooth(_rampageColorLerp, rampaging ? 1f : 0f, rampageColorLerpSpeed, dt);

        // 자아 상태 → HP 아이콘 연출(자아는 폭주 중에만 의미 있는 값이라 전부 폭주 게이트를 공유한다).
        bool egoDepleted = rampaging && _player.currentEgo <= 0;
        float egoRatio = rampaging ? Ratio(_player.currentEgo, _player.maxEgo) : 1f;
        // 자아가 줄어드는 만큼 아이콘 위에 회색이 위→아래로 차오른다. egoRatio가 폭주 아닐 때 1로
        // 고정되므로 별도 게이트 없이도 0이 나온다.
        float grayFill = Mathf.Clamp01(1f - egoRatio);

        // 자아 0 붕괴 — 마지막 유닛의 표시값을 5초 붕괴 진행률로 직접 덮어써서, 원래 "칸이 꺼질 때" 쓰는
        // 스무딩(ApplyHpIcon이 _pipDisplay 합으로 그리는 그 연출)을 5초짜리로 늘려 재생한다. 이 유닛은
        // 아직 살아 있어 위 스무딩 루프가 target=1로 계속 끌어올리려 하므로, 그 다음에 값을 덮어써야 한다.
        // ⚠️ 여기에 Smooth()를 한 번 얹었다가 되돌렸다(사용자 피드백 2026-08-02) — EgoDepletedProgress는
        // PlayerController가 실시간(uncapped) deltaTime으로 이미 선형으로 채운 값인데, Smooth()를 쓰면
        // 이 파일의 dt가 maxSmoothDelta(0.05s)에 상한 걸려 있어(위 주석 참고) 실제 프레임 간격이 그보다
        // 크면 목표를 못 따라잡고 계속 뒤처지다가 틱이 나갈 때 "덜 줄어든 채로 갑자기 사라지는" 것처럼
        // 보였다. 원본 값 자체가 이미 매끈한 선형이라 그대로 대입하면 충분하다.
        if (egoDepleted && lit > 0) _pipDisplay[lit - 1] = 1f - _player.EgoDepletedProgress;

        // 폭주 중엔 진행시키고, 아니면 리셋 — 폭주가 다시 시작될 때마다 항상 상승부터 시작한다.
        if (rampaging) _energyGlowPulsePhase += dt; else _energyGlowPulsePhase = 0f;

        ApplyHpIcon(grayFill);
        ApplyEnergyIcon(EnergyTint(), rampaging);
    }

    /// <summary>평상시(흰)→저에너지/폭주(경고색) 색 전환 — Update/SnapToPlayer가 공유하는 계산.</summary>
    Color EnergyTint()
    {
        Color blended = Color.Lerp(Color.Lerp(energyColor, energyLowColor, _energyColorLerp), transcendColor, _transcendColorLerp);
        return Color.Lerp(blended, rampageColor, _rampageColorLerp);
    }

    /// <summary>보간 없이 현재 수치로 맞춘다(HUD 생성 직후 칸이 0에서 차오르지 않도록).</summary>
    public void SnapToPlayer()
    {
        if (_player == null || _pipDisplay == null) return;
        if (_player.maxHealth != _builtPipCount) BuildHpIcon(_player.maxHealth);

        int lit = Mathf.Clamp(_player.currentHealth, 0, _pipDisplay.Length);
        for (int i = 0; i < _pipDisplay.Length; i++) _pipDisplay[i] = i < lit ? 1f : 0f;
        bool rampaging = _player.IsRampaging;
        bool transcending = _player.IsTranscending;
        _energyDisplay = rampaging
            ? Ratio(_player.currentEnergy, _player.RampageExitEnergy)
            : transcending
                ? Ratio(_player.currentEnergy - _player.TranscendExitEnergy, _player.maxEnergy - _player.TranscendExitEnergy)
                : Ratio(_player.currentEnergy, _player.maxEnergy);
        _energyFlashTimer = 0f;
        _energyColorLerp = (!rampaging && !transcending && _player.maxEnergy > 0 && _player.currentEnergy <= _player.RampageEnterEnergy) ? 1f : 0f;
        _transcendColorLerp = transcending ? 1f : 0f;
        _rampageColorLerp = rampaging ? 1f : 0f;
        _lossHoldTimer = 0f;
        _prevLit = lit;
        _energyGlowPulsePhase = 0f;

        ApplyHpIcon(0f);
        ApplyEnergyIcon(EnergyTint(), rampaging);
    }

    /// <summary>에디터 프리뷰용(UISandbox 등 플레이어 없는 씬) — 칸·에너지를 꽉 찬 상태로 보여준다.</summary>
    void SnapToFullPreview()
    {
        if (_pipDisplay == null) return;
        for (int i = 0; i < _pipDisplay.Length; i++) _pipDisplay[i] = 1f;
        _energyDisplay = 1f;
        _energyColorLerp = _transcendColorLerp = _rampageColorLerp = 0f;
        _energyGlowPulsePhase = 0f;
        ApplyHpIcon(0f);
        ApplyEnergyIcon(EnergyTint(), false);
    }

    /// <summary>
    /// _pipDisplay 합(유닛별 스무딩 값의 총합, 0~칸 수)을 프레임 인덱스로 바꿔 그린다.
    /// floor(sum) = Base 프레임(더 어두운 쪽, 항상 불투명), floor+1 = Ghost 프레임(더 밝은 쪽,
    /// 알파=소수부). 예: sum이 4.9→4.0으로 줄어드는 동안 Base=HP4·Ghost=HP5(알파 0.9→0)로
    /// 서서히 옅어져 "칸이 빨강에서 검정으로 전환"되는 것처럼 보이고, 반대로 늘어나는 동안엔
    /// Ghost(더 밝은 프레임)가 0→1로 짙어지며 "칸이 채워지는" 것처럼 보인다. 한 프레임짜리
    /// 크로스페이드라 sum이 여러 정수를 연속으로 가로질러도(한 번에 2~3칸) 자연히 프레임을
    /// 훑고 지나간다.
    /// </summary>
    void ApplyHpIcon(float grayFill)
    {
        if (_hpIconBase == null || _pipDisplay == null) return;

        float sum = 0f;
        for (int i = 0; i < _pipDisplay.Length; i++) sum += _pipDisplay[i];
        float hp = Mathf.Clamp(sum, 0f, _pipDisplay.Length);
        int floorHp = Mathf.Clamp(Mathf.FloorToInt(hp), 0, 8);
        float frac = floorHp >= 8 ? 0f : hp - floorHp;

        Sprite baseSprite = floorHp <= 0 ? _hpZeroSpriteCache : GetHpSprite(floorHp - 1);
        Sprite ghostSprite = frac > 0.001f ? GetHpSprite(floorHp) : null;

        // 만피(안 찼을 때와 색으로 구분) + 변화 번쩍임(채움/깎임을 훨씬 잘 보이게) — 둘 다 사용자
        // 지시 2026-08-11. 만피 틴트가 바탕이고, 그 위에 변화 번쩍임 색이 순간적으로 섞여 든다.
        bool isFull = _pipDisplay.Length > 0 && hp >= _pipDisplay.Length - 0.001f;
        Color hpTint = Color.Lerp(isFull ? hpFullTint : Color.white, _hpChangeFlashColor, _hpChangeFlash);
        if (_hpIconGO != null)
            _hpIconGO.transform.localScale = Vector3.one * (1f + hpChangePunchScale * _hpChangePunch);

        _hpIconBase.sprite = baseSprite;
        _hpIconBase.color = hpTint;

        // 자아가 0이 되어 HP가 깎일 때만 "꺼진 칸"을 투명하게 비운다(사용자 지시 2026-08-11) —
        // 평소(폭주 아님 또는 자아 남아있음) 깎이는 칸은 원래대로 어두운 적갈색으로 보인다.
        bool hideUnlit = _player != null && _player.IsRampaging && _player.currentEgo <= 0;
        if (_hpBaseMat != null) _hpBaseMat.SetFloat("_HideUnlit", hideUnlit ? 1f : 0f);
        if (_hpGhostMat != null) _hpGhostMat.SetFloat("_HideUnlit", hideUnlit ? 1f : 0f);

        if (_hpIconOutline != null)
        {
            bool showOutline = hpOutlineEnabled && baseSprite != null;
            _hpIconOutline.enabled = showOutline;
            if (showOutline)
            {
                _hpIconOutline.sprite = baseSprite;
                // ⚠️ CanvasRenderer는 SpriteRenderer와 달리 _MainTex_TexelSize를 자동으로 채우지
                // 않는다(실측 2026-08-10: 152x19 텍스처인데도 기본값 (1,1,1,1)로 고정돼 있었다) —
                // 셰이더의 이웃 텍셀 오프셋이 통째로 어긋나 아웃라인이 전혀 안 그려졌다. 직접 채운다.
                if (_hpOutlineMat != null && baseSprite.texture != null)
                {
                    var tex = baseSprite.texture;
                    _hpOutlineMat.SetVector("_MainTex_TexelSize",
                        new Vector4(1f / tex.width, 1f / tex.height, tex.width, tex.height));
                }
            }
        }

        if (_hpIconGhost != null)
        {
            _hpIconGhost.enabled = ghostSprite != null;
            if (ghostSprite != null)
            {
                _hpIconGhost.sprite = ghostSprite;
                var c = hpTint;
                c.a = frac;
                _hpIconGhost.color = c;
            }
        }

        if (_hpEgoOverlay != null)
        {
            // 자아가 0이 되면 grayFill이 늘 1(전체 덮음)로 고정된다 — 그 상태로 계속 켜 두면 이
            // 불투명한 회색이 Base/Ghost보다 위에 그려져 hideUnlit로 비워낸 "꺼진 칸" 투명 처리가
            // 화면에 전혀 안 보인다(사용자 스크린샷 2026-08-11 "그대로입니다"). 자아 0 붕괴 구간
            // (hideUnlit)에서는 회색 오버레이 자체를 끄고 Base/Ghost의 투명 처리가 그대로 보이게 한다.
            bool showEgo = grayFill > 0.0001f && hp > 0.0001f && baseSprite != null && !hideUnlit;
            _hpEgoOverlay.enabled = showEgo;
            if (showEgo && _hpEgoMat != null)
            {
                _hpEgoOverlay.sprite = baseSprite; // 아이콘 실루엣과 같은 스프라이트를 셰이더가 알파 마스크로 쓴다
                _hpEgoMat.SetFloat("_FillAmount", grayFill);
            }
        }

        if (_hpGlowIcon != null)
        {
            bool showGlow = baseSprite != null && baseSprite != _hpZeroSpriteCache;
            _hpGlowIcon.enabled = showGlow;
            if (showGlow)
            {
                _hpGlowIcon.sprite = baseSprite;
                if (_hpGlowMat != null)
                {
                    Color glowBase = isFull ? hpFullGlowColor : hpGlowColor;
                    float boostBase = isFull ? hpFullGlowBoost : hpGlowBoost;
                    _hpGlowMat.SetColor("_Color", Color.Lerp(glowBase, _hpChangeFlashColor, _hpChangeFlash * 0.8f));
                    _hpGlowMat.SetFloat("_BloomBoost", Mathf.Lerp(boostBase, boostBase * 1.6f, _hpChangeFlash));
                    _hpGlowMat.SetFloat("_Intensity", hpGlowIntensity);
                }
            }
        }
    }

    /// <summary>
    /// 광원 비율(_energyDisplay, 0~1 — 폭주·초월 재스케일까지 이미 반영된 값)을 8분할 스프라이트로
    /// 매핑한다. n이라면 n/8 이상일 때 프레임 n으로 스위치하는 계단 함수(사용자 지시 2026-08-11) —
    /// HP처럼 프레임 사이를 알파 크로스페이드하지 않는다. 대신 _energyDisplay 자체가 이미 매 프레임
    /// 지수 스무딩되므로(Update의 Smooth 호출), 문턱을 넘는 순간 프레임이 바뀌는 것도 부드럽게
    /// 이어진다. 폭주/초월 상태에서도 이 값은 그대로 재사용된다 — 그 상태에서 이미 재스케일된 비율을
    /// 만드는 쪽(Update의 energyTarget 계산)은 이 함수와 별개이고, 여기선 결과 비율만 받는다.
    /// </summary>
    void ApplyEnergyIcon(Color tint, bool rampaging)
    {
        if (_energyIcon == null) return;

        int n = Mathf.Clamp(Mathf.FloorToInt(_energyDisplay * 8f + 0.0001f), 0, 8);
        Sprite sprite = n <= 0 ? _energyZeroSpriteCache : GetEnergySprite(n - 1);
        _energyIcon.sprite = sprite;

        if (_energyBaseMat != null)
        {
            // 켜진 칸은 항상 이 색으로 완전히 덮어 그린다(평소 청록에 가깝게, 저에너지/폭주 시 빨강).
            _energyBaseMat.SetColor("_LitTint", tint);
            // 꺼진 칸은 평소엔 원본(어두운 청록) 그대로(알파 0 = pass-through), 폭주 중에만 대비용
            // 어두운 색으로 덮는다(사용자 지시 2026-08-11 "폭주 상태에서 빈칸이 잘 안보이니까").
            Color unlitOverride = energyUnlitRampageTint;
            unlitOverride.a = _rampageColorLerp;
            _energyBaseMat.SetColor("_UnlitTint", unlitOverride);
        }

        if (_energyIconOutline != null)
        {
            bool showOutline = energyOutlineEnabled && sprite != null;
            _energyIconOutline.enabled = showOutline;
            if (showOutline)
            {
                _energyIconOutline.sprite = sprite;
                // HP 아웃라인과 같은 이유로 텍셀 크기를 직접 채운다(PlayerHudUI.cs의 HP 아웃라인
                // 블록 주석 참고 — CanvasRenderer는 _MainTex_TexelSize를 자동으로 안 채운다).
                if (_energyOutlineMat != null && sprite.texture != null)
                {
                    var tex = sprite.texture;
                    _energyOutlineMat.SetVector("_MainTex_TexelSize",
                        new Vector4(1f / tex.width, 1f / tex.height, tex.width, tex.height));
                }
            }
        }

        if (_energyGlowIcon != null)
        {
            bool showGlow = sprite != null && sprite != _energyZeroSpriteCache;
            _energyGlowIcon.enabled = showGlow;
            if (showGlow && _energyGlowMat != null)
            {
                _energyGlowIcon.sprite = sprite;
                Color glowColor = Color.Lerp(energyGlowColor, energyRampageGlowColor, _rampageColorLerp);
                float pulse = rampaging ? RampagePulse() : 1f;
                _energyGlowMat.SetColor("_Color", glowColor);
                _energyGlowMat.SetFloat("_BloomBoost", energyGlowBoost);
                _energyGlowMat.SetFloat("_Intensity", energyGlowIntensity * pulse);
            }
        }
    }

    /// <summary>
    /// 하트비트 리듬의 빠른 삼각파 펄스(0~1, 바닥은 energyRampagePulseFloor) — 폭주 중 광원 블룸에만
    /// 건다(사용자 지시 2026-08-11 "하트 쉐이킹 효과 같이 빠르게 페이드 인 아웃", RampageHeartbeatFx.cs의
    /// lub 박동 리듬(상승 0.05s/하강 0.10s) 참고).
    /// </summary>
    float RampagePulse()
    {
        float cycle = Mathf.Max(0.001f, energyRampagePulseRise + energyRampagePulseFall);
        float t = _energyGlowPulsePhase % cycle;
        float k = t < energyRampagePulseRise
            ? t / Mathf.Max(0.001f, energyRampagePulseRise)
            : 1f - (t - energyRampagePulseRise) / Mathf.Max(0.001f, energyRampagePulseFall);
        return Mathf.Lerp(energyRampagePulseFloor, 1f, Mathf.Clamp01(k));
    }

    static float Ratio(int current, int max) => max <= 0 ? 0f : Mathf.Clamp01((float)current / max);

    // 프레임레이트에 독립적인 지수 보간(같은 dt 총합이면 같은 결과).
    static float Smooth(float current, float target, float speed, float dt)
        => Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));

    /// <summary>HP1~8/Hp_Zero 스프라이트와 빨간 칸 글로우 마스크를 한 번만 로드해 캐싱한다.</summary>
    static void LoadHpAssets()
    {
        if (_hpSpriteCache != null) return;

        var all = Resources.LoadAll<Sprite>("UI/HpBar");
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning("[PlayerHudUI] Resources/UI/HpBar 스프라이트를 찾을 수 없습니다.");
            return;
        }
        // 이름이 아니라 아틀라스 내 x좌표로 정렬한다 — HP1..HP8이 왼쪽→오른쪽 순으로 슬라이스돼
        // 있음을 실측(밝은 빨강 픽셀 수 30→152 단조 증가)으로 확인했다. 이름은 재슬라이스되면
        // 바뀔 수 있지만 rect.x 순서는 그대로다.
        System.Array.Sort(all, (a, b) => a.rect.x.CompareTo(b.rect.x));
        _hpSpriteCache = all;
        _hpZeroSpriteCache = Resources.Load<Sprite>("UI/Hp_Zero");
        _hpGlowMaskCache = Resources.Load<Texture2D>("UI/HpBarGlowMask");
    }

    static Sprite GetHpSprite(int index) =>
        _hpSpriteCache != null && index >= 0 && index < _hpSpriteCache.Length ? _hpSpriteCache[index] : null;

    /// <summary>EnergyBar 1~8/EnergyBar-Zero 스프라이트를 한 번만 로드해 캐싱한다(LoadHpAssets와 같은 패턴).</summary>
    static void LoadEnergyAssets()
    {
        if (_energySpriteCache != null) return;

        var all = Resources.LoadAll<Sprite>("UI/EnergyBar");
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning("[PlayerHudUI] Resources/UI/EnergyBar 스프라이트를 찾을 수 없습니다.");
            return;
        }
        // HpBar와 동일하게 이름이 아니라 아틀라스 내 x좌표로 정렬한다(왼쪽=적게 참, 오른쪽=많이 참).
        System.Array.Sort(all, (a, b) => a.rect.x.CompareTo(b.rect.x));
        _energySpriteCache = all;
        _energyZeroSpriteCache = Resources.Load<Sprite>("UI/EnergyBar-Zero");
        _energyGlowMaskCache = Resources.Load<Texture2D>("UI/EnergyBarGlowMask");
    }

    static Sprite GetEnergySprite(int index) =>
        _energySpriteCache != null && index >= 0 && index < _energySpriteCache.Length ? _energySpriteCache[index] : null;

}

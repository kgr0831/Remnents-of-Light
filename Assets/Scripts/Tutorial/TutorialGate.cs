using UnityEngine;

/// <summary>튜토리얼에서 "지금 쓸 수 있는 동작"을 나타내는 비트 마스크.
/// 각 항목은 PlayerController 안에서 그 동작이 **시작되는 지점 한 곳**에만 걸린다 —
/// 잠긴 동작은 연출도, 판정도, 입력 수신도 일어나지 않는다(사용자 스펙:
/// "패링 튜토리얼 중에는 우클릭 홀드를 해도 일섬이 아예 입력조차 안 됨 + 처형 아웃라인도 안 뜸").</summary>
[System.Flags]
public enum TutorialAbility
{
    None = 0,
    Move = 1 << 0,          // A/D 이동 · 벽타기 상하 입력(moveInput 자체)
    Jump = 1 << 1,
    WallClimb = 1 << 2,
    Attack = 1 << 3,
    Dash = 1 << 4,
    DodgeCounter = 1 << 5,  // 대시 회피 → 카운터
    Parry = 1 << 6,
    TimeAccel = 1 << 7,
    Ilseom = 1 << 8,
    LightSpend = 1 << 9,    // E 홀드 광원 방출
    Execution = 1 << 10,
    Transcend = 1 << 11,    // 광원 100% 자동 진입 상태
    Rampage = 1 << 12,      // 광원 0% 자동 진입 상태
    All = ~0,
}

/// <summary>튜토리얼 전용 전역 게이트.
///
/// ⚠️ 기본값이 <see cref="TutorialAbility.All"/> + 무적/크리티컬 제한 없음이라, 튜토리얼이 아닌 씬은
///    이 클래스가 존재하는지도 모르는 것과 똑같이 동작한다(회귀 0). 값을 바꾸는 곳은
///    TutorialDirector 한 곳뿐이고, 그쪽 OnDisable이 항상 되돌린다.
///
/// ⚠️ static 값은 "Enter Play Mode without domain reload" 설정에서 Play 세션을 넘어 살아남는다 —
///    튜토리얼 도중 Stop하면 다음 Play(다른 씬)까지 잠금이 새어 나갈 수 있어
///    RuntimeInitializeOnLoadMethod로 매 Play 시작에 반드시 초기화한다
///    (PlayerController.Awake가 Time.timeScale을 방어적으로 1로 되돌리는 것과 같은 이유).</summary>
public static class TutorialGate
{
    public static TutorialAbility Allowed = TutorialAbility.All;

    /// <summary>true면 플레이어가 피해를 전혀 받지 않는다(스펙: "적의 공격을 받아도 체력이 닳지 않음").</summary>
    public static bool Invulnerable;

    /// <summary>true면 좌클릭 일반 공격의 크리티컬이 발생하지 않는다(스펙: 공격 스텝 "크리티컬 불가능").</summary>
    public static bool NoCrit;

    public static bool Has(TutorialAbility a) => (Allowed & a) == a;

    public static void ResetAll()
    {
        Allowed = TutorialAbility.All;
        Invulnerable = false;
        NoCrit = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => ResetAll();
}

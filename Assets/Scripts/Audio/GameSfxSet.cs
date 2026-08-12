using UnityEngine;

/// <summary>효과음 종류 — <see cref="GameSfxSet.entries"/>와 1:1로 대응한다.</summary>
public enum Sfx
{
    Footstep,       // 걷기1/2/3 — 재생할 때마다 랜덤
    Jump,           // 점프
    Land,           // 착지
    Dash,           // 대시
    Attack1,        // 1타
    Attack2,        // 2타
    EnemyHit,       // 적피격1/2 — 재생할 때마다 랜덤
    PlayerHit,      // 플레이어피격
    Parry,          // 패링(성공)
    ShieldBreak,    // 실드파괴 — 패링 실드가 한 대 막고 깨질 때
    Ilseom,         // 일섬(발동)
    IlseomCharge,   // 일섬홀드 — 차지 중 루프
    DodgeCounter,   // 회피카운터 — 회피-카운터 · 시간 가속 공용(루프 없이 1회)
    RampageEnter,   // 폭주or광원진입 — 폭주 · 초월 진입
    LightEmit,      // 광원방출 — E 홀드 중 루프
    Heal,           // 회복

    // ⚠️ 새 항목은 반드시 **끝에** 추가한다 — id가 .asset에 정수로 직렬화돼 있어 중간에 끼우면
    //    기존 엔트리가 통째로 밀려 엉뚱한 클립이 붙는다.
    Impact,         // Impect — 낙하 컷신에서 로고가 나오는 순간
    Glitch,         // GlitchSFX1 — 검 획득 컷신 마무리 글리치

    LightGain,      // 광원획득 — 광원 오브젝트를 쳐서 얻을 때 1회
    Born,           // born — IntroScene의 born 애니메이션이 시작될 때 1회
    Pickup,         // 획득 — IntroScene_2에서 암전 뒤 Sword를 삭제할 때 1회
    EgoZero,        // 자아 0상태 — 폭주 중 자아가 0인 동안 루프(다시 차면 정지)
    CounterAttack,  // 회피카운터공격 — 카운터가 적을 타격하는 순간
    Execution,      // 처형 — 처형 시퀀스 발동 순간

    BossAppear,     // BossSFX-2 — Map-test 보스 등장(어둠 속에서 걸어 나올 때) 1회
    BossRoar,       // BossSFX-1 — 보스가 빔을 플레이어에게 겨눈 뒤 1회
    BossBeamNoise,  // BossBeamSFX — 빔 노출 3초가 넘어 화면 노이즈가 켜지는 순간 1회
}

/// <summary>
/// 전역 효과음 테이블. <c>Assets/Resources/GameSfxSet.asset</c> 하나만 존재하고
/// <see cref="GameSfx"/>가 런타임 시작 시 Resources에서 읽는다 — 씬마다 배선할 필요가 없다.
/// mp3/wav 원본은 <c>Assets/SFX/</c> 아래 그대로 두고 여기서 참조만 한다.
/// </summary>
[CreateAssetMenu(fileName = "GameSfxSet", menuName = "Audio/Game SFX Set")]
public class GameSfxSet : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public Sfx id;
        [Tooltip("2개 이상이면 재생할 때마다 랜덤으로 하나를 고른다(걷기 3종 · 적피격 2종).")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public Entry[] entries;

    [Tooltip("지상 이동 중 발소리 간격(초). Run 클립이 12프레임@12fps = 1초 1사이클이라 0.5면 사이클당 2발.")]
    public float footstepInterval = 0.5f;
}

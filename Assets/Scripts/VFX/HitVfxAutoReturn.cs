using UnityEngine;

// UniTrio-Game-2026(Assets/Scripts/Player/Weapon/HitVfxAutoReturn.cs)에서 이식.
// 원본은 SimpleObjectPool로 반환했지만 이 프로젝트엔 풀링 컨벤션이 없어(DashAfterImage.cs와 동일하게)
// 자가 파괴로 대체했다.
// 원본은 Awake에서 Custom/VFXLit2D 발광 머티리얼을 런타임 생성해 SpriteRenderer에 씌웠지만 제거했다 —
// 그 셰이더의 유일한 Pass가 "LightMode"="UniversalForward"인데, URP 2D Renderer는 Universal2D와
// SRPDefaultUnlit 태그만 수집하므로(DrawRenderer2DPass.k_ShaderTags) 패스가 통째로 스킵돼 VFX가
// 아예 안 그려졌다. 프리팹에 지정된 Sprite-Lit-Default를 그대로 쓴다.
[RequireComponent(typeof(Animator))]
public class HitVfxAutoReturn : MonoBehaviour
{
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        // 매번 0프레임부터 재생되도록 Animator 초기화
        _animator.Rebind();
        _animator.Update(0f);

        float dur = _animator.GetCurrentAnimatorStateInfo(0).length;
        StartCoroutine(SelfDestroyAfter(dur > 0f ? dur : 0.5f));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    // ⚠️ Invoke가 아니라 **실시간** 대기다(사용자 리포트 2026-08-12 "전투학습 종료 시점에 공격
    //    이펙트가 남아있음"). Invoke는 스케일 시간으로 도는데, 튜토리얼은 스텝 성공 연출에서
    //    Time.timeScale = 0으로 세계를 멈춘 채 암전으로 넘어간다(TutorialDirector.Freeze) —
    //    그 순간 살아 있던 히트 VFX는 파괴 타이머가 아예 안 돌아 화면에 얼어붙은 채로 남았다.
    //    TutorialDirector가 successHold(1초)만큼 시간을 흘려 완화해 뒀지만, 그보다 늦게 터진
    //    스윙은 여전히 남는다. 실시간으로 세면 세계가 멈춰 있어도 예정대로 사라진다.
    private System.Collections.IEnumerator SelfDestroyAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Destroy(gameObject);
    }
}

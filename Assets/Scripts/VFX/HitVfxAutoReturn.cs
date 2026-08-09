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
        Invoke(nameof(SelfDestroy), dur > 0f ? dur : 0.5f);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(SelfDestroy));
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}

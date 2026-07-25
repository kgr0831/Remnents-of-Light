using UnityEngine;

// 에디터에서 실드 링을 눈으로 보며 배치·크기를 잡기 위한 미리보기(VfxSandbox 씬 전용).
// 플레이 모드가 아니어도 보이도록 [ExecuteAlways]로 메시를 만든다.
//
// ★ 런타임 값과의 대응 (이 오브젝트를 씬 뷰에서 옮기고 스케일하면 그대로 읽어 쓸 수 있다)
//   transform.localPosition.xy  →  PlayerController.parryShieldOffset   (플레이어 로컬 단위)
//   transform.localScale.x      →  PlayerController.parryShieldRadius   (균등 스케일)
// 메시는 링 반지름이 로컬 1.0이 되도록 만든다(셰이더가 |p|=0.5에 링을 두므로 반너비는 2.0).
// 따라서 부모(Player_StandIn, 스케일 1.3)의 자식으로 두면 값이 1:1로 대응된다.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ParryShieldPreview : MonoBehaviour
{
    const float HalfExtent = 2f; // 셰이더 규약: 링 반지름 = 반너비의 절반

    Mesh mesh;

    void OnEnable() { Rebuild(); }

    void Rebuild()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh != null && mf.sharedMesh.name == "ParryShieldPreview") return;

        mesh = new Mesh();
        mesh.name = "ParryShieldPreview";
        mesh.vertices = new[]
        {
            new Vector3(-HalfExtent, -HalfExtent, 0f),
            new Vector3( HalfExtent, -HalfExtent, 0f),
            new Vector3( HalfExtent,  HalfExtent, 0f),
            new Vector3(-HalfExtent,  HalfExtent, 0f),
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }

    // 씬 뷰에서 실제 링 반지름이 어디인지 바로 보이도록 기즈모를 그린다.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(Vector3.zero, 1f);
    }
}

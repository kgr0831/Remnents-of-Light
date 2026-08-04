using UnityEditor;
using UnityEngine;

// 벽타기가 붙을 "벽"을 씬에서 직접 지정하기 위한 도구 (사용자 지시 2026-08-04).
//
// 왜 필요한가: 예전엔 PlayerController가 지형(Ground) 콜라이더를 벽으로도 같이 봤기 때문에 바닥·플랫폼·
// 타일 이음매가 전부 벽으로 잡혔고, 높이로 "진짜 벽인지" 추정해 걸러내려 해도 얇은 플랫폼 같은 예외가
// 계속 나왔다. 이제 추정하지 않는다 — 여기서 만든 **Wall 레이어 콜라이더에만** 벽타기가 붙는다
// (PlayerController.climbWallLayer).
//
// 사용법:
//   1) Scene 뷰에서 벽이 될 위치를 대충 잡고 (선택된 오브젝트가 있으면 그 근처, 없으면 씬 뷰 화면 중앙)
//      메뉴 Tools/Level/Create Wall Collider (단축키 Ctrl+Shift+W)
//   2) 생성된 "Wall" 오브젝트를 씬 뷰에서 이동·스케일로 실제 벽면에 맞춘다(BoxCollider2D 핸들 사용).
//   3) 다 배치했으면 씬을 저장한다. ← 저장은 사용자가 직접(이 도구는 씬을 저장하지 않는다)
//
// 주의: 이 콜라이더는 **벽타기 판정 전용**이다. 실제 충돌(못 지나감)은 기존 Ground 지형이 계속 담당하므로
// isTrigger로 만들어 물리적으로 끼거나 밀리지 않게 한다 — 벽면에 겹쳐 놔도 이동에 영향이 없다.
public static class WallColliderTool
{
    const string WallLayerName = "Wall";
    const string RootName = "Walls";

    [MenuItem("Tools/Level/Create Wall Collider %#w")]
    public static void CreateWallCollider()
    {
        int layer = LayerMask.NameToLayer(WallLayerName);
        if (layer < 0)
        {
            EditorUtility.DisplayDialog("Wall 레이어 없음",
                $"프로젝트에 \"{WallLayerName}\" 레이어가 없습니다. Project Settings > Tags and Layers에서 먼저 추가하세요.",
                "확인");
            return;
        }

        // 부모 정리용 루트("Walls")를 재사용한다 — 씬 하이어라키가 벽 오브젝트로 흩어지지 않게.
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Walls Root");
        }

        var go = new GameObject("Wall");
        go.layer = layer;
        Undo.RegisterCreatedObjectUndo(go, "Create Wall Collider");
        go.transform.SetParent(root.transform, true);
        go.transform.position = SpawnPosition();

        var box = go.AddComponent<BoxCollider2D>();
        // 세로로 긴 기본 크기(벽의 전형적인 형태) — 실제 크기는 씬 뷰에서 맞춘다.
        box.size = new Vector2(1f, 4f);
        // 판정 전용이라 트리거로 둔다(주석 상단 참고). 물리 충돌은 Ground 지형이 그대로 담당한다.
        box.isTrigger = true;

        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"[WallColliderTool] Wall 콜라이더 생성 — 씬 뷰에서 크기·위치를 맞춘 뒤 씬을 저장하세요. " +
                  $"(layer={WallLayerName}, isTrigger=true)");
    }

    // 선택된 오브젝트가 있으면 그 위치, 없으면 씬 뷰 카메라가 보고 있는 지점에 만든다.
    static Vector3 SpawnPosition()
    {
        if (Selection.activeTransform != null)
        {
            Vector3 p = Selection.activeTransform.position;
            p.z = 0f;
            return p;
        }
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            Vector3 p = sv.pivot;
            p.z = 0f;
            return p;
        }
        return Vector3.zero;
    }

    // 배치가 끝났는지 한눈에 확인하는 용도 — 씬에 있는 Wall 레이어 콜라이더를 세어 콘솔에 찍는다.
    [MenuItem("Tools/Level/Count Wall Colliders")]
    public static void CountWallColliders()
    {
        int layer = LayerMask.NameToLayer(WallLayerName);
        var all = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        int n = 0;
        Bounds total = new Bounds();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject.layer != layer) continue;
            if (n == 0) total = all[i].bounds; else total.Encapsulate(all[i].bounds);
            n++;
        }
        Debug.Log(n == 0
            ? "[WallColliderTool] Wall 레이어 콜라이더 0개 — 지금은 벽타기가 어디서도 발동하지 않습니다."
            : $"[WallColliderTool] Wall 레이어 콜라이더 {n}개, 전체 범위 {total.min} ~ {total.max}");
    }
}

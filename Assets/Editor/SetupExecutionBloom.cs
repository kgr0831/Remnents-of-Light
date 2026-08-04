using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 처형 스펙 1의 "적 아웃라인이 붉은 색 + 블룸"에서 **블룸 쪽 전제**를 켜주는 1회성 셋업.
//
// EnemyExecutionGlow.shader는 HDR 가산(_BloomBoost 4.0)으로 1.0을 넘는 붉은 값을 뱉지만,
// URP엔 오브젝트별 블룸이 없어서 그걸 실제로 번지게 하는 건 카메라의 Bloom 포스트 프로세스다.
// 현재 프로젝트는 그 전제가 둘 다 꺼져 있어 지금은 "납작한 붉은 실루엣"으로만 보인다(실측 2026-07-26):
//   - Map1의 Main Camera: m_RenderPostProcessing = 0  → Bloom 패스 자체가 안 돌아감
//   - Assets/DefaultVolumeProfile.asset의 Bloom: intensity = 0 → 켜도 출력이 0
//   - 같은 Bloom의 threshold = 0.9 → 1.0 미만이라, 켜는 순간 평범한 밝은 LDR 스프라이트까지 번진다
//
// ⚠️ 이건 게임 전체 렌더링에 영향을 주는 전역 설정이다(처형뿐 아니라 모든 화면).
//    실행 전에 값을 확인하고, 마음에 안 들면 Bloom.intensity를 0으로 되돌리면 원상복구된다.
//
// 출처: 오브젝트별 블룸의 전제(HDR 출력 + threshold를 1.0 위로) — .claude/skills/add-combat-move/SKILL.md
//       "특정 오브젝트만 블룸시키려면" 항목.
public static class SetupExecutionBloom
{
    // 임계값은 1.0보다 위에 둔다 — LDR 스프라이트(최대 1.0)는 절대 못 넘고,
    // 셰이더가 뱉는 HDR 값(붉은 틴트 1.0×boost 4.0 = 4.0)만 통과한다.
    const float BloomThreshold = 1.15f;
    const float BloomIntensity = 1.0f;

    [MenuItem("Tools/Setup Execution Bloom")]
    public static void Run()
    {
        VolumeProfile profile = GraphicsSettings
            .GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>()?.volumeProfile;
        if (profile == null)
        {
            Debug.LogError("[SetupExecutionBloom] 기본 Volume Profile을 찾지 못했습니다 " +
                "(Project Settings > Graphics > Volumes > Default Profile).");
            return;
        }

        if (!profile.TryGet(out Bloom bloom))
        {
            Debug.LogError($"[SetupExecutionBloom] '{profile.name}'에 Bloom 오버라이드가 없습니다.");
            return;
        }

        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = BloomThreshold;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = BloomIntensity;
        EditorUtility.SetDirty(profile);

        // 씬의 URP 카메라에 포스트 프로세싱을 켠다(이게 꺼져 있으면 Bloom 패스가 아예 안 돈다).
        int cameraCount = 0;
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.renderPostProcessing) continue;
            Undo.RecordObject(data, "Enable Post Processing");
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
            cameraCount++;
        }

        AssetDatabase.SaveAssets();
        if (cameraCount > 0) EditorSceneManagerSaveHint();

        Debug.Log($"[SetupExecutionBloom] Bloom threshold={BloomThreshold} intensity={BloomIntensity} " +
            $"(profile='{profile.name}'), 포스트 프로세싱을 켠 카메라 {cameraCount}개. " +
            "카메라 변경은 씬 저장이 필요합니다.");
    }

    static void EditorSceneManagerSaveHint()
    {
        Scene scene = SceneManager.GetActiveScene();
        Debug.LogWarning($"[SetupExecutionBloom] 씬 '{scene.name}'의 카메라를 수정했습니다 — Ctrl+S로 저장하세요.");
    }
}

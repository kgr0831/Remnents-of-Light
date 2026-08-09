using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

// 자아 고갈 화면 글리치(사용자 지시 2026-08-01)의 1회성 셋업 — ScreenGlitchFeature를
// Renderer2D.asset에 등록한다. GrayscaleRendererFeature/RampageVisionFeature도 같은 자산에
// 이미 등록돼 있어야 이 기능이 정상 동작한다(둘 다 이번 셋업 이전에 이미 붙어 있음).
//
// ⚠️ 이건 게임 전체 렌더링에 영향을 주는 전역 설정이다(SetupExecutionBloom과 같은 이유로 신중해야 함).
//    실행은 멱등적이다 — 이미 등록돼 있으면 아무것도 하지 않고 로그만 남긴다.
//
// 등록 시점이 GrayscaleRendererFeature/RampageVisionFeature와 같은 RenderPassEvent
// (BeforeRenderingPostProcessing)를 쓰므로, 같은 이벤트를 쓰는 패스는 Renderer2D.asset의
// 피처 목록 순서로 실행된다(ScreenGlitchFeature.cs Create() 주석 참고) — 그래서 리스트 끝에
// 추가해 두 피처보다 항상 뒤에 실행되게 한다(폭주 중 화면 암전 위의 보호 레이어까지 글리치가 겹치도록).
public static class SetupScreenGlitch
{
    const string RendererAssetPath = "Assets/Settings/Renderer2D.asset";

    [MenuItem("Tools/Setup Screen Glitch")]
    public static void Run()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererAssetPath);
        if (data == null)
        {
            Debug.LogError($"[SetupScreenGlitch] '{RendererAssetPath}'에서 ScriptableRendererData를 찾지 못했습니다.");
            return;
        }

        foreach (var existing in data.rendererFeatures)
        {
            if (existing is ScreenGlitchFeature)
            {
                Debug.Log($"[SetupScreenGlitch] 이미 등록되어 있습니다('{existing.name}') — 변경 없음.");
                return;
            }
        }

        var feature = ScriptableObject.CreateInstance<ScreenGlitchFeature>();
        feature.name = "ScreenGlitchFeature";
        AssetDatabase.AddObjectToAsset(feature, data);
        data.rendererFeatures.Add(feature); // 끝에 추가 — Grayscale/RampageVision보다 뒤에 실행되어야 한다
        data.SetDirty();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SetupScreenGlitch] ScreenGlitchFeature를 '{RendererAssetPath}'에 등록했습니다 " +
            $"(피처 {data.rendererFeatures.Count}개 중 마지막).");
    }
}

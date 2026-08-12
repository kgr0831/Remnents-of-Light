using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ⚠️ 임시 진단용. 빌드에서만 죽는 블룸의 원인을 좁히기 위한 것이며, 확정되면 삭제한다.
//
// 빌드에서 "무엇이 에디터와 다른가"를 Player.log에 [ASSERT]로 남긴다. 씬 배선이 필요 없도록
// RuntimeInitializeOnLoadMethod로 스스로 붙고, 튜토리얼 패널처럼 도중에만 떠 있는 것도 잡히도록
// 몇 초 간격으로 여러 번 찍는다.
//
// ⚠️ 셰이더 이름을 문자열 리터럴로 적을 때 주의 — ShaderIncludeValidator가 모든 .cs를 스캔해
//    AlwaysIncludedShaders에 없는 이름이 있으면 빌드를 막는다(그게 그 검증기의 목적이고 옳다).
public class BloomDiagnostics : MonoBehaviour
{
    // 빌드는 TitleScene에서 시작한다 — 타이틀 → 인트로 → 튜토리얼까지 이동할 시간이 필요하므로
    // 넉넉히 10분간 찍는다(3초 × 200회). 짧게 잡으면 정작 패널이 뜬 순간을 못 잡는다.
    const int Samples = 200;
    const float Interval = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("~BloomDiagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<BloomDiagnostics>();
    }

    IEnumerator Start()
    {
        LogEnvironment();
        for (int i = 0; i < Samples; i++)
        {
            yield return new WaitForSecondsRealtime(Interval);
            LogFrame(i);
        }
        Debug.Log("[ASSERT] bloom_done 샘플 종료");
    }

    static void LogEnvironment()
    {
        int q = QualitySettings.GetQualityLevel();
        var urp = QualitySettings.GetRenderPipelineAssetAt(q) as UniversalRenderPipelineAsset;
        Debug.Log($"[ASSERT] bloom_env quality={q}:{QualitySettings.names[q]} " +
                  $"urp={(urp != null ? urp.name : "null")} hdr={(urp != null ? urp.supportsHDR.ToString() : "-")} " +
                  $"colorSpace={QualitySettings.activeColorSpace} screen={Screen.width}x{Screen.height}");

        string[] shaders = { "Custom/PlayerBloomOverlay", "Custom/PlayerMaskEmissive" };
        for (int i = 0; i < shaders.Length; i++)
        {
            Shader sh = Shader.Find(shaders[i]);
            Debug.Log($"[ASSERT] bloom_shader name={shaders[i]} found={(sh != null)} " +
                      $"supported={(sh != null ? sh.isSupported.ToString() : "-")}");
        }
    }

    static void LogFrame(int idx)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            var ud = cam.GetComponent<UniversalAdditionalCameraData>();
            Debug.Log($"[ASSERT] bloom_cam #{idx} name={cam.name} allowHDR={cam.allowHDR} " +
                      $"post={(ud != null ? ud.renderPostProcessing.ToString() : "no-data")} " +
                      $"type={(ud != null ? ud.renderType.ToString() : "-")} " +
                      $"volumeMask={(ud != null ? ud.volumeLayerMask.value.ToString() : "-")}");
        }

        var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
        var bloom = stack != null ? stack.GetComponent<Bloom>() : null;
        Debug.Log(bloom != null
            ? $"[ASSERT] bloom_volume #{idx} active={bloom.active} threshold={bloom.threshold.value:F3} " +
              $"intensity={bloom.intensity.value:F3} scatter={bloom.scatter.value:F2}"
            : $"[ASSERT] bloom_volume #{idx} **스택에서 Bloom을 못 찾음**");

        LogCanvases(idx);
        LogSprites(idx);
        LogPlayerBloom(idx);
    }

    // 캔버스가 다른 캔버스의 자식이면 renderMode는 루트 캔버스 것이 적용된다 — 루트가 Overlay면
    // URP 포스트프로세싱을 아예 안 받아서 HDR을 내보내도 블룸이 안 걸린다.
    static void LogCanvases(int idx)
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            Canvas root = c.rootCanvas;
            Debug.Log($"[ASSERT] bloom_canvas #{idx} name={c.name} mode={c.renderMode} isRoot={c.isRootCanvas} " +
                      $"root={(root != null ? root.name : "-")} rootMode={(root != null ? root.renderMode.ToString() : "-")} " +
                      $"cam={(root != null && root.worldCamera != null ? root.worldCamera.name : "null")}");
        }

        // _Boost를 가진 그래픽 = 블룸 부스트 계열. 실제 렌더에 쓰이는 머티리얼(materialForRendering)을 본다.
        var graphics = Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < graphics.Length; i++)
        {
            Material m = graphics[i].materialForRendering;
            if (m == null || m.shader == null || !m.HasProperty("_Boost")) continue;
            float boost = m.GetFloat("_Boost");
            Color mc = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
            Color vc = graphics[i].color;
            Debug.Log($"[ASSERT] bloom_ui #{idx} name={graphics[i].name} shader={m.shader.name} " +
                      $"supported={m.shader.isSupported} boost={boost:F2} matColor={mc:F2} vertColor={vc:F2} " +
                      $"canvasAlpha={GetGroupAlpha(graphics[i])} " +
                      $"maxOut={Mathf.Max(mc.r * vc.r, Mathf.Max(mc.g * vc.g, mc.b * vc.b)) * Mathf.Max(boost, 0f):F3}");
        }
    }

    static string GetGroupAlpha(Component c)
    {
        var g = c.GetComponentInParent<CanvasGroup>();
        return g != null ? g.alpha.ToString("F2") : "-";
    }

    // 머티리얼 유니폼으로 옮긴 HDR이 빌드에서도 살아 있는지
    static void LogSprites(int idx)
    {
        string[] want = { "Glow", "Edge_jump_L", "SimScanline" };
        var srs = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int printed = 0;
        for (int i = 0; i < srs.Length && printed < 4; i++)
        {
            if (System.Array.IndexOf(want, srs[i].name) < 0) continue;
            Material m = srs[i].sharedMaterial;
            Color mc = m != null && m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
            Color sc = srs[i].color;
            Debug.Log($"[ASSERT] bloom_sprite #{idx} name={srs[i].name} mat={(m != null ? m.name : "null")} " +
                      $"shaderSupported={(m != null && m.shader != null ? m.shader.isSupported.ToString() : "-")} " +
                      $"matColor={mc:F2} srColor={sc:F2} " +
                      $"maxOut={Mathf.Max(mc.r * sc.r, Mathf.Max(mc.g * sc.g, mc.b * sc.b)):F3}");
            printed++;
        }
    }

    static void LogPlayerBloom(int idx)
    {
        var fxs = Object.FindObjectsByType<PlayerBloomFx>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var sb = new StringBuilder();
        sb.Append($"[ASSERT] bloom_player #{idx} count={fxs.Length}");
        for (int i = 0; i < fxs.Length; i++)
        {
            var sr = fxs[i].GetComponent<SpriteRenderer>();
            Material m = sr != null ? sr.sharedMaterial : null;
            if (m == null) { sb.Append(" | (mat null)"); continue; }
            float inten = m.HasProperty("_Intensity") ? m.GetFloat("_Intensity") : -1f;
            float boost = m.HasProperty("_BloomBoost") ? m.GetFloat("_BloomBoost") : -1f;
            Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
            sb.Append($" | owner={(fxs[i].transform.parent != null ? fxs[i].transform.parent.name : "-")} " +
                      $"shader={m.shader.name} supported={m.shader.isSupported} " +
                      $"intensity={inten:F3} boost={boost:F2} color={col:F2} sprite={(sr.sprite != null ? sr.sprite.name : "null")}");
        }
        Debug.Log(sb.ToString());
    }
}

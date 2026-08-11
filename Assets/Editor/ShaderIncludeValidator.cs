using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 빌드 전 검사: 스크립트가 **이름 문자열로** 찾는 셰이더가 빌드에 포함되는지 확인한다.
///
/// 왜 필요한가 — 머티리얼 에셋이 참조하지 않고 Shader.Find("...")로만 쓰는 셰이더는 빌드에 아예
/// 포함되지 않는다(에디터는 프로젝트 전체를 들고 있어 항상 찾아진다). 빌드에서만 Shader.Find가
/// null을 반환하고, 그 자리를 기본 머티리얼이 대신 그려 "블룸이 안 걸린다 / 마스크를 무시하고
/// 전부 밝게 나온다"가 된다. 이 원인으로 세 번 터졌다:
///   · 2026-08-10 b6d58d6 — 커스텀 셰이더 12개를 뒤늦게 AlwaysIncludedShaders에 추가
///   · 2026-08-11 e853839 / f361890 — HP·광원 UI 셰이더 6개를 같은 이유로 추가
///   · 2026-08-11 — Custom/PlayerMaskEmissive 누락으로 빌드에서 폭주·초월·사망 블룸이 전부 사라짐
/// 매번 사람이 기억해서 등록하는 구조라 또 터진다. 그래서 빌드를 막는다.
///
/// 규칙: **스크립트가 이름으로 참조하는 프로젝트 셰이더는 반드시 AlwaysIncludedShaders에 있어야 한다.**
/// 머티리얼 에셋이 참조하니 괜찮다는 논리는 쓰지 않는다 — 그 머티리얼이 빌드에 포함된 씬에서
/// 빠지는 순간(예: 씬 정리) 조용히 다시 깨진다.
/// </summary>
public class ShaderIncludeValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    // "Custom/Foo", "Hidden/Bar" 처럼 슬래시가 들어간 문자열 리터럴만 후보로 본다. 실제 셰이더
    // 이름과 정확히 일치하는 것만 남기므로(아래 lookup) 경로·태그 문자열이 섞여도 걸러진다.
    static readonly Regex SlashLiteral = new Regex("\"([A-Za-z0-9_]+(?:/[A-Za-z0-9_ \\-]+)+)\"", RegexOptions.Compiled);

    public void OnPreprocessBuild(BuildReport report)
    {
        var shaderNames = CollectProjectShaderNames();
        var included = CollectAlwaysIncludedShaderNames();

        // 셰이더 이름 -> 그 이름을 문자열로 들고 있는 스크립트들
        var missing = new SortedDictionary<string, SortedSet<string>>();
        foreach (var csPath in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
        {
            string src = File.ReadAllText(csPath);
            foreach (Match m in SlashLiteral.Matches(src))
            {
                string name = m.Groups[1].Value;
                if (!shaderNames.Contains(name)) continue; // 프로젝트 셰이더 이름이 아니면 무관
                if (included.Contains(name)) continue;
                if (!missing.TryGetValue(name, out var users))
                    missing[name] = users = new SortedSet<string>();
                users.Add(csPath.Replace('\\', '/'));
            }
        }

        if (missing.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("[ShaderIncludeValidator] 빌드에서 스트립될 셰이더가 있습니다.");
        sb.AppendLine("Project Settings > Graphics > Always Included Shaders 에 아래를 추가하세요.");
        foreach (var kv in missing)
        {
            sb.AppendLine("  · " + kv.Key);
            foreach (var u in kv.Value) sb.AppendLine("        참조: " + u);
        }
        throw new BuildFailedException(sb.ToString());
    }

    static HashSet<string> CollectProjectShaderNames()
    {
        var names = new HashSet<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue; // 패키지 셰이더는 패키지가 알아서 포함한다
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (sh != null) names.Add(sh.name);
        }
        return names;
    }

    static HashSet<string> CollectAlwaysIncludedShaderNames()
    {
        var names = new HashSet<string>();
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0) return names;

        var arr = new SerializedObject(assets[0]).FindProperty("m_AlwaysIncludedShaders");
        if (arr == null) return names;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var sh = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (sh != null) names.Add(sh.name);
        }
        return names;
    }
}

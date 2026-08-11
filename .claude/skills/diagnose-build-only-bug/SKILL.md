# 빌드에서만 깨지는 렌더링 버그 잡기

## 언제 쓰나(트리거)

- "에디터는 멀쩡한데 빌드에서만" 안 보인다 · 안 빛난다 · 색이 다르다 · 화면이 깨진다
- 새 커스텀 셰이더 / 런타임 생성 머티리얼 / 런타임 로드 에셋을 추가한 직후 (터지기 **전에** 예방 점검)
- 스크린샷으로 "블룸이 안 걸린다"고 판단하려는 순간 (0번 항목을 먼저 읽을 것)

## 절차(단계)

### 0. 먼저 "정말 안 걸리는 게 맞나"를 의심한다 — MCP 스크린샷은 LDR이다

`manage_camera screenshot`(MCP 기본 경로)은 **LDR 타깃**으로 렌더한다. UI의 HDR 출력과 그로 인한
블룸이 **찍히지 않는다**. 이걸 모르고 "블룸이 안 걸린다"고 판단하면 엉뚱한 데를 판다(실제로 한 번 팠다).

진짜 화면은 HDR 렌더타깃에 직접 그려서 봐야 한다:

```csharp
var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBHalf); rt.Create();
var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
RenderTexture.active = rt;
var tex = new Texture2D(1600, 900, TextureFormat.RGBAHalf, false);
tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
RenderTexture.active = null;
// ⚠️ 프로젝트가 Linear 컬러스페이스면 RT 값은 리니어다 — PNG로 저장하기 전 c.gamma 변환 필수.
//    안 하면 전체가 어둡게 나와 또 오판한다.
```

블룸이 실제로 도는지 판별하려면 임시 Volume(priority 100, `hideFlags = DontSave`)에
threshold를 확 낮춰 넣어 본다. **공유 프로파일 에셋은 절대 건드리지 말 것** — 게임플레이 씬까지 바뀐다.

### 1. `Shader.Find` / `CoreUtils.CreateEngineMaterial` 셰이더 → Always Included Shaders

씬·에셋에서 참조되지 않으므로 빌드에서 통째로 스트립된다. 호출부가 대부분 null 가드라
**에러도 없이 이펙트만 조용히 사라진다.**

점검 스크립트 (Assets 안 셰이더 중 등록 안 된 것 나열):

```csharp
var inc = new System.Collections.Generic.HashSet<string>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset"))
{
    var p = new UnityEditor.SerializedObject(o).FindProperty("m_AlwaysIncludedShaders");
    if (p != null) for (int i = 0; i < p.arraySize; i++)
    { var s = p.GetArrayElementAtIndex(i).objectReferenceValue as Shader; if (s != null) inc.Add(s.name); }
}
// FindAssets("t:Shader", ["Assets"]) 돌면서 inc에 없는 것 출력
```

`grep -rn "Shader.Find" Assets/Scripts`로 나온 이름은 **전부** 목록에 있어야 한다.
머티리얼 에셋으로만 쓰는 셰이더는 자동 포함되므로 목록에 없어도 정상 — 둘을 구분해서 볼 것.

### 1-B. HDR을 **정점 색 경로**로 태우면 빌드에서만 잘린다 ★

가장 최근에 밟은 함정(2026-08-12). 증상이 "에디터는 빛나는데 빌드만 밋밋" 이면 1순위로 의심한다.

- `SpriteRenderer.color = tint * 2.0f` 처럼 **per-renderer 색**에 1을 넘는 값을 넣는 방식.
  에디터는 `_RendererColor`(float4 유니폼) 경로가 살아남아 멀쩡히 빛나지만, 빌드에서는 배칭이
  per-renderer 색을 **Color32 정점 색으로 구워 넣으며 1.0에서 잘라** 블룸 문턱을 못 넘는다.
- 같은 함정이 UGUI에도 있다 — UGUI 정점 색도 Color32다(그래서 `Custom/UIBloomBoost`가 존재한다).

**고치는 법**: HDR을 **머티리얼 유니폼**으로 옮긴다. 정점으로 안 구워지므로 절대 안 잘린다.

```csharp
// ✗ 빌드에서 잘린다
sr.color = tint * Mathf.Lerp(glowMin, glowMax, depth);

// ✓ 머티리얼 유니폼. 밝기를 N단계로 끊어 공유하면 배칭도 유지된다(낱개 MPB는 드로우콜 폭증)
glowMats[i].SetColor("_Color", tint * glowLevel);   // a는 1로, 알파는 sr.color에서 준다
sr.sharedMaterial = glowMats[step];
sr.color = new Color(1f, 1f, 1f, tint.a);
```

판별 요령: 같은 화면에서 **머티리얼 유니폼으로 HDR을 태우는 것(예: UIBloomBoost 텍스트)은 빛나는데
per-renderer 색을 쓰는 것만 안 빛나면** 확정이다. 블룸 파이프라인 자체는 정상이라는 뜻이므로
카메라·Volume·셰이더 스트립을 더 팔 필요가 없다.

### 2. `AssetDatabase`는 에디터 전용

런타임 코드에 있으면(`#if UNITY_EDITOR` 안이어도) 빌드에선 항상 폴백으로 빠진다.
실제 사고: `PlayerBloomFx`가 발광 마스크를 AssetDatabase로 찾아 빌드에선 흰색 폴백 →
눈만 빛나야 할 플레이어가 몸 전체가 빛났다.

### 3. 경로로 부르는 에셋은 `Resources` 폴더 아래

폴더 이름이 `Resources`면 어디에 있든 로드 루트가 된다
(예: `Assets/Sprites/Player/Mask/Resources/PlayerMask/`) — 원래 위치를 유지한 채 하위에 만들면
정리도 안 깨진다. 프리팹을 Resources 밖으로 옮겼다가 UI가 통째로 안 뜬 적 있음.

### 4. `camera.rect` 레터박스는 빌드에서 뷰포트 밖이 안 지워진다

에디터 게임 뷰는 매 프레임 타깃을 지워 주지만 빌드 백버퍼는 아니다 →
옛 프레임이 번갈아 보이며 "앞뒤 프레임이 반복되며 깨지는" 증상.
화면 전체를 지우기만 하는 카메라(cullingMask=0, depth 낮게)를 하나 더 돌린다.
UI 이미지 두 장으로 만든 레터박스(`CinematicLetterbox`)는 이 문제가 없다.

### 5. 씬·품질 설정 차이

- `QualitySettings.GetRenderPipelineAssetAt(i)` — **모든** 레벨이 같은 URP 에셋인지.
  빌드는 에디터와 다른 품질 레벨로 시작할 수 있고, 레벨마다 HDR/포스트가 꺼져 있을 수 있다.
- 카메라의 `m_RenderPostProcessing`, `m_RendererIndex`(-1이 기본), `m_VolumeLayerMask`
- Volume의 `sharedProfile`이 null이 아닌지, 그 프로파일이 **빌드 씬에서 참조되는지**
- `EditorBuildSettings.scenes`에 대상 씬이 enabled로 들어 있는지

## 검증(ASSERT 채널)

추측으로 끝내지 말고 실제 빌드로 확인한다 (이 프로젝트 Windows64 개발 빌드 ~42초):

1. `manage_build`로 Windows64 **개발 빌드**
2. exe 실행: `-screen-fullscreen 0 -screen-width W -screen-height H -logFile <path>`
   화면비를 16:10 등으로 주면 레터박스 경로까지 실제로 밟는다
3. PowerShell `CopyFromScreen`으로 화면 캡처 → 눈으로 비교
4. `Player.log`를 grep — `[ASSERT]` 로그를 미리 심어두면 원인 좁히기가 빠르다

⚠️ 새 경로로 처음 실행하면 Windows 방화벽 팝업이 뜬다(개발 빌드의 프로파일러 소켓).
보안 대화상자이므로 **직접 누르지 말고 사용자에게 맡긴다.**

## 출처

- 2026-08-09 세션에서 실제 빌드로 재현·수정한 4종 (1~4번)
- 2026-08-12 세션에서 새로 밟은 함정 2종
  - 0번: MCP 스크린샷이 LDR이라 UI 블룸이 안 찍힘 / Linear→gamma 변환 누락
  - 1-B번: `FallingPixelStreaksFx`의 낙하 픽셀이 빌드에서만 안 빛남 → `SpriteRenderer.color`
    HDR이 정점 색으로 잘림. 밝기 8단계 머티리얼로 분리해 수정, 릴리스 빌드로 검증
- Unity 6 URP 셰이더 스트립: https://docs.unity3d.com/6000.0/Documentation/Manual/shader-variant-stripping.html

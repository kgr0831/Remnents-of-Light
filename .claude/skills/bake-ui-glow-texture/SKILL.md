# UI 스프라이트 글로우를 텍스처로 구워 넣기

## 언제 쓰나(트리거)

- 로고 · 아이콘 · 팀 UI 같은 **정적 스프라이트**를 빛나게 해달라는 요청
- 셰이더 기반 아우라(`Custom/UIAuraGlow`)가 계단·블록·사각 잘림으로 깨질 때
- "깨지지 않게" 라는 조건이 붙었을 때 — 이 방식은 구조적으로 깨질 곳이 없다

**안 쓰는 경우**: 글자가 바뀌는 텍스트, 애니메이션으로 실루엣이 변하는 것. (그건 머티리얼
유니폼 HDR + URP Bloom으로 간다 — `Custom/UIBloomBoost`)

## 왜 셰이더 대신 텍스처인가

셰이더 아우라가 이 프로젝트에서 반복해서 깨진 이유는 두 가지였다.

1. **스프라이트 UV가 0~1이 아니다.** 투명 여백이 트리밍되면 `DataUtility.GetOuterUV`가
   (0.30, 0.29)~(0.76, 0.83) 같은 값을 준다. `(uv-0.5)*expand+0.5` 식의 셰이더는 중심·배율이
   어긋나고, 실루엣 바깥 판정이 스프라이트가 아닌 **텍스처 경계** 기준이라 사각으로 잘린다.
2. **텍스처가 Compressed(DXT)** 면 알파가 4×4 블록으로 뭉개져 블러하면 블록 무늬가 남는다.

구운 텍스처는 원본 PNG를 압축 우회로 직접 디코드해 만들고 여백을 텍스처 안에 포함하므로
둘 다 원천 차단된다. `_MainTex_TexelSize` 의존도, 싱크 스크립트도 필요 없다.

## 절차(단계)

### 1. 굽기

핵심만: **원본 PNG를 `File.ReadAllBytes` + `ImageConversion.LoadImage`로 직접 디코드**한다
(임포트 압축을 우회해 원본 알파를 그대로 읽는다). 그 다음:

1. 알파를 `scale`(0.4 권장)로 박스 평균 다운샘플 — 여백 `pad`를 포함한 캔버스에
2. 근거리 · 원거리 **두 반경**으로 각각 박스블러 3패스(≈가우시안)
3. 각각 `*= (1 - 실루엣)` — 실루엣 안쪽은 본체가 덮으므로 뺀다
4. 각각 자기 최대값으로 **정규화** — 안 하면 획이 얇은 로고가 두꺼운 아이콘보다 훨씬 어둡다
5. `pow(x, falloff) * strength` 후 **스크린 합성** `1-(1-n)(1-f)`
6. **Bayer 8×8 디더** 후 8비트 알파로 양자화 — 안 하면 완만한 그라데이션에 동심원 띠가 생긴다
7. `EncodeToPNG` → 저장

```csharp
// 6번 디더가 없으면 반드시 띠가 보인다(실측)
float dith = ((bayer[(y & 7) * 8 + (x & 7)] + 0.5f) / 64f - 0.5f) / 255f;
byte a = (byte)Mathf.Clamp(Mathf.RoundToInt((Mathf.Clamp01(g) + dith) * 255f), 0, 255);
```

저해상도로 구워도 된다 — 확대되며 바이리니어가 알아서 매끈하게 만든다. `scale = 0.4` 기준
400×400 원본이 280×280 정도로 나온다.

### 2. 임포트 설정 (빠뜨리면 1번 함정으로 되돌아간다)

```csharp
ti.textureType = TextureImporterType.Sprite;
ti.spriteImportMode = SpriteImportMode.Single;
ti.mipmapEnabled = false;
ti.alphaIsTransparency = true;
ti.filterMode = FilterMode.Bilinear;
ti.wrapMode = TextureWrapMode.Clamp;
ti.textureCompression = TextureImporterCompression.Uncompressed;  // 알파 블록 노이즈 방지
var st = new TextureImporterSettings();
ti.ReadTextureSettings(st);
st.spriteMeshType = SpriteMeshType.FullRect;   // ★ 이게 없으면 트리밍돼 UV가 0~1이 아니게 된다
st.spriteExtrude = 0;
ti.SetTextureSettings(st);
ti.SaveAndReimport();
```

임포트 후 `DataUtility.GetOuterUV(sprite)`가 `(0,0,1,1)`인지 반드시 확인한다.

### 3. 씬 배치

- 글로우 이미지는 본체의 **형제**이며 **형제 순서가 더 앞**(먼저 그려짐 = 뒤에 깔림)이어야 한다
- 페이드가 있는 연출이면 본체와 **같은 CanvasGroup 안**에 둔다 — 그래야 같이 나타나고 같이 사라진다
- 쿼드 크기는 축별로 따로 계산한다 (pad는 픽셀 고정이라 가로세로 배율이 다르다):

```csharp
auraRt.sizeDelta = new Vector2(bodyRt.rect.width  * (W + 2f * pad) / W,
                               bodyRt.rect.height * (H + 2f * pad) / H);
auraRt.anchoredPosition = bodyRt.anchoredPosition;   // 앵커·피벗·스케일도 본체와 동일하게
```

- 머티리얼은 `Custom/UIBloomBoost`, **`_Boost = 1.0`**. 1을 넘기면 헤일로 자체가 블룸을 타서
  번짐이 폭주한다 — 밝기는 구운 알파(`strength`)로 조절하는 게 통제하기 쉽다.
- 본체 이미지는 **건드리지 않는다**. 톤매핑이 없으면 1을 넘는 값은 전부 흰색으로 클립돼
  로고의 색 디테일이 날아간다.

### 4. 파라미터 감(2026-08-12 Intro-cutScene 확정값)

| | pad | 근거리 R / falloff / strength | 원거리 R / falloff / strength |
|---|---|---|---|
| 팀 아이콘 400×400 (본체 635px) | 150 | 26 / 2.0 / 0.34 | 45 / 1.3 / 0.20 |
| 로고 1280×730 (본체 1083px) | 200 | 30 / 2.2 / 0.42 | 60 / 1.25 / 0.26 |

- `pad ≥ 3 × 원거리 R` — 안 그러면 헤일로가 텍스처 경계에서 잘린다
- 로고처럼 **글자 사이가 벌어진 실루엣**은 근거리 반경을 작게(획에 달라붙게) 하고 falloff를
  높인다. 크게 잡으면 글자 사이가 차올라 가독성을 먹는다.
- 모폴로지 클로징으로 "바깥만" 빛나게 하는 변형은 **하지 말 것** — 안쪽이 검은 덩어리로 남아
  더 깨져 보인다(실제로 시도했다가 되돌렸다).

## 검증(ASSERT 채널)

⚠️ **MCP 기본 스크린샷(`manage_camera screenshot`)으로 판단하면 안 된다.** LDR 타깃이라
UI의 HDR과 블룸이 안 찍혀서 "블룸이 안 걸린다"고 오판하게 된다(이 세션에서 실제로 한참 헤맸다).

HDR 렌더타깃에 직접 그려서 확인한다. Linear 컬러스페이스면 **`c.gamma` 변환 필수** —
안 하면 전체가 어둡게 나와 또 오판한다.

```csharp
var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBHalf); rt.Create();
var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
RenderTexture.active = rt;
var tex = new Texture2D(1600, 900, TextureFormat.RGBAHalf, false);
tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
// 저장 전: if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.gamma;
```

페이드 연출이 있는 씬이면 촬영을 위해 CanvasGroup 알파를 잠시 1로 올리고 **반드시 원래 값으로
되돌린다**(보통 0). 되돌리는 코드를 같은 스크립트 안에 같이 넣어 두면 잊지 않는다.

## 출처

- 2026-08-12 세션: Intro-cutScene의 TeamUI · Logo 글로우 작업에서 확정
- 함께 볼 것: `.claude/skills/diagnose-build-only-bug/SKILL.md` (LDR 스크린샷 함정 · HDR 캡처)

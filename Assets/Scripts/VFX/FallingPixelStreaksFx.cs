using UnityEngine;

// 낙하 컷신용 시안 픽셀 스트릭. 카메라 뷰 아래에서 위로 흘려보내 "플레이어가 떨어지고 있다"는
// 착각만 만든다 — 플레이어도 카메라도 실제로는 한 발짝도 움직이지 않는다.
//
// 픽셀 하나 = SpriteRenderer 1개. 개수가 고정된 풀이라 스폰·파괴가 없고, 화면 위로 벗어나면
// 화면 중앙 아래로 되돌려 재사용한다(무한 루프). 시작 프레임부터 화면이 차 있어야 하므로
// 초기 배치만 뷰 전체에 흩뿌린다.
//
// 낙하감의 정체는 "나란히 위로"가 아니라 원근이다. 그래서 세 가지를 겹친다.
//   1) 방사형 — 화면 중앙 근처에서 나와 바깥으로 벌어지며, 가장자리로 갈수록 빠르고 커진다
//      (아래로 떨어질 때 눈에 보이는 소실점 원근 그대로다)
//   2) 깊이 — depth 난수 하나가 크기·속도·밝기·길이를 한꺼번에 정한다. 따로 굴리면
//      큰데 느린 픽셀이 섞여 낙하감이 무너진다
//   3) 길이 — 빠른(가까운) 픽셀일수록 세로로 늘어나 모션 블러처럼 보인다
// 앞뒤 레이어의 속도 차도 같이 쓰면 좋다 — 이 컴포넌트를 sortingOrder만 높여(플레이어 앞) 하나 더
// 두고 크고 빠르고 반투명하게 설정하면 전경 레이어가 된다.
//
// ⚠️ 색은 HDR(1을 넘는 값)로 넣는다. Global Volume(IlseomBloomProfile)의 Bloom threshold가
//    1.15라 그보다 밝아야 번진다. 머티리얼도 반드시 Unlit — Lit이면 Global Light 2D
//    (intensity 0.25)에 눌려 어두워져서 블룸 문턱을 못 넘는다.
public class FallingPixelStreaksFx : MonoBehaviour
{
    [Header("개수")]
    public int count = 70;

    [Header("크기(월드 유닛) — 높이 = 폭 x aspect x stretch")]
    public float widthMin = 0.03125f;
    public float widthMax = 0.09375f;
    public float aspectMin = 2f;
    public float aspectMax = 4f;
    [Tooltip("가장 가까운(빠른) 픽셀의 세로 길이 배수 — 모션 블러처럼 늘어난다")]
    public float stretchMax = 2f;

    [Header("상승 속도(유닛/초)")]
    public float speedMin = 6f;
    public float speedMax = 22f;

    [Header("방사형 흐름")]
    [Tooltip("중심에서 바깥으로 벌어지는 비율(초당). 0이면 나란히 위로만 흐른다")]
    public float spreadPerSecond = 0.5f;
    [Tooltip("화면 가장자리에서 상승 속도에 더해지는 배수")]
    public float radialSpeedBoost = 0.9f;
    [Tooltip("화면 가장자리에서 크기에 더해지는 배수")]
    public float growthAtEdge = 0.7f;
    [Tooltip("클수록 화면 중앙에 몰려서 나온다(소실점). 1이면 균등")]
    public float spawnCenterBias = 3f;

    [Header("색")]
    [Tooltip("알파를 낮추면 흐릿해진다 — 전경 레이어용")]
    public Color tint = new Color(0.25f, 1f, 1f, 1f);
    [Tooltip("HDR 배수. Bloom threshold(1.15)보다 커야 빛난다")]
    public float glowMin = 1f;
    public float glowMax = 2f;

    [Header("그리기")]
    [Tooltip("플레이어(order 13)보다 낮으면 뒤로, 높으면 앞으로 흐른다")]
    public int sortingOrder = 5;
    [Tooltip("좌우로 화면 밖까지 얼마나 더 뿌릴지(유닛)")]
    public float marginX = 0.5f;

    Camera cam;
    Transform[] pixels;
    float[] speeds;
    float[] baseWidths;
    float[] baseHeights;
    Sprite sprite;
    // 밝기 단계별 머티리얼. 낱개로 MaterialPropertyBlock을 물리면 70개가 전부 개별 드로우콜이 되므로
    // 단계를 끊어 공유한다 — 늘어나는 드로우콜은 이 개수뿐이고, 눈으로는 연속과 구분이 안 된다.
    const int GlowSteps = 8;
    Material[] glowMats;

    void Start()
    {
        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[FallingPixelStreaksFx] MainCamera 태그가 붙은 카메라가 없어 연출을 건너뜁니다.", this);
            enabled = false;
            return;
        }

        BuildAssets();
        BuildPixels();
    }

    // 1x1 흰 스프라이트를 PPU 1로 만든다 — 스프라이트가 정확히 1유닛이라 localScale이 곧 월드 크기가 된다.
    void BuildAssets()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        // ⚠️ HDR을 SpriteRenderer.color로 태우면 **빌드에서만** 사라진다(사용자 리포트 2026-08-12).
        //    배칭이 per-renderer 색을 Color32 정점 색으로 구워 넣으면서 1.0에서 잘리기 때문이다.
        //    에디터는 _RendererColor(float4) 경로가 살아남아 멀쩡해 보여서 원인을 찾기 어려웠다.
        //    머티리얼 유니폼 _Color는 정점으로 안 구워지므로 1을 넘겨도 그대로 나간다.
        //    (UGUI 정점 색이 Color32라 잘리는 것과 같은 함정 — UIBloomBoost.shader 주석 참고)
        glowMats = new Material[GlowSteps];
        for (int i = 0; i < GlowSteps; i++)
        {
            Color c = tint * Mathf.Lerp(glowMin, glowMax, (i + 0.5f) / GlowSteps);
            c.a = 1f;                       // 알파는 SpriteRenderer.color 쪽에서 따로 준다
            glowMats[i] = new Material(sh);
            glowMats[i].SetColor("_Color", c);
        }
    }

    void BuildPixels()
    {
        int n = Mathf.Max(1, count);
        pixels = new Transform[n];
        speeds = new float[n];
        baseWidths = new float[n];
        baseHeights = new float[n];

        float left, right, bottom, top;
        GetViewBounds(out left, out right, out bottom, out top);
        float centerX = cam.transform.position.x;
        float halfW = (right - left) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Streak");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            float depth = Random.value;
            baseWidths[i] = Mathf.Lerp(widthMin, widthMax, depth);
            baseHeights[i] = baseWidths[i] * Random.Range(aspectMin, aspectMax) * Mathf.Lerp(1f, stretchMax, depth);
            speeds[i] = Mathf.Lerp(speedMin, speedMax, depth);

            // 색·밝기는 머티리얼이 들고 있다(BuildAssets 주석 참고) — 여기서는 알파만 준다.
            sr.sharedMaterial = glowMats[Mathf.Clamp((int)(depth * GlowSteps), 0, GlowSteps - 1)];
            sr.color = new Color(1f, 1f, 1f, tint.a);

            // 첫 프레임부터 화면이 차 있도록 뷰 전체에 흩뿌린다(이후로는 아래에서만 올라온다).
            pixels[i] = go.transform;
            pixels[i].position = new Vector3(BiasedX(centerX, halfW), Random.Range(bottom, top), 0f);
            ApplyScale(i, centerX, halfW);
        }
    }

    void Update()
    {
        float left, right, bottom, top;
        GetViewBounds(out left, out right, out bottom, out top);
        float centerX = cam.transform.position.x;
        float halfW = (right - left) * 0.5f;

        // unscaledDeltaTime은 maximumDeltaTime으로 클램프되지 않는다 — 플레이 진입·도메인 리로드 같은
        // 히치가 그대로 들어오면 한 프레임에 전부 화면 위로 밀려나 바닥에 몰린다(실측).
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        for (int i = 0; i < pixels.Length; i++)
        {
            Vector3 p = pixels[i].position;
            float dx = p.x - centerX;
            float edge01 = Mathf.Clamp01(Mathf.Abs(dx) / halfW);

            p.x += dx * spreadPerSecond * dt;                            // 중심에서 멀수록 더 벌어진다
            p.y += speeds[i] * (1f + edge01 * radialSpeedBoost) * dt;    // 가장자리일수록 빠르다

            float halfH = baseHeights[i] * (1f + edge01 * growthAtEdge) * 0.5f;
            if (p.y - halfH > top)
            {
                p.x = BiasedX(centerX, halfW);   // 다시 중앙 근처(소실점)에서 태어난다
                p.y = bottom - halfH;
            }

            pixels[i].position = p;
            ApplyScale(i, centerX, halfW);
        }
    }

    // 가장자리로 갈수록 커진다 — 가까워지는 것처럼 보이게 하는 원근의 나머지 반쪽.
    void ApplyScale(int i, float centerX, float halfW)
    {
        float edge01 = Mathf.Clamp01(Mathf.Abs(pixels[i].position.x - centerX) / halfW);
        float k = 1f + edge01 * growthAtEdge;
        pixels[i].localScale = new Vector3(baseWidths[i] * k, baseHeights[i] * k, 1f);
    }

    // 중앙에 몰린 분포. 균등 난수를 홀수 거듭제곱해 부호를 유지한 채 0쪽으로 끌어당긴다.
    float BiasedX(float centerX, float halfW)
    {
        float u = Random.value * 2f - 1f;
        float biased = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), Mathf.Max(1f, spawnCenterBias));
        return centerX + biased * halfW;
    }

    void GetViewBounds(out float left, out float right, out float bottom, out float top)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect + marginX;
        Vector3 c = cam.transform.position;
        left = c.x - halfW;
        right = c.x + halfW;
        bottom = c.y - halfH;
        top = c.y + halfH;
    }

    void OnDestroy()
    {
        if (glowMats != null)
            foreach (var m in glowMats) if (m != null) Destroy(m);
        if (sprite != null)
        {
            if (sprite.texture != null) Destroy(sprite.texture);
            Destroy(sprite);
        }
    }
}

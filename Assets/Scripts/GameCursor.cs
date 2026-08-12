using UnityEngine;

/// <summary>
/// 게임 커서를 크로스헤어로 바꾼다. <see cref="GameSfx"/>와 같은 방식으로 첫 씬이 로드된 뒤 스스로
/// 적용되므로 씬마다 배선할 필요가 없다.
///
/// 텍스처는 <c>Assets/UI/Crosshairs/crosshairs Static.png</c>의 'Cursor' 스프라이트(7×7)를 4배
/// 니어리스트 업스케일해 구운 것이다 — <see cref="Cursor.SetCursor"/>는 Sprite가 아니라 Texture2D를
/// 받아서, 시트의 하위 스프라이트를 그대로 넘길 수 없기 때문이다.
/// </summary>
public static class GameCursor
{
    const string ResourcePath = "Cursor";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        var tex = Resources.Load<Texture2D>(ResourcePath);
        if (tex == null)
        {
            Debug.LogWarning($"[GameCursor] Resources/{ResourcePath} 를 찾지 못했다 — 기본 커서를 그대로 쓴다.");
            return;
        }
        // 크로스헤어는 조준점이 한가운데라 핫스팟도 정중앙이어야 한다(좌상단 기본값이면 클릭 지점이 어긋난다).
        Cursor.SetCursor(tex, new Vector2(tex.width * 0.5f, tex.height * 0.5f), CursorMode.Auto);
    }
}

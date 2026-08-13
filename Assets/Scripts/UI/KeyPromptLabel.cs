using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 키 프롬프트(ExecutionUI "R 처형" · DashUI "F 카운터 공격" · SwordUI "F 획득")의 키캡 글자를
/// 현재 바인딩으로 맞춘다. 프리팹에는 기본 키가 박혀 있어서, 설정에서 키를 바꾸면 그림이 거짓말이 된다.
///
/// 세 프리팹의 구조가 같다 — 루트 → 키캡 <see cref="Image"/> → **그 자식** <see cref="Text"/>(글자),
/// 그리고 루트 바로 아래에 설명 Text. 이름("Text (Legacy)")이 아니라 이 구조로 찾는다:
/// 이름은 언제든 바뀔 수 있지만 "키캡 안에 글자가 들어 있다"는 배치는 그 프롬프트의 정의 자체다.
///
/// 프리팹·씬을 고치지 않고 런타임에만 글자를 갈아끼우므로, 설정을 되돌리면 표시도 그대로 돌아온다.
/// </summary>
public static class KeyPromptLabel
{
    /// <summary>프롬프트를 띄울 때마다 부른다 — 일시정지 중에 키를 바꿔도 다음에 뜰 때 반영된다.</summary>
    public static void Apply(GameObject prompt, string keyText)
    {
        if (prompt == null || string.IsNullOrEmpty(keyText)) return;
        Text cap = FindKeyCapText(prompt);
        if (cap != null) cap.text = keyText;
    }

    static Text FindKeyCapText(GameObject prompt)
    {
        Image[] images = prompt.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Text inside = images[i].GetComponentInChildren<Text>(true);
            // 키캡 자신에 붙은 Text가 아니라 **자식** Text여야 한다(설명 문구는 키캡 밖에 있다).
            if (inside != null && inside.transform.parent == images[i].transform) return inside;
        }
        return null;
    }
}

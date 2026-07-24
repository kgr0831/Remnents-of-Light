using UnityEngine;
using TMPro;

// UniTrio-Game-2026(Assets/Scripts/Player/Weapon/DamageText.cs)에서 이식.
// 원본의 SimpleObjectPool 반환·ElementalWeaponSystem 색상 연동은 이 프로젝트에 해당 시스템이 없어 제거.
// (자가 파괴는 DashAfterImage.cs/HitVfxAutoReturn.cs와 동일한 이 프로젝트 컨벤션)
[RequireComponent(typeof(TextMeshPro))]
public class DamageText : MonoBehaviour
{
    [Header("Settings")]
    public float lifetime = 0.6f;
    public float floatSpeed = 2f;
    public Vector3 randomScatter = new Vector3(0.5f, 0.5f, 0f);

    [Header("Outline")]
    public Color outlineColor = Color.black;
    [Range(0f, 1f)] public float outlineWidth = 0.12f;

    TextMeshPro textMesh;
    Color currentColor;
    float timer;

    public void Setup(int damage, Color color)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();

        textMesh.sortingOrder = 9999;
        textMesh.outlineColor = outlineColor;
        textMesh.outlineWidth = outlineWidth;
        textMesh.text = damage.ToString();
        color.a = 1f;
        currentColor = color;
        textMesh.color = color;

        transform.position += new Vector3(
            Random.Range(-randomScatter.x, randomScatter.x),
            Random.Range(-randomScatter.y, randomScatter.y),
            0f);

        timer = 0f;
    }

    void Update()
    {
        transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

        timer += Time.deltaTime;
        currentColor.a = 1f - Mathf.Clamp01(timer / lifetime);
        if (textMesh != null) textMesh.color = currentColor;

        if (timer >= lifetime) Destroy(gameObject);
    }
}

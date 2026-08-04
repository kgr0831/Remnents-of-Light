using UnityEngine;

// 부서지는 바닥이 무너질 때 흩어지는 파편 한 조각. 런타임 전용 — 씬에 저장되지 않는다.
// 파편 에셋을 따로 만들지 않고 바닥 스프라이트를 작게 축소해 재사용한다(같은 재질의 부스러기처럼 보인다).
// 물리는 Rigidbody2D 대신 직접 적분한다 — 파편이 플레이어/지형과 충돌해 조작을 방해하면 안 되기 때문.
public class CrumbleDebris : MonoBehaviour
{
    const float Gravity = -14f;

    SpriteRenderer sr;
    Color startColor;
    Vector2 velocity;
    float spin;
    float lifetime;
    float age;

    public static void Burst(SpriteRenderer source, int count, float speed, float lifetime)
    {
        Bounds b = source.bounds;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("CrumbleDebris");
            go.transform.position = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                source.transform.position.z);
            go.transform.localScale = source.transform.lossyScale * Random.Range(0.15f, 0.3f);

            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = source.sprite;
            r.color = source.color;
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = source.sortingOrder;

            var d = go.AddComponent<CrumbleDebris>();
            d.sr = r;
            d.startColor = r.color;
            d.velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1f)).normalized
                         * speed * Random.Range(0.6f, 1.2f);
            d.spin = Random.Range(-360f, 360f);
            d.lifetime = lifetime;
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        velocity.y += Gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);

        var c = startColor;
        c.a = startColor.a * (1f - age / lifetime);
        sr.color = c;
    }
}

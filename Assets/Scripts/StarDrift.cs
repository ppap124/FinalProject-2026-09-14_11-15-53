using UnityEngine;

/// <summary>
/// 별빛 수로의 별이 천천히 흘러가게 한다.
///
/// 재질을 직접 건드리지 않고 `MaterialPropertyBlock` 으로 `_BaseMap_ST` 의 오프셋만
/// 민다. URP Lit 는 발광 맵도 같은 UV 를 쓰므로 별이 같이 흐른다. 재질을 만지면
/// 에디터에서 씬이 계속 더러워지고, 플레이를 끝내도 오프셋이 남는다.
///
/// **플레이 중에만 흐른다.** 에디터에서도 돌리면 씬 뷰를 열어 둘 때마다 저장 표시가 뜬다.
/// </summary>
[DisallowMultipleComponent]
public class StarDrift : MonoBehaviour
{
    [Tooltip("반복 횟수 (x, z). MapDecor 가 수로 크기에서 넣는다")]
    public Vector2 tiling = Vector2.one;

    [Tooltip("초당 흐르는 양 (텍스처 한 장 = 1)")]
    public Vector2 velocity = new Vector2(0.004f, 0.010f);

    Renderer r;
    MaterialPropertyBlock mpb;

    void OnEnable()
    {
        r = GetComponent<Renderer>();
        Apply(Vector2.zero);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        Apply(velocity * Time.time);
    }

    void Apply(Vector2 offset)
    {
        if (r == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetVector("_BaseMap_ST", new Vector4(tiling.x, tiling.y, offset.x % 1f, offset.y % 1f));
        r.SetPropertyBlock(mpb);
    }
}

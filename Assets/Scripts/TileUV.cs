using UnityEngine;

/// <summary>
/// 타일 반복 횟수를 **씬에 남긴다.**
///
/// `MapDecor.TileTexture` 는 반복 횟수를 `MaterialPropertyBlock` 에 넣는다.
/// 재질 하나를 크기가 제각각인 바닥들이 나눠 쓰기 때문에 재질에 넣을 수가 없어서다
/// (60x60 바닥과 6x50 통로가 같은 재질이다).
///
/// **그런데 MPB 는 직렬화되지 않는다.** 씬 파일 35MB 를 뒤져도 `_BaseMap_ST` 가
/// 한 번도 안 나온다. 그래서 도메인 리로드(스크립트 컴파일, 플레이 진입)나
/// 씬을 다시 열 때마다 반복 횟수가 통째로 날아가고, 바닥이 1배로 —
/// 무늬 하나가 배치 구역 전체에 늘어난 채로 — 보인다.
///
/// `MapDecor.Build()` 는 손으로 눌러야만 도는 물건이라(Start/OnEnable 도,
/// 부르는 다른 코드도 없다) 아무도 그걸 되돌려 주지 않았다. 즉 **게임이 실제로
/// 보여주던 건 늘어난 쪽**이었다.
///
/// 이 컴포넌트가 그 값을 필드로 들고 있다가 `OnEnable` 에서 다시 넣는다.
/// 필드는 씬에 저장되므로 리로드를 넘어 살아남는다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
[DisallowMultipleComponent]
public class TileUV : MonoBehaviour
{
    [Tooltip("타일 반복. (x반복, y반복, x오프셋, y오프셋) — _BaseMap_ST 그대로")]
    public Vector4 st = new Vector4(1f, 1f, 0f, 0f);

    void OnEnable()  { Apply(); }
    void OnValidate() { Apply(); }

    public void Apply()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;

        // 남이 넣어 둔 다른 속성을 지우지 않게 기존 블록을 읽어서 덮어쓴다
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetVector("_BaseMap_ST", st);
        r.SetPropertyBlock(mpb);
    }
}

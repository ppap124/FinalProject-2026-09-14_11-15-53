using UnityEngine;

/// <summary>
/// 순환 경로. 자식 오브젝트(WP_0 ~ WP_3)를 순서대로 웨이포인트로 쓴다.
/// 마지막 지점에 닿으면 0번으로 돌아간다.
/// </summary>
public class PathRoute : MonoBehaviour
{
    [Tooltip("비워두면 자식 오브젝트를 순서대로 자동 수집한다.")]
    public Transform[] points;

    public int Count => points != null ? points.Length : 0;

    void Awake()
    {
        Collect();
    }

    void Collect()
    {
        if (points != null && points.Length > 0) return;

        points = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            points[i] = transform.GetChild(i);
    }

    public Vector3 GetPoint(int index)
    {
        if (Count == 0) return transform.position;
        return points[((index % Count) + Count) % Count].position;
    }

    /// <summary>
    /// 길 가운데선에서 `lane` 만큼 바깥(+)·안쪽(-)으로 비킨 줄의 웨이포인트.
    /// 길이 넓어서 몹이 한 줄로 걷지 않고 여러 줄로 퍼진다. 고리가 축에 나란한 사각이라
    /// 중심에서 축마다 늘리면 네 변이 모두 같은 거리만큼 평행하게 비킨다 (모서리도 맞는다)
    /// </summary>
    public Vector3 GetPoint(int index, float lane)
    {
        Vector3 p = GetPoint(index);
        if (Mathf.Abs(lane) < 0.001f || Count < 2) return p;
        Extents();
        Vector3 d = p - center;
        float kx = half.x > 0.01f ? (half.x + lane) / half.x : 1f;
        float kz = half.y > 0.01f ? (half.y + lane) / half.y : 1f;
        return new Vector3(center.x + d.x * kx, p.y, center.z + d.z * kz);
    }

    Vector3 center;
    Vector2 half;
    bool extentsReady;

    void Extents()
    {
        if (extentsReady) return;
        Vector3 min = GetPoint(0), max = min;
        for (int i = 1; i < Count; i++) { Vector3 q = GetPoint(i); min = Vector3.Min(min, q); max = Vector3.Max(max, q); }
        center = (min + max) * 0.5f;
        half = new Vector2((max.x - min.x) * 0.5f, (max.z - min.z) * 0.5f);
        extentsReady = true;
    }

    // 에디터에서 경로를 선으로 그려준다
    void OnDrawGizmos()
    {
        if (points == null || points.Length < 2)
        {
            if (transform.childCount < 2) return;
            Collect();
        }

        Gizmos.color = Color.cyan;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            Transform next = points[(i + 1) % points.Length];
            if (next == null) continue;

            Gizmos.DrawLine(points[i].position, next.position);
            Gizmos.DrawSphere(points[i].position, 0.5f);
        }
    }
}

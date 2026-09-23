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

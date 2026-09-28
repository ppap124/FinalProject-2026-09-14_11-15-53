using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제목 글자에 위→아래 명암을 넣는다. 금박을 위에서 비춘 것처럼 위가 밝고 아래가 짙다.
///
/// 색을 **바꾸지 않고 곱한다** — 승리(금)·패배(붉은색)처럼 글자 색이 상황마다 바뀌어도
/// 명암만 그대로 따라간다.
/// </summary>
[RequireComponent(typeof(Text))]
public class TitleGradient : BaseMeshEffect
{
    [Tooltip("글자 윗부분 밝기 배율")]
    public float top = 1.18f;

    [Tooltip("글자 아랫부분 밝기 배율")]
    public float bottom = 0.72f;

    readonly List<UIVertex> verts = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        verts.Clear();
        vh.GetUIVertexStream(verts);

        // 줄마다가 아니라 글자마다 칠한다 — 여러 줄이면 아래 줄 전체가 어두워지므로.
        // 글자 하나 = 삼각형 둘 = 정점 여섯
        for (int g = 0; g + 5 < verts.Count; g += 6)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = g; i < g + 6; i++) { float y = verts[i].position.y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            float span = Mathf.Max(0.001f, hi - lo);

            for (int i = g; i < g + 6; i++)
            {
                UIVertex v = verts[i];
                float k = Mathf.Lerp(bottom, top, (v.position.y - lo) / span);
                Color c = v.color;
                v.color = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
                verts[i] = v;
            }
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}

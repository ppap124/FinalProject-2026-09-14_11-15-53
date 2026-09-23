using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적이 도는 길 위에 **진행 방향 화살표(＞)** 를 깔고, 빛이 그 방향으로 흘러가게 한다.
///
/// 원랜디에서 가장 중요한 정보는 적이 어디로 도는가인데, 길과 배치 구역이 둘 다
/// 어두운 바닥에 **같은 청록으로** 빛나서 경계선 하나로만 갈렸다. 적이 나오기 전까지
/// 길을 알 수 없었다. 화살표가 방향을 말해 주고, 흐르는 빛이 "여기가 움직이는 곳"
/// 임을 말해 준다.
///
/// **화살표는 씬에 저장하지 않는다** (`HideFlags.DontSave`). 켜질 때마다 경로에서
/// 다시 만든다 — 경로 점을 옮기면 화살표가 저절로 따라온다. 밝기는
/// `MaterialPropertyBlock` 으로 매 프레임 넣으므로 직렬화될 필요도 없다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(PathRoute))]
public class RouteArrows : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("화살표 사이 간격(월드)")]
    public float spacing = 4.5f;

    [Tooltip("화살표 폭 — 길 폭(6)보다 좁게")]
    public float width = 2.4f;

    [Tooltip("화살표 앞뒤 길이")]
    public float depth = 1.1f;

    [Tooltip("팔 두께")]
    public float thickness = 0.32f;

    [Tooltip("모서리에서 이만큼은 비운다 — 꺾이는 자리에 겹쳐 놓이면 방향이 헷갈린다")]
    public float cornerGap = 3.2f;

    [Tooltip("길 윗면 높이. 길(`Road_*`) 윗면이 0.15 다")]
    public float surfaceY = 0.16f;

    [Header("빛")]
    [Tooltip("**청록이 아니라 금빛이다.** 청록은 배치 구역 문양과 규칙 표시가 쓰고 있어서, " +
             "길까지 청록이면 다시 한 덩어리가 된다. 금은 성벽 테두리와 같은 계열이라 " +
             "팔레트를 안 깬다")]
    public Color color = new Color(1f, 0.62f, 0.22f, 1f);

    [Tooltip("평소 밝기")]
    public float baseIntensity = 0.55f;

    [Tooltip("흐르는 빛이 지나갈 때 밝기")]
    public float peakIntensity = 2.2f;

    [Tooltip("흐르는 빛의 속도(월드/초) — 적보다 조금 빠르게")]
    public float flowSpeed = 9f;

    [Tooltip("흐르는 빛 사이 간격(월드)")]
    public float flowWavelength = 22f;

    readonly List<Renderer> arrows = new List<Renderer>();
    readonly List<float> along = new List<float>();   // 경로 시작점에서 잰 거리
    MaterialPropertyBlock mpb;
    Material mat;
    Mesh mesh;
    PathRoute route;

    void OnEnable()  { Rebuild(); }
    void OnDisable() { Clear(); }
    // OnValidate 안에서 오브젝트를 바로 지우면 유니티가 막는다. 한 박자 미룬다
    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
#endif
    }

    public void Rebuild()
    {
        Clear();
        route = GetComponent<PathRoute>();
        if (route == null || route.Count < 2) return;

        if (mesh == null) mesh = BuildChevron();
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(sh);
            mat.name = "RouteArrow";
            mat.hideFlags = HideFlags.DontSave;
            // 위에서만 보이는 판이라 감김 방향에 기대지 않는다 — 양면으로 그린다
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        }

        float total = 0f;
        int n = route.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = route.GetPoint(i), b = route.GetPoint((i + 1) % n);
            a.y = b.y = 0f;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) continue;
            Vector3 dir = d / len;

            for (float s = cornerGap; s <= len - cornerGap + 0.001f; s += spacing)
            {
                GameObject g = new GameObject("Arrow");
                g.hideFlags = HideFlags.DontSave;
                g.transform.SetParent(transform, false);
                g.transform.position = new Vector3(a.x + dir.x * s, surfaceY, a.z + dir.z * s);
                g.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                arrows.Add(r);
                along.Add(total + s);
            }
            total += len;
        }
        Tick(0f);
    }

    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name != "Arrow") continue;
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
        arrows.Clear();
        along.Clear();
    }

    void Update()
    {
        Tick(Application.isPlaying ? Time.time : 0f);
    }

    void Tick(float t)
    {
        if (arrows.Count == 0) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        float wl = Mathf.Max(1f, flowWavelength);

        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] == null) continue;
            // 진행 방향으로 흘러가는 빛. 뾰족하게 — 넓게 퍼지면 그냥 깜박임으로 보인다
            float ph = (along[i] - t * flowSpeed) / wl;
            float w = 0.5f + 0.5f * Mathf.Cos(ph * Mathf.PI * 2f);
            w = w * w * w * w;
            float k = Mathf.Lerp(baseIntensity, peakIntensity, w);
            mpb.SetColor("_BaseColor", new Color(color.r * k, color.g * k, color.b * k, 1f));
            arrows[i].SetPropertyBlock(mpb);
        }
    }

    /// <summary>앞(+Z)을 가리키는 납작한 ＞. 팔 두 개를 끝에서 맞붙인다.</summary>
    Mesh BuildChevron()
    {
        float hw = width * 0.5f, th = thickness;
        Vector3 tip = new Vector3(0f, 0f, depth * 0.5f);
        Vector3 L = new Vector3(-hw, 0f, -depth * 0.5f), R = new Vector3(hw, 0f, -depth * 0.5f);
        Vector3 back = new Vector3(0f, 0f, -th * 1.4f);   // 안쪽 모서리를 뒤로 — 팔 두께

        Vector3[] v = { tip, L, L + back, tip + back,  tip, tip + back, R + back, R };
        int[] tri = { 0, 1, 2, 0, 2, 3,  4, 5, 6, 4, 6, 7 };
        Mesh m = new Mesh();
        m.name = "RouteChevron";
        m.hideFlags = HideFlags.DontSave;
        m.vertices = v;
        m.triangles = tri;
        // 위에서 보이도록 감김 방향과 상관없이 법선을 위로
        Vector3[] nrm = new Vector3[v.Length];
        for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
        m.normals = nrm;
        m.RecalculateBounds();
        return m;
    }
}

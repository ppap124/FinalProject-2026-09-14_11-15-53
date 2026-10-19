using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영혼 블록 통로에 **패드 쪽을 가리키는 화살표**를 깔고 빛을 패드 쪽으로 흘린다.
///
/// 통로 셋이 그냥 어두운 띠라서 블록이 텅 비어 보였고, 영혼을 어디로 밀어야 하는지는
/// 제단 모양으로만 짐작해야 했다. 전투장 길 화살표(`RouteArrows`)와 같은 말투로
/// "여기서 저기로" 를 보여 준다. 색은 패드마다 다르다 — 유닛 · 금화 · 재료.
///
/// `MapDecor.BuildSoul` 이 통로 자리를 넣어 준다. 화살표 자체는 씬에 저장하지 않고
/// 켜질 때마다 다시 만든다 (`RouteArrows` 와 같다).
/// </summary>
[ExecuteAlways]
public class LaneArrows : MonoBehaviour
{
    [System.Serializable]
    public class Lane
    {
        public Vector3 from, to;   // 로컬. 화살표는 from → to 를 가리킨다
        public Color color = Color.white;
    }

    public List<Lane> lanes = new List<Lane>();

    [Header("모양")]
    public float spacing = 2.6f;
    public float width = 2.6f;
    public float depth = 1.1f;
    public float thickness = 0.32f;
    [Tooltip("통로 윗면 높이. 통로(`Lane_*`) 윗면이 0.03 이다")]
    public float surfaceY = 0.05f;

    [Header("빛 — 전투장 화살표보다 약하게. 이 블록은 조작하는 곳이지 위험한 곳이 아니다")]
    public float baseIntensity = 0.35f;
    public float peakIntensity = 1.6f;
    public float flowSpeed = 6f;
    public float flowWavelength = 14f;

    readonly List<Renderer> arrows = new List<Renderer>();
    readonly List<float> along = new List<float>();
    readonly List<Color> tints = new List<Color>();
    MaterialPropertyBlock mpb;
    Material mat;
    Mesh mesh;

    void OnEnable()  { Rebuild(); }
    void OnDisable() { Clear(); }

    public void Rebuild()
    {
        Clear();
        if (lanes.Count == 0) return;

        if (mesh == null) mesh = RouteArrows.Chevron(width, depth, thickness);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.name = "LaneArrow";
            mat.hideFlags = HideFlags.DontSave;
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        }

        foreach (Lane l in lanes)
        {
            Vector3 d = l.to - l.from; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.5f) continue;
            Vector3 dir = d / len;

            for (float s = spacing * 0.5f; s <= len; s += spacing)
            {
                GameObject g = new GameObject("Arrow");
                g.hideFlags = HideFlags.DontSave;
                g.transform.SetParent(transform, false);
                g.transform.localPosition = new Vector3(l.from.x + dir.x * s, surfaceY, l.from.z + dir.z * s);
                g.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                arrows.Add(r);
                along.Add(s);
                tints.Add(l.color);
            }
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
        tints.Clear();
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
            // 패드 쪽으로 흘러가는 빛 — RouteArrows 와 같은 뾰족한 물결
            float ph = (along[i] - t * flowSpeed) / wl;
            float w = 0.5f + 0.5f * Mathf.Cos(ph * Mathf.PI * 2f);
            w = w * w * w * w;
            float k = Mathf.Lerp(baseIntensity, peakIntensity, w);
            Color c = tints[i];
            mpb.SetColor("_BaseColor", new Color(c.r * k, c.g * k, c.b * k, 1f));
            arrows[i].SetPropertyBlock(mpb);
        }
    }
}

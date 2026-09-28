using UnityEngine;

/// <summary>
/// 50라운드 최종 보스 **카오스**를 부품으로 조립하고 움직인다.
///
/// 카오스는 생물이 아니라 **주기 자체**다 (세계를 만들고 부수는 것, 모티브는
/// 우주의 팽창과 수축). 그래서 다른 아홉처럼 걸어오지 않는다.
///
/// **왜 생성 메시 한 덩어리로 안 했나.** 한 번 뽑아 봤고 실패했다:
/// 떨어져 있어야 할 고리들이 서로 붙어서 회색 바위 덩어리가 됐고, 핵의 빛은
/// 아예 안 들어왔다 (`emissiveFactor` 가 0). 게다가 설령 잘 나왔어도 못 썼다 —
/// 고리가 따로 돌아야 하고 핵이 맥동해야 하는데 한 덩어리로는 둘 다 안 되고,
/// **glTFast 셰이더는 `emissiveFactor` 를 `MaterialPropertyBlock` 으로 안 받아서**
/// 빛을 런타임에 조절할 방법 자체가 없다.
///
/// 그래서 **빛나는 핵만 URP 구체로 직접 만들고**, 고리·세계·파편은 생성 부품을
/// 코드로 배치한다. 부품은 어둡게 뽑았다 — 핵의 빛이 살아야 해서다.
/// </summary>
public class ChaosBoss : MonoBehaviour
{
    [Header("부품")]
    public GameObject ringSegment;
    public GameObject worldSphere;
    public GameObject rubble;

    [Header("생김새")]
    [Tooltip("전체 지름. 보스 크기에 맞춰 넣는다")]
    public float diameter = 5.5f;

    [Tooltip("핵 지름 — 전체 대비 비율")]
    [Range(0.1f, 0.8f)] public float coreRatio = 0.20f;

    [Tooltip("고리 겹 수")]
    [Range(1, 5)] public int ringCount = 3;

    [Tooltip("고리 한 겹의 조각 수. 조각이 30도쯤 휘어 있어 12개면 한 바퀴다")]
    [Range(4, 24)] public int segmentsPerRing = 16;

    [Tooltip("고리마다 비우는 칸 수. 부서진 고리라 군데군데 뚫려 있어야 한다")]
    [Range(0, 6)] public int gapsPerRing = 3;

    // **고리는 핵 밖에서 시작해야 한다.** 처음에 0.52 로 뒀더니 수축했을 때
    // 안쪽 고리가 핵 속으로 들어가 버려서, 빛나는 공 하나만 보였다
    [Tooltip("가장 안쪽 고리 반지름 — 전체 반지름 대비. 핵 반지름보다 넉넉히 커야 한다")]
    [Range(0.4f, 1.6f)] public float ringInner = 0.88f;

    [Tooltip("가장 바깥 고리 반지름 — 전체 반지름 대비")]
    [Range(0.6f, 2f)] public float ringOuter = 1.4f;

    [Header("빛")]
    public Color coreColor = new Color(1f, 0.25f, 0.55f);

    [Tooltip("수축했을 때의 발광 세기")]
    public float emissionLow = 1.2f;

    [Tooltip("팽창했을 때의 발광 세기. 1 을 넘으면 코어가 하얗게 타고 " +
             "색은 블룸이 실어 나른다 — 영혼 구슬에서 겪은 것과 같다")]
    public float emissionHigh = 4.5f;

    // 부품을 어둡게만 두면 필드에서 검은 고철 덩어리로 읽혔다 (2026-09-28). 카오스는
    // 하나뿐이라 부품 재질을 복제해도 되므로, 겹마다 발광을 넣는다 — 안쪽은 진홍,
    // 바깥으로 갈수록 보라로 식는다 (§카오스 색: 전 대역)
    [Tooltip("가장 안쪽 고리의 발광색")]
    public Color innerGlow = new Color(1f, 0.28f, 0.42f);
    [Tooltip("가장 바깥 고리의 발광색")]
    public Color outerGlow = new Color(0.42f, 0.2f, 1f);
    [Tooltip("고리 발광 세기. 수축 때 절반, 팽창 때 1.5 배")]
    public float ringGlow = 0.7f;
    [Tooltip("핵의 점광 세기(팽창 때). 둘레 바닥과 유닛을 물들여 거리가 읽힌다")]
    public float lightIntensity = 7f;

    [Header("맥동 — 팽창과 수축")]
    [Tooltip("한 주기에 걸리는 초")]
    public float period = 4.5f;

    [Tooltip("수축했을 때 고리 반지름 배율")]
    [Range(0.3f, 1f)] public float contract = 0.78f;

    [Tooltip("고리가 도는 속도(초당 도). 겹마다 부호가 뒤집혀 서로 반대로 돈다")]
    public float spinSpeed = 14f;

    Transform core;
    Material coreMat;
    Transform[] rings;
    Light coreLight;
    readonly System.Collections.Generic.List<Material> made = new System.Collections.Generic.List<Material>();
    System.Collections.Generic.List<Material>[] ringMats;
    Color[] ringBase;

    void Start()
    {
        Build();
    }

    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }

        foreach (Material m in made)
        {
            if (m == null) continue;
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
        }
        made.Clear();

        float R = diameter * 0.5f;

        // ── 핵 ──
        // URP 구체다. 생성 부품을 안 쓴 이유는 이것만이 런타임에 발광을 바꿀 수 있어서다
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Core";
        Strip(orb);
        orb.transform.SetParent(transform, false);
        orb.transform.localScale = Vector3.one * (diameter * coreRatio);

        coreMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        coreMat.SetColor("_BaseColor", coreColor * 0.35f);
        coreMat.SetFloat("_Smoothness", 0.25f);
        coreMat.EnableKeyword("_EMISSION");
        Renderer orbR = orb.GetComponent<Renderer>();
        orbR.sharedMaterial = coreMat;
        orbR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        core = orb.transform;
        made.Add(coreMat);

        coreLight = new GameObject("CoreLight").AddComponent<Light>();
        coreLight.transform.SetParent(transform, false);
        coreLight.type = LightType.Point;
        coreLight.color = coreColor;
        coreLight.range = diameter * 2.6f;
        coreLight.shadows = LightShadows.None;

        // ── 고리 ──
        rings = new Transform[ringCount];
        ringMats = new System.Collections.Generic.List<Material>[ringCount];
        ringBase = new Color[ringCount];

        for (int r = 0; r < ringCount; r++)
        {
            GameObject ring = new GameObject("Ring_" + r);
            ring.transform.SetParent(transform, false);

            // 겹마다 다른 각도로 기울인다 — 같은 평면에 두면 토성 고리가 된다
            ring.transform.localRotation = Quaternion.Euler(24f + r * 33f, r * 47f, r * 19f);

            float t = ringCount == 1 ? 1f : r / (float)(ringCount - 1);
            float radius = Mathf.Lerp(R * ringInner, R * ringOuter, t);
            rings[r] = ring.transform;
            ringMats[r] = new System.Collections.Generic.List<Material>();
            ringBase[r] = Color.Lerp(innerGlow, outerGlow, t) * ringGlow;
            var glowCache = new System.Collections.Generic.Dictionary<Material, Material>();

            // 비울 칸을 고른다. 황금각으로 흩어야 겹마다 같은 쪽이 안 뚫린다
            var skip = new System.Collections.Generic.HashSet<int>();
            for (int k = 0; k < gapsPerRing; k++)
                skip.Add(Mathf.FloorToInt(Frac((r * 3 + k) * 0.6180339f) * segmentsPerRing));

            for (int i = 0; i < segmentsPerRing; i++)
            {
                if (skip.Contains(i)) continue;

                float ang = i / (float)segmentsPerRing * 360f;
                Quaternion turn = Quaternion.Euler(0f, ang, 0f);
                Vector3 at = turn * new Vector3(radius, 0f, 0f);

                // 조각은 긴 축이 X 라 원의 접선 방향으로 눕혀야 이어진다
                GameObject piece = Spawn(ringSegment, ring.transform, at,
                                         turn * Quaternion.Euler(0f, 90f, 0f));
                if (piece == null) continue;

                // 한 바퀴를 조각 수로 나눈 만큼이 조각 하나의 길이다
                float arc = 2f * Mathf.PI * radius / segmentsPerRing;
                FitLongest(piece, arc * 1.06f);          // 살짝 겹쳐 실틈을 없앤다
                Glow(piece, r, glowCache);
            }

            // 세계 하나와 파편 몇 개를 고리에 박는다
            if (worldSphere != null)
            {
                float wAng = Frac(r * 0.382f) * 360f;
                Vector3 wAt = Quaternion.Euler(0f, wAng, 0f) * new Vector3(radius, 0f, 0f);
                GameObject w = Spawn(worldSphere, ring.transform, wAt, Random.rotation);
                if (w != null) { FitLongest(w, R * Mathf.Lerp(0.30f, 0.20f, t)); Glow(w, r, glowCache); }
            }

            if (rubble != null)
            {
                for (int k = 0; k < 3; k++)
                {
                    float cAng = Frac((r * 7 + k * 5) * 0.6180339f) * 360f;
                    float cRad = radius * Mathf.Lerp(0.88f, 1.12f, Frac(k * 0.37f));
                    Vector3 cAt = Quaternion.Euler(0f, cAng, 0f) * new Vector3(cRad, 0f, 0f);
                    GameObject chunk = Spawn(rubble, ring.transform, cAt, Random.rotation);
                    if (chunk != null) { FitLongest(chunk, R * 0.13f); Glow(chunk, r, glowCache); }
                }
            }
        }

        Pulse(0f);
    }

    void Update()
    {
        if (rings == null) return;

        // **주기가 곧 정체성이다.** 0 이면 수축, 1 이면 팽창
        float k = 0.5f - 0.5f * Mathf.Cos(Time.time / Mathf.Max(0.1f, period) * 2f * Mathf.PI);
        Pulse(k);

        for (int r = 0; r < rings.Length; r++)
        {
            if (rings[r] == null) continue;
            // 겹마다 반대로, 바깥일수록 느리게 — 같이 돌면 한 덩어리로 보인다
            float dir = (r % 2 == 0) ? 1f : -1f;
            float speed = spinSpeed * dir / (1f + r * 0.6f);
            rings[r].Rotate(Vector3.up, speed * Time.deltaTime, Space.Self);
        }
    }

    void Pulse(float k)
    {
        if (rings == null) return;

        float scale = Mathf.Lerp(contract, 1f, k);
        for (int r = 0; r < rings.Length; r++)
            if (rings[r] != null) rings[r].localScale = Vector3.one * scale;

        // 수축하면 응축돼 어둡고, 팽창하면 터져 나온다
        if (coreMat != null)
        {
            Color e = coreColor;
            float mx = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
            if (mx > 0.001f) e /= mx;
            coreMat.SetColor("_EmissionColor", e * Mathf.Lerp(emissionLow, emissionHigh, k));
        }

        // 고리도 같이 숨쉰다 — 수축 때 식고 팽창 때 달아오른다
        if (ringMats != null)
            for (int r = 0; r < ringMats.Length; r++)
            {
                if (ringMats[r] == null) continue;
                Color c = ringBase[r] * Mathf.Lerp(0.5f, 1.5f, k);
                foreach (Material m in ringMats[r]) if (m != null) m.SetColor("emissiveFactor", c);
            }

        if (coreLight != null) coreLight.intensity = lightIntensity * Mathf.Lerp(0.35f, 1f, k);

        // 핵은 고리와 **반대로** 움직인다 — 고리가 좁혀들 때 눌려 부푸는 것처럼
        if (core != null)
            core.localScale = Vector3.one * (diameter * coreRatio * Mathf.Lerp(1.08f, 0.92f, k));
    }

    GameObject Spawn(GameObject prefab, Transform parent, Vector3 at, Quaternion rot)
    {
        if (prefab == null) return null;
        GameObject g = Instantiate(prefab, parent);
        g.transform.localPosition = at;
        g.transform.localRotation = rot;
        Strip(g);
        return g;
    }

    /// <summary>
    /// 부품에 그 겹의 발광 재질을 입힌다. 원본 재질마다 복제 하나를 겹 안에서 나눠 쓴다.
    /// **블록이 아니라 재질 자체를 바꾼다** — glTFast 셰이더는 `emissiveFactor` 를
    /// 프로퍼티 블록으로 안 받는다. 발광 텍스처가 비어 있으면 검정으로 곱해지므로 흰색을 채우고,
    /// `_EMISSIVE` 키워드를 켠다.
    /// 그림자도 끈다 — 떠 있는 고리 그림자가 바닥에 어지럽게 깔렸다
    /// </summary>
    void Glow(GameObject piece, int ring, System.Collections.Generic.Dictionary<Material, Material> cache)
    {
        foreach (Renderer rr in piece.GetComponentsInChildren<Renderer>(true))
        {
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material src = rr.sharedMaterial;
            if (src == null || !src.HasProperty("emissiveFactor")) continue;

            Material m;
            if (!cache.TryGetValue(src, out m))
            {
                m = new Material(src);
                m.EnableKeyword("_EMISSIVE");   // 이게 없으면 glTF 셰이더가 발광을 통째로 건너뛴다 (MapDecor 와 같다)
                if (m.HasProperty("emissiveTexture") && m.GetTexture("emissiveTexture") == null)
                    m.SetTexture("emissiveTexture", Texture2D.whiteTexture);
                m.SetColor("emissiveFactor", ringBase[ring]);
                cache[src] = m;
                ringMats[ring].Add(m);
                made.Add(m);
            }
            rr.sharedMaterial = m;
        }
    }

    void OnDestroy()
    {
        foreach (Material m in made) if (m != null) Destroy(m);
    }

    /// <summary>가장 긴 축을 `want` 에 맞춘다. 부품마다 원본 크기가 달라서 필요하다.</summary>
    static void FitLongest(GameObject g, float want)
    {
        Bounds b;
        if (!TryWorldBounds(g, out b)) return;
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f) g.transform.localScale *= want / longest;
    }

    static void Strip(GameObject g)
    {
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
    }

    static bool TryWorldBounds(GameObject g, out Bounds b)
    {
        b = new Bounds(g.transform.position, Vector3.zero);
        Renderer[] rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    static float Frac(float v) { return v - Mathf.Floor(v); }
}

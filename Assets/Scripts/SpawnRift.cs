using UnityEngine;

/// <summary>
/// 몹 출발점의 **차원의 균열**. 몹은 이 틈 아래에서 솟아오른다 (`Monster.Emerge`).
///
/// 두 겹이다:
///   · 그을음 — 틈 둘레 돌이 검붉게 탄 자국 (알파 혼합, 그림의 밝기를 알파로)
///   · 빛     — 틈 안에서 새는 진홍빛 (가산 혼합, 검정은 투명)
/// 빛은 천천히 숨쉬고, 라운드가 시작되면 확 밝아지고, 몹이 나올 때마다 불티가 튄다.
///
/// 조각은 코드로 만들고 **씬에 저장하지 않는다**(DontSave) — 코드로 만든 재질은 씬에 못 담겨서
/// 저장했다 다시 열면 분홍이 된다. 편집 중엔 미리보기로, 플레이하면 새로 만든다.
/// </summary>
[ExecuteAlways]
public class SpawnRift : MonoBehaviour
{
    public static SpawnRift Instance { get; private set; }

    [Tooltip("검은 바탕 + 빛나는 틈. 알파가 밝기로 채워져 있어야 그을음 겹이 산다")]
    public Texture2D texture;
    [Tooltip("지름(월드)")]
    public float size = 7f;

    public Color glowColor = new Color(1f, 0.25f, 0.45f);
    [Tooltip("평소 빛 세기 (숨쉬는 폭의 아래 · 위)")]
    public Vector2 glow = new Vector2(1.1f, 2.0f);
    public float breathePeriod = 3.2f;
    [Tooltip("라운드 시작 · 보스 등장 때 치솟는 세기")]
    public float flarePeak = 6f;
    [Tooltip("그을음 진하기")]
    [Range(0f, 1f)] public float scorch = 0.85f;

    Material glowMat, scorchMat;
    Light lamp;
    float flareAt = -10f, flareAmp;
    bool subscribed;

    void OnEnable()
    {
        Instance = this;
        Rebuild();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        if (subscribed && GameLoop.Instance != null) GameLoop.Instance.RoundStarted -= OnRound;
        subscribed = false;
    }

    void Start()
    {
        if (Application.isPlaying && GameLoop.Instance != null && !subscribed)
        {
            GameLoop.Instance.RoundStarted += OnRound;
            subscribed = true;
        }
    }

    void OnRound(int round)
    {
        GameLoop gl = GameLoop.Instance;
        bool big = gl != null && (gl.IsFinalRound || gl.IsBossRound(round));
        Flare(big ? 1.6f : 1f);
    }

    /// <summary>틈을 확 밝힌다. 1 = 라운드 시작</summary>
    public void Flare(float amp)
    {
        flareAt = Time.time;
        flareAmp = Mathf.Max(amp, flareAmp * Fade());
    }

    float Fade() => Mathf.Clamp01(1f - (Time.time - flareAt) / 1.4f);

    /// <summary>몹 한 마리가 틈에서 나왔다 — 불티 + 작은 번쩍임</summary>
    public static void Emerge(Vector3 at, bool boss)
    {
        if (Instance == null) return;
        SkillFx.Sparks(new Vector3(at.x, 0.1f, at.z), Instance.glowColor, boss ? 60 : 14, boss ? 9f : 5f, boss ? 1.2f : 0.7f);
        Instance.Flare(boss ? 1.3f : 0.35f);
    }

    public void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        if (texture == null) return;

        scorchMat = Mat(false);
        glowMat = Mat(true);
        Quad("Scorch", size * 1.12f, 0.00f, scorchMat);
        Quad("Glow", size, 0.02f, glowMat);

        lamp = new GameObject("RiftLight").AddComponent<Light>();
        lamp.gameObject.hideFlags = HideFlags.DontSave;
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = Vector3.up * 1.2f;
        lamp.type = LightType.Point;
        lamp.color = glowColor;
        lamp.range = size * 1.3f;
        lamp.shadows = LightShadows.None;
        Tick();
    }

    void Quad(string name, float s, float y, Material m)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.hideFlags = HideFlags.DontSave;
        if (Application.isPlaying) Destroy(q.GetComponent<Collider>()); else DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.localPosition = Vector3.up * y;
        q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힌다
        q.transform.localScale = new Vector3(s, s, 1f);
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    Material Mat(bool additive)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.hideFlags = HideFlags.DontSave;
        m.SetTexture("_BaseMap", texture);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetInt("_SrcBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
        m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + (additive ? 1 : 0);
        // 그을음은 색이 아니라 어둠이다 — 흰 그림에 검붉은 색을 곱해 알파만큼 덮는다
        if (!additive) m.SetColor("_BaseColor", new Color(0.06f, 0.0f, 0.025f, scorch));
        return m;
    }

    void Update() { Tick(); }

    void Tick()
    {
        if (glowMat == null) return;
        float k = 0.5f - 0.5f * Mathf.Cos(Time.time / Mathf.Max(0.1f, breathePeriod) * Mathf.PI * 2f);
        float g = Mathf.Lerp(glow.x, glow.y, k);
        float f = Application.isPlaying ? flareAmp * Fade() * Fade() : 0f;
        g += (flarePeak - glow.y) * f;
        glowMat.SetColor("_BaseColor", glowColor * g);
        if (lamp != null) lamp.intensity = 1.5f * g;
    }

    void OnDestroy()
    {
        if (glowMat != null) { if (Application.isPlaying) Destroy(glowMat); else DestroyImmediate(glowMat); }
        if (scorchMat != null) { if (Application.isPlaying) Destroy(scorchMat); else DestroyImmediate(scorchMat); }
    }
}

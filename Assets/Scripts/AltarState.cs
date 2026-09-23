using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한복판 제단이 **게임 상태를 보여 주는 판**이 되게 한다.
///
/// 제단은 화면 한가운데서 가장 크게 보이는 물건인데 지금까지는 장식이었다.
/// 원랜디에서 제일 중요한 숫자는 "필드에 몇 마리가 쌓였나(100이면 패배)"인데,
/// 그걸 보려면 화면 구석 HUD 를 읽어야 했다. 이제 제단을 보면 된다.
///
/// - **위험 고리.** 제단 둘레 바닥에 50칸. 한 칸이 2마리, 100마리에서 가득 찬다.
///   북쪽에서 시계 방향으로 찬다. 50% 까지는 제단과 같은 파랑, 그 위는 주황,
///   80% 위는 빨강으로 빠르게 맥동한다.
/// - **제단 빛.** 제단 위 마법진 파티클과 제단 조명이 같은 색을 따라간다.
///   가운데가 빨개지면 게이지를 안 읽어도 위험을 안다.
/// - **라운드 시작.** 빛이 솟구쳤다가 가라앉는다. 보스 라운드는 보라.
/// - **준비 시간.** 흐리게 천천히 숨쉰다 — "지금은 쉬는 중".
///
/// 고리와 조명은 **씬에 저장하지 않는다** (`HideFlags.DontSave`). 켜질 때마다
/// 다시 만든다. 밝기는 `MaterialPropertyBlock` 으로 매 프레임 넣는다.
/// </summary>
[ExecuteAlways]
public class AltarState : MonoBehaviour
{
    [Header("위험 고리")]
    [Tooltip("칸 수. 필드 한계(100)를 나눠 떨어지게 — 50이면 한 칸에 2마리")]
    public int segments = 50;

    [Tooltip("고리 안쪽 반지름. 제단(지름 11)의 반지름 5.5 보다 바깥")]
    public float innerRadius = 6.3f;

    [Tooltip("고리 폭")]
    public float width = 0.7f;

    [Tooltip("10칸마다(20마리) 눈금을 이만큼 바깥으로 길게 뺀다")]
    public float majorTick = 0.35f;

    [Tooltip("칸 사이 틈(도)")]
    public float gapDegrees = 1.6f;

    [Tooltip("바닥 높이. 배치 구역 판 윗면이 0.03 이다")]
    public float surfaceY = 0.06f;

    [Header("색")]
    [Tooltip("평온 — 제단 불꽃과 같은 파랑")]
    public Color calm = new Color(0.30f, 0.66f, 1f);

    [Tooltip("절반을 넘으면. **길 화살표 금색(1, 0.62, 0.22)과 떨어뜨린다** — 번짐이 " +
             "먹으면 주황이 노랗게 떠서 화살표와 한 색으로 읽힌다")]
    public Color warn = new Color(1f, 0.26f, 0.06f);

    [Tooltip("80% 를 넘으면")]
    public Color danger = new Color(1f, 0.10f, 0.08f);

    [Tooltip("보스 라운드")]
    public Color boss = new Color(0.72f, 0.30f, 1f);

    [Range(0f, 1f)] public float warnAt = 0.5f;
    [Range(0f, 1f)] public float dangerAt = 0.8f;

    [Header("밝기")]
    [Tooltip("빈 칸 밝기 — 트랙이 있다는 것만 보이게")]
    public float emptyIntensity = 0.32f;

    [Tooltip("찬 칸 밝기")]
    public float fillIntensity = 1.4f;

    [Tooltip("라운드 시작 때 솟구치는 배율")]
    public float surgeBoost = 2.2f;

    [Tooltip("솟구친 빛이 가라앉는 시간(초)")]
    public float surgeFade = 1.6f;

    [Header("제단 조명")]
    public float lightHeight = 4.2f;
    public float lightIntensity = 5f;
    public float lightRange = 15f;

    [Tooltip("이 이름의 파티클을 상태 색으로 물들인다")]
    public string sealEffectName = "SealEffect";

    [Tooltip("물들여도 색이 안 바뀌는 파티클 — 셰이더가 파티클 색을 무시하고 텍스처 " +
             "청록만 그린다. 빨간 제단 위에 청록 연기가 한 줄 남았다. 위험할수록 끈다")]
    public string[] fadeWhenAlarmed = { "Aura_02" };

    [Header("에디터 미리보기")]
    [Range(0f, 1f)]
    [Tooltip("플레이 중이 아닐 때 보여 줄 채움 정도")]
    public float previewFill = 0.36f;

    // ── 만든 것 ──
    readonly List<Renderer> cells = new List<Renderer>();
    Material mat;
    Light lamp;
    MaterialPropertyBlock mpb;

    // ── 파티클 원래 값 ──
    struct PsOrig { public ParticleSystem ps; public ParticleSystem.MinMaxGradient color; public float rate; public bool textured; }

    static float Saturation(ParticleSystem.MinMaxGradient g)
    {
        float h, s, v, s2 = 0f;
        if (g.mode == ParticleSystemGradientMode.Color) { Color.RGBToHSV(g.color, out h, out s, out v); return s; }
        if (g.mode != ParticleSystemGradientMode.TwoColors) return 1f;   // 그라디언트는 건드리지 않는다
        Color.RGBToHSV(g.colorMin, out h, out s, out v);
        Color.RGBToHSV(g.colorMax, out h, out s2, out v);
        return Mathf.Max(s, s2);
    }
    readonly List<PsOrig> particles = new List<PsOrig>();

    // ── 상태 ──
    float shown;          // 화면에 보이는 채움 (부드럽게 따라간다)
    float surge;          // 1 에서 0 으로 가라앉는다
    Color current;
    GameLoop loop;

    void OnEnable()
    {
        Rebuild();
        current = calm;
        shown = Application.isPlaying ? 0f : previewFill;
    }

    void OnDisable()
    {
        if (loop != null) loop.RoundStarted -= OnRoundStarted;
        loop = null;
        RestoreParticles();
        Clear();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
#endif
    }

    public void Rebuild()
    {
        Clear();

        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.name = "AltarGauge";
            mat.hideFlags = HideFlags.DontSave;
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        }

        int n = Mathf.Max(4, segments);
        float step = 360f / n;
        for (int i = 0; i < n; i++)
        {
            bool major = i % 10 == 0;
            float outer = innerRadius + width + (major ? majorTick : 0f);

            // 북쪽(+Z)에서 시작해 시계 방향으로 — 위에서 내려다본 시계와 같은 방향
            float a0 = 90f - i * step - gapDegrees * 0.5f;
            float a1 = 90f - (i + 1) * step + gapDegrees * 0.5f;

            GameObject g = new GameObject("Gauge");
            g.hideFlags = HideFlags.DontSave;
            g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0f, surfaceY, 0f);
            g.AddComponent<MeshFilter>().sharedMesh = Sector(innerRadius, outer, a1, a0);
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            cells.Add(r);
        }

        GameObject lg = new GameObject("AltarLight");
        lg.hideFlags = HideFlags.DontSave;
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, lightHeight, 0f);
        lamp = lg.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = lightRange;
        lamp.shadows = LightShadows.None;

        Tick(Application.isPlaying ? 0f : previewFill, 0f);
    }

    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name != "Gauge" && c.name != "AltarLight") continue;
            MeshFilter mf = c.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(mf.sharedMesh); else DestroyImmediate(mf.sharedMesh);
            }
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
        cells.Clear();
        lamp = null;
    }

    void Update()
    {
        if (!Application.isPlaying) { Tick(previewFill, 0f); return; }

        if (loop == null && GameLoop.Instance != null)
        {
            loop = GameLoop.Instance;
            loop.RoundStarted += OnRoundStarted;
            GrabParticles();
        }

        float fill = 0f;
        if (loop != null) fill = Mathf.Clamp01(loop.AliveCount / (float)Mathf.Max(1, loop.fieldLimit));
        Tick(fill, Time.deltaTime);
    }

    void OnRoundStarted(int r) { surge = 1f; }

    void Tick(float fill, float dt)
    {
        if (cells.Count == 0) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();

        // 한 마리씩 툭툭 끊기지 않게 따라간다. 줄어들 때는 빨리 — 잡았다는 손맛
        float rate = fill > shown ? 5f : 9f;
        shown = dt > 0f ? Mathf.Lerp(shown, fill, 1f - Mathf.Exp(-dt * rate)) : fill;
        surge = Mathf.Max(0f, surge - dt / Mathf.Max(0.05f, surgeFade));

        bool playing = Application.isPlaying && loop != null;
        bool prep = playing && loop.InPrep;
        bool over = playing && loop.IsOver;
        bool bossNow = playing && !prep && loop.IsBossRound(loop.Round);

        // ── 상태 색 ──
        Color target;
        if (over || shown >= dangerAt) target = danger;
        else if (shown >= warnAt) target = Color.Lerp(warn, danger, (shown - warnAt) / Mathf.Max(0.01f, dangerAt - warnAt) * 0.35f);
        else if (bossNow) target = boss;
        else target = calm;
        current = dt > 0f ? Color.Lerp(current, target, 1f - Mathf.Exp(-dt * 4f)) : target;

        // ── 맥동: 위험할수록 빠르고 깊게. 준비 시간엔 느리게 숨쉰다 ──
        float t = Application.isPlaying ? Time.time : 0f;
        float speed = prep ? 0.6f : shown >= dangerAt ? 5.5f : shown >= warnAt ? 2.6f : 1.2f;
        float depth = prep ? 0.25f : shown >= dangerAt ? 0.45f : shown >= warnAt ? 0.25f : 0.12f;
        float pulse = 1f + depth * Mathf.Sin(t * speed * Mathf.PI * 2f);
        float boost = 1f + surgeBoost * surge * surge;
        float calmDim = prep ? 0.65f : 1f;

        int n = cells.Count;
        float filled = shown * n;
        for (int i = 0; i < n; i++)
        {
            if (cells[i] == null) continue;
            // 경계 칸은 비율만큼만 켠다 — 99마리와 100마리가 같아 보이지 않게
            float on = Mathf.Clamp01(filled - i);
            float k = Mathf.Lerp(emptyIntensity * boost, fillIntensity * pulse * boost * calmDim, on);
            Color c = on > 0f ? current : Color.Lerp(calm, current, 0.5f);
            mpb.SetColor("_BaseColor", new Color(c.r * k, c.g * k, c.b * k, 1f));
            cells[i].SetPropertyBlock(mpb);
        }

        if (lamp != null)
        {
            lamp.color = current;
            lamp.intensity = lightIntensity * (0.6f + 0.4f * shown) * pulse * boost * calmDim;
        }

        TintParticles(boost);
    }

    // ── 마법진 파티클 ──

    void GrabParticles()
    {
        particles.Clear();
        GameObject fx = GameObject.Find(sealEffectName);
        if (fx == null) return;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            PsOrig o;
            o.ps = ps;
            o.color = ps.main.startColor;
            o.rate = ps.emission.rateOverTimeMultiplier;
            o.textured = Saturation(o.color) < 0.1f
                      || System.Array.IndexOf(fadeWhenAlarmed, ps.name) >= 0;
            particles.Add(o);
        }
    }

    /// <summary>
    /// 원래 색에서 상태 색으로 **섞는다**. 곱하면 청록 × 빨강 이 검정이 된다.
    /// 평온할 땐 원래 색 그대로 — 제단이 원래 보기 좋던 모습이 기본이다.
    /// </summary>
    void TintParticles(float boost)
    {
        if (particles.Count == 0) return;
        float w = ColorDistance(current, calm);
        for (int i = 0; i < particles.Count; i++)
        {
            PsOrig o = particles[i];
            if (o.ps == null) continue;
            ParticleSystem.MainModule main = o.ps.main;
            ParticleSystem.MinMaxGradient g = o.color;
            float keep = 1f;

            // **색을 텍스처가 들고 있는 파티클은 물들이지 않는다.** 시작색이 흰색이면
            // 청록은 텍스처 쪽 색이라, 주황을 곱하면 청록×주황 = 탁한 초록이 된다.
            // 이런 것은 위험할수록 끈다 — 남은 파티클만 상태 색으로 탄다
            // 빨리 끈다 — 보스 보라는 평온 파랑과 가까워서 w 가 0.6 에 그치는데,
            // 그만큼만 끄면 청록 연기가 남아 보라가 묻혔다
            if (o.textured) keep = 1f - Mathf.Clamp01(w * 1.7f);
            else if (g.mode == ParticleSystemGradientMode.Color)
                main.startColor = Mix(g.color, w);
            else if (g.mode == ParticleSystemGradientMode.TwoColors)
                main.startColor = new ParticleSystem.MinMaxGradient(Mix(g.colorMin, w), Mix(g.colorMax, w));

            ParticleSystem.EmissionModule em = o.ps.emission;
            em.rateOverTimeMultiplier = o.rate * keep * Mathf.Lerp(1f, 1.8f, (boost - 1f) / Mathf.Max(0.01f, surgeBoost));
        }
    }

    /// <summary>
    /// **색상환을 보라 쪽으로 돌아서** 섞는다. RGB 로 섞으면 청록 → 주황 사이가
    /// 초록이라, 바뀌는 도중에 나온 파티클이 몇 초씩 초록 불꽃으로 남았다.
    /// 청록(0.5)에서 위로 — 파랑·보라·자홍을 지나 빨강·주황으로 간다.
    /// </summary>
    Color Mix(Color orig, float w)
    {
        float h0, s0, v0, h1, s1, v1;
        Color.RGBToHSV(orig, out h0, out s0, out v0);
        Color.RGBToHSV(current, out h1, out s1, out v1);
        if (s0 < 0.05f) h0 = h1;              // 흰색은 색상이 없다 — 목표 색상을 그대로
        if (h1 < h0) h1 += 1f;
        float h = Mathf.Repeat(Mathf.Lerp(h0, h1, w), 1f);
        Color c = Color.HSVToRGB(h, Mathf.Lerp(s0, s1, w), Mathf.Lerp(v0, Mathf.Max(v0, v1), w));
        c.a = orig.a;
        return c;
    }

    static float ColorDistance(Color a, Color b)
    {
        return Mathf.Clamp01((Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) * 0.8f);
    }

    void RestoreParticles()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            PsOrig o = particles[i];
            if (o.ps == null) continue;
            ParticleSystem.MainModule main = o.ps.main;
            main.startColor = o.color;
            ParticleSystem.EmissionModule em = o.ps.emission;
            em.rateOverTimeMultiplier = o.rate;
        }
        particles.Clear();
    }

    /// <summary>바닥에 눕힌 부채꼴 한 칸. 각도는 도, x-z 평면에서 +X 기준 반시계.</summary>
    static Mesh Sector(float r0, float r1, float a0, float a1)
    {
        const int sub = 3;
        Vector3[] v = new Vector3[(sub + 1) * 2];
        Vector3[] nrm = new Vector3[v.Length];
        for (int s = 0; s <= sub; s++)
        {
            float a = Mathf.Lerp(a0, a1, s / (float)sub) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v[s * 2] = d * r0;
            v[s * 2 + 1] = d * r1;
            nrm[s * 2] = nrm[s * 2 + 1] = Vector3.up;
        }
        int[] tri = new int[sub * 6];
        for (int s = 0; s < sub; s++)
        {
            int b = s * 2;
            tri[s * 6 + 0] = b;     tri[s * 6 + 1] = b + 1; tri[s * 6 + 2] = b + 3;
            tri[s * 6 + 3] = b;     tri[s * 6 + 4] = b + 3; tri[s * 6 + 5] = b + 2;
        }
        Mesh m = new Mesh();
        m.name = "AltarGaugeCell";
        m.hideFlags = HideFlags.DontSave;
        m.vertices = v;
        m.triangles = tri;
        m.normals = nrm;
        m.RecalculateBounds();
        return m;
    }
}

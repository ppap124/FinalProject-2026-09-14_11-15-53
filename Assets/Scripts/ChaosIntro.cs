using UnityEngine;

/// <summary>
/// 카오스 등장 연출. 50라운드에 카오스가 그냥 출발점에 켜지기만 하면 다른 보스와
/// 다를 게 없었다 — 최종전은 **도착**이 먼저 보여야 한다.
///
///   0.0초  하늘에서 출발점으로 빛기둥이 꽂힌다 · 화면이 흔들린다 · 경고 띠
///   0.2초  바닥에 충격파 두 겹, 불티가 솟는다
///   ~2.4초 카오스가 바닥에서 떠오르며 제 크기로 커진다
///   hold초 그때부터 길을 따라 움직인다
///
/// 부품은 모두 코드로 만든다 — 에셋 팩은 저장소에 못 올리므로(라이선스) 받은 사람 쪽에서
/// 빠져도 연출이 통째로 사라지지 않게 한다. `GameLoop.SpawnChaos` 가 붙인다.
/// </summary>
public class ChaosIntro : MonoBehaviour
{
    [Tooltip("이 시간(초) 동안 제자리에 머문다. 최종전 제한 시간은 이만큼 늦게 줄기 시작한다")]
    public float hold = 3.2f;

    [Tooltip("바닥에서 제 높이·제 크기까지 떠오르는 시간")]
    public float grow = 2.4f;

    public Color color = new Color(1f, 0.28f, 0.55f);

    [Header("빛기둥")]
    public float pillarHeight = 42f;
    public float pillarWidth = 3.4f;
    [Tooltip("빛기둥 발광 세기 — 가산 혼합이라 1 을 넘으면 블룸이 번진다")]
    public float pillarGlow = 3.5f;
    [Tooltip("바깥 번짐 굵기 — 기둥 폭의 배수. 크면 화면 한쪽이 통째로 분홍에 덮인다")]
    public float haloScale = 1.7f;
    [Tooltip("바깥 번짐 밝기 — 기둥 밝기 대비")]
    public float haloGlow = 0.12f;

    [Header("충격파")]
    public float shockRadius = 16f;
    public float shockTime = 1.3f;

    [Header("흔들림")]
    public float shakeHit = 0.55f;
    public float shakeRumble = 0.16f;

    [Header("알림")]
    public string title = "카오스";
    public string subtitle = "혼돈이 깨어납니다 — 제한 시간 안에 쓰러뜨리세요";

    Monster monster;
    float keepSpeed;
    Transform body;
    Vector3 bodyScale;
    float groundY, hoverY;
    float t0;
    bool started, released;

    GameObject pillar, halo;
    Material pillarMat, haloMat;
    LineRenderer[] waves;
    Material waveMat;
    Light flash;
    ParticleSystem sparks;
    Material sparkMat;
    Texture2D gradTex, dotTex;

    /// <summary>카오스를 세운 직후 부른다. 이동은 hold 동안 멈춘다</summary>
    public void Begin()
    {
        monster = GetComponent<Monster>();
        if (monster != null) { keepSpeed = monster.speed; monster.speed = 0f; }

        hoverY = transform.position.y;
        groundY = 0.3f;
        t0 = Time.time;
        started = true;

        Vector3 foot = new Vector3(transform.position.x, 0f, transform.position.z);
        BuildPillar(foot);
        BuildWaves(foot);
        BuildFlash(foot);
        BuildSparks(foot);

        if (CameraRig.Instance != null)
        {
            CameraRig.Instance.FocusOn(CameraRig.Instance.battlePoint);
            CameraRig.Instance.Shake(shakeHit, 1.1f);
        }

        GenesisHud hud = FindFirstObjectByType<GenesisHud>();
        if (hud != null) hud.Announce(title, subtitle, color, 2.6f);
    }

    void Update()
    {
        if (!started) return;
        float t = Time.time - t0;

        // 몸은 ChaosBoss.Start 가 조립한 다음 프레임부터 잡는다 — 조립 전에 줄여 두면
        // 부품 맞춤(FitLongest)이 줄어든 크기를 기준으로 재서 커졌을 때 부품이 부푼다
        if (body == null)
        {
            Transform b = transform.Find("ChaosBody");
            if (b != null && b.childCount > 0) { body = b; bodyScale = b.localScale; }
        }

        // ── 솟아오름 ──
        float g = Mathf.Clamp01((t - 0.15f) / Mathf.Max(0.01f, grow));
        float e = BackOut(g);
        if (body != null) body.localScale = bodyScale * Mathf.Max(0.02f, e);
        Vector3 p = transform.position;
        p.y = Mathf.Lerp(groundY, hoverY, Mathf.SmoothStep(0f, 1f, g));
        transform.position = p;

        // 떠오르는 동안 낮게 웅웅 울린다
        if (g < 1f && CameraRig.Instance != null && t > 1.0f) CameraRig.Instance.Shake(shakeRumble, 0.25f);

        // ── 빛기둥: 번쩍 꽂혔다가 가늘어지며 사라진다 ──
        if (pillar != null)
        {
            float w = t < 0.18f ? t / 0.18f : 1f - Mathf.Clamp01((t - 0.6f) / 2.2f);
            w = Mathf.Max(0f, w);
            pillar.transform.localScale = new Vector3(pillarWidth * w, pillarHeight * 0.5f, pillarWidth * w);
            halo.transform.localScale = new Vector3(pillarWidth * haloScale * w, pillarHeight * 0.5f, pillarWidth * haloScale * w);
            pillarMat.SetColor("_BaseColor", color * (pillarGlow * w));
            haloMat.SetColor("_BaseColor", color * (pillarGlow * haloGlow * w));
            if (w <= 0f) { Destroy(pillar); Destroy(halo); pillar = null; }
        }

        // ── 충격파 두 겹 ──
        if (waves != null)
        {
            bool alive = false;
            for (int i = 0; i < waves.Length; i++)
            {
                float k = (t - 0.15f - i * 0.28f) / shockTime;
                LineRenderer lr = waves[i];
                if (lr == null) continue;
                if (k < 0f) { lr.enabled = false; alive = true; continue; }
                if (k > 1f) { lr.enabled = false; continue; }
                alive = true;
                lr.enabled = true;
                float r = shockRadius * (1f - (1f - k) * (1f - k));   // 빠르게 퍼졌다가 느려진다
                Ring(lr, r);
                lr.widthMultiplier = Mathf.Lerp(1.1f, 0.15f, k);
                Color c = color; c.a = 1f - k;
                lr.startColor = lr.endColor = c;
            }
            if (!alive) { foreach (LineRenderer lr in waves) if (lr != null) Destroy(lr.gameObject); waves = null; }
        }

        // ── 섬광 ──
        if (flash != null)
        {
            flash.intensity = t < 0.12f ? 40f * t / 0.12f : 40f * Mathf.Exp(-(t - 0.12f) * 2.2f);
            if (t > 3f) { Destroy(flash.gameObject); flash = null; }
        }

        // ── 풀어 준다 ──
        if (!released && t >= hold)
        {
            released = true;
            if (monster != null) monster.speed = keepSpeed;
        }

        if (released && pillar == null && waves == null && flash == null && (sparks == null || !sparks.IsAlive(true)))
        {
            if (sparks != null) Destroy(sparks.gameObject);
            Destroy(this);
        }
    }

    // ── 부품 ────────────────────────────

    void BuildPillar(Vector3 foot)
    {
        gradTex = Gradient();
        pillarMat = Additive(gradTex);
        haloMat = Additive(gradTex);

        pillar = Column("ChaosPillar", foot, pillarMat);
        halo = Column("ChaosPillarHalo", foot, haloMat);
    }

    GameObject Column(string name, Vector3 foot, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.position = foot + Vector3.up * (pillarHeight * 0.5f);
        g.transform.localScale = new Vector3(0f, pillarHeight * 0.5f, 0f);
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    void BuildWaves(Vector3 foot)
    {
        waveMat = new Material(Shader.Find("Sprites/Default"));
        waves = new LineRenderer[2];
        for (int i = 0; i < waves.Length; i++)
        {
            GameObject g = new GameObject("ChaosShock_" + i);
            g.transform.position = foot + Vector3.up * 0.12f;
            LineRenderer lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 72;
            lr.sharedMaterial = waveMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;
            g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힌다
            lr.enabled = false;
            waves[i] = lr;
        }
    }

    static void Ring(LineRenderer lr, float r)
    {
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
    }

    void BuildFlash(Vector3 foot)
    {
        flash = new GameObject("ChaosFlash").AddComponent<Light>();
        flash.transform.position = foot + Vector3.up * 3f;
        flash.type = LightType.Point;
        flash.color = color;
        flash.range = 26f;
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;
    }

    void BuildSparks(Vector3 foot)
    {
        GameObject g = new GameObject("ChaosSparks");
        g.transform.position = foot + Vector3.up * 0.3f;
        g.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // 위로 뿜는다
        sparks = g.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sparks.main;
        main.duration = 1.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 19f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, new Color(0.55f, 0.3f, 1f));
        main.gravityModifier = 0.55f;
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = sparks.emission;
        em.rateOverTime = 40f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 140), new ParticleSystem.Burst(0.5f, 60) });

        var sh = sparks.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 28f;
        sh.radius = 1.4f;

        var col = sparks.colorOverLifetime;
        col.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;

        var size = sparks.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        dotTex = Dot();
        sparkMat = Additive(dotTex);
        sparkMat.SetColor("_BaseColor", Color.white * 2.2f);
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = sparkMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        sparks.Play();
    }

    // ── 재질 ────────────────────────────

    /// <summary>URP Unlit 가산 혼합. 검정이 투명이라 색을 줄이면 곧 흐려진다</summary>
    static Material Additive(Texture2D tex)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (tex != null) m.SetTexture("_BaseMap", tex);
        return m;
    }

    /// <summary>원기둥 옆면용 — 아래는 진하고 위로 갈수록 옅다 (원기둥 UV 의 v 가 높이 방향)</summary>
    static Texture2D Gradient()
    {
        Texture2D t = new Texture2D(4, 64, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            float v = y / 63f;
            float a = Mathf.Pow(1f - v, 1.6f) * 0.9f + 0.1f;
            for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(a, a, a, 1f));
        }
        t.Apply();
        return t;
    }

    /// <summary>불티 한 알 — 가운데가 밝은 둥근 점</summary>
    static Texture2D Dot()
    {
        const int n = 32;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a;
                t.SetPixel(x, y, new Color(a, a, a, a));
            }
        t.Apply();
        return t;
    }

    static float BackOut(float x)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    void OnDestroy()
    {
        if (!released && monster != null) monster.speed = keepSpeed;
        if (pillar != null) Destroy(pillar);
        if (halo != null) Destroy(halo);
        if (waves != null) foreach (LineRenderer lr in waves) if (lr != null) Destroy(lr.gameObject);
        if (flash != null) Destroy(flash.gameObject);
        if (sparks != null && Application.isPlaying) Destroy(sparks.gameObject, 2f);
        foreach (Object o in new Object[] { pillarMat, haloMat, waveMat, sparkMat, gradTex, dotTex })
            if (o != null) Destroy(o);
    }
}

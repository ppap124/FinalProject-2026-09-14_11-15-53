using UnityEngine;

/// <summary>
/// 카오스 등장 연출 — 벽 너머 하늘의 눈이 떠오르고, 눈을 뜬다 (기획 §61).
///
///   0.0초~   드드드 — 땅울림과 함께 **여러 번** 흔들린다 (rumbleBeats). 눈은 벽 아래 어둠에서 떠오른다
///            이때 눈은 위로 굴러가 있어 흰자만 보인다
///   awakenAt 눈이 내려와 경기장을 본다 — 번쩍 · 충격파 · 가장 큰 흔들림 · 알림
///   hold     싸움 시작. 그 전엔 아무도 못 친다 (GameLoop.chaosAwake)
///
/// 흔들림은 **등장할 때만**이다 — 싸우는 동안 흔들리면 피곤하다.
/// 흔들리는 박자(rumbleBeats)에 맞춰 효과음(Cue.ChaosRumble)을 넣는다 — 소리는 다음 주(2026-10-05 주)
///
/// 부품은 모두 코드로 만든다 — 에셋 팩은 저장소에 못 올리므로(라이선스) 받은 사람 쪽에서
/// 빠져도 연출이 통째로 사라지지 않게 한다. `GameLoop.SpawnChaos` 가 붙인다.
/// </summary>
public class ChaosIntro : MonoBehaviour
{
    [Tooltip("이 시간(초) 동안은 싸움이 시작되지 않는다. 최종전 제한 시간은 이만큼 늦게 줄기 시작한다")]
    public float hold = 4.6f;

    [Tooltip("떠오르는 깊이 — 벽 아래 어둠에서 제자리까지")]
    public float riseDepth = 36f;
    [Tooltip("떠오르는 시간")]
    public float rise = 2.9f;
    [Tooltip("눈을 뜨는 순간(초)")]
    public float awakenAt = 3.2f;

    public Color color = new Color(1f, 0.28f, 0.55f);

    [Header("흔들림 — 드드드")]
    [Tooltip("땅울림이 오는 순간들(초). 점점 세진다")]
    public float[] rumbleBeats = { 0f, 0.55f, 1.1f, 1.65f, 2.2f, 2.7f };
    public float rumbleFrom = 0.22f, rumbleTo = 0.5f;
    [Tooltip("울림 사이의 잔떨림")]
    public float rumbleHum = 0.1f;
    [Tooltip("눈을 뜰 때의 흔들림")]
    public float shakeAwaken = 1.0f;

    [Header("충격파")]
    public float shockRadius = 24f;
    public float shockTime = 1.4f;

    [Header("알림")]
    public string title = "카오스";
    public string subtitle = "혼돈이 눈을 뜹니다 — 제한 시간 안에 쓰러뜨리세요";

    ChaosBoss boss;
    Vector3 anchor;
    float t0;
    bool started, awakened, released;
    int beat;

    LineRenderer[] waves;
    Material waveMat;
    Light flash;
    ParticleSystem sparks;
    Material sparkMat;
    Texture2D dotTex;

    public void Begin()
    {
        anchor = transform.position;
        transform.position = anchor - Vector3.up * riseDepth;
        t0 = Time.time;
        started = true;

        if (CameraRig.Instance != null) CameraRig.Instance.FocusOn(CameraRig.Instance.battlePoint);
        GenesisAudio.Play(GenesisAudio.Cue.ChaosRumble);
    }

    void Update()
    {
        if (!started) return;
        float t = Time.time - t0;
        if (boss == null) boss = GetComponentInChildren<ChaosBoss>();

        // ── 떠오름 ──
        float g = Mathf.Clamp01(t / Mathf.Max(0.01f, rise));
        transform.position = anchor - Vector3.up * (riseDepth * (1f - Mathf.SmoothStep(0f, 1f, g)));

        // ── 드드드 ──
        CameraRig cr = CameraRig.Instance;
        while (beat < rumbleBeats.Length && t >= rumbleBeats[beat])
        {
            float k = rumbleBeats.Length > 1 ? beat / (float)(rumbleBeats.Length - 1) : 1f;
            if (cr != null) cr.Shake(Mathf.Lerp(rumbleFrom, rumbleTo, k), 0.42f);
            beat++;
        }
        if (!awakened && cr != null) cr.Shake(rumbleHum, 0.2f);

        // ── 눈을 뜬다 ──
        if (!awakened && t >= awakenAt)
        {
            awakened = true;
            if (boss != null) boss.Awaken();
            if (cr != null) cr.Shake(shakeAwaken, 1.3f);
            Vector3 eye = transform.position;
            float front = boss != null ? boss.Radius : 5f;
            BuildWaves(eye + Vector3.back * front);
            BuildFlash(eye + Vector3.back * (front + 2f));
            BuildSparks(eye);
            GenesisAudio.Play(GenesisAudio.Cue.Chaos);
            GenesisHud hud = FindFirstObjectByType<GenesisHud>();
            if (hud != null) hud.Announce(title, subtitle, color, 2.6f);
        }

        // ── 충격파 — 카메라를 보고 퍼지는 고리 둘 ──
        if (waves != null)
        {
            float ta = t - awakenAt;
            bool alive = false;
            for (int i = 0; i < waves.Length; i++)
            {
                LineRenderer lr = waves[i];
                if (lr == null) continue;
                float k = (ta - i * 0.25f) / shockTime;
                if (k < 0f) { lr.enabled = false; alive = true; continue; }
                if (k > 1f) { lr.enabled = false; continue; }
                alive = true;
                lr.enabled = true;
                Ring(lr, shockRadius * (1f - (1f - k) * (1f - k)));
                lr.widthMultiplier = Mathf.Lerp(0.9f, 0.1f, k);
                Color c = color; c.a = 1f - k;
                lr.startColor = lr.endColor = c;
            }
            if (!alive) { foreach (LineRenderer lr in waves) if (lr != null) Destroy(lr.gameObject); waves = null; }
        }

        if (flash != null)
        {
            float ta = t - awakenAt;
            flash.intensity = ta < 0.1f ? 260f * ta / 0.1f : 260f * Mathf.Exp(-(ta - 0.1f) * 3f);
            if (ta > 3f) { Destroy(flash.gameObject); flash = null; }
        }

        // ── 싸움 시작 ──
        if (!released && t >= hold)
        {
            released = true;
            transform.position = anchor;
            if (GameLoop.Instance != null) GameLoop.Instance.chaosAwake = true;
        }

        if (released && waves == null && flash == null && (sparks == null || !sparks.IsAlive(true)))
        {
            if (sparks != null) Destroy(sparks.gameObject);
            Destroy(this);
        }
    }

    // ── 부품 ────────────────────────────

    void BuildWaves(Vector3 at)
    {
        waveMat = new Material(Shader.Find("Sprites/Default"));
        Camera cam = Camera.main;
        Quaternion face = cam != null ? Quaternion.LookRotation(cam.transform.forward) : Quaternion.identity;
        waves = new LineRenderer[2];
        for (int i = 0; i < waves.Length; i++)
        {
            GameObject g = new GameObject("ChaosShock_" + i);
            g.transform.position = at;
            g.transform.rotation = face;
            LineRenderer lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 96;
            lr.sharedMaterial = waveMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;
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

    void BuildFlash(Vector3 at)
    {
        flash = new GameObject("ChaosFlash").AddComponent<Light>();
        flash.transform.position = at;
        flash.type = LightType.Point;
        flash.color = color;
        flash.range = 40f;   // 넓으면 경기장 전체가 분홍으로 덮였다
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;
    }

    void BuildSparks(Vector3 at)
    {
        GameObject g = new GameObject("ChaosSparks");
        g.transform.position = at;
        sparks = g.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sparks.main;
        main.duration = 1.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 13f);   // 빠르면 경기장까지 날아와 빗줄기처럼 떨어졌다
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, new Color(0.55f, 0.3f, 1f));
        main.gravityModifier = 0f;
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = sparks.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 260), new ParticleSystem.Burst(0.25f, 120) });

        var sh = sparks.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = boss != null ? boss.Radius : 5f;

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
        sparkMat.SetColor("_BaseColor", color * 2.2f);   // URP Unlit 은 입자 색을 안 받는다 — 흰 비가 됐다
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = sparkMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        sparks.Play();
    }

    // ── 재질 (ChaosBoss 도 쓴다) ────────────

    /// <summary>URP Unlit 가산 혼합. 검정이 투명이라 색을 줄이면 곧 흐려진다</summary>
    public static Material Additive(Texture2D tex)
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

    /// <summary>불티 한 알 — 가운데가 밝은 둥근 점</summary>
    public static Texture2D Dot()
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

    void OnDestroy()
    {
        if (!released)
        {
            transform.position = anchor;
            if (GameLoop.Instance != null) GameLoop.Instance.chaosAwake = true;
        }
        if (waves != null) foreach (LineRenderer lr in waves) if (lr != null) Destroy(lr.gameObject);
        if (flash != null) Destroy(flash.gameObject);
        if (sparks != null && Application.isPlaying) Destroy(sparks.gameObject, 2f);
        foreach (Object o in new Object[] { waveMat, sparkMat, dotTex })
            if (o != null) Destroy(o);
    }
}

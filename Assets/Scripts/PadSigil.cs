using UnityEngine;

/// <summary>
/// 영혼 패드 바닥의 **마법진 문양**. 예전 발광 고리를 대신한다.
///
/// 두 겹이다:
///   · 룬 고리 — 바깥 테두리와 룬 글자 띠. 셋 모두 같고, 천천히 돈다
///   · 문장    — 가운데 상징. 패드마다 다르다 (소환 = 육망성, 금화 = 엽전, 재료 = 결정)
/// 안쪽 실선 고리는 **판정 반경(TriggerBlock.radius)** 에 맞춰 그린다 — 예전 고리처럼
/// "여기까지 밀면 들어간다"가 그대로 보인다.
///
/// 영혼이 쌓일수록 밝아지고, 먹을 때 살짝 · 발동할 때 크게 번쩍이며 룬 고리가 빨리 돈다.
///
/// 그림은 코드로 그린다 (외부 생성 없음). 조각은 **씬에 저장하지 않는다**(DontSave) —
/// 코드로 만든 재질은 씬에 못 담긴다. 편집 중엔 미리보기로, 플레이하면 새로 만든다 (SpawnRift 와 같다).
/// </summary>
[ExecuteAlways]
public class PadSigil : MonoBehaviour
{
    public enum Kind { Summon, Coin, Crystal }

    public Kind kind = Kind.Summon;
    public Color color = Color.white;
    [Tooltip("지름(월드). 받침(5.4)보다 조금 크게")]
    public float size = 5.8f;
    [Tooltip("이 패드의 판정 — 쌓인 영혼 수와 발동을 읽는다. 비우면 숨쉬기만")]
    public TriggerBlock pad;

    [Tooltip("평소 빛 세기 (숨쉬는 폭의 아래 · 위)")]
    public Vector2 glow = new Vector2(0.9f, 1.5f);
    [Tooltip("영혼이 꽉 찼을 때 더해지는 세기")]
    public float fillGlow = 1.6f;
    [Tooltip("발동할 때 치솟는 세기")]
    public float flarePeak = 5f;
    public float breathePeriod = 3.6f;
    [Tooltip("룬 고리가 도는 속도 (도/초)")]
    public float spin = 5f;

    const int Res = 512;
    static Texture2D runeTex;
    static readonly Texture2D[] emblemTex = new Texture2D[3];
    static readonly float[] emblemJudge = new float[3];

    Material runeMat, emblemMat;
    Transform runes;
    Light lamp;
    float flareAt = -10f, flareAmp, angle, seenStored;
    int lastStored = -1;
    TriggerBlock hooked;

    void OnEnable() { Rebuild(); }

    void OnDisable() { Hook(null); }

    void Hook(TriggerBlock t)
    {
        if (hooked != null) hooked.Fired -= OnFired;
        hooked = t;
        if (hooked != null) hooked.Fired += OnFired;
    }

    void OnFired(bool ok)
    {
        Flare(ok ? 1f : 0.4f);
        if (ok) SkillFx.Sparks(transform.position + Vector3.up * 0.15f, color, 26, 5f, 0.8f);
    }

    public void Flare(float amp)
    {
        float now = Application.isPlaying ? Time.time : 0f;
        flareAmp = Mathf.Max(amp, flareAmp * Fade());
        flareAt = now;
    }

    float Fade() => Mathf.Clamp01(1f - (Time.time - flareAt) / 1.1f);

    public void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        Kill(runeMat); Kill(emblemMat);

        // 판정 원을 문양 안에서 몇 % 자리에 그을지 — 판정이 바뀌면 그림도 따라간다
        float judge = pad != null ? Mathf.Clamp(pad.radius / (size * 0.5f), 0.3f, 0.8f) : 0.66f;

        runeMat = Mat(RuneTexture(), 1);
        emblemMat = Mat(EmblemTexture(kind, judge), 0);
        runes = Quad("Runes", size, 0.012f, runeMat);
        Quad("Emblem", size, 0.010f, emblemMat);

        lamp = new GameObject("SigilLight").AddComponent<Light>();
        lamp.gameObject.hideFlags = HideFlags.DontSave;
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = Vector3.up * 0.8f;
        lamp.type = LightType.Point;
        lamp.color = color;
        lamp.range = size * 0.9f;
        lamp.shadows = LightShadows.None;

        if (Application.isPlaying) Hook(pad);
        Tick();
    }

    Transform Quad(string name, float s, float y, Material m)
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
        return q.transform;
    }

    /// <summary>가산 혼합 — 검정은 투명, 겹치면 밝아진다. 균열 빛과 같은 설정</summary>
    Material Mat(Texture2D tex, int order)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.hideFlags = HideFlags.DontSave;
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + order;
        return m;
    }

    void Update() { Tick(); }

    void Tick()
    {
        if (runeMat == null) return;
        bool play = Application.isPlaying;
        float t = play ? Time.time : 0f;

        float k = 0.5f - 0.5f * Mathf.Cos(t / Mathf.Max(0.1f, breathePeriod) * Mathf.PI * 2f);
        float g = Mathf.Lerp(glow.x, glow.y, k);

        // 쌓인 영혼 — 부드럽게 따라가고, 하나 먹을 때마다 살짝 번쩍
        float fill = 0f;
        if (play && pad != null && pad.soulCost > 0)
        {
            if (lastStored >= 0 && pad.Stored > lastStored) Flare(0.25f);
            lastStored = pad.Stored;
            seenStored = Mathf.MoveTowards(seenStored, pad.Stored, Time.deltaTime * 8f);
            fill = seenStored / pad.soulCost;
        }
        g += fillGlow * fill;

        float f = play ? flareAmp * Fade() * Fade() : 0f;
        g += (flarePeak - glow.y) * f;

        runeMat.SetColor("_BaseColor", color * g * 0.8f);
        emblemMat.SetColor("_BaseColor", color * g);
        if (lamp != null) lamp.intensity = 0.8f * g;

        if (play && runes != null)
        {
            angle += Time.deltaTime * spin * (1f + fill * 1.5f + f * 12f);
            runes.localRotation = Quaternion.Euler(90f, angle, 0f);
        }
    }

    void OnDestroy() { Kill(runeMat); Kill(emblemMat); }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    // ── 그림 ─────────────────────────────
    // 좌표는 문양 중심 0, 가장자리 1. 획은 "획 가운데선까지 거리 - 반폭" 으로 재고
    // 가장 가까운 획 하나로 알파를 정한다 — 심은 또렷하게, 둘레로 은은한 번짐

    const float Px = 1f / (Res * 0.5f);   // 한 픽셀
    const float Stroke = Px * 2.2f;       // 획 반폭

    public static Texture2D RuneTexture()
    {
        if (runeTex != null) return runeTex;
        const int N = 24;
        runeTex = Paint("PadSigil_Runes", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float e = Mathf.Min(Ring(r, 0.965f, Stroke * 1.1f), Ring(r, 0.845f, Stroke * 0.7f));

            // 룬 띠 (0.86 ~ 0.95). 가장 가까운 글자 칸 하나만 본다
            if (r > 0.80f && r < 1f)
            {
                float step = Mathf.PI * 2f / N;
                float th = Mathf.Atan2(y, x);
                int i = Mathf.RoundToInt(th / step);
                float u = (th - i * step) * r;     // 옆으로 (접선)
                float v = r - 0.905f;              // 위아래 (반지름)
                e = Mathf.Min(e, Rune(((i % N) + N) % N, u, v));
            }
            return e;
        });
        return runeTex;
    }

    /// <summary>룬 글자 하나. 칸 크기 ±0.035 × ±0.04. 번호로 획을 고른다</summary>
    static float Rune(int i, float u, float v)
    {
        const float h = 0.034f, w = 0.022f;
        float e = Seg(u, v, 0f, -h, 0f, h);                           // 세로획은 늘
        switch (i % 6)
        {
            case 0: e = Mathf.Min(e, Seg(u, v, 0f, h, w, h * 0.3f)); break;                          // ᚠ 꼴
            case 1: e = Mathf.Min(e, Mathf.Min(Seg(u, v, 0f, h * 0.4f, w, 0f), Seg(u, v, w, 0f, 0f, -h * 0.4f))); break;
            case 2: e = Mathf.Min(e, Seg(u, v, -w, h, w, -h)); break;                               // 사선
            case 3: e = Mathf.Min(e, Mathf.Min(Seg(u, v, 0f, h, -w, h * 0.3f), Seg(u, v, 0f, 0f, w, -h * 0.6f))); break;
            case 4: e = Mathf.Min(e, Seg(u, v, -w, 0f, w, 0f)); break;                              // 십자
            default: e = Mathf.Min(e, Mathf.Abs(Mathf.Sqrt(u * u + (v - h * 0.35f) * (v - h * 0.35f)) - w * 0.7f) - Stroke * 0.8f); break;
        }
        return e + Stroke * 0.15f;   // 룬은 테두리보다 살짝 가늘게
    }

    static Texture2D EmblemTexture(Kind k, float judge)
    {
        int ki = (int)k;
        if (emblemTex[ki] != null && Mathf.Approximately(emblemJudge[ki], judge)) return emblemTex[ki];
        emblemJudge[ki] = judge;   // 어떤 판정으로 그렸는지 — 판정이 바뀌면 다시 그린다

        emblemTex[ki] = Paint("PadSigil_" + k, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            // 판정 원 — 실선. 그 바깥 점선 고리
            float e = Ring(r, judge, Stroke * 1.2f);
            float th = Mathf.Atan2(y, x);
            float dash = Mathf.Repeat(th / (Mathf.PI * 2f) * 48f, 1f);
            if (dash < 0.55f) e = Mathf.Min(e, Ring(r, judge + 0.07f, Stroke * 0.6f));

            float s = judge * 0.78f;   // 문장 크기
            switch (k)
            {
                case Kind.Summon:  e = Mathf.Min(e, Hexagram(x, y, s)); break;
                case Kind.Coin:    e = Mathf.Min(e, Coin(x, y, s)); break;
                case Kind.Crystal: e = Mathf.Min(e, Crystal(x, y, s)); break;
            }
            return e;
        }, r => r < judge ? 0.05f : 0f);   // 판정 원 안은 아주 옅게 채운다
        return emblemTex[ki];
    }

    /// <summary>소환 — 두 삼각형이 겹친 육망성 + 가운데 작은 원</summary>
    static float Hexagram(float x, float y, float s)
    {
        float e = Ring(Mathf.Sqrt(x * x + y * y), s * 0.22f, Stroke);
        for (int t = 0; t < 2; t++)
            for (int j = 0; j < 3; j++)
            {
                float a0 = Mathf.PI * 0.5f + t * Mathf.PI + j * Mathf.PI * 2f / 3f;
                float a1 = a0 + Mathf.PI * 2f / 3f;
                e = Mathf.Min(e, Seg(x, y, Mathf.Cos(a0) * s, Mathf.Sin(a0) * s, Mathf.Cos(a1) * s, Mathf.Sin(a1) * s));
            }
        return e;
    }

    /// <summary>금화 — 엽전 (둥근 테 두 겹 + 네모 구멍, 네 방향 점)</summary>
    static float Coin(float x, float y, float s)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float e = Mathf.Min(Ring(r, s, Stroke * 1.2f), Ring(r, s * 0.84f, Stroke * 0.7f));
        float q = s * 0.3f;
        e = Mathf.Min(e, Mathf.Abs(Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) - q) - Stroke);   // 네모 구멍
        for (int j = 0; j < 4; j++)
        {
            float a = j * Mathf.PI * 0.5f;
            float cx = Mathf.Cos(a) * s * 0.58f, cy = Mathf.Sin(a) * s * 0.58f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            e = Mathf.Min(e, d - s * 0.07f);   // 속이 찬 점
        }
        return e;
    }

    /// <summary>재료 — 세로로 긴 결정 (마름모 + 가운데 면 선) 과 양옆 작은 결정</summary>
    static float Crystal(float x, float y, float s)
    {
        float e = Diamond(x, y, 0f, 0f, s * 0.45f, s);
        e = Mathf.Min(e, Seg(x, y, 0f, s, 0f, -s));
        e = Mathf.Min(e, Seg(x, y, -s * 0.45f, 0f, s * 0.45f, 0f));
        e = Mathf.Min(e, Diamond(x, y, -s * 0.72f, -s * 0.2f, s * 0.16f, s * 0.36f));
        e = Mathf.Min(e, Diamond(x, y,  s * 0.72f, -s * 0.2f, s * 0.16f, s * 0.36f));
        return e;
    }

    static float Diamond(float x, float y, float cx, float cy, float hw, float hh)
    {
        float e = Seg(x, y, cx, cy + hh, cx + hw, cy);
        e = Mathf.Min(e, Seg(x, y, cx + hw, cy, cx, cy - hh));
        e = Mathf.Min(e, Seg(x, y, cx, cy - hh, cx - hw, cy));
        return Mathf.Min(e, Seg(x, y, cx - hw, cy, cx, cy + hh));
    }

    static float Ring(float r, float at, float half) => Mathf.Abs(r - at) - half;

    /// <summary>선분까지 거리 - 반폭</summary>
    static float Seg(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
        float ex = px - ax - dx * t, ey = py - ay - dy * t;
        return Mathf.Sqrt(ex * ex + ey * ey) - Stroke;
    }

    /// <summary>
    /// 거리 함수로 그림을 칠한다. 가산 혼합이라 **RGB 가 곧 밝기**다 — 알파와 같게 넣는다.
    /// 가장자리는 흐리게 사라져야 블룸이 네모로 안 잘린다
    /// </summary>
    static Texture2D Paint(string name, System.Func<float, float, float> edge, System.Func<float, float> fill = null)
    {
        Texture2D t = new Texture2D(Res, Res, TextureFormat.RGBA32, true);
        t.name = name;
        t.hideFlags = HideFlags.DontSave;
        t.wrapMode = TextureWrapMode.Clamp;
        t.anisoLevel = 4;   // 비스듬히 내려다보는 바닥이라 이게 없으면 가는 획이 뭉개진다

        Color32[] px = new Color32[Res * Res];
        for (int j = 0; j < Res; j++)
            for (int i = 0; i < Res; i++)
            {
                float x = (i + 0.5f) / Res * 2f - 1f;
                float y = (j + 0.5f) / Res * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);

                float a = 0f;
                if (r < 1.02f)
                {
                    float e = edge(x, y);
                    float core = Mathf.Clamp01(0.5f - e / Px);
                    float halo = 0.32f * Mathf.Exp(-Mathf.Pow(Mathf.Max(e, 0f) / 0.03f, 2f));
                    a = Mathf.Max(core, halo);
                    if (fill != null) a = Mathf.Max(a, fill(r));
                    a *= Mathf.Clamp01((1.0f - r) / 0.02f + 1f);   // 테두리 밖은 잘라낸다
                }
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[j * Res + i] = new Color32(b, b, b, b);
            }
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }
}

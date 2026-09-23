using System.Collections.Generic;
using UnityEngine;

/// <summary>공격을 **어떻게 보여 줄지**. 피해 계산은 전부 `Projectile.Hit` 하나로 간다.</summary>
public enum AttackStyle
{
    /// <summary>사거리로 고른다 — 10 이하면 근접, 아니면 빛구슬</summary>
    Auto,
    /// <summary>날아가는 것이 없다. 휘두르는 순간 맞은 자리에서 터진다</summary>
    Melee,
    /// <summary>문화권 색 빛구슬 + 꼬리</summary>
    Orb,
    /// <summary>모양이 있는 투사체(화살·창·깃털). 날아가는 방향을 보고, 포물선을 그린다</summary>
    Missile,
    /// <summary>하늘에서 내리꽂는 번개. 즉시 맞는다</summary>
    Bolt,
}

/// <summary>
/// 공격 연출 공용 도구 — 빛구슬 재질, 꼬리, 적중 이펙트, 번개.
///
/// 17종이 전부 흰 구체를 쏘던 것을 바꾼다. **무엇이 쏘는지가 색만으로 보여야 한다**
/// — 조합표의 문화권 색(채도를 뺀 것)과 같은 계열을 쓴다.
/// </summary>
public static class AttackFx
{
    /// <summary>문화권 빛깔. 발광이라 조합표 원판보다 밝고 채도가 조금 높다</summary>
    public static Color CultureGlow(Culture c)
    {
        switch (c)
        {
            case Culture.Greek:  return new Color(1.00f, 0.82f, 0.45f);   // 금빛
            case Culture.Norse:  return new Color(0.55f, 0.78f, 1.00f);   // 서리빛
            case Culture.Korean: return new Color(1.00f, 0.42f, 0.50f);   // 단청 붉은빛
            default:             return new Color(0.92f, 0.94f, 1.00f);   // 1단계 — 흰빛
        }
    }

    static readonly Dictionary<int, Material> orbMats = new Dictionary<int, Material>();
    static Material trailMat;

    /// <summary>빛구슬 재질. 색마다 하나만 만들어 나눠 쓴다 (배칭 유지)</summary>
    public static Material OrbMaterial(Color c, float intensity)
    {
        int key = (c * intensity).GetHashCode();
        Material m;
        if (orbMats.TryGetValue(key, out m) && m != null) return m;

        m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.name = "Orb";
        m.hideFlags = HideFlags.DontSave;
        // HDR 로 넣는다 — 1 을 넘는 값이 블룸에 걸려 빛나 보인다
        m.SetColor("_BaseColor", new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f));
        orbMats[key] = m;
        return m;
    }

    /// <summary>
    /// 꼬리를 단다. `Sprites/Default` 는 정점 알파를 읽어서 꼬리 끝이 투명하게 사그라든다 —
    /// URP Unlit 은 불투명이라 꼬리가 막대기가 된다.
    /// </summary>
    public static void AddTrail(GameObject g, Color c, float width, float time)
    {
        if (c.a <= 0.01f || width <= 0.001f) return;
        if (trailMat == null)
        {
            trailMat = new Material(Shader.Find("Sprites/Default"));
            trailMat.name = "Trail";
            trailMat.hideFlags = HideFlags.DontSave;
        }
        TrailRenderer t = g.GetComponent<TrailRenderer>();
        if (t == null) t = g.AddComponent<TrailRenderer>();
        t.sharedMaterial = trailMat;
        t.time = time;
        t.minVertexDistance = 0.15f;
        t.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.25f), new GradientColorKey(c, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        t.colorGradient = gr;
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.receiveShadows = false;
    }

    /// <summary>
    /// 적중 이펙트를 한 번 터뜨린다.
    ///
    /// **Magic effects pack 은 전부 반복 재생으로 돼 있다** (데모용). 그대로 두면 적이
    /// 죽은 자리에서 영원히 번쩍인다. 꺼 두고, 제일 긴 파티클이 끝나면 지운다.
    /// </summary>
    public static void Burst(GameObject prefab, Vector3 at, float size)
    {
        if (prefab == null || size <= 0.001f) return;
        GameObject g = Object.Instantiate(prefab, at, Quaternion.identity);
        g.name = "HitFx";
        g.transform.localScale = prefab.transform.localScale * size;

        float life = 0.5f;
        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;
            // 부모 배율을 따라가게 — 안 그러면 크기를 줄여도 파티클은 원래 크기로 나온다
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            life = Mathf.Max(life, main.duration + main.startLifetime.constantMax);
        }
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.shadows = LightShadows.None;
        Object.Destroy(g, Mathf.Min(life, 3f));
    }

    /// <summary>
    /// 모양 있는 투사체를 **긴 축이 진행 방향(+Z)을 보게** 감싸서 만든다.
    ///
    /// 생성 모델은 긴 축이 X 로 나오기도 하고 Y 로 나오기도 한다. 그대로 `LookRotation`
    /// 을 걸면 창이 옆으로 누워서 날아간다. 한 번 재서 자식을 돌려 둔다.
    /// 끝(날)이 어느 쪽인지는 모양만으로 알 수 없어서 `flip` 으로 뒤집는다.
    /// </summary>
    public static GameObject WrapMissile(GameObject prefab, Vector3 at, float length, bool flip)
    {
        GameObject root = new GameObject("Missile");
        root.transform.position = at;
        GameObject m = Object.Instantiate(prefab, root.transform);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = Vector3.one;
        foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

        Renderer[] rs = m.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return root;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        Vector3 s = b.size;
        Quaternion q = Quaternion.identity;
        if (s.x >= s.y && s.x >= s.z) q = Quaternion.Euler(0f, -90f, 0f);      // X → Z
        else if (s.y >= s.x && s.y >= s.z) q = Quaternion.Euler(90f, 0f, 0f);  // Y → Z
        if (flip) q = Quaternion.Euler(0f, 180f, 0f) * q;

        float longest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        float k = longest > 0.0001f ? length / longest : 1f;
        m.transform.localRotation = q;
        m.transform.localScale = Vector3.one * k;
        // 가운데를 뿌리에 — 피벗이 끝에 있으면 창이 한쪽으로 쏠려 돈다
        Vector3 centerLocal = q * ((b.center - at) * k);
        m.transform.localPosition = -centerLocal;
        return root;
    }

    static Material boltMat;

    /// <summary>
    /// 하늘에서 내리꽂는 번개 한 줄기. 지그재그 선을 한 번 긋고 0.18초 만에 사라진다.
    /// 번개는 **날아오는 게 아니라 이미 친 것**이라, 선이 길게 남으면 레이저처럼 보인다.
    /// </summary>
    public static void Lightning(Vector3 target, Color c, float height)
    {
        if (boltMat == null)
        {
            boltMat = new Material(Shader.Find("Sprites/Default"));
            boltMat.name = "Bolt";
            boltMat.hideFlags = HideFlags.DontSave;
        }
        GameObject g = new GameObject("Bolt");
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.sharedMaterial = boltMat;
        const int n = 9;
        lr.positionCount = n;
        Vector3 top = target + new Vector3(Random.Range(-2f, 2f), height, Random.Range(-2f, 2f));
        for (int i = 0; i < n; i++)
        {
            float t = i / (n - 1f);
            Vector3 p = Vector3.Lerp(top, target, t);
            if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.9f, 0.9f));
            lr.SetPosition(i, p);
        }
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 0.2f));
        lr.startColor = Color.white;
        lr.endColor = c;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // 번쩍임 — 주변이 한 번 밝아져야 번개로 읽힌다
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = 30f;
        l.range = 9f;
        l.shadows = LightShadows.None;
        g.transform.position = target + Vector3.up * 2f;
        lr.useWorldSpace = true;

        Object.Destroy(g, 0.18f);
    }
}

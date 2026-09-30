using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 50라운드 최종 보스 **카오스** — 먼 쪽 벽 너머 하늘에 떠 있는 거대한 눈 (기획 §61).
///
/// 예전 카오스(빛나는 핵 + 부서진 고리)는 "마지막 보스답지 않다"는 말을 들었다 (2026-09-30).
/// 태초의 혼돈은 모습이 없다 — 그래서 **모든 것을 지켜보는 눈 하나**로 바꿨다.
///
///   눈알   흰자(핏줄) 구체 + 홍채를 입힌 구면 조각. 늘 경기장 쪽을 보고, 몇 초마다 **휙** 다른 곳을 본다
///   발톱   흑요석 발톱 여럿이 뒤에서 눈을 움켜쥔다. 이것이 제자리에서 돈다 — 눈까지 돌면 절반은 뒤통수를 본다
///   고리   금빛 고리 둘이 서로 다른 축으로 돈다
///
/// 눈알은 VARCO 3D 로 통째로 뽑지 않았다 — 홍채가 모델에 굳으면 따로 굴릴 수 없다.
/// 홍채 · 흰자 그림만 VARCO 로 뽑아 코드가 만든 구체에 입힌다. 발톱은 VARCO 3D 소품.
/// </summary>
public class ChaosBoss : MonoBehaviour
{
    [Header("부품 — GameLoop 가 넣는다")]
    public Texture2D irisTex;
    public Texture2D scleraTex;
    public GameObject talon;

    [Header("크기")]
    [Tooltip("눈알 지름(월드)")]
    public float diameter = 10f;
    [Tooltip("홍채가 덮는 각도(눈알 중심에서, 도) — 크면 눈이 순해 보이고 작으면 흰자만 보인다")]
    [Range(15f, 60f)] public float irisAngle = 31f;
    [Tooltip("홍채 그림에서 홍채 원이 차지하는 반지름 (그림 한 변 = 1)")]
    public float irisInImage = 0.455f;

    [Header("빛")]
    public Color glowColor = new Color(1f, 0.28f, 0.55f);
    [Tooltip("홍채 발광 — 1 을 넘으면 블룸이 번진다")]
    public float irisGlow = 1.6f;
    [Tooltip("흰자 발광 — 어두운 하늘에서 윤곽이 묻히지 않을 만큼만")]
    public float scleraGlow = 0.22f;
    [Tooltip("눈앞 점광 — 발톱 · 고리를 물들인다. 세면 흰자 · 홍채가 하얗게 날아간다")]
    public float lightIntensity = 6f;

    [Header("발톱")]
    [Range(3, 10)] public int talonCount = 7;
    [Tooltip("발톱 길이 — 눈알 지름 대비")]
    public float talonLength = 1.2f;
    [Tooltip("발톱 뿌리가 놓이는 반지름 — 눈알 반지름 대비")]
    public float talonRadius = 0.98f;
    [Tooltip("발톱 뿌리를 뒤로 뺀 거리 — 눈알 반지름 대비 (+ 가 뒤)")]
    public float talonBack = 0.4f;
    [Tooltip("발톱 모델 자체의 돌림 — 긴 축이 앞(+Z)을 보고 휜 쪽이 안을 보게. 모델마다 다르다")]
    public Vector3 talonEuler = new Vector3(90f, 270f, 0f);   // VARCO 발톱: 긴 축 X · 휜 쪽 Y · 두께 Z
    [Tooltip("발톱을 바깥으로 벌린 각(도). + 면 끝이 밖으로 벌어진다")]
    public float talonSplay = 10f;
    [Tooltip("발톱 두께 배율 — 생성 모델이 뭉툭해서 몽둥이처럼 보였다")]
    [Range(0.2f, 1f)] public float talonThin = 0.6f;
    [Tooltip("발톱 발광 — 기본색 그림을 발광에도 넣어서 금 룬 · 분홍 금만 빛난다 (검은 몸은 그대로 검다)")]
    public float talonGlow = 0.9f;
    [Tooltip("발톱 껍질이 도는 속도(초당 도)")]
    public float spinSpeed = 16f;

    [Header("고리")]
    public Color ringColor = new Color(1f, 0.78f, 0.36f);
    public float ringGlow = 0.8f;

    [Header("시선")]
    [Tooltip("다음으로 휙 보기까지 (초, 최소 ~ 최대)")]
    public Vector2 gazeHold = new Vector2(1.2f, 3.6f);
    [Tooltip("휙 보는 빠르기(초당 도) — 실제 눈처럼 순간에 꽂힌다")]
    public float saccadeSpeed = 900f;
    [Tooltip("좌우로 보는 폭(도)")]
    public float gazeYaw = 38f;
    [Tooltip("위아래로 보는 폭(도, 아래 + / 위 -)")]
    public Vector2 gazePitch = new Vector2(-8f, 20f);
    [Tooltip("시선을 고를 때 무작위 대신 필드의 유닛 하나를 노려볼 확률")]
    [Range(0f, 1f)] public float lookAtUnitChance = 0.35f;

    [Header("주변 효과 — 등장 땐 희미하다가 눈을 뜨면 확 켜진다")]
    [Tooltip("눈 둘레를 공전하는 바위 파편 (VARCO 소품). 비우면 건너뛴다")]
    public GameObject debris;
    [Range(0, 24)] public int debrisCount = 12;
    [Tooltip("눈 뒤에서 도는 혼돈의 소용돌이 색 (가산 — 1 을 넘으면 블룸)")]
    public Color vortexColor = new Color(0.9f, 0.22f, 0.85f);
    [Tooltip("소용돌이 지름 — 눈알 지름 대비")]
    public float vortexSize = 3.2f;
    [Tooltip("소용돌이가 도는 속도(초당 도)")]
    public float vortexSpin = 9f;
    [Tooltip("번개가 튀는 간격(초, 최소 ~ 최대)")]
    public Vector2 boltEvery = new Vector2(0.2f, 0.9f);
    [Tooltip("소용돌이 · 후광의 가로 배율")]
    public float cardWide = 1.45f;

    /// <summary>눈알 반지름 (월드)</summary>
    public float Radius => diameter * 0.5f;

    // 주변 효과
    Transform vortexA, vortexB, halo;
    Material vortexMatA, vortexMatB, haloMat, boltMat;
    readonly List<Transform> debrisList = new List<Transform>();
    readonly List<Vector3> debrisOrbit = new List<Vector3>();   // (반지름, 각, 초당 도)
    Quaternion debrisTilt;
    ParticleSystem inflow;
    LineRenderer[] bolts;
    float[] boltUntil;
    float nextBolt;

    Transform eye, shell, ringA, ringB, spin;
    Light eyeLight;
    Material irisMat, scleraMat, ringMat;
    readonly List<Object> made = new List<Object>();
    readonly List<Transform> talons = new List<Transform>();
    ParticleSystem motes;

    Quaternion gazeTarget = Quaternion.identity;
    float nextGaze, doubleAt = -1f;
    bool awake;
    float awakenAt = -1f;
    Monster monster;
    float dieAt = -1f;
    Vector3 baseLocal;

    void Start()
    {
        monster = GetComponentInParent<Monster>();
        Build();
    }

    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) Kill(transform.GetChild(i).gameObject);
        foreach (Object o in made) Kill(o);
        made.Clear();
        talons.Clear();
        debrisList.Clear();
        debrisOrbit.Clear();

        float D = diameter, R = D * 0.5f;
        baseLocal = transform.localPosition;

        // 몸 전체가 경기장(-Z 쪽 카메라)을 본다
        transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        spin = new GameObject("Spin").transform;       // 발톱 껍질 — 이것만 제자리에서 돈다
        spin.SetParent(transform, false);

        // ── 눈알 ──
        eye = new GameObject("Eye").transform;
        eye.SetParent(transform, false);

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Sclera";
        Strip(ball);
        ball.transform.SetParent(eye, false);
        ball.transform.localScale = Vector3.one * D;
        // 구체 UV 이음매는 -Z 쪽에 있다 — 뒤로 돌려 둔 채 둔다 (시선은 ±45° 안이라 안 보인다)
        scleraMat = Lit(scleraTex, new Color(1f, 0.93f, 0.94f), 0.72f);
        scleraMat.SetTextureScale("_BaseMap", new Vector2(2f, 1f));
        Emit(scleraMat, scleraTex, new Color(1f, 0.8f, 0.85f) * scleraGlow);
        scleraMat.SetTextureScale("_EmissionMap", new Vector2(2f, 1f));
        ball.GetComponent<Renderer>().sharedMaterial = scleraMat;

        GameObject iris = new GameObject("Iris");
        iris.transform.SetParent(eye, false);
        iris.transform.localScale = Vector3.one * D;
        Mesh cap = IrisCap(irisAngle, irisInImage);
        made.Add(cap);
        iris.AddComponent<MeshFilter>().sharedMesh = cap;
        irisMat = Lit(irisTex, Color.white, 0.9f);
        Emit(irisMat, irisTex, Color.white * irisGlow);
        MeshRenderer ir = iris.AddComponent<MeshRenderer>();
        ir.sharedMaterial = irisMat;
        ir.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        eyeLight = new GameObject("EyeLight").AddComponent<Light>();
        eyeLight.transform.SetParent(transform, false);
        eyeLight.transform.localPosition = new Vector3(0f, 0f, R * 2.2f);
        eyeLight.type = LightType.Point;
        eyeLight.color = glowColor;
        eyeLight.range = D * 2.4f;
        eyeLight.intensity = lightIntensity;
        eyeLight.shadows = LightShadows.None;

        // ── 발톱 — 뒤에서 움켜쥔다 ──
        if (talon != null)
        {
            var glowCache = new Dictionary<Material, Material>();
            for (int i = 0; i < talonCount; i++)
            {
                float a = i / (float)talonCount * 360f;
                Transform slot = new GameObject("TalonSlot_" + i).transform;
                slot.SetParent(spin, false);
                slot.localRotation = Quaternion.Euler(0f, 0f, a);

                GameObject t = Instantiate(talon, slot);
                t.name = "Talon_" + i;
                Strip(t);
                t.transform.localRotation = Quaternion.Euler(0f, talonSplay, 0f) * Quaternion.Euler(talonEuler);
                t.transform.localPosition = Vector3.zero;
                FitLongest(t, D * talonLength);
                Vector3 ls = t.transform.localScale; ls.z *= talonThin; t.transform.localScale = ls;   // 모델의 Z 가 두께
                // 뿌리(모델 원점 = 바닥) 를 눈알 둘레 뒤쪽에 둔다
                t.transform.localPosition = new Vector3(R * talonRadius, 0f, -R * talonBack);
                GlowTalon(t, glowCache);
                talons.Add(t.transform);
            }
        }

        // ── 금빛 고리 둘 ──
        ringMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ringMat.SetColor("_BaseColor", ringColor);
        ringMat.SetFloat("_Metallic", 1f);
        ringMat.SetFloat("_Smoothness", 0.72f);
        Emit(ringMat, null, ringColor * ringGlow);
        made.Add(ringMat);
        ringA = Ring("RingA", R * 1.55f, D * 0.018f, Quaternion.Euler(68f, 0f, 18f));
        ringB = Ring("RingB", R * 1.78f, D * 0.013f, Quaternion.Euler(-60f, 30f, -24f));

        motes = Motes(R);
        BuildFx(R);

        LookHome(true);
        if (!awake) eye.localRotation = Closed();
    }

    void Update()
    {
        if (eye == null) return;
        float dt = Time.deltaTime;

        if (monster != null && monster.IsDying) { Dying(); return; }

        // 몸은 천천히 떠 오르내린다
        transform.localPosition = baseLocal + Vector3.up * (Mathf.Sin(Time.time * 0.55f) * Radius * 0.06f);

        // 주변 효과 세기 — 떠오르는 동안 0.3, 눈 뜬 순간 번쩍 넘쳤다가 1
        float lvl = 0.3f;
        if (awake)
        {
            float ta = Time.time - awakenAt;
            lvl = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(ta / 0.35f)) + Mathf.Max(0f, 1.2f * (1f - ta / 1.2f));
        }
        UpdateFx(dt, lvl, 1f);

        // 발톱 껍질이 돈다 · 고리는 제 축으로
        if (spin != null) spin.Rotate(0f, 0f, spinSpeed * dt, Space.Self);
        if (ringA != null) ringA.Rotate(0f, 0f, 22f * dt, Space.Self);
        if (ringB != null) ringB.Rotate(0f, 0f, -15f * dt, Space.Self);

        // 홍채가 숨쉰다 — 싸우는 동안에만
        if (irisMat != null)
        {
            float k = awake ? 0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f) : 0.25f;
            if (awakenAt >= 0f) k += Mathf.Max(0f, 1.8f * (1f - (Time.time - awakenAt) / 0.9f));   // 눈 뜬 순간 번쩍
            irisMat.SetColor("_EmissionColor", Color.white * (irisGlow * k));
        }
        if (eyeLight != null)
            eyeLight.intensity = lightIntensity * (awake ? 1f : 0.2f) *
                                 (awakenAt >= 0f ? 1f + Mathf.Max(0f, 3f * (1f - (Time.time - awakenAt) / 1.2f)) : 1f);

        if (!awake) return;

        // ── 시선: 가만히 → 휙 ──
        if (Time.time >= nextGaze || (doubleAt >= 0f && Time.time >= doubleAt))
        {
            bool second = doubleAt >= 0f && Time.time >= doubleAt;
            doubleAt = -1f;
            PickGaze();
            nextGaze = Time.time + Random.Range(gazeHold.x, gazeHold.y);
            // 가끔 두 번 연달아 — 두리번거리는 것처럼
            if (!second && Random.value < 0.2f) doubleAt = Time.time + Random.Range(0.18f, 0.35f);
        }
        Quaternion q = Quaternion.RotateTowards(eye.localRotation, gazeTarget, saccadeSpeed * dt);
        // 멈춰 있어도 아주 미세하게 떨린다 — 살아 있는 눈
        float f = Time.time * 3f;
        Quaternion tremor = Quaternion.Euler((Mathf.PerlinNoise(f, 0.1f) - 0.5f) * 1.2f, (Mathf.PerlinNoise(0.4f, f) - 0.5f) * 1.2f, 0f);
        eye.localRotation = q;
        if (Quaternion.Angle(q, gazeTarget) < 0.5f) eye.localRotation = gazeTarget * tremor;
    }

    /// <summary>등장 연출의 절정 — 굴려 올라가 있던 눈이 내려와 경기장을 본다</summary>
    public void Awaken()
    {
        awake = true;
        awakenAt = Time.time;
        LookHome(false);
        nextGaze = Time.time + 1.4f;   // 눈 뜬 뒤 잠깐은 플레이어(카메라)를 똑바로 노려본다
    }

    /// <summary>눈을 아직 안 떴다 — 홍채가 위로 굴러가 흰자만 보인다</summary>
    Quaternion Closed() => Quaternion.Euler(-80f, 0f, 0f);

    /// <summary>
    /// 쉬는 시선 — **카메라**(플레이어). 경기장 한가운데를 보게 했더니 위에서 내려다보는 카메라엔
    /// 홍채가 눈알 아래쪽에 깔려 벽에 가렸다. 플레이어를 노려보는 편이 더 섬뜩하기도 하다
    /// </summary>
    static Vector3 Home()
    {
        Camera c = Camera.main;
        return c != null ? c.transform.position : new Vector3(0f, 30f, -45f);
    }

    void LookHome(bool snap)
    {
        gazeTarget = LocalLook(Home());
        if (snap && eye != null) eye.localRotation = gazeTarget;
    }

    void PickGaze()
    {
        if (Random.value < lookAtUnitChance)
        {
            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            if (units.Length > 0)
            {
                Unit u = units[Random.Range(0, units.Length)];
                if (u != null) { gazeTarget = Clamp(LocalLook(u.transform.position)); return; }
            }
        }
        // 플레이어를 중심으로 무작위 — 좌우는 넓게, 위아래는 좁게 (아래가 더 넓다: 필드를 본다)
        Quaternion home = LocalLook(Home());
        gazeTarget = home * Quaternion.Euler(Random.Range(gazePitch.x, gazePitch.y) * 0.6f, Random.Range(-gazeYaw, gazeYaw), 0f);
    }

    /// <summary>월드의 한 점을 보는 눈알 로컬 회전</summary>
    Quaternion LocalLook(Vector3 world)
    {
        Vector3 d = world - eye.position;
        if (d.sqrMagnitude < 0.01f) return Quaternion.identity;
        Vector3 local = transform.InverseTransformDirection(d.normalized);
        return Quaternion.LookRotation(local, Vector3.up);
    }

    /// <summary>눈이 뒤로 넘어가지 않게 — 정면에서 50° 안</summary>
    static Quaternion Clamp(Quaternion q) => Quaternion.RotateTowards(Quaternion.identity, q, 50f);

    // ── 쓰러짐 ─────────────────────────────

    void Dying()
    {
        if (dieAt < 0f)
        {
            dieAt = Time.time;
            if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.8f, 1.4f);
        }
        float t = Time.time - dieAt;   // 결과 연출이 배속을 늦춰도 따라 느려진다

        // 눈이 마구 떨다가 빛이 꺼지고, 발톱이 사방으로 풀려 흩어진다
        eye.localRotation = gazeTarget * Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-6f, 6f), 0f);
        float fade = Mathf.Clamp01(1f - t / 1.6f);
        if (irisMat != null) irisMat.SetColor("_EmissionColor", Color.white * (irisGlow * (0.3f + 2.5f * fade * fade)));
        if (eyeLight != null) eyeLight.intensity = lightIntensity * 3f * fade;
        foreach (Transform tl in talons)
        {
            if (tl == null) continue;
            tl.localPosition += new Vector3(1f, 0f, -0.4f) * (Radius * 1.4f * Time.deltaTime);
            tl.Rotate(40f * Time.deltaTime, 0f, 25f * Time.deltaTime, Space.Self);
        }
        float s = Mathf.Lerp(1f, 0.02f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 1.4f));
        eye.localScale = Vector3.one * s;
        // 소용돌이는 빨라지며 잦아들고, 번개는 처음 잠깐 미친 듯이 튄다
        UpdateFx(Time.deltaTime, (t < 0.8f ? 1.6f : 1f) * fade, 1f + 5f * (1f - fade));
        if (inflow != null && t > 0.1f) inflow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (spin != null) spin.Rotate(0f, 0f, spinSpeed * 6f * Time.deltaTime, Space.Self);
    }

    // ── 주변 효과 ───────────────────────

    /// <summary>
    /// 소용돌이 둘(서로 반대로) · 후광 · 공전 파편 · 빨려 드는 불티 · 번개.
    /// 소용돌이 · 후광은 눈알 **뒤**에 둔다 — 불투명한 눈알이 가운데를 가려서 테두리로만 번진다
    /// </summary>
    void BuildFx(float R)
    {
        float D = R * 2f;
        Texture2D spiralA = Spiral(256, 3, 5.5f, 0.3f), spiralB = Spiral(256, 5, -3.5f, 7.7f), glow = Glow(128);
        made.Add(spiralA); made.Add(spiralB); made.Add(glow);

        vortexMatA = ChaosIntro.Additive(spiralA); made.Add(vortexMatA);
        vortexMatB = ChaosIntro.Additive(spiralB); made.Add(vortexMatB);
        haloMat = ChaosIntro.Additive(glow); made.Add(haloMat);
        vortexA = Card("VortexA", vortexMatA, D * vortexSize, -R * 1.1f);
        vortexB = Card("VortexB", vortexMatB, D * vortexSize * 0.78f, -R * 0.95f);
        halo = Card("Halo", haloMat, D * 1.9f, -R * 0.4f);

        // 공전 파편 — 기울어진 띠를 따라 제각각 빠르기로
        debrisTilt = Quaternion.Euler(58f, 0f, -14f);
        if (debris != null)
        {
            var cache = new Dictionary<Material, Material>();
            for (int i = 0; i < debrisCount; i++)
            {
                GameObject d = Instantiate(debris, transform);
                d.name = "Debris_" + i;
                Strip(d);
                d.transform.localRotation = Random.rotation;
                FitLongest(d, R * Random.Range(0.16f, 0.36f));
                GlowTalon(d, cache);
                debrisList.Add(d.transform);
                debrisOrbit.Add(new Vector3(R * Random.Range(1.45f, 2.1f), i / (float)debrisCount * 360f + Random.Range(-12f, 12f),
                                            Random.Range(7f, 16f)));
            }
        }

        inflow = Inflow(R);

        // 번개 — 선 넷을 돌려 쓴다
        boltMat = ChaosIntro.Additive(null); made.Add(boltMat);
        bolts = new LineRenderer[4];
        boltUntil = new float[bolts.Length];
        for (int i = 0; i < bolts.Length; i++)
        {
            GameObject g = new GameObject("Bolt_" + i);
            g.transform.SetParent(transform, false);
            LineRenderer lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 12;
            lr.sharedMaterial = boltMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.15f);
            lr.widthMultiplier = R * 0.05f;
            lr.enabled = false;
            bolts[i] = lr;
        }
    }

    /// <summary>`lvl` = 밝기 배율(1 = 싸움 중), `pace` = 빠르기 배율(쓰러질 때 빨라진다)</summary>
    void UpdateFx(float dt, float lvl, float pace)
    {
        if (vortexA != null) vortexA.Rotate(0f, 0f, vortexSpin * pace * dt, Space.Self);
        if (vortexB != null) vortexB.Rotate(0f, 0f, -vortexSpin * 0.65f * pace * dt, Space.Self);
        float breathe = 0.85f + 0.15f * Mathf.Sin(Time.time * 1.7f);
        if (vortexMatA != null) vortexMatA.SetColor("_BaseColor", vortexColor * (2.1f * lvl * breathe));
        if (vortexMatB != null) vortexMatB.SetColor("_BaseColor", new Color(0.45f, 0.25f, 1f) * (1.6f * lvl));
        if (haloMat != null) haloMat.SetColor("_BaseColor", glowColor * (1.1f * lvl * (0.8f + 0.2f * Mathf.Sin(Time.time * 2.3f))));
        if (halo != null) { float hs = diameter * 1.9f * (1f + 0.05f * Mathf.Sin(Time.time * 2.3f)); halo.localScale = new Vector3(hs * cardWide, hs * 0.8f, 1f); }

        for (int i = 0; i < debrisList.Count; i++)
        {
            Transform d = debrisList[i];
            if (d == null) continue;
            Vector3 o = debrisOrbit[i];
            o.y += o.z * pace * dt;
            debrisOrbit[i] = o;
            float a = o.y * Mathf.Deg2Rad;
            d.localPosition = debrisTilt * new Vector3(Mathf.Cos(a) * o.x, Mathf.Sin(a) * o.x, 0f);
            d.Rotate(20f * dt, 33f * dt, 0f, Space.Self);
        }

        if (inflow != null)
        {
            var em = inflow.emission;
            em.rateOverTime = 45f * Mathf.Clamp01(lvl);
        }

        // 번개 — 눈 가장자리에서 밖으로 튄다. 사는 동안 매 프레임 다시 꺾여 지직거린다
        if (bolts == null) return;
        if (Time.time >= nextBolt)
        {
            for (int i = 0; i < bolts.Length; i++)
            {
                if (bolts[i].enabled) continue;
                Bolt(bolts[i]);
                bolts[i].enabled = true;
                boltUntil[i] = Time.time + Random.Range(0.08f, 0.2f);
                break;
            }
            float gap = Random.Range(boltEvery.x, boltEvery.y) / Mathf.Max(0.2f, lvl * pace);
            nextBolt = Time.time + gap;
        }
        for (int i = 0; i < bolts.Length; i++)
        {
            LineRenderer lr = bolts[i];
            if (!lr.enabled) continue;
            if (Time.time >= boltUntil[i]) { lr.enabled = false; continue; }
            Jitter(lr);
        }
        boltMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.95f) * (3.2f * Mathf.Max(0.3f, lvl)));
    }

    void Bolt(LineRenderer lr)
    {
        float R = Radius;
        // 앞쪽 반구 가장자리 — 정면(홍채)은 피한다
        float ang = Random.Range(0f, Mathf.PI * 2f);
        Vector3 dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), Random.Range(-0.1f, 0.55f)).normalized;
        Vector3 a = dir * (R * 1.02f);
        Vector3 b = (dir + Random.insideUnitSphere * 0.35f).normalized * (R * Random.Range(1.7f, 2.6f));
        int n = lr.positionCount;
        for (int i = 0; i < n; i++) lr.SetPosition(i, Vector3.Lerp(a, b, i / (float)(n - 1)));
        Jitter(lr);
    }

    void Jitter(LineRenderer lr)
    {
        int n = lr.positionCount;
        Vector3 a = lr.GetPosition(0), b = lr.GetPosition(n - 1);
        float len = (b - a).magnitude;
        for (int i = 1; i < n - 1; i++)
        {
            float k = i / (float)(n - 1);
            lr.SetPosition(i, Vector3.Lerp(a, b, k) + Random.insideUnitSphere * (len * 0.09f * (1f - k * 0.5f)));
        }
    }

    /// <summary>눈을 보는 판 하나 — 몸 뒤쪽(z) 에 세운다. 가산이라 양면</summary>
    Transform Card(string name, Material m, float size, float z)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Strip(g);
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(0f, 0f, z);
        // 가로로 넓힌다 — 위는 화면 밖, 아래는 벽 뒤라 게임 카메라엔 양옆만 보인다
        g.transform.localScale = new Vector3(size * cardWide, size * 0.8f, 1f);
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g.transform;
    }

    /// <summary>바깥에서 눈으로 빨려 드는 불티 — 늘어진 빛줄기로 그린다</summary>
    ParticleSystem Inflow(float R)
    {
        GameObject g = new GameObject("Inflow");
        g.transform.SetParent(transform, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.8f), new Color(0.55f, 0.35f, 1f));
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var em = ps.emission; em.rateOverTime = 20f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = R * 3.4f; sh.radiusThickness = 0.15f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.radial = new ParticleSystem.MinMaxCurve(-R * 0.95f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0.35f);   // 빨려 들며 소용돌이 쪽으로 감긴다
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        Texture2D dot = ChaosIntro.Dot(); made.Add(dot);
        // URP Unlit 은 입자 색(startColor)을 안 받는다 — 흰 빗줄기처럼 보여서 재질에 색을 입힌다
        Material m = ChaosIntro.Additive(dot); m.SetColor("_BaseColor", new Color(1f, 0.38f, 0.85f) * 1.5f); made.Add(m);
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = m;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.35f;
        pr.lengthScale = 1.5f;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    /// <summary>
    /// 나선 은하 그림 — 팔 `arms` 개가 `twist` 만큼 감긴다. 가운데는 비운다(눈알이 가린다), 바깥으로 흐려진다.
    /// 흑백으로 만들고 색은 재질이 입힌다 (가산이라 검정이 투명)
    /// </summary>
    static Texture2D Spiral(int n, int arms, float twist, float seed)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float th = Mathf.Atan2(dy, dx);
                float arm = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(arms * th + twist * r * Mathf.PI), 3.5f);
                float noise = Mathf.PerlinNoise(dx * 4f + seed, dy * 4f - seed) * 0.8f + Mathf.PerlinNoise(dx * 11f - seed, dy * 11f + seed) * 0.4f;
                float fall = Mathf.SmoothStep(0f, 1f, (r - 0.16f) / 0.22f) * Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                float v = Mathf.Clamp01((arm * 0.85f + 0.15f) * noise * fall * 1.6f);
                px[y * n + x] = new Color(v, v, v, v);
            }
        t.SetPixels(px);
        t.Apply(true);
        return t;
    }

    /// <summary>후광 — 가운데가 밝고 바깥으로 부드럽게 꺼지는 원</summary>
    static Texture2D Glow(int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float v = Mathf.Clamp01(Mathf.Exp(-r * r * 5f) - 0.01f) * Mathf.Clamp01((1f - r) * 4f);
                t.SetPixel(x, y, new Color(v, v, v, v));
            }
        t.Apply(true);
        return t;
    }

    // ── 부품 ────────────────────────────

    /// <summary>
    /// 홍채 —눈알 겉면(+Z 쪽)의 구면 조각. 반지름 0.5(부모가 지름만큼 키운다)보다 아주 조금 밖에 둔다.
    /// UV 는 정면 투영이라 그림 속 원이 조각 가장자리에 정확히 맞는다
    /// </summary>
    static Mesh IrisCap(float angleDeg, float inImage)
    {
        const int rings = 18, segs = 56;
        float A = angleDeg * Mathf.Deg2Rad, sinA = Mathf.Sin(A);
        const float r = 0.5035f;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var tri = new List<int>();
        v.Add(new Vector3(0f, 0f, r)); uv.Add(new Vector2(0.5f, 0.5f)); n.Add(Vector3.forward);
        for (int i = 1; i <= rings; i++)
        {
            float th = A * i / rings;
            for (int j = 0; j < segs; j++)
            {
                float ph = j / (float)segs * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th));
                v.Add(d * r); n.Add(d);
                // 앞에서 보면 +X 가 왼쪽이다 — 그림이 뒤집히지 않게 u 를 거꾸로
                uv.Add(new Vector2(0.5f - d.x / sinA * inImage, 0.5f + d.y / sinA * inImage));
            }
        }
        for (int j = 0; j < segs; j++) { tri.Add(0); tri.Add(1 + j); tri.Add(1 + (j + 1) % segs); }
        for (int i = 1; i < rings; i++)
        {
            int a0 = 1 + (i - 1) * segs, b0 = 1 + i * segs;
            for (int j = 0; j < segs; j++)
            {
                int j1 = (j + 1) % segs;
                tri.Add(a0 + j); tri.Add(b0 + j); tri.Add(a0 + j1);
                tri.Add(a0 + j1); tri.Add(b0 + j); tri.Add(b0 + j1);
            }
        }
        Mesh m = new Mesh { name = "ChaosIris" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(n); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }

    Transform Ring(string name, float radius, float tube, Quaternion tilt)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(transform, false);
        g.transform.localRotation = tilt;
        Mesh m = Torus(radius, tube, 96, 10);
        made.Add(m);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ringMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    /// <summary>XY 평면에 누운 도넛 (Z 축으로 돌리면 제자리에서 돈다)</summary>
    static Mesh Torus(float R, float r, int seg, int side)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= side; j++)
            {
                float b = j / (float)side * Mathf.PI * 2f;
                Vector3 nn = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                v.Add(c * R + nn * r); n.Add(nn);
            }
        }
        int row = side + 1;
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < side; j++)
            {
                int a = i * row + j, b = (i + 1) * row + j;
                tri.Add(a); tri.Add(b); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
            }
        Mesh m = new Mesh { name = "ChaosRing" };
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    /// <summary>눈 둘레를 떠도는 분홍 티끌</summary>
    ParticleSystem Motes(float R)
    {
        GameObject g = new GameObject("Motes");
        g.transform.SetParent(transform, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(glowColor, new Color(0.6f, 0.35f, 1f));
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var em = ps.emission; em.rateOverTime = 40f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = R * 1.7f; sh.radiusThickness = 0.35f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        Texture2D dot = ChaosIntro.Dot();
        made.Add(dot);
        Material m = ChaosIntro.Additive(dot);
        m.SetColor("_BaseColor", glowColor * 2f);   // URP Unlit 은 입자 색을 안 받는다
        made.Add(m);
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = m;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    /// <summary>
    /// 발톱 재질 — 기본색 그림을 발광에도 넣는다. 검은 흑요석은 검은 채로, 금 룬과 분홍 금만 빛난다.
    /// glTFast 셰이더는 프로퍼티 블록을 안 받으므로 재질을 복제한다 (카오스는 하나뿐)
    /// </summary>
    void GlowTalon(GameObject piece, Dictionary<Material, Material> cache)
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
                m.EnableKeyword("_EMISSIVE");
                Texture baseTex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
                if (m.HasProperty("emissiveTexture"))
                    m.SetTexture("emissiveTexture", baseTex != null ? baseTex : Texture2D.blackTexture);
                m.SetColor("emissiveFactor", Color.white * talonGlow);
                cache[src] = m;
                made.Add(m);
            }
            rr.sharedMaterial = m;
        }
    }

    Material Lit(Texture tex, Color tint, float smooth)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        made.Add(m);
        return m;
    }

    static void Emit(Material m, Texture map, Color c)
    {
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        if (map != null) m.SetTexture("_EmissionMap", map);
        m.SetColor("_EmissionColor", c);
    }

    void OnDestroy()
    {
        foreach (Object o in made) if (o != null) Destroy(o);
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    /// <summary>가장 긴 축을 `want` 에 맞춘다. 생성 모델은 원본 크기가 제각각이다</summary>
    static void FitLongest(GameObject g, float want)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f) g.transform.localScale *= want / longest;
    }

    static void Strip(GameObject g)
    {
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Kill(c);
    }
}

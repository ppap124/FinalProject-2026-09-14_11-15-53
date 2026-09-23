using UnityEngine;

/// <summary>
/// 영혼 수급. **잔액은 숫자가 아니라 필드에 서 있는 영혼 유닛의 수다.**
/// 라운드마다 영혼 블록에 영혼이 생기고, 패드에 먹히면 사라진다.
/// </summary>
public class SoulBank : MonoBehaviour
{
    public static SoulBank Instance { get; private set; }

    [Header("수급")]
    public int startingSouls = 25;
    public int soulsPerRound = 5;

    [Header("생성 위치 (영혼 블록)")]
    public Vector3 spawnCenter = new Vector3(-11f, 1.0f, -18f);
    public float spawnSpread = 3.2f;
    public float soulSize = 1.1f;

    [Header("겉모습")]
    [Tooltip("영혼으로 쓸 불꽃 프리팹. 비우면 예전처럼 민무늬 캡슐로 나온다")]
    public GameObject soulEffect;

    [Tooltip("불꽃 크기. 영혼은 제 시점에서 24픽셀뿐이라 잘게 키울 필요가 없다")]
    public float effectScale = 0.55f;

    [Tooltip("불티·연기까지 살리면 영혼 한 마리가 초당 145개를 뿜는다. " +
             "20마리면 2900개다 — 24픽셀에서 불티는 한 픽셀도 안 된다")]
    public bool trimEffect = true;

    [Tooltip("끌 파티클 이름 조각. 남기는 쪽이 아니라 **버리는 쪽**을 적는다 — " +
             "루트 파티클은 프리팹 이름을 그대로 달고 나와서 프리팹마다 달라지기 때문이다")]
    public string[] dropParticles = { "Spark", "Smoke", "Dark" };

    [Tooltip("발광 구슬 지름(월드). 잔액이 곧 필드의 영혼 수라 **셀 수 있어야 한다** — " +
             "불꽃만 두면 여러 개가 한 덩어리로 뭉쳐 몇 개인지 안 보인다")]
    public float coreSize = 0.34f;

    public Color coreColor = new Color(0.35f, 1f, 0.45f);

    [Tooltip("발광 세기. 1을 넘으면 코어가 하얗게 타고 색은 블룸이 실어 나른다")]
    public float coreEmission = 1.4f;

    /// <summary>구슬 재질은 영혼마다 새로 만들지 않고 하나를 나눠 쓴다</summary>
    Material coreMat;

    /// <summary>필드의 영혼 수 = 잔액</summary>
    public int Souls => SoulAvatar.All.Count;

    public int TotalEarned { get; private set; }

    Transform root;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameObject g = GameObject.Find("Souls");
        if (g == null) g = new GameObject("Souls");
        root = g.transform;

        Spawn(startingSouls);
    }

    public void AddRoundSouls()
    {
        int bonus = ResearchLab.Instance != null ? ResearchLab.Instance.BonusSoulsPerRound : 0;
        Spawn(soulsPerRound + bonus);
    }

    public void Spawn(int count)
    {
        for (int i = 0; i < count; i++) SpawnOne();
        TotalEarned += count;
    }

    void SpawnOne()
    {
        if (root == null)
        {
            GameObject g0 = GameObject.Find("Souls");
            if (g0 == null) g0 = new GameObject("Souls");
            root = g0.transform;
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Soul";
        go.transform.SetParent(root, true);

        // 캐프슐은 기본 높이 2 — 납작하게 눌러서 바닥에 세운다
        float w = soulSize;
        float hy = soulSize * 0.55f;          // 실제 반높이
        go.transform.localScale = new Vector3(w, hy, w);

        Vector2 r = Random.insideUnitCircle * spawnSpread;
        go.transform.position = new Vector3(spawnCenter.x + r.x, hy, spawnCenter.z + r.y);

        // 트리거로 두면 서로 밀지 않고 겹쳐진다. 클릭 판정은 그대로 돌아간다
        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Renderer rr = go.GetComponent<Renderer>();
        if (rr != null)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = new Color(0.55f, 0.90f, 1f);
            rr.sharedMaterial = m;
        }

        // 불꽃을 달면 캡슐은 **안 보이게만** 한다. 지우면 안 된다 —
        // 클릭으로 집는 판정이 이 캡슐의 콜라이더에 붙어 있고,
        // `SoulAvatar` 의 선택 고리도 이 트랜스폼을 기준으로 깔린다
        if (soulEffect != null)
        {
            if (rr != null) rr.enabled = false;
            Dress(go.transform, hy);
        }

        go.AddComponent<SoulAvatar>();
    }

    /// <summary>
    /// 영혼에 불꽃을 입힌다.
    ///
    /// **부모 캡슐이 (w, hy, w) 로 눌려 있어서** 그냥 자식으로 붙이면 불꽃까지
    /// 같이 찌그러진다. 부모 배율을 되돌린 값을 자식 배율로 넣는다.
    ///
    /// 불티·연기는 끈다. 기본 프리팹은 다섯 덩어리로 초당 145개를 뿜는데
    /// 그중 불티만 100개다. 영혼이 20마리 깔리면 초당 2900개가 되고,
    /// 정작 영혼은 제 시점에서 24픽셀이라 불티 한 알은 한 픽셀도 안 된다.
    /// </summary>
    void Dress(Transform soul, float halfHeight)
    {
        Vector3 s = soul.localScale;

        // ── 발광 구슬 ──
        // 캡슐을 그대로 켜 두면 알약처럼 읽힌다. 판정용 캡슐은 숨기고
        // 보이는 것은 구슬로 따로 둔다
        if (coreMat == null)
        {
            coreMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            coreMat.SetColor("_BaseColor", coreColor);
            coreMat.EnableKeyword("_EMISSION");

            // **가장 밝은 채널이 1 을 넘는 순간 흰색으로 뭉갠다.** 그래서 색을
            // 그대로 곱하면 안 된다 — 제일 밝은 채널을 1 로 맞춰 색을 세운 뒤에
            // 세기를 곱한다. 이렇게 해야 `coreColor` 를 바꾼 게 실제로 먹는다
            Color e = coreColor;
            float mx = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
            if (mx > 0.001f) e /= mx;
            coreMat.SetColor("_EmissionColor", e * coreEmission);
            coreMat.SetFloat("_Smoothness", 0.8f);
        }

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Core";
        Destroy(orb.GetComponent<Collider>());
        orb.transform.SetParent(soul, false);
        orb.transform.localPosition = Vector3.zero;
        orb.transform.localScale = new Vector3(
            coreSize / Mathf.Max(0.0001f, s.x),
            coreSize / Mathf.Max(0.0001f, s.y),
            coreSize / Mathf.Max(0.0001f, s.z));
        orb.GetComponent<Renderer>().sharedMaterial = coreMat;

        GameObject fx = Instantiate(soulEffect, soul);
        fx.name = "SoulFlame";

        foreach (Collider c in fx.GetComponentsInChildren<Collider>(true)) Destroy(c);

        fx.transform.localPosition = Vector3.zero;
        fx.transform.localRotation = Quaternion.identity;
        fx.transform.localScale = new Vector3(
            effectScale / Mathf.Max(0.0001f, s.x),
            effectScale / Mathf.Max(0.0001f, s.y),
            effectScale / Mathf.Max(0.0001f, s.z));

        if (!trimEffect) return;

        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            bool drop = false;
            for (int i = 0; i < dropParticles.Length; i++)
                if (!string.IsNullOrEmpty(dropParticles[i])
                    && ps.name.IndexOf(dropParticles[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                { drop = true; break; }

            if (drop) ps.gameObject.SetActive(false);
        }
    }

    public bool CanAfford(int cost) => Souls >= cost;

    /// <summary>영혼 cost개를 먹는다 (단축키용). 패드는 직접 Consume을 부른다.</summary>
    public bool TrySpend(int cost)
    {
        if (cost < 0 || Souls < cost) return false;

        for (int i = 0; i < cost; i++)
        {
            if (SoulAvatar.All.Count == 0) return false;
            SoulAvatar.All[SoulAvatar.All.Count - 1].Consume();
        }

        return true;
    }

    public void ResetRun()
    {
        for (int i = SoulAvatar.All.Count - 1; i >= 0; i--)
            if (SoulAvatar.All[i] != null) SoulAvatar.All[i].Consume();

        TotalEarned = 0;
        Spawn(startingSouls);
    }
}

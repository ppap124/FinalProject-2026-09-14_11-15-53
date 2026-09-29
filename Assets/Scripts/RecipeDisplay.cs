using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조합표를 **문화권마다 아래에서 위로 자라는 계보도**로 세운다. 실제 유닛이 칸마다 서 있다.
///
///      그리스            북유럽            한국          ← 문화권 이름
///      제우스             오딘             환웅          4단계
///        ↑ ×2              ↑ ×2             ↑ ×2
///      티탄               요툰             이무기         3단계  (아래 셋 1/3)
///     ↗  ↑  ↖           ↗  ↑  ↖          ↗  ↑  ↖
///  미노 하피 오라클    광전사 발키리 룬마녀   도깨비 도사 구미호  2단계  (전사 2/2 …)
///    + 암브로시아 1/1      + 룬석 0/1         + 여의주 0/1
///
/// 전에는 격자(행 = 1단계 재료, 열 = 문화권)였다. 공식이 어디에도 안 적혀 있어서
/// "2단계 셋을 다 모아야 3단계" 를 따로 외워야 했고, 강한 유닛이 화면 **아래**에 있어
/// 읽는 방향도 거꾸로였다. 이제 화살표가 공식이고, 칸마다 **지금 몇 개 가졌는지**가 붙는다.
///
/// 나무는 표에서 스스로 만들어진다 — 문화권이 늘면 나무가 하나 더 선다.
/// </summary>
public class RecipeDisplay : MonoBehaviour
{
    [Header("계보도 배치 (블록 한복판 기준 로컬 좌표)")]
    [Tooltip("문화권 나무 사이 간격")]
    public float treeGap = 19f;
    [Tooltip("한 나무 안 2단계 셋 사이 간격")]
    public float trioGap = 5.8f;
    [Tooltip("2 · 3 · 4단계 줄의 z. 카메라 쪽(-z)이 아래 — 약한 것이 아래, 강한 것이 위")]
    public float rowTier2 = -14f, rowTier3 = 0f, rowTier4 = 14f;
    [Tooltip("2단계 명판 아래로 문화권 이름 · 재료 명판까지의 거리")]
    public float rootGap = 3.6f;
    [Tooltip("단계별 받침 지름")]
    public float padTier2 = 4.2f, padTier3 = 5.0f, padTier4 = 5.8f;

    [Tooltip("조합표 유닛 크기. **카메라 높이를 올리면 같이 올려야 한다** — " +
             "전투용 크기 그대로 두면 걸어가서 읽을 수가 없다")]
    public float scaleMult = 0.9f;

    [Header("화살표")]
    public Color arrowColor = new Color(0.92f, 0.72f, 0.34f, 0.85f);
    public float arrowWidth = 0.22f;

    [Header("명판")]
    public bool showLabels = true;
    [Tooltip("숫자 · 설명 글꼴. HUD 와 같은 본고딕(NotoSansKR-Bold)")]
    public Font labelFont;
    [Tooltip("유닛 이름 · 문화권 이름 글꼴. HUD 제목과 같은 본명조(NotoSerifKR-Bold)")]
    public Font titleFont;
    public float labelRange = 120f;   // 조합표 먼 줄(4단계)은 카메라에서 70 넘게 떨어져 있다

    [Header("자리")]
    [Tooltip("이 블록 한복판으로 자기 위치를 맞춘다. 비우면 지금 위치를 그대로 쓴다.\n\n" +
             "**좌표를 손으로 맞추지 않는다** — 블록을 옮겼을 때 조합표만 제자리에 " +
             "남아 허공에 뜬 적이 있다")]
    public string snapToBlock = "Block_Recipe";

    readonly List<Transform> shown = new List<Transform>();

    /// <summary>
    /// 조합표 블록 한복판으로 옮겨 붙는다. 블록을 옮기면 같이 따라온다.
    /// 높이는 건드리지 않는다 — 블록 윗면은 항상 y=0 이다.
    /// </summary>
    void Snap()
    {
        if (string.IsNullOrEmpty(snapToBlock)) return;

        GameObject blk = GameObject.Find(snapToBlock);
        if (blk == null) return;

        Vector3 p = blk.transform.position;
        transform.position = new Vector3(p.x, transform.position.y, p.z);
    }

    Camera cam;

    void Start()
    {
        Build();
    }

    // ── 명판 ── 칸 앞에 서는 글자. 보유 수는 `Refresh` 가 readyCheck 마다 다시 센다
    enum PlateKind { Tier2, Tier3, Tier4, Culture, Times }

    class Plate
    {
        public PlateKind kind;
        public Vector3 at;           // 로컬
        public UnitType type;        // 이 칸의 유닛 (Tier2~4)
        public UnitType need;        // 재료 유닛 (Tier2: 1단계, Tier4: 3단계)
        public Culture culture;
        public TextAnchor anchor = TextAnchor.UpperCenter;
        public string title, line;   // line 은 색 태그가 든 서식 글
        public string plain = "";    // line 에서 태그를 뺀 것 — 그림자와 폭 재기용
        public bool ready;
    }

    readonly List<Plate> plates = new List<Plate>();

    public void Build()
    {
        Clear();
        Snap();

        UnitType[] bases = UnitTable.OfTier(1);
        Culture[] cultures = MaterialTable.All;

        for (int ci = 0; ci < cultures.Length; ci++)
        {
            Culture c = cultures[ci];
            Color tint = MaterialTable.Color(c);
            float cx = (ci - (cultures.Length - 1) * 0.5f) * treeGap;

            UnitType t3 = UnitTable.Tier3Of(c);
            UnitType t4 = UnitTable.Tier4Of(c);

            // 2단계 셋 — 각자 3단계로 화살표가 모인다
            for (int bi = 0; bi < bases.Length; bi++)
            {
                UnitType res;
                if (!UnitTable.TryCombine(bases[bi], c, out res)) continue;

                float x = cx + (bi - (bases.Length - 1) * 0.5f) * trioGap;
                Cell(res, x, rowTier2, padTier2, tint);
                Arrow(new Vector3(x, 0f, rowTier2), padTier2 * 0.5f, new Vector3(cx, 0f, rowTier3), padTier3 * 0.5f);
                plates.Add(new Plate { kind = PlateKind.Tier2, type = res, need = bases[bi], culture = c,
                                       at = new Vector3(x, 0f, rowTier2 - padTier2 * 0.5f) });
            }

            // 나무 뿌리 — 문화권 이름 + 재료. 셋 모두 이 재료를 하나씩 먹는다.
            // 이름을 나무 꼭대기에 두면 원근 때문에 4단계 유닛 머리와 겹친다
            plates.Add(new Plate { kind = PlateKind.Culture, culture = c,
                                   at = new Vector3(cx, 0f, rowTier2 - padTier2 * 0.5f - rootGap) });

            Cell(t3, cx, rowTier3, padTier3, tint);
            Cell(t4, cx, rowTier4, padTier4, tint);
            Zone(ci, cx, tint);
            Arrow(new Vector3(cx, 0f, rowTier3), padTier3 * 0.5f, new Vector3(cx, 0f, rowTier4), padTier4 * 0.5f);

            // 3·4단계 명판은 옆에 — 아래는 모여드는 화살표 자리다.
            // 3단계는 **오른쪽 위**로 올린다: 노드 높이에 두면 오른쪽 아래에서 올라오는 화살표 끝과 겹쳤다.
            // 4단계는 고리가 커서(원근으로 옆으로 퍼진다) 더 밀어낸다 — 명판이 고리 테를 덮었다
            plates.Add(new Plate { kind = PlateKind.Tier3, type = t3, culture = c, anchor = TextAnchor.MiddleLeft,
                                   at = new Vector3(cx + padTier3 * 0.5f + 1.0f, 0f, rowTier3 + padTier3 * 0.35f) });
            plates.Add(new Plate { kind = PlateKind.Tier4, type = t4, need = t3, culture = c, anchor = TextAnchor.MiddleLeft,
                                   at = new Vector3(cx + padTier4 * 0.5f + 1.4f, 0f, rowTier4) });
            // ×2 는 세로 화살표의 **왼쪽** — 오른쪽은 3단계 명판 자리다
            plates.Add(new Plate { kind = PlateKind.Times, culture = c, anchor = TextAnchor.MiddleRight,
                                   at = new Vector3(cx - 0.5f, 0f, (rowTier3 + padTier3 * 0.5f + rowTier4 - padTier4 * 0.5f) * 0.5f) });
        }

        Refresh();
        if (Application.isPlaying) BuildFx();
    }

    /// <summary>받침 + 전시 유닛 + 칸 자리 기록</summary>
    void Cell(UnitType t, float x, float z, float pad, Color tint)
    {
        Pad(x, z, tint, pad);
        Place(t, x, z);
        cells[t] = new Vector3(x, 0f, z);
    }

    static Material arrowMat;

    /// <summary>
    /// 바닥에 눕힌 금 화살표. 받침 가장자리에서 가장자리까지 — 받침 위로 올라가면 유닛 발을 가린다.
    /// 선(LineRenderer) 두 개: 곧은 몸통 + 끝이 뾰족해지는 머리.
    /// </summary>
    void Arrow(Vector3 from, float fromR, Vector3 to, float toR)
    {
        Vector3 dir = to - from; dir.y = 0f;
        float len = dir.magnitude;
        if (len < 0.01f) return;
        dir /= len;

        const float gap = 0.35f, head = 0.9f;
        Vector3 a = from + dir * (fromR + gap);
        Vector3 b = to - dir * (toR + gap);
        if ((b - a).magnitude < head + 0.2f) return;
        Vector3 neck = b - dir * head;

        if (arrowMat == null) arrowMat = new Material(Shader.Find("Sprites/Default"));
        ArrowLine("Recipe_Arrow", a, neck, AnimationCurve.Constant(0f, 1f, arrowWidth));
        ArrowLine("Recipe_ArrowHead", neck, b, AnimationCurve.Linear(0f, arrowWidth * 3.2f, 1f, 0f));
    }

    void ArrowLine(string name, Vector3 a, Vector3 b, AnimationCurve width)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(transform, false);
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 선의 면이 바닥을 보게

        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.alignment = LineAlignment.TransformZ;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.sharedMaterial = arrowMat;
        lr.startColor = lr.endColor = arrowColor;
        lr.widthCurve = width;
        lr.numCapVertices = 0;

        // 90° 눕혔으므로 로컬 (x, y) 가 바닥의 (x, z) 다. 받침(y≈0.11) 위로 살짝 띄운다
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(a.x, a.z, -0.12f));
        lr.SetPosition(1, new Vector3(b.x, b.z, -0.12f));
    }

    // ── 파티클 ────────────────────────────
    // 에셋 스토어 팩(@Asset)을 쓴다. 저장소에는 없으므로 **비어 있어도 조합표는 그대로 선다**

    [Header("파티클")]
    [Tooltip("4단계 칸 바닥에 늘 도는 마법진 — 최종 목표 칸이 멀리서도 보이게")]
    public GameObject tier4Circle;
    public float tier4CircleScale = 1.4f;   // 받침 지름(≈4.3)에 맞는 크기

    [Tooltip("3단계 전시 유닛에 두르는 오라")]
    public GameObject tier3Aura;
    public float tier3AuraScale = 1.4f;

    [Tooltip("**지금 만들 수 있는 칸**에 켜지는 반짝임. 표를 읽지 않아도 뭘 만들 수 있는지 보인다")]
    public GameObject readyGlow;
    public float readyGlowScale = 1.6f;
    [Tooltip("반짝임 높이 — 바닥에 깔면 유닛 발밑에 가려 안 보인다")]
    public float readyGlowHeight = 1.0f;

    [Tooltip("조합하면 조합표의 그 칸과 필드의 새 유닛 자리에서 한 번 터진다")]
    public GameObject combineFlash;
    public float combineFlashScale = 0.6f;
    public float fieldFlashScale = 0.5f;

    [Tooltip("만들 수 있는지 다시 재는 간격(초)")]
    public float readyCheck = 0.25f;

    readonly Dictionary<UnitType, Vector3> cells = new Dictionary<UnitType, Vector3>();   // 칸 한복판 (로컬)
    readonly Dictionary<UnitType, GameObject> readyFx = new Dictionary<UnitType, GameObject>();
    float nextCheck;

    void OnEnable()  { UnitCombiner.Combined += OnCombined; }
    void OnDisable() { UnitCombiner.Combined -= OnCombined; }

    void BuildFx()
    {
        foreach (KeyValuePair<UnitType, Vector3> kv in cells)
        {
            int tier = UnitTable.Get(kv.Key).tier;
            if (tier < 2) continue;

            // 받침 윗면(y≈0.11) 바로 위 — 더 낮으면 원반에 묻힌다
            Vector3 floor = transform.TransformPoint(kv.Value + Vector3.up * 0.13f);

            if (tier == 4 && tier4Circle != null) Fx(tier4Circle, floor, tier4CircleScale, "Recipe_FX_Circle");
            if (tier == 3 && tier3Aura != null)   Fx(tier3Aura, floor, tier3AuraScale, "Recipe_FX_Aura");

            if (readyGlow != null)
            {
                GameObject g = Fx(readyGlow, floor + Vector3.up * readyGlowHeight, readyGlowScale, "Recipe_FX_Ready");
                g.SetActive(false);
                readyFx[kv.Key] = g;
            }
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // 성벽 문장이 둥실 떠 있다
        foreach (Crest3 c in crests)
        {
            if (c.pivot == null) continue;
            Vector3 p = c.pivot.localPosition;
            p.y = c.baseY + crestBob * Mathf.Sin(Time.time * 0.9f + c.phase);
            c.pivot.localPosition = p;
        }

        // 룬이 천천히 숨쉰다 — 돌마다 박자를 어긋나게
        for (int i = 0; i < runeMats.Count; i++)
        {
            if (runeMats[i] == null) continue;
            float k = 0.7f + 0.3f * Mathf.Sin(Time.time * 1.3f + i * 1.9f);
            runeMats[i].SetColor("emissiveFactor", runeGlow * runeGlowIntensity * k);
        }

        if (Time.time < nextCheck) return;
        nextCheck = Time.time + readyCheck;
        Refresh();

        UnitCombiner uc = UnitCombiner.Instance;
        foreach (KeyValuePair<UnitType, GameObject> kv in readyFx)
        {
            if (kv.Value == null) continue;
            bool on = uc != null && uc.CanMake(kv.Key);
            if (kv.Value.activeSelf != on) kv.Value.SetActive(on);
        }
    }

    void OnCombined(UnitType result, Vector3 at)
    {
        if (combineFlash == null) return;

        Vector3 cell;
        if (cells.TryGetValue(result, out cell))
            OneShot(transform.TransformPoint(cell + Vector3.up * 1.2f), combineFlashScale);

        // 필드 쪽이 실제로 눈에 들어오는 곳이다 — 조합표 앞에 서 있을 때만 조합하는 게 아니므로
        OneShot(at + Vector3.up * 1.2f, fieldFlashScale);
    }

    void OneShot(Vector3 at, float scale)
    {
        GameObject g = Instantiate(combineFlash, at, combineFlash.transform.rotation);
        g.transform.localScale = combineFlash.transform.localScale * scale;
        float life = 0f;
        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>())
            life = Mathf.Max(life, ps.main.duration + ps.main.startLifetime.constantMax);
        Destroy(g, life + 0.5f);
    }

    /// <summary>파티클 프리팹을 조합표 아래에 세운다. 이름이 `Recipe_` 로 시작해야 `Clear` 가 같이 치운다.</summary>
    GameObject Fx(GameObject prefab, Vector3 world, float scale, string name)
    {
        GameObject g = Instantiate(prefab, world, prefab.transform.rotation, transform);
        g.name = name;
        g.transform.localScale = prefab.transform.localScale * scale;   // 파티클 배율이 Hierarchy 라 이걸로 준다
        return g;
    }

    [Header("받침 색")]
    [Tooltip("받침 테두리. 성벽 장식과 같은 금 — 판 전체를 한 벌로 묶는다")]
    public Color rimGold = new Color(0.50f, 0.38f, 0.17f);

    [Tooltip("받침 안쪽. 바닥과 같은 남색 계열")]
    public Color padNavy = new Color(0.055f, 0.065f, 0.11f);

    [Range(0f, 1f)]
    [Tooltip("문화권 색을 회색 쪽으로 얼마나 빼는가. 0이면 원색 그대로")]
    public float cultureDesaturate = 0.4f;

    [Range(0f, 1f)]
    [Tooltip("문화권 색 밝기")]
    public float cultureBrightness = 0.62f;

    /// <summary>
    /// 칸 받침. **금 테두리 + 남색 원반 + 문화권 색 가는 고리.**
    ///
    /// 전에는 원반 전체를 문화권 색(노랑·하늘·빨강)으로 칠했다. 열은 잘 읽혔지만
    /// 남색+금으로 맞춘 판 위에 원색 원반 열두 개가 떠서 조합표만 다른 게임처럼
    /// 보였다. 색을 **면이 아니라 선으로** 줄이면 열 구분은 그대로 살고 판은 한 벌이 된다.
    ///
    /// 고리는 원반을 겹쳐 만든다 (색 원반 위에 조금 작은 남색 원반).
    /// 지름은 단계마다 다르다 — 강한 단계일수록 칸이 크다.
    /// </summary>
    void Pad(float x, float z, Color tint, float d)
    {
        string key = x + "_" + z;

        // **단(Dais) 윗면이 y=0.06 이다.** 그보다 낮게 깔면 단에 묻혀 안 보인다
        Disc("Recipe_PadRim_" + key, x, z, d, 0.08f, rimGold, 0.6f);
        Disc("Recipe_Pad_" + key, x, z, d * 0.92f, 0.09f, padNavy);

        if (tint.a <= 0f) return;

        // 원색은 판을 깬다 — 회색 쪽으로 빼고 어둡게
        float luma = tint.r * 0.3f + tint.g * 0.59f + tint.b * 0.11f;
        Color c = Color.Lerp(tint, new Color(luma, luma, luma), cultureDesaturate) * cultureBrightness;
        c.a = 1f;
        Disc("Recipe_PadRing_" + key, x, z, d * 0.74f, 0.10f, c);
        Disc("Recipe_PadCore_" + key, x, z, d * 0.64f, 0.11f, padNavy);
    }

    // ── 바닥: 문화권 구역 + 문장 ──────────────
    // 판이 민바닥이라 나무 셋이 빈 땅에 서 있는 것처럼 보였다. 나무마다 구역을 깔아
    // 판을 세 칸으로 나누고, 꼭대기 뒤에 그 문화권 문장을 새긴다

    [Header("바닥 구역")]
    [Tooltip("구역 판 색 = 남색에 문화권 색을 이만큼 섞는다. 크면 원색 판이 되어 판이 깨진다")]
    [Range(0f, 1f)] public float zoneTint = 0.22f;
    [Tooltip("구역 사이 틈")]
    public float zoneGap = 1.4f;
    [Tooltip("구역 금 테두리 두께")]
    public float zoneEdge = 0.14f;

    [Tooltip("문화권 문장 (MaterialTable.All 순서 — 그리스 · 북유럽 · 한국). 검은 바탕 금선 그림을 알파로 바꾼 것")]
    public Texture2D[] emblems;
    [Tooltip("문장 지름 = 구역 폭 × 이 값")]
    [Range(0.3f, 1f)] public float emblemFill = 0.92f;
    [Tooltip("바닥 문장은 무늬일 뿐이라 아주 옅게 — 알아보는 몫은 성벽 문장(Crest)")]
    [Range(0f, 1f)] public float emblemAlpha = 0.18f;

    void Zone(int ci, float cx, Color tint)
    {
        float w = treeGap - zoneGap;
        float z0 = rowTier2 - padTier2 * 0.5f - rootGap - frontDrop - 2f;       // 맨 앞 석물 쌍 아래
        // 바닥 문장이 4단계 칸을 중심으로 깔리므로 그 뒤쪽 반원까지 덮게 늘린다
        float z1 = rowTier4 + Mathf.Max(padTier4 * 0.5f + 3f, w * emblemFill * 0.5f + 0.8f);
        float zc = (z0 + z1) * 0.5f, d = z1 - z0;

        // **단(Dais) 윗면 y=0.06 과 받침(0.08~) 사이**에 깐다 — 받침을 덮으면 안 된다
        // 단색 판은 플라스틱처럼 떴다 — 둘레 바닥과 같은 돌 결에 문화권 색을 곱한다
        Color fill;
        if (zoneTex != null) { fill = Color.Lerp(Color.white, tint, zoneTexTint) * zoneTexBright; }
        else { fill = Color.Lerp(padNavy, tint * 0.55f, zoneTint); }
        fill.a = 1f;
        Box("Recipe_Zone_" + ci, cx, 0.065f, zc, w, 0.01f, d, fill, 0f, zoneTex, zoneNormal);

        Color edge = rimGold;
        float y = 0.07f;
        Box("Recipe_ZoneEdgeN_" + ci, cx, y, z1 - zoneEdge * 0.5f, w, 0.01f, zoneEdge, edge, 0.6f);
        Box("Recipe_ZoneEdgeS_" + ci, cx, y, z0 + zoneEdge * 0.5f, w, 0.01f, zoneEdge, edge, 0.6f);
        Box("Recipe_ZoneEdgeW_" + ci, cx - w * 0.5f + zoneEdge * 0.5f, y, zc, zoneEdge, 0.01f, d, edge, 0.6f);
        Box("Recipe_ZoneEdgeE_" + ci, cx + w * 0.5f - zoneEdge * 0.5f, y, zc, zoneEdge, 0.01f, d, edge, 0.6f);

        // 문장은 구역 바닥 한가운데에 **크게** 깐다. 4단계 칸 뒤에 작게 두었더니
        // 유닛에 가리고 원근에 눌려 있으나 마나였다. 칸 받침 · 화살표보다 아래에 옅게
        // 그래도 받침 · 화살표 · 유닛 · 명판이 전부 그 위에 올라가 가려지고, 비스듬한 카메라에
        // 원이 납작하게 눌렸다. **바닥에 두는 한 못 피한다** — 바닥 것은 아주 옅은 무늬로만 남기고,
        // 알아보는 몫은 나무 뒤 성벽 위에 세운 문장(Crest)이 맡는다
        if (emblems != null && ci < emblems.Length && emblems[ci] != null)
        {
            // 중심은 4단계 칸 — 제우스 · 오딘 · 환웅이 문장 한가운데 선다. 나무의 목표가 어디인지 바닥이 말한다
            Emblem("Recipe_Emblem_" + ci, emblems[ci], cx, rowTier4, w * emblemFill);
            Crest(ci, emblems[ci], cx);
        }

        // 구역의 빈 자리에 그 문화권 석물. **좌우 대칭으로 한 쌍씩, 줄을 맞춰** 세운다 —
        // 빈 곳마다 하나씩 채웠더니 중구난방으로 보였다. 줄은 단계 사이(2↔3, 3↔4)와 뿌리.
        // 사람 모양은 안 쓴다: 전시 유닛과 헷갈린다
        if (zoneProps != null && ci < zoneProps.Length && zoneProps[ci] != null)
        {
            float side = w * 0.5f - 2.2f;
            float root = rowTier2 - padTier2 * 0.5f - rootGap;
            // (z, 좌우 거리, 키 배율). 줄마다 키가 같다 — 앞줄만 작으면 그 줄만 다른 물건처럼 보였다.
            // 그래서 앞줄은 줄이지 않고 **더 앞으로** 내려 2단계 명판을 가리지 않게 한다
            Vector3[] rows =
            {
                new Vector3((rowTier2 + rowTier3) * 0.5f, side, 1f),
                new Vector3((rowTier3 + rowTier4) * 0.5f, side, 1f),
                new Vector3(root - frontDrop, side - 1.6f, 1f),
            };
            Culture c = MaterialTable.All[ci];
            int k = 0;
            foreach (Vector3 r in rows)
                for (int s = -1; s <= 1; s += 2, k++)
                {
                    GameObject g = ZoneProp("Recipe_ZoneProp_" + ci + "_" + k, zoneProps[ci], cx + s * r.y, r.x,
                                            zonePropHeight * r.z * Perspective(r.x));
                    if (g != null && Application.isPlaying) ZoneLight(g, c, k);
                }
        }
    }

    /// <summary>
    /// 줄마다 **화면에서 같은 크기**로 보이게 키를 보정한다. 카메라가 비스듬히 내려다봐서
    /// 같은 키라도 앞줄은 크고 뒷줄은 작게 보였다 — 카메라까지 거리에 비례해 키를 준다.
    /// 기준은 판 한가운데(z=0) 거리. 카메라 값은 CameraRig 에서 읽는다 (조합표를 볼 때의 자리)
    /// </summary>
    float Perspective(float z)
    {
        CameraRig rig = CameraRig.Instance != null ? CameraRig.Instance : FindFirstObjectByType<CameraRig>();
        float h = rig != null ? rig.height : 30f;
        float back = h / Mathf.Tan((rig != null ? rig.pitch : 36f) * Mathf.Deg2Rad);   // 카메라가 초점 뒤로 떨어진 거리
        float d = Mathf.Sqrt(h * h + (z + back) * (z + back));
        return d / Mathf.Sqrt(h * h + back * back);
    }

    [Header("구역 석물 빛 — 그리스 화로 불 · 북유럽 룬 · 한국 석등")]
    public Color greekFire = new Color(1f, 0.55f, 0.18f, 1f);
    [Tooltip("화로 불꽃 크기 (성배 불꽃과 같은 프리팹)")]
    public float greekFireScale = 3.4f;
    [Tooltip("룬스톤에서 빛날 자리 — 텍스처의 금 상감만 흰색인 마스크")]
    public Texture2D runeGlowMask;
    public Color runeGlow = new Color(0.45f, 0.80f, 1f, 1f);
    public float runeGlowIntensity = 6f;
    [Tooltip("석등 화사석 안에 띄우는 빛 알갱이")]
    public GameObject lanternGlow;
    public Color lanternLight = new Color(1f, 0.76f, 0.42f, 1f);
    [Range(0f, 1f)] public float lanternHeight = 0.6f;

    readonly List<Material> runeMats = new List<Material>();

    /// <summary>문화권마다 다른 빛. 불 · 룬 · 등불 모두 **조금씩 숨쉬게** — 가만히 있으면 형광등이다</summary>
    void ZoneLight(GameObject g, Culture c, int k)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        Vector3 top = transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
        float h = b.size.y;

        if (c == Culture.Greek)
        {
            // 화로 그릇 위로 불 — 성배 불꽃과 같은 손질(MapDecor.Flame)을 색만 바꿔 쓴다
            MapDecor md = FindFirstObjectByType<MapDecor>();
            if (md != null)
            {
                GameObject f = md.Flame(transform, top + Vector3.up * h * 0.06f, greekFireScale * (h / zonePropHeight), greekFire, 1.4f, 5f, 100 + k);
                if (f != null) f.name = "Recipe_ZoneFire";
            }
        }
        else if (c == Culture.Norse && runeGlowMask != null)
        {
            // 금 상감만 빛나게 — glTF 셰이더는 _EMISSIVE 키워드 + 발광 텍스처가 있어야 켜진다.
            // 재질은 사본 (프로퍼티 블록으로는 glTF 발광이 안 먹는다)
            foreach (Renderer r in rs)
            {
                Material m = new Material(r.sharedMaterial);
                m.EnableKeyword("_EMISSIVE");
                m.SetTexture("emissiveTexture", runeGlowMask);
                m.SetColor("emissiveFactor", runeGlow * runeGlowIntensity);
                r.sharedMaterial = m;
                runeMats.Add(m);
            }
        }
        else if (c == Culture.Korean)
        {
            // 화사석(불 넣는 칸)에서 새어 나오는 빛. 알갱이는 칸보다 조금 앞(카메라 쪽)에 — 안에 두면 돌에 가린다
            Vector3 at = top - Vector3.up * h * (1f - lanternHeight) - Vector3.forward * 0.15f;
            if (lanternGlow != null)
            {
                GameObject gl = Instantiate(lanternGlow, transform);
                gl.name = "Recipe_LanternGlow";
                gl.transform.localPosition = at;
                gl.transform.localScale = Vector3.one * 0.45f * (h / zonePropHeight);
            }
            GameObject lo = new GameObject("Recipe_LanternLight");
            lo.transform.SetParent(transform, false);
            lo.transform.localPosition = at;
            Light l = lo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = lanternLight;
            l.intensity = 0.7f;   // 세면 석등 몸통까지 하얗게 뜬다 — 빛은 화사석에서 새어 나오는 정도로
            l.range = 3f;
            l.shadows = LightShadows.None;
            DecorPulse p = lo.AddComponent<DecorPulse>();
            p.target = l;
            p.baseIntensity = l.intensity;
            p.amount = 0.12f;
            p.speed = 0.9f;
            p.phase = k * 1.7f;
        }
    }

    [Tooltip("구역 석물 (문화권 순서). 그리스 화로 기둥 · 북유럽 룬스톤 · 한국 석등")]
    public GameObject[] zoneProps;
    public float zonePropHeight = 3.4f;

    GameObject ZoneProp(string name, GameObject prefab, float x, float z, float height)
    {
        GameObject g = Instantiate(prefab, transform);
        g.name = name;
        g.transform.localPosition = new Vector3(x, 0.07f, z);
        // 전부 정면(카메라 쪽). 제각각 틀었더니 줄을 맞춰도 어수선했다
        g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        g.transform.localScale = Vector3.one;
        foreach (Collider c in g.GetComponentsInChildren<Collider>())
        { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }

        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return g;
        Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        if (b.size.y > 0.0001f) g.transform.localScale = Vector3.one * (height / b.size.y);

        // 바닥(단 윗면)에 앉힌다
        b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        g.transform.position += new Vector3(0f, transform.TransformPoint(new Vector3(0f, 0.07f, 0f)).y - b.min.y, 0f);
        foreach (Renderer r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return g;
    }

    [Tooltip("맨 앞 석물 쌍을 뿌리 명판에서 카메라 쪽으로 얼마나 내리나 — 2단계 명판을 가리지 않을 만큼")]
    public float frontDrop = 3.8f;
    [Tooltip("구역 판 돌 결 (MapDecor.auxFloorTex 와 같은 것). 비우면 단색")]
    public Texture2D zoneTex;
    public Texture2D zoneNormal;
    public float zoneTile = 9f;
    [Range(0f, 1f)] public float zoneTexTint = 0.5f;
    public float zoneTexBright = 0.9f;

    void Box(string name, float x, float y, float z, float sx, float sy, float sz, Color c, float metallic,
             Texture2D tex = null, Texture2D normal = null)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(x, y, z);
        g.transform.localScale = new Vector3(sx, sy, sz);
        Collider col = g.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }

        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", metallic > 0f ? 0.45f : 0.12f);
        m.SetFloat("_Metallic", metallic);
        if (tex != null)
        {
            // 판 크기에 맞춰 반복 — 판마다 결의 크기가 같아야 한 바닥으로 읽힌다
            Vector2 tiling = new Vector2(sx / Mathf.Max(0.5f, zoneTile), sz / Mathf.Max(0.5f, zoneTile));
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetTextureScale("_BumpMap", tiling);
                m.EnableKeyword("_NORMALMAP");
            }
        }
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    [Header("성벽 문장")]
    [Tooltip("나무 뒤 성벽 위에 세우는 문장 지름")]
    public float crestSize = 9f;
    [Tooltip("성벽 윗면에서 문장 아래끝까지 띄우는 높이")]
    public float crestLift = 0.2f;

    /// <summary>
    /// 나무 뒤 성벽 위에 **세워 건** 문장. 유닛이 가리지 않는 자리이고 뒤가 하늘이라 대비가 좋다.
    /// 카메라 쪽으로 카메라 각도만큼 뒤로 눕혀 원이 찌그러지지 않게 한다.
    /// 뒤에 남색 원판을 대서 하늘 별빛이 금선 사이로 비치지 않게.
    /// </summary>
    void Crest(int ci, Texture2D tex, float cx)
    {
        if (block == null && !string.IsNullOrEmpty(snapToBlock))
        {
            GameObject blk = GameObject.Find(snapToBlock);
            if (blk != null) block = blk.transform;
        }
        float hz = block != null ? block.lossyScale.z * 0.5f : 30f;
        MapDecor md = FindFirstObjectByType<MapDecor>();
        float wallTop = md != null ? md.wallHeight * md.recipeWallScale : 2.6f;
        CameraRig rig = CameraRig.Instance != null ? CameraRig.Instance : FindFirstObjectByType<CameraRig>();
        float tilt = rig != null ? rig.pitch : 36f;

        float z = hz - 1.2f;                                   // 뒤 성벽 바로 앞
        float y = wallTop + crestLift + crestSize * 0.5f;

        // 문장 · 받침 · 테두리 마법진 · 반짝임을 **한 축**에 묶는다 — 같이 오르내려야 한 물건이다.
        // 축은 카메라 각도만큼 뒤로 눕혀(윗부분을 뒤로) 판이 카메라를 정면으로 본다. 축의 XY 가 문장 면이다
        GameObject pivot = new GameObject("Recipe_CrestPivot_" + ci);
        pivot.transform.SetParent(transform, false);
        pivot.transform.localPosition = new Vector3(cx, y, z);
        pivot.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        crests.Add(new Crest3 { pivot = pivot.transform, baseY = y, phase = ci * 2.1f });

        // 받침 원판: 남색 — 하늘 별빛이 금선 사이로 비치지 않게
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "CrestBack";
        disc.transform.SetParent(pivot.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 원통 축을 판 법선으로
        disc.transform.localScale = new Vector3(crestSize * 1.04f, 0.05f, crestSize * 1.04f);
        Collider dc = disc.GetComponent<Collider>();
        if (dc != null) { if (Application.isPlaying) Destroy(dc); else DestroyImmediate(dc); }
        Material dm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        dm.SetColor("_BaseColor", padNavy);
        dm.SetFloat("_Smoothness", 0.35f);
        disc.GetComponent<Renderer>().sharedMaterial = dm;

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = "Crest";
        g.transform.SetParent(pivot.transform, false);
        g.transform.localScale = new Vector3(crestSize, crestSize, 1f);
        Collider col = g.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        g.GetComponent<Renderer>().sharedMaterial = EmblemMat(tex, 1f);
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (!Application.isPlaying) return;

        // 테두리 마법진 — 룬 고리가 문장 둘레를 두른다. 그냥 떠 있으면 개연성이 없어서,
        // "마법진이 문장을 붙잡고 있다" 로 읽히게. 룬 고리만 남기고 빛줄기 · 안쪽 고리 · 별은 끈다
        // (빛줄기는 위로 솟는 것이라 세운 판에선 카메라 쪽으로 쏟아지고, 안쪽 고리는 문장을 덮는다)
        if (crestFx != null)
        {
            GameObject fx = Instantiate(crestFx, pivot.transform);
            fx.name = "CrestRing";
            fx.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one * crestFxScale;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name == "Runes") { ps.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); continue; }   // 판과 같은 면으로
                if (ps.gameObject == fx) { ParticleSystem.EmissionModule em = ps.emission; em.enabled = false; }
                else ps.gameObject.SetActive(false);
            }
        }

        // 테두리 반짝임 — 문장 **가장자리에서만** 생겨 둘레를 따라 돈다. 가운데로 들어가면 그림을 덮는다
        if (crestSparkleMat != null) RimSparkle(pivot.transform);
    }

    [Tooltip("테두리 반짝임 알갱이 재질 (가산 · 별 모양)")]
    public Material crestSparkleMat;
    public Color crestSparkleColor = new Color(1f, 0.85f, 0.45f, 1f);

    void RimSparkle(Transform pivot)
    {
        GameObject go = new GameObject("CrestSparkle");
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = crestSparkleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 120;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 28f;

        // 원 둘레(XY 면)에서만 — 두께 0 이면 가장자리 선 위에서만 생긴다
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = crestSize * 0.52f;
        sh.radiusThickness = 0f;

        // 둘레를 따라 천천히 돈다
        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = 0.5f;

        // 생겼다 사라질 때 깜박이지 않게 부드럽게
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = crestSparkleMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    [Tooltip("문장 테두리 마법진 (룬 고리)")]
    public GameObject crestFx;
    [Tooltip("룬 고리 크기 — 고리 지름 ≈ 2 × 이 값. 문장보다 조금 크게")]
    public float crestFxScale = 5.3f;
    [Tooltip("문장이 오르내리는 높이")]
    public float crestBob = 0.25f;

    struct Crest3 { public Transform pivot; public float baseY, phase; }
    readonly List<Crest3> crests = new List<Crest3>();

    Material EmblemMat(Texture2D tex, float alpha)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
        // 투명(알파 섞기)로 — URP 재질 창이 해 주는 설정을 손으로
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    /// <summary>바닥에 눕힌 반투명 문장. 그림자 없이, 빛 안 받게(Unlit) — 새김이라 조명에 따라 사라지면 안 된다</summary>
    void Emblem(string name, Texture2D tex, float x, float z, float size)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(x, 0.075f, z);
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        g.transform.localScale = new Vector3(size, size, 1f);
        Collider col = g.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }

        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = EmblemMat(tex, emblemAlpha);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Disc(string name, float x, float z, float diameter, float y, Color c, float metallic = 0f)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(x, y, z);
        g.transform.localScale = new Vector3(diameter, 0.01f, diameter);

        Collider col = g.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
        }

        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", metallic > 0f ? 0.45f : 0.15f);
        m.SetFloat("_Metallic", metallic);
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    void Place(UnitType t, float x, float z)
    {
        UnitTable.Stats s = UnitTable.Get(t);

        float size = (s.tier == 1 ? 1.2f : s.tier == 2 ? 1.6f : s.tier == 3 ? 2.0f : 2.5f) * scaleMult;

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "Recipe_" + s.name;
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(x, size * 0.45f, z);
        g.transform.localScale = new Vector3(size, size * 0.45f, size);
        // 카메라를 보게 돌려 세운다 — 모델 앞이 +z 라 그대로 두면 판 안쪽을 보고 서서 등만 보였다
        g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // 전시용 — 클릭 대상이 아니다
        Collider col = g.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
        }

        Renderer r = g.GetComponent<Renderer>();

        // 등록된 모델이 있으면 실린더 대신 그걸 세운다. 없으면 색 실린더.
        if (UnitArt.Attach(g.transform, t, size))
        {
            if (r != null) r.enabled = false;
            Animate(g, t);
        }
        else if (r != null)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = s.color;
            r.sharedMaterial = m;
        }

        shown.Add(g.transform);
    }

    /// <summary>
    /// 전시 유닛도 **전투 유닛과 똑같이** 대기 모션을 돌린다.
    ///
    /// 모델에 애니메이터와 컨트롤러는 `UnitArt.Attach` 가 이미 붙인다. 그런데
    /// 대기 배속(`IdleSpeed`)은 `ActorAnimator` 가 넣는 값이라, 그게 없는 전시
    /// 유닛은 기본값 0 으로 멈춰 섰다 — 휴머노이드 컨트롤러는 대기 상태 배속을
    /// 이 파라미터에 걸어 놨다. 전시장만 T포즈 마네킹이 늘어선 이유다.
    ///
    /// 무기를 든 유닛은 대기 위상도 못 박아야 무기가 바로 선다 (`Unit.Setup` 과 같은 값).
    /// 에디터에서는 애니메이터가 돌지 않으므로 몇 프레임 밀어 첫 자세라도 세워 둔다.
    /// </summary>
    void Animate(GameObject g, UnitType t)
    {
        ActorAnimator anim = g.GetComponent<ActorAnimator>();
        if (anim == null) anim = g.AddComponent<ActorAnimator>();

        UnitArt.Entry e = UnitArt.Instance != null ? UnitArt.Instance.Find(t) : null;
        if (e != null)
        {
            anim.idleSpeed   = e.idleSpeed;
            anim.idlePhase   = e.idlePhase;
            anim.attackCycle = e.attackCycle;
        }
        anim.Rebind();

        if (!Application.isPlaying && anim.animator != null && anim.Ready)
            for (int s = 0; s < 3; s++) anim.animator.Update(0.02f);
    }

    void Clear()
    {
        // 에디터에서는 Destroy 가 즉시 지우지 않아 Build 를 부를 때마다 겹쳐 쌓인다.
        // 목록에 없는 잔여물까지 이름으로 훑어 지운다.
        foreach (Transform t in shown)
        {
            if (t == null) continue;
            if (Application.isPlaying) Destroy(t.gameObject);
            else DestroyImmediate(t.gameObject);
        }
        shown.Clear();
        plates.Clear();
        cells.Clear();
        foreach (Material m in runeMats) if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
        runeMats.Clear();
        crests.Clear();
        readyFx.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (!c.name.StartsWith("Recipe_")) continue;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }

    // ── 명판 글 ──────────────────────────
    const string Have = "#9CE8A4", Miss = "#F09A86";

    static string Count(int n, int need)
    {
        return "<color=" + (n >= need ? Have : Miss) + ">" + Mathf.Min(n, 99) + "/" + need + "</color>";
    }

    /// <summary>보유 수를 다시 세서 명판 글을 새로 쓴다. 창고에 든 유닛도 센다 (`UnitCombiner.CountOf`).</summary>
    void Refresh()
    {
        UnitCombiner uc = UnitCombiner.Instance;
        MaterialBank mb = MaterialBank.Instance;

        foreach (Plate p in plates)
        {
            switch (p.kind)
            {
                case PlateKind.Tier2:
                    p.title = UnitTable.Get(p.type).name;
                    p.line = UnitTable.Get(p.need).name + " " + Count(uc != null ? uc.CountOf(p.need) : 0, 2);
                    break;

                case PlateKind.Tier3:
                {
                    int got = 0;
                    UnitType[] trio = UnitTable.Tier2Of(p.culture);
                    foreach (UnitType t in trio) if (uc != null && uc.CountOf(t) >= 1) got++;
                    p.title = UnitTable.Get(p.type).name;
                    p.line = "아래 셋 " + Count(got, trio.Length);
                    break;
                }

                case PlateKind.Tier4:
                    p.title = UnitTable.Get(p.type).name;
                    p.line = UnitTable.Get(p.need).name + " " + Count(uc != null ? uc.CountOf(p.need) : 0, 2);
                    break;

                case PlateKind.Culture:
                    p.title = MaterialTable.CultureName(p.culture);
                    p.line = "+ " + MaterialTable.Name(p.culture) + " " + Count(mb != null ? mb.Get(p.culture) : 0, 1);
                    break;

                case PlateKind.Times:
                    p.title = "×2";
                    p.line = null;
                    break;
            }

            p.plain = p.line != null ? System.Text.RegularExpressions.Regex.Replace(p.line, "<.*?>", "") : "";
            p.ready = uc != null && (p.kind == PlateKind.Tier2 || p.kind == PlateKind.Tier3 || p.kind == PlateKind.Tier4)
                      && uc.CanMake(p.type);
        }
    }

    GUIStyle titleStyle, lineStyle, shadowStyle, headStyle, timesStyle;
    GenesisHud hud;

    int styledFor;

    void Styles()
    {
        // OnGUI 는 화면 픽셀 그대로 그린다 — HUD(1920×1080 기준 배율)와 같이 커지고 작아지게
        // 화면 높이가 바뀌면 다시 만든다. 전엔 고정 픽셀이라 창을 줄이면 명판만 커 보였다
        if (titleStyle != null && styledFor == Screen.height) return;
        styledFor = Screen.height;
        float k = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);

        // **공용 스킨을 건드리지 않는다.** 예전엔 `GUI.skin.label.fontSize` 를 바꿨는데,
        // 다른 OnGUI 창들도 같은 스킨을 써서 누가 먼저 그리느냐에 따라 글자 크기가 바뀌었다
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.font = titleFont != null ? titleFont : labelFont;
        titleStyle.fontSize = Mathf.RoundToInt(17 * k);
        titleStyle.fontStyle = titleFont != null ? FontStyle.Normal : FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
        titleStyle.padding = new RectOffset(0, 0, 0, 0);
        titleStyle.wordWrap = false;

        lineStyle = new GUIStyle(titleStyle);
        lineStyle.font = labelFont;
        lineStyle.fontSize = Mathf.RoundToInt(14 * k);
        lineStyle.fontStyle = FontStyle.Bold;
        lineStyle.richText = true;
        lineStyle.normal.textColor = new Color(0.78f, 0.80f, 0.86f);

        shadowStyle = new GUIStyle(lineStyle);
        shadowStyle.richText = false;
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);

        headStyle = new GUIStyle(titleStyle);
        headStyle.fontSize = Mathf.RoundToInt(22 * k);

        timesStyle = new GUIStyle(titleStyle);
        timesStyle.font = labelFont;
        timesStyle.fontStyle = FontStyle.Bold;
        timesStyle.fontSize = Mathf.RoundToInt(18 * k);
        timesStyle.normal.textColor = arrowColor;
    }

    void OnGUI()
    {
        if (!showLabels || plates.Count == 0) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (!Looking()) return;
        Styles();

        // HUD 위아래 띠에 걸리는 명판은 안 그린다 — OnGUI 는 HUD 보다 위에 그려져 콘솔을 뚫고 나온다
        if (hud == null) hud = FindFirstObjectByType<GenesisHud>();
        float ui = Screen.height / 1080f;
        float top = (hud != null ? hud.topBarHeight : 0f) * ui;
        float bottom = Screen.height - (hud != null ? hud.consoleHeight : 0f) * ui;

        foreach (Plate p in plates)
        {
            Vector3 w = transform.TransformPoint(p.at);
            if ((w - cam.transform.position).sqrMagnitude > labelRange * labelRange) continue;

            Vector3 sp = cam.WorldToScreenPoint(w);
            if (sp.z < 0f) continue;
            Draw(p, sp.x, Screen.height - sp.y, top, bottom);
        }
    }

    /// <summary>
    /// 카메라가 조합표 블록을 보고 있는가 — 화면 한가운데가 바닥에 닿는 점이 블록 안이면.
    /// 거리로만 거르면 옆 블록(전투 · 연구소)을 볼 때도 먼 명판이 화면 가장자리에 겹쳐 떴다.
    /// </summary>
    bool Looking()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Mathf.Abs(ray.direction.y) < 0.001f) return false;
        float t = (transform.position.y - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;
        Vector3 local = transform.InverseTransformPoint(ray.origin + ray.direction * t);

        // OnGUI 는 한 프레임에 여러 번 불린다 — 블록은 한 번만 찾는다
        if (block == null && !string.IsNullOrEmpty(snapToBlock))
        {
            GameObject blk = GameObject.Find(snapToBlock);
            if (blk != null) block = blk.transform;
        }
        float hx = block != null ? block.lossyScale.x * 0.5f : 30f;
        float hz = block != null ? block.lossyScale.z * 0.5f : 30f;
        return Mathf.Abs(local.x) <= hx && Mathf.Abs(local.z) <= hz;
    }

    Transform block;

    void Draw(Plate p, float x, float y, float top, float bottom)
    {
        // ×2 는 명판 없이 글자만 — 화살표에 붙은 꼬리표다
        if (p.kind == PlateKind.Times)
        {
            Vector2 sz = timesStyle.CalcSize(new GUIContent(p.title));
            Rect r = new Rect(p.anchor == TextAnchor.MiddleRight ? x - sz.x : x, y - sz.y * 0.5f, sz.x, sz.y);
            if (r.yMin < top || r.yMax > bottom) return;
            Shadowed(r, p.title, timesStyle);
            return;
        }

        // 문화권 이름은 크게, 문화권 색으로
        GUIStyle tst = titleStyle;
        if (p.kind == PlateKind.Culture)
        {
            tst = headStyle;
            tst.normal.textColor = Color.Lerp(MaterialTable.Color(p.culture), Color.white, 0.3f);
        }

        string plain = p.plain;
        Vector2 ts = p.title != null ? tst.CalcSize(new GUIContent(p.title)) : Vector2.zero;
        Vector2 ls = p.line != null ? lineStyle.CalcSize(new GUIContent(plain)) : Vector2.zero;

        const float padX = 8f, padY = 4f;
        float wBox = Mathf.Max(ts.x, ls.x) + padX * 2f;
        float hBox = ts.y + ls.y + padY * 2f;

        Rect box = p.anchor == TextAnchor.MiddleLeft
            ? new Rect(x, y - hBox * 0.5f, wBox, hBox)
            : new Rect(x - wBox * 0.5f, y + 4f, wBox, hBox);
        if (box.yMin < top || box.yMax > bottom) return;

        // 만들 수 있는 칸은 명판이 초록으로 — 반짝임과 같은 신호를 글자 쪽에도
        GUI.color = p.ready ? new Color(0.06f, 0.30f, 0.14f, 0.85f) : new Color(0.02f, 0.03f, 0.07f, 0.72f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = p.ready ? new Color(0.45f, 1f, 0.55f, 0.9f) : new Color(0.92f, 0.72f, 0.34f, 0.55f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.x, box.yMax - 1f, box.width, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cy = box.y + padY;
        if (p.title != null)
        {
            Shadowed(new Rect(box.x, cy, box.width, ts.y), p.title, tst);
            cy += ts.y;
        }
        if (p.line != null)
        {
            Rect lr = new Rect(box.x, cy, box.width, ls.y);
            GUI.Label(new Rect(lr.x + 1f, lr.y + 1f, lr.width, lr.height), plain, shadowStyle);
            GUI.Label(lr, p.line, lineStyle);
        }
    }

    void Shadowed(Rect r, string text, GUIStyle st)
    {
        Color keep = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, st);
        st.normal.textColor = keep;
        GUI.Label(r, text, st);
    }
}

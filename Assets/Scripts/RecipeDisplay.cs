using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조합표를 **실제 유닛을 세워놓은 격자**로 보여준다. 별도 창이 없다.
/// 워크3 유즈맵이 조합표를 보여주던 방식 — 걸어가서 눈으로 읽는다.
///
///          그리스      북유럽      한국
///  전사 ×2  미노타우로스 광전사      도깨비
///  궁수 ×2  하피         발키리      도사
///  사제 ×2  오라클       룬마녀      구미호
///  ─────────────────────────────────────
///  3단계    티탄         요툰        이무기
///  4단계    제우스       오딘        환웅
///
/// **격자는 표에서 스스로 만들어진다.** 1단계 유닛이나 문화권이 늘어나면
/// 행·열이 알아서 늘어난다 — 여기를 고칠 필요가 없다.
/// </summary>
public class RecipeDisplay : MonoBehaviour
{
    [Header("격자")]
    public float colGap = 6.5f;
    public float rowGap = 5.5f;

    [Tooltip("조합표 유닛 크기. **카메라 높이를 올리면 같이 올려야 한다** — " +
             "전투용 크기 그대로 두면 걸어가서 읽을 수가 없다")]
    public float scaleMult = 0.9f;

    [Range(0.4f, 1f)]
    [Tooltip("칸 받침 지름을 칸 간격의 몇 배로. 1에 가까우면 받침끼리 붙는다")]
    public float padRatio = 0.78f;

    [Header("이름표")]
    public bool showLabels = true;
    public float labelRange = 60f;

    [Header("자리")]
    [Tooltip("이 블록 한복판으로 자기 위치를 맞춘다. 비우면 지금 위치를 그대로 쓴다.\n\n" +
             "**좌표를 손으로 맞추지 않는다** — 블록을 옮겼을 때 조합표만 제자리에 " +
             "남아 허공에 뜬 적이 있다")]
    public string snapToBlock = "Block_Recipe";

    readonly List<Transform> shown = new List<Transform>();
    readonly List<string> names = new List<string>();

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

    int cols, rows;
    Camera cam;

    void Start()
    {
        Build();
    }

    public void Build()
    {
        Clear();
        Snap();

        UnitType[] bases = UnitTable.OfTier(1);
        Culture[] cultures = MaterialTable.All;

        // 열 = 재료칸 1 + 문화권 수,  행 = 1단계 수 + 3단계 + 4단계
        cols = 1 + cultures.Length;
        rows = bases.Length + 2;

        // 칸 받침을 먼저 깐다 — 유닛만 띄엄띄엄 세워 두면 표가 아니라
        // 그냥 모여 선 무리로 보인다. **문화권 색으로 물들여 열을 읽게 한다**
        for (int row = 0; row < rows; row++)
        {
            // 재료 열(0번)은 문화권이 없다 — 고리 없이 금 테두리만
            Pad(Col(0), Row(row), Color.clear, row < bases.Length);

            for (int col = 0; col < cultures.Length; col++)
                Pad(Col(col + 1), Row(row), MaterialTable.Color(cultures[col]), true);
        }

        for (int row = 0; row < bases.Length; row++)
        {
            Place(bases[row], Col(0), Row(row));

            for (int col = 0; col < cultures.Length; col++)
            {
                UnitType res;
                if (!UnitTable.TryCombine(bases[row], cultures[col], out res)) continue;
                Place(res, Col(col + 1), Row(row));
            }
        }

        // 3·4단계는 문화권마다 하나씩
        for (int col = 0; col < cultures.Length; col++)
        {
            PlaceAll(UnitTable.OfTier(3, cultures[col]), Col(col + 1), Row(bases.Length));
            PlaceAll(UnitTable.OfTier(4, cultures[col]), Col(col + 1), Row(bases.Length + 1));
        }
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
    /// `filled` 가 false 면 그 칸은 비어 있다는 뜻이라 흐린 테두리만 깐다.
    /// `tint` 의 알파가 0 이면 문화권이 없는 칸(재료 열)이라 고리를 안 넣는다.
    /// </summary>
    void Pad(float x, float z, Color tint, bool filled)
    {
        float d = Mathf.Min(colGap, rowGap) * padRatio;
        string key = x + "_" + z;

        // **단(Dais) 윗면이 y=0.06 이다.** 그보다 낮게 깔면 단에 묻혀 안 보인다
        if (!filled)
        {
            Disc("Recipe_PadRim_" + key, x, z, d, 0.08f, Color.Lerp(rimGold, padNavy, 0.7f));
            Disc("Recipe_Pad_" + key, x, z, d * 0.94f, 0.085f, padNavy);
            return;
        }

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

    /// <summary>한 칸에 여러 종류가 걸리면 가로로 조금씩 밀어 나란히 세운다.</summary>
    void PlaceAll(UnitType[] types, float x, float z)
    {
        if (types.Length == 0) return;

        float step = colGap * 0.45f;
        float start = -(types.Length - 1) * 0.5f * step;

        for (int i = 0; i < types.Length; i++)
            Place(types[i], x + start + i * step, z);
    }

    // 격자를 가운데 정렬한다 — 열·행 수가 바뀌어도 조합표 블록 한복판에 놓이게
    float Col(int i) => (i - (cols - 1) * 0.5f) * colGap;
    float Row(int i) => ((rows - 1) * 0.5f - i) * rowGap;

    void Place(UnitType t, float x, float z)
    {
        UnitTable.Stats s = UnitTable.Get(t);

        float size = (s.tier == 1 ? 1.2f : s.tier == 2 ? 1.6f : s.tier == 3 ? 2.0f : 2.5f) * scaleMult;

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "Recipe_" + s.name;
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(x, size * 0.45f, z);
        g.transform.localScale = new Vector3(size, size * 0.45f, size);

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
        names.Add(s.name);
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
        names.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (!c.name.StartsWith("Recipe_")) continue;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }

    // 이름표 — 실제 모델이 들어와도 어느 조합인지 읽으려면 필요하다
    void OnGUI()
    {
        if (!showLabels) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        GUI.skin.label.fontSize = 12;

        for (int i = 0; i < shown.Count; i++)
        {
            Transform t = shown[i];
            if (t == null) continue;

            Vector3 world = t.position;
            if ((world - cam.transform.position).sqrMagnitude > labelRange * labelRange) continue;

            Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * 1.3f);
            if (sp.z < 0f) continue;
            if (sp.x < -80f || sp.x > Screen.width + 80f) continue;

            float y = Screen.height - sp.y;
            GUI.Label(new Rect(sp.x - 45f, y - 8f, 90f, 18f), names[i]);
        }
    }
}

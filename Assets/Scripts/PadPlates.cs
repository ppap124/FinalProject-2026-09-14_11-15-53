using UnityEngine;

/// <summary>
/// 영혼 블록 통로 입구에 **패드가 무엇을 하는지 · 영혼이 몇 개 쌓였는지** 명판을 세운다.
///
/// 제단 모양(문 · 금화 더미 · 수정)만으로는 처음 보는 사람이 어느 패드가 무엇인지 몰랐고,
/// 영혼을 몇 개 더 넣어야 발동하는지는 어디에도 안 나왔다. 조합표 명판(`RecipeDisplay`)과
/// 같은 모양으로 그린다 — 카메라가 영혼 블록을 볼 때만.
///
/// 건물 블록의 **연구소 · 창고** 명판도 여기서 그린다 (이름 + 살 수 있는 연구 · 보관 수).
/// </summary>
public class PadPlates : MonoBehaviour
{
    [Tooltip("명판을 패드에서 카메라 쪽(-z)으로 이만큼 앞에 — 통로 입구")]
    public float frontOffset = 20f;

    public string block = "Block_Soul";

    TriggerBlock[] pads;
    Transform blockT;
    Camera cam;
    GenesisHud hud;
    GUIStyle titleStyle, lineStyle;

    static readonly Color UnitCol = new Color(1f, 0.55f, 0.45f);
    static readonly Color GoldCol = new Color(1f, 0.85f, 0.40f);
    static readonly Color MatCol  = new Color(0.50f, 0.95f, 0.62f);

    void OnGUI()
    {
        if (!glyphsWarm) { glyphsWarm = true; WarmGlyphs(hud != null ? hud : (hud = FindFirstObjectByType<GenesisHud>())); }
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (pads == null) pads = FindObjectsByType<TriggerBlock>(FindObjectsSortMode.None);
        bool soul = Looking(block, ref blockT), lab = Looking(labBlock, ref labT);
        if (!soul && !lab) return;
        Styles();

        if (hud == null) hud = FindFirstObjectByType<GenesisHud>();
        float ui = Screen.height / 1080f;
        float top = (hud != null ? hud.topBarHeight : 0f) * ui;
        float bottom = Screen.height - (hud != null ? hud.consoleHeight : 0f) * ui;

        if (lab) DrawBuildings(top, bottom);
        if (!soul) return;

        foreach (TriggerBlock p in pads)
        {
            if (p == null || p.soulCost <= 0) continue;

            string title; Color col; string extra = "";
            switch (p.action)
            {
                case TriggerBlock.Action.PullUnit:     title = "유닛 소환"; col = UnitCol; break;
                case TriggerBlock.Action.Exchange:     title = "금화";      col = GoldCol; break;
                case TriggerBlock.Action.PullMaterial:
                    title = "재료"; col = MatCol;
                    if (MaterialShop.Instance != null) extra = " · " + Mathf.RoundToInt(MaterialShop.Instance.dropChance * 100f) + "%";
                    break;
                default: continue;
            }
            // 한 개에 한 번 발동하면 "0/1" 은 늘 같은 글이라 정보가 없다 — 그땐 값만 적는다
            string line = p.soulCost > 1 ? "영혼 " + p.Stored + "/" + p.soulCost + extra
                                         : "영혼 1개" + extra;
            // 인구수가 차면 유닛 패드는 영혼을 안 먹는다 — 왜 안 들어가는지 여기서 보여 준다
            bool full = p.action == TriggerBlock.Action.PullUnit && SoulShop.Instance != null && SoulShop.Instance.AtCap;
            if (full) line = "인구 가득 " + SoulShop.Instance.UnitCount + "/" + SoulShop.Instance.unitCap;

            Vector3 w = p.transform.position; w.y = 0f; w.z -= frontOffset;
            Plate(w, title, line, col, full ? new Color(1f, 0.45f, 0.40f) : (Color?)null, top, bottom);
        }
    }

    /// <summary>명판 한 장 — 바닥 점 w 위에, 제목 한 줄 + 설명 한 줄. 위 띠 · 콘솔에 걸리면 안 그린다</summary>
    void Plate(Vector3 w, string title, string line, Color col, Color? lineCol, float top, float bottom)
    {
        Vector3 sp = cam.WorldToScreenPoint(w);
        if (sp.z < 0f) return;

        Vector2 ts = titleStyle.CalcSize(new GUIContent(title));
        Vector2 ls = lineStyle.CalcSize(new GUIContent(line));
        const float padX = 10f, padY = 4f;
        float bw = Mathf.Max(ts.x, ls.x) + padX * 2f, bh = ts.y + ls.y + padY * 2f;
        Rect box = new Rect(sp.x - bw * 0.5f, Screen.height - sp.y - bh * 0.5f, bw, bh);
        if (box.yMin < top || box.yMax > bottom) return;

        GUI.color = new Color(0.02f, 0.03f, 0.07f, 0.72f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(col.r, col.g, col.b, 0.8f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.x, box.yMax - 1f, box.width, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        titleStyle.normal.textColor = col;
        Shadowed(new Rect(box.x, box.y + padY, box.width, ts.y), title, titleStyle);
        Color lineKeep = lineStyle.normal.textColor;
        if (lineCol.HasValue) lineStyle.normal.textColor = lineCol.Value;
        Shadowed(new Rect(box.x, box.y + padY + ts.y, box.width, ls.y), line, lineStyle);
        lineStyle.normal.textColor = lineKeep;
    }

    // ── 건물 블록 — 연구소 · 창고 ─────────────

    [Header("건물 블록")]
    public string labBlock = "Block_Lab";
    [Tooltip("명판 줄을 두 건물 중 **덜 튀어나온** 앞면에서 카메라 쪽(-z)으로 이만큼 앞에 — 두 명판을 한 줄에 세운다")]
    public float buildingFront = 1f;

    Transform labT;
    Bounds researchBox, warehouseBox;
    bool researchFound, warehouseFound;

    static readonly Color ResearchCol  = new Color(0.62f, 0.78f, 1f);
    static readonly Color WarehouseCol = new Color(1f, 0.80f, 0.52f);
    static readonly Color ReadyCol     = new Color(0.55f, 1f, 0.62f);

    /// <summary>
    /// 두 건물 앞에 이름과 **지금 할 수 있는 것**을 세운다. 영혼 블록 패드와 달리 건물은 모양만으로
    /// 무엇인지 알 수 없었다 (돔 · 기와집). 자리는 보이는 건물(MapDecor 의 *Prop)의 앞면에서 잰다 —
    /// 건물을 옮기거나 키워도 명판이 따라간다
    /// </summary>
    void DrawBuildings(float top, float bottom)
    {
        if (!researchFound) researchFound = PropBounds("ResearchProp", ref researchBox);
        if (!warehouseFound) warehouseFound = PropBounds("WarehouseProp", ref warehouseBox);

        ResearchLab lab = ResearchLab.Instance;
        if (researchFound && lab != null)
        {
            int ready = 0, cheapest = int.MaxValue;
            foreach (Research r in System.Enum.GetValues(typeof(Research)))
            {
                if (lab.CanBuy(r)) ready++;
                int c = lab.CostOf(r);
                if (c > 0 && c < cheapest) cheapest = c;
            }
            string line = ready > 0 ? "살 수 있는 연구 " + ready + "개"
                        : cheapest < int.MaxValue ? "다음 연구 금화 " + cheapest : "클릭해서 열기";
            Plate(Front(researchBox), "연구소", line, ResearchCol, ready > 0 ? ReadyCol : (Color?)null, top, bottom);
        }

        Warehouse wh = Warehouse.Instance;
        if (warehouseFound && wh != null)
        {
            int n = 0;
            foreach (UnitType t in wh.Kinds()) n += wh.Get(t);
            string line = n > 0 ? "보관 " + n + "마리" + (wh.CanMove ? "" : " · 꺼내기는 준비 시간에")
                                : "비어 있음 · 유닛을 골라 C";
            Plate(Front(warehouseBox), "창고", line, WarehouseCol, null, top, bottom);
        }
    }

    // 두 명판을 한 줄로 — 연구소 돔은 계단 받침이 넓어 제 앞면에 세우면 명판이 블록 앞 화로 위까지 나갔다
    Vector3 Front(Bounds b)
    {
        float z = b.min.z;
        if (researchFound && warehouseFound) z = Mathf.Max(researchBox.min.z, warehouseBox.min.z);
        return new Vector3(b.center.x, 0f, z - buildingFront);
    }

    static bool PropBounds(string name, ref Bounds box)
    {
        GameObject g = GameObject.Find(name);
        if (g == null) return false;
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        box = rs[0].bounds;
        foreach (Renderer r in rs) box.Encapsulate(r.bounds);
        return true;
    }

    /// <summary>카메라가 이 블록을 보고 있는가 — 화면 한가운데가 바닥에 닿는 점이 블록 안이면 (조합표와 같다)</summary>
    bool Looking(string name, ref Transform t)
    {
        if (t == null) { GameObject g = GameObject.Find(name); if (g != null) t = g.transform; }
        if (t == null) return false;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Mathf.Abs(ray.direction.y) < 0.001f) return false;
        float d = -ray.origin.y / ray.direction.y;
        if (d < 0f) return false;
        Vector3 hit = ray.origin + ray.direction * d;
        Vector3 c = t.position, s = t.lossyScale;
        return Mathf.Abs(hit.x - c.x) <= s.x * 0.5f && Mathf.Abs(hit.z - c.z) <= s.z * 0.5f;
    }

    int styledFor;

    void Styles()
    {
        // HUD 와 같은 배율로 — 화면 높이가 바뀌면 다시 만든다 (조합표 명판과 같다)
        if (titleStyle != null && styledFor == Screen.height) return;
        styledFor = Screen.height;
        float k = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
        if (hud == null) hud = FindFirstObjectByType<GenesisHud>();

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.font = hud != null && hud.fontTitle != null ? hud.fontTitle : (hud != null ? hud.fontBold : null);
        titleStyle.fontSize = Mathf.RoundToInt(18 * k);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.padding = new RectOffset(0, 0, 0, 0);
        titleStyle.wordWrap = false;

        lineStyle = new GUIStyle(titleStyle);
        lineStyle.font = hud != null ? hud.fontBold : null;
        lineStyle.fontSize = Mathf.RoundToInt(14 * k);
        lineStyle.normal.textColor = new Color(0.82f, 0.84f, 0.90f);

    }

    // ── 글자 미리 재기 ──
    // 명판 · 조합표 · 보스 이름표(IMGUI)는 한글 글자를 **처음 그리는 순간** 글꼴 엔진이 그 글자의 자간 표(GPOS · GSUB)를
    // 읽느라 한 프레임에 60~170ms 를 썼다 — 카메라를 연구소로 처음 옮길 때 화면이 툭 멈추던 원인 (빌드 프로파일, 2026-10-16).
    // 판 첫 프레임에 쓸 글자를 한꺼번에 재 둔다. 글꼴 자산은 씬을 다시 열어도 남으므로 한 번이면 된다
    static bool glyphsWarm;

    static void WarmGlyphs(GenesisHud hud)
    {
        if (hud == null) return;
        var sb = new System.Text.StringBuilder(
            "0123456789/·%×+-:().,!?★ ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "영혼 유닛 소환 금화 재료 인구 가득 살 수 있는 연구 개 다음 클릭해서 열기 창고 보관 마리 꺼내기는 준비 시간에 비어 있음 골라" +
            "라운드 보스 카오스 단계 그리스 북유럽 한국 히든 시너지");
        foreach (UnitType t in System.Enum.GetValues(typeof(UnitType))) sb.Append(UnitTable.Get(t).name);
        foreach (Culture c in MaterialTable.All) sb.Append(MaterialTable.Name(c)).Append(MaterialTable.CultureName(c));
        if (MonsterArt.Instance != null && MonsterArt.Instance.entries != null)
            foreach (MonsterArt.Entry e in MonsterArt.Instance.entries) if (e != null) sb.Append(e.label);

        var seen = new System.Collections.Generic.HashSet<char>();
        var chars = new System.Text.StringBuilder();
        foreach (char ch in sb.ToString()) if (seen.Add(ch)) chars.Append(ch);
        var content = new GUIContent(chars.ToString());

        foreach (Font f in new[] { hud.fontTitle, hud.fontBold })
        {
            if (f == null) continue;
            var st = new GUIStyle(GUI.skin.label) { font = f, fontSize = 16, wordWrap = false };
            st.CalcSize(content);
            st.fontStyle = FontStyle.Bold;   // 조합표 줄 글자는 굵게 그린다
            st.CalcSize(content);
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

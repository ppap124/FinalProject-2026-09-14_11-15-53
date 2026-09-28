using UnityEngine;

/// <summary>
/// 영혼 블록 통로 입구에 **패드가 무엇을 하는지 · 영혼이 몇 개 쌓였는지** 명판을 세운다.
///
/// 제단 모양(문 · 금화 더미 · 수정)만으로는 처음 보는 사람이 어느 패드가 무엇인지 몰랐고,
/// 영혼을 몇 개 더 넣어야 발동하는지는 어디에도 안 나왔다. 조합표 명판(`RecipeDisplay`)과
/// 같은 모양으로 그린다 — 카메라가 영혼 블록을 볼 때만.
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
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (pads == null) pads = FindObjectsByType<TriggerBlock>(FindObjectsSortMode.None);
        if (!Looking()) return;
        Styles();

        if (hud == null) hud = FindFirstObjectByType<GenesisHud>();
        float ui = Screen.height / 1080f;
        float top = (hud != null ? hud.topBarHeight : 0f) * ui;
        float bottom = Screen.height - (hud != null ? hud.consoleHeight : 0f) * ui;

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

            Vector3 w = p.transform.position; w.y = 0f; w.z -= frontOffset;
            Vector3 sp = cam.WorldToScreenPoint(w);
            if (sp.z < 0f) continue;

            Vector2 ts = titleStyle.CalcSize(new GUIContent(title));
            Vector2 ls = lineStyle.CalcSize(new GUIContent(line));
            const float padX = 10f, padY = 4f;
            float bw = Mathf.Max(ts.x, ls.x) + padX * 2f, bh = ts.y + ls.y + padY * 2f;
            Rect box = new Rect(sp.x - bw * 0.5f, Screen.height - sp.y - bh * 0.5f, bw, bh);
            if (box.yMin < top || box.yMax > bottom) continue;

            GUI.color = new Color(0.02f, 0.03f, 0.07f, 0.72f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(col.r, col.g, col.b, 0.8f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(box.x, box.yMax - 1f, box.width, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            titleStyle.normal.textColor = col;
            Shadowed(new Rect(box.x, box.y + padY, box.width, ts.y), title, titleStyle);
            Shadowed(new Rect(box.x, box.y + padY + ts.y, box.width, ls.y), line, lineStyle);
        }
    }

    /// <summary>카메라가 이 블록을 보고 있는가 — 화면 한가운데가 바닥에 닿는 점이 블록 안이면 (조합표와 같다)</summary>
    bool Looking()
    {
        if (blockT == null) { GameObject g = GameObject.Find(block); if (g != null) blockT = g.transform; }
        if (blockT == null) return false;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Mathf.Abs(ray.direction.y) < 0.001f) return false;
        float t = -ray.origin.y / ray.direction.y;
        if (t < 0f) return false;
        Vector3 hit = ray.origin + ray.direction * t;
        Vector3 c = blockT.position, s = blockT.lossyScale;
        return Mathf.Abs(hit.x - c.x) <= s.x * 0.5f && Mathf.Abs(hit.z - c.z) <= s.z * 0.5f;
    }

    void Styles()
    {
        if (titleStyle != null) return;
        if (hud == null) hud = FindFirstObjectByType<GenesisHud>();

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.font = hud != null && hud.fontTitle != null ? hud.fontTitle : (hud != null ? hud.fontBold : null);
        titleStyle.fontSize = 18;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.padding = new RectOffset(0, 0, 0, 0);
        titleStyle.wordWrap = false;

        lineStyle = new GUIStyle(titleStyle);
        lineStyle.font = hud != null ? hud.fontBold : null;
        lineStyle.fontSize = 14;
        lineStyle.normal.textColor = new Color(0.82f, 0.84f, 0.90f);

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

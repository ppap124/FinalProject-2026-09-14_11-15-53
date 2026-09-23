using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 워크래프트식 마우스 조작.
///   좌클릭 / 드래그   선택 (영혼 또는 유닛)
///   Shift + 좌클릭    선택에 추가
///   우클릭            선택한 것을 그 자리로 이동
///   ESC               선택 해제
///
/// 영혼과 유닛은 섞어서 선택되지 않는다 — 조작이 꼬이기 때문.
/// </summary>
public class UnitControl : MonoBehaviour
{
    public static UnitControl Instance { get; private set; }

    public float dragThreshold = 6f;

    readonly List<Unit> selected = new List<Unit>();
    readonly List<SoulAvatar> souls = new List<SoulAvatar>();

    Camera cam;
    Vector2 dragStart;
    bool dragging;

    public int SelectedCount => selected.Count;
    public int SelectedSoulCount => souls.Count;

    /// <summary>딱 하나만 고른 유닛. 조합창은 이때만 뜨다.</summary>
    public Unit SoleSelectedUnit
    {
        get
        {
            selected.RemoveAll(x => x == null);
            return (selected.Count == 1 && souls.Count == 0) ? selected[0] : null;
        }
    }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Mouse m = Mouse.current;
        Keyboard k = Keyboard.current;
        if (m == null) return;

        if (k != null && k.escapeKey.wasPressedThisFrame) ClearSelection();

        // **HUD 위를 누른 것은 땅을 누른 게 아니다.** 이게 없으면 조합 버튼을 누르는 순간
        // 그 뒤 땅이 같이 눌려서 선택이 풀리고, 버튼은 빈 선택에 대고 실행된다
        bool overUi = UnityEngine.EventSystems.EventSystem.current != null
                   && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (m.leftButton.wasPressedThisFrame && !overUi)
        {
            dragStart = m.position.ReadValue();
            dragging = true;
        }

        if (m.leftButton.wasReleasedThisFrame && dragging)
        {
            dragging = false;

            Vector2 end = m.position.ReadValue();
            bool add = k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);

            if ((end - dragStart).magnitude < dragThreshold) ClickSelect(end, add);
            else BoxSelect(dragStart, end, add);
        }

        if (m.rightButton.wasPressedThisFrame && !overUi) IssueMove(m.position.ReadValue());
    }

    // ── 선택 ──────────────────────────────────

    void ClickSelect(Vector2 screenPos, bool add)
    {
        // 건물을 먼저 본다 — 클릭하면 창이 열린다
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Collide);

        ResearchBuilding lab = null;
        WarehouseBuilding ware = null;
        float labD = float.MaxValue, wareD = float.MaxValue;

        foreach (RaycastHit h in hits)
        {
            ResearchBuilding l = h.collider.GetComponentInParent<ResearchBuilding>();
            if (l != null && h.distance < labD) { labD = h.distance; lab = l; }

            WarehouseBuilding w = h.collider.GetComponentInParent<WarehouseBuilding>();
            if (w != null && h.distance < wareD) { wareD = h.distance; ware = w; }
        }

        if (lab != null && labD <= wareD) { lab.Toggle(); return; }
        if (ware != null) { ware.Toggle(); return; }

        if (!add) ClearSelection();

        SoulAvatar soul = SoulUnderCursor(screenPos);
        if (soul != null) { SelectSoul(soul); return; }

        Unit hit = UnitUnderCursor(screenPos);
        if (hit != null) SelectUnit(hit);
    }

    ResearchBuilding LabUnderCursor(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Collide);

        ResearchBuilding best = null;
        float bestDist = float.MaxValue;

        foreach (RaycastHit h in hits)
        {
            ResearchBuilding p = h.collider.GetComponentInParent<ResearchBuilding>();
            if (p == null) continue;

            if (h.distance < bestDist) { bestDist = h.distance; best = p; }
        }

        return best;
    }

    void BoxSelect(Vector2 a, Vector2 b, bool add)
    {
        if (!add) ClearSelection();

        Rect r = Rect.MinMaxRect(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
            Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        // 영혼이 하나라도 잡히면 영혼만 고른다
        bool anySoul = false;

        foreach (SoulAvatar s in SoulAvatar.All)
        {
            if (s == null) continue;

            Vector3 sp = cam.WorldToScreenPoint(s.transform.position);
            if (sp.z < 0f) continue;

            if (r.Contains(new Vector2(sp.x, sp.y))) { SelectSoul(s); anySoul = true; }
        }

        if (anySoul || SoulShop.Instance == null) return;

        foreach (Unit u in SoulShop.Instance.Units)
        {
            if (u == null) continue;

            Vector3 sp = cam.WorldToScreenPoint(u.transform.position);
            if (sp.z < 0f) continue;

            if (r.Contains(new Vector2(sp.x, sp.y))) SelectUnit(u);
        }
    }

    SoulAvatar SoulUnderCursor(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Collide);

        SoulAvatar best = null;
        float bestDist = float.MaxValue;

        foreach (RaycastHit h in hits)
        {
            SoulAvatar s = h.collider.GetComponentInParent<SoulAvatar>();
            if (s == null) continue;

            if (h.distance < bestDist) { bestDist = h.distance; best = s; }
        }

        return best;
    }

    Unit UnitUnderCursor(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Collide);

        Unit best = null;
        float bestDist = float.MaxValue;

        foreach (RaycastHit h in hits)
        {
            Unit u = h.collider.GetComponentInParent<Unit>();
            if (u == null) continue;

            if (h.distance < bestDist) { bestDist = h.distance; best = u; }
        }

        return best;
    }

    void SelectSoul(SoulAvatar s)
    {
        if (s == null || souls.Contains(s)) return;

        souls.Add(s);
        s.SetSelected(true);
    }

    void SelectUnit(Unit u)
    {
        if (u == null || selected.Contains(u)) return;

        selected.Add(u);
        u.SetSelected(true);
    }

    public void ClearSelection()
    {
        foreach (Unit u in selected)
            if (u != null) u.SetSelected(false);
        selected.Clear();

        foreach (SoulAvatar s in souls)
            if (s != null) s.SetSelected(false);
        souls.Clear();
    }

    // ── 이동 ──────────────────────────────────

    void IssueMove(Vector2 screenPos)
    {
        if (!GroundPoint(screenPos, out Vector3 dest)) return;

        souls.RemoveAll(s => s == null);
        selected.RemoveAll(u => u == null);

        // 영혼은 한 점으로 겹쳐서 보낸다 — 패드에 다 들어가야 하므로
        if (souls.Count > 0)
        {
            foreach (SoulAvatar s in souls) s.MoveTo(dest);
            return;
        }

        // 전투 유닛은 겹치면 사거리가 낭비되므로 흔다
        for (int i = 0; i < selected.Count; i++)
            selected[i].MoveTo(dest + Offset(i, selected.Count, 2.6f));
    }

    Vector3 Offset(int i, int n, float gap)
    {
        int side = Mathf.CeilToInt(Mathf.Sqrt(n));
        int row = i / side;
        int col = i % side;

        return new Vector3(
            (col - (side - 1) * 0.5f) * gap, 0f,
            (row - (side - 1) * 0.5f) * gap);
    }

    bool GroundPoint(Vector2 screenPos, out Vector3 point)
    {
        point = Vector3.zero;

        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);

        if (!ground.Raycast(ray, out float dist)) return false;

        point = ray.GetPoint(dist);
        return true;
    }

    // ── 드래그 박스 ───────────────────────────

    void OnGUI()
    {
        if (!dragging || Mouse.current == null) return;

        Vector2 cur = Mouse.current.position.ReadValue();
        if ((cur - dragStart).magnitude < dragThreshold) return;

        float y1 = Screen.height - dragStart.y;
        float y2 = Screen.height - cur.y;

        Rect r = Rect.MinMaxRect(
            Mathf.Min(dragStart.x, cur.x), Mathf.Min(y1, y2),
            Mathf.Max(dragStart.x, cur.x), Mathf.Max(y1, y2));

        GUI.color = new Color(0.3f, 1f, 0.4f, 0.12f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 1f, 0.4f, 0.8f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}

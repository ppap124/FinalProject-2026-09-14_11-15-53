using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛을 클릭하면 **그 유닛 옆에** 만들 수 있는 상위 단계가 뜬다.
/// 화면 구석의 큰 창이 아니라 유닛에 붙어 있는 작은 목록 — 시선이 안 끊긴다.
/// </summary>
public class CombinePanel : MonoBehaviour
{
    public static CombinePanel Instance { get; private set; }

    public float width = 230f;
    public float rowHeight = 24f;

    Camera cam;

    void Awake()
    {
        Instance = this;
    }

    void OnGUI()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || UnitControl.Instance == null || UnitCombiner.Instance == null) return;

        Unit u = UnitControl.Instance.SoleSelectedUnit;
        if (u == null) return;

        List<UnitCombiner.Option> opts = UnitCombiner.Instance.OptionsFor(u);
        if (opts.Count == 0) return;

        Vector3 sp = cam.WorldToScreenPoint(u.transform.position + Vector3.up * 1.2f);
        if (sp.z < 0f) return;

        float h = (opts.Count + 1) * rowHeight + 8f;   // +1 = 창고 버튼

        // 유닛 오른쪽에 붙인다. 화면 밖으로 나가면 왼쪽으로
        float x = sp.x + 26f;
        if (x + width > Screen.width - 8f) x = sp.x - width - 26f;

        float y = Mathf.Clamp(Screen.height - sp.y - h * 0.5f, 8f, Screen.height - h - 8f);

        Rect area = new Rect(x, y, width, h);

        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.skin.label.fontSize = 12;
        GUI.skin.button.fontSize = 12;

        for (int i = 0; i < opts.Count; i++)
        {
            UnitCombiner.Option o = opts[i];
            UnitTable.Stats r = UnitTable.Get(o.result);

            Rect row = new Rect(area.x + 4f, area.y + 4f + i * rowHeight, width - 8f, rowHeight - 2f);

            if (o.ready)
            {
                string mat = o.tier == 2 ? $" +{MaterialTable.Name(o.material)}" : "";

                if (GUI.Button(row, $"{r.name}{mat}"))
                {
                    UnitCombiner.Instance.Execute(u, o);
                    UnitControl.Instance.ClearSelection();
                    return;
                }
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(row, $"{r.name}  —  {o.need}");
                GUI.color = Color.white;
            }
        }

        // 창고로 보내기 — 지금 안 쓸 재료를 치워둔다
        Rect last = new Rect(area.x + 4f, area.y + 4f + opts.Count * rowHeight, width - 8f, rowHeight - 2f);

        if (GUI.Button(last, "↓ 창고에 넣기"))
        {
            if (Warehouse.Instance != null) Warehouse.Instance.Store(u);
            UnitControl.Instance.ClearSelection();
        }
    }
}

using UnityEngine;

/// <summary>
/// 우측 상단 자원 표시 — 영혼 · 돈 · 재료.
/// 워크3 유즈맵의 골드/나무 표시 자리. 항상 보여야 하는 것만 여기 둔다.
/// </summary>
public class ResourceHud : MonoBehaviour
{
    public static ResourceHud Instance { get; private set; }

    [Header("배치")]
    public float width = 300f;
    public float rowHeight = 24f;
    public float margin = 12f;

    void Awake()
    {
        Instance = this;
    }

    void OnGUI()
    {
        int rows = 2 + MaterialTable.All.Length;      // 영혼 · 돈 · 재료 3종
        float h = rows * rowHeight + 14f;

        Rect area = new Rect(Screen.width - width - margin, margin, width, h);

        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.skin.label.fontSize = 15;

        float y = area.y + 7f;
        float x = area.x + 12f;
        float w = width - 24f;

        // 영혼
        int souls = SoulBank.Instance != null ? SoulBank.Instance.Souls : 0;
        Row(ref y, x, w, new Color(0.55f, 0.90f, 1f), "영혼", souls.ToString());

        // 돈
        int gold = GoldBank.Instance != null ? GoldBank.Instance.Gold : 0;
        Row(ref y, x, w, new Color(0.95f, 0.80f, 0.30f), "돈", gold.ToString());

        // 재료
        foreach (Culture c in MaterialTable.All)
        {
            int n = MaterialBank.Instance != null ? MaterialBank.Instance.Get(c) : 0;
            Row(ref y, x, w, MaterialTable.Color(c), MaterialTable.Name(c), n.ToString());
        }
    }

    void Row(ref float y, float x, float w, Color dot, string name, string value)
    {
        // 색 점
        GUI.color = dot;
        GUI.DrawTexture(new Rect(x, y + 6f, 10f, 10f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x + 18f, y, w - 80f, rowHeight), name);

        GUIStyle right = new GUIStyle(GUI.skin.label);
        right.alignment = TextAnchor.UpperRight;
        right.fontSize = GUI.skin.label.fontSize;

        GUI.Label(new Rect(x, y, w, rowHeight), value, right);

        y += rowHeight;
    }
}

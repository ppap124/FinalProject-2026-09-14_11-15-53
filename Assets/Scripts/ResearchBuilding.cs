using UnityEngine;

/// <summary>
/// 연구소 건물 하나. 클릭하면 연구 창이 열린다.
/// 항목마다 패드를 두는 대신 건물 하나로 합쳤다 — 항목이 늘어나도 맵이 안 지저분해진다.
/// </summary>
public class ResearchBuilding : MonoBehaviour
{
    public static ResearchBuilding Instance { get; private set; }

    [Header("창")]
    public bool open;
    public float width = 420f;

    [Header("색")]
    public Color idleColor = new Color(0.30f, 0.45f, 0.42f);
    public Color openColor = new Color(0.45f, 0.70f, 0.62f);

    static readonly Research[] Order =
    {
        Research.Power, Research.Speed,
        Research.Tier1, Research.Tier2, Research.Tier3, Research.Tier4,
        Research.SoulIncome, Research.SynergyEase
    };

    static readonly string[] Names =
    {
        "공격력  +10%", "공격속도  +8%",
        "1단계 강화  +20%", "2단계 강화  +20%", "3단계 강화  +20%", "4단계 강화  +20%",
        "영혼 수급  +1", "시너지 문턱  -1"
    };

    Renderer rend;

    void Awake()
    {
        Instance = this;
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (rend != null) rend.material.color = open ? openColor : idleColor;
    }

    public void Toggle()
    {
        open = !open;
    }

    void OnGUI()
    {
        if (!open || ResearchLab.Instance == null) return;

        float h = 108f + Order.Length * 30f;
        Rect area = new Rect(16f, (Screen.height - h) * 0.5f, width, h);

        GUI.color = new Color(0f, 0f, 0f, 0.86f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 12f, width - 28f, h - 24f));

        GUI.skin.label.fontSize = 17;
        int gold = GoldBank.Instance != null ? GoldBank.Instance.Gold : 0;
        GUILayout.Label($"연 구 소        돈 {gold}");

        GUI.skin.label.fontSize = 13;
        GUILayout.Space(6);

        for (int i = 0; i < Order.Length; i++)
        {
            Research r = Order[i];

            int lv = ResearchLab.Instance.Level(r);
            int cost = ResearchLab.Instance.CostOf(r);
            bool max = cost == int.MaxValue;
            bool can = !max && GoldBank.Instance != null && GoldBank.Instance.CanAfford(cost);

            string title = max
                ? $"{Names[i]}     Lv{lv}   최대"
                : $"{Names[i]}     Lv{lv}   {cost}원";

            GUI.enabled = can;
            if (GUILayout.Button(title, GUILayout.Height(26f)))
                ResearchLab.Instance.Buy(r);
            GUI.enabled = true;
        }

        GUILayout.Space(6);
        if (GUILayout.Button("닫기", GUILayout.Height(24f))) open = false;

        GUILayout.EndArea();
    }
}

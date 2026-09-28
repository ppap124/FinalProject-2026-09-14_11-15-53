using UnityEngine;

/// <summary>
/// 연구소 건물 하나. **클릭하면 유닛처럼 선택되고**, 연구 여덟 가지가 HUD 명령 칸
/// (QWER / ASDF)에 뜬다 (`GenesisHud.ShowLab`). 예전에는 따로 뜨는 OnGUI 창이었는데,
/// 콘솔과 다른 창이 하나 더 떠서 HUD 가 두 벌로 보였다.
/// 항목마다 패드를 두는 대신 건물 하나로 합쳤다 — 항목이 늘어나도 맵이 안 지저분해진다.
/// </summary>
public class ResearchBuilding : MonoBehaviour
{
    public static ResearchBuilding Instance { get; private set; }

    [Tooltip("선택되어 있는가. UnitControl 이 켜고 끈다")]
    public bool open;

    [Header("색")]
    public Color idleColor = new Color(0.30f, 0.45f, 0.42f);
    public Color openColor = new Color(0.45f, 0.70f, 0.62f);

    Renderer rend;

    void Awake()
    {
        Instance = this;
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (rend != null && rend.enabled) rend.material.color = open ? openColor : idleColor;
    }
}

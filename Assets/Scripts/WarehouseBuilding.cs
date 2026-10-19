using UnityEngine;

/// <summary>
/// 창고 건물. **클릭하면 유닛처럼 선택되고**, 보관 중인 유닛이 HUD 명령 칸에
/// 초상화로 뜬다 (`GenesisHud.ShowWarehouse`). 칸을 누르면 그 종류를 바로 조합하거나 꺼낸다.
/// 넣는 건 유닛을 골랐을 때의 "창고" 칸으로 한다.
/// </summary>
public class WarehouseBuilding : MonoBehaviour
{
    public static WarehouseBuilding Instance { get; private set; }

    [Tooltip("선택되어 있는가. UnitControl 이 켜고 끈다")]
    public bool open;

    [Header("색")]
    public Color idleColor = new Color(0.42f, 0.36f, 0.28f);
    public Color openColor = new Color(0.65f, 0.55f, 0.40f);

    [Tooltip("꺼낸 유닛이 나올 자리 (전투 블록 안)")]
    public Vector3 dropPoint = new Vector3(0f, 0f, -6f);

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

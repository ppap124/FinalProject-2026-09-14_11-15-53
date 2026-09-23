using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 창고 건물. 클릭하면 보관 목록이 열리고, 눌러서 꺼낸다.
/// 넣는 건 유닛 조합창의 "창고" 버튼으로 한다.
/// </summary>
public class WarehouseBuilding : MonoBehaviour
{
    public static WarehouseBuilding Instance { get; private set; }

    public bool open;
    public float width = 300f;

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
        if (rend != null) rend.material.color = open ? openColor : idleColor;
    }

    public void Toggle()
    {
        open = !open;
    }

    void OnGUI()
    {
        if (!open || Warehouse.Instance == null) return;

        List<UnitType> kinds = Warehouse.Instance.Kinds();

        float h = 86f + Mathf.Max(1, kinds.Count) * 28f;
        Rect area = new Rect(16f, (Screen.height - h) * 0.5f, width, h);

        GUI.color = new Color(0f, 0f, 0f, 0.86f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 12f, width - 28f, h - 24f));

        GUI.skin.label.fontSize = 17;
        GUILayout.Label($"창 고        {Warehouse.Instance.TotalStored}개");

        GUI.skin.label.fontSize = 13;
        GUILayout.Space(4);

        if (kinds.Count == 0)
        {
            GUILayout.Label("비어 있다");
        }
        else
        {
            foreach (UnitType t in kinds)
            {
                UnitTable.Stats s = UnitTable.Get(t);
                int n = Warehouse.Instance.Get(t);

                if (GUILayout.Button($"{s.name}  ×{n}    꺼내기", GUILayout.Height(24f)))
                {
                    Warehouse.Instance.TakeOut(t, dropPoint);
                    break;
                }
            }
        }

        GUILayout.Space(6);
        if (GUILayout.Button("닫기", GUILayout.Height(22f))) open = false;

        GUILayout.EndArea();
    }
}

using UnityEngine;

/// <summary>
/// 배치 테스트용 자동 소비.
/// 영혼이 실제 유닛이 된 뒤로는 패드로 밀어 넣는 대신 **직접 먹고 효과만 준다.**
/// 물리적인 이동은 밸런스와 무관하므로 측정에는 영향이 없다.
///
/// 사람이 플레이할 땐 꺼둔다.
/// </summary>
public class AutoSpender : MonoBehaviour
{
    public static AutoSpender Instance { get; private set; }

    public bool auto = false;

    [Header("영혼 배분 — 나머지는 전부 유닛")]
    [Range(0f, 1f)] public float materialRatio = 0.3f;
    [Range(0f, 1f)] public float goldRatio = 0.3f;

    public int SpentOnUnits { get; private set; }
    public int SpentOnMaterials { get; private set; }
    public int SpentOnGold { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (!auto) return;
        if (SoulBank.Instance == null) return;

        int earned = SoulBank.Instance.TotalEarned;

        float matBudget = earned * materialRatio;
        float goldBudget = earned * goldRatio;
        float unitBudget = earned - matBudget - goldBudget;

        int guard = 0;

        while (guard++ < 40 && SoulBank.Instance.Souls > 0)
        {
            if (SpentOnMaterials < matBudget && MaterialShop.Instance != null)
            {
                if (!Eat()) return;
                MaterialShop.Instance.Grant();
                SpentOnMaterials++;
                continue;
            }

            if (SpentOnGold < goldBudget && GoldBank.Instance != null)
            {
                if (!Eat()) return;
                GoldBank.Instance.Grant();
                SpentOnGold++;
                continue;
            }

            if (SpentOnUnits < unitBudget && SoulShop.Instance != null)
            {
                if (!Eat()) return;
                SoulShop.Instance.SpawnPulledUnit();
                SpentOnUnits++;
                continue;
            }

            return;
        }
    }

    /// <summary>영혼 하나를 먹는다.</summary>
    bool Eat()
    {
        if (SoulAvatar.All.Count == 0) return false;

        SoulAvatar.All[SoulAvatar.All.Count - 1].Consume();
        return true;
    }

    public void ResetRun()
    {
        SpentOnUnits = 0;
        SpentOnMaterials = 0;
        SpentOnGold = 0;
    }
}

using UnityEngine;

public enum Research
{
    Power,        // 전 유닛 공격력
    Speed,        // 전 유닛 공격속도
    Tier1,        // 1단계 강화 — 싸다. 초반 전용 (조합 재료로 사라지므로)
    Tier2,
    Tier3,
    Tier4,        // 비싸다. 4단계를 갖기 전엔 0
    SoulIncome,   // 라운드당 영혼 +1
    SynergyEase   // 시너지 필요 개수 -1
}

/// <summary>
/// 연구소. 돈으로 사는 강화. **판마다 리셋**된다.
///
/// 유닛 수는 영혼이 선형이라 선형으로만 늘고, 단계 압축은 4단계에서 끝난다.
/// 필요 DPS는 지수로 오르므로 **지수 성장을 담당하는 건 여기뿐이다.**
/// </summary>
public class ResearchLab : MonoBehaviour
{
    public static ResearchLab Instance { get; private set; }

    [Header("비용 — base x growth^level")]
    public int baseCost = 30;
    public float costGrowth = 1.5f;

    [Header("효과 (레벨당)")]
    public float powerPerLevel = 0.10f;    // 공격력 +10%
    public float speedPerLevel = 0.08f;    // 공격속도 +8%
    public float tierPerLevel = 0.20f;     // 해당 단계 공격력 +20%
    public int soulPerLevel = 1;           // 영혼 +1/라운드

    [Header("상한")]
    public int synergyEaseMax = 2;

    [Header("테스트")]
    [Tooltip("켜두면 살 수 있는 것 중 제일 싼 것을 자동으로 산다")]
    public bool autoBuy = true;

    readonly int[] levels = new int[8];

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (autoBuy) AutoBuy();
    }

    // ── 구매 ──────────────────────────────────

    public int Level(Research r) => levels[(int)r];

    public int CostOf(Research r)
    {
        int lv = levels[(int)r];

        // 시너지 완화는 판을 크게 바꾸므로 훨씬 비싸다
        if (r == Research.SynergyEase)
        {
            if (lv >= synergyEaseMax) return int.MaxValue;
            return Mathf.RoundToInt(baseCost * 5f * Mathf.Pow(3f, lv));
        }

        return Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, lv));
    }

    public bool CanBuy(Research r)
    {
        int cost = CostOf(r);
        return cost != int.MaxValue
            && GoldBank.Instance != null
            && GoldBank.Instance.CanAfford(cost);
    }

    public bool Buy(Research r)
    {
        if (!CanBuy(r)) return false;
        if (!GoldBank.Instance.TrySpend(CostOf(r))) return false;

        levels[(int)r]++;
        return true;
    }

    /// <summary>테스트용 — 살 수 있는 것 중 제일 싼 것을 계속 산다.</summary>
    void AutoBuy()
    {
        int guard = 0;

        while (guard++ < 10)
        {
            Research best = Research.Power;
            int bestCost = int.MaxValue;

            for (int i = 0; i < levels.Length; i++)
            {
                Research r = (Research)i;

                // 아직 쓸모없는 단계 강화는 건너뛴다
                if (!IsUseful(r)) continue;

                int c = CostOf(r);
                if (c < bestCost) { bestCost = c; best = r; }
            }

            if (bestCost == int.MaxValue || !Buy(best)) return;
        }
    }

    /// <summary>4단계 강화는 4단계 유닛이 있어야 의미가 있다.</summary>
    bool IsUseful(Research r)
    {
        int tier = r == Research.Tier1 ? 1
                 : r == Research.Tier2 ? 2
                 : r == Research.Tier3 ? 3
                 : r == Research.Tier4 ? 4 : 0;

        if (tier == 0) return true;
        if (SoulShop.Instance == null) return false;

        foreach (Unit u in SoulShop.Instance.Units)
            if (u != null && u.Tier == tier) return true;

        return false;
    }

    // ── 효과 조회 ─────────────────────────────

    /// <summary>유닛 하나에 붙는 공격력 배수.</summary>
    public float DamageMult(int tier)
    {
        float m = 1f + powerPerLevel * Level(Research.Power);

        Research tr = tier == 1 ? Research.Tier1
                    : tier == 2 ? Research.Tier2
                    : tier == 3 ? Research.Tier3 : Research.Tier4;

        m += tierPerLevel * Level(tr);
        return m;
    }

    public float AttackRateMult()
    {
        return 1f + speedPerLevel * Level(Research.Speed);
    }

    public int BonusSoulsPerRound => soulPerLevel * Level(Research.SoulIncome);

    public int SynergyThresholdReduction => Level(Research.SynergyEase);

    public void ResetRun()
    {
        for (int i = 0; i < levels.Length; i++) levels[i] = 0;
    }

    public string Summary()
    {
        return $"공{Level(Research.Power)} 속{Level(Research.Speed)} "
             + $"T{Level(Research.Tier1)}/{Level(Research.Tier2)}/{Level(Research.Tier3)}/{Level(Research.Tier4)} "
             + $"영{Level(Research.SoulIncome)} 시{Level(Research.SynergyEase)}";
    }
}

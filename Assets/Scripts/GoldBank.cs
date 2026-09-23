using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 돈. 영혼을 걸어서 확률적으로 얻고, 연구소에 쓴다.
/// 영혼이 확정 자원이라면 돈은 도박 자원이다 — 그래서 수급이 튄다.
/// </summary>
public class GoldBank : MonoBehaviour
{
    public static GoldBank Instance { get; private set; }

    [Header("환전 — 영혼을 걸고 돈을 딴다")]
    public int exchangeSoulCost = 3;
    public int minGold = 5;
    public int maxGold = 20;

    public int Gold { get; private set; }
    public int TotalEarned { get; private set; }
    public int TotalExchanges { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // R — 영혼을 돈으로 (언제든 가능)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            Exchange();
    }

    public bool CanExchange()
    {
        return SoulBank.Instance != null && SoulBank.Instance.CanAfford(exchangeSoulCost);
    }

    /// <summary>영혼을 걸고 돈을 딴다. 얼마가 나올지는 모른다.</summary>
    public bool Exchange()
    {
        if (!CanExchange()) return false;
        if (!SoulBank.Instance.TrySpend(exchangeSoulCost)) return false;

        Add(Random.Range(minGold, maxGold + 1));
        TotalExchanges++;
        return true;
    }

    /// <summary>영혼을 이미 패드가 먹었을 때 — 비용 없이 돈만 준다.</summary>
    public bool Grant()
    {
        Add(Random.Range(minGold, maxGold + 1));
        TotalExchanges++;
        return true;
    }


    public void Add(int amount)
    {
        if (amount <= 0) return;

        Gold += amount;
        TotalEarned += amount;
    }

    public bool CanAfford(int cost) => Gold >= cost;

    public bool TrySpend(int cost)
    {
        if (cost < 0 || Gold < cost) return false;

        Gold -= cost;
        return true;
    }

    public void ResetRun()
    {
        Gold = 0;
        TotalEarned = 0;
        TotalExchanges = 0;
    }
}

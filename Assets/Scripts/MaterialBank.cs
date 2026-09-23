using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 문화권 재료 보유량만 관리한다. SoulBank과 같은 역할, 다른 자원.
/// 재료는 2단계 진화에만 쓰인다 (3~4단계는 재료가 필요 없다).
/// </summary>
public class MaterialBank : MonoBehaviour
{
    public static MaterialBank Instance { get; private set; }

    readonly Dictionary<Culture, int> stock = new Dictionary<Culture, int>();

    void Awake()
    {
        Instance = this;

        foreach (Culture c in MaterialTable.All)
            stock[c] = 0;
    }

    public int Get(Culture c)
    {
        return stock.TryGetValue(c, out int n) ? n : 0;
    }

    public void Add(Culture c, int amount = 1)
    {
        if (c == Culture.None || amount <= 0) return;

        stock[c] = Get(c) + amount;
    }

    public bool TrySpend(Culture c, int amount = 1)
    {
        if (Get(c) < amount) return false;

        stock[c] = Get(c) - amount;
        return true;
    }

    /// <summary>가진 재료 중 아무거나 하나. 없으면 None.</summary>
    public Culture AnyAvailable()
    {
        foreach (Culture c in MaterialTable.All)
            if (Get(c) > 0) return c;

        return Culture.None;
    }

    public string Summary()
    {
        return $"{MaterialTable.Name(Culture.Greek)} {Get(Culture.Greek)}  "
             + $"{MaterialTable.Name(Culture.Norse)} {Get(Culture.Norse)}  "
             + $"{MaterialTable.Name(Culture.Korean)} {Get(Culture.Korean)}";
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 창고. 필드에서 뺀 유닛을 쌓아둔다.
///
/// 창고에 있는 유닛은:
///   · 싸우지 않는다 (DPS 0)
///   · 시너지 카운트에 안 들어간다
///   · **조합 재료로는 그대로 쓰인다**
///   · 배치 면적을 안 먹는다
///
/// 그래서 "지금 안 쓸 재료를 치워두는" 용도가 된다.
/// </summary>
public class Warehouse : MonoBehaviour
{
    public static Warehouse Instance { get; private set; }

    readonly Dictionary<UnitType, int> stock = new Dictionary<UnitType, int>();

    public int TotalStored { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public int Get(UnitType t)
    {
        return stock.TryGetValue(t, out int n) ? n : 0;
    }

    /// <summary>필드 유닛을 창고로 넣는다. 유닛은 사라진다.</summary>
    public bool Store(Unit u)
    {
        if (u == null || SoulShop.Instance == null) return false;

        Add(u.type, 1);
        SoulShop.Instance.Consume(u);
        return true;
    }

    public void Add(UnitType t, int n = 1)
    {
        if (n <= 0) return;

        stock[t] = Get(t) + n;
        TotalStored += n;
    }

    /// <summary>창고에서 하나 꺼낸다. 성공하면 필드에 나온다.</summary>
    public bool TakeOut(UnitType t, Vector3? at = null)
    {
        if (!Remove(t, 1)) return false;
        if (SoulShop.Instance == null) return false;

        return SoulShop.Instance.SpawnUnit(t, at) != null;
    }

    /// <summary>재료로 소모한다. 필드에 안 꺼내고 바로 쓴다.</summary>
    public bool Remove(UnitType t, int n = 1)
    {
        if (Get(t) < n) return false;

        stock[t] = Get(t) - n;
        TotalStored -= n;
        return true;
    }

    /// <summary>보관 중인 종류 목록 (수량 1 이상).</summary>
    public List<UnitType> Kinds()
    {
        List<UnitType> list = new List<UnitType>();

        foreach (KeyValuePair<UnitType, int> kv in stock)
            if (kv.Value > 0) list.Add(kv.Key);

        list.Sort((a, b) => ((int)a).CompareTo((int)b));
        return list;
    }

    public void ResetRun()
    {
        stock.Clear();
        TotalStored = 0;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 조합.
///   1단계 x2 + 문화권 재료  →  2단계   (재료가 문화권을 정한다)
///   같은 문화권 2단계 3종    →  3단계   (재료 없음)
///   같은 문화권 3단계 x2     →  4단계   (재료 없음)
///
/// 결과 유닛은 첫 재료 유닛이 서 있던 자리에 나온다.
/// 조합하면 유닛 수가 줄어든다 — 그게 시너지 카운트로 치르는 대가다.
/// </summary>
public class UnitCombiner : MonoBehaviour
{
    public static UnitCombiner Instance { get; private set; }

    [Tooltip("켜두면 가능한 조합을 전부 자동 실행한다. 사람이 플레이할 땐 끈다")]
    public bool autoCombine = false;

    public int TotalCombined { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // E — 가능한 조합 하나 실행
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            CombineOnce();

        if (autoCombine)
        {
            int guard = 0;
            while (CombineOnce() && guard++ < 20) { }
        }
    }

    /// <summary>조합 하나를 실행한다. 위에서부터 시도 — 큰 것을 먼저 합친다.</summary>
    public bool CombineOnce()
    {
        if (SoulShop.Instance == null) return false;

        if (TryTier4()) return true;
        if (TryTier3()) return true;
        if (TryTier2()) return true;

        return false;
    }

    bool TryTier4()
    {
        foreach (Culture c in MaterialTable.All)
        {
            List<Unit> found = Find(UnitTable.Tier3Of(c), 2);
            if (found.Count < 2) continue;

            Vector3 at = found[0].transform.position;

            SoulShop.Instance.Consume(found[0]);
            SoulShop.Instance.Consume(found[1]);
            SoulShop.Instance.SpawnUnit(UnitTable.Tier4Of(c), at);

            TotalCombined++;
            return true;
        }

        return false;
    }

    bool TryTier3()
    {
        foreach (Culture c in MaterialTable.All)
        {
            UnitType[] trio = UnitTable.Tier2Of(c);

            Unit a = FindOne(trio[0]);
            Unit b = FindOne(trio[1]);
            Unit d = FindOne(trio[2]);
            if (a == null || b == null || d == null) continue;

            Vector3 at = a.transform.position;

            SoulShop.Instance.Consume(a);
            SoulShop.Instance.Consume(b);
            SoulShop.Instance.Consume(d);
            SoulShop.Instance.SpawnUnit(UnitTable.Tier3Of(c), at);

            TotalCombined++;
            return true;
        }

        return false;
    }

    bool TryTier2()
    {
        if (MaterialBank.Instance == null) return false;

        Culture material = MaterialBank.Instance.AnyAvailable();
        if (material == Culture.None) return false;

        for (int t = 0; t < 3; t++)
        {
            List<Unit> found = Find((UnitType)t, 2);
            if (found.Count < 2) continue;

            return Combine(found[0], found[1], material);
        }

        return false;
    }

    public bool Combine(Unit a, Unit b, Culture material)
    {
        if (a == null || b == null || a == b) return false;
        if (a.type != b.type || a.Tier != 1) return false;

        if (!UnitTable.TryCombine(a.type, material, out UnitType result)) return false;
        if (!MaterialBank.Instance.TrySpend(material, 1)) return false;

        Vector3 at = a.transform.position;

        SoulShop.Instance.Consume(a);
        SoulShop.Instance.Consume(b);
        SoulShop.Instance.SpawnUnit(result, at);

        TotalCombined++;
        return true;
    }

    List<Unit> Find(UnitType type, int need)
    {
        List<Unit> result = new List<Unit>();

        foreach (Unit u in SoulShop.Instance.Units)
        {
            if (u == null || u.type != type) continue;

            result.Add(u);
            if (result.Count >= need) break;
        }

        return result;
    }

    Unit FindOne(UnitType type)
    {
        foreach (Unit u in SoulShop.Instance.Units)
            if (u != null && u.type == type) return u;

        return null;
    }

    public void ResetRun()
    {
        TotalCombined = 0;
    }


    // ── 클릭 조합용 ──────────────────────

    public struct Option
    {
        public UnitType result;
        public Culture material;   // 2단계만 쓴다
        public int tier;
        public bool ready;
        public string need;        // 모자란 것
    }

    /// <summary>이 유닛으로 만들 수 있는 상위 단계 목록.</summary>
    public List<Option> OptionsFor(Unit u)
    {
        List<Option> list = new List<Option>();
        if (u == null || SoulShop.Instance == null) return list;

        if (u.Tier == 1)
        {
            int same = CountOf(u.type);

            foreach (Culture c in MaterialTable.All)
            {
                if (!UnitTable.TryCombine(u.type, c, out UnitType res)) continue;

                int mat = MaterialBank.Instance != null ? MaterialBank.Instance.Get(c) : 0;

                Option o = new Option();
                o.result = res;
                o.material = c;
                o.tier = 2;
                o.ready = same >= 2 && mat >= 1;

                if (same < 2) o.need = $"{UnitTable.Get(u.type).name} 2개 필요 ({same}/2)";
                else if (mat < 1) o.need = $"{MaterialTable.Name(c)} 필요";
                else o.need = "";

                list.Add(o);
            }
        }
        else if (u.Tier == 2)
        {
            UnitType[] trio = UnitTable.Tier2Of(u.Culture);

            Option o = new Option();
            o.result = UnitTable.Tier3Of(u.Culture);
            o.tier = 3;

            string missing = "";
            bool ok = true;

            foreach (UnitType t in trio)
            {
                if (CountOf(t) >= 1) continue;
                ok = false;
                missing += UnitTable.Get(t).name + " ";
            }

            o.ready = ok;
            o.need = ok ? "" : missing.Trim() + " 필요";
            list.Add(o);
        }
        else if (u.Tier == 3)
        {
            int same = CountOf(u.type);

            Option o = new Option();
            o.result = UnitTable.Tier4Of(u.Culture);
            o.tier = 4;
            o.ready = same >= 2;
            o.need = same >= 2 ? "" : $"{UnitTable.Get(u.type).name} 2개 필요 ({same}/2)";
            list.Add(o);
        }

        return list;
    }

    /// <summary>고른 조합을 실행한다. 결과는 이 유닛 자리에 나온다.</summary>
    /// <summary>고른 조합을 실행한다. 결과는 이 유닛 자리에 나온다.</summary>
    public bool Execute(Unit u, Option o)
    {
        if (u == null || !o.ready) return false;

        Vector3 at = u.transform.position;

        if (o.tier == 2)
        {
            if (MaterialBank.Instance == null || !MaterialBank.Instance.TrySpend(o.material, 1)) return false;
            if (!SpendOne(u.type, u)) return false;

            SoulShop.Instance.Consume(u);
        }
        else if (o.tier == 3)
        {
            UnitType[] trio = UnitTable.Tier2Of(u.Culture);

            foreach (UnitType t in trio)
                if (!SpendOne(t, null)) return false;
        }
        else
        {
            if (!SpendOne(u.type, u)) return false;
            SoulShop.Instance.Consume(u);
        }

        SoulShop.Instance.SpawnUnit(o.result, at);
        TotalCombined++;
        return true;
    }

    /// <summary>필드 + 창고. 창고에 있어도 재료로는 쓰인다.</summary>
    int CountOf(UnitType t)
    {
        int n = 0;
        foreach (Unit x in SoulShop.Instance.Units)
            if (x != null && x.type == t) n++;

        if (Warehouse.Instance != null) n += Warehouse.Instance.Get(t);
        return n;
    }

    /// <summary>재료 하나를 쓴다. 필드에서 먼저 찾고 없으면 창고에서 가져온다.</summary>
    bool SpendOne(UnitType t, Unit except)
    {
        foreach (Unit x in SoulShop.Instance.Units)
        {
            if (x == null || x == except || x.type != t) continue;

            SoulShop.Instance.Consume(x);
            return true;
        }

        return Warehouse.Instance != null && Warehouse.Instance.Remove(t, 1);
    }

    Unit FindOther(UnitType t, Unit except)
    {
        foreach (Unit x in SoulShop.Instance.Units)
            if (x != null && x != except && x.type == t) return x;
        return null;
    }
}

using System.Collections.Generic;
using UnityEngine;

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

    public bool TryTier4()
    {
        foreach (Culture c in MaterialTable.All)
        {
            List<Unit> found = Find(UnitTable.Tier3Of(c), 2);
            if (found.Count < 2) continue;

            Vector3 at = found[0].transform.position;

            SoulShop.Instance.Consume(found[0]);
            SoulShop.Instance.Consume(found[1]);
            SoulShop.Instance.SpawnUnit(UnitTable.Tier4Of(c), at);

            Done(UnitTable.Tier4Of(c), at);
            return true;
        }

        return false;
    }

    public bool TryTier3()
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

            Done(UnitTable.Tier3Of(c), at);
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

        Done(result, at);
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
        if (u == null) return new List<Option>();
        return OptionsFor(u.type);
    }

    /// <summary>
    /// 이 종류로 만들 수 있는 상위 단계 목록. 필드에 한 마리도 없어도 된다 —
    /// 창고 콘솔에서 창고에 든 유닛을 바로 조합할 때 쓴다
    /// </summary>
    public List<Option> OptionsFor(UnitType type)
    {
        List<Option> list = new List<Option>();
        if (SoulShop.Instance == null) return list;

        UnitTable.Stats us = UnitTable.Get(type);
        if (us.hidden) return list;   // 히든은 막다른 유닛 — 더 올라갈 데가 없다
        int tier = us.tier;
        Culture culture = us.culture;

        if (tier == 1)
        {
            int same = CountOf(type);

            foreach (Culture c in MaterialTable.All)
            {
                if (!UnitTable.TryCombine(type, c, out UnitType res)) continue;

                int mat = MaterialBank.Instance != null ? MaterialBank.Instance.Get(c) : 0;

                Option o = new Option();
                o.result = res;
                o.material = c;
                o.tier = 2;
                o.ready = same >= 2 && mat >= 1;

                if (same < 2) o.need = $"{us.name} 2개 필요 ({same}/2)";
                else if (mat < 1) o.need = $"{MaterialTable.Name(c)} 필요";
                else o.need = "";

                list.Add(o);
            }
        }
        else if (tier == 2)
        {
            UnitType[] trio = UnitTable.Tier2Of(culture);

            Option o = new Option();
            o.result = UnitTable.Tier3Of(culture);
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
        else if (tier == 3)
        {
            int same = CountOf(type);

            Option o = new Option();
            o.result = UnitTable.Tier4Of(culture);
            o.tier = 4;
            o.ready = same >= 2;
            o.need = same >= 2 ? "" : $"{us.name} 2개 필요 ({same}/2)";
            list.Add(o);
        }

        return list;
    }

    /// <summary>고른 조합을 실행한다. 결과는 이 유닛 자리에 나온다.</summary>
    public bool Execute(Unit u, Option o)
    {
        if (u == null || !o.ready) return false;

        Vector3 at = u.transform.position;
        if (!SpendRest(u.type, o, u)) return false;

        SoulShop.Instance.Consume(u);
        SoulShop.Instance.SpawnUnit(o.result, at);
        Done(o.result, at);
        return true;
    }

    /// <summary>
    /// 히든 조합 — 서로 다른 1단계 둘 + 재료. 결과는 a 자리에 나오고, 처음이면 이름이 풀린다
    /// </summary>
    public bool CombineHidden(Unit a, Unit b, Culture material)
    {
        if (a == null || b == null || a == b) return false;
        UnitTable.Hidden h;
        if (!UnitTable.TryHidden(a.type, b.type, material, out h)) return false;
        if (MaterialBank.Instance == null || !MaterialBank.Instance.TrySpend(h.material, 1)) return false;

        Vector3 at = a.transform.position;
        SoulShop.Instance.Consume(a);
        SoulShop.Instance.Consume(b);
        SoulShop.Instance.SpawnUnit(h.result, at);

        bool first = !UnitTable.Discovered(h.result);
        UnitTable.Discover(h.result);
        if (HiddenFound != null) HiddenFound(h.result, first);

        Done(h.result, at);
        return true;
    }

    /// <summary>히든을 만들었다. 두 번째 값은 처음 찾았는지 — 알림 띠가 듣는다</summary>
    public static event System.Action<UnitType, bool> HiddenFound;

    /// <summary>
    /// 창고에 든 유닛을 기준으로 조합한다 — 꺼내지 않고 창고 콘솔에서 바로 한다.
    /// 결과는 `at` 자리(전투 블록)에 나온다
    /// </summary>
    public bool ExecuteStored(UnitType type, Option o, Vector3? at)
    {
        if (!o.ready || Warehouse.Instance == null || Warehouse.Instance.Get(type) < 1) return false;
        if (!SpendRest(type, o, null)) return false;
        if (!Warehouse.Instance.Remove(type, 1)) return false;

        // 필드가 가득이면 결과도 창고로 — 창고 것끼리 조합해서 인구수를 넘기는 길을 막는다
        if (SoulShop.Instance.AtCap)
        {
            Warehouse.Instance.Add(o.result, 1);
            Done(o.result, at ?? Vector3.zero);
            return true;
        }
        Unit made = SoulShop.Instance.SpawnUnit(o.result, at);
        Done(o.result, made != null ? made.transform.position : (at ?? Vector3.zero));
        return true;
    }

    /// <summary>
    /// 기준 유닛 한 마리를 뺀 나머지 재료를 쓴다. 기준 유닛은 부르는 쪽이 치운다.
    /// 모자라면 아무것도 쓰지 않고 false — 반쯤 먹고 실패하면 유닛이 증발한다
    /// </summary>
    bool SpendRest(UnitType type, Option o, Unit except)
    {
        // 기준이 창고에 있으면 창고 한 자리는 기준 몫이라 짝으로 쓰면 안 된다
        int keep = except == null ? 1 : 0;

        if (o.tier == 2)
        {
            if (MaterialBank.Instance == null || MaterialBank.Instance.Get(o.material) < 1) return false;
            if (CountOf(type) < 2) return false;

            MaterialBank.Instance.TrySpend(o.material, 1);
            return SpendOne(type, except, keep);
        }

        if (o.tier == 3)
        {
            UnitType[] trio = UnitTable.Tier2Of(UnitTable.Get(type).culture);
            foreach (UnitType t in trio)
                if (t != type && CountOf(t) < 1) return false;

            foreach (UnitType t in trio)
                if (t != type && !SpendOne(t, except, 0)) return false;
            return true;
        }

        if (CountOf(type) < 2) return false;
        return SpendOne(type, except, keep);
    }

    /// <summary>
    /// 조합이 끝났다. 조합표·필드 연출은 이 신호를 받아 번쩍인다 — 조합 경로가 넷(자동 3단 + 클릭)이라
    /// 연출을 각자 부르면 하나쯤 빠진다.
    /// </summary>
    void Done(UnitType result, Vector3 at)
    {
        TotalCombined++;
        if (Combined != null) Combined(result, at);
    }

    /// <summary>조합 결과와 나온 자리.</summary>
    public static event System.Action<UnitType, Vector3> Combined;

    /// <summary>
    /// 이 유닛을 **지금** 만들 수 있는가. 조합표가 칸에 불을 켤 때 쓴다.
    /// `OptionsFor` 와 같은 기준이다 — 창고에 든 유닛도 재료로 센다.
    /// </summary>
    public bool CanMake(UnitType result)
    {
        if (SoulShop.Instance == null) return false;
        UnitTable.Stats s = UnitTable.Get(result);
        Culture c = s.culture;

        if (s.tier == 2)
        {
            if (MaterialBank.Instance == null || MaterialBank.Instance.Get(c) < 1) return false;
            foreach (UnitType b in UnitTable.OfTier(1))
            {
                UnitType r;
                if (UnitTable.TryCombine(b, c, out r) && r == result) return CountOf(b) >= 2;
            }
            return false;
        }
        if (s.tier == 3)
        {
            foreach (UnitType t in UnitTable.Tier2Of(c))
                if (CountOf(t) < 1) return false;
            return true;
        }
        if (s.tier == 4) return CountOf(UnitTable.Tier3Of(c)) >= 2;
        return false;
    }

    /// <summary>필드 + 창고. 창고에 있어도 재료로는 쓰인다.</summary>
    public int CountOf(UnitType t)
    {
        int n = 0;
        foreach (Unit x in SoulShop.Instance.Units)
            if (x != null && x.type == t) n++;

        if (Warehouse.Instance != null) n += Warehouse.Instance.Get(t);
        return n;
    }

    /// <summary>
    /// 재료 하나를 쓴다. **창고에서 먼저** 가져오고 없으면 필드에서 뺀다 — 창고는
    /// "지금 안 쓸 재료를 치워두는" 곳이라, 싸우는 유닛을 두고 창고분을 남기면 거꾸로다.
    /// `keep` 은 창고에서 건드리면 안 되는 수(창고에 든 기준 유닛 몫)
    /// </summary>
    bool SpendOne(UnitType t, Unit except, int keep)
    {
        if (Warehouse.Instance != null && Warehouse.Instance.Get(t) > keep)
            return Warehouse.Instance.Remove(t, 1);

        foreach (Unit x in SoulShop.Instance.Units)
        {
            if (x == null || x == except || x.type != t) continue;

            SoulShop.Instance.Consume(x);
            return true;
        }

        return false;
    }

    Unit FindOther(UnitType t, Unit except)
    {
        foreach (Unit x in SoulShop.Instance.Units)
            if (x != null && x != except && x.type == t) return x;
        return null;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 밸런스 측정용 자동 플레이어. 사람이 하는 판단을 흉내 내 두 가지 수준으로 둔다.
///
///   잘하는 봇 (Good)  — 필요할 때 재료 · 돈을 뽑고, 3단계 셋을 채우는 쪽으로 재료를 고르고,
///                        히든 조합을 쓰고, 연구는 "비용 대비 얼마나 세지나"로 고르고,
///                        유닛은 모서리부터 길 가장자리에 세운다 (모서리는 두 변을 본다)
///   대충 하는 봇 (Casual) — 영혼을 정해진 비율로 쓰고, 되는 조합은 아무거나 하고,
///                        연구는 제일 싼 것, 유닛은 나온 자리에 둔다
///
/// 영혼은 패드로 밀어 넣는 대신 직접 먹고 효과만 준다 (AutoSpender 와 같다 — 이동은 밸런스와 무관).
/// 켜 있으면 AutoSpender · 자동 조합 · 연구 자동 구매를 끈다 (둘이 같이 돌면 섞인다).
/// 사람이 플레이할 땐 Off.
/// </summary>
public class BalanceBot : MonoBehaviour
{
    public static BalanceBot Instance { get; private set; }

    public enum Style { Off, Good, Casual }
    public Style style = Style.Off;

    [Header("잘하는 봇 — 영혼 배분 (나머지는 유닛)")]
    [Range(0f, 1f)] public float goodMaterialShare = 0.3f;
    [Range(0f, 1f)] public float goodGoldShare = 0.25f;

    [Header("대충 하는 봇 — 영혼 배분 (나머지는 유닛)")]
    [Range(0f, 1f)] public float casualMaterialShare = 0.3f;
    [Range(0f, 1f)] public float casualGoldShare = 0.1f;

    [Tooltip("생각하는 간격(게임 초)")]
    public float thinkEvery = 0.5f;

    int spentUnit, spentMat, spentGold;
    float nextThink;
    bool wasPrep;
    readonly HashSet<int> placed = new HashSet<int>();

    void Awake() { Instance = this; }

    void Update()
    {
        if (style == Style.Off) return;
        GameLoop gl = GameLoop.Instance;
        if (gl == null || gl.IsOver) return;

        // 다른 자동 장치는 끈다
        if (AutoSpender.Instance != null) AutoSpender.Instance.auto = false;
        if (UnitCombiner.Instance != null) UnitCombiner.Instance.autoCombine = false;
        if (ResearchLab.Instance != null) ResearchLab.Instance.autoBuy = false;

        if (Time.time < nextThink) return;
        nextThink = Time.time + thinkEvery;

        Spend();
        if (style == Style.Good) CombineGood(); else CombineCasual();
        if (style == Style.Good) ResearchGood(); else ResearchCasual();
        if (style == Style.Good) Place(gl.InPrep && !wasPrep);
        wasPrep = gl.InPrep;
    }

    // ── 영혼 쓰기 ─────────────────────────

    void Spend()
    {
        int guard = 0;
        while (guard++ < 60 && SoulAvatar.All.Count > 0)
        {
            int total = spentUnit + spentMat + spentGold + 1;
            int act;   // 0 유닛 · 1 재료 · 2 돈

            if (style == Style.Good)
            {
                // 짝은 있는데 재료가 모자라면 재료부터 — 짝을 세워 두는 동안 조합이 멈춘다
                bool needMat = PairsWaiting() > MaterialTotal();
                if (needMat && spentMat < total * 0.5f) act = 1;
                else if (spentGold < total * goodGoldShare) act = 2;
                else if (spentMat < total * goodMaterialShare) act = 1;
                else act = 0;
            }
            else
            {
                if (spentMat < total * casualMaterialShare) act = 1;
                else if (spentGold < total * casualGoldShare) act = 2;
                else act = 0;
            }

            // 인구수가 차면 유닛 대신 — 잘하는 봇은 짝이 있으면 재료(조합하면 자리가 난다) 아니면 돈,
            // 대충 봇은 그냥 재료
            if (act == 0 && SoulShop.Instance != null && SoulShop.Instance.AtCap)
                act = style == Style.Good ? (PairsWaiting() > MaterialTotal() ? 1 : 2) : 1;

            SoulAvatar.All[SoulAvatar.All.Count - 1].Consume();
            if (act == 1 && MaterialShop.Instance != null) { MaterialShop.Instance.Grant(); spentMat++; }
            else if (act == 2 && GoldBank.Instance != null) { GoldBank.Instance.Grant(); spentGold++; }
            else if (SoulShop.Instance != null) { SoulShop.Instance.SpawnPulledUnit(); spentUnit++; }
        }
    }

    int PairsWaiting()
    {
        int n = 0;
        foreach (UnitType t in UnitTable.OfTier(1)) n += Count(t) / 2;
        return n;
    }

    int MaterialTotal()
    {
        if (MaterialBank.Instance == null) return 0;
        int n = 0;
        foreach (Culture c in MaterialTable.All) n += MaterialBank.Instance.Get(c);
        return n;
    }

    static int Count(UnitType t) => UnitCombiner.Instance != null ? UnitCombiner.Instance.CountOf(t) : 0;

    // ── 조합 ─────────────────────────────

    void CombineCasual()
    {
        if (UnitCombiner.Instance == null) return;
        int guard = 0;
        while (guard++ < 20 && UnitCombiner.Instance.CombineOnce()) { }
    }

    void CombineGood()
    {
        UnitCombiner uc = UnitCombiner.Instance;
        if (uc == null || MaterialBank.Instance == null) return;

        for (int guard = 0; guard < 20; guard++)
        {
            if (uc.TryTier4()) continue;
            if (uc.TryTier3()) continue;
            if (BestTier2()) continue;
            if (TryHidden()) continue;
            break;
        }
    }

    /// <summary>
    /// 2단계 — 어느 짝에 어느 재료를 쓸지 고른다. 3단계 셋을 채우는 쪽이 먼저이고,
    /// 이미 가진 2단계는 뒤로(4단계용으로 둘째가 필요하긴 하다), 주력 문화권에 조금 더 준다
    /// </summary>
    bool BestTier2()
    {
        Culture main = MainCulture();
        float best = float.MinValue;
        UnitType bestType = UnitType.Warrior;
        Culture bestMat = Culture.None;

        foreach (UnitType t in UnitTable.OfTier(1))
        {
            if (Count(t) < 2) continue;
            foreach (Culture c in MaterialTable.All)
            {
                if (MaterialBank.Instance.Get(c) < 1) continue;
                UnitType res;
                if (!UnitTable.TryCombine(t, c, out res)) continue;

                float s = 0f;
                foreach (UnitType other in UnitTable.Tier2Of(c))
                    if (other != res && Count(other) > 0) s += 10f;   // 셋을 채워 간다
                s -= Count(res) * 6f;                                  // 이미 있으면 뒤로
                if (c == main) s += 3f;
                s += MaterialBank.Instance.Get(c) * 0.5f;

                if (s > best) { best = s; bestType = t; bestMat = c; }
            }
        }

        if (bestMat == Culture.None) return false;
        List<Unit> pair = FindUnits(bestType, 2);
        return pair.Count == 2 && UnitCombiner.Instance.Combine(pair[0], pair[1], bestMat);
    }

    /// <summary>히든 — 짝이 안 맞아 남은 1단계 둘(각각 홀수)을 재료 하나와 묶는다</summary>
    bool TryHidden()
    {
        foreach (UnitTable.Hidden h in UnitTable.Hiddens)
        {
            if (Count(h.a) % 2 == 0 || Count(h.b) % 2 == 0) continue;
            if (MaterialBank.Instance.Get(h.material) < 1) continue;
            List<Unit> a = FindUnits(h.a, 1), b = FindUnits(h.b, 1);
            if (a.Count == 1 && b.Count == 1 && UnitCombiner.Instance.CombineHidden(a[0], b[0], h.material)) return true;
        }
        return false;
    }

    Culture MainCulture()
    {
        Culture best = Culture.Greek; int n = -1;
        foreach (Culture c in MaterialTable.All)
        {
            int k = SynergyManager.Instance != null ? SynergyManager.Instance.Count(c) : 0;
            if (k > n) { n = k; best = c; }
        }
        return best;
    }

    static List<Unit> FindUnits(UnitType t, int need)
    {
        List<Unit> r = new List<Unit>();
        if (SoulShop.Instance == null) return r;
        foreach (Unit u in SoulShop.Instance.Units)
            if (u != null && u.type == t) { r.Add(u); if (r.Count >= need) break; }
        return r;
    }

    // ── 연구 ─────────────────────────────

    void ResearchCasual()
    {
        ResearchLab lab = ResearchLab.Instance;
        if (lab == null) return;
        for (int guard = 0; guard < 10; guard++)
        {
            Research pick = Research.Power; int cost = int.MaxValue;
            foreach (Research r in System.Enum.GetValues(typeof(Research)))
            {
                if (r == Research.SynergyEase) continue;
                if (TierOf(r) > 0 && TierShare(TierOf(r)) <= 0f) continue;
                int c = lab.CostOf(r);
                if (c < cost) { cost = c; pick = r; }
            }
            if (!lab.Buy(pick)) return;
        }
    }

    /// <summary>
    /// "금화 1 당 얼마나 세지나" 가 가장 큰 연구를 산다. 못 사면 **모은다** (더 싼 걸로 새지 않는다).
    /// 값은 전체 화력이 몇 % 오르는지로 잰다
    /// </summary>
    void ResearchGood()
    {
        ResearchLab lab = ResearchLab.Instance;
        GameLoop gl = GameLoop.Instance;
        if (lab == null || gl == null) return;

        for (int guard = 0; guard < 10; guard++)
        {
            Research pick = Research.Power; float bestRatio = 0f;
            foreach (Research r in System.Enum.GetValues(typeof(Research)))
            {
                int c = lab.CostOf(r);
                if (c == int.MaxValue) continue;
                float v = Value(r, lab, gl);
                if (v <= 0f) continue;
                float ratio = v / c;
                if (ratio > bestRatio) { bestRatio = ratio; pick = r; }
            }
            if (bestRatio <= 0f || !lab.Buy(pick)) return;
        }
    }

    float Value(Research r, ResearchLab lab, GameLoop gl)
    {
        float power = lab.Level(Research.Power) * lab.powerPerLevel;
        switch (r)
        {
            case Research.Power:
            {
                float v = 0f;
                for (int t = 1; t <= 4; t++) v += TierShare(t) * lab.powerPerLevel / (1f + power + lab.tierPerLevel * lab.Level(TierResearch(t)));
                return v;
            }
            case Research.Speed:
                return lab.speedPerLevel / (1f + lab.speedPerLevel * lab.Level(Research.Speed));
            case Research.SoulIncome:
            {
                // 남은 라운드 동안 들어올 영혼이 지금까지 번 영혼에 비해 얼마나 되나
                int left = Mathf.Max(0, 49 - gl.Round);
                float earned = SoulBank.Instance != null ? SoulBank.Instance.TotalEarned : 20f;
                return 0.5f * left / Mathf.Max(20f, earned + 3f * left);
            }
            case Research.SynergyEase:
            {
                // 문턱 하나 아래에 걸린 문화권이 있으면 값이 크다
                SynergyManager syn = SynergyManager.Instance;
                if (syn == null) return 0f;
                foreach (Culture c in MaterialTable.All)
                {
                    int n = syn.Count(c);
                    if (n == syn.SmallCut - 1 || n == syn.BigCut - 1 || n == syn.HugeCut - 1) return 0.25f;
                }
                return 0f;
            }
            default:
            {
                int t = TierOf(r);
                float share = TierShare(t);
                if (share <= 0f) return 0f;
                return share * lab.tierPerLevel / (1f + power + lab.tierPerLevel * lab.Level(r));
            }
        }
    }

    static int TierOf(Research r)
    {
        return r == Research.Tier1 ? 1 : r == Research.Tier2 ? 2 : r == Research.Tier3 ? 3 : r == Research.Tier4 ? 4 : 0;
    }

    static Research TierResearch(int t)
    {
        return t == 1 ? Research.Tier1 : t == 2 ? Research.Tier2 : t == 3 ? Research.Tier3 : Research.Tier4;
    }

    /// <summary>그 단계 유닛이 전체 화력에서 차지하는 몫 (0~1)</summary>
    static float TierShare(int tier)
    {
        if (SoulShop.Instance == null) return 0f;
        float all = 0f, mine = 0f;
        foreach (Unit u in SoulShop.Instance.Units)
        {
            if (u == null) continue;
            float d = u.EffectiveDps;
            all += d;
            if (u.Tier == tier) mine += d;
        }
        return all > 0f ? mine / all : 0f;
    }

    // ── 배치 ─────────────────────────────

    /// <summary>
    /// 센 유닛부터 모서리에 세운다. 근접은 길 가장자리(16.2), 원거리는 조금 안쪽(14.5).
    /// 정비 시간이 시작될 때 전부 다시 세우고, 그 사이엔 새로 나온 유닛만 세운다
    /// </summary>
    void Place(bool full)
    {
        if (SoulShop.Instance == null) return;
        List<Unit> units = new List<Unit>();
        foreach (Unit u in SoulShop.Instance.Units) if (u != null) units.Add(u);

        bool fresh = false;
        foreach (Unit u in units) if (!placed.Contains(u.GetInstanceID())) { fresh = true; break; }
        if (!full && !fresh) return;

        units.Sort((a, b) => b.EffectiveDps.CompareTo(a.EffectiveDps));
        List<Vector3> melee = Spots(16.2f), ranged = Spots(14.5f);
        int mi = 0, ri = 0;

        placed.Clear();
        foreach (Unit u in units)
        {
            bool isMelee = u.range <= 9f;
            List<Vector3> spots = isMelee ? melee : ranged;
            int i = isMelee ? mi++ : ri++;
            Vector3 p = spots[i % spots.Count];
            Vector3 now = u.transform.position;
            if ((new Vector2(now.x - p.x, now.z - p.z)).sqrMagnitude > 1f)
                u.transform.position = new Vector3(p.x, now.y, p.z);
            placed.Add(u.GetInstanceID());
        }
    }

    /// <summary>한 변 길이 2h 인 네모 둘레 자리 — 모서리에서 가까운 순</summary>
    static List<Vector3> Spots(float h)
    {
        List<Vector3> list = new List<Vector3>();
        const float step = 2.2f;
        int n = Mathf.FloorToInt(h / step);
        Vector2[] corners = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        for (int k = 0; k <= n; k++)
            foreach (Vector2 c in corners)
            {
                float a = h, b = h - k * step;
                list.Add(new Vector3(c.x * a, 0f, c.y * b));
                if (k > 0) list.Add(new Vector3(c.x * b, 0f, c.y * a));
            }
        return list;
    }

    public void ResetRun()
    {
        spentUnit = spentMat = spentGold = 0;
        placed.Clear();
        wasPrep = false;
    }
}

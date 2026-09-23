using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 문화권 시너지. **필드에 배치된 유닛만** 세고, 단계는 무관하다(2단계도 1개).
/// 그래서 조합해서 강해질수록 시너지 카운트가 떨어진다 — 그게 조합의 대가다.
///
///   3개  ☆   소(小)   — 수치 보너스
///   6개  ★   대(大)   — 질적 변화
///  10개  ★★  극(極)   — 질적 변화 한 번 더
///  10개 초과 — 1개마다 그 문화권 공격력 +2%. 상한 없음.
///
/// 계단을 세 번 밟게 해서 후반에도 목표가 남고, 그 위로는 완만하게 이어진다.
/// </summary>
public class SynergyManager : MonoBehaviour
{
    public static SynergyManager Instance { get; private set; }

    [Header("발동 기준")]
    public int smallThreshold = 3;
    public int bigThreshold = 6;
    public int hugeThreshold = 10;

    [Header("초과분 — 문턱을 넘긴 유닛 1개당")]
    public float overflowDamageBonus = 0.02f;   // 공격력 +2%

    [Header("그리스 — 힘")]
    public float greekDamageBonus = 0.20f;      // ☆ 공격력 +20%
    public float greekSplashRadius = 2.5f;      // ★ 모든 공격이 범위 피해
    public float greekSplashFraction = 0.5f;    //   주변은 절반 피해
    public float greekHugeRadiusMult = 2f;      // ★★ 반경 2배
    public float greekHugeFraction = 1f;        //   주변도 온전한 피해

    [Header("북유럽 — 광란")]
    public float norseStackRate = 0.05f;        // ☆ 처치마다 공격속도 +5%
    public int norseMaxStackSmall = 10;
    public int norseMaxStackBig = 20;           // ★ 상한 20 (라운드마다 초기화)
    public int norseMaxStackHuge = 40;          // ★★ 상한 40 + 라운드를 넘어 유지
    public float norseHugeStackRate = 0.08f;    //   중첩당 +8%

    [Header("한국 — 홀림")]
    public float koreanCritChance = 0.15f;      // ☆ 치명타 +15%
    public float koreanCritMult = 2.0f;
    public float koreanConfuseTime = 2.5f;      // ★ 치명타 시 역주행
    public float koreanHugeCritChance = 0.15f;  // ★★ 치명타 확률 +15% 추가
    public float koreanHugeCritMult = 3.0f;     //   치명타 피해 3배

    readonly Dictionary<Culture, int> counts = new Dictionary<Culture, int>();
    int norseStacks;

    void Awake()
    {
        Instance = this;
        foreach (Culture c in MaterialTable.All) counts[c] = 0;
    }

    // ── 카운트 ────────────────────────────────

    /// <summary>SoulShop이 유닛 목록을 바꿀 때마다 불러준다.</summary>
    public void Recount(IReadOnlyList<Unit> units)
    {
        foreach (Culture c in MaterialTable.All) counts[c] = 0;

        if (units == null) return;

        foreach (Unit u in units)
        {
            if (u == null || u.Culture == Culture.None) continue;
            counts[u.Culture] = counts[u.Culture] + 1;
        }
    }

    public int Count(Culture c)
    {
        return counts.TryGetValue(c, out int n) ? n : 0;
    }

    // 연구소가 문턱을 낮춰준다
    int Cut => ResearchLab.Instance != null ? ResearchLab.Instance.SynergyThresholdReduction : 0;

    public int SmallCut => Mathf.Max(1, smallThreshold - Cut);
    public int BigCut => Mathf.Max(2, bigThreshold - Cut);
    public int HugeCut => Mathf.Max(3, hugeThreshold - Cut);

    /// <summary>0 = 미발동, 1 = 소(☆), 2 = 대(★), 3 = 극(★★)</summary>
    public int Level(Culture c)
    {
        int n = Count(c);
        if (n >= HugeCut) return 3;
        if (n >= BigCut) return 2;
        if (n >= SmallCut) return 1;
        return 0;
    }

    /// <summary>최종 문턱을 넘긴 유닛 수. 여기부터는 상한 없이 완만하게 쌓인다.</summary>
    public int Overflow(Culture c)
    {
        return Mathf.Max(0, Count(c) - HugeCut);
    }

    // ── 그리스 ────────────────────────────────

    public float DamageMult(Culture c)
    {
        if (c == Culture.None) return 1f;

        float m = 1f;
        if (c == Culture.Greek && Level(Culture.Greek) >= 1) m += greekDamageBonus;
        m += Overflow(c) * overflowDamageBonus;   // 초과분은 모든 문화권 공통
        return m;
    }

    public float SplashRadius(Culture c)
    {
        if (c != Culture.Greek) return 0f;
        int lv = Level(Culture.Greek);
        if (lv < 2) return 0f;
        return lv >= 3 ? greekSplashRadius * greekHugeRadiusMult : greekSplashRadius;
    }

    /// <summary>범위 피해가 본체 피해의 몇 배로 들어가는지.</summary>
    public float SplashFraction(Culture c)
    {
        if (c != Culture.Greek) return 0f;
        return Level(Culture.Greek) >= 3 ? greekHugeFraction : greekSplashFraction;
    }

    // ── 북유럽 ────────────────────────────────

    public float AttackRateMult(Culture c)
    {
        if (c != Culture.Norse || Level(Culture.Norse) < 1) return 1f;
        float rate = Level(Culture.Norse) >= 3 ? norseHugeStackRate : norseStackRate;
        return 1f + norseStacks * rate;
    }

    public int NorseStacks => norseStacks;

    public void OnKill()
    {
        int lv = Level(Culture.Norse);
        if (lv < 1) return;

        int max = lv >= 3 ? norseMaxStackHuge
                : lv >= 2 ? norseMaxStackBig
                          : norseMaxStackSmall;

        if (norseStacks < max) norseStacks++;
    }

    /// <summary>
    /// 라운드가 끝나면 중첩이 풀린다. ★★에서만 라운드를 넘어 유지된다.
    /// 그래야 중첩이 쌓였다 빠지는 자원이 되고, ★→★★가 질적 변화가 된다.
    /// </summary>
    public void OnRoundEnd()
    {
        if (Level(Culture.Norse) < 3) norseStacks = 0;
    }

    // ── 한국 ──────────────────────────────────

    public float CritChance(Culture c)
    {
        if (c != Culture.Korean) return 0f;
        int lv = Level(Culture.Korean);
        if (lv < 1) return 0f;
        return lv >= 3 ? koreanCritChance + koreanHugeCritChance : koreanCritChance;
    }

    public float CritMult(Culture c)
    {
        if (c != Culture.Korean) return koreanCritMult;
        return Level(Culture.Korean) >= 3 ? koreanHugeCritMult : koreanCritMult;
    }

    /// <summary>★ — 치명타가 터진 적이 역주행한다.</summary>
    public float ConfuseTime(Culture c)
    {
        if (c != Culture.Korean || Level(Culture.Korean) < 2) return 0f;
        return koreanConfuseTime;
    }

    // ── 표시용 ────────────────────────────────

    public string Summary()
    {
        return $"그리스 {Count(Culture.Greek)}{Mark(Culture.Greek)}{Over(Culture.Greek)}  "
             + $"북유럽 {Count(Culture.Norse)}{Mark(Culture.Norse)}"
             + (Level(Culture.Norse) >= 1 ? $"[+{norseStacks}]" : "") + Over(Culture.Norse) + "  "
             + $"한국 {Count(Culture.Korean)}{Mark(Culture.Korean)}{Over(Culture.Korean)}";
    }

    string Mark(Culture c)
    {
        int lv = Level(c);
        return lv >= 3 ? "★★" : lv == 2 ? "★" : lv == 1 ? "☆" : "";
    }

    string Over(Culture c)
    {
        int n = Overflow(c);
        return n > 0 ? $"+{Mathf.RoundToInt(n * overflowDamageBonus * 100f)}%" : "";
    }
}

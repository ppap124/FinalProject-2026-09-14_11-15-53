using UnityEngine;

public enum UnitType
{
    // 1단계 — 인간. 문화권 없음
    Warrior, Archer, Priest,

    // 2단계 — 그리스 / 북유럽 / 한국
    Minotaur, Harpy, Oracle,
    Berserker, Valkyrie, RuneWitch,
    Dokkaebi, Dosa, Gumiho,

    // 3단계 — 신 이전의 거대한 존재
    Titan,      // 티탄   (그리스)
    Jotunn,     // 요툰   (북유럽)
    Imugi,      // 이무기 (한국)

    // 4단계 — 신
    Zeus,       // 제우스 (그리스)
    Odin,       // 오딘   (북유럽)
    Hwanung     // 환웅   (한국)
}

/// <summary>
/// 유닛 수치표. **밸런싱은 이 파일 하나만 고친다.**
///
/// 압축 배수 — 조합하면 반드시 이득이어야 한다:
///   2단계 = 1단계 x2 의 약 2.25배
///   3단계 = 2단계 x3 의 약 1.85배
///   4단계 = 3단계 x2 의 약 3배
/// </summary>
public static class UnitTable
{
    public struct Stats
    {
        public string name;
        public int tier;
        public Culture culture;

        public float damage;
        public float attackRate;
        public float range;

        public float slowChance;
        public float slowAmount;
        public float slowDuration;

        public Color color;

        public float Dps => damage * attackRate;
    }

    public static Stats Get(UnitType t)
    {
        switch (t)
        {
            // ── 1단계 ──────────────────────────────
            case UnitType.Warrior:
                return Make("전사", 1, Culture.None, 20f, 1.0f, 7f, 0f, 0f, 0f,
                            new Color(0.85f, 0.35f, 0.25f));
            case UnitType.Archer:
                return Make("궁수", 1, Culture.None, 6f, 2.5f, 14f, 0f, 0f, 0f,
                            new Color(0.30f, 0.70f, 0.35f));
            case UnitType.Priest:
                return Make("사제", 1, Culture.None, 4f, 1.0f, 12f, 0.20f, 0.15f, 3f,
                            new Color(0.55f, 0.45f, 0.85f));

            // ── 2단계 그리스 — 물리 · 광역 · 체급 ──
            case UnitType.Minotaur:
                return Make("미노타우로스", 2, Culture.Greek, 60f, 1.5f, 8f, 0f, 0f, 0f,
                            new Color(0.80f, 0.55f, 0.25f));
            case UnitType.Harpy:
                return Make("하피", 2, Culture.Greek, 12f, 5.5f, 15f, 0f, 0f, 0f,
                            new Color(0.95f, 0.80f, 0.40f));
            case UnitType.Oracle:
                return Make("오라클", 2, Culture.Greek, 9f, 2.0f, 14f, 0.40f, 0.30f, 3f,
                            new Color(0.95f, 0.90f, 0.70f));

            // ── 2단계 북유럽 — 공속 · 광폭 ─────────
            case UnitType.Berserker:
                return Make("베르세르크", 2, Culture.Norse, 25f, 3.6f, 8f, 0f, 0f, 0f,
                            new Color(0.35f, 0.55f, 0.80f));
            case UnitType.Valkyrie:
                return Make("발키리", 2, Culture.Norse, 45f, 1.5f, 16f, 0f, 0f, 0f,
                            new Color(0.60f, 0.80f, 0.95f));
            case UnitType.RuneWitch:
                return Make("룬 마녀", 2, Culture.Norse, 6f, 3.0f, 14f, 0.50f, 0.25f, 3f,
                            new Color(0.45f, 0.65f, 0.90f));

            // ── 2단계 한국 — 한방 · 홀림 ───────────
            case UnitType.Dokkaebi:
                return Make("도깨비", 2, Culture.Korean, 90f, 1.0f, 7f, 0f, 0f, 0f,
                            new Color(0.85f, 0.30f, 0.35f));
            case UnitType.Dosa:
                return Make("도사", 2, Culture.Korean, 22f, 3.0f, 15f, 0f, 0f, 0f,
                            new Color(0.55f, 0.25f, 0.30f));
            case UnitType.Gumiho:
                return Make("구미호", 2, Culture.Korean, 36f, 0.5f, 13f, 0.60f, 0.35f, 3f,
                            new Color(0.95f, 0.55f, 0.65f));

            // ── 3단계 — 거대한 존재 ────────────────
            case UnitType.Titan:
                return Make("티탄", 3, Culture.Greek, 215f, 1.5f, 10f, 0f, 0f, 0f,
                            new Color(0.70f, 0.50f, 0.20f));
            case UnitType.Jotunn:
                return Make("요툰", 3, Culture.Norse, 90f, 3.6f, 10f, 0f, 0f, 0f,
                            new Color(0.30f, 0.45f, 0.75f));
            case UnitType.Imugi:
                return Make("이무기", 3, Culture.Korean, 640f, 0.5f, 14f, 0.60f, 0.40f, 3f,
                            new Color(0.75f, 0.20f, 0.30f));

            // ── 4단계 — 신 ─────────────────────────
            case UnitType.Zeus:
                return Make("제우스", 4, Culture.Greek, 400f, 5.0f, 18f, 0f, 0f, 0f,
                            new Color(1.00f, 0.95f, 0.55f));
            case UnitType.Odin:
                return Make("오딘", 4, Culture.Norse, 1000f, 2.0f, 20f, 0f, 0f, 0f,
                            new Color(0.75f, 0.90f, 1.00f));
            default: // Hwanung
                return Make("환웅", 4, Culture.Korean, 2000f, 1.0f, 16f, 0.80f, 0.50f, 3f,
                            new Color(1.00f, 0.45f, 0.40f));
        }
    }

    static Stats Make(string name, int tier, Culture culture,
                      float damage, float rate, float range,
                      float slowChance, float slowAmount, float slowDuration,
                      Color color)
    {
        return new Stats
        {
            name = name, tier = tier, culture = culture,
            damage = damage, attackRate = rate, range = range,
            slowChance = slowChance, slowAmount = slowAmount, slowDuration = slowDuration,
            color = color
        };
    }

    // ── 조합표 ──────────────────────────────────

    /// <summary>1단계 x2 + 문화권 재료 → 2단계</summary>
    public static bool TryCombine(UnitType baseType, Culture material, out UnitType result)
    {
        result = baseType;
        if (Get(baseType).tier != 1 || material == Culture.None) return false;

        int offset = baseType == UnitType.Warrior ? 0
                   : baseType == UnitType.Archer ? 1 : 2;

        int cultureBase = material == Culture.Greek ? (int)UnitType.Minotaur
                        : material == Culture.Norse ? (int)UnitType.Berserker
                        : (int)UnitType.Dokkaebi;

        result = (UnitType)(cultureBase + offset);
        return true;
    }

    /// <summary>같은 문화권 2단계 3종 전부 → 3단계 (재료 없음)</summary>
    public static UnitType Tier3Of(Culture c)
    {
        return c == Culture.Greek ? UnitType.Titan
             : c == Culture.Norse ? UnitType.Jotunn
             : UnitType.Imugi;
    }

    /// <summary>같은 문화권 3단계 x2 → 4단계 (재료 없음)</summary>
    public static UnitType Tier4Of(Culture c)
    {
        return c == Culture.Greek ? UnitType.Zeus
             : c == Culture.Norse ? UnitType.Odin
             : UnitType.Hwanung;
    }

    /// <summary>해당 문화권의 2단계 3종</summary>
    public static UnitType[] Tier2Of(Culture c)
    {
        int b = c == Culture.Greek ? (int)UnitType.Minotaur
              : c == Culture.Norse ? (int)UnitType.Berserker
              : (int)UnitType.Dokkaebi;

        return new[] { (UnitType)b, (UnitType)(b + 1), (UnitType)(b + 2) };
    }

    static System.Collections.Generic.Dictionary<int, UnitType[]> tierCache;

    /// <summary>
    /// 그 단계의 유닛 전부. **열거형을 훑어서 만든다** — 유닛을 추가해도
    /// 여기를 안 고쳐도 되도록. 예전에는 열거형 번호가 박혀 있어서
    /// 종류를 하나 끼워넣으면 뽑기와 보스 보상이 조용히 깨졌다.
    /// </summary>
    public static UnitType[] OfTier(int tier)
    {
        if (tierCache == null)
        {
            tierCache = new System.Collections.Generic.Dictionary<int, UnitType[]>();

            var byTier = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<UnitType>>();
            foreach (UnitType t in System.Enum.GetValues(typeof(UnitType)))
            {
                int k = Get(t).tier;
                if (!byTier.ContainsKey(k)) byTier[k] = new System.Collections.Generic.List<UnitType>();
                byTier[k].Add(t);
            }
            foreach (var kv in byTier) tierCache[kv.Key] = kv.Value.ToArray();
        }

        UnitType[] arr;
        return tierCache.TryGetValue(tier, out arr) ? arr : new UnitType[0];
    }

    /// <summary>그 단계 + 그 문화권의 유닛 전부.</summary>
    public static UnitType[] OfTier(int tier, Culture c)
    {
        var list = new System.Collections.Generic.List<UnitType>();
        foreach (UnitType t in OfTier(tier))
            if (Get(t).culture == c) list.Add(t);
        return list.ToArray();
    }

    public static UnitType RandomTier1()
    {
        return RandomOfTier(1);
    }

    /// <summary>보스 보상용 — 해당 단계에서 랜덤 하나</summary>
    public static UnitType RandomOfTier(int tier)
    {
        UnitType[] arr = OfTier(tier);
        if (arr.Length == 0) arr = OfTier(1);
        if (arr.Length == 0) return UnitType.Warrior;
        return arr[UnityEngine.Random.Range(0, arr.Length)];
    }
}

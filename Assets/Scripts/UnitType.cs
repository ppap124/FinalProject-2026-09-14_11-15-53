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
    Hwanung,    // 환웅   (한국)

    // 히든 — 조합표에 없는 조합 (서로 다른 1단계 둘 + 재료). 문화권이 없어 시너지를 안 받고,
    // 더 조합할 수 없는 막다른 유닛이다. **반드시 맨 뒤에 둔다** — 앞 번호를 계산에 쓰는 곳이 있다
    // 재료 유닛의 성격을 잇는다 — 전사(무예) · 궁수(활) · 사제(신앙)
    Chiron,     // 케이론     (전사 + 궁수 + 그리스 재료) — 영웅들에게 무예와 활을 가르친 켄타우로스
    Einherjar,  // 에인헤랴르 (전사 + 사제 + 북유럽 재료) — 오딘이 골라 발할라로 데려간 전사
    Jumong,     // 주몽       (궁수 + 사제 + 한국 재료) — 해모수의 아들, 신궁

    // 히든 2차 — 짝 셋 × 재료 셋 = 9칸을 채운다. "모든 짝 + 재료에 무언가 숨어 있다"
    Sigurd,     // 시구르드   (전사 + 궁수 + 북유럽 재료) — 용 파프니르를 벤 영웅
    Hwarang,    // 화랑       (전사 + 궁수 + 한국 재료)   — 신라의 무예 · 궁술 청년
    Heracles,   // 헤라클레스 (전사 + 사제 + 그리스 재료) — 제우스의 피를 받은 장사
    Gangnim,    // 강림도령   (전사 + 사제 + 한국 재료)   — 염라대왕의 저승차사
    Odysseus,   // 오디세우스 (궁수 + 사제 + 그리스 재료) — 꾀 많은 영웅, 열두 도끼를 꿴 활
    Ullr        // 울르       (궁수 + 사제 + 북유럽 재료) — 활과 겨울의 신
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

        /// <summary>히든 유닛 — 조합표 · 뽑기 · 보스 보상에 안 나오고, 더 조합되지 않는다</summary>
        public bool hidden;

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

            // ── 히든 — 3단계 덩치, 문화권 없음(시너지 없음), 2단계 둘보다 조금 센 값 ──
            //    값은 1단계 둘 + 재료 하나라는 싼 값에 비해 강하다 — 찾아낸 보상이다.
            //    대신 시너지를 못 받고 더 올라갈 데가 없다
            // 케이론 — 전사 + 궁수: 빠르게 연달아 쏘는 궁수. 스승이라 가르친 둘의 장점을 겸한다
            case UnitType.Chiron:
                return Hide(Make("케이론", 3, Culture.None, 48f, 3.2f, 19f, 0f, 0f, 0f,
                                 new Color(0.85f, 0.70f, 0.45f)));
            // 에인헤랴르 — 전사 + 사제: 신의 축복을 받은 근접 전사
            case UnitType.Einherjar:
                return Hide(Make("에인헤랴르", 3, Culture.None, 80f, 2.0f, 9f, 0f, 0f, 0f,
                                 new Color(0.55f, 0.65f, 0.85f)));
            // 주몽 — 궁수 + 사제: 가장 멀리서 한 발씩 무겁게 — 신궁
            case UnitType.Jumong:
                return Hide(Make("주몽", 3, Culture.None, 120f, 1.2f, 22f, 0f, 0f, 0f,
                                 new Color(0.90f, 0.40f, 0.35f)));

            // 히든 2차 — DPS 는 1차와 같게 140~155. 전사 짝(무예)은 근접, 궁수가 섞이면 원거리
            case UnitType.Sigurd:
                return Hide(Make("시구르드", 3, Culture.None, 80f, 1.8f, 9f, 0f, 0f, 0f,
                                 new Color(0.75f, 0.30f, 0.25f)));
            case UnitType.Hwarang:
                return Hide(Make("화랑", 3, Culture.None, 42f, 3.4f, 17f, 0f, 0f, 0f,
                                 new Color(0.95f, 0.45f, 0.50f)));
            case UnitType.Heracles:
                return Hide(Make("헤라클레스", 3, Culture.None, 100f, 1.5f, 8f, 0f, 0f, 0f,
                                 new Color(0.85f, 0.65f, 0.30f)));
            case UnitType.Gangnim:
                return Hide(Make("강림도령", 3, Culture.None, 70f, 2.2f, 9f, 0f, 0f, 0f,
                                 new Color(0.45f, 0.40f, 0.55f)));
            case UnitType.Odysseus:
                return Hide(Make("오디세우스", 3, Culture.None, 115f, 1.25f, 21f, 0f, 0f, 0f,
                                 new Color(0.40f, 0.60f, 0.85f)));
            case UnitType.Ullr:
                return Hide(Make("울르", 3, Culture.None, 50f, 3.0f, 18f, 0f, 0f, 0f,
                                 new Color(0.70f, 0.85f, 0.95f)));

            default: // Hwanung
                return Make("환웅", 4, Culture.Korean, 2000f, 1.0f, 16f, 0.80f, 0.50f, 3f,
                            new Color(1.00f, 0.45f, 0.40f));
        }
    }

    static Stats Hide(Stats s) { s.hidden = true; return s; }

    // ── 히든 조합 ───────────────────────────────

    public struct Hidden
    {
        public UnitType a, b;       // 서로 다른 1단계 둘 (순서 무관)
        public Culture material;    // 재료 하나
        public UnitType result;
        /// <summary>도감 힌트 — 못 찾은 히든 칸에 뜬다. 짝(칼 · 활 · 기도)과 재료(신들의 음식 · 새겨진 돌 · 용의 구슬)를 수수께끼로</summary>
        public string hint;
    }

    /// <summary>
    /// 조합표에 없는 조합 (기획 §23 · §60). 정규 2단계는 **같은** 1단계 둘 + 재료라서,
    /// **다른** 1단계 둘 + 재료가 비어 있다 — 그 자리가 히든이다. 짝 셋 × 재료 셋 = 9칸 모두 찼다.
    /// 순서는 도감 표 순서 — 짝(전사+궁수 · 전사+사제 · 궁수+사제) × 재료(그리스 · 북유럽 · 한국)
    /// </summary>
    public static readonly Hidden[] Hiddens =
    {
        H(UnitType.Warrior, UnitType.Archer, Culture.Greek,  UnitType.Chiron,    "칼과 활이 신들의 음식을 나눠 먹으면, 둘을 모두 가르친 스승이 온다"),
        H(UnitType.Warrior, UnitType.Archer, Culture.Norse,  UnitType.Sigurd,    "칼과 활이 새겨진 돌 앞에 서면, 용을 벤 영웅이 깨어난다"),
        H(UnitType.Warrior, UnitType.Archer, Culture.Korean, UnitType.Hwarang,   "칼과 활이 용의 구슬을 품으면, 꽃처럼 젊은 무사가 모인다"),
        H(UnitType.Warrior, UnitType.Priest, Culture.Greek,  UnitType.Heracles,  "칼과 기도가 신들의 음식을 받으면, 신의 피를 이은 장사가 태어난다"),
        H(UnitType.Warrior, UnitType.Priest, Culture.Norse,  UnitType.Einherjar, "칼과 기도가 새겨진 돌에 닿으면, 발할라가 전사를 부른다"),
        H(UnitType.Warrior, UnitType.Priest, Culture.Korean, UnitType.Gangnim,   "칼과 기도가 용의 구슬에 비치면, 저승의 차사가 명부를 편다"),
        H(UnitType.Archer,  UnitType.Priest, Culture.Greek,  UnitType.Odysseus,  "활과 기도가 신들의 음식을 맛보면, 꾀 많은 영웅이 돌아온다"),
        H(UnitType.Archer,  UnitType.Priest, Culture.Norse,  UnitType.Ullr,      "활과 기도가 새겨진 돌을 두드리면, 겨울의 사냥꾼이 시위를 당긴다"),
        H(UnitType.Archer,  UnitType.Priest, Culture.Korean, UnitType.Jumong,    "활과 기도가 용의 구슬을 얻으면, 천제의 손자가 태어난다"),
    };

    static Hidden H(UnitType a, UnitType b, Culture m, UnitType r, string hint)
    {
        return new Hidden { a = a, b = b, material = m, result = r, hint = hint };
    }

    /// <summary>두 유닛이 히든 짝인가 (재료는 안 본다). 순서는 상관없다</summary>
    public static bool IsHiddenPair(UnitType x, UnitType y)
    {
        foreach (Hidden c in Hiddens)
            if ((c.a == x && c.b == y) || (c.a == y && c.b == x)) return true;
        return false;
    }

    /// <summary>두 유닛 + 재료가 만드는 히든. 순서는 상관없다</summary>
    public static bool TryHidden(UnitType x, UnitType y, Culture material, out Hidden h)
    {
        foreach (Hidden c in Hiddens)
            if (c.material == material && ((c.a == x && c.b == y) || (c.a == y && c.b == x))) { h = c; return true; }
        h = default(Hidden);
        return false;
    }

    /// <summary>이 히든의 조합식</summary>
    public static bool RecipeOf(UnitType result, out Hidden h)
    {
        foreach (Hidden c in Hiddens) if (c.result == result) { h = c; return true; }
        h = default(Hidden);
        return false;
    }

    /// <summary>찾아낸 적이 있는가 — 판을 넘어 남는다 (한 번 찾으면 이름이 보인다)</summary>
    public static bool Discovered(UnitType t) => PlayerPrefs.GetInt("Genesis.Hidden." + t, 0) == 1;
    public static void Discover(UnitType t) { PlayerPrefs.SetInt("Genesis.Hidden." + t, 1); PlayerPrefs.Save(); }

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
                if (Get(t).hidden) continue;   // 히든은 단계 목록(뽑기 · 보스 보상 · 조합표)에 없다
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

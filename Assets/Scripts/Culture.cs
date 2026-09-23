using UnityEngine;

public enum Culture
{
    None,     // 1단계 인간 — 아직 문화권 없음
    Greek,    // 그리스 — 영웅과 괴물. 몸으로 싸운다
    Norse,    // 북유럽 — 전쟁과 광폭. 끝을 향해 달린다
    Korean    // 한국 — 둔갑과 홀림. 속이고 홀린다
}

/// <summary>문화권 재료. 각 신화에서 진화·신성을 상징하는 물건.</summary>
public static class MaterialTable
{
    public static readonly Culture[] All =
    {
        Culture.Greek, Culture.Norse, Culture.Korean
    };

    public static string Name(Culture c)
    {
        switch (c)
        {
            case Culture.Greek:  return "암브로시아";   // 신들의 음식. 먹으면 불사
            case Culture.Norse:  return "룬석";         // 룬이 새겨진 돌. 힘의 원천
            case Culture.Korean: return "여의주";       // 이무기를 용으로 만드는 구슬
            default: return "-";
        }
    }

    public static string CultureName(Culture c)
    {
        switch (c)
        {
            case Culture.Greek:  return "그리스";
            case Culture.Norse:  return "북유럽";
            case Culture.Korean: return "한국";
            default: return "인간";
        }
    }

    public static Color Color(Culture c)
    {
        switch (c)
        {
            case Culture.Greek:  return new Color(0.95f, 0.80f, 0.35f); // 금빛
            case Culture.Norse:  return new Color(0.45f, 0.70f, 0.90f); // 서리빛
            case Culture.Korean: return new Color(0.85f, 0.35f, 0.45f); // 단청 붉은빛
            default: return UnityEngine.Color.gray;
        }
    }

    public static Culture Random()
    {
        return All[UnityEngine.Random.Range(0, All.Length)];
    }
}
